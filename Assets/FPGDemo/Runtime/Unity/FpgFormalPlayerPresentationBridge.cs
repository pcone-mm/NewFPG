using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only formal player boundary. Composition injects the
    /// selected definition/entity/profile; this bridge observes committed
    /// formal runtime state and never writes combat decisions.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerPresentationBridge : MonoBehaviour,
        IFpgFormalPlayerPresentationSource
    {
        private const int ActionQueueCapacity = 32;
        private const int SkillPresentationQueueCapacity = 64;
        private const int ActionPresentationBindingCapacity = 64;
        [Header("Formal runtime")]
        [SerializeField] private FpgRoomEncounterDirector encounterDirector;
        [SerializeField] private FpgFormalPlayerTickDriver playerTickDriver;

        [Header("Player presentation")]
        [SerializeField] private FpgFormalPlayerHudPresenter playerHud;
        [SerializeField] private FpgFormalPlayerCameraFeedback cameraFeedback;
        [SerializeField] private D0CombatVfxWorld skillVfxWorld;
        [SerializeField] private FpgSkillPresentationWorld
            skillPresentationWorld;

        [Header("Scene-owned camera")]
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Camera targetCamera;

        private readonly FpgFormalPlayerActionEvent[] actionQueue =
            new FpgFormalPlayerActionEvent[ActionQueueCapacity];
        private readonly FpgFormalPlayerSkillSequenceEvent[] skillSequenceQueue =
            new FpgFormalPlayerSkillSequenceEvent[
                SkillPresentationQueueCapacity];
        private readonly FpgFormalPlayerActivePresentationEvent[]
            activePresentationQueue =
                new FpgFormalPlayerActivePresentationEvent[
                    SkillPresentationQueueCapacity];
        private FpgVitalsSnapshot[] vitalsBuffer =
            Array.Empty<FpgVitalsSnapshot>();
        private IProjectilePresentationFeed observedProjectilePresentationFeed;
        private IPlayerShotPresentationFeed observedPlayerShotPresentationFeed;
        private ProjectilePresentationState[] projectilePresentationStates =
            Array.Empty<ProjectilePresentationState>();
        private ProjectilePresentationEvent[] projectilePresentationEvents =
            Array.Empty<ProjectilePresentationEvent>();
        private PlayerShotPresentationEvent[] playerShotPresentationEvents =
            Array.Empty<PlayerShotPresentationEvent>();
        private PlayerProjectileVisualSlot[] playerProjectileVisuals =
            Array.Empty<PlayerProjectileVisualSlot>();
        private readonly ShotTrajectoryBinding[] shotTrajectoryBindings =
            new ShotTrajectoryBinding[ActionPresentationBindingCapacity];
        private readonly ProjectileFlightBinding[] projectileFlightBindings =
            new ProjectileFlightBinding[ActionPresentationBindingCapacity];

        private FpgPlayableCharacterSelection selection;
        private FpgPlayerEntityView playerEntity;
        private Actor2DPresenter actorPresenter;
        private CombatTrace observedTrace;
        private FpgFormalCombatRuntimeBundle observedVitalsRuntime;
        private FpgFormalPlayerPresentationSnapshot snapshot =
            FpgFormalPlayerPresentationSnapshot.Unavailable;
        private long nextCombatEventOrdinal;
        private long vitalsCursor;
        private long projectilePresentationCursor;
        private long playerShotPresentationCursor;
        private long nextActionPresentationBindingOrdinal;
        private int actionHead;
        private int actionCount;
        private int skillSequenceHead;
        private int skillSequenceCount;
        private int activePresentationHead;
        private int activePresentationCount;
        private bool actionGap;
        private bool skillSequenceGap;
        private bool hasActiveSkillSequence;
        private FpgFormalPlayerSkillSequenceEvent activeSkillSequence;
        private FpgPresentationHandle secondaryChargeVfxHandle;
        private GameObject secondaryChargeVfxInstance;
        private Transform secondaryChargeVfxSource;
        private bool prepared;
        private bool active;
        private bool subscribed;

        public FpgRoomEncounterDirector EncounterDirector => encounterDirector;
        public FpgFormalPlayerTickDriver PlayerTickDriver => playerTickDriver;
        public FpgFormalPlayerHudPresenter PlayerHud => playerHud;
        public FpgFormalPlayerCameraFeedback CameraFeedback => cameraFeedback;
        public D0CombatVfxWorld SkillVfxWorld => skillVfxWorld;
        public FpgSkillPresentationWorld SkillPresentationWorld =>
            skillPresentationWorld;
        public Transform CameraRig => cameraRig;
        public Camera TargetCamera => targetCamera;
        public FpgPlayableCharacterSelection Selection => selection;
        public FpgPlayerEntityView PlayerEntity => playerEntity;
        public FpgFormalPlayerPresentationSnapshot Snapshot => snapshot;
        public bool IsPrepared => prepared;
        public bool IsActive => active;
        public bool HasSecondaryChargeVfx => secondaryChargeVfxInstance != null;
        public int VitalsGapCount { get; private set; }
        public int VitalsReadCapacity => vitalsBuffer.Length;
        public int SkillSequenceGapCount { get; private set; }
        public int ActivePresentationGapCount { get; private set; }
        public int SkillPresentationFaultCount { get; private set; }
        public string LastSkillPresentationPrepareError { get; private set; } =
            string.Empty;

        public event Action<FpgFormalPlayerActivePresentationEvent>
            ActivePresentationPresented;

        private void LateUpdate()
        {
            if (!active || encounterDirector == null || playerTickDriver == null)
            {
                return;
            }

            if (!playerTickDriver.TryRefreshPresentationSnapshot(
                    out FpgFormalPlayerPresentationSnapshot nextSnapshot))
            {
                return;
            }

            nextSnapshot = ApplyVitalsChanges(nextSnapshot);

            FpgFormalPlayerPresentationSnapshot previous = snapshot;
            snapshot = nextSnapshot;
            ApplyCoverPresentation(snapshot);
            if (snapshot.IsPaused && !previous.IsPaused)
            {
                ClearSkillRuntimePresentation();
            }

            ConsumeSkillSequenceEvents();
            bool primaryPresented = ConsumeCommittedActions();
            PresentationFrameFlags traceFlags = ConsumeCombatTrace();
            ApplySnapshotTransitions(
                previous,
                snapshot,
                primaryPresented,
                traceFlags);
            actorPresenter?.SetPaused(snapshot.IsPaused);
            EvaluateActiveSkillAnimation();
            ConsumeActivePresentations();
            UpdateSecondaryChargeFeedback();
            ConsumePlayerShotPresentation();
            ConsumePlayerProjectilePresentation();
            cameraFeedback?.SetPaused(snapshot.IsPaused);
            playerTickDriver.CoverTraversalPresenter?.SetPaused(snapshot.IsPaused);
            playerHud?.Refresh(snapshot);
        }

        public bool TryPrepare(
            FpgPlayableCharacterSelection nextSelection,
            FpgPlayerEntityView nextPlayerEntity,
            out string error)
        {
            if (prepared)
            {
                error = "Formal player presentation supports one preparation per scene lifetime.";
                return false;
            }

            if (!TryValidateAuthoring(out error)
                || !nextSelection.TryValidate(out error))
            {
                return false;
            }

            if (nextPlayerEntity == null || !nextPlayerEntity.TryValidate(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal player presentation requires a valid scene entity.";
                }

                return false;
            }

            D0CharacterDefinition definition = nextSelection.CharacterDefinition;
            if (!playerTickDriver.IsPlayerConfigured
                || playerTickDriver.PlayerDefinition != definition
                || playerTickDriver.PlayerEntity != nextPlayerEntity
                || playerTickDriver.ThreeCProfile != nextSelection.ThreeCProfile
                || playerTickDriver.PlayerSecondaryTriggerMode
                    != nextSelection.SelectedSecondaryTriggerMode)
            {
                error = "Formal player presentation and tick driver must share the selected player binding.";
                return false;
            }

            Actor2DPresenter nextActorPresenter = nextPlayerEntity.ActorPresenter;
            if (nextActorPresenter == null || !nextActorPresenter.IsInitialized
                || nextActorPresenter.RuntimePresentationOverride
                    != definition.ActorPresentation
                || nextActorPresenter.RuntimeWeaponDefinition != definition.Weapon)
            {
                error = "Formal player Actor2DPresenter must be initialized from the selected definition.";
                return false;
            }

            if (!playerHud.TryPrepare(out error)
                || !cameraFeedback.TryPrepare(
                    nextSelection.ThreeCProfile,
                    targetCamera,
                    cameraRig,
                    playerHud.PresentationProfile.CameraShake,
                    playerHud.PresentationProfile.PoolCapacities.ScreenEffectCapacity,
                    out error)
                || !playerTickDriver.TryBindCameraFeedback(cameraFeedback, out error))
            {
                return false;
            }

            if (skillVfxWorld == null)
            {
                error =
                    "Formal player presentation requires the shared skill VFX world.";
                return false;
            }

            if (skillPresentationWorld == null)
            {
                skillPresentationWorld =
                    skillVfxWorld.GetComponent<FpgSkillPresentationWorld>();
                if (skillPresentationWorld == null)
                {
                    skillPresentationWorld = skillVfxWorld.gameObject
                        .AddComponent<FpgSkillPresentationWorld>();
                }
            }

            if (!skillPresentationWorld.TryConfigure(
                    skillVfxWorld,
                    cameraFeedback,
                    out error))
            {
                return false;
            }

            FpgPlayerBarrierPresentationController barrier = nextPlayerEntity.Barrier;
            if (barrier == null
                || !barrier.TrySetThreeCProfile(nextSelection.ThreeCProfile, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal player presentation requires a configured barrier view.";
                }

                return false;
            }
            if (!barrier.TryBindFormalSource(this, out error))
            {
                return false;
            }

            selection = nextSelection;
            playerEntity = nextPlayerEntity;
            actorPresenter = nextActorPresenter;
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            ResetEventCursors();
            prepared = true;
            error = string.Empty;
            return true;
        }

        private IReadOnlyList<FpgSkillTimelineDefinition>
            CollectPresentationSkills(D0WeaponDefinition weapon)
        {
            List<FpgSkillTimelineDefinition> skills =
                new List<FpgSkillTimelineDefinition>();
            HashSet<FpgSkillTimelineDefinition> seen =
                new HashSet<FpgSkillTimelineDefinition>();
            AddPresentationSkill(weapon.PrimarySkill, skills, seen);
            AddPresentationSkill(
                weapon.ImmediateSecondarySkill,
                skills,
                seen);
            AddPresentationSkill(
                weapon.ChargeSecondarySkill,
                skills,
                seen);
            AddPresentationSkill(weapon.ReloadSkill, skills, seen);

            FpgEnemyDefinitionCatalog catalog = encounterDirector == null
                ? null
                : encounterDirector.EnemyCatalog;
            if (catalog == null)
            {
                return skills;
            }

            for (int enemyIndex = 0;
                enemyIndex < catalog.Definitions.Count;
                enemyIndex++)
            {
                FpgEnemyDefinition enemy = catalog.Definitions[enemyIndex];
                if (enemy == null)
                {
                    continue;
                }

                for (int attackIndex = 0;
                    attackIndex < enemy.AttackPatternCount;
                    attackIndex++)
                {
                    AddPresentationSkill(
                        enemy.GetAttackPattern(attackIndex),
                        skills,
                        seen);
                }
            }

            return skills;
        }

        private static void AddPresentationSkill(
            FpgSkillTimelineDefinition skill,
            ICollection<FpgSkillTimelineDefinition> output,
            ISet<FpgSkillTimelineDefinition> seen)
        {
            if (skill != null && seen.Add(skill))
            {
                output.Add(skill);
            }
        }

        /// <summary>
        /// Called after the director has placed the player at the room entry.
        /// This is the only point that applies the scene-owned camera rig.
        /// </summary>
        public bool TryActivate(out string error)
        {
            if (active)
            {
                error = string.Empty;
                return true;
            }

            if (!prepared || playerEntity == null || actorPresenter == null)
            {
                error = "Formal player presentation must be prepared before activation.";
                return false;
            }

            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (runtime == null || runtime.IsDisposed
                || runtime.Player == null
                || runtime.Player.RuntimeId.IsValid == false)
            {
                error = "Formal player presentation requires a prepared combat runtime.";
                return false;
            }

            D0WeaponDefinition weapon = selection.CharacterDefinition == null
                ? null
                : selection.CharacterDefinition.Weapon;
            if (weapon == null)
            {
                error =
                    "Formal player presentation requires a selected weapon definition.";
                return false;
            }

            if (!skillPresentationWorld.TryPrepare(
                    CollectPresentationSkills(weapon),
                    playerHud.PresentationProfile,
                    out string presentationError))
            {
                LastSkillPresentationPrepareError =
                    string.IsNullOrWhiteSpace(presentationError)
                        ? "Formal player presentation could not prepare the shared V3 skill registry."
                        : presentationError;
                SkillPresentationFaultCount++;
            }
            else
            {
                LastSkillPresentationPrepareError = string.Empty;
            }

            // Skill presentation is non-authoritative. A broken registry or
            // resource pool disables presentation for this activation only.
            error = string.Empty;

            int vitalsReadCapacity = runtime.CombatPort.Vitals.EventCapacity;
            if (vitalsReadCapacity <= 0)
            {
                error = "Formal player presentation requires a positive Vitals event capacity.";
                return false;
            }
            vitalsBuffer = new FpgVitalsSnapshot[vitalsReadCapacity];

            if (!TryBindProjectilePresentationFeed(
                    runtime.ProjectilePresentationFeed,
                    out error))
            {
                return false;
            }

            if (!TryBindPlayerShotPresentationFeed(
                    runtime.PlayerShotPresentationFeed,
                    out error))
            {
                return false;
            }

            playerEntity.gameObject.SetActive(true);
            if (playerEntity.VisualRoot != null
                && (playerTickDriver.CoverTraversalPresenter == null
                    || !playerTickDriver.CoverTraversalPresenter.IsPlaying))
            {
                playerEntity.VisualRoot.gameObject.SetActive(true);
            }
            if (!cameraFeedback.TryApplyFixedSceneRig(playerEntity.transform, out error))
            {
                return false;
            }

            Subscribe();
            observedTrace = runtime.CombatKernel.Trace;
            nextCombatEventOrdinal = observedTrace.TotalEventCount;
            active = true;
            skillVfxWorld?.BeginCombat();
            if (playerTickDriver.TryRefreshPresentationSnapshot(out snapshot))
            {
                ApplyCoverPresentation(snapshot);
                ApplyDurableActorState(snapshot);
                actorPresenter.SetPaused(snapshot.IsPaused);
                cameraFeedback.SetPaused(snapshot.IsPaused);
                playerTickDriver.CoverTraversalPresenter?.SetPaused(snapshot.IsPaused);
                playerHud.Refresh(snapshot);
                UpdateSecondaryChargeFeedback();
            }

            error = string.Empty;
            return true;
        }

        public bool TryGetPlayerPresentationSnapshot(
            out FpgFormalPlayerPresentationSnapshot result)
        {
            result = snapshot;
            return active && result.IsValid;
        }

        public bool TryValidateAuthoring(out string error)
        {
            if (encounterDirector == null || playerTickDriver == null
                || playerHud == null || cameraFeedback == null
                || cameraRig == null || targetCamera == null)
            {
                error = "Formal player presentation requires director, driver, HUD, camera feedback, rig and camera references.";
                return false;
            }

            if (playerTickDriver.EncounterDirector != encounterDirector)
            {
                error = "Formal player presentation driver must target its encounter director.";
                return false;
            }

            if (!playerHud.TryValidate(out error)
                || !cameraFeedback.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Clear()
        {
            Unsubscribe();
            ResetCoverPresentation();
            ClearSecondaryChargeFeedback();
            ClearPlayerProjectileVisuals();
            skillVfxWorld?.EndCombat();
            skillPresentationWorld?.ClearRuntimePresentation();
            if (playerEntity != null && playerEntity.Barrier != null)
            {
                playerEntity.Barrier.UnbindFormalSource();
            }

            actorPresenter?.SetPaused(false);
            actorPresenter?.ClearAndReturnToIdle();
            playerHud?.Clear();
            cameraFeedback?.Clear();
            selection = default(FpgPlayableCharacterSelection);
            playerEntity = null;
            actorPresenter = null;
            observedTrace = null;
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            VitalsGapCount = 0;
            SkillSequenceGapCount = 0;
            ActivePresentationGapCount = 0;
            SkillPresentationFaultCount = 0;
            LastSkillPresentationPrepareError = string.Empty;
            prepared = false;
            active = false;
            ResetEventCursors();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            playerTickDriver.ActionCommitted += HandleActionCommitted;
            playerTickDriver.SkillSequenceAdvanced +=
                HandleSkillSequenceAdvanced;
            playerTickDriver.ActivePresentationCommitted +=
                HandleActivePresentationCommitted;
            encounterDirector.LifecycleEvent += HandleEncounterLifecycle;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (playerTickDriver != null)
            {
                playerTickDriver.ActionCommitted -= HandleActionCommitted;
                playerTickDriver.SkillSequenceAdvanced -=
                    HandleSkillSequenceAdvanced;
                playerTickDriver.ActivePresentationCommitted -=
                    HandleActivePresentationCommitted;
            }

            if (encounterDirector != null)
            {
                encounterDirector.LifecycleEvent -= HandleEncounterLifecycle;
            }

            subscribed = false;
        }

        private void HandleActionCommitted(FpgFormalPlayerActionEvent action)
        {
            if (!active)
            {
                return;
            }

            RegisterActionPresentationBindings(action);

            if (actionCount == actionQueue.Length)
            {
                actionHead = (actionHead + 1) % actionQueue.Length;
                actionCount--;
                actionGap = true;
            }

            int writeIndex = (actionHead + actionCount) % actionQueue.Length;
            actionQueue[writeIndex] = action;
            actionCount++;
        }

        private void HandleSkillSequenceAdvanced(
            FpgFormalPlayerSkillSequenceEvent sequenceEvent)
        {
            if (!active)
            {
                return;
            }

            if (skillSequenceCount == skillSequenceQueue.Length)
            {
                skillSequenceQueue[skillSequenceHead] =
                    default(FpgFormalPlayerSkillSequenceEvent);
                skillSequenceHead =
                    (skillSequenceHead + 1) % skillSequenceQueue.Length;
                skillSequenceCount--;
                skillSequenceGap = true;
                SkillSequenceGapCount++;
            }

            int writeIndex = (skillSequenceHead + skillSequenceCount)
                % skillSequenceQueue.Length;
            skillSequenceQueue[writeIndex] = sequenceEvent;
            skillSequenceCount++;
        }

        private void HandleActivePresentationCommitted(
            FpgFormalPlayerActivePresentationEvent presentationEvent)
        {
            if (!active)
            {
                return;
            }

            if (activePresentationCount == activePresentationQueue.Length)
            {
                activePresentationQueue[activePresentationHead] =
                    default(FpgFormalPlayerActivePresentationEvent);
                activePresentationHead = (activePresentationHead + 1)
                    % activePresentationQueue.Length;
                activePresentationCount--;
                ActivePresentationGapCount++;
            }

            int writeIndex =
                (activePresentationHead + activePresentationCount)
                % activePresentationQueue.Length;
            activePresentationQueue[writeIndex] = presentationEvent;
            activePresentationCount++;
        }

        private void ConsumeSkillSequenceEvents()
        {
            if (skillSequenceGap)
            {
                if (hasActiveSkillSequence)
                {
                    actorPresenter.CancelSkillAnimation(
                        activeSkillSequence.ExecutionId);
                }

                hasActiveSkillSequence = false;
                activeSkillSequence =
                    default(FpgFormalPlayerSkillSequenceEvent);
                skillSequenceGap = false;
            }

            while (skillSequenceCount > 0)
            {
                FpgFormalPlayerSkillSequenceEvent sequenceEvent =
                    skillSequenceQueue[skillSequenceHead];
                skillSequenceQueue[skillSequenceHead] =
                    default(FpgFormalPlayerSkillSequenceEvent);
                skillSequenceHead =
                    (skillSequenceHead + 1) % skillSequenceQueue.Length;
                skillSequenceCount--;

                if (sequenceEvent.State == FpgSkillExecutionState.Canceled)
                {
                    actorPresenter.CancelSkillAnimation(
                        sequenceEvent.ExecutionId);
                    if (sequenceEvent.Slot == FpgPlayerSkillSlot.Secondary
                        && (sequenceEvent.SequenceKind
                                == FpgSkillSequenceKind.ChargeEnter
                            || sequenceEvent.SequenceKind
                                == FpgSkillSequenceKind.ChargeLoop))
                    {
                        ReleaseSecondaryChargeVfx();
                    }
                    if (hasActiveSkillSequence
                        && activeSkillSequence.ExecutionId
                            == sequenceEvent.ExecutionId)
                    {
                        hasActiveSkillSequence = false;
                        activeSkillSequence =
                            default(FpgFormalPlayerSkillSequenceEvent);
                    }

                    continue;
                }

                if (hasActiveSkillSequence
                    && activeSkillSequence.ExecutionId
                        != sequenceEvent.ExecutionId)
                {
                    actorPresenter.CancelSkillAnimation(
                        activeSkillSequence.ExecutionId);
                }

                activeSkillSequence = sequenceEvent;
                hasActiveSkillSequence = true;
            }
        }

        private void EvaluateActiveSkillAnimation()
        {
            if (!hasActiveSkillSequence || actorPresenter == null
                || snapshot.IsPaused)
            {
                return;
            }

            FpgFormalPlayerSkillSequenceEvent sequenceEvent =
                activeSkillSequence;
            long relativeValue = sequenceEvent.RelativeTick;
            if (snapshot.Tick.IsValid && sequenceEvent.StartTick.IsValid)
            {
                relativeValue = snapshot.Tick.Value
                    - sequenceEvent.StartTick.Value;
            }

            int relativeTick = (int)Math.Max(
                0L,
                Math.Min(
                    sequenceEvent.CompiledSequence.DurationTicks,
                    relativeValue));
            double interpolation = sequenceEvent.IsTerminal
                ? 0d
                : FpgFormalPlayerSkillAnimationClock.ResolveInterpolation(
                    Time.timeAsDouble,
                    Time.fixedTimeAsDouble,
                    Time.fixedDeltaTime);
            if (!actorPresenter.TryEvaluateSkillAnimation(
                    sequenceEvent.ExecutionId,
                    sequenceEvent.AnimationName,
                    sequenceEvent.CompiledSequence,
                    relativeTick,
                    interpolation,
                    out _))
            {
                SkillPresentationFaultCount++;
                actorPresenter.CancelSkillAnimation(
                    sequenceEvent.ExecutionId);
                hasActiveSkillSequence = false;
                activeSkillSequence =
                    default(FpgFormalPlayerSkillSequenceEvent);
                return;
            }

            if (sequenceEvent.State == FpgSkillExecutionState.Completed)
            {
                actorPresenter.CompleteSkillAnimation(
                    sequenceEvent.ExecutionId);
                hasActiveSkillSequence = false;
                activeSkillSequence =
                    default(FpgFormalPlayerSkillSequenceEvent);
            }
        }

        private void ConsumeActivePresentations()
        {
            if (snapshot.IsPaused)
            {
                return;
            }

            while (activePresentationCount > 0)
            {
                FpgFormalPlayerActivePresentationEvent presentationEvent =
                    activePresentationQueue[activePresentationHead];
                activePresentationQueue[activePresentationHead] =
                    default(FpgFormalPlayerActivePresentationEvent);
                activePresentationHead = (activePresentationHead + 1)
                    % activePresentationQueue.Length;
                activePresentationCount--;
                PresentActivePresentation(presentationEvent);
            }
        }

        private void ClearSkillRuntimePresentation()
        {
            if (snapshot.IsPaused && snapshot.IsSecondaryCharging)
            {
                SuspendSecondaryChargeFeedback();
            }
            else
            {
                ClearSecondaryChargeFeedback();
            }
            Array.Clear(actionQueue, 0, actionQueue.Length);
            actionHead = 0;
            actionCount = 0;
            actionGap = false;
            Array.Clear(
                activePresentationQueue,
                0,
                activePresentationQueue.Length);
            activePresentationHead = 0;
            activePresentationCount = 0;
            ResetProjectilePresentation();
            ResetPlayerShotPresentation();
            skillPresentationWorld?.ClearRuntimePresentation();
            cameraFeedback?.ClearPresentationShake();
        }

        private void PresentActivePresentation(
            in FpgFormalPlayerActivePresentationEvent presentationEvent)
        {
            if (skillPresentationWorld == null
                || !skillPresentationWorld.Registry.TryResolve(
                    presentationEvent.Handle,
                    out FpgRegisteredPresentation registered))
            {
                SkillPresentationFaultCount++;
                return;
            }

            Transform source = playerEntity == null
                ? null
                : playerEntity.transform;
            if (registered.Kind == FpgRegisteredPresentationKind.Vfx
                && registered.Anchor == FpgVfxPresentationAnchor.OwnerSocket)
            {
                if (playerEntity == null
                    || !playerEntity.TryResolvePresentationSocket(
                        registered.SocketId,
                        out source))
                {
                    SkillPresentationFaultCount++;
                    return;
                }
            }

            if (source == null
                || !TryPresentActivePresentation(
                    presentationEvent,
                    registered,
                    source))
            {
                SkillPresentationFaultCount++;
                return;
            }

            try
            {
                ActivePresentationPresented?.Invoke(presentationEvent);
            }
            catch (Exception)
            {
                SkillPresentationFaultCount++;
            }
        }

        private bool TryPresentActivePresentation(
            in FpgFormalPlayerActivePresentationEvent presentationEvent,
            in FpgRegisteredPresentation registered,
            Transform source)
        {
            bool isHeldSecondaryCharge =
                presentationEvent.Slot == FpgPlayerSkillSlot.Secondary
                && presentationEvent.SequenceKind
                    == FpgSkillSequenceKind.ChargeEnter
                && presentationEvent.Kind == FpgActivePresentationKind.Vfx
                && registered.Kind == FpgRegisteredPresentationKind.Vfx
                && !presentationEvent.RequiresGameplayCommit;
            if (!isHeldSecondaryCharge)
            {
                return skillPresentationWorld.TryPresent(
                    presentationEvent.Handle,
                    source);
            }

            if (!ShouldShowSecondaryChargeFeedback())
            {
                return true;
            }

            ReleaseSecondaryChargeVfx();
            if (!skillPresentationWorld.TryBorrowHeldVfx(
                    presentationEvent.Handle,
                    source,
                    out GameObject instance)
                || instance == null)
            {
                return false;
            }

            secondaryChargeVfxHandle = presentationEvent.Handle;
            secondaryChargeVfxInstance = instance;
            secondaryChargeVfxSource = source;
            return skillPresentationWorld.TryUpdateHeldVfx(
                secondaryChargeVfxHandle,
                secondaryChargeVfxInstance,
                secondaryChargeVfxSource,
                snapshot.SecondaryChargeProgress);
        }

        private void UpdateSecondaryChargeFeedback()
        {
            bool visible = ShouldShowSecondaryChargeFeedback();
            CombatAimReticle reticle = playerTickDriver == null
                ? null
                : playerTickDriver.AimViewportSourceComponent
                    as CombatAimReticle;
            reticle?.SetChargeProgress(
                visible,
                visible ? snapshot.SecondaryChargeProgress : 0f);

            if (!visible)
            {
                if (snapshot.IsPaused && snapshot.IsSecondaryCharging)
                {
                    SuspendSecondaryChargeFeedback();
                }
                else
                {
                    ClearSecondaryChargeFeedback();
                }
                return;
            }

            if (secondaryChargeVfxInstance == null)
            {
                if (!secondaryChargeVfxHandle.IsValid
                    || secondaryChargeVfxSource == null
                    || skillPresentationWorld == null
                    || !skillPresentationWorld.TryBorrowHeldVfx(
                        secondaryChargeVfxHandle,
                        secondaryChargeVfxSource,
                        out secondaryChargeVfxInstance))
                {
                    return;
                }
            }

            if (secondaryChargeVfxSource == null
                || skillPresentationWorld == null
                || !skillPresentationWorld.TryUpdateHeldVfx(
                    secondaryChargeVfxHandle,
                    secondaryChargeVfxInstance,
                    secondaryChargeVfxSource,
                    snapshot.SecondaryChargeProgress))
            {
                SkillPresentationFaultCount++;
                ReleaseSecondaryChargeVfx();
            }
        }

        private bool ShouldShowSecondaryChargeFeedback()
        {
            return snapshot.IsSecondaryCharging
                && snapshot.IsCombatActive
                && snapshot.SecondaryChargeStartedTick.IsValid;
        }

        private void ClearSecondaryChargeFeedback()
        {
            CombatAimReticle reticle = playerTickDriver == null
                ? null
                : playerTickDriver.AimViewportSourceComponent
                    as CombatAimReticle;
            reticle?.SetChargeProgress(false, 0f);
            ReleaseSecondaryChargeVfx();
        }

        private void SuspendSecondaryChargeFeedback()
        {
            CombatAimReticle reticle = playerTickDriver == null
                ? null
                : playerTickDriver.AimViewportSourceComponent
                    as CombatAimReticle;
            reticle?.SetChargeProgress(false, 0f);
            ReleaseSecondaryChargeVfx(clearBinding: false);
        }

        private void ReleaseSecondaryChargeVfx(bool clearBinding = true)
        {
            GameObject instance = secondaryChargeVfxInstance;
            secondaryChargeVfxInstance = null;
            if (clearBinding)
            {
                secondaryChargeVfxHandle = default(FpgPresentationHandle);
                secondaryChargeVfxSource = null;
            }
            if (instance == null || !instance.activeSelf)
            {
                return;
            }

            if (skillPresentationWorld == null
                || !skillPresentationWorld.TryReleaseHeldVfx(instance))
            {
                SkillPresentationFaultCount++;
            }
        }

        private void RegisterActionPresentationBindings(
            in FpgFormalPlayerActionEvent action)
        {
            if (!action.HasSkillCorrelation
                || !action.AttackId.IsValid
                || !TryResolveActionPresentation(
                    action,
                    out FpgCompiledSkillActionPresentation presentation))
            {
                return;
            }

            if (presentation.ActionKind == FpgSkillActionKind.Attack
                && presentation.TrajectoryVfx.IsValid)
            {
                RegisterShotTrajectoryBinding(
                    action.AttackId,
                    presentation.TrajectoryVfx);
            }
            else if (presentation.ActionKind
                    == FpgSkillActionKind.LaunchProjectile
                && presentation.FlightVfx.IsValid)
            {
                RegisterProjectileFlightBinding(
                    action.AttackId,
                    presentation.FlightVfx);
            }
        }

        private bool TryResolveActionPresentation(
            in FpgFormalPlayerActionEvent action,
            out FpgCompiledSkillActionPresentation presentation)
        {
            presentation = default(FpgCompiledSkillActionPresentation);
            D0CharacterDefinition character = selection.CharacterDefinition;
            D0WeaponDefinition weapon = character == null
                ? null
                : character.Weapon;
            if (!action.HasSkillCorrelation || weapon == null)
            {
                return false;
            }

            FpgSkillTimelineDefinition skill = null;
            if (action.Type == FpgFormalPlayerActionType.ReloadCompleted)
            {
                skill = weapon.ReloadSkill;
            }
            else if (action.ReleaseKind == WeaponReleaseKind.Primary)
            {
                skill = weapon.PrimarySkill;
            }
            else if (action.ReleaseKind == WeaponReleaseKind.Secondary
                && weapon.TryResolveSecondarySkill(
                    selection.SelectedSecondaryTriggerMode,
                    out FpgPlayerSkillDefinition secondary,
                    out _))
            {
                skill = secondary;
            }

            return FpgSkillPresentationRegistry.TryResolveActionPresentation(
                skill,
                action.GameplayEventId,
                out presentation);
        }

        private void RegisterShotTrajectoryBinding(
            AttackId attackId,
            FpgPresentationHandle handle)
        {
            int slot = FindShotTrajectoryBinding(attackId);
            if (slot < 0)
            {
                slot = FindAvailableShotTrajectoryBinding();
            }

            shotTrajectoryBindings[slot] = new ShotTrajectoryBinding
            {
                IsUsed = true,
                AttackId = attackId,
                Handle = handle,
                Ordinal = ++nextActionPresentationBindingOrdinal
            };
        }

        private void RegisterProjectileFlightBinding(
            AttackId attackId,
            FpgPresentationHandle handle)
        {
            int slot = FindProjectileFlightBinding(attackId);
            if (slot < 0)
            {
                slot = FindAvailableProjectileFlightBinding();
            }

            projectileFlightBindings[slot] = new ProjectileFlightBinding
            {
                IsUsed = true,
                AttackId = attackId,
                Handle = handle,
                Ordinal = ++nextActionPresentationBindingOrdinal
            };
        }

        private void ConsumePlayerShotPresentation()
        {
            try
            {
                ConsumePlayerShotPresentationCore();
            }
            catch (Exception)
            {
                SkillPresentationFaultCount++;
                ResetPlayerShotPresentation();
            }
        }

        private void ConsumePlayerShotPresentationCore()
        {
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            IPlayerShotPresentationFeed feed = runtime == null
                    || runtime.IsDisposed
                ? null
                : runtime.PlayerShotPresentationFeed;
            if (!ReferenceEquals(feed, observedPlayerShotPresentationFeed)
                || feed != null
                    && feed.EventCapacity > playerShotPresentationEvents.Length)
            {
                if (!TryBindPlayerShotPresentationFeed(feed, out _))
                {
                    SkillPresentationFaultCount++;
                    return;
                }
            }

            if (feed == null)
            {
                return;
            }

            if (feed.LastSequence < playerShotPresentationCursor)
            {
                ResetPlayerShotPresentationCursor(feed);
            }

            int eventCount = feed.CopyEventsAfter(
                playerShotPresentationCursor,
                playerShotPresentationEvents,
                out bool hasGap);
            if (hasGap)
            {
                ClearShotTrajectoryBindings();
                playerShotPresentationCursor = feed.LastSequence;
                if (eventCount > 0)
                {
                    Array.Clear(
                        playerShotPresentationEvents,
                        0,
                        eventCount);
                }

                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                PlayerShotPresentationEvent presentationEvent =
                    playerShotPresentationEvents[index];
                playerShotPresentationCursor = Math.Max(
                    playerShotPresentationCursor,
                    presentationEvent.Sequence);
                PresentPlayerShotTrajectories(presentationEvent.Snapshot);
                playerShotPresentationEvents[index] =
                    default(PlayerShotPresentationEvent);
            }
        }

        private void PresentPlayerShotTrajectories(
            in PlayerShotPresentationSnapshot snapshot)
        {
            if (!TryTakeShotTrajectoryBinding(
                    snapshot.AttackId,
                    out FpgPresentationHandle handle))
            {
                return;
            }

            for (int index = 0; index < snapshot.TrajectoryCount; index++)
            {
                PlayerShotTrajectory trajectory = snapshot.GetTrajectory(index);
                Vector3 authoritativeStart =
                    ToWorldPosition(trajectory.Start);
                Vector3 presentationStart = ResolvePresentationSocketPosition(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    authoritativeStart);
                if (!skillPresentationWorld.TryPresentTrajectory(
                        handle,
                        presentationStart,
                        ToWorldPosition(trajectory.TerminalPoint)))
                {
                    SkillPresentationFaultCount++;
                }
            }
        }

        private bool TryBindPlayerShotPresentationFeed(
            IPlayerShotPresentationFeed feed,
            out string error)
        {
            if (feed == null)
            {
                ResetPlayerShotPresentation();
                error = string.Empty;
                return true;
            }

            if (feed.EventCapacity <= 0)
            {
                ResetPlayerShotPresentation();
                error =
                    "Player shot presentation feed capacity must be positive.";
                return false;
            }

            if (ReferenceEquals(feed, observedPlayerShotPresentationFeed)
                && playerShotPresentationEvents.Length >= feed.EventCapacity)
            {
                error = string.Empty;
                return true;
            }

            try
            {
                playerShotPresentationEvents =
                    new PlayerShotPresentationEvent[feed.EventCapacity];
                observedPlayerShotPresentationFeed = feed;
                playerShotPresentationCursor = feed.LastSequence;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ResetPlayerShotPresentation();
                error =
                    $"Unable to allocate player shot presentation buffer: {exception.Message}";
                return false;
            }
        }

        private void ResetPlayerShotPresentation()
        {
            observedPlayerShotPresentationFeed = null;
            playerShotPresentationCursor = 0L;
            ClearShotTrajectoryBindings();
            if (playerShotPresentationEvents.Length > 0)
            {
                Array.Clear(
                    playerShotPresentationEvents,
                    0,
                    playerShotPresentationEvents.Length);
            }
        }

        private void ResetPlayerShotPresentationCursor(
            IPlayerShotPresentationFeed feed)
        {
            playerShotPresentationCursor = feed == null
                ? 0L
                : feed.LastSequence;
            ClearShotTrajectoryBindings();
            if (playerShotPresentationEvents.Length > 0)
            {
                Array.Clear(
                    playerShotPresentationEvents,
                    0,
                    playerShotPresentationEvents.Length);
            }
        }

        private void ConsumePlayerProjectilePresentation()
        {
            try
            {
                ConsumePlayerProjectilePresentationCore();
            }
            catch (Exception)
            {
                SkillPresentationFaultCount++;
                ResetProjectilePresentation();
            }
        }

        private void ConsumePlayerProjectilePresentationCore()
        {
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            IProjectilePresentationFeed feed = runtime == null || runtime.IsDisposed
                ? null
                : runtime.ProjectilePresentationFeed;
            if (!ReferenceEquals(feed, observedProjectilePresentationFeed))
            {
                if (!TryBindProjectilePresentationFeed(feed, out _))
                {
                    SkillPresentationFaultCount++;
                    return;
                }
            }

            if (feed == null)
            {
                return;
            }

            if (feed.ActiveCapacity > projectilePresentationStates.Length
                || feed.EventCapacity > projectilePresentationEvents.Length
                || feed.ActiveCapacity > playerProjectileVisuals.Length)
            {
                if (!TryBindProjectilePresentationFeed(feed, out _))
                {
                    SkillPresentationFaultCount++;
                    return;
                }
            }

            if (feed.LastSequence < projectilePresentationCursor)
            {
                ResetProjectilePresentationCursor(feed);
            }

            int eventCount = feed.CopyEventsAfter(
                projectilePresentationCursor,
                projectilePresentationEvents,
                out bool hasGap);
            if (hasGap)
            {
                ClearPlayerProjectileVisuals();
                projectilePresentationCursor = feed.LastSequence;
                if (eventCount > 0)
                {
                    Array.Clear(
                        projectilePresentationEvents,
                        0,
                        eventCount);
                }

                SynchronizePlayerProjectileVisuals(feed);
                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                ProjectilePresentationEvent presentationEvent =
                    projectilePresentationEvents[index];
                projectilePresentationCursor = Math.Max(
                    projectilePresentationCursor,
                    presentationEvent.Sequence);
                if (presentationEvent.State.Request.Team == Team.Player)
                {
                    if (presentationEvent.Type
                        == ProjectilePresentationEventType.Spawn)
                    {
                        PresentPlayerProjectileSpawn(
                            presentationEvent.State);
                    }
                    else if (presentationEvent.Type
                        == ProjectilePresentationEventType.Terminal)
                    {
                        PresentPlayerProjectileTerminal(
                            presentationEvent);
                    }
                }

                projectilePresentationEvents[index] =
                    default(ProjectilePresentationEvent);
            }

            SynchronizePlayerProjectileVisuals(feed);
        }

        private bool TryBindProjectilePresentationFeed(
            IProjectilePresentationFeed feed,
            out string error)
        {
            if (feed == null)
            {
                ResetProjectilePresentation();
                error = string.Empty;
                return true;
            }

            if (feed.ActiveCapacity <= 0 || feed.EventCapacity <= 0)
            {
                ResetProjectilePresentation();
                error = "Projectile presentation feed capacities must be positive.";
                return false;
            }

            if (ReferenceEquals(feed, observedProjectilePresentationFeed)
                && projectilePresentationStates.Length >= feed.ActiveCapacity
                && projectilePresentationEvents.Length >= feed.EventCapacity
                && playerProjectileVisuals.Length >= feed.ActiveCapacity)
            {
                error = string.Empty;
                return true;
            }

            ClearPlayerProjectileVisuals();
            try
            {
                projectilePresentationStates =
                    new ProjectilePresentationState[feed.ActiveCapacity];
                projectilePresentationEvents =
                    new ProjectilePresentationEvent[feed.EventCapacity];
                playerProjectileVisuals =
                    new PlayerProjectileVisualSlot[feed.ActiveCapacity];
                observedProjectilePresentationFeed = feed;
                projectilePresentationCursor = feed.LastSequence;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ResetProjectilePresentation();
                error = $"Unable to allocate projectile presentation buffers: {exception.Message}";
                return false;
            }
        }

        private void SynchronizePlayerProjectileVisuals(
            IProjectilePresentationFeed feed)
        {
            int stateCount = feed.CopyActiveStates(
                projectilePresentationStates);
            ResetProjectileFlightBindingSnapshotFlags();
            for (int index = 0; index < playerProjectileVisuals.Length; index++)
            {
                PlayerProjectileVisualSlot slot = playerProjectileVisuals[index];
                slot.SeenInActiveSnapshot = false;
                playerProjectileVisuals[index] = slot;
            }

            for (int index = 0; index < stateCount; index++)
            {
                ProjectilePresentationState state =
                    projectilePresentationStates[index];
                if (state.Request.Team == Team.Player)
                {
                    MarkProjectileFlightBindingSeen(
                        state.Request.AttackId);
                    if (TryAcquirePlayerProjectileVisual(
                            state,
                            out int slotIndex))
                    {
                        UpdatePlayerProjectileVisual(slotIndex, state);
                        PlayerProjectileVisualSlot slot =
                            playerProjectileVisuals[slotIndex];
                        slot.SeenInActiveSnapshot = true;
                        playerProjectileVisuals[slotIndex] = slot;
                    }
                }

                projectilePresentationStates[index] =
                    default(ProjectilePresentationState);
            }

            for (int index = 0; index < playerProjectileVisuals.Length; index++)
            {
                if (playerProjectileVisuals[index].IsUsed
                    && !playerProjectileVisuals[index].SeenInActiveSnapshot)
                {
                    ReleasePlayerProjectileVisual(index);
                }
            }

            ClearCompletedProjectileFlightBindings();
        }

        private void PresentPlayerProjectileSpawn(
            in ProjectilePresentationState state)
        {
            MarkProjectileFlightBindingSeen(state.Request.AttackId);
            if (TryAcquirePlayerProjectileVisual(state, out int slotIndex))
            {
                UpdatePlayerProjectileVisual(slotIndex, state);
            }
        }

        private void PresentPlayerProjectileTerminal(
            in ProjectilePresentationEvent presentationEvent)
        {
            ProjectilePresentationState state = presentationEvent.State;
            int slotIndex = FindPlayerProjectileVisualSlot(state);
            if (slotIndex >= 0)
            {
                PlayerProjectileVisualSlot slot =
                    playerProjectileVisuals[slotIndex];
                Vector3 terminalPosition = ToWorldPosition(state.LastPoint);
                if (slot.Instance != null
                    && !skillPresentationWorld.TryUpdateFlightVfx(
                        slot.Handle,
                        slot.Instance,
                        terminalPosition,
                        ResolveProjectilePresentationRotation(
                            terminalPosition,
                            slot.LastVisualPosition,
                            ToWorldPosition(state.Path.End)
                                - slot.PresentationStart)))
                {
                    SkillPresentationFaultCount++;
                }

                ReleasePlayerProjectileVisual(slotIndex);
            }
        }

        private bool TryAcquirePlayerProjectileVisual(
            in ProjectilePresentationState state,
            out int slotIndex)
        {
            slotIndex = FindPlayerProjectileVisualSlot(state);
            if (slotIndex >= 0)
            {
                return true;
            }

            if (state.Request.Team != Team.Player
                || skillPresentationWorld == null
                || !TryGetProjectileFlightBinding(
                    state.Request.AttackId,
                    out FpgPresentationHandle handle))
            {
                return false;
            }

            slotIndex = FindFreePlayerProjectileVisualSlot();
            if (slotIndex < 0)
            {
                SkillPresentationFaultCount++;
                return false;
            }

            Vector3 presentationStart = ResolvePresentationSocketPosition(
                D0ActorSocketRegistry.SecondaryMuzzleId,
                ToWorldPosition(state.Path.Start));
            Vector3 presentationPoint = RemapProjectilePresentationPoint(
                state,
                presentationStart);
            if (!skillPresentationWorld.TryBorrowFlightVfx(
                    handle,
                    presentationPoint,
                    ResolveProjectilePresentationRotation(
                        presentationPoint,
                        presentationStart,
                        ToWorldPosition(state.Path.End)
                            - presentationStart),
                    out GameObject instance))
            {
                SkillPresentationFaultCount++;
                slotIndex = -1;
                return false;
            }

            playerProjectileVisuals[slotIndex] =
                new PlayerProjectileVisualSlot
                {
                    IsUsed = true,
                    Instance = instance,
                    Handle = handle,
                    State = state,
                    PresentationStart = presentationStart,
                    LastVisualPosition = presentationPoint
                };
            return true;
        }

        private void UpdatePlayerProjectileVisual(
            int slotIndex,
            in ProjectilePresentationState state)
        {
            if (slotIndex < 0 || slotIndex >= playerProjectileVisuals.Length)
            {
                return;
            }

            if (skillPresentationWorld == null)
            {
                ReleasePlayerProjectileVisual(slotIndex);
                return;
            }

            PlayerProjectileVisualSlot slot =
                playerProjectileVisuals[slotIndex];
            if (!slot.IsUsed)
            {
                return;
            }

            if (slot.Instance != null)
            {
                Vector3 presentationPoint = RemapProjectilePresentationPoint(
                    state,
                    slot.PresentationStart);
                if (!skillPresentationWorld.TryUpdateFlightVfx(
                    slot.Handle,
                    slot.Instance,
                    presentationPoint,
                    ResolveProjectilePresentationRotation(
                        presentationPoint,
                        slot.LastVisualPosition,
                        ToWorldPosition(state.Path.End)
                            - slot.PresentationStart)))
                {
                    SkillPresentationFaultCount++;
                }

                slot.LastVisualPosition = presentationPoint;
            }

            slot.State = state;
            playerProjectileVisuals[slotIndex] = slot;
        }

        private void ClearPlayerProjectileVisuals()
        {
            for (int index = 0; index < playerProjectileVisuals.Length; index++)
            {
                ReleasePlayerProjectileVisual(index);
            }
        }

        private void ReleasePlayerProjectileVisual(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= playerProjectileVisuals.Length)
            {
                return;
            }

            PlayerProjectileVisualSlot slot =
                playerProjectileVisuals[slotIndex];
            if (!slot.IsUsed)
            {
                return;
            }

            if (slot.Instance != null)
            {
                if (skillPresentationWorld == null
                    || !skillPresentationWorld.TryReleaseFlightVfx(
                        slot.Instance))
                {
                    SkillPresentationFaultCount++;
                }
            }

            playerProjectileVisuals[slotIndex] =
                default(PlayerProjectileVisualSlot);
        }

        private void ResetProjectilePresentation()
        {
            ClearPlayerProjectileVisuals();
            ClearProjectileFlightBindings();
            observedProjectilePresentationFeed = null;
            projectilePresentationCursor = 0L;
            if (projectilePresentationStates.Length > 0)
            {
                Array.Clear(
                    projectilePresentationStates,
                    0,
                    projectilePresentationStates.Length);
            }

            if (projectilePresentationEvents.Length > 0)
            {
                Array.Clear(
                    projectilePresentationEvents,
                    0,
                    projectilePresentationEvents.Length);
            }
        }

        private void ResetProjectilePresentationCursor(
            IProjectilePresentationFeed feed)
        {
            ClearPlayerProjectileVisuals();
            ClearProjectileFlightBindings();
            projectilePresentationCursor = feed == null
                ? 0L
                : feed.LastSequence;
            if (projectilePresentationStates.Length > 0)
            {
                Array.Clear(
                    projectilePresentationStates,
                    0,
                    projectilePresentationStates.Length);
            }

            if (projectilePresentationEvents.Length > 0)
            {
                Array.Clear(
                    projectilePresentationEvents,
                    0,
                    projectilePresentationEvents.Length);
            }
        }

        private int FindPlayerProjectileVisualSlot(
            in ProjectilePresentationState state)
        {
            for (int index = 0; index < playerProjectileVisuals.Length; index++)
            {
                PlayerProjectileVisualSlot slot =
                    playerProjectileVisuals[index];
                if (slot.IsUsed
                    && slot.State.Request.ProjectileId
                        == state.Request.ProjectileId
                    && slot.State.Request.RuntimeId
                        == state.Request.RuntimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreePlayerProjectileVisualSlot()
        {
            for (int index = 0; index < playerProjectileVisuals.Length; index++)
            {
                if (!playerProjectileVisuals[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindShotTrajectoryBinding(AttackId attackId)
        {
            for (int index = 0; index < shotTrajectoryBindings.Length; index++)
            {
                if (shotTrajectoryBindings[index].IsUsed
                    && shotTrajectoryBindings[index].AttackId == attackId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindAvailableShotTrajectoryBinding()
        {
            int oldest = 0;
            for (int index = 0; index < shotTrajectoryBindings.Length; index++)
            {
                if (!shotTrajectoryBindings[index].IsUsed)
                {
                    return index;
                }

                if (shotTrajectoryBindings[index].Ordinal
                    < shotTrajectoryBindings[oldest].Ordinal)
                {
                    oldest = index;
                }
            }

            return oldest;
        }

        private bool TryTakeShotTrajectoryBinding(
            AttackId attackId,
            out FpgPresentationHandle handle)
        {
            handle = default(FpgPresentationHandle);
            int slot = FindShotTrajectoryBinding(attackId);
            if (slot < 0)
            {
                return false;
            }

            handle = shotTrajectoryBindings[slot].Handle;
            shotTrajectoryBindings[slot] = default(ShotTrajectoryBinding);
            return handle.IsValid;
        }

        private void ClearShotTrajectoryBindings()
        {
            Array.Clear(
                shotTrajectoryBindings,
                0,
                shotTrajectoryBindings.Length);
        }

        private int FindProjectileFlightBinding(AttackId attackId)
        {
            for (int index = 0; index < projectileFlightBindings.Length; index++)
            {
                if (projectileFlightBindings[index].IsUsed
                    && projectileFlightBindings[index].AttackId == attackId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindAvailableProjectileFlightBinding()
        {
            int oldest = 0;
            for (int index = 0; index < projectileFlightBindings.Length; index++)
            {
                if (!projectileFlightBindings[index].IsUsed)
                {
                    return index;
                }

                if (projectileFlightBindings[index].Ordinal
                    < projectileFlightBindings[oldest].Ordinal)
                {
                    oldest = index;
                }
            }

            return oldest;
        }

        private bool TryGetProjectileFlightBinding(
            AttackId attackId,
            out FpgPresentationHandle handle)
        {
            handle = default(FpgPresentationHandle);
            int slot = FindProjectileFlightBinding(attackId);
            if (slot < 0)
            {
                return false;
            }

            handle = projectileFlightBindings[slot].Handle;
            return handle.IsValid;
        }

        private void ResetProjectileFlightBindingSnapshotFlags()
        {
            for (int index = 0;
                index < projectileFlightBindings.Length;
                index++)
            {
                ProjectileFlightBinding binding =
                    projectileFlightBindings[index];
                binding.SeenInActiveSnapshot = false;
                projectileFlightBindings[index] = binding;
            }
        }

        private void MarkProjectileFlightBindingSeen(AttackId attackId)
        {
            int slot = FindProjectileFlightBinding(attackId);
            if (slot < 0)
            {
                return;
            }

            ProjectileFlightBinding binding =
                projectileFlightBindings[slot];
            binding.SpawnObserved = true;
            binding.SeenInActiveSnapshot = true;
            projectileFlightBindings[slot] = binding;
        }

        private void ClearCompletedProjectileFlightBindings()
        {
            for (int index = 0;
                index < projectileFlightBindings.Length;
                index++)
            {
                ProjectileFlightBinding binding =
                    projectileFlightBindings[index];
                if (binding.IsUsed && binding.SpawnObserved
                    && !binding.SeenInActiveSnapshot)
                {
                    projectileFlightBindings[index] =
                        default(ProjectileFlightBinding);
                }
            }
        }

        private void ClearProjectileFlightBindings()
        {
            Array.Clear(
                projectileFlightBindings,
                0,
                projectileFlightBindings.Length);
        }

        private static Vector3 ToWorldPosition(SpatialVectorKey point)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(
                point.X * scale,
                point.Y * scale,
                point.Z * scale);
        }

        private Vector3 ResolvePresentationSocketPosition(
            string socketId,
            Vector3 authoritativeFallback)
        {
            return playerEntity != null
                && playerEntity.TryResolvePresentationSocket(
                    socketId,
                    out Transform anchor)
                && anchor != null
                    ? anchor.position
                    : authoritativeFallback;
        }

        private static Vector3 RemapProjectilePresentationPoint(
            in ProjectilePresentationState state,
            Vector3 presentationStart)
        {
            Vector3 authoritativeStart = ToWorldPosition(state.Path.Start);
            Vector3 authoritativeEnd = ToWorldPosition(state.Path.End);
            Vector3 authoritativePoint = ToWorldPosition(state.LastPoint);
            Vector3 authoritativePath = authoritativeEnd - authoritativeStart;
            float pathLengthSquared = authoritativePath.sqrMagnitude;
            float progress = pathLengthSquared <= 0.000001f
                ? 1f
                : Mathf.Clamp01(Vector3.Dot(
                    authoritativePoint - authoritativeStart,
                    authoritativePath) / pathLengthSquared);
            return Vector3.Lerp(presentationStart, authoritativeEnd, progress);
        }

        private static Quaternion ResolveProjectilePresentationRotation(
            Vector3 currentPoint,
            Vector3 previousPoint,
            Vector3 fallbackDirection)
        {
            Vector3 direction = currentPoint - previousPoint;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = fallbackDirection;
            }

            return direction.sqrMagnitude <= 0.000001f
                ? Quaternion.identity
                : Quaternion.LookRotation(direction, Vector3.up);
        }

        private void ApplyCoverPresentation(
            in FpgFormalPlayerPresentationSnapshot value)
        {
            playerEntity?.Barrier?.ApplyCommittedSnapshot(
                value,
                Time.unscaledDeltaTime);
        }

        private void ResetCoverPresentation()
        {
            playerEntity?.Barrier?.ResetPresentation();
        }

        private bool ConsumeCommittedActions()
        {
            bool primaryPresented = false;
            while (actionCount > 0)
            {
                FpgFormalPlayerActionEvent action = actionQueue[actionHead];
                actionQueue[actionHead] = default(FpgFormalPlayerActionEvent);
                actionHead = (actionHead + 1) % actionQueue.Length;
                actionCount--;

                switch (action.Type)
                {
                    case FpgFormalPlayerActionType.PrimaryReleaseCommitted:
                        actorPresenter.NotifyPrimarySkillCommitted();
                        primaryPresented = true;
                        break;
                    case FpgFormalPlayerActionType.SecondaryChargeStarted:
                        actorPresenter.NotifySecondaryChargeStarted();
                        break;
                    case FpgFormalPlayerActionType.SecondaryChargeCanceled:
                        actorPresenter.NotifySecondaryChargeCanceled();
                        break;
                    case FpgFormalPlayerActionType.SecondaryReleaseCommitted:
                        actorPresenter.NotifySecondaryReleaseCommitted();
                        break;
                    case FpgFormalPlayerActionType.ReloadStarted:
                        actorPresenter.NotifyReloadStarted();
                        break;
                    case FpgFormalPlayerActionType.ReloadCompleted:
                        actorPresenter.NotifyReloadCompleted();
                        PresentReloadSuccessAnimation(action);
                        break;
                }

                cameraFeedback.PresentCommittedAction(action);
            }

            return primaryPresented;
        }

        private void PresentReloadSuccessAnimation(
            in FpgFormalPlayerActionEvent action)
        {
            if (!action.HasSkillCorrelation
                || actorPresenter == null
                || !TryResolveReloadSuccessAnimationName(
                    action.GameplayEventId,
                    out string animationName))
            {
                return;
            }

            if (!actorPresenter.TryPlaySkillOneShotAnimation(
                    animationName,
                    false,
                    out _))
            {
                SkillPresentationFaultCount++;
            }
        }

        private bool TryResolveReloadSuccessAnimationName(
            int gameplayEventId,
            out string animationName)
        {
            animationName = string.Empty;
            D0CharacterDefinition character = selection.CharacterDefinition;
            FpgSkillTimelineDefinition skill = character == null
                    || character.Weapon == null
                ? null
                : character.Weapon.ReloadSkill;
            if (skill == null || gameplayEventId <= 0)
            {
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < skill.Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence =
                    skill.Sequences[sequenceIndex];
                if (sequence == null)
                {
                    continue;
                }

                for (int actionIndex = 0;
                    actionIndex < sequence.ReloadEvents.Count;
                    actionIndex++)
                {
                    FpgSkillReloadEventDefinition reload =
                        sequence.ReloadEvents[actionIndex];
                    if (reload != null
                        && FpgSkillStableId.CompileEvent(reload.EventId)
                            == gameplayEventId
                        && !string.IsNullOrWhiteSpace(
                            reload.SuccessAnimationName))
                    {
                        animationName = reload.SuccessAnimationName;
                        return true;
                    }
                }
            }

            return false;
        }

        private PresentationFrameFlags ConsumeCombatTrace()
        {
            PresentationFrameFlags flags = default(PresentationFrameFlags);
            FpgFormalCombatRuntimeBundle runtime = encounterDirector.CombatRuntime;
            CombatTrace trace = runtime == null || runtime.IsDisposed
                ? null
                : runtime.CombatKernel.Trace;
            if (!ReferenceEquals(trace, observedTrace))
            {
                observedTrace = trace;
                nextCombatEventOrdinal = trace == null ? 0L : trace.TotalEventCount;
                return flags;
            }

            if (trace == null)
            {
                return flags;
            }

            long total = trace.TotalEventCount;
            long oldest = total - trace.Count;
            if (nextCombatEventOrdinal < oldest || nextCombatEventOrdinal > total)
            {
                nextCombatEventOrdinal = total;
                flags.HasGap = true;
                return flags;
            }

            for (long ordinal = nextCombatEventOrdinal; ordinal < total; ordinal++)
            {
                CombatEvent combatEvent = trace.GetOldest((int)(ordinal - oldest));
                if (combatEvent.TargetId != snapshot.PlayerRuntimeId)
                {
                    continue;
                }

                switch (combatEvent.EventType)
                {
                    case CombatEventType.DamageApplied:
                        if (combatEvent.DamageChannel == DamageChannel.Life)
                        {
                            actorPresenter.PlayHit();
                            flags.PlayerHit = true;
                        }
                        break;
                    case CombatEventType.BarrierBroken:
                        actorPresenter.PlayGroggy();
                        flags.BarrierBroken = true;
                        break;
                    case CombatEventType.Death:
                        actorPresenter.PlayDefeat();
                        flags.Defeat = true;
                        break;
                }
            }

            nextCombatEventOrdinal = total;
            return flags;
        }

        private void ApplySnapshotTransitions(
            in FpgFormalPlayerPresentationSnapshot previous,
            in FpgFormalPlayerPresentationSnapshot current,
            bool primaryPresented,
            in PresentationFrameFlags traceFlags)
        {
            if (!current.IsValid)
            {
                return;
            }

            if (current.PresentationState == FpgFormalPlayerPresentationState.Victory
                && previous.PresentationState
                    != FpgFormalPlayerPresentationState.Victory)
            {
                actorPresenter.PlayVictory();
                return;
            }

            if (current.PresentationState == FpgFormalPlayerPresentationState.Defeat
                && previous.PresentationState
                    != FpgFormalPlayerPresentationState.Defeat
                && !traceFlags.Defeat)
            {
                actorPresenter.PlayDefeat();
                return;
            }

            if (previous.IsValid)
            {
                if (current.Life < previous.Life && !traceFlags.PlayerHit)
                {
                    actorPresenter.PlayHit();
                }

                if (previous.CurrentCoverId == current.CurrentCoverId
                    && previous.CoverDurability > 0
                    && current.CoverDurability <= 0
                    && !traceFlags.BarrierBroken)
                {
                    actorPresenter.PlayGroggy();
                }

                if (!primaryPresented
                    && (actionGap || traceFlags.HasGap)
                    && previous.WeaponState != WeaponState.PrimaryRecovery
                    && current.WeaponState == WeaponState.PrimaryRecovery)
                {
                    actorPresenter.NotifyPrimarySkillCommitted();
                }
            }

            ApplyDurableActorState(current);
            actionGap = false;
        }

        private void ApplyDurableActorState(
            in FpgFormalPlayerPresentationSnapshot value)
        {
            if (!value.IsValid
                || value.PresentationState == FpgFormalPlayerPresentationState.Victory
                || value.PresentationState == FpgFormalPlayerPresentationState.Defeat)
            {
                return;
            }

            if (value.WeaponState == WeaponState.Reloading)
            {
                if (!actorPresenter.IsReloading)
                {
                    actorPresenter.NotifyReloadStarted();
                }
                return;
            }

            if (value.WeaponState == WeaponState.AltCharging)
            {
                if (!actorPresenter.IsChargingSecondary)
                {
                    actorPresenter.NotifySecondaryChargeStarted();
                }
                return;
            }

            if (actorPresenter.IsReloading)
            {
                actorPresenter.NotifyReloadCompleted();
                actorPresenter.ReturnToIdle();
            }

            if (actorPresenter.IsChargingSecondary)
            {
                actorPresenter.NotifySecondaryChargeCanceled();
                actorPresenter.ReturnToIdle();
            }
        }

        private FpgFormalPlayerPresentationSnapshot ApplyVitalsChanges(
            in FpgFormalPlayerPresentationSnapshot fallback)
        {
            try
            {
                return ApplyVitalsChangesCore(fallback);
            }
            catch (Exception)
            {
                VitalsGapCount++;
                observedVitalsRuntime = null;
                vitalsCursor = 0L;
                if (vitalsBuffer != null && vitalsBuffer.Length > 0)
                {
                    Array.Clear(vitalsBuffer, 0, vitalsBuffer.Length);
                }
                return fallback;
            }
        }

        private FpgFormalPlayerPresentationSnapshot ApplyVitalsChangesCore(
            in FpgFormalPlayerPresentationSnapshot fallback)
        {
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!ReferenceEquals(runtime, observedVitalsRuntime))
            {
                observedVitalsRuntime = runtime;
                vitalsCursor = 0L;
            }

            if (runtime == null || runtime.IsDisposed
                || !fallback.PlayerRuntimeId.IsValid)
            {
                return fallback;
            }

            IFpgVitalsView view = runtime.CombatPort.Vitals;
            FpgVitalsSnapshot latest = default(FpgVitalsSnapshot);
            bool hasLatest = false;
            if (view.LastSequence < vitalsCursor)
            {
                VitalsGapCount++;
                vitalsCursor = 0L;
            }

            if (view.EventCapacity > vitalsBuffer.Length)
            {
                if (vitalsCursor != view.LastSequence)
                {
                    VitalsGapCount++;
                }
                vitalsCursor = view.LastSequence;
                hasLatest = view.TryGetLatest(
                    fallback.PlayerRuntimeId,
                    out latest);
            }
            else
            {
                try
                {
                    int count = view.CopyChangesAfter(
                        vitalsCursor,
                        vitalsBuffer,
                        out bool hasGap);
                    if (hasGap)
                    {
                        VitalsGapCount++;
                        hasLatest = view.TryGetLatest(
                            fallback.PlayerRuntimeId,
                            out latest);
                    }
                    else
                    {
                        for (int index = 0; index < count; index++)
                        {
                            FpgVitalsSnapshot candidate = vitalsBuffer[index];
                            vitalsCursor = Math.Max(
                                vitalsCursor,
                                candidate.Sequence);
                            if (candidate.RuntimeId == fallback.PlayerRuntimeId)
                            {
                                latest = candidate;
                                hasLatest = true;
                            }
                            vitalsBuffer[index] = default(FpgVitalsSnapshot);
                        }
                    }

                    if (hasGap)
                    {
                        vitalsCursor = view.LastSequence;
                    }
                }
                catch (Exception)
                {
                    VitalsGapCount++;
                    vitalsCursor = view.LastSequence;
                    hasLatest = view.TryGetLatest(
                        fallback.PlayerRuntimeId,
                        out latest);
                }
            }

            if (!hasLatest)
            {
                return fallback;
            }

            return new FpgFormalPlayerPresentationSnapshot(
                fallback.Tick,
                fallback.PlayerRuntimeId,
                fallback.EncounterPhase,
                fallback.IsPaused,
                latest.Life,
                latest.MaxLife,
                fallback.CoverDurability,
                fallback.MaxCoverDurability,
                fallback.Ammo,
                fallback.MagazineCapacity,
                fallback.ExposureState,
                fallback.WeaponState,
                fallback.IsSecondaryCharging,
                fallback.SecondaryChargeProgress,
                fallback.SecondaryChargeStartedTick,
                fallback.IsCoverPeekRequested,
                fallback.CoverPeekStartedTick,
                fallback.CurrentCoverId,
                fallback.IsCoverDestroyed,
                fallback.IsCoverMoving);
        }

        private void ResetEventCursors()
        {
            for (int index = 0; index < actionQueue.Length; index++)
            {
                actionQueue[index] = default(FpgFormalPlayerActionEvent);
            }
            actionHead = 0;
            actionCount = 0;
            actionGap = false;
            if (hasActiveSkillSequence && actorPresenter != null)
            {
                actorPresenter.CancelSkillAnimation(
                    activeSkillSequence.ExecutionId);
            }
            Array.Clear(
                skillSequenceQueue,
                0,
                skillSequenceQueue.Length);
            Array.Clear(
                activePresentationQueue,
                0,
                activePresentationQueue.Length);
            skillSequenceHead = 0;
            skillSequenceCount = 0;
            activePresentationHead = 0;
            activePresentationCount = 0;
            skillSequenceGap = false;
            hasActiveSkillSequence = false;
            activeSkillSequence =
                default(FpgFormalPlayerSkillSequenceEvent);
            nextCombatEventOrdinal = 0L;
            observedVitalsRuntime = null;
            vitalsCursor = 0L;
            if (vitalsBuffer.Length > 0)
            {
                Array.Clear(vitalsBuffer, 0, vitalsBuffer.Length);
            }
            ResetProjectilePresentation();
            ResetPlayerShotPresentation();
            nextActionPresentationBindingOrdinal = 0L;
        }

        private void OnEnable()
        {
            if (!active)
            {
                return;
            }

            Subscribe();
            ResetCoverPresentation();
            ClearSecondaryChargeFeedback();
            skillVfxWorld?.BeginCombat();
            actorPresenter?.ClearAndReturnToIdle();
            cameraFeedback?.ResetRuntimeFeedback();
            playerHud?.Clear();
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            ResetEventCursors();
            observedTrace = encounterDirector == null
                || encounterDirector.CombatRuntime == null
                || encounterDirector.CombatRuntime.IsDisposed
                ? null
                : encounterDirector.CombatRuntime.CombatKernel.Trace;
            nextCombatEventOrdinal =
                observedTrace == null ? 0L : observedTrace.TotalEventCount;
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetCoverPresentation();
            ClearSecondaryChargeFeedback();
            ClearPlayerProjectileVisuals();
            skillVfxWorld?.EndCombat();
            skillPresentationWorld?.ClearRuntimePresentation();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private struct PlayerProjectileVisualSlot
        {
            public bool IsUsed;
            public bool SeenInActiveSnapshot;
            public ProjectilePresentationState State;
            public Vector3 PresentationStart;
            public Vector3 LastVisualPosition;
            public FpgPresentationHandle Handle;
            public GameObject Instance;
        }

        private struct ShotTrajectoryBinding
        {
            public bool IsUsed;
            public AttackId AttackId;
            public FpgPresentationHandle Handle;
            public long Ordinal;
        }

        private struct ProjectileFlightBinding
        {
            public bool IsUsed;
            public bool SpawnObserved;
            public bool SeenInActiveSnapshot;
            public AttackId AttackId;
            public FpgPresentationHandle Handle;
            public long Ordinal;
        }

        private struct PresentationFrameFlags
        {
            public bool PlayerHit;
            public bool BarrierBroken;
            public bool Defeat;
            public bool HasGap;
        }

        private void HandleEncounterLifecycle(
            FpgEncounterLifecycleEvent lifecycle)
        {
            if (lifecycle.Type == FpgEncounterLifecycleEventType.Defeated
                || lifecycle.Type == FpgEncounterLifecycleEventType.Failed
                || lifecycle.Type == FpgEncounterLifecycleEventType.Faulted
                || lifecycle.Type == FpgEncounterLifecycleEventType.Disposed)
            {
                playerTickDriver?.CoverTraversalPresenter?.Cancel();
                ResetCoverPresentation();
                ClearSecondaryChargeFeedback();
                ClearPlayerProjectileVisuals();
                skillVfxWorld?.ClearActive();
                skillPresentationWorld?.ClearRuntimePresentation();
                cameraFeedback?.ResetRuntimeFeedback();
                return;
            }

            if (lifecycle.Type != FpgEncounterLifecycleEventType.Restarted)
            {
                return;
            }

            actorPresenter?.SetPaused(false);
            actorPresenter?.ClearAndReturnToIdle();
            ResetCoverPresentation();
            ClearSecondaryChargeFeedback();
            ClearPlayerProjectileVisuals();
            skillVfxWorld?.ClearActive();
            skillPresentationWorld?.ClearRuntimePresentation();
            cameraFeedback?.ResetRuntimeFeedback();
            playerHud?.Clear();
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            observedTrace = encounterDirector == null
                || encounterDirector.CombatRuntime == null
                || encounterDirector.CombatRuntime.IsDisposed
                ? null
                : encounterDirector.CombatRuntime.CombatKernel.Trace;
            ResetEventCursors();
        }
}
}
