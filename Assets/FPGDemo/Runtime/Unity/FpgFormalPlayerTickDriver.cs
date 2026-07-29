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
    public sealed class FpgFormalPlayerTickDriver : MonoBehaviour, IFpgFormalPlayerTickDriver
    {
        private const ulong PlayerAttackRandomDomain = 0x4650475F504C4159UL;
        private const int CoverPeekGateTickCount = 5;
        // Five gated ticks plus the ready tick, drained in InputSequence order.
        private const int CoverPeekPendingEdgeCapacity =
            BattleTickInput.MaxEdgeCommandCount * (CoverPeekGateTickCount + 1);

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
        private bool playerConfigured;
        private bool runtimeObserved;
        private bool roomInteractionArmed;
        private bool reloadCompletionActionPublishedThisTick;
        private bool presentationPaused;
        private bool isCoverPeekRequested;
        private bool coverPeekPrimaryPending;
        private bool coverPeekAimFrozen;
        private bool useFrozenAimForActiveAttack;
        private int coverPeekPendingEdgeCount;
        private TickIndex coverPeekStartedTick = TickIndex.Invalid;
        private AimPoseSnapshot coverPeekFrozenAimPose;

        public FpgRoomEncounterDirector EncounterDirector => encounterDirector;
        public Transform AimAnchor => aimAnchor;
        public MonoBehaviour AimViewportSourceComponent => aimViewportSource;
        public ICombatAimViewportSource AimViewportSource =>
            aimViewportSource as ICombatAimViewportSource;
        public FpgFormalPlayerCameraFeedback CameraFeedback => cameraFeedback;
        public D0CharacterDefinition PlayerDefinition => playerDefinition;
        public FpgPlayerEntityView PlayerEntity => playerEntity;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public SecondaryTriggerMode PlayerSecondaryTriggerMode =>
            playerSecondaryTriggerMode;
        public bool IsPlayerConfigured => playerConfigured;
        public bool HasCaptureFault => playerConfigured
            && captureFault != RejectReason.None;
        public TickIndex LastProcessedTick => lastProcessedTick;
        public FpgFormalAimSolution AimSolution => aimSolution;
        public int AimPreviewFaultCount { get; private set; }
        public int AimPresentationFaultCount { get; private set; }
        public int SkillPresentationFaultCount { get; private set; }
        public bool IsCoverPeekRequested => isCoverPeekRequested;
        public TickIndex CoverPeekStartedTick => coverPeekStartedTick;
        public FpgFormalPlayerPresentationSource PresentationSource =>
            presentationSource;

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
            SetAimViewportFrozen(isPaused);

            if (inputSource == null)
            {
                ResetInputSource();
            }

            if (captureFromDevices)
            {
                if (!projectWideBattleInputAdapter.TryCapture(inputSource))
                {
                    inputSource.ClearGameplayInput();
                    ClearCoverPeekGate();
                    captureFault = RejectReason.InvalidState;
                    return;
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
                ClearCoverPeekGate();
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
                ClearCoverPeekGate();
                SetAimViewportFrozen(encounterDirector.IsPaused);
            }
            else if (encounterDirector != null && encounterDirector.IsPaused)
            {
                inputSource.ClearGameplayInput();
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
                    out FpgPlayerSkillExecutionController controller,
                    out error))
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
            playerRoot = entity.transform;
            aimDistance = profile.MaximumAimDistance;
            aimLayerMask = querySettings.HitboxLayerMask
                | querySettings.BlockerLayerMask;
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
                || playerEntity == null || threeCProfile == null
                || skillExecutionController == null)
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
                UpdateAimSolution(aimAnchor.position, aimAnchor.forward);
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
            UpdateAimSolution(aimAnchor.position, aimDirection);
        }

        private void UpdateAimSolution(Vector3 origin, Vector3 direction)
        {
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (runtime == null || runtime.IsDisposed
                || runtime.AttackQueryPort == null)
            {
                SetAimSolution(FpgFormalAimSolution.Idle);
                return;
            }

            DomainResult solved = runtime.AttackQueryPort.SolveAim(
                origin,
                direction,
                runtime.Player.RuntimeId,
                Team.Player,
                runtime.Player.Weapon.Definition.PrimaryAllowedTargetKinds,
                out FpgFormalAimSolution next);
            if (!solved.IsSuccess)
            {
                AimPreviewFaultCount++;
                next = FpgFormalAimSolution.Idle;
            }

            SetAimSolution(next);
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

            BattleTickInput tickInput = inputSource.GetTickInput(tick);
            if (!tickInput.IsValid || tickInput.Tick != tick)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            PlayerInputFrame frame = tickInput.CopyToPlayerInputFrame(edgeBuffer);
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
                skillExecutionController.RequiresExposureAt(tick));
            if (!posture.IsSuccess && posture.RejectReason != RejectReason.BarrierLocked)
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

            runtime.Player.Combatant.TryRestoreBarrier(tick);
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
                skillExecutionController.RequiresExposureAt(tick));
            if (!posture.IsSuccess
                && posture.RejectReason != RejectReason.BarrierLocked)
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
            aimAnchor = null;
            playerRoot = null;
            SetAimSolution(FpgFormalAimSolution.Idle);
            playerConfigured = false;
            inputSource = null;
            presentationSource.Clear();
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
            if (capturedFrame.HasReloadInput || capturedFrame.CancelSecondary)
            {
                ClearCoverPeekGate();
                return DomainResult.Success;
            }

            bool activeSkillRequiresExposure = skillExecutionController != null
                && skillExecutionController.RequiresExposureAt(tick);
            if (useFrozenAimForActiveAttack
                && !activeSkillRequiresExposure
                && !HasPendingCoverAttack)
            {
                useFrozenAimForActiveAttack = false;
                coverPeekAimFrozen = false;
                coverPeekFrozenAimPose = default(AimPoseSnapshot);
            }

            bool hasLiveAimIntent = capturedFrame.AimHeld
                || capturedFrame.PrimaryHeld
                || capturedFrame.SecondaryHeld;
            bool peekRequested = hasLiveAimIntent
                || capturedFrame.HasSecondaryInput
                || activeSkillRequiresExposure
                || player.Weapon.State == WeaponState.AltCharging
                || HasPendingCoverAttack
                || useFrozenAimForActiveAttack;
            if (!peekRequested)
            {
                ClearCoverPeekGate();
                return DomainResult.Success;
            }

            if (!coverPeekStartedTick.IsValid)
            {
                coverPeekStartedTick = tick;
            }
            isCoverPeekRequested = true;

            bool attackReleasedThisTick =
                (coverPeekPrimaryPending && !capturedFrame.PrimaryHeld)
                || ContainsInputEdge(
                    capturedFrame,
                    InputEdgeType.SecondaryReleased);
            bool attackStillHeld = capturedFrame.PrimaryHeld
                || capturedFrame.SecondaryHeld;
            if (attackReleasedThisTick)
            {
                if (!coverPeekAimFrozen)
                {
                    FreezeCoverPeekAim(capturedTickInput.AimPose);
                }
            }
            else if (attackStillHeld && !useFrozenAimForActiveAttack)
            {
                coverPeekAimFrozen = false;
                coverPeekFrozenAimPose = default(AimPoseSnapshot);
            }
            else if (!attackStillHeld
                && HasPendingCoverAttack
                && !coverPeekAimFrozen)
            {
                FreezeCoverPeekAim(capturedTickInput.AimPose);
            }

            bool fullyPeeked = tick.Value - coverPeekStartedTick.Value
                >= CoverPeekGateTickCount;
            int frameEdgeCount = 0;
            bool deliveredPendingAttack = false;
            if (!fullyPeeked)
            {
                coverPeekPrimaryPending |= capturedFrame.PrimaryHeld;
                bool queueFollowingSecondaryEdge =
                    coverPeekPendingEdgeCount > 0;
                for (int index = 0;
                    index < capturedFrame.EdgeCommandCount;
                    index++)
                {
                    InputEdgeCommand edge = capturedFrame.EdgeCommands[index];
                    bool queueEdge = playerSecondaryTriggerMode
                            == SecondaryTriggerMode.ImmediateRepeatWhileHeld
                        || queueFollowingSecondaryEdge
                        || edge.Type == InputEdgeType.SecondaryReleased;
                    if (queueEdge)
                    {
                        if (!TryEnqueueCoverPeekEdge(edge))
                        {
                            return DomainResult.Rejected(
                                RejectReason.BufferCapacity);
                        }
                        queueFollowingSecondaryEdge = true;
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
                && (capturedFrame.PrimaryHeld || coverPeekPrimaryPending);
            bool gatedSecondaryHeld = fullyPeeked
                ? capturedFrame.SecondaryHeld
                : playerSecondaryTriggerMode == SecondaryTriggerMode.ChargeRelease
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
                aimHeld: true,
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
                gatedAimPose = RetickAimPose(coverPeekFrozenAimPose, tick);
                useFrozenAimForActiveAttack = true;
            }
            gatedTickInput = new BattleTickInput(gatedFrame, gatedAimPose);
            return DomainResult.Success;
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
            if (!aimPose.IsValid)
            {
                return;
            }

            coverPeekFrozenAimPose = aimPose;
            coverPeekAimFrozen = true;
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

        private void FinishCoverPeekTick(TickIndex tick)
        {
            if (!useFrozenAimForActiveAttack
                || skillExecutionController.RequiresExposureAt(tick))
            {
                return;
            }

            useFrozenAimForActiveAttack = false;
            if (!HasPendingCoverAttack)
            {
                coverPeekAimFrozen = false;
                coverPeekFrozenAimPose = default(AimPoseSnapshot);
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
                    eventTickInput = ApplySkillOffset(
                        tickInput,
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
            FpgPlayerSkillDefinition authored =
                ResolveAuthoredSkill(skillEvent.Slot);
            if (playerEntity == null
                || !FpgPlayerSkillGameplayEventResolver.TryResolveSocketName(
                    authored,
                    skillEvent.RuntimeEvent.SequenceKind,
                    skillEvent.Event,
                    out string socketName)
                || !FpgPlayerSkillPresentationResolver.TryResolvePresentationSource(
                    playerEntity,
                    socketName,
                    out Transform muzzle)
                || muzzle == null
                || !TryQuantizePosition(muzzle.position, out SpatialVectorKey start))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

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
                runtime.IdAllocator);
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
                runtime.IdAllocator);
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
                    runtime.IdAllocator);
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
                || !TryQuantizeAxis(position.x, out int x)
                || !TryQuantizeAxis(position.y, out int y)
                || !TryQuantizeAxis(position.z, out int z))
            {
                return false;
            }

            key = new SpatialVectorKey(x, y, z);
            return true;
        }

        private static bool TryQuantizeAxis(float value, out int key)
        {
            double scaled = value * SpatialContract.PositionUnitsPerMeter;
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

        private static DomainResult ApplyPosture(
            PlayerRuntime player,
            PlayerInputFrame frame,
            TickIndex tick,
            bool activeSkillRequiresExposure)
        {
            PlayerExposureState previousExposure = player.Exposure.State;
            bool reloadKeepsWithdrawn = player.Weapon.State == WeaponState.Reloading
                && (!player.Weapon.StateUntilTick.IsValid
                    || tick < player.Weapon.StateUntilTick);
            bool reloadRequestsWithdrawn = reloadKeepsWithdrawn || frame.HasReloadInput;
            bool shouldExpose = !reloadRequestsWithdrawn
                && !frame.CancelSecondary
                && (activeSkillRequiresExposure
                    || frame.AimHeld
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
                    tick.IsValid
                        && player.Combatant.IsBarrierLocked(tick)));
        }

        public void ResetRuntimeState()
        {
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
            presentationSource.Clear();
            presentationCommitCache.Clear();
            roomInteractionArmed = false;
            reloadCompletionActionPublishedThisTick = false;
            presentationPaused = false;
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
                ClearCoverPeekGate();
            }
        }

        private void OnDisable()
        {
            inputSource?.ClearGameplayInput();
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
