using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Consumes only the committed player-shot presentation feed and turns its
    /// frozen trajectories into short-lived muzzle, tracer and secondary-area
    /// feedback. It never performs Physics queries or writes battle state.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponPresentationController : MonoBehaviour
    {
        private const int DefaultTracerCapacity = 32;
        private const int DefaultTargetBurstCapacity = 4;
        private const int DefaultEventBufferCapacity = 64;
        private const int DefaultActorTraceBufferCapacity = 64;
        private static readonly Color MissColor = new Color(0.62f, 0.84f, 1f, 0.48f);
        private static readonly Color BlockerColor = new Color(1f, 0.67f, 0.2f, 0.9f);
        private static readonly Color BodyHitColor = new Color(0.42f, 0.9f, 1f, 0.96f);
        private static readonly Color WeakpointHitColor = new Color(1f, 0.9f, 0.22f, 1f);
        private static readonly Color ProjectileHitColor = new Color(0.32f, 1f, 0.92f, 1f);

        private BattleSessionHost sessionHost;
        private D0PlayerEntityView playerEntity;
        private D0WeaponDefinition weaponDefinition;

        [Header("Presentation structure")]
        [SerializeField]
        private Transform shotViewRoot;

        private Camera presentationCamera;
        private Actor2DPresenter actorPresenter;

        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [SerializeField]
        private Material shotMaterial;

        [Header("Fixed presentation capacity")]
        [SerializeField, Min(1)]
        private int tracerCapacity = DefaultTracerCapacity;

        [SerializeField, Min(1)]
        private int areaCapacity = DefaultTargetBurstCapacity;

        private readonly PlayerShotPresentationCursor shotCursor = new PlayerShotPresentationCursor();
        private readonly CombatTraceCursor actorTraceCursor = new CombatTraceCursor();

        private PlayerShotTracerPool tracerPool;
        private PlayerShotTargetBurstPool targetBurstPool;
        private PlayerMuzzleFlashView muzzleFlash;
        private D0SecondaryChargeView secondaryChargeView;
        private PlayerShotPresentationEvent[] eventBuffer;
        private CombatEvent[] actorTraceBuffer;
        private IPlayerShotPresentationFeed boundFeed;
        private bool initialized;
        private bool skipRetainedEventsOnNextBind;

        public BattleSessionHost SessionHost => sessionHost;
        public bool IsSceneServicesBound => sessionHost != null && presentationCamera != null;
        public D0PlayerEntityView PlayerEntity => playerEntity;
        public D0WeaponDefinition WeaponDefinition => weaponDefinition;
        public D0ActorSocketRegistry SocketRegistry =>
            playerEntity == null ? null : playerEntity.SocketRegistry;
        public Transform ShotViewRoot => shotViewRoot;
        public Camera PresentationCamera => presentationCamera;
        public Actor2DPresenter ActorPresenter => actorPresenter;
        public CombatPresentationProfile PresentationProfile => presentationProfile;
        public Material ShotMaterial => shotMaterial;
        public bool IsInitialized => initialized;
        public int TracerCapacity => tracerCapacity;
        /// <summary>
        /// Kept for scene compatibility with the former secondary-area pool.
        /// It now describes target-local burst capacity; no D0 secondary may
        /// create a ground area ring.
        /// </summary>
        public int AreaCapacity => areaCapacity;
        public int TargetBurstCapacity => areaCapacity;
        public int ActiveTracerCount => tracerPool == null ? 0 : tracerPool.ActiveCount;
        public int ActiveAreaCount => 0;
        public int ActiveTargetBurstCount => targetBurstPool == null ? 0 : targetBurstPool.ActiveCount;
        public int ActiveSecondaryChargeVisualCount =>
            secondaryChargeView != null && secondaryChargeView.IsActive ? 1 : 0;
        public bool IsSecondaryChargeVisualActive =>
            secondaryChargeView != null && secondaryChargeView.IsActive;
        public int SecondaryHitMarkerCount => secondaryChargeView == null
            ? 0
            : secondaryChargeView.HitMarkerCount;
        public int SecondaryStopMarkerCount => secondaryChargeView == null
            ? 0
            : secondaryChargeView.StopMarkerCount;
        public int TracerPoolRejectCount => tracerPool == null ? 0 : tracerPool.SpawnRejectCount;
        public int AreaPoolRejectCount => targetBurstPool == null ? 0 : targetBurstPool.SpawnRejectCount;
        public int ShotFeedGapCount { get; private set; }
        public int PresentationFaultCount { get; private set; }
        public int PresentedShotCount { get; private set; }
        public IPlayerShotPresentationFeed BoundFeed => boundFeed;

        /// <summary>
        /// Injects scene-owned session and camera services. These references are
        /// never part of the complete player Entity Prefab authoring contract.
        /// </summary>
        public bool TryBindSceneServices(
            BattleSessionHost nextSessionHost,
            Camera nextPresentationCamera,
            out string error)
        {
            if (nextSessionHost == null)
            {
                error = "Player weapon scene services require a BattleSessionHost.";
                return false;
            }

            if (nextPresentationCamera == null)
            {
                error = "Player weapon scene services require a presentation Camera.";
                return false;
            }

            if (sessionHost == nextSessionHost && presentationCamera == nextPresentationCamera)
            {
                if (isActiveAndEnabled)
                {
                    SubscribeToHostRestart();
                }

                error = string.Empty;
                return true;
            }

            if (initialized
                && !TryApplyPresentationCamera(nextPresentationCamera, out error))
            {
                return false;
            }

            UnsubscribeFromHostRestart();
            UnbindAndClear();
            sessionHost = nextSessionHost;
            presentationCamera = nextPresentationCamera;
            if (isActiveAndEnabled)
            {
                SubscribeToHostRestart();
            }

            error = string.Empty;
            return true;
        }

        public void UnbindSceneServices()
        {
            UnsubscribeFromHostRestart();
            sessionHost = null;
            presentationCamera = null;
            UnbindAndClear();
            ClearPlayerEntityBinding();
            if (initialized)
            {
                TryApplyPresentationCamera(null, out _);
                skipRetainedEventsOnNextBind = true;
            }
        }

        /// <summary>
        /// Binds the active complete player Entity Prefab and its weapon-owned
        /// presentation. The entity socket registry is the authoritative
        /// source; legacy scene sockets remain only as an unbound fallback.
        /// </summary>
        public bool TryBindPlayerEntity(
            D0PlayerEntityView nextPlayerEntity,
            D0WeaponDefinition nextWeaponDefinition,
            out string error)
        {
            if (nextPlayerEntity == null || nextWeaponDefinition == null)
            {
                error =
                    "Player weapon presentation requires a player Entity and weapon definition.";
                return false;
            }

            if (!nextWeaponDefinition.TryValidatePresentation(out error))
            {
                return false;
            }

            D0ActorSocketRegistry registry =
                nextPlayerEntity.SocketRegistry;
            if (registry == null || !registry.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Player Entity requires a valid actor socket registry.";
                }

                return false;
            }

            D0WeaponShotPresentationDefinition primary =
                nextWeaponDefinition.PrimaryPresentation;
            D0WeaponSecondaryPresentationDefinition secondary =
                nextWeaponDefinition.SecondaryPresentation;
            if (!registry.TryResolve(primary.SocketId, out _)
                || !registry.TryResolve(secondary.Shot.SocketId, out _))
            {
                error =
                    "Player Entity socket registry cannot resolve every weapon source socket.";
                return false;
            }

            Actor2DPresenter nextActorPresenter =
                nextPlayerEntity.ActorPresenter;
            if (nextActorPresenter == null
                || !nextActorPresenter.TrySetRuntimeWeaponDefinition(
                    nextWeaponDefinition,
                    out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Player Entity requires an Actor2DPresenter for weapon actions.";
                }

                return false;
            }

            playerEntity = nextPlayerEntity;
            weaponDefinition = nextWeaponDefinition;
            actorPresenter = nextActorPresenter;
            if (initialized)
            {
                ClearPresentation();
            }
            else if (isActiveAndEnabled
                && IsSceneServicesBound
                && !TryInitialize(out error))
            {
                ClearPlayerEntityBinding();
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void ClearPlayerEntityBinding()
        {
            if (actorPresenter != null)
            {
                actorPresenter.TrySetRuntimeWeaponDefinition(null, out _);
            }

            playerEntity = null;
            weaponDefinition = null;
            actorPresenter = null;
            if (initialized)
            {
                ClearPresentation();
            }
        }

        /// <summary>
        /// Validates only authoring-time dependencies. Feed availability is a
        /// runtime concern because BattleSessionHost creates a new feed when it
        /// starts or restarts a session.
        /// </summary>
        public bool TryValidateAuthoring(out string error)
        {
            error = string.Empty;
            if (shotViewRoot == null)
            {
                error =
                    "PlayerWeaponPresentationController requires a PlayerShotViews root.";
                return false;
            }

            if (shotMaterial == null)
            {
                error =
                    "PlayerWeaponPresentationController requires a transparent shot material.";
                return false;
            }

            if (tracerCapacity <= 0 || areaCapacity <= 0)
            {
                error =
                    "PlayerWeaponPresentationController capacities must be positive.";
                return false;
            }

            if (presentationProfile == null
                || !presentationProfile.TryValidateStatic(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Player weapon presentation requires the global presentation profile.";
                }

                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!TryValidateAuthoring(out error))
            {
                return false;
            }

            if (playerEntity == null
                || weaponDefinition == null
                || actorPresenter == null)
            {
                error =
                    "Player weapon presentation requires runtime-bound Entity, weapon and Actor2DPresenter references.";
                return false;
            }

            if (!weaponDefinition.TryValidatePresentation(out error))
            {
                return false;
            }

            D0ActorSocketRegistry registry = playerEntity.SocketRegistry;
            D0WeaponSecondaryPresentationDefinition secondary =
                weaponDefinition.SecondaryPresentation;
            if (registry == null
                || !registry.TryValidate(out error)
                || !registry.TryResolve(
                    weaponDefinition.PrimaryPresentation.SocketId,
                    out _)
                || !registry.TryResolve(
                    secondary.Shot.SocketId,
                    out _))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error =
                        "Player Entity socket registry cannot resolve every weapon source socket.";
                }

                return false;
            }

            return actorPresenter.TryValidate(out error);
        }

        public bool TryInitialize(out string error)
        {
            if (initialized)
            {
                error = string.Empty;
                return true;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            PlayerShotTracerPool nextTracerPool = new PlayerShotTracerPool();
            PlayerShotTargetBurstPool nextTargetBurstPool = new PlayerShotTargetBurstPool();
            GameObject muzzleObject = null;
            GameObject secondaryChargeObject = null;
            try
            {
                if (!nextTracerPool.TryPrepare(
                        shotViewRoot,
                        shotMaterial,
                        tracerCapacity,
                        ResolveWorldEffectsSortingLayerName(),
                        ResolveWorldEffectsSortingOrder(),
                        out error))
                {
                    return false;
                }

                if (!nextTargetBurstPool.TryPrepare(
                        shotViewRoot,
                        shotMaterial,
                        presentationCamera,
                        areaCapacity,
                        ResolveWorldEffectsSortingLayerName(),
                        ResolveWorldEffectsSortingOrder(),
                        out error))
                {
                    return false;
                }

                muzzleObject = new GameObject("PlayerMuzzleFlash");
                muzzleObject.transform.SetParent(shotViewRoot, false);
                PlayerMuzzleFlashView nextMuzzleFlash = muzzleObject.AddComponent<PlayerMuzzleFlashView>();
                if (!nextMuzzleFlash.TryPrepare(shotMaterial, out error))
                {
                    return false;
                }

                secondaryChargeObject = new GameObject("D0SecondaryChargeVisual");
                secondaryChargeObject.transform.SetParent(shotViewRoot, false);
                D0SecondaryChargeView nextSecondaryChargeView =
                    secondaryChargeObject.AddComponent<D0SecondaryChargeView>();
                if (!nextSecondaryChargeView.TryPrepare(
                        shotMaterial,
                        presentationCamera,
                        ResolveWorldEffectsSortingLayerName(),
                        ResolveWorldEffectsSortingOrder(),
                        out error))
                {
                    return false;
                }

                tracerPool = nextTracerPool;
                targetBurstPool = nextTargetBurstPool;
                muzzleFlash = nextMuzzleFlash;
                secondaryChargeView = nextSecondaryChargeView;
                eventBuffer = new PlayerShotPresentationEvent[DefaultEventBufferCapacity];
                actorTraceBuffer = new CombatEvent[DefaultActorTraceBufferCapacity];
                initialized = true;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Unable to prepare player weapon presentation: {exception.Message}";
                return false;
            }
            finally
            {
                if (!initialized)
                {
                    nextTracerPool.Dispose();
                    nextTargetBurstPool.Dispose();
                    DestroyObject(muzzleObject);
                    DestroyObject(secondaryChargeObject);
                }
            }
        }

        public void ClearPresentation()
        {
            tracerPool?.Clear();
            targetBurstPool?.Clear();
            muzzleFlash?.Deactivate();
            secondaryChargeView?.Clear();
        }

        private void Start()
        {
            if (playerEntity == null || weaponDefinition == null)
            {
                return;
            }

            if (!TryInitialize(out string error))
            {
                Debug.LogError(
                    $"[{nameof(PlayerWeaponPresentationController)}] {error}",
                    this);
            }
        }

        private void OnEnable()
        {
            SubscribeToHostRestart();
        }

        private void OnDisable()
        {
            UnsubscribeFromHostRestart();
            UnbindAndClear();
            // A disabled consumer must not replay short-lived tracer feedback
            // from while it was absent. Initial and restart bindings keep this
            // false so a first real shot cannot be lost between Host.Update and
            // this component's first LateUpdate.
            skipRetainedEventsOnNextBind = initialized;
        }

        private void OnDestroy()
        {
            UnbindSceneServices();
            GameObject muzzleObject = muzzleFlash == null ? null : muzzleFlash.gameObject;
            GameObject secondaryChargeObject = secondaryChargeView == null
                ? null
                : secondaryChargeView.gameObject;
            tracerPool?.Dispose();
            targetBurstPool?.Dispose();
            // The flash is a separately pooled scene child rather than a child
            // of this controller. Dispose the two array-backed pools above and
            // explicitly remove this single view so reinstalling/rebuilding the
            // controller cannot leave a hidden renderer or light behind.
            DestroyObject(muzzleObject);
            DestroyObject(secondaryChargeObject);
            tracerPool = null;
            targetBurstPool = null;
            muzzleFlash = null;
            secondaryChargeView = null;
            eventBuffer = null;
            actorTraceBuffer = null;
            initialized = false;
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            try
            {
                RefreshFeedBinding();
                BattleSession session = sessionHost == null ? null : sessionHost.Session;
                if (session != null && session.State == BattleSessionState.Paused)
                {
                    return;
                }

                if (session != null && session.State != BattleSessionState.Running)
                {
                    // A terminal session owns no transient player-shot feedback.
                    // Clear immediately rather than allowing the final committed
                    // shot to be consumed after Victory, Defeat or shutdown.
                    // Keeping the cursor at the current tail also prevents a
                    // terminal feed from being replayed if the host is rebound.
                    ClearPresentation();
                    if (boundFeed != null)
                    {
                        shotCursor.SetBaseline(boundFeed);
                    }

                    return;
                }

                float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
                tracerPool.Advance(deltaTime);
                targetBurstPool.Advance(deltaTime);
                muzzleFlash.Advance(deltaTime);
                secondaryChargeView.Advance(deltaTime);

                if (boundFeed != null)
                {
                    ConsumeCommittedShots(session);
                }

                ConsumeActorTrace(session);
            }
            catch (Exception)
            {
                PresentationFaultCount++;
            }
        }

        private void RefreshFeedBinding()
        {
            IPlayerShotPresentationFeed nextFeed = sessionHost == null
                ? null
                : sessionHost.PlayerShotPresentationFeed;
            if (ReferenceEquals(boundFeed, nextFeed))
            {
                return;
            }

            ClearPresentation();
            shotCursor.Reset();
            boundFeed = nextFeed;
            actorTraceCursor.Reset();
            if (boundFeed == null)
            {
                return;
            }

            EnsureEventBufferCapacity(boundFeed.EventCapacity);
            BattleSession session = sessionHost == null ? null : sessionHost.Session;
            EstablishActorTraceBaseline(session);
            if (skipRetainedEventsOnNextBind)
            {
                // A presentation component can enable after combat has started.
                // Do not recreate old transient shots in that case; only consume
                // events committed after this binding point.
                shotCursor.SetBaseline(boundFeed);
                skipRetainedEventsOnNextBind = false;
            }

            // The shot feed can be rebound while the authoritative weapon is
            // still charging. Its original InputAccepted trace may predate this
            // consumer, so restore only the durable charge view.
            if (session != null && session.State == BattleSessionState.Running)
            {
                ResynchronizeSecondaryChargeVisual(session);
            }
        }

        private void ConsumeCommittedShots(BattleSession session)
        {
            int eventCount = shotCursor.CopyUnread(boundFeed, eventBuffer, out bool hasGap);
            if (hasGap)
            {
                ClearPresentation();
                shotCursor.ResolveGap(boundFeed);
                ShotFeedGapCount++;
                // The paired combat trace may still retain events that predate
                // this shot-feed gap. Do not replay those short transitions
                // into a newly rebuilt view; establish a fresh trace baseline
                // and restore only the current durable weapon state.
                EstablishActorTraceBaseline(session);
                ResynchronizeSecondaryChargeVisual(session);
                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                PlayerShotPresentationEvent shotEvent = eventBuffer[index];
                try
                {
                    PresentCommittedShot(shotEvent.Snapshot);
                }
                catch (Exception)
                {
                    // A damaged or misconfigured view is a presentation fault,
                    // never a reason to retain a committed combat event forever.
                    // Drop its transient output, advance the read cursor and let
                    // later events retain their chance to render.
                    ClearPresentation();
                    PresentationFaultCount++;
                }
                finally
                {
                    shotCursor.Commit(shotEvent);
                }
            }
        }

        private void PresentCommittedShot(
            in PlayerShotPresentationSnapshot snapshot)
        {
            PresentWeaponDefinedShot(snapshot);
        }

        private void PresentWeaponDefinedShot(
            in PlayerShotPresentationSnapshot snapshot)
        {
            D0WeaponSecondaryPresentationDefinition secondaryPresentation =
                weaponDefinition.SecondaryPresentation;
            bool secondary = snapshot.ReleaseKind == WeaponReleaseKind.Secondary;
            D0WeaponShotPresentationDefinition shotPresentation = secondary
                ? secondaryPresentation.Shot
                : weaponDefinition.PrimaryPresentation;
            Transform presentationMuzzle = ResolvePresentationMuzzle(shotPresentation);
            Vector3 visualRayOrigin = presentationMuzzle.position;
            Color effectColor = shotPresentation.EffectColor;
            muzzleFlash.Activate(
                visualRayOrigin,
                presentationMuzzle.forward,
                effectColor,
                shotPresentation.MuzzleDuration,
                shotPresentation.MuzzleLength,
                shotPresentation.MuzzleWidth,
                shotPresentation.MuzzleLightIntensity);

            if (secondary)
            {
                PlayerShotTrajectory directTrajectory = snapshot.GetTrajectory(0);
                tracerPool.TrySpawn(
                    visualRayOrigin,
                    ToPosition(directTrajectory.TerminalPoint),
                    effectColor,
                    shotPresentation.TracerDuration,
                    shotPresentation.TracerWidth,
                    directTrajectory.TerminalKind == PlayerShotTerminalKind.Miss
                        ? 0f
                        : shotPresentation.TracerEndpointLightIntensity);

                secondaryChargeView?.Release(
                    visualRayOrigin,
                    ToPosition(directTrajectory.TerminalPoint),
                    effectColor,
                    shotPresentation.TracerDuration,
                    ResolveSecondaryStopMarkerDelay());

                if (PlayerShotVisualAggregation.TryGetSecondaryBurstAnchor(
                        snapshot,
                        out SpatialVectorKey burstAnchor))
                {
                    float committedAreaRadius = snapshot.SecondaryAreaRadiusKey
                        / (float)SpatialContract.PositionUnitsPerMeter;
                    targetBurstPool.TrySpawn(
                        ToPosition(burstAnchor),
                        effectColor,
                        shotPresentation.TracerDuration,
                        Mathf.Clamp(
                            committedAreaRadius * secondaryPresentation.TargetBurstRadiusScale,
                            secondaryPresentation.TargetBurstMinRadius,
                            secondaryPresentation.TargetBurstMaxRadius));
                }
            }
            else if (PlayerShotVisualAggregation.TryGetPrimaryRepresentative(
                         snapshot,
                         out PlayerShotTrajectory representativeTrajectory))
            {
                tracerPool.TrySpawn(
                    visualRayOrigin,
                    ToPosition(representativeTrajectory.TerminalPoint),
                    GetTrajectoryColor(representativeTrajectory),
                    shotPresentation.TracerDuration,
                    shotPresentation.TracerWidth,
                    representativeTrajectory.TerminalKind == PlayerShotTerminalKind.Miss
                        ? 0f
                        : shotPresentation.TracerEndpointLightIntensity);
                actorPresenter?.PlayPrimaryAttack();
            }

            PresentedShotCount++;
        }

        private Color GetTrajectoryColor(in PlayerShotTrajectory trajectory)
        {
            switch (trajectory.TerminalKind)
            {
                case PlayerShotTerminalKind.EnvironmentBlocker:
                    return BlockerColor;
                case PlayerShotTerminalKind.Combatant:
                    return trajectory.HitPart == HitPart.Weakpoint
                        ? ResolveHitColor(CombatHitPresentationKind.Weakpoint, WeakpointHitColor)
                        : ResolveHitColor(CombatHitPresentationKind.Body, BodyHitColor);
                case PlayerShotTerminalKind.Projectile:
                    return ResolveHitColor(CombatHitPresentationKind.Intercept, ProjectileHitColor);
                default:
                    return MissColor;
            }
        }

        private void BeginSecondaryChargeVisual()
        {
            if (secondaryChargeView == null)
            {
                return;
            }

            D0WeaponSecondaryPresentationDefinition secondary =
                weaponDefinition.SecondaryPresentation;
            D0WeaponShotPresentationDefinition shot = secondary.Shot;
            Transform muzzle = ResolvePresentationMuzzle(shot);
            Vector3 source = muzzle.position;
            secondaryChargeView.BeginCharge(
                source,
                ResolveSecondaryChargeTarget(source, secondary),
                shot.EffectColor,
                secondary.ChargePulseDuration);
        }

        private Transform ResolvePresentationMuzzle(
            D0WeaponShotPresentationDefinition shotPresentation)
        {
            D0ActorSocketRegistry registry = playerEntity == null
                ? null
                : playerEntity.SocketRegistry;
            if (shotPresentation == null
                || registry == null
                || !registry.TryResolve(shotPresentation.SocketId, out Transform anchor))
            {
                throw new InvalidOperationException(
                    "Player weapon presentation cannot resolve the configured Entity socket.");
            }

            return anchor;
        }

        private Vector3 ResolveSecondaryChargeTarget(
            Vector3 source,
            D0WeaponSecondaryPresentationDefinition secondaryPresentation)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            Transform depthAnchor = null;
            switch (secondaryPresentation.TargetDepthAnchor)
            {
                case D0SkillTargetDepthAnchor.ActiveEnemyWeakpoint:
                    depthAnchor = context == null ? null : context.ActiveEnemyWeakpointAnchor;
                    break;

                case D0SkillTargetDepthAnchor.ActiveEnemyGameplay:
                    depthAnchor = context == null ? null : context.ActiveEnemyGameplayAnchor;
                    break;
            }

            Vector3 cameraForward = presentationCamera == null
                ? Vector3.forward
                : presentationCamera.transform.forward;
            Vector3 fallback = depthAnchor == null
                ? source + cameraForward * secondaryPresentation.FallbackCameraDistance
                : depthAnchor.position;
            CombatAimReticle reticle = context == null ? null : context.CombatAimReticle;
            if (presentationCamera == null
                || reticle == null
                || !reticle.TryGetViewport(out Vector2 viewport))
            {
                return fallback;
            }

            Plane visualPlane = new Plane(presentationCamera.transform.forward, fallback);
            Ray viewportRay = presentationCamera.ViewportPointToRay(
                new Vector3(viewport.x, viewport.y, 0f));
            return visualPlane.Raycast(viewportRay, out float distance)
                ? viewportRay.GetPoint(distance)
                : fallback;
        }

        private string ResolveWorldEffectsSortingLayerName()
        {
            CombatPresentationSorting sorting = presentationProfile == null
                ? null
                : presentationProfile.Sorting;
            return sorting == null || string.IsNullOrWhiteSpace(sorting.SortingLayerName)
                ? "Default"
                : sorting.SortingLayerName;
        }

        private int ResolveWorldEffectsSortingOrder()
        {
            CombatPresentationSorting sorting = presentationProfile == null
                ? null
                : presentationProfile.Sorting;
            return sorting == null ? 0 : sorting.WorldEffectsOrder;
        }

        private float ResolveSecondaryStopMarkerDelay()
        {
            D0WeaponSecondaryPresentationDefinition secondary =
                weaponDefinition.SecondaryPresentation;
            return Mathf.Max(
                0f,
                secondary.StopMarkerTime - secondary.HitMarkerTime);
        }

        private Color ResolveHitColor(
            CombatHitPresentationKind kind,
            Color fallback)
        {
            if (presentationProfile != null
                && presentationProfile.TryGetHitDefinition(kind, out CombatHitPresentationDefinition definition))
            {
                return definition.PrimaryColor;
            }

            return fallback;
        }

        private void EnsureEventBufferCapacity(int feedCapacity)
        {
            int requiredCapacity = Mathf.Max(DefaultEventBufferCapacity, feedCapacity);
            if (eventBuffer == null || eventBuffer.Length < requiredCapacity)
            {
                eventBuffer = new PlayerShotPresentationEvent[requiredCapacity];
            }
        }

        private void ConsumeActorTrace(BattleSession session)
        {
            if (session == null)
            {
                return;
            }

            EnsureActorTraceBufferCapacity(session.Trace.Capacity);
            int eventCount = actorTraceCursor.CopyUnread(
                session.Trace,
                actorTraceBuffer,
                out bool hasGap);
            if (hasGap)
            {
                actorTraceCursor.ResolveGap(session.Trace);
                ResynchronizeSecondaryChargeVisual(session);
                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                CombatEvent combatEvent = actorTraceBuffer[index];
                if (combatEvent.EventType == CombatEventType.InputAccepted
                    && combatEvent.SourceId == session.PlayerRuntimeId
                    && combatEvent.ValueBefore == (int)WeaponState.Ready
                    && combatEvent.ValueAfter == (int)WeaponState.AltCharging
                    && session.PlayerWeaponState == WeaponState.AltCharging)
                {
                    BeginSecondaryChargeVisual();
                }
                else if (combatEvent.SourceId == session.PlayerRuntimeId
                    && combatEvent.ValueBefore == (int)WeaponState.AltCharging
                    && (combatEvent.EventType == CombatEventType.AttackCanceled
                        || (combatEvent.EventType == CombatEventType.InputAccepted
                            && combatEvent.ValueAfter == (int)WeaponState.Ready)))
                {
                    secondaryChargeView?.CancelCharge();
                }
                actorTraceCursor.Commit(combatEvent);
            }
        }

        private void ResynchronizeSecondaryChargeVisual(BattleSession session)
        {
            if (session == null)
            {
                return;
            }

            if (session.PlayerWeaponState == WeaponState.AltCharging)
            {
                BeginSecondaryChargeVisual();
                return;
            }

            secondaryChargeView?.CancelCharge();
        }

        private void EstablishActorTraceBaseline(BattleSession session)
        {
            actorTraceCursor.Reset();
            if (session == null || session.Trace.Count <= 0)
            {
                return;
            }

            actorTraceCursor.Commit(session.Trace.GetOldest(session.Trace.Count - 1));
        }

        private void EnsureActorTraceBufferCapacity(int traceCapacity)
        {
            int requiredCapacity = Mathf.Max(DefaultActorTraceBufferCapacity, traceCapacity);
            if (actorTraceBuffer == null || actorTraceBuffer.Length < requiredCapacity)
            {
                actorTraceBuffer = new CombatEvent[requiredCapacity];
            }
        }

        private void HandleSessionRestarted(BattleSessionHost restartedHost)
        {
            if (restartedHost == sessionHost)
            {
                UnbindAndClear();
                actorPresenter?.ClearAndReturnToIdle();
                skipRetainedEventsOnNextBind = false;
            }
        }

        private bool TryApplyPresentationCamera(Camera nextPresentationCamera, out string error)
        {
            if (targetBurstPool != null
                && !targetBurstPool.TryPrepare(
                    shotViewRoot,
                    shotMaterial,
                    nextPresentationCamera,
                    areaCapacity,
                    ResolveWorldEffectsSortingLayerName(),
                    ResolveWorldEffectsSortingOrder(),
                    out error))
            {
                return false;
            }

            if (secondaryChargeView != null
                && !secondaryChargeView.TryPrepare(
                    shotMaterial,
                    nextPresentationCamera,
                    ResolveWorldEffectsSortingLayerName(),
                    ResolveWorldEffectsSortingOrder(),
                    out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void SubscribeToHostRestart()
        {
            if (sessionHost != null)
            {
                sessionHost.SessionRestarted -= HandleSessionRestarted;
                sessionHost.SessionRestarted += HandleSessionRestarted;
            }
        }

        private void UnsubscribeFromHostRestart()
        {
            if (sessionHost != null)
            {
                sessionHost.SessionRestarted -= HandleSessionRestarted;
            }
        }

        private void UnbindAndClear()
        {
            ClearPresentation();
            boundFeed = null;
            shotCursor.Reset();
            actorTraceCursor.Reset();
        }

        private static Vector3 ToPosition(SpatialVectorKey key)
        {
            float inverseScale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(key.X * inverseScale, key.Y * inverseScale, key.Z * inverseScale);
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
