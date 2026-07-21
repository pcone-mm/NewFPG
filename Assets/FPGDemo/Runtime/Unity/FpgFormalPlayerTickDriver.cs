using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Captures rendered-frame input into fixed buffers and consumes it only
    /// from the formal PlayerAttackAndHit tick phase. It never calls the legacy
    /// single-enemy BattleSession.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerTickDriver : MonoBehaviour, IFpgFormalPlayerTickDriver
    {
        private const ulong PlayerAttackRandomDomain = 0x4650475F504C4159UL;

        [Header("Formal Runtime")]
        [SerializeField] private FpgRoomEncounterDirector encounterDirector = null;
        [SerializeField] private Transform aimAnchor = null;
        [SerializeField]
        [Tooltip("Optional formal-room camera used to aim through the configured viewport.")]
        private Camera aimCamera = null;
        [SerializeField]
        [Tooltip("Optional scene component implementing ICombatAimViewportSource.")]
        private MonoBehaviour aimViewportSource = null;
        [SerializeField]
        [Tooltip("Optional visual recoil source removed from the deterministic aim ray.")]
        private FpgFormalPlayerCameraFeedback cameraFeedback = null;
        [SerializeField] private bool aimFromPointerPosition = false;
        [SerializeField, Min(1f)] private float aimDistance = 50f;
        [SerializeField] private LayerMask aimLayerMask = (1 << 29) | (1 << 28);
        [SerializeField] private Transform playerRoot = null;

        [Header("Input")]
        [SerializeField] private bool captureFromDevices = true;
        [SerializeField] private bool handlePauseAndRestart = true;
        [SerializeField, Range(1, 32)] private int inputBufferTicks = 8;

        private readonly RaycastHit[] aimRaycastBuffer = new RaycastHit[16];
        private readonly InputEdgeCommand[] edgeBuffer =
            new InputEdgeCommand[BattleTickInput.MaxEdgeCommandCount];
        private readonly QueryCandidate[] queryCandidates =
            new QueryCandidate[TargetSelector.DefaultCandidateCapacity];
        private readonly QueryCandidate[] selectedCandidates =
            new QueryCandidate[TargetSelector.DefaultCandidateCapacity];
        private readonly WeaponReleaseBuffer weaponRelease = new WeaponReleaseBuffer();
        private readonly ProjectWideBattleInputAdapter projectWideBattleInputAdapter =
            new ProjectWideBattleInputAdapter();
        private readonly FpgFormalPlayerPresentationSource presentationSource =
            new FpgFormalPlayerPresentationSource();

        private UnityBattleInputSource inputSource;
        private D0CharacterDefinition playerDefinition;
        private D0PlayerEntityView playerEntity;
        private D0ThreeCProfile threeCProfile;
        private TickIndex lastProcessedTick = TickIndex.Invalid;
        private long nextCommandSequence;
        private RejectReason captureFault = RejectReason.None;
        private bool playerConfigured;
        private bool runtimeObserved;

        public FpgRoomEncounterDirector EncounterDirector => encounterDirector;
        public Transform AimAnchor => aimAnchor;
        public MonoBehaviour AimViewportSourceComponent => aimViewportSource;
        public ICombatAimViewportSource AimViewportSource =>
            aimViewportSource as ICombatAimViewportSource;
        public FpgFormalPlayerCameraFeedback CameraFeedback => cameraFeedback;
        public D0CharacterDefinition PlayerDefinition => playerDefinition;
        public D0PlayerEntityView PlayerEntity => playerEntity;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public bool IsPlayerConfigured => playerConfigured;
        public bool HasCaptureFault => playerConfigured
            && captureFault != RejectReason.None;
        public TickIndex LastProcessedTick => lastProcessedTick;
        public FpgFormalPlayerPresentationSource PresentationSource =>
            presentationSource;

        public event Action<FpgFormalPlayerActionEvent> ActionCommitted
        {
            add => presentationSource.ActionCommitted += value;
            remove => presentationSource.ActionCommitted -= value;
        }

        private void Awake()
        {
            captureFault = RejectReason.None;
        }

        private void Update()
        {
            if (!playerConfigured)
            {
                return;
            }

            SetAimViewportFrozen(
                encounterDirector != null && encounterDirector.IsPaused);

            if (inputSource == null)
            {
                ResetInputSource();
            }

            if (captureFromDevices)
            {
                if (!projectWideBattleInputAdapter.TryCapture(inputSource))
                {
                    inputSource.CaptureFromDevices();
                }
            }

            if (aimAnchor == null)
            {
                captureFault = RejectReason.InvalidState;
                return;
            }

            try
            {
                CaptureAimPose();
            }
            catch (Exception)
            {
                captureFault = RejectReason.InvariantFault;
                return;
            }

            if (!handlePauseAndRestart)
            {
                return;
            }

            if (inputSource.ConsumeRestartPressed())
            {
                if (encounterDirector == null || !encounterDirector.TryRestart(out _))
                {
                    captureFault = RejectReason.InvalidState;
                }

                return;
            }

            if (inputSource.ConsumePausePressed())
            {
                bool changed = encounterDirector != null
                    && (encounterDirector.IsPaused
                        ? encounterDirector.TryResume(out _)
                        : encounterDirector.TryPause(out _));
                if (!changed)
                {
                    captureFault = RejectReason.InvalidState;
                    return;
                }

                inputSource.ClearGameplayInput();
                SetAimViewportFrozen(encounterDirector.IsPaused);
            }
            else if (encounterDirector != null && encounterDirector.IsPaused)
            {
                inputSource.ClearGameplayInput();
                SetAimViewportFrozen(true);
            }
        }

        /// <summary>
        /// One-shot composition entry. It must run before the first formal
        /// runtime tick; a different selection requires ClearPlayerConfiguration.
        /// </summary>
        public bool TryConfigurePlayer(
            D0CharacterDefinition definition,
            D0PlayerEntityView entity,
            D0ThreeCProfile profile,
            out string error)
        {
            if (playerConfigured || runtimeObserved)
            {
                error = "Formal player tick driver is already configured for this runtime.";
                return false;
            }

            if (definition == null || entity == null || profile == null)
            {
                error = "Formal player tick driver requires a definition, scene entity and 3C profile.";
                return false;
            }

            if (!definition.TryValidate(out error)
                || !entity.TryValidate(out error)
                || !profile.TryValidate(out error))
            {
                return false;
            }

            if (entity.AimAnchor == null)
            {
                error = "Formal player entity has no aim anchor.";
                return false;
            }

            if (aimViewportSource is CombatAimReticle reticle
                && !reticle.TrySetThreeCProfile(profile, out error))
            {
                return false;
            }

            playerDefinition = definition;
            playerEntity = entity;
            threeCProfile = profile;
            aimAnchor = entity.AimAnchor;
            playerRoot = entity.transform;
            aimDistance = profile.MaximumAimDistance;
            inputBufferTicks = profile.InputBufferTicks;
            playerConfigured = true;
            ResetRuntimeState();
            error = string.Empty;
            return true;
        }

        public bool TryBindCameraFeedback(
            FpgFormalPlayerCameraFeedback feedback,
            out string error)
        {
            if (runtimeObserved)
            {
                error = "Formal camera feedback cannot change after runtime binding.";
                return false;
            }

            cameraFeedback = feedback;
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!playerConfigured || playerDefinition == null
                || playerEntity == null || threeCProfile == null)
            {
                error = "Formal player tick driver must be configured before runtime binding.";
                return false;
            }

            if (encounterDirector == null || aimAnchor == null)
            {
                error = "Formal player tick driver requires explicit director and aim anchor references.";
                return false;
            }

            
            if (aimFromPointerPosition && AimViewportSource == null)
            {
                error = "Viewport-based formal aiming requires an ICombatAimViewportSource.";
                return false;
            }
if (aimViewportSource != null
                && !(aimViewportSource is ICombatAimViewportSource))
            {
                error = "Formal aim viewport component must implement ICombatAimViewportSource.";
                return false;
            }

            if (aimFromPointerPosition && aimCamera == null)
            {
                error = "Viewport-based formal aiming requires an explicit camera.";
                return false;
            }

            if (aimFromPointerPosition
                && (aimDistance <= 0f || aimLayerMask.value == 0 || playerRoot == null))
            {
                error = "Viewport-based formal aiming requires distance, collision mask, and player root references.";
                return false;
            }

            if (playerRoot != playerEntity.transform)
            {
                error = "Formal player tick driver root must match its configured scene entity.";
                return false;
            }

            if (aimAnchor != playerRoot && !aimAnchor.IsChildOf(playerRoot))
            {
                error = "Formal aim anchor must belong to the configured player root.";
                return false;
            }

            if (inputBufferTicks < 1 || inputBufferTicks > 32)
            {
                error = "Formal player tick driver input buffer must be between 1 and 32 ticks.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void CaptureAimPose()
        {
            if (!aimFromPointerPosition || aimCamera == null)
            {
                inputSource.CaptureAimPose(aimAnchor);
                return;
            }

            Vector2 viewport = CombatAimViewportMath.Center;
            ICombatAimViewportSource viewportSource = AimViewportSource;
            if (viewportSource != null
                && viewportSource.TryGetViewport(out Vector2 suppliedViewport)
                && IsFinite(suppliedViewport))
            {
                viewport = new Vector2(
                    Mathf.Clamp01(suppliedViewport.x),
                    Mathf.Clamp01(suppliedViewport.y));
            }

            Ray cameraRay = aimCamera.ViewportPointToRay(
                new Vector3(viewport.x, viewport.y, 0f));

            Vector3 targetPoint = cameraRay.GetPoint(aimDistance);
            float nearestDistance = float.PositiveInfinity;
            int hitCount = Physics.RaycastNonAlloc(
                cameraRay,
                aimRaycastBuffer,
                aimDistance,
                aimLayerMask,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = aimRaycastBuffer[index];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;
                if (playerRoot != null
                    && (hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot)))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    targetPoint = hit.point;
                }
            }

            Vector3 aimDirection = targetPoint - aimAnchor.position;
            if (aimDirection.sqrMagnitude <= 0.000001f)
            {
                aimDirection = aimAnchor.forward;
            }

            inputSource.CaptureAimPose(
                aimAnchor.position,
                aimDirection,
                aimCamera.transform.up);
        }

        public void Capture(UnityInputSnapshot snapshot)
        {
            if (!playerConfigured)
            {
                return;
            }

            if (inputSource == null)
            {
                ResetInputSource();
            }

            inputSource.Capture(snapshot);
        }

        public bool ConsumePausePressed()
        {
            return inputSource != null && inputSource.ConsumePausePressed();
        }

        public bool ConsumeRestartPressed()
        {
            return inputSource != null && inputSource.ConsumeRestartPressed();
        }

        public DomainResult ProcessPlayerTick(
            TickIndex tick,
            FpgFormalCombatRuntimeBundle runtime)
        {
            if (!playerConfigured || !tick.IsValid || runtime == null
                || runtime.IsDisposed || inputSource == null
                || captureFault != RejectReason.None)
            {
                return DomainResult.Rejected(
                    captureFault == RejectReason.None
                        ? RejectReason.InvalidState
                        : captureFault);
            }

            runtimeObserved = true;
            if (lastProcessedTick.IsValid && tick.Value != lastProcessedTick.Value + 1L)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            WeaponState stateAtTickStart = runtime.Player.Weapon.State;
            int ammoAtTickStart = runtime.Player.Weapon.Magazine.Ammo;
            if (runtime.Player.Combatant.IsDead)
            {
                lastProcessedTick = tick;
                PublishSnapshot(runtime, tick);
                return DomainResult.Success;
            }

            BattleTickInput tickInput = inputSource.GetTickInput(tick);
            if (!tickInput.IsValid || tickInput.Tick != tick)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            PlayerInputFrame frame = tickInput.CopyToPlayerInputFrame(edgeBuffer);
            DomainResult posture = ApplyPosture(runtime.Player, frame, tick);
            if (!posture.IsSuccess && posture.RejectReason != RejectReason.BarrierLocked)
            {
                return posture;
            }

            ulong playerSeed = runtime.RunContext.DeriveSeed(PlayerAttackRandomDomain);
            DomainResult weapon = runtime.Player.Weapon.ProcessFrame(
                frame,
                runtime.Player.Exposure,
                runtime.Player.RuntimeId,
                runtime.IdAllocator,
                playerSeed,
                weaponRelease);
            if (!weapon.IsSuccess)
            {
                return weapon;
            }

            if (weaponRelease.HasRelease)
            {
                DomainResult submitted = QueryAndSubmitHits(runtime, tickInput, tick);
                if (!submitted.IsSuccess)
                {
                    PublishSnapshot(runtime, tick);
                    return submitted;
                }
            }

            PublishCommittedActions(
                tick,
                stateAtTickStart,
                runtime.Player.Weapon.State,
                ammoAtTickStart,
                runtime.Player.Weapon.Magazine.Ammo);
            lastProcessedTick = tick;
            PublishSnapshot(runtime, tick);
            return DomainResult.Success;
        }

        public bool TryRefreshPresentationSnapshot(
            out FpgFormalPlayerPresentationSnapshot snapshot)
        {
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!playerConfigured || runtime == null || runtime.IsDisposed)
            {
                snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
                return false;
            }

            TickIndex tick = runtime.Player.Weapon.LastProcessedTick.IsValid
                ? runtime.Player.Weapon.LastProcessedTick
                : lastProcessedTick;
            PublishSnapshot(runtime, tick);
            return presentationSource.TryGetPlayerPresentationSnapshot(out snapshot);
        }

        /// <summary>
        /// IFpgFormalPlayerTickDriver restart hook. This clears tick-local state
        /// but intentionally retains the one-shot player composition.
        /// </summary>
        public void Clear()
        {
            ResetRuntimeState();
            runtimeObserved = false;
        }

        public void ClearPlayerBinding()
        {
            Clear();
            playerDefinition = null;
            playerEntity = null;
            threeCProfile = null;
            aimAnchor = null;
            playerRoot = null;
            playerConfigured = false;
            inputSource = null;
            presentationSource.Clear();
        }

        private DomainResult QueryAndSubmitHits(
            FpgFormalCombatRuntimeBundle runtime,
            BattleTickInput tickInput,
            TickIndex tick)
        {
            Array.Clear(queryCandidates, 0, queryCandidates.Length);
            Array.Clear(selectedCandidates, 0, selectedCandidates.Length);

            AttackQueryRequest request;
            try
            {
                request = new AttackQueryRequest(
                    tickInput,
                    weaponRelease.Attack,
                    weaponRelease.Pellets,
                    weaponRelease.PelletCount);
            }
            catch (Exception)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            DomainResult query = runtime.AttackQueryPort.Query(
                request,
                queryCandidates,
                out AttackQueryResult queryResult);
            if (!query.IsSuccess)
            {
                return query;
            }

            DomainResult selected = TargetSelector.Select(
                weaponRelease.Attack,
                queryCandidates,
                queryResult,
                selectedCandidates,
                out int selectedCount);
            if (!selected.IsSuccess)
            {
                return selected;
            }

            if (selectedCount > 0 && nextCommandSequence > long.MaxValue - selectedCount)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int index = 0; index < selectedCount; index++)
            {
                QueryCandidate candidate = selectedCandidates[index];
                bool projectile = candidate.TargetKind == QueryTargetKind.Projectile;
                if (projectile)
                {
                    if (!runtime.CombatPort.TryGetProjectile(
                            candidate.TargetId,
                            out ProjectileRuntime ignoredProjectile))
                    {
                        return DomainResult.Rejected(RejectReason.InvalidTarget);
                    }
                }
                else if (candidate.TargetKind != QueryTargetKind.Combatant
                    || !runtime.CombatPort.TryGetEnemyRuntime(
                        candidate.TargetId,
                        out EnemyRuntime ignoredEnemy))
                {
                    return DomainResult.Rejected(RejectReason.InvalidTarget);
                }

                ImpactIntent intent = new ImpactIntent(
                    runtime.IdAllocator.NextImpactId(),
                    weaponRelease.Attack.AttackId,
                    weaponRelease.Attack.ShotId,
                    runtime.Player.RuntimeId,
                    candidate.TargetId,
                    tick,
                    weaponRelease.Attack.DamageSpec,
                    candidate.HitPart,
                    projectile
                        ? DamageType.ProjectileIntercept
                        : weaponRelease.Kind == WeaponReleaseKind.Secondary
                            ? DamageType.Explosive
                            : DamageType.Normal,
                    weaponRelease.Kind == WeaponReleaseKind.Secondary
                        ? CombatTags.Secondary
                        : CombatTags.Primary,
                    weaponRelease.Kind == WeaponReleaseKind.Primary
                        ? candidate.SampleIndex
                        : -1,
                    index);
                ImpactPhasePriority priority = projectile
                    ? ImpactPhasePriority.PlayerProjectileIntercept
                    : ImpactPhasePriority.PlayerCombatantHit;
                DomainResult submitted = runtime.CombatPort.TrySubmitPlayerHit(
                    new FpgPlayerHitCommand(nextCommandSequence, intent, priority));
                if (!submitted.IsSuccess)
                {
                    return submitted;
                }

                nextCommandSequence++;
            }

            return DomainResult.Success;
        }

        private static DomainResult ApplyPosture(
            PlayerRuntime player,
            PlayerInputFrame frame,
            TickIndex tick)
        {
            PlayerExposureState previousExposure = player.Exposure.State;
            bool reloadKeepsWithdrawn = player.Weapon.State == WeaponState.Reloading
                && (!player.Weapon.StateUntilTick.IsValid
                    || tick < player.Weapon.StateUntilTick);
            bool reloadRequestsWithdrawn = reloadKeepsWithdrawn || frame.HasReloadInput;
            bool shouldExpose = !reloadRequestsWithdrawn
                && !frame.CancelSecondary
                && (frame.AimHeld
                    || frame.PrimaryHeld
                    || frame.HasSecondaryInput
                    || player.Weapon.State == WeaponState.AltCharging);
            DomainResult result = reloadRequestsWithdrawn
                ? player.Exposure.ApplyReloadPosture(tick, out bool ignoredReloadChange)
                : player.Exposure.ApplyCombatPosture(
                    shouldExpose,
                    tick,
                    player.Combatant.IsBarrierLocked(tick)
                        || player.Combatant.Barrier <= 0,
                    out bool ignoredPostureChange);

            if (player.Exposure.State == PlayerExposureState.Withdrawn
                && previousExposure != PlayerExposureState.Withdrawn)
            {
                player.Weapon.CancelForWithdrawn();
            }

            return result;
        }

        private void PublishCommittedActions(
            TickIndex tick,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter)
        {
            if (stateBefore == WeaponState.Reloading
                && stateAfter != WeaponState.Reloading)
            {
                PublishAction(
                    tick,
                    FpgFormalPlayerActionType.ReloadCompleted,
                    WeaponReleaseKind.None,
                    AttackId.Invalid,
                    stateBefore,
                    stateAfter,
                    ammoBefore,
                    ammoAfter);
            }

            if (stateBefore == WeaponState.AltCharging
                && stateAfter != WeaponState.AltCharging
                && (!weaponRelease.HasRelease
                    || weaponRelease.Kind != WeaponReleaseKind.Secondary))
            {
                PublishAction(
                    tick,
                    FpgFormalPlayerActionType.SecondaryChargeCanceled,
                    WeaponReleaseKind.None,
                    AttackId.Invalid,
                    stateBefore,
                    stateAfter,
                    ammoBefore,
                    ammoAfter);
            }

            if (stateBefore != WeaponState.Reloading
                && stateAfter == WeaponState.Reloading)
            {
                PublishAction(
                    tick,
                    FpgFormalPlayerActionType.ReloadStarted,
                    WeaponReleaseKind.None,
                    AttackId.Invalid,
                    stateBefore,
                    stateAfter,
                    ammoBefore,
                    ammoAfter);
            }

            if (stateBefore != WeaponState.AltCharging
                && stateAfter == WeaponState.AltCharging)
            {
                PublishAction(
                    tick,
                    FpgFormalPlayerActionType.SecondaryChargeStarted,
                    WeaponReleaseKind.None,
                    AttackId.Invalid,
                    stateBefore,
                    stateAfter,
                    ammoBefore,
                    ammoAfter);
            }

            if (!weaponRelease.HasRelease)
            {
                return;
            }

            PublishAction(
                tick,
                weaponRelease.Kind == WeaponReleaseKind.Secondary
                    ? FpgFormalPlayerActionType.SecondaryReleaseCommitted
                    : FpgFormalPlayerActionType.PrimaryReleaseCommitted,
                weaponRelease.Kind,
                weaponRelease.Attack.AttackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter);
        }

        private void PublishAction(
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter)
        {
            presentationSource.PublishAction(
                tick,
                type,
                releaseKind,
                attackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter);
        }

        private void PublishSnapshot(
            FpgFormalCombatRuntimeBundle runtime,
            TickIndex tick)
        {
            PlayerRuntime player = runtime.Player;
            presentationSource.PublishSnapshot(
                new FpgFormalPlayerPresentationSnapshot(
                    tick,
                    player.RuntimeId,
                    encounterDirector == null
                        ? FpgEncounterPhase.None
                        : encounterDirector.Phase,
                    encounterDirector != null && encounterDirector.IsPaused,
                    player.Combatant.Life,
                    player.Combatant.MaxLife,
                    player.Combatant.Barrier,
                    player.Combatant.MaxBarrier,
                    player.Weapon.Magazine.Ammo,
                    player.Weapon.Magazine.Capacity,
                    player.Exposure.State,
                    player.Weapon.State));
        }

        public void ResetRuntimeState()
        {
            Array.Clear(edgeBuffer, 0, edgeBuffer.Length);
            Array.Clear(queryCandidates, 0, queryCandidates.Length);
            Array.Clear(selectedCandidates, 0, selectedCandidates.Length);
            weaponRelease.Reset();
            lastProcessedTick = TickIndex.Invalid;
            nextCommandSequence = 0L;
            captureFault = RejectReason.None;
            presentationSource.Clear();
            cameraFeedback?.ResetRuntimeFeedback();
            if (aimViewportSource is CombatAimReticle reticle)
            {
                reticle.SetInputFrozen(false);
                reticle.ResetToCenter();
            }
            ResetInputSource();
        }

        private void ResetInputSource()
        {
            inputSource = new UnityBattleInputSource();
            inputSource.ConfigureInputBufferTicks(Mathf.Clamp(inputBufferTicks, 1, 32));
            captureFault = RejectReason.None;
            if (!playerConfigured || aimAnchor == null)
            {
                return;
            }

            try
            {
                CaptureAimPose();
            }
            catch (Exception)
            {
                captureFault = RejectReason.InvariantFault;
            }
        }

        private void SetAimViewportFrozen(bool frozen)
        {
            if (aimViewportSource is CombatAimReticle reticle)
            {
                reticle.SetInputFrozen(frozen);
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                inputSource?.ClearGameplayInput();
            }
        }

        private void OnDisable()
        {
            inputSource?.ClearGameplayInput();
            SetAimViewportFrozen(false);
        }
    }
}








