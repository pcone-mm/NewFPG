using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Captures rendered-frame input into fixed buffers and consumes it only
    /// from the formal PlayerAttackAndHit tick phase. It never calls the legacy
    /// single-enemy BattleSession.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerTickDriver : MonoBehaviour,
        IFpgFormalPlayerTickDriver,
        IFpgShootingDiagnosticsProvider,
        IFpgPlayerFacingActionSource
    {
        private const ulong PlayerAttackRandomDomain = 0x4650475F504C4159UL;
        private const int MaximumCoverPeekGateTickCount = 32;
        private const int CoverPeekPendingEdgeCapacity =
            BattleTickInput.MaxEdgeCommandCount
                * (MaximumCoverPeekGateTickCount + 1);

        [Header("Formal Runtime")]
        [SerializeField] private FpgRoomEncounterDirector encounterDirector = null;
        [SerializeField] private Transform aimAnchor = null;
        [SerializeField] private Transform shotOrigin = null;
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
        [SerializeField]
        private FpgCoverTraversalPresenter coverTraversalPresenter = null;

        [Header("Input")]
        [SerializeField] private bool captureFromDevices = true;
        [SerializeField] private bool handlePauseAndRestart = true;
        [SerializeField, Range(1, 32)] private int inputBufferTicks = 8;

        private readonly RaycastHit[] aimRaycastBuffer = new RaycastHit[16];
        private readonly InputEdgeCommand[] edgeBuffer =
            new InputEdgeCommand[BattleTickInput.MaxEdgeCommandCount];
        private readonly InputEdgeCommand[] coverPeekFrameEdges =
            new InputEdgeCommand[BattleTickInput.MaxEdgeCommandCount];
        private readonly InputEdgeCommand[] coverPeekPendingEdges =
            new InputEdgeCommand[CoverPeekPendingEdgeCapacity];
        private readonly QueryCandidate[] queryCandidates =
            new QueryCandidate[TargetSelector.DefaultCandidateCapacity];
        private readonly QueryCandidate[] selectedCandidates =
            new QueryCandidate[TargetSelector.DefaultCandidateCapacity];
        private readonly FpgPlayerHitCommand[] playerHitBatch =
            new FpgPlayerHitCommand[TargetSelector.DefaultCandidateCapacity];
        private readonly WeaponReleaseBuffer weaponRelease = new WeaponReleaseBuffer();
        private readonly ProjectWideBattleInputAdapter projectWideBattleInputAdapter =
            new ProjectWideBattleInputAdapter();
        private readonly FpgFormalPlayerPresentationSource presentationSource =
            new FpgFormalPlayerPresentationSource();
        private FpgSkillPresentationCommitCache
            presentationCommitCache =
                new FpgSkillPresentationCommitCache(1);

        private UnityBattleInputSource inputSource;
        private FpgPlayerSkillExecutionController skillExecutionController;
        private D0CharacterDefinition playerDefinition;
        private FpgPlayerEntityView playerEntity;
        private D0ThreeCProfile threeCProfile;
        private SecondaryTriggerMode playerSecondaryTriggerMode;
        private TickIndex lastProcessedTick = TickIndex.Invalid;
        private long nextCommandSequence;
        private RejectReason captureFault = RejectReason.None;
        private FpgFormalAimSolution aimSolution = FpgFormalAimSolution.Idle;
        private FpgResolvedAimContext liveAimContext =
            FpgResolvedAimContext.Invalid;
        private FpgResolvedAimContext liveAttackAimContext =
            FpgResolvedAimContext.Invalid;
        private FpgResolvedAimContext frozenAimContext =
            FpgResolvedAimContext.Invalid;
        private FpgAttackAvailability primaryAttackAvailability;
        private FpgAttackAvailability secondaryAttackAvailability;
        private UnityAttackQuerySettings attackQuerySettings;
        private bool hasShootingPreview;
        private FpgShootingTuningSnapshot shootingPreview;
        private TickIndex reloadPresentationStartTick = TickIndex.Invalid;
        private long nextAimContextVersion = 1L;
        private int coverPeekGateTickCount = 5;
        private FpgPlayerSkillSlot queuedAttackAfterReload =
            FpgPlayerSkillSlot.None;
        private bool playerConfigured;
        private bool runtimeObserved;
        private bool roomInteractionArmed;
        private bool reloadCompletionActionPublishedThisTick;
        private bool presentationPaused;
        private bool lifecycleAimViewportFrozen;
        private bool isCoverPeekRequested;
        private bool coverPeekPrimaryPending;
        private bool coverPeekAimFrozen;
        private bool useFrozenAimForActiveAttack;
        private int coverPeekPendingEdgeCount;
        private TickIndex coverPeekStartedTick = TickIndex.Invalid;
        private AimPoseSnapshot coverPeekFrozenAimPose;
        private FpgPlayerFacingDirection coverPeekDirection =
            FpgPlayerFacingDirection.Right;

        public FpgRoomEncounterDirector EncounterDirector => encounterDirector;
        public Transform AimAnchor => aimAnchor;
        public Transform ShotOrigin => shotOrigin;
        public MonoBehaviour AimViewportSourceComponent => aimViewportSource;
        public ICombatAimViewportSource AimViewportSource =>
            aimViewportSource as ICombatAimViewportSource;
        public FpgFormalPlayerCameraFeedback CameraFeedback => cameraFeedback;
        public D0CharacterDefinition PlayerDefinition => playerDefinition;
        public FpgPlayerEntityView PlayerEntity => playerEntity;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public bool HasShootingPreview => hasShootingPreview;
        public FpgShootingTuningSnapshot ShootingPreview => shootingPreview;
        public float EffectiveCoverTraversalSeconds => hasShootingPreview
            ? shootingPreview.CoverTraversalSeconds
            : threeCProfile == null
                ? 0f
                : threeCProfile.CoverTraversalSeconds;
        public SecondaryTriggerMode PlayerSecondaryTriggerMode =>
            playerSecondaryTriggerMode;
        public bool IsPlayerConfigured => playerConfigured;
        public bool HasCaptureFault => playerConfigured
            && captureFault != RejectReason.None;
        public TickIndex LastProcessedTick => lastProcessedTick;
        public FpgFormalAimSolution AimSolution => aimSolution;
        public FpgResolvedAimContext LiveAimContext => liveAimContext;
        public FpgResolvedAimContext ResolvedAimContext =>
            frozenAimContext.IsFrozen
                ? frozenAimContext
                : liveAttackAimContext.IsValid
                    ? liveAttackAimContext
                    : liveAimContext;
        public FpgAttackAvailability PrimaryAttackAvailability =>
            primaryAttackAvailability;
        public FpgAttackAvailability SecondaryAttackAvailability =>
            secondaryAttackAvailability;
        public int AimPreviewFaultCount { get; private set; }
        public int AimPresentationFaultCount { get; private set; }
        public int SkillPresentationFaultCount { get; private set; }
        public bool IsCoverPeekRequested => isCoverPeekRequested;
        public TickIndex CoverPeekStartedTick => coverPeekStartedTick;
        public FpgPlayerFacingDirection CoverPeekDirection =>
            coverPeekDirection;
        public bool CanUpdateFacingFromReticle
        {
            get
            {
                FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                    ? null
                    : encounterDirector.CombatRuntime;
                Actor2DPresenter actor = playerEntity == null
                    ? null
                    : playerEntity.ActorPresenter;
                FpgEncounterPhase phase = encounterDirector == null
                    ? FpgEncounterPhase.None
                    : encounterDirector.Phase;
                return playerConfigured
                    && runtime != null
                    && !runtime.IsDisposed
                    && runtime.Player != null
                    && !runtime.Player.Combatant.IsDead
                    && (runtime.Covers == null || !runtime.Covers.IsTraversing)
                    && skillExecutionController != null
                    && !skillExecutionController.IsExecuting
                    && coverTraversalPresenter != null
                    && !coverTraversalPresenter.IsPlaying
                    && actor != null
                    && actor.IsFacingIdle
                    && encounterDirector != null
                    && !encounterDirector.IsPaused
                    && IsFacingPhase(phase);
            }
        }
        public FpgFormalPlayerPresentationSource PresentationSource =>
            presentationSource;
        public FpgCoverTraversalPresenter CoverTraversalPresenter =>
            coverTraversalPresenter;

        public event Action<FpgFormalPlayerActionEvent> ActionCommitted
        {
            add => presentationSource.ActionCommitted += value;
            remove => presentationSource.ActionCommitted -= value;
        }

        public event Action<FpgFormalPlayerSkillSequenceEvent> SkillSequenceAdvanced
        {
            add => presentationSource.SkillSequenceAdvanced += value;
            remove => presentationSource.SkillSequenceAdvanced -= value;
        }

        public event Action<FpgFormalPlayerActivePresentationEvent>
            ActivePresentationCommitted
        {
            add => presentationSource.ActivePresentationCommitted += value;
            remove => presentationSource.ActivePresentationCommitted -= value;
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

            bool isPaused = encounterDirector != null
                && encounterDirector.IsPaused;
            if (isPaused && !presentationPaused)
            {
                presentationCommitCache.Clear();
                ClearCoverPeekGate();
            }

            presentationPaused = isPaused;
            coverTraversalPresenter?.SetPaused(isPaused);
            cameraFeedback?.SetPaused(isPaused);
            SetAimViewportFrozen(isPaused);

            if (inputSource == null)
            {
                ResetInputSource();
            }

            FpgEncounterPhase phase = encounterDirector == null
                ? FpgEncounterPhase.None
                : encounterDirector.Phase;
            if (phase == FpgEncounterPhase.None
                || phase == FpgEncounterPhase.Preparing)
            {
                inputSource.ConsumeRestartPressed();
                inputSource.ConsumePausePressed();
                ClearGameplayInputAndPendingAttackIntent();
                ClearCoverPeekGate();
                coverTraversalPresenter?.SetPaused(false);
                return;
            }

            if (captureFromDevices)
            {
                if (!projectWideBattleInputAdapter.TryCapture(inputSource))
                {
                    ClearGameplayInputAndPendingAttackIntent();
                    ClearCoverPeekGate();
                    captureFault = RejectReason.InvalidState;
                    return;
                }
            }

            if (aimAnchor == null || shotOrigin == null)
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
                ClearCoverPeekGate();
                if (encounterDirector == null || !encounterDirector.TryRestart(out _))
                {
                    captureFault = RejectReason.InvalidState;
                }

                return;
            }

            if (inputSource.ConsumePausePressed())
            {
                if (phase != FpgEncounterPhase.Warning
                    && phase != FpgEncounterPhase.Spawning
                    && phase != FpgEncounterPhase.Combat
                    && phase != FpgEncounterPhase.WaveDelay
                    && phase != FpgEncounterPhase.Paused)
                {
                    ClearGameplayInputAndPendingAttackIntent();
                    ClearCoverPeekGate();
                    return;
                }

                bool changed = encounterDirector != null
                    && (encounterDirector.IsPaused
                        ? encounterDirector.TryResume(out _)
                        : encounterDirector.TryPause(out _));
                if (!changed)
                {
                    captureFault = RejectReason.InvalidState;
                    return;
                }

                ClearGameplayInputAndPendingAttackIntent();
                ClearCoverPeekGate();
                coverTraversalPresenter?.SetPaused(encounterDirector.IsPaused);
                SetAimViewportFrozen(encounterDirector.IsPaused);
            }
            else if (encounterDirector != null && encounterDirector.IsPaused)
            {
                ClearGameplayInputAndPendingAttackIntent();
                ClearCoverPeekGate();
                SetAimViewportFrozen(true);
            }
        }

        /// <summary>
        /// One-shot composition entry. It must run before the first formal
        /// runtime tick; a different selection requires ClearPlayerConfiguration.
        /// </summary>
        public bool TryConfigurePlayer(
            D0CharacterDefinition definition,
            FpgPlayerEntityView entity,
            D0ThreeCProfile profile,
            UnityAttackQuerySettings querySettings,
            SecondaryTriggerMode secondaryTriggerMode,
            out string error)
        {
            if (playerConfigured || runtimeObserved)
            {
                error = "Formal player tick driver is already configured for this runtime.";
                return false;
            }

            if (definition == null || entity == null || profile == null
                || !querySettings.IsValid)
            {
                error = "Formal player tick driver requires a definition, scene entity and 3C profile.";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(SecondaryTriggerMode),
                    secondaryTriggerMode))
            {
                error =
                    $"Formal player tick driver received invalid secondary trigger mode '{secondaryTriggerMode}'.";
                return false;
            }

            if (!definition.TryValidate(out error)
                || !entity.TryValidate(out error)
                || !profile.TryValidate(out error))
            {
                return false;
            }

            if (!definition.Weapon.TryResolveSecondarySkill(
                    secondaryTriggerMode,
                    out FpgPlayerSkillDefinition authoredSecondary,
                    out error))
            {
                return false;
            }

            if (!definition.Weapon.TryCompileSkills(
                    secondaryTriggerMode,
                    out FpgCompiledPlayerSkillDefinition compiledPrimary,
                    out FpgCompiledPlayerSkillDefinition compiledSecondary,
                    out FpgCompiledPlayerSkillDefinition compiledReload,
                    out error)
                || !TryValidatePresentationMappings(
                    definition.Weapon.PrimarySkill,
                    compiledPrimary,
                    out error)
                || !TryValidatePresentationMappings(
                    authoredSecondary,
                    compiledSecondary,
                    out error)
                || !TryValidatePresentationMappings(
                    definition.Weapon.ReloadSkill,
                    compiledReload,
                    out error)
                || !FpgPlayerSkillPresentationResolver.TryValidatePrefabBindings(
                    definition.EntityPrefab,
                    definition.Weapon.PrimarySkill,
                    authoredSecondary,
                    definition.Weapon.ReloadSkill,
                    out error)
                || !FpgPlayerSkillExecutionController.TryCreate(
                    compiledPrimary,
                    compiledSecondary,
                    compiledReload,
                    secondaryTriggerMode,
                    definition.AttackSpeedProfile,
                    StaticAttackSpeedBonusProvider.Zero,
                    profile.InputBufferTicks,
                    out FpgPlayerSkillExecutionController controller,
                    out error))
            {
                return false;
            }

            if (entity.AimAnchor == null || entity.ShotOrigin == null)
            {
                error = "Formal player entity has no aim anchor or shot origin.";
                return false;
            }

            if (aimViewportSource is CombatAimReticle reticle
                && !reticle.TrySetThreeCProfile(profile, out error))
            {
                return false;
            }

            FpgCoverTraversalPresenter traversalPresenter =
                entity.GetComponent<FpgCoverTraversalPresenter>();
            if (traversalPresenter == null
                || !traversalPresenter.TryConfigure(
                    entity.VisualRoot,
                    out error))
            {
                error = traversalPresenter == null
                    ? "Formal player entity requires a cover traversal presenter."
                    : error;
                return false;
            }

            int presentationCommitCapacity;
            try
            {
                presentationCommitCapacity = checked(
                    compiledPrimary.AttackActionCount
                    + compiledPrimary.ProjectileActionCount
                    + compiledPrimary.ReloadActionCount
                    + compiledSecondary.AttackActionCount
                    + compiledSecondary.ProjectileActionCount
                    + compiledSecondary.ReloadActionCount
                    + compiledReload.AttackActionCount
                    + compiledReload.ProjectileActionCount
                    + compiledReload.ReloadActionCount);
            }
            catch (OverflowException)
            {
                error = "Player skill presentation commit capacity overflowed.";
                return false;
            }

            playerDefinition = definition;
            skillExecutionController = controller;
            presentationCommitCache = new FpgSkillPresentationCommitCache(
                Math.Max(1, presentationCommitCapacity));
            playerEntity = entity;
            threeCProfile = profile;
            playerSecondaryTriggerMode = secondaryTriggerMode;
            aimAnchor = entity.AimAnchor;
            shotOrigin = entity.ShotOrigin;
            playerRoot = entity.transform;
            coverTraversalPresenter = traversalPresenter;
            attackQuerySettings = querySettings;
            aimDistance = querySettings.MaxDistance;
            aimLayerMask = querySettings.HitboxLayerMask
                | querySettings.BlockerLayerMask;
            inputBufferTicks = profile.InputBufferTicks;
            coverPeekGateTickCount = Mathf.Clamp(
                TickDuration.FromSeconds(profile.PeekTransitionSeconds).Value,
                0,
                MaximumCoverPeekGateTickCount);
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

            error = string.Empty;
            if (feedback == null || !feedback.IsPrepared
                || feedback.CameraRig == null
                || coverTraversalPresenter == null
                || playerEntity == null
                || feedback.CameraRig == playerEntity.transform
                || feedback.CameraRig.IsChildOf(playerEntity.transform))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Formal cover traversal requires prepared scene-owned camera feedback."
                    : error;
                return false;
            }

            cameraFeedback = feedback;
            aimCamera = feedback.TargetCamera;
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!playerConfigured || playerDefinition == null
                || playerEntity == null || threeCProfile == null
                || skillExecutionController == null
                || coverTraversalPresenter == null
                || cameraFeedback == null
                || !cameraFeedback.IsPrepared)
            {
                error = "Formal player tick driver and scene camera feedback must be configured before runtime binding.";
                return false;
            }

            if (encounterDirector == null || aimAnchor == null
                || shotOrigin == null)
            {
                error = "Formal player tick driver requires explicit director, aim anchor and shot origin references.";
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

            if (shotOrigin != playerRoot && !shotOrigin.IsChildOf(playerRoot))
            {
                error = "Formal shot origin must belong to the configured player root.";
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
            if (playerEntity != null
                && !playerEntity.TryRefreshSpineSocketFollowers(out string socketError))
            {
                throw new InvalidOperationException(socketError);
            }

            cameraFeedback?.SynchronizeForAimSampling();
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

            Vector3 cameraOrigin;
            Vector3 cameraDirection;
            Vector3 referenceUp;
            if (aimFromPointerPosition && aimCamera != null)
            {
                Ray cameraRay = aimCamera.ViewportPointToRay(
                    new Vector3(viewport.x, viewport.y, 0f));
                cameraOrigin = cameraRay.origin;
                cameraDirection = cameraRay.direction;
                referenceUp = aimCamera.transform.up;
            }
            else
            {
                cameraOrigin = aimAnchor.position;
                cameraDirection = aimAnchor.forward;
                referenceUp = aimAnchor.up;
            }

            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (runtime == null || runtime.IsDisposed
                || runtime.AttackQueryPort == null)
            {
                Vector3 targetPoint = cameraOrigin
                    + cameraDirection.normalized * aimDistance;
                Vector3 direction = targetPoint - shotOrigin.position;
                if (direction.sqrMagnitude <= 0.000001f)
                {
                    direction = shotOrigin.forward;
                }

                inputSource.CaptureAimPose(
                    shotOrigin.position,
                    direction,
                    referenceUp);
                liveAimContext = FpgResolvedAimContext.Invalid;
                liveAttackAimContext = FpgResolvedAimContext.Invalid;
                SetAimSolution(FpgFormalAimSolution.Idle);
                return;
            }

            FpgCoverSnapshot cover = runtime.Covers == null
                ? default(FpgCoverSnapshot)
                : runtime.Covers.CurrentSnapshot;
            DomainResult solved = runtime.AttackQueryPort.ResolveAimContext(
                viewport,
                cameraOrigin,
                cameraDirection,
                shotOrigin.position,
                runtime.Player.RuntimeId,
                Team.Player,
                runtime.Player.Weapon.Definition.PrimaryAllowedTargetKinds
                    | runtime.Player.Weapon.Definition.SecondaryAllowedTargetKinds,
                cover.IsValid ? cover.CoverId : string.Empty,
                encounterDirector.RoomInstance,
                NextAimContextVersion(),
                out FpgResolvedAimContext next);
            if (!solved.IsSuccess)
            {
                AimPreviewFaultCount++;
                liveAimContext = FpgResolvedAimContext.Invalid;
                liveAttackAimContext = FpgResolvedAimContext.Invalid;
                inputSource.CaptureAimPose(
                    shotOrigin.position,
                    shotOrigin.forward,
                    referenceUp);
                if (!frozenAimContext.IsFrozen)
                {
                    SetAimSolution(FpgFormalAimSolution.Idle);
                }
                return;
            }

            liveAimContext = next;
            inputSource.CaptureAimPose(
                next.ShotOrigin,
                next.CenterDirection,
                referenceUp);

            Vector3 previewShotOrigin = ResolveAttackPreviewShotOrigin(
                next.ReticleViewport);
            liveAttackAimContext = next;
            if ((previewShotOrigin - next.ShotOrigin).sqrMagnitude
                > 0.0000001f)
            {
                DomainResult previewSolved = runtime.AttackQueryPort.ResolveAimContext(
                    next.ReticleViewport,
                    next.CameraOrigin,
                    next.CameraDirection,
                    previewShotOrigin,
                    runtime.Player.RuntimeId,
                    Team.Player,
                    runtime.Player.Weapon.Definition.PrimaryAllowedTargetKinds
                        | runtime.Player.Weapon.Definition.SecondaryAllowedTargetKinds,
                    cover.IsValid ? cover.CoverId : string.Empty,
                    encounterDirector.RoomInstance,
                    NextAimContextVersion(),
                    out FpgResolvedAimContext previewContext);
                if (!previewSolved.IsSuccess)
                {
                    AimPreviewFaultCount++;
                    liveAttackAimContext = FpgResolvedAimContext.Invalid;
                }
                else
                {
                    liveAttackAimContext = previewContext;
                }
            }

            if (frozenAimContext.IsFrozen)
            {
                DomainResult rebased = runtime.AttackQueryPort.ResolveFrozenAimShotOrigin(
                    frozenAimContext,
                    ResolveAttackPreviewShotOrigin(
                        frozenAimContext.ReticleViewport),
                    runtime.Player.RuntimeId,
                    Team.Player,
                    runtime.Player.Weapon.Definition.PrimaryAllowedTargetKinds
                        | runtime.Player.Weapon.Definition.SecondaryAllowedTargetKinds,
                    cover.IsValid ? cover.CoverId : string.Empty,
                    encounterDirector.RoomInstance,
                    out FpgResolvedAimContext rebasedContext);
                if (!rebased.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "Frozen aim could not be rebased to the current Spine ShotOrigin.");
                }

                frozenAimContext = rebasedContext;
                SetAimSolution(
                    frozenAimContext.IsReticleEnemy
                        || frozenAimContext.IsCurrentCoverBlocked
                        ? FpgFormalAimSolution.FromContext(frozenAimContext)
                        : FpgFormalAimSolution.Idle);
            }
            else
            {
                SetAimSolution(
                    liveAttackAimContext.IsReticleEnemy
                        || liveAttackAimContext.IsCurrentCoverBlocked
                        ? FpgFormalAimSolution.FromContext(liveAttackAimContext)
                        : FpgFormalAimSolution.Idle);
            }
        }

        private Vector3 ResolveAttackPreviewShotOrigin(
            Vector2 reticleViewport)
        {
            if (playerEntity == null || playerEntity.ShotOrigin == null)
            {
                return shotOrigin == null ? Vector3.zero : shotOrigin.position;
            }

            FpgPlayerBarrierPresentationController barrier =
                playerEntity.Barrier;
            FpgCoverRuntime covers = encounterDirector?.CombatRuntime?.Covers;
            FpgCoverSnapshot cover = covers == null
                ? default(FpgCoverSnapshot)
                : covers.CurrentSnapshot;
            if (!cover.IsValid || cover.IsDestroyed
                || barrier == null
                || !barrier.TryGetPreviewPeekWorldOffset(
                    cover.CoverId,
                    barrier.HasSelectedPeekTarget
                        ? barrier.SelectedPeekDirection
                        : FpgPlayerFacingController.ResolveDirection(
                            reticleViewport.x),
                    out Vector3 remainingWorldOffset))
            {
                return playerEntity.ShotOrigin.position;
            }

            return playerEntity.ShotOrigin.position
                + remainingWorldOffset;
        }

        private long NextAimContextVersion()
        {
            long result = nextAimContextVersion;
            nextAimContextVersion = nextAimContextVersion == long.MaxValue
                ? 1L
                : nextAimContextVersion + 1L;
            return result;
        }

        private void SetAimSolution(in FpgFormalAimSolution next)
        {
            aimSolution = next;
            if (!(aimViewportSource is CombatAimReticle reticle))
            {
                return;
            }

            FpgReticleTargetState state;
            switch (next.Kind)
            {
                case FpgAimSolutionKind.Hittable:
                    state = FpgReticleTargetState.Hittable;
                    break;
                case FpgAimSolutionKind.Blocked:
                    state = FpgReticleTargetState.Blocked;
                    break;
                default:
                    state = FpgReticleTargetState.Idle;
                    break;
            }

            try
            {
                reticle.SetResolvedAimContext(ResolvedAimContext);
                reticle.SetTargetState(state);
            }
            catch (Exception)
            {
                AimPresentationFaultCount++;
            }
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

        public void BeginRoomInteraction()
        {
            ClearCoverPeekGate();
            skillExecutionController?.ClearPendingInputIntents();
            roomInteractionArmed = inputSource == null
                || !inputSource.PrimaryHeld && !inputSource.SecondaryHeld;
            WeaponRuntime weapon =
                encounterDirector?.CombatRuntime?.Player?.Weapon;
            bool cancelSecondary = weapon?.State == WeaponState.AltCharging;
            if (weapon != null && skillExecutionController != null)
            {
                TickIndex interruptTick = skillExecutionController.NextTick.IsValid
                    ? skillExecutionController.NextTick
                    : weapon.LastProcessedTick.IsValid
                        ? new TickIndex(weapon.LastProcessedTick.Value + 1L)
                        : new TickIndex(0L);
                DomainResult interrupted = skillExecutionController.HardInterrupt(
                    interruptTick,
                    weapon);
                if (!interrupted.IsSuccess)
                {
                    captureFault = RejectReason.InvariantFault;
                }
                else
                {
                    PublishSkillSequenceFrames();
                    ReleaseTerminalPresentationCommits();
                }
            }

            weaponRelease.Reset();
            inputSource?.BeginRoomInteraction(cancelSecondary);
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

            if (!skillExecutionController.TryBindExecutionIdAllocator(
                    runtime.SkillExecutionIds,
                    out _))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
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
                ClearCoverPeekGate();
                DomainResult interrupted = skillExecutionController.HardInterrupt(
                    tick,
                    runtime.Player.Weapon);
                if (!interrupted.IsSuccess)
                {
                    return interrupted;
                }

                PublishSkillSequenceFrames();
                ReleaseTerminalPresentationCommits();
                lastProcessedTick = tick;
                PublishSnapshot(runtime, tick);
                return DomainResult.Success;
            }

            UpdateReloadAttackQueue(runtime, tick);

            BattleTickInput tickInput = inputSource.GetTickInput(tick);
            if (!tickInput.IsValid || tickInput.Tick != tick)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            PlayerInputFrame capturedFrame =
                tickInput.CopyToPlayerInputFrame(edgeBuffer);
            DomainResult coverMovement = ProcessCoverMovement(
                tickInput,
                tick,
                runtime,
                out bool movementConsumedTick);
            if (!coverMovement.IsSuccess || movementConsumedTick)
            {
                return coverMovement;
            }

            PlayerInputFrame frame = capturedFrame;
            DomainResult gated = TryBuildCoverGatedInput(
                tickInput,
                frame,
                runtime.Player,
                tick,
                out BattleTickInput gatedTickInput,
                out PlayerInputFrame gatedFrame);
            if (!gated.IsSuccess)
            {
                ClearCoverPeekGate();
                return gated;
            }
            frame = gatedFrame;

            DomainResult posture = ApplyPosture(
                runtime.Player,
                frame,
                tick,
                skillExecutionController.RequiresExposureAt(tick),
                runtime.Covers?.CurrentCoverIsDestroyed
                    ?? runtime.Player.Combatant.Barrier <= 0);
            if (!posture.IsSuccess && posture.RejectReason != RejectReason.BarrierDepleted)
            {
                return posture;
            }

            DomainResult skill = skillExecutionController.ProcessFrame(
                frame,
                runtime.Player);
            if (!skill.IsSuccess)
            {
                ClearCoverPeekGate();
                return skill;
            }

            if (stateAtTickStart != WeaponState.Reloading
                && runtime.Player.Weapon.State == WeaponState.Reloading)
            {
                SnapFacingForReloadStart();
                ClearCoverPeekGate();
            }

            PublishActivePresentations(requiresGameplayCommit: false);

            DomainResult events = ProcessSkillEvents(
                runtime,
                gatedTickInput,
                tick,
                roomInteraction: false);
            FinishCoverPeekTick(tick);
            if (!events.IsSuccess)
            {
                ClearCoverPeekGate();
                PublishSkillSequenceFrames();
                ReleaseTerminalPresentationCommits();
                PublishSnapshot(runtime, tick);
                return events;
            }

            PublishSkillPresentationEvents();
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

        public DomainResult ProcessRoomInteractionTick(
            TickIndex tick,
            FpgFormalCombatRuntimeBundle runtime)
        {
            if (!playerConfigured || !tick.IsValid || runtime == null
                || runtime.IsDisposed || inputSource == null
                || captureFault != RejectReason.None)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!skillExecutionController.TryBindExecutionIdAllocator(
                    runtime.SkillExecutionIds,
                    out _))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            ClearCoverPeekGate();

            if (lastProcessedTick.IsValid
                && tick.Value != lastProcessedTick.Value + 1L)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            WeaponState stateAtTickStart = runtime.Player.Weapon.State;
            int ammoAtTickStart = runtime.Player.Weapon.Magazine.Ammo;
            if (runtime.Player.Combatant.IsDead)
            {
                DomainResult interrupted = skillExecutionController.HardInterrupt(
                    tick,
                    runtime.Player.Weapon);
                if (!interrupted.IsSuccess)
                {
                    return interrupted;
                }

                PublishSkillSequenceFrames();
                ReleaseTerminalPresentationCommits();
                lastProcessedTick = tick;
                PublishSnapshot(runtime, tick);
                return DomainResult.Success;
            }

            BattleTickInput tickInput = inputSource.GetTickInput(tick);
            if (!tickInput.IsValid || tickInput.Tick != tick)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            PlayerInputFrame capturedFrame =
                tickInput.CopyToPlayerInputFrame(edgeBuffer);
            PlayerInputFrame frame =
                FilterRoomInteractionFrame(capturedFrame, tick);
            DomainResult posture = ApplyPosture(
                runtime.Player,
                frame,
                tick,
                skillExecutionController.RequiresExposureAt(tick),
                runtime.Covers?.CurrentCoverIsDestroyed
                    ?? runtime.Player.Combatant.Barrier <= 0);
            if (!posture.IsSuccess
                && posture.RejectReason != RejectReason.BarrierDepleted)
            {
                return posture;
            }

            DomainResult skill = skillExecutionController.ProcessFrame(
                frame,
                runtime.Player);
            if (!skill.IsSuccess)
            {
                return skill;
            }

            if (stateAtTickStart != WeaponState.Reloading
                && runtime.Player.Weapon.State == WeaponState.Reloading)
            {
                SnapFacingForReloadStart();
            }

            PublishActivePresentations(requiresGameplayCommit: false);

            DomainResult events = ProcessSkillEvents(
                runtime,
                tickInput,
                tick,
                roomInteraction: true);
            if (!events.IsSuccess)
            {
                PublishSkillSequenceFrames();
                ReleaseTerminalPresentationCommits();
                return events;
            }

            PublishSkillPresentationEvents();
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

        public bool TryGetShootingDiagnostics(
            out FpgShootingDiagnosticsSnapshot snapshot,
            out string error)
        {
            snapshot = default(FpgShootingDiagnosticsSnapshot);
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!playerConfigured || runtime == null || runtime.IsDisposed
                || runtime.Player == null || runtime.Player.Weapon == null)
            {
                error = "Shooting diagnostics require an active formal player runtime.";
                return false;
            }

            if (!presentationSource.TryGetPlayerPresentationSnapshot(
                    out FpgFormalPlayerPresentationSnapshot presentation)
                && !TryRefreshPresentationSnapshot(out presentation))
            {
                error = "Shooting diagnostics require a valid player presentation snapshot.";
                return false;
            }

            FpgResolvedAimContext aim = ResolvedAimContext;
            if (!presentation.Tick.IsValid || !aim.IsValid)
            {
                error = !presentation.Tick.IsValid
                    ? "Shooting diagnostics are waiting for the first simulation tick."
                    : "Shooting diagnostics require a resolved aim context.";
                return false;
            }

            try
            {
                snapshot = new FpgShootingDiagnosticsSnapshot(
                    presentation.Tick.Value,
                    presentation.Ammo,
                    presentation.MagazineCapacity,
                    presentation.WeaponState,
                    presentation.AimIndicatorBaseState,
                    presentation.ExposureState,
                    presentation.ReloadProgress01,
                    presentation.IsCoverPeekRequested,
                    presentation.IsCoverPeekRequested
                        ? presentation.CoverPeekStartedTick.Value
                        : FpgShootingDiagnosticsSnapshot.UnavailableTick,
                    runtime.Player.Weapon.Definition.PrimaryPayloadCount,
                    presentation.PrimarySpreadTangent,
                    liveAimContext,
                    aim,
                    primaryAttackAvailability,
                    secondaryAttackAvailability);
            }
            catch (ArgumentException exception)
            {
                snapshot = default(FpgShootingDiagnosticsSnapshot);
                error = "Shooting diagnostics snapshot is invalid: "
                    + exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
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
            skillExecutionController = null;
            playerEntity = null;
            threeCProfile = null;
            playerSecondaryTriggerMode = default(SecondaryTriggerMode);
            attackQuerySettings = default(UnityAttackQuerySettings);
            ClearShootingPreview();
            aimAnchor = null;
            shotOrigin = null;
            playerRoot = null;
            cameraFeedback = null;
            SetAimSolution(FpgFormalAimSolution.Idle);
            playerConfigured = false;
            inputSource = null;
            presentationSource.Clear();
        }

        private void UpdateReloadAttackQueue(
            FpgFormalCombatRuntimeBundle runtime,
            TickIndex tick)
        {
            if (runtime == null || runtime.IsDisposed || inputSource == null
                || skillExecutionController == null)
            {
                queuedAttackAfterReload = FpgPlayerSkillSlot.None;
                return;
            }

            WeaponRuntime weapon = runtime.Player.Weapon;
            bool reloadActive = weapon.State == WeaponState.Reloading
                || skillExecutionController.IsExecuting
                    && skillExecutionController.ActiveSlot
                        == FpgPlayerSkillSlot.Reload;
            if (reloadActive)
            {
                queuedAttackAfterReload = inputSource.SecondaryHeld
                    ? FpgPlayerSkillSlot.Secondary
                    : inputSource.PrimaryHeld
                        ? FpgPlayerSkillSlot.Primary
                        : FpgPlayerSkillSlot.None;
                return;
            }

            if (queuedAttackAfterReload == FpgPlayerSkillSlot.Secondary)
            {
                if (!inputSource.SecondaryHeld)
                {
                    queuedAttackAfterReload = FpgPlayerSkillSlot.None;
                }
                else if (!skillExecutionController.IsExecuting
                    && ResolveAttackAvailability(
                        FpgPlayerSkillSlot.Secondary,
                        runtime,
                        tick,
                        liveAttackAimContext).Ready
                    && inputSource.TryEnqueueSyntheticEdge(
                        InputEdgeType.SecondaryPressed))
                {
                    queuedAttackAfterReload = FpgPlayerSkillSlot.None;
                }
            }
            else if (queuedAttackAfterReload == FpgPlayerSkillSlot.Primary
                && (!inputSource.PrimaryHeld
                    || weapon.Magazine.Ammo
                        >= skillExecutionController.GetRequiredAmmo(
                            FpgPlayerSkillSlot.Primary)))
            {
                queuedAttackAfterReload = FpgPlayerSkillSlot.None;
            }

            if (queuedAttackAfterReload != FpgPlayerSkillSlot.None
                || skillExecutionController.IsExecuting
                || weapon.State != WeaponState.Ready)
            {
                return;
            }

            bool secondaryPressedThisTick =
                inputSource.HasQueuedGameplayEdge(InputEdgeType.SecondaryPressed);
            FpgPlayerSkillSlot requestedSlot = inputSource.PrimaryHeld
                ? FpgPlayerSkillSlot.Primary
                : inputSource.SecondaryHeld || secondaryPressedThisTick
                    ? FpgPlayerSkillSlot.Secondary
                    : FpgPlayerSkillSlot.None;
            if (requestedSlot == FpgPlayerSkillSlot.None)
            {
                return;
            }

            FpgAttackAvailability availability = ResolveAttackAvailability(
                requestedSlot,
                runtime,
                tick,
                liveAttackAimContext);
            if (availability.ShouldAutoReload
                && inputSource.TryEnqueueSyntheticEdge(
                    InputEdgeType.ReloadPressed))
            {
                queuedAttackAfterReload = requestedSlot;
            }
        }

        private FpgAttackAvailability ResolveAttackAvailability(
            FpgPlayerSkillSlot slot,
            FpgFormalCombatRuntimeBundle runtime,
            TickIndex tick,
            in FpgResolvedAimContext aim,
            bool finalCommit = false,
            bool roomInteraction = false)
        {
            if (runtime == null || runtime.IsDisposed
                || runtime.Player == null
                || skillExecutionController == null)
            {
                return FpgAttackAvailability.Resolve(
                    slot,
                    false,
                    false,
                    false,
                    false,
                    WeaponState.Disabled,
                    TickIndex.Invalid,
                    tick,
                    0,
                    0,
                    aim,
                    finalCommit);
            }

            PlayerRuntime player = runtime.Player;
            WeaponRuntime weapon = player.Weapon;
            FpgCoverSnapshot cover = runtime.Covers == null
                ? default(FpgCoverSnapshot)
                : runtime.Covers.CurrentSnapshot;
            FpgResolvedAimContext currentAim = aim.WithCurrentCover(
                cover.IsValid ? cover.CoverId : string.Empty);
            TickIndex recast = slot == FpgPlayerSkillSlot.Primary
                ? weapon.PrimaryRecastLockedUntilTick
                : weapon.SecondaryRecastLockedUntilTick;
            FpgEncounterPhase phase = encounterDirector == null
                ? FpgEncounterPhase.None
                : encounterDirector.Phase;
            bool encounterActive = encounterDirector != null
                && IsAttackPhaseActive(
                    phase,
                    encounterDirector.IsPaused,
                    roomInteraction,
                    encounterDirector.HasAvailableExits);
            return FpgAttackAvailability.Resolve(
                slot,
                true,
                player.Combatant.IsDead,
                encounterActive,
                runtime.Covers != null && runtime.Covers.IsTraversing,
                weapon.State,
                recast,
                tick,
                weapon.Magazine.Ammo,
                skillExecutionController.GetRequiredAmmo(slot),
                currentAim,
                finalCommit);
        }

        internal static bool IsAttackPhaseActive(
            FpgEncounterPhase phase,
            bool paused,
            bool roomInteraction,
            bool hasAvailableExits)
        {
            return !paused
                && phase != FpgEncounterPhase.None
                && phase != FpgEncounterPhase.Preparing
                && (phase != FpgEncounterPhase.Cleared
                    || roomInteraction && hasAvailableExits)
                && phase != FpgEncounterPhase.Defeated
                && phase != FpgEncounterPhase.Failed
                && phase != FpgEncounterPhase.Faulted
                && phase != FpgEncounterPhase.Disposed;
        }

        private FpgPlayerFacingDirection ResolveAcceptedAttackDirection(
            in FpgResolvedAimContext aim)
        {
            if (aim.IsValid)
            {
                return FpgPlayerFacingController.ResolveDirection(
                    aim.ReticleViewport.x);
            }

            FpgPlayerBarrierPresentationController barrier =
                playerEntity == null ? null : playerEntity.Barrier;
            if (barrier != null && barrier.HasSelectedPeekTarget)
            {
                return barrier.SelectedPeekDirection;
            }

            FpgPlayerFacingController facing = playerEntity == null
                ? null
                : playerEntity.FacingController;
            return facing != null && facing.IsPrepared
                ? facing.TargetDirection
                : coverPeekDirection;
        }

        private void SnapFacingForReloadStart()
        {
            FpgPlayerFacingController facing = playerEntity == null
                ? null
                : playerEntity.FacingController;
            if (facing == null || !facing.IsPrepared
                || !facing.IsPresentationActive)
            {
                return;
            }

            if (!facing.TryForceDirection(facing.TargetDirection, out _))
            {
                AimPresentationFaultCount++;
            }
        }

        private DomainResult TryCommitCoverPeekDirection(
            FpgPlayerSkillSlot slot,
            FpgPlayerFacingDirection direction,
            in AimPoseSnapshot sourceAimPose,
            TickIndex tick,
            FpgFormalCombatRuntimeBundle runtime,
            out AimPoseSnapshot committedAimPose,
            out bool accepted)
        {
            committedAimPose = sourceAimPose;
            accepted = false;
            if (!sourceAimPose.IsValid || !tick.IsValid
                || runtime == null || runtime.IsDisposed
                || slot == FpgPlayerSkillSlot.None
                || !Enum.IsDefined(
                    typeof(FpgPlayerFacingDirection),
                    direction))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            // Low-level controller fixtures intentionally omit the composed
            // entity. Production composition validates all three components.
            if (playerEntity == null)
            {
                coverPeekDirection = direction;
                accepted = true;
                return DomainResult.Success;
            }

            FpgPlayerFacingController facing =
                playerEntity.FacingController;
            FpgPlayerBarrierPresentationController barrier =
                playerEntity.Barrier;
            FpgCoverSnapshot cover = runtime.Covers == null
                ? default(FpgCoverSnapshot)
                : runtime.Covers.CurrentSnapshot;
            FpgPlayerPeekPresentationState previousPeek =
                barrier == null
                    ? default(FpgPlayerPeekPresentationState)
                    : barrier.CapturePeekState();
            if (facing == null || !facing.IsPrepared
                || !facing.IsPresentationActive
                || barrier == null || !cover.IsValid
                || !barrier.TrySelectPeekTarget(
                    cover.CoverId,
                    direction,
                    out _))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            FpgPlayerFacingTransitionState previousFacing =
                facing.CaptureTransitionState();
            if (!facing.TryForceDirection(direction, out _))
            {
                barrier.RestorePeekState(previousPeek);
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            try
            {
                CaptureAimPose();
            }
            catch (Exception)
            {
                facing.RestoreTransitionState(previousFacing);
                barrier.RestorePeekState(previousPeek);
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            FpgResolvedAimContext finalAim = liveAttackAimContext;
            FpgAttackAvailability finalAvailability =
                ResolveAttackAvailability(
                    slot,
                    runtime,
                    tick,
                    finalAim);
            if (!finalAvailability.Ready
                || !TryCreateRebasedAimPose(
                    tick,
                    finalAim,
                    sourceAimPose,
                    out committedAimPose))
            {
                facing.RestoreTransitionState(previousFacing);
                barrier.RestorePeekState(previousPeek);
                try
                {
                    CaptureAimPose();
                }
                catch (Exception)
                {
                    return DomainResult.Rejected(
                        RejectReason.InvariantFault);
                }

                primaryAttackAvailability = ResolveAttackAvailability(
                    FpgPlayerSkillSlot.Primary,
                    runtime,
                    tick,
                    liveAttackAimContext);
                secondaryAttackAvailability = ResolveAttackAvailability(
                    FpgPlayerSkillSlot.Secondary,
                    runtime,
                    tick,
                    liveAttackAimContext);
                committedAimPose = sourceAimPose;
                return DomainResult.Success;
            }

            coverPeekDirection = direction;
            accepted = true;
            return DomainResult.Success;
        }

        private DomainResult TryBuildCoverGatedInput(
            BattleTickInput capturedTickInput,
            PlayerInputFrame capturedFrame,
            PlayerRuntime player,
            TickIndex tick,
            out BattleTickInput gatedTickInput,
            out PlayerInputFrame gatedFrame)
        {
            gatedTickInput = capturedTickInput;
            gatedFrame = capturedFrame;
            if (capturedFrame.CancelSecondary)
            {
                ClearCoverPeekGate();
                return DomainResult.Success;
            }

            bool activeSkillRequiresExposure = skillExecutionController != null
                && skillExecutionController.RequiresExposureAt(tick);
            if (useFrozenAimForActiveAttack
                && !activeSkillRequiresExposure
                && !HasPendingCoverAttack
                && player.Weapon.State != WeaponState.AltCharging)
            {
                useFrozenAimForActiveAttack = false;
                coverPeekAimFrozen = false;
                coverPeekFrozenAimPose = default(AimPoseSnapshot);
                frozenAimContext = FpgResolvedAimContext.Invalid;
                RefreshAimViewportFreeze();
            }

            FpgResolvedAimContext availabilityAim = frozenAimContext.IsFrozen
                ? frozenAimContext
                : liveAttackAimContext;
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            primaryAttackAvailability = ResolveAttackAvailability(
                FpgPlayerSkillSlot.Primary,
                runtime,
                tick,
                availabilityAim);
            secondaryAttackAvailability = ResolveAttackAvailability(
                FpgPlayerSkillSlot.Secondary,
                runtime,
                tick,
                availabilityAim);

            bool hasPrimaryIntent = capturedFrame.PrimaryHeld;
            bool hasSecondaryIntent = capturedFrame.SecondaryHeld
                || capturedFrame.HasSecondaryInput;
            bool reloadHasPriority = capturedFrame.HasReloadInput
                || skillExecutionController != null
                    && skillExecutionController.HasPendingReloadIntent;
            bool secondaryAlreadyActive = player.Weapon.State
                == WeaponState.AltCharging;
            bool canStartPrimary = !reloadHasPriority && hasPrimaryIntent
                && primaryAttackAvailability.Ready;
            bool canStartSecondary = !reloadHasPriority && hasSecondaryIntent
                && (secondaryAttackAvailability.Ready
                    || secondaryAlreadyActive);
            if (!isCoverPeekRequested
                && (canStartPrimary || canStartSecondary))
            {
                FpgPlayerSkillSlot startingSlot = ResolveStartingAttackSlot(
                    capturedFrame,
                    canStartPrimary,
                    canStartSecondary);
                FpgPlayerFacingDirection startingDirection =
                    ResolveAcceptedAttackDirection(availabilityAim);
                DomainResult facingCommit =
                    TryCommitCoverPeekDirection(
                        startingSlot,
                        startingDirection,
                        capturedTickInput.AimPose,
                        tick,
                        runtime,
                        out AimPoseSnapshot committedAimPose,
                        out bool facingAccepted);
                if (!facingCommit.IsSuccess)
                {
                    return facingCommit;
                }

                if (facingAccepted)
                {
                    capturedTickInput = new BattleTickInput(
                        capturedFrame,
                        committedAimPose);
                    availabilityAim = liveAttackAimContext.IsValid
                        ? liveAttackAimContext
                        : availabilityAim;
                    primaryAttackAvailability = ResolveAttackAvailability(
                        FpgPlayerSkillSlot.Primary,
                        runtime,
                        tick,
                        availabilityAim);
                    secondaryAttackAvailability = ResolveAttackAvailability(
                        FpgPlayerSkillSlot.Secondary,
                        runtime,
                        tick,
                        availabilityAim);
                    canStartPrimary = hasPrimaryIntent
                        && primaryAttackAvailability.Ready;
                    canStartSecondary = hasSecondaryIntent
                        && (secondaryAttackAvailability.Ready
                            || secondaryAlreadyActive);
                }
                else
                {
                    canStartPrimary = false;
                    canStartSecondary = false;
                }
            }
            bool continueHeldPrimary = hasPrimaryIntent
                && player.Weapon.State == WeaponState.PrimaryRecovery
                && skillExecutionController != null
                && skillExecutionController.IsExecuting
                && skillExecutionController.ActiveSlot
                    == FpgPlayerSkillSlot.Primary
                && skillExecutionController.ActiveSequenceKind
                    == FpgSkillSequenceKind.Execute
                && skillExecutionController.ActiveTiming.IsValid
                && skillExecutionController.ActiveTiming
                    .UsesCharacterAttackSpeed;
            bool peekRequested = canStartPrimary
                || continueHeldPrimary
                || canStartSecondary
                || activeSkillRequiresExposure
                || secondaryAlreadyActive
                || HasPendingCoverAttack
                || useFrozenAimForActiveAttack;
            if (!peekRequested)
            {
                ClearCoverPeekGate();
                gatedFrame = FilterAttackInputs(
                    capturedFrame,
                    tick,
                    preserveReload: true);
                gatedTickInput = new BattleTickInput(
                    gatedFrame,
                    capturedTickInput.AimPose);
                return DomainResult.Success;
            }

            if (!coverPeekStartedTick.IsValid)
            {
                coverPeekStartedTick = tick;
                FreezeCoverPeekAim(capturedTickInput.AimPose);
            }
            isCoverPeekRequested = true;

            bool attackReleasedThisTick =
                (coverPeekPrimaryPending && !capturedFrame.PrimaryHeld)
                || ContainsInputEdge(
                    capturedFrame,
                    InputEdgeType.SecondaryReleased);
            bool attackStillHeld = capturedFrame.PrimaryHeld
                || capturedFrame.SecondaryHeld;
            if ((attackReleasedThisTick || !attackStillHeld
                    && HasPendingCoverAttack)
                && !coverPeekAimFrozen)
            {
                FreezeCoverPeekAim(capturedTickInput.AimPose);
            }

            bool fullyPeeked = tick.Value - coverPeekStartedTick.Value
                >= coverPeekGateTickCount;
            int frameEdgeCount = 0;
            bool deliveredPendingAttack = false;
            if (!fullyPeeked)
            {
                coverPeekPrimaryPending |= canStartPrimary;
                for (int index = 0;
                    index < capturedFrame.EdgeCommandCount;
                    index++)
                {
                    InputEdgeCommand edge = capturedFrame.EdgeCommands[index];
                    if (edge.Type == InputEdgeType.SecondaryPressed
                        || edge.Type == InputEdgeType.SecondaryReleased)
                    {
                        if ((canStartSecondary || secondaryAlreadyActive
                                || coverPeekPendingEdgeCount > 0)
                            && !TryEnqueueCoverPeekEdge(edge))
                        {
                            return DomainResult.Rejected(
                                RejectReason.BufferCapacity);
                        }
                    }
                    else
                    {
                        coverPeekFrameEdges[frameEdgeCount++] = edge;
                    }
                }
            }
            else
            {
                int drainedCount = DrainCoverPeekEdges(
                    coverPeekFrameEdges,
                    BattleTickInput.MaxEdgeCommandCount);
                frameEdgeCount = drainedCount;
                deliveredPendingAttack = drainedCount > 0
                    || coverPeekPrimaryPending;
                for (int index = 0;
                    index < capturedFrame.EdgeCommandCount;
                    index++)
                {
                    InputEdgeCommand edge = capturedFrame.EdgeCommands[index];
                    if (frameEdgeCount < BattleTickInput.MaxEdgeCommandCount)
                    {
                        coverPeekFrameEdges[frameEdgeCount++] = edge;
                    }
                    else if (!TryEnqueueCoverPeekEdge(edge))
                    {
                        return DomainResult.Rejected(
                            RejectReason.BufferCapacity);
                    }
                }
            }

            bool gatedPrimaryHeld = fullyPeeked
                && (canStartPrimary
                    || continueHeldPrimary
                    || coverPeekPrimaryPending);
            bool gatedSecondaryHeld = fullyPeeked
                && (canStartSecondary || secondaryAlreadyActive)
                && capturedFrame.SecondaryHeld;
            if (fullyPeeked)
            {
                coverPeekPrimaryPending = false;
            }

            if (!attackStillHeld
                && HasPendingCoverAttack
                && !coverPeekAimFrozen)
            {
                FreezeCoverPeekAim(capturedTickInput.AimPose);
            }

            gatedFrame = new PlayerInputFrame(
                tick,
                aimHeld: capturedFrame.AimHeld,
                primaryHeld: gatedPrimaryHeld,
                edgeCommands: frameEdgeCount == 0
                    ? null
                    : coverPeekFrameEdges,
                edgeCommandCount: frameEdgeCount,
                cancelSecondary: false,
                secondaryHeld: gatedSecondaryHeld);

            AimPoseSnapshot gatedAimPose = capturedTickInput.AimPose;
            if ((useFrozenAimForActiveAttack || deliveredPendingAttack)
                && coverPeekAimFrozen)
            {
                if (!TryCreateRebasedAimPose(
                        tick,
                        frozenAimContext,
                        coverPeekFrozenAimPose,
                        out gatedAimPose))
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                useFrozenAimForActiveAttack = true;
            }
            gatedTickInput = new BattleTickInput(gatedFrame, gatedAimPose);
            return DomainResult.Success;
        }

        private FpgPlayerSkillSlot ResolveStartingAttackSlot(
            in PlayerInputFrame frame,
            bool canStartPrimary,
            bool canStartSecondary)
        {
            bool hasCurrentSecondaryEdge = ContainsInputEdge(
                    frame,
                    InputEdgeType.SecondaryPressed)
                || ContainsInputEdge(frame, InputEdgeType.SecondaryReleased);
            if (canStartSecondary && hasCurrentSecondaryEdge)
            {
                return FpgPlayerSkillSlot.Secondary;
            }

            if (skillExecutionController != null
                && skillExecutionController.HasPendingAttackIntent)
            {
                FpgPlayerSkillSlot pendingSlot =
                    skillExecutionController.PendingAttackIntent.Slot;
                if (pendingSlot == FpgPlayerSkillSlot.Primary
                    && canStartPrimary
                    || pendingSlot == FpgPlayerSkillSlot.Secondary
                        && canStartSecondary)
                {
                    return pendingSlot;
                }
            }

            return canStartPrimary
                ? FpgPlayerSkillSlot.Primary
                : FpgPlayerSkillSlot.Secondary;
        }

        private PlayerInputFrame FilterAttackInputs(
            PlayerInputFrame frame,
            TickIndex tick,
            bool preserveReload)
        {
            int count = 0;
            for (int index = 0; index < frame.EdgeCommandCount; index++)
            {
                InputEdgeCommand edge = frame.EdgeCommands[index];
                if (preserveReload && edge.Type == InputEdgeType.ReloadPressed)
                {
                    coverPeekFrameEdges[count++] = edge;
                }
            }

            return new PlayerInputFrame(
                tick,
                frame.AimHeld,
                primaryHeld: false,
                edgeCommands: count == 0 ? null : coverPeekFrameEdges,
                edgeCommandCount: count,
                cancelSecondary: frame.CancelSecondary,
                secondaryHeld: false);
        }

        private bool HasPendingCoverAttack => coverPeekPrimaryPending
            || coverPeekPendingEdgeCount > 0;

        private static bool ContainsInputEdge(
            PlayerInputFrame frame,
            InputEdgeType edgeType)
        {
            for (int index = 0; index < frame.EdgeCommandCount; index++)
            {
                if (frame.EdgeCommands[index].Type == edgeType)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryEnqueueCoverPeekEdge(InputEdgeCommand edge)
        {
            if (coverPeekPendingEdgeCount >= coverPeekPendingEdges.Length)
            {
                return false;
            }

            coverPeekPendingEdges[coverPeekPendingEdgeCount++] = edge;
            return true;
        }

        public bool TryApplyShootingPreview(
            in FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            if (!playerConfigured || playerDefinition == null
                || threeCProfile == null)
            {
                error = "Shooting preview requires a configured formal player.";
                return false;
            }

            if (!snapshot.TryValidate(out error)
                || !ReferenceEquals(playerDefinition, snapshot.Character)
                || !ReferenceEquals(threeCProfile, snapshot.ThreeCProfile)
                || playerSecondaryTriggerMode
                    != snapshot.SecondaryTriggerMode)
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Shooting preview does not match the configured player."
                    : error;
                return false;
            }

            if (aimViewportSource is CombatAimReticle reticle
                && !reticle.TryApplyShootingPreview(snapshot, out error))
            {
                return false;
            }

            shootingPreview = snapshot;
            hasShootingPreview = true;
            inputBufferTicks = snapshot.InputBufferTicks;
            coverPeekGateTickCount = Mathf.Clamp(
                TickDuration.FromSeconds(snapshot.PeekTransitionSeconds).Value,
                0,
                MaximumCoverPeekGateTickCount);
            inputSource?.ConfigureInputBufferTicks(inputBufferTicks);
            error = string.Empty;
            return true;
        }

        public void ClearShootingPreview()
        {
            shootingPreview = default(FpgShootingTuningSnapshot);
            hasShootingPreview = false;
        }

        private int DrainCoverPeekEdges(
            InputEdgeCommand[] destination,
            int destinationCapacity)
        {
            int count = Math.Min(coverPeekPendingEdgeCount, destinationCapacity);
            if (count <= 0)
            {
                return 0;
            }

            Array.Copy(coverPeekPendingEdges, 0, destination, 0, count);
            int remaining = coverPeekPendingEdgeCount - count;
            if (remaining > 0)
            {
                Array.Copy(
                    coverPeekPendingEdges,
                    count,
                    coverPeekPendingEdges,
                    0,
                    remaining);
            }
            Array.Clear(coverPeekPendingEdges, remaining, count);
            coverPeekPendingEdgeCount = remaining;
            return count;
        }

        private void FreezeCoverPeekAim(AimPoseSnapshot aimPose)
        {
            if (!aimPose.IsValid || !liveAttackAimContext.IsValid)
            {
                return;
            }

            coverPeekFrozenAimPose = aimPose;
            coverPeekAimFrozen = true;
            useFrozenAimForActiveAttack = true;
            frozenAimContext = liveAttackAimContext.Freeze();
            if (TryCreateRebasedAimPose(
                    aimPose.Tick,
                    frozenAimContext,
                    aimPose,
                    out AimPoseSnapshot rebasedPose))
            {
                coverPeekFrozenAimPose = rebasedPose;
            }

            RefreshAimViewportFreeze();
            SetAimSolution(
                frozenAimContext.IsReticleEnemy
                    || frozenAimContext.IsCurrentCoverBlocked
                    ? FpgFormalAimSolution.FromContext(frozenAimContext)
                    : FpgFormalAimSolution.Idle);
        }

        private static AimPoseSnapshot RetickAimPose(
            AimPoseSnapshot aimPose,
            TickIndex tick)
        {
            return new AimPoseSnapshot(
                tick,
                aimPose.Origin,
                aimPose.Forward,
                aimPose.Right,
                aimPose.Up,
                aimPose.PoseVersion);
        }

        private static bool TryCreateRebasedAimPose(
            TickIndex tick,
            in FpgResolvedAimContext context,
            in AimPoseSnapshot sourcePose,
            out AimPoseSnapshot pose)
        {
            pose = default(AimPoseSnapshot);
            if (!tick.IsValid || !context.IsValid || !sourcePose.IsValid)
            {
                return false;
            }

            Vector3 referenceUp = new Vector3(
                sourcePose.Up.X / (float)SpatialContract.DirectionUnits,
                sourcePose.Up.Y / (float)SpatialContract.DirectionUnits,
                sourcePose.Up.Z / (float)SpatialContract.DirectionUnits);
            Vector3 forward = context.CenterDirection.normalized;
            if (!TryQuantizePosition(context.ShotOrigin, out SpatialVectorKey origin)
                || !TryQuantizeDirection(forward, referenceUp, out SpatialVectorKey quantizedForward, out SpatialVectorKey right, out SpatialVectorKey up))
            {
                return false;
            }

            pose = new AimPoseSnapshot(
                tick,
                origin,
                quantizedForward,
                right,
                up,
                sourcePose.PoseVersion);
            return pose.IsValid;
        }

        private void FinishCoverPeekTick(TickIndex tick)
        {
            WeaponRuntime weapon = encounterDirector?.CombatRuntime?.Player?.Weapon;
            if (!useFrozenAimForActiveAttack
                || skillExecutionController.RequiresExposureAt(tick)
                || weapon?.State == WeaponState.AltCharging)
            {
                return;
            }

            useFrozenAimForActiveAttack = false;
            if (!HasPendingCoverAttack)
            {
                coverPeekAimFrozen = false;
                coverPeekFrozenAimPose = default(AimPoseSnapshot);
                frozenAimContext = FpgResolvedAimContext.Invalid;
                RefreshAimViewportFreeze();
            }
        }

        private void ClearCoverPeekGate()
        {
            Array.Clear(
                coverPeekFrameEdges,
                0,
                coverPeekFrameEdges.Length);
            Array.Clear(
                coverPeekPendingEdges,
                0,
                coverPeekPendingEdges.Length);
            isCoverPeekRequested = false;
            coverPeekPrimaryPending = false;
            coverPeekAimFrozen = false;
            useFrozenAimForActiveAttack = false;
            coverPeekPendingEdgeCount = 0;
            coverPeekStartedTick = TickIndex.Invalid;
            coverPeekFrozenAimPose = default(AimPoseSnapshot);
            coverPeekDirection = FpgPlayerFacingDirection.Right;
            frozenAimContext = FpgResolvedAimContext.Invalid;
            RefreshAimViewportFreeze();
            FpgResolvedAimContext currentAim =
                liveAttackAimContext.IsValid
                    ? liveAttackAimContext
                    : liveAimContext;
            if (currentAim.IsValid)
            {
                SetAimSolution(
                    currentAim.IsReticleEnemy
                        || currentAim.IsCurrentCoverBlocked
                        ? FpgFormalAimSolution.FromContext(currentAim)
                        : FpgFormalAimSolution.Idle);
            }
        }

        private PlayerInputFrame FilterRoomInteractionFrame(
            PlayerInputFrame captured,
            TickIndex tick)
        {
            if (roomInteractionArmed)
            {
                return captured;
            }

            int retainedCount = 0;
            for (int index = 0; index < captured.EdgeCommandCount; index++)
            {
                InputEdgeCommand edge = captured.EdgeCommands[index];
                if (edge.Type == InputEdgeType.ReloadPressed)
                {
                    edgeBuffer[retainedCount++] = edge;
                }
            }

            if (!captured.PrimaryHeld && !captured.SecondaryHeld)
            {
                roomInteractionArmed = true;
            }

            return new PlayerInputFrame(
                tick,
                captured.AimHeld,
                false,
                retainedCount == 0 ? null : edgeBuffer,
                retainedCount,
                cancelSecondary: true,
                secondaryHeld: false);
        }

        private DomainResult ProcessSkillEvents(
            FpgFormalCombatRuntimeBundle runtime,
            BattleTickInput tickInput,
            TickIndex tick,
            bool roomInteraction)
        {
            reloadCompletionActionPublishedThisTick = false;
            ulong playerSeed = runtime.RunContext.DeriveSeed(
                PlayerAttackRandomDomain);
            for (int index = 0;
                index < skillExecutionController.ResultCount;
                index++)
            {
                FpgPlayerSkillExecutionEvent skillEvent =
                    skillExecutionController.GetResult(index);
                if (skillEvent.Outcome != FpgSkillEventOutcome.Triggered
                    || !skillEvent.HasGameplayAction)
                {
                    continue;
                }

                if (skillEvent.Action.Kind
                    == FpgPlayerSkillActionKind.ReloadCommit)
                {
                    if (skillEvent.Event.TargetSource
                            != FpgSkillTargetSource.Self)
                    {
                        skillExecutionController.AbortAfterProcessedTick(
                            runtime.Player.Weapon);
                        return DomainResult.Rejected(
                            RejectReason.InvalidDefinition);
                    }

                    WeaponState stateBeforeReload =
                        runtime.Player.Weapon.State;
                    int ammoBeforeReload =
                        runtime.Player.Weapon.Magazine.Ammo;
                    DomainResult reloadCommitted =
                        runtime.Player.Weapon.CommitSkillReloadEvent(tick);
                    if (!reloadCommitted.IsSuccess)
                    {
                        skillExecutionController.AbortAfterProcessedTick(
                            runtime.Player.Weapon);
                        return reloadCommitted;
                    }

                    RecordSkillGameplayCommit(
                        runtime,
                        tick,
                        skillEvent,
                        AttackId.Invalid,
                        ammoBeforeReload,
                        runtime.Player.Weapon.Magazine.Ammo);
                    PublishAction(
                        tick,
                        FpgFormalPlayerActionType.ReloadCompleted,
                        WeaponReleaseKind.None,
                        AttackId.Invalid,
                        stateBeforeReload,
                        runtime.Player.Weapon.State,
                        ammoBeforeReload,
                        runtime.Player.Weapon.Magazine.Ammo,
                        skillEvent.RuntimeEvent.ExecutionId,
                        skillEvent.Event.EventId);
                    reloadCompletionActionPublishedThisTick = true;
                    RecordPresentationCommit(skillEvent);
                    continue;
                }

                if (skillEvent.Event.TargetSource
                        != FpgSkillTargetSource.CurrentAim
                    || (skillEvent.Slot != FpgPlayerSkillSlot.Primary
                        && skillEvent.Slot != FpgPlayerSkillSlot.Secondary))
                {
                    skillExecutionController.AbortAfterProcessedTick(
                        runtime.Player.Weapon);
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                }

                WeaponReleaseKind releaseKind =
                    skillEvent.Slot == FpgPlayerSkillSlot.Primary
                        ? WeaponReleaseKind.Primary
                        : WeaponReleaseKind.Secondary;
                FpgResolvedAimContext commitAim =
                    liveAttackAimContext.Freeze();
                FpgAttackAvailability finalAvailability =
                    ResolveAttackAvailability(
                        skillEvent.Slot,
                        runtime,
                        tick,
                        commitAim,
                        finalCommit: true,
                        roomInteraction: roomInteraction);
                if (!finalAvailability.Ready)
                {
                    skillExecutionController.AbortAfterProcessedTick(
                        runtime.Player.Weapon);
                    ClearCoverPeekGate();
                    bool currentCoverDepleted = runtime.Covers
                        ?.CurrentCoverIsDestroyed
                        ?? runtime.Player.Combatant.Barrier <= 0;
                    DomainResult withdrawn = runtime.Player.Exposure
                        .ApplyCombatPosture(
                            shouldExpose: currentCoverDepleted,
                            currentTick: tick,
                            barrierDepleted: currentCoverDepleted,
                            changed: out _);
                    if (!withdrawn.IsSuccess
                        && withdrawn.RejectReason
                            != RejectReason.BarrierDepleted)
                    {
                        return withdrawn;
                    }

                    return DomainResult.Success;
                }

                if (!TryCreateRebasedAimPose(
                        tick,
                        commitAim,
                        tickInput.AimPose,
                        out AimPoseSnapshot commitAimPose))
                {
                    skillExecutionController.AbortAfterProcessedTick(
                        runtime.Player.Weapon);
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                WeaponSkillReleaseSpec releaseSpec =
                    new WeaponSkillReleaseSpec(
                        releaseKind,
                        skillEvent.Action.Damage,
                        skillEvent.Action.QueryPolicy,
                        skillEvent.Action.QueryMode,
                        skillEvent.Action.PayloadCount,
                        skillEvent.Action.MaxImpactCount,
                        skillEvent.Action.AmmoCost,
                        skillEvent.Action.AdditionalPenetrationCount,
                        skillEvent.Action.AreaCombatantLimit,
                        skillEvent.Action.AreaProjectileLimit,
                        skillEvent.Action.AllowedTargetKinds);
                WeaponState stateBeforeRelease = runtime.Player.Weapon.State;
                int ammoBeforeRelease = runtime.Player.Weapon.Magazine.Ammo;
                WeaponRuntimeSnapshot weaponSnapshot =
                    runtime.Player.Weapon.CaptureRoomSnapshot();
                DomainResult prepared = runtime.Player.Weapon.PrepareSkillRelease(
                    tick,
                    runtime.Player.RuntimeId,
                    runtime.IdAllocator,
                    playerSeed,
                    releaseSpec,
                    weaponRelease);
                if (!prepared.IsSuccess)
                {
                    skillExecutionController.AbortAfterProcessedTick(
                        runtime.Player.Weapon);
                    return prepared;
                }

                BattleTickInput eventTickInput;
                try
                {
                    BattleTickInput commitTickInput = new BattleTickInput(
                        tickInput.CopyToPlayerInputFrame(edgeBuffer),
                        commitAimPose);
                    eventTickInput = ApplySkillOffset(
                        commitTickInput,
                        skillEvent.Event.Offset);
                }
                catch (Exception exception) when (
                    exception is ArgumentException
                    || exception is OverflowException)
                {
                    weaponRelease.Reset();
                    skillExecutionController.AbortAfterProcessedTick(
                        runtime.Player.Weapon);
                    return DomainResult.Rejected(RejectReason.InvalidDefinition);
                }

                DomainResult committed = roomInteraction
                    ? QueryAndCommitRoomInteraction(
                        runtime,
                        eventTickInput,
                        tick,
                        skillEvent)
                    : skillEvent.Action.Kind
                            == FpgPlayerSkillActionKind
                                .ProjectileAreaAtFirstSurface
                        ? SpawnAndCommitPlayerAreaProjectile(
                            runtime,
                            eventTickInput,
                            tick,
                            skillEvent)
                        : QueryAndSubmitHits(
                            runtime,
                            eventTickInput,
                            tick,
                            skillEvent);
                if (!committed.IsSuccess)
                {
                    (runtime.PlayerShotPresentationSink
                        as IUncommittedPlayerShotPresentationSink)
                        ?.DiscardUncommittedShot(
                            weaponRelease.Attack.AttackId);
                    if (!weaponRelease.IsCommitted)
                    {
                        DomainResult restored =
                            runtime.Player.Weapon.RestoreRoomSnapshot(
                                weaponSnapshot);
                        weaponRelease.Reset();
                        if (!restored.IsSuccess)
                        {
                            skillExecutionController.AbortAfterProcessedTick(
                                runtime.Player.Weapon);
                            return DomainResult.Rejected(
                                RejectReason.InvariantFault);
                        }
                    }

                    skillExecutionController.AbortAfterProcessedTick(
                        runtime.Player.Weapon);
                    return committed;
                }

                RecordSkillGameplayCommit(
                    runtime,
                    tick,
                    skillEvent,
                    weaponRelease.Attack.AttackId,
                    ammoBeforeRelease,
                    runtime.Player.Weapon.Magazine.Ammo);
                PublishAction(
                    tick,
                    releaseKind == WeaponReleaseKind.Secondary
                        ? FpgFormalPlayerActionType.SecondaryReleaseCommitted
                        : FpgFormalPlayerActionType.PrimaryReleaseCommitted,
                    releaseKind,
                    weaponRelease.Attack.AttackId,
                    stateBeforeRelease,
                    runtime.Player.Weapon.State,
                    ammoBeforeRelease,
                    runtime.Player.Weapon.Magazine.Ammo,
                    skillEvent.RuntimeEvent.ExecutionId,
                    skillEvent.Event.EventId);
                if (skillEvent.Event.ActionKind
                    == FpgSkillActionKind.Attack)
                {
                    runtime.PlayerShotPresentationSink
                        ?.PublishCommittedShot(
                            weaponRelease.Attack.AttackId,
                            releaseKind);
                }
                RecordPresentationCommit(skillEvent);
                weaponRelease.Reset();
            }

            return DomainResult.Success;
        }

        private void PublishSkillPresentationEvents()
        {
            PublishSkillSequenceFrames();
            PublishActivePresentations(requiresGameplayCommit: true);
            ReleaseTerminalPresentationCommits();
        }

        private void PublishActivePresentations(bool requiresGameplayCommit)
        {
            for (int index = 0;
                index < skillExecutionController.ResultCount;
                index++)
            {
                FpgPlayerSkillExecutionEvent skillEvent =
                    skillExecutionController.GetResult(index);
                if (skillEvent.Outcome != FpgSkillEventOutcome.Triggered
                    || skillEvent.Event.Kind
                        != FpgSkillEventKind.ActivePresentation)
                {
                    continue;
                }

                bool isBound =
                    FpgSkillPresentationCommitRules
                        .RequiresSuccessfulGameplayCommit(skillEvent.Event);
                if (isBound != requiresGameplayCommit)
                {
                    continue;
                }

                bool commitSucceeded = isBound
                    && presentationCommitCache.WasSuccessful(
                        skillEvent.RuntimeEvent.ExecutionId,
                        skillEvent.Event.BoundGameplayEventId);
                if (!FpgSkillPresentationCommitRules.CanPresent(
                        skillEvent.Event,
                        commitSucceeded))
                {
                    continue;
                }

                presentationSource.PublishActivePresentation(skillEvent);
            }
        }

        private void RecordPresentationCommit(
            in FpgPlayerSkillExecutionEvent skillEvent)
        {
            if (!presentationCommitCache.TryRecordSuccess(
                skillEvent.RuntimeEvent.ExecutionId,
                skillEvent.Event.EventId))
            {
                SkillPresentationFaultCount++;
            }
        }

        private void ReleaseTerminalPresentationCommits()
        {
            for (int index = 0;
                index < skillExecutionController.SequenceFrameCount;
                index++)
            {
                FpgPlayerSkillSequenceFrame frame =
                    skillExecutionController.GetSequenceFrame(index);
                if (frame.IsTerminal)
                {
                    presentationCommitCache.ReleaseExecution(
                        frame.ExecutionId);
                }
            }
        }

        private void PublishSkillSequenceFrames()
        {
            if (skillExecutionController == null)
            {
                return;
            }

            for (int index = 0;
                index < skillExecutionController.SequenceFrameCount;
                index++)
            {
                FpgPlayerSkillSequenceFrame frame =
                    skillExecutionController.GetSequenceFrame(index);
                FpgPlayerSkillDefinition authored =
                    ResolveAuthoredSkill(frame.Slot);
                if (!FpgPlayerSkillPresentationResolver
                    .TryResolveAnimationName(
                        authored,
                        frame.Sequence.Kind,
                        frame.ResolvedAnimationId,
                        out string animationName))
                {
                    SkillPresentationFaultCount++;
                    continue;
                }

                presentationSource.PublishSkillSequence(
                    frame,
                    animationName);
            }
        }

        private FpgPlayerSkillDefinition ResolveAuthoredSkill(
            FpgPlayerSkillSlot slot)
        {
            D0WeaponDefinition weapon = playerDefinition == null
                ? null
                : playerDefinition.Weapon;
            if (weapon == null)
            {
                return null;
            }

            switch (slot)
            {
                case FpgPlayerSkillSlot.Primary:
                    return weapon.PrimarySkill;
                case FpgPlayerSkillSlot.Secondary:
                    return weapon.TryResolveSecondarySkill(
                        playerSecondaryTriggerMode,
                        out FpgPlayerSkillDefinition secondary,
                        out _)
                        ? secondary
                        : null;
                case FpgPlayerSkillSlot.Reload:
                    return weapon.ReloadSkill;
                default:
                    return null;
            }
        }

        private static bool TryValidatePresentationMappings(
            FpgPlayerSkillDefinition authored,
            FpgCompiledPlayerSkillDefinition compiled,
            out string error)
        {
            if (authored == null || compiled == null)
            {
                error = "Player skill presentation mapping requires authored and compiled definitions.";
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < compiled.Timeline.SequenceCount;
                sequenceIndex++)
            {
                FpgCompiledSkillSequence sequence =
                    compiled.Timeline.GetSequence(sequenceIndex);
                for (int animationIndex = 0;
                    animationIndex < sequence.AnimationVariantCount;
                    animationIndex++)
                {
                    if (!FpgPlayerSkillPresentationResolver
                        .TryResolveAnimationName(
                            authored,
                            sequence.Kind,
                            sequence.GetAnimationVariant(animationIndex),
                            out _))
                    {
                        error = $"Player skill '{authored.SkillId}' cannot resolve a compiled animation for sequence {sequence.Kind}.";
                        return false;
                    }
                }

            }

            error = string.Empty;
            return true;
        }

        private BattleTickInput ApplySkillOffset(
            BattleTickInput tickInput,
            FpgSkillOffset offset)
        {
            if (offset.Equals(default(FpgSkillOffset)))
            {
                return tickInput;
            }

            AimPoseSnapshot pose = tickInput.AimPose;
            SpatialVectorKey origin = new SpatialVectorKey(
                ApplySkillOffsetAxis(
                    pose.Origin.X,
                    pose.Right.X,
                    pose.Up.X,
                    pose.Forward.X,
                    offset),
                ApplySkillOffsetAxis(
                    pose.Origin.Y,
                    pose.Right.Y,
                    pose.Up.Y,
                    pose.Forward.Y,
                    offset),
                ApplySkillOffsetAxis(
                    pose.Origin.Z,
                    pose.Right.Z,
                    pose.Up.Z,
                    pose.Forward.Z,
                    offset));
            AimPoseSnapshot adjustedPose = new AimPoseSnapshot(
                pose.Tick,
                origin,
                pose.Forward,
                pose.Right,
                pose.Up,
                pose.PoseVersion);
            return new BattleTickInput(
                tickInput.CopyToPlayerInputFrame(edgeBuffer),
                adjustedPose);
        }

        private static int ApplySkillOffsetAxis(
            int origin,
            int right,
            int up,
            int forward,
            FpgSkillOffset offset)
        {
            long displaced = checked(
                (long)origin
                + (long)right * offset.XMillimeters
                    / SpatialContract.DirectionUnits
                + (long)up * offset.YMillimeters
                    / SpatialContract.DirectionUnits
                + (long)forward * offset.ZMillimeters
                    / SpatialContract.DirectionUnits);
            return checked((int)displaced);
        }

        private DomainResult SpawnAndCommitPlayerAreaProjectile(
            FpgFormalCombatRuntimeBundle runtime,
            BattleTickInput tickInput,
            TickIndex tick,
            in FpgPlayerSkillExecutionEvent skillEvent)
        {
            if (!tickInput.AimPose.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            SpatialVectorKey start = tickInput.AimPose.Origin;

            DomainResult aimed = runtime.AttackQueryPort.TryGetAimRangeEndpoint(
                tickInput,
                out SpatialVectorKey end);
            if (!aimed.IsSuccess)
            {
                return aimed;
            }

            ProjectileDefinition projectileDefinition;
            FpgPlayerAreaProjectileRequest request;
            try
            {
                int flightTicks = skillEvent.Action.ProjectileFlightTicks;
                projectileDefinition = new ProjectileDefinition(
                    skillEvent.Action.ProjectileDefinitionId,
                    new TickDuration(flightTicks),
                    new TickDuration(
                        skillEvent.Action.ProjectileLifetimeTicks),
                    skillEvent.Action.Damage,
                    maxHitPoints: skillEvent.Action.ProjectileMaxHitPoints,
                    interceptable:
                        skillEvent.Action.ProjectileInterceptable,
                    budgetUnits: skillEvent.Action.ProjectileBudgetUnits,
                    sweepRadiusKey: skillEvent.Action.ProjectileSweepRadiusKey);
                request = new FpgPlayerAreaProjectileRequest(
                    tick,
                    weaponRelease.Attack,
                    projectileDefinition,
                    start,
                    end,
                    skillEvent.RuntimeEvent.ExecutionId,
                    skillEvent.Event.EventId);
            }
            catch (Exception)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult spawned = runtime.CombatPort.TrySpawnPlayerAreaProjectile(
                request,
                out RuntimeId projectileRuntimeId);
            if (!spawned.IsSuccess)
            {
                return spawned;
            }

            DomainResult committed = runtime.Player.Weapon.CommitPreparedSkillRelease(
                weaponRelease,
                runtime.IdAllocator,
                ResolveResolvedRecastReadyTick(skillEvent.Timing));
            if (committed.IsSuccess)
            {
                return DomainResult.Success;
            }

            DomainResult cancelled = runtime.CombatPort.TryCancelPlayerAreaProjectile(
                projectileRuntimeId,
                tick);
            return cancelled.IsSuccess
                ? DomainResult.Rejected(RejectReason.InvariantFault)
                : cancelled;
        }

        private DomainResult QueryAndSubmitHits(
            FpgFormalCombatRuntimeBundle runtime,
            BattleTickInput tickInput,
            TickIndex tick,
            in FpgPlayerSkillExecutionEvent skillEvent)
        {
            Array.Clear(queryCandidates, 0, queryCandidates.Length);
            Array.Clear(selectedCandidates, 0, selectedCandidates.Length);
            Array.Clear(playerHitBatch, 0, playerHitBatch.Length);

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

            DomainResult preflight = runtime.CombatPort.ValidatePlayerHitBatch(
                runtime.Player.RuntimeId,
                tick,
                selectedCandidates,
                selectedCount,
                nextCommandSequence);
            if (!preflight.IsSuccess)
            {
                return preflight;
            }

            for (int index = 0; index < selectedCount; index++)
            {
                QueryCandidate candidate = selectedCandidates[index];
                bool projectile = candidate.TargetKind == QueryTargetKind.Projectile;
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
                    index,
                    new ImpactSpatialContext(
                        candidate.ImpactPointKey,
                        candidate.GeometryId,
                        candidate.TargetKind,
                        candidate.HitPart));
                ImpactPhasePriority priority = projectile
                    ? ImpactPhasePriority.PlayerProjectileIntercept
                    : ImpactPhasePriority.PlayerCombatantHit;
                playerHitBatch[index] = new FpgPlayerHitCommand(
                    nextCommandSequence + index,
                    intent,
                    skillEvent.RuntimeEvent.ExecutionId,
                    skillEvent.Event.EventId,
                    priority);
            }

            DomainResult submittedBatch = runtime.CombatPort.TrySubmitPlayerHits(
                playerHitBatch,
                selectedCount);
            if (!submittedBatch.IsSuccess)
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            DomainResult committed = runtime.Player.Weapon.CommitPreparedSkillRelease(
                weaponRelease,
                runtime.IdAllocator,
                ResolveResolvedRecastReadyTick(skillEvent.Timing));
            if (!committed.IsSuccess)
            {
                runtime.CombatPort.TryCompensatePlayerHitBatch(
                    playerHitBatch,
                    selectedCount);
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (weaponRelease.Kind == WeaponReleaseKind.Primary)
            {
                int environmentContactOrdinal = selectedCount;
                for (int sampleIndex = 0;
                    sampleIndex < weaponRelease.PelletCount;
                    sampleIndex++)
                {
                    if (!TargetSelector.TryFindReachedEnvironmentBlocker(
                            weaponRelease.Attack,
                            queryCandidates,
                            queryResult,
                            selectedCandidates,
                            selectedCount,
                            sampleIndex,
                            out QueryCandidate blocker))
                    {
                        continue;
                    }

                    if (!runtime.CombatPort
                        .TryPublishImmediateEnvironmentContact(
                            runtime.Player.RuntimeId,
                            skillEvent.RuntimeEvent.ExecutionId,
                            skillEvent.Event.EventId,
                            tick,
                            weaponRelease.Attack.AttackId,
                            blocker.ImpactPointKey,
                            environmentContactOrdinal++))
                    {
                        SkillPresentationFaultCount++;
                    }
                }
            }

            if (selectedCount == 0
                && !runtime.CombatPort.TryCompleteImmediateSkillPresentationGroup(
                    runtime.Player.RuntimeId,
                    skillEvent.RuntimeEvent.ExecutionId,
                    skillEvent.Event.EventId,
                    tick,
                    weaponRelease.Attack.AttackId))
            {
                SkillPresentationFaultCount++;
            }

            nextCommandSequence += selectedCount;
            return DomainResult.Success;
        }

        private DomainResult QueryAndCommitRoomInteraction(
            FpgFormalCombatRuntimeBundle runtime,
            BattleTickInput tickInput,
            TickIndex tick,
            in FpgPlayerSkillExecutionEvent skillEvent)
        {
            Array.Clear(queryCandidates, 0, queryCandidates.Length);
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
                out AttackQueryResult result);
            if (!query.IsSuccess)
            {
                return query;
            }

            if (result.DroppedCandidateCount > 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            GeometryId exitGeometryId = GeometryId.Invalid;
            if (encounterDirector != null)
            {
                encounterDirector.ExitAttackRegistry.TryFindFirstVisibleExit(
                    queryCandidates,
                    result.CandidateCount,
                    out exitGeometryId);
            }

            if (exitGeometryId.IsValid
                && (encounterDirector == null
                    || !encounterDirector.TrySelectExit(exitGeometryId, out _)))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            DomainResult committed =
                runtime.Player.Weapon.CommitPreparedSkillRelease(
                    weaponRelease,
                    runtime.IdAllocator,
                    ResolveResolvedRecastReadyTick(skillEvent.Timing));
            if (!committed.IsSuccess)
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (!runtime.CombatPort.TryCompleteImmediateSkillPresentationGroup(
                runtime.Player.RuntimeId,
                skillEvent.RuntimeEvent.ExecutionId,
                skillEvent.Event.EventId,
                tick,
                weaponRelease.Attack.AttackId))
            {
                SkillPresentationFaultCount++;
            }

            return DomainResult.Success;
        }

        private static bool TryQuantizePosition(
            Vector3 position,
            out SpatialVectorKey key)
        {
            key = default(SpatialVectorKey);
            if (!IsFinite(position.x) || !IsFinite(position.y)
                || !IsFinite(position.z)
                || !TryQuantizeAxis(
                    position.x,
                    SpatialContract.PositionUnitsPerMeter,
                    out int x)
                || !TryQuantizeAxis(
                    position.y,
                    SpatialContract.PositionUnitsPerMeter,
                    out int y)
                || !TryQuantizeAxis(
                    position.z,
                    SpatialContract.PositionUnitsPerMeter,
                    out int z))
            {
                return false;
            }

            key = new SpatialVectorKey(x, y, z);
            return true;
        }

        private static bool TryQuantizeDirection(
            Vector3 forward,
            Vector3 referenceUp,
            out SpatialVectorKey quantizedForward,
            out SpatialVectorKey quantizedRight,
            out SpatialVectorKey quantizedUp)
        {
            quantizedForward = default(SpatialVectorKey);
            quantizedRight = default(SpatialVectorKey);
            quantizedUp = default(SpatialVectorKey);
            if (!IsFinite(forward) || forward.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            Vector3 normalizedForward = forward.normalized;
            Vector3 normalizedUp = referenceUp.sqrMagnitude <= 0.000001f
                ? Vector3.up
                : referenceUp.normalized;
            Vector3 right = Vector3.Cross(normalizedUp, normalizedForward);
            if (right.sqrMagnitude <= 0.000001f)
            {
                Vector3 fallbackUp = Mathf.Abs(normalizedForward.y) < 0.99f
                    ? Vector3.up
                    : Vector3.forward;
                right = Vector3.Cross(fallbackUp, normalizedForward);
            }

            if (right.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            right.Normalize();
            Vector3 up = Vector3.Cross(normalizedForward, right).normalized;
            return TryQuantizeDirectionVector(
                       normalizedForward,
                       out quantizedForward)
                && TryQuantizeDirectionVector(right, out quantizedRight)
                && TryQuantizeDirectionVector(up, out quantizedUp);
        }

        private static bool TryQuantizeDirectionVector(
            Vector3 value,
            out SpatialVectorKey key)
        {
            key = default(SpatialVectorKey);
            if (!TryQuantizeAxis(
                    value.x,
                    SpatialContract.DirectionUnits,
                    out int x)
                || !TryQuantizeAxis(
                    value.y,
                    SpatialContract.DirectionUnits,
                    out int y)
                || !TryQuantizeAxis(
                    value.z,
                    SpatialContract.DirectionUnits,
                    out int z))
            {
                return false;
            }

            key = new SpatialVectorKey(x, y, z);
            return !key.IsZero;
        }

        private static bool TryQuantizeAxis(float value, out int key)
        {
            return TryQuantizeAxis(
                value,
                SpatialContract.PositionUnitsPerMeter,
                out key);
        }

        private static bool TryQuantizeAxis(
            float value,
            int unitsPerValue,
            out int key)
        {
            double scaled = value * unitsPerValue;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled)
                || scaled > int.MaxValue || scaled < int.MinValue)
            {
                key = 0;
                return false;
            }

            key = checked((int)Math.Round(
                scaled,
                MidpointRounding.AwayFromZero));
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private DomainResult ProcessCoverMovement(
            BattleTickInput tickInput,
            TickIndex tick,
            FpgFormalCombatRuntimeBundle runtime,
            out bool consumedTick)
        {
            consumedTick = false;
            FpgCoverRuntime covers = runtime.Covers;
            if (covers == null)
            {
                return DomainResult.Success;
            }

            bool wasTraversing = covers.IsTraversing;
            Pose completedPose = default;
            FpgResolvedCameraShot completedShot = default;
            FpgCoverSnapshot completedSource = default;
            FpgResolvedCameraShot completedSourceShot = default;
            if (wasTraversing && tick.IsValid
                && tick >= covers.TraversalEndsTick)
            {
                FpgCoverSnapshot target = covers.TargetSnapshot;
                completedSource = covers.CurrentSnapshot;
                if (coverTraversalPresenter == null
                    || cameraFeedback == null
                    || encounterDirector == null
                    || !target.IsValid
                    || !completedSource.IsValid
                    || !cameraFeedback.HasCommittedShot
                    || !cameraFeedback.IsTransitioning
                    || !encounterDirector
                        .TryResolveCoverReachablePoseAndCameraShot(
                        target.CoverId,
                        out completedPose,
                        out _,
                        out completedShot,
                        out _))
                {
                    covers.CancelTraversal();
                    coverTraversalPresenter?.Cancel();
                    cameraFeedback?.CancelShotTransition();
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                completedSourceShot = cameraFeedback.CommittedShot;
                if ((!cameraFeedback.TryCommitShotTransition(out _)
                        && !cameraFeedback.TryApplyImmediateShot(
                            completedShot,
                            out _))
                    || !encounterDirector.TryPlacePlayerAtCover(
                        target.CoverId,
                        out _))
                {
                    encounterDirector.TryPlacePlayerAtCover(
                        completedSource.CoverId,
                        out _);
                    cameraFeedback.TryApplyImmediateShot(
                        completedSourceShot,
                        out _);
                    covers.CancelTraversal();
                    coverTraversalPresenter.Cancel();
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }
            }

            DomainResult advanced = covers.Advance(tick, out bool completed);
            if (!advanced.IsSuccess)
            {
                if (wasTraversing)
                {
                    if (completedSource.IsValid)
                    {
                        encounterDirector?.TryPlacePlayerAtCover(
                            completedSource.CoverId,
                            out _);
                        cameraFeedback?.TryApplyImmediateShot(
                            completedSourceShot,
                            out _);
                    }

                    covers.CancelTraversal();
                    coverTraversalPresenter?.Cancel();
                    cameraFeedback?.CancelShotTransition();
                }
                return advanced;
            }

            if (completed)
            {
                FpgCoverSnapshot current = covers.CurrentSnapshot;
                coverTraversalPresenter.Complete(completedPose);

                if (current.IsDestroyed)
                {
                    runtime.Player.Exposure.ForceExposed(tick, out _);
                }
                else
                {
                    runtime.Player.Exposure.ApplyCombatPosture(
                        false,
                        tick,
                        false,
                        out _);
                }
            }

            if (!wasTraversing
                && tickInput.CoverMoveDirection
                    != FpgCoverMoveDirection.None)
            {
                FpgCoverSnapshot source = covers.CurrentSnapshot;
                DomainResult started = covers.TryBeginMove(
                    tickInput.CoverMoveDirection,
                    tick,
                    out FpgCoverSnapshot target);
                if (!started.IsSuccess)
                {
                    return started.RejectReason == RejectReason.InvalidTarget
                        ? DomainResult.Success
                        : started;
                }

                if (coverTraversalPresenter == null
                    || cameraFeedback == null
                    || !cameraFeedback.HasCommittedShot
                    || encounterDirector == null
                    || !encounterDirector
                        .TryResolveCoverReachablePoseAndCameraShot(
                        source.CoverId,
                        out Pose sourcePose,
                        out _,
                        out _,
                        out _)
                    || !encounterDirector
                        .TryResolveCoverReachablePoseAndCameraShot(
                        target.CoverId,
                        out Pose targetPose,
                        out _,
                        out FpgResolvedCameraShot targetShot,
                        out _)
                    || !coverTraversalPresenter.TryBegin(
                        sourcePose,
                        targetPose,
                        threeCProfile.CoverTraversalSeconds,
                        out _)
                    || !cameraFeedback.TryBeginShotTransition(
                        cameraFeedback.CommittedShot,
                        targetShot,
                        threeCProfile.CoverTraversalSeconds,
                        out _))
                {
                    covers.CancelTraversal();
                    coverTraversalPresenter?.Cancel();
                    cameraFeedback?.CancelShotTransition();
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                ClearCoverPeekGate();
                ClearGameplayInputAndPendingAttackIntent();
                weaponRelease.Reset();
                DomainResult interrupted =
                    skillExecutionController.HardInterrupt(
                        tick,
                        runtime.Player.Weapon);
                if (!interrupted.IsSuccess)
                {
                    covers.CancelTraversal();
                    coverTraversalPresenter.Cancel();
                    cameraFeedback.CancelShotTransition();
                    return interrupted;
                }

                PublishSkillSequenceFrames();
                ReleaseTerminalPresentationCommits();
                runtime.Player.Weapon.CancelSkillAction();
                runtime.Player.Exposure.ForceExposed(tick, out _);
                wasTraversing = true;
            }

            if (!wasTraversing)
            {
                return DomainResult.Success;
            }

            PlayerInputFrame emptyFrame = PlayerInputFrame.Empty(tick);
            DomainResult processed = skillExecutionController.ProcessFrame(
                emptyFrame,
                runtime.Player);
            if (!processed.IsSuccess)
            {
                return processed;
            }

            PublishSkillSequenceFrames();
            ReleaseTerminalPresentationCommits();
            lastProcessedTick = tick;
            encounterDirector?.RefreshCoverViews();
            PublishSnapshot(runtime, tick);
            consumedTick = true;
            return DomainResult.Success;
        }

        private static DomainResult ApplyPosture(
            PlayerRuntime player,
            PlayerInputFrame frame,
            TickIndex tick,
            bool activeSkillRequiresExposure,
            bool coverDepleted)
        {
            PlayerExposureState previousExposure = player.Exposure.State;
            bool reloadKeepsWithdrawn = player.Weapon.State == WeaponState.Reloading
                && (!player.Weapon.StateUntilTick.IsValid
                    || tick < player.Weapon.StateUntilTick);
            bool reloadRequestsWithdrawn = !coverDepleted
                && reloadKeepsWithdrawn;
            bool shouldExpose = coverDepleted || !reloadRequestsWithdrawn
                && !frame.CancelSecondary
                && (activeSkillRequiresExposure
                    || frame.PrimaryHeld
                    || frame.HasSecondaryInput
                    || player.Weapon.State == WeaponState.AltCharging);
            DomainResult result = reloadRequestsWithdrawn
                ? player.Exposure.ApplyReloadPosture(tick, out bool ignoredReloadChange)
                : player.Exposure.ApplyCombatPosture(
                    shouldExpose,
                    tick,
                    coverDepleted,
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
                && stateAfter != WeaponState.Reloading
                && !reloadCompletionActionPublishedThisTick)
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

            if (stateBefore == WeaponState.Reloading
                && stateAfter != WeaponState.Reloading)
            {
                reloadPresentationStartTick = TickIndex.Invalid;
            }

            if (stateBefore == WeaponState.AltCharging
                && stateAfter != WeaponState.AltCharging
                && stateAfter != WeaponState.AltRecovery)
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
                reloadPresentationStartTick = tick;
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
            PublishAction(
                tick,
                type,
                releaseKind,
                attackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter,
                SkillExecutionId.Invalid,
                0);
        }

        private void PublishAction(
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
        {
            presentationSource.PublishAction(
                tick,
                type,
                releaseKind,
                attackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter,
                skillExecutionId,
                gameplayEventId);
        }

        private void PublishSnapshot(
            FpgFormalCombatRuntimeBundle runtime,
            TickIndex tick)
        {
            PlayerRuntime player = runtime.Player;
            bool isSecondaryCharging = skillExecutionController != null
                && player.Weapon.State == WeaponState.AltCharging
                && player.Weapon.SecondaryChargeStartedTick.IsValid;
            float secondaryChargeProgress = isSecondaryCharging
                ? skillExecutionController.GetSecondaryChargeProgress(
                    player.Weapon,
                    tick)
                : 0f;
            FpgCoverSnapshot cover = runtime.Covers == null
                ? default(FpgCoverSnapshot)
                : runtime.Covers.CurrentSnapshot;
            FpgResolvedAimContext resolvedAim = ResolvedAimContext.WithCurrentCover(
                cover.IsValid ? cover.CoverId : string.Empty);
            FpgEncounterPhase phase = encounterDirector == null
                ? FpgEncounterPhase.None
                : encounterDirector.Phase;
            bool roomInteraction = phase == FpgEncounterPhase.Cleared
                && encounterDirector != null
                && encounterDirector.HasAvailableExits;
            primaryAttackAvailability = ResolveAttackAvailability(
                FpgPlayerSkillSlot.Primary,
                runtime,
                tick,
                resolvedAim,
                roomInteraction: roomInteraction);
            secondaryAttackAvailability = ResolveAttackAvailability(
                FpgPlayerSkillSlot.Secondary,
                runtime,
                tick,
                resolvedAim,
                roomInteraction: roomInteraction);
            bool reticleHidden = player.Combatant.IsDead
                || encounterDirector == null
                || encounterDirector.IsPaused
                || phase == FpgEncounterPhase.None
                || phase == FpgEncounterPhase.Preparing
                || phase == FpgEncounterPhase.Defeated
                || phase == FpgEncounterPhase.Failed
                || phase == FpgEncounterPhase.Faulted
                || phase == FpgEncounterPhase.Disposed;
            FpgAimIndicatorBaseState aimIndicatorBaseState =
                CombatAimReticle.ResolveBaseState(
                    reticleHidden,
                    player.Weapon.State == WeaponState.Reloading,
                    resolvedAim.IsCurrentCoverBlocked,
                    !primaryAttackAvailability.Ready
                        && !secondaryAttackAvailability.Ready,
                    resolvedAim.IsReticleEnemy);
            float reloadProgress01 = ResolveReloadProgress01(
                player.Weapon.State,
                tick);
            presentationSource.PublishSnapshot(
                new FpgFormalPlayerPresentationSnapshot(
                    tick,
                    player.RuntimeId,
                    phase,
                    encounterDirector != null && encounterDirector.IsPaused,
                    player.Combatant.Life,
                    player.Combatant.MaxLife,
                    cover.IsValid
                        ? cover.Durability
                        : player.Combatant.Barrier,
                    cover.IsValid
                        ? cover.MaxDurability
                        : player.Combatant.MaxBarrier,
                    player.Weapon.Magazine.Ammo,
                    player.Weapon.Magazine.Capacity,
                    player.Exposure.State,
                    player.Weapon.State,
                    isSecondaryCharging,
                    secondaryChargeProgress,
                    isSecondaryCharging
                        ? player.Weapon.SecondaryChargeStartedTick
                        : TickIndex.Invalid,
                    isCoverPeekRequested,
                    isCoverPeekRequested
                        ? coverPeekStartedTick
                        : TickIndex.Invalid,
                    cover.IsValid ? cover.CoverId : string.Empty,
                    cover.IsValid && cover.IsDestroyed,
                    runtime.Covers != null && runtime.Covers.IsTraversing,
                    aimIndicatorBaseState,
                    reloadProgress01,
                    attackQuerySettings.PrimarySpreadTangent,
                    attackQuerySettings.SecondaryAreaRadius,
                    frozenAimContext.IsFrozen
                        ? frozenAimContext.FrozenVersion
                        : 0L,
                    coverPeekDirection));
        }

        private float ResolveReloadProgress01(
            WeaponState weaponState,
            TickIndex tick)
        {
            if (weaponState != WeaponState.Reloading
                || skillExecutionController == null || !tick.IsValid)
            {
                return 0f;
            }

            int durationTicks = skillExecutionController.ReloadDurationTicks;
            if (durationTicks <= 0)
            {
                return 0f;
            }

            if (!reloadPresentationStartTick.IsValid
                && skillExecutionController.IsExecuting
                && skillExecutionController.ActiveSlot == FpgPlayerSkillSlot.Reload
                && skillExecutionController.ActiveStartTick.IsValid)
            {
                reloadPresentationStartTick =
                    skillExecutionController.ActiveStartTick;
            }

            return CalculateReloadProgress01(
                reloadPresentationStartTick,
                tick,
                durationTicks);
        }

        public static float CalculateReloadProgress01(
            TickIndex reloadStartTick,
            TickIndex currentTick,
            int durationTicks)
        {
            if (!reloadStartTick.IsValid || !currentTick.IsValid
                || currentTick < reloadStartTick || durationTicks <= 0)
            {
                return 0f;
            }

            long elapsedTicks = currentTick.Value - reloadStartTick.Value + 1L;
            return Mathf.Clamp01((float)elapsedTicks / durationTicks);
        }

        public void CancelCoverTraversalForTerminalState()
        {
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            FpgCoverRuntime covers = runtime == null || runtime.IsDisposed
                ? null
                : runtime.Covers;
            bool presentationPlaying = coverTraversalPresenter != null
                && coverTraversalPresenter.IsPlaying;
            if (covers == null)
            {
                if (presentationPlaying)
                {
                    coverTraversalPresenter.Cancel();
                }
                cameraFeedback?.CancelShotTransition();
                return;
            }

            bool runtimeTraversing = covers.IsTraversing;
            if (!runtimeTraversing && !presentationPlaying)
            {
                cameraFeedback?.CancelShotTransition();
                return;
            }

            if (runtimeTraversing)
            {
                covers.CancelTraversal();
                FpgCoverSnapshot current = covers.CurrentSnapshot;
                encounterDirector.TryPlacePlayerAtCover(current.CoverId, out _);
                TickIndex terminalTick = encounterDirector.CurrentTick.IsValid
                    ? encounterDirector.CurrentTick
                    : new TickIndex(0L);
                if (current.IsDestroyed)
                {
                    runtime.Player.Exposure.ForceExposed(terminalTick, out _);
                }
                else
                {
                    runtime.Player.Exposure.ApplyCombatPosture(
                        false,
                        terminalTick,
                        false,
                        out _);
                }

                encounterDirector.RefreshCoverViews();
            }

            coverTraversalPresenter?.Cancel();
            cameraFeedback?.CancelShotTransition();
        }

        public void ResetRuntimeState()
        {
            coverTraversalPresenter?.Cancel();
            Array.Clear(edgeBuffer, 0, edgeBuffer.Length);
            ClearCoverPeekGate();
            Array.Clear(queryCandidates, 0, queryCandidates.Length);
            Array.Clear(selectedCandidates, 0, selectedCandidates.Length);
            Array.Clear(playerHitBatch, 0, playerHitBatch.Length);
            weaponRelease.Reset();
            skillExecutionController?.Reset();
            lastProcessedTick = TickIndex.Invalid;
            nextCommandSequence = 0L;
            captureFault = RejectReason.None;
            AimPreviewFaultCount = 0;
            AimPresentationFaultCount = 0;
            SkillPresentationFaultCount = 0;
            aimSolution = FpgFormalAimSolution.Idle;
            liveAimContext = FpgResolvedAimContext.Invalid;
            liveAttackAimContext = FpgResolvedAimContext.Invalid;
            frozenAimContext = FpgResolvedAimContext.Invalid;
            primaryAttackAvailability = default(FpgAttackAvailability);
            secondaryAttackAvailability = default(FpgAttackAvailability);
            reloadPresentationStartTick = TickIndex.Invalid;
            nextAimContextVersion = 1L;
            queuedAttackAfterReload = FpgPlayerSkillSlot.None;
            presentationSource.Clear();
            presentationCommitCache.Clear();
            roomInteractionArmed = false;
            reloadCompletionActionPublishedThisTick = false;
            presentationPaused = false;
            lifecycleAimViewportFrozen = false;
            cameraFeedback?.ResetRuntimeFeedback();
            if (aimViewportSource is CombatAimReticle reticle)
            {
                reticle.SetInputFrozen(false);
                reticle.ResetToCenter();
                reticle.ResetFeedback();
                reticle.SetChargeProgress(false, 0f);
            }
            ResetInputSource();
        }

        private void ResetInputSource()
        {
            inputSource = new UnityBattleInputSource();
            inputSource.ConfigureInputBufferTicks(Mathf.Clamp(inputBufferTicks, 1, 32));
            captureFault = RejectReason.None;
            if (!playerConfigured || aimAnchor == null || shotOrigin == null)
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
            lifecycleAimViewportFrozen = frozen;
            RefreshAimViewportFreeze();
        }

        private void RefreshAimViewportFreeze()
        {
            if (aimViewportSource is CombatAimReticle reticle)
            {
                // Attack events snapshot the latest live aim when they commit.
                // Only lifecycle states such as pause/overlays freeze viewport
                // input itself.
                reticle.SetInputFrozen(lifecycleAimViewportFrozen);
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private static bool IsFacingPhase(FpgEncounterPhase phase)
        {
            return phase == FpgEncounterPhase.Warning
                || phase == FpgEncounterPhase.Spawning
                || phase == FpgEncounterPhase.Combat
                || phase == FpgEncounterPhase.WaveDelay;
        }

        private void ClearGameplayInputAndPendingAttackIntent()
        {
            inputSource?.ClearGameplayInput();
            skillExecutionController?.ClearPendingInputIntents();
        }

        private static TickIndex ResolveResolvedRecastReadyTick(
            in FpgResolvedSkillTimingSnapshot timing)
        {
            return timing.IsValid && timing.UsesCharacterAttackSpeed
                ? timing.SameAttackReadyTick
                : TickIndex.Invalid;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearGameplayInputAndPendingAttackIntent();
                ClearCoverPeekGate();
            }
        }

        private void OnDisable()
        {
            ClearGameplayInputAndPendingAttackIntent();
            ClearCoverPeekGate();
            SetAimSolution(FpgFormalAimSolution.Idle);
            SetAimViewportFrozen(false);
        }

        private static void RecordSkillGameplayCommit(
            FpgFormalCombatRuntimeBundle runtime,
            TickIndex tick,
            in FpgPlayerSkillExecutionEvent skillEvent,
            AttackId attackId,
            int valueBefore,
            int valueAfter)
        {
            runtime.CombatKernel.Trace.Record(
                tick,
                CombatEventType.SkillGameplayCommitted,
                runtime.Player.RuntimeId,
                RuntimeId.Invalid,
                attackId,
                ImpactId.Invalid,
                valueBefore,
                valueAfter,
                skillExecutionId:
                    skillEvent.RuntimeEvent.ExecutionId.Value,
                gameplayEventId: skillEvent.Event.EventId);
        }
    }
}
