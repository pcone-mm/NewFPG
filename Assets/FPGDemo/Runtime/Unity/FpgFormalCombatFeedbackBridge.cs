using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgFormalCombatFeedbackBridge : MonoBehaviour
    {
        [Header("Formal runtime")]
        [SerializeField] private FpgRoomEncounterDirector encounterDirector;
        [SerializeField] private FpgFormalPlayerTickDriver playerTickDriver;
        [SerializeField] private CombatPresentationProfile presentationProfile;
        [SerializeField] private CombatAimReticle aimReticle;

        [Header("Combat VFX")]
        [SerializeField] private D0CombatVfxWorld skillVfxWorld;
        [SerializeField] private FpgSkillPresentationWorld
            skillPresentationWorld;

        [Header("Screen-space damage pool")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private FpgDamagePopupView popupPrefab;
        [SerializeField, Min(1)] private int feedbackReadCapacity = 128;

        private FpgDamagePopupView[] popupPool = Array.Empty<FpgDamagePopupView>();
        private FpgResolvedDamageFeedback[] feedbackBuffer =
            Array.Empty<FpgResolvedDamageFeedback>();
        private Vector2[] framePositions = Array.Empty<Vector2>();
        private FpgFormalCombatRuntimeBundle observedRuntime;
        private readonly FpgSkillImpactPresentationConsumer
            skillImpactPresentationConsumer =
                new FpgSkillImpactPresentationConsumer();
        private IProjectilePresentationFeed observedEnemyProjectileFeed;
        private ProjectilePresentationState[] enemyProjectileStates =
            Array.Empty<ProjectilePresentationState>();
        private ProjectilePresentationEvent[] enemyProjectileEvents =
            Array.Empty<ProjectilePresentationEvent>();
        private EnemyProjectileVisualSlot[] enemyProjectileVisuals =
            Array.Empty<EnemyProjectileVisualSlot>();
        private EnemyProjectileFlightBinding[] enemyProjectileBindings =
            Array.Empty<EnemyProjectileFlightBinding>();
        private long damageCursor;
        private long enemyProjectileCursor;
        private long nextEnemyProjectileBindingOrdinal;
        private int framePositionCount;
        private bool prepared;
        private bool presentationPaused;
        private bool actionSubscribed;
        private bool lifecycleSubscribed;
        private EnemySkillWarningBinding[] enemySkillWarnings =
            Array.Empty<EnemySkillWarningBinding>();
        [NonSerialized]
        private IFpgFormalEnemySkillPresentationConsumer
            enemySkillPresentationConsumer;

        private int activeEnemySkillWarningCount;

        public int DroppedPoolCount { get; private set; }
        public int DroppedProjectionCount { get; private set; }
        public int FeedbackGapCount { get; private set; }
        public int PresentationFaultCount { get; private set; }
        public int ReticleFeedbackFaultCount { get; private set; }
        public int PrepareFaultCount { get; private set; }
        public int EnemySkillTimelineFaultCount { get; private set; }
        public int EnemySkillWarningStartCount { get; private set; }
        public int EnemySkillWarningEndCount { get; private set; }
        public int EnemyActivePresentationCount { get; private set; }
        public int ActiveEnemySkillWarningCount =>
            activeEnemySkillWarningCount;
        public D0CombatVfxWorld SkillVfxWorld => skillVfxWorld;
        public string LastPrepareError { get; private set; } = string.Empty;
        public event Action<FpgFormalEnemySkillWarningPresentationEvent>
            EnemySkillWarningChanged;

        public int ActivePopupCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < popupPool.Length; index++)
                {
                    if (popupPool[index] != null && popupPool[index].IsActive)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        private void Awake()
        {
            TryPrepareWithDiagnostics();
        }

        private void OnEnable()
        {
            if (!prepared)
            {
                TryPrepareWithDiagnostics();
            }

            if (!prepared)
            {
                return;
            }

            UnsubscribeFromActions();
            UnsubscribeFromLifecycle();
            ResetRuntimePresentationState();
            SubscribeToActions();
            SubscribeToLifecycle();
        }

        private void LateUpdate()
        {
            if (!prepared)
            {
                return;
            }

            bool paused = encounterDirector != null && encounterDirector.IsPaused;
            float deltaTime = Time.unscaledDeltaTime;
            for (int index = 0; index < popupPool.Length; index++)
            {
                popupPool[index]?.Advance(deltaTime, paused);
            }

            if (paused)
            {
                if (!presentationPaused)
                {
                    ResetRuntimePresentationState();
                    skillPresentationWorld?.ClearRuntimePresentation();
                }

                presentationPaused = true;
                return;
            }

            presentationPaused = false;

            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!ReferenceEquals(runtime, observedRuntime))
            {
                bool replacingRuntime = observedRuntime != null;
                observedRuntime = runtime;
                damageCursor = 0L;
                IFpgSkillImpactPresentationView impactFeed =
                    runtime == null || runtime.IsDisposed
                        || runtime.CombatPort == null
                            ? null
                            : runtime.CombatPort.SkillImpactPresentation;
                if (!ReferenceEquals(
                        skillImpactPresentationConsumer.ObservedFeed,
                        impactFeed))
                {
                    RebindSkillImpactConsumer(runtime);
                }
                if (replacingRuntime)
                {
                    ResetEnemyProjectilePresentation();
                }
                ReleaseAll();
                TryResetReticle();
            }

            if (runtime == null || runtime.IsDisposed)
            {
                return;
            }

            skillImpactPresentationConsumer.Consume();
            ConsumeEnemyProjectilePresentation(
                runtime.ProjectilePresentationFeed);

            IFpgResolvedDamageFeedbackView feed = runtime.CombatPort.DamageFeedback;
            if (feed.Capacity > feedbackBuffer.Length)
            {
                if (feed.LastSequence > damageCursor)
                {
                    DiscardFeedbackBatch(feed, feedbackBuffer.Length);
                }
                return;
            }

            int count;
            bool hasGap;
            try
            {
                count = feed.CopyAfter(damageCursor, feedbackBuffer, out hasGap);
            }
            catch (Exception)
            {
                DiscardFeedbackBatch(feed, feedbackBuffer.Length);
                return;
            }

            if (hasGap)
            {
                DiscardFeedbackBatch(feed, count);
                return;
            }

            framePositionCount = 0;
            for (int index = 0; index < count; index++)
            {
                FpgResolvedDamageFeedback feedback = feedbackBuffer[index];
                feedbackBuffer[index] = default(FpgResolvedDamageFeedback);
                damageCursor = Math.Max(damageCursor, feedback.Sequence);
                if (feedback.SourceId != runtime.Player.RuntimeId)
                {
                    continue;
                }

                TryPresentReticleHit();
                TryPresent(feedback);
            }
        }

        public bool TryPrepare(out string error)
        {
            error = string.Empty;
            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            if (encounterDirector == null || playerTickDriver == null
                || presentationProfile == null || aimReticle == null
                || worldCamera == null || targetCanvas == null
                || popupRoot == null || popupPrefab == null
                || feedbackReadCapacity <= 0
                || !presentationProfile.TryValidateStatic(out error)
                || !popupPrefab.TryValidate(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal combat feedback bridge references are incomplete.";
                }
                return false;
            }

            if (!aimReticle.TrySetPresentationProfile(
                    presentationProfile,
                    out error))
            {
                return false;
            }

            int poolCapacity = presentationProfile.PoolCapacities.HitTipCapacity;
            popupPool = new FpgDamagePopupView[poolCapacity];
            framePositions = new Vector2[poolCapacity];
            feedbackBuffer = new FpgResolvedDamageFeedback[feedbackReadCapacity];
            int enemyProjectileCapacity = presentationProfile.PoolCapacities
                .EnemyProjectileCapacity;
            enemyProjectileStates = new ProjectilePresentationState[
                enemyProjectileCapacity];
            enemyProjectileEvents = new ProjectilePresentationEvent[
                feedbackReadCapacity];
            enemyProjectileVisuals = new EnemyProjectileVisualSlot[
                enemyProjectileCapacity];
            enemyProjectileBindings = new EnemyProjectileFlightBinding[
                enemyProjectileCapacity];
            enemySkillWarnings = new EnemySkillWarningBinding[
                presentationProfile.PoolCapacities
                    .ThreatTelegraphCapacity];
            activeEnemySkillWarningCount = 0;
            try
            {
                for (int index = 0; index < popupPool.Length; index++)
                {
                    FpgDamagePopupView view =
                        Instantiate(popupPrefab, popupRoot, false);
                    popupPool[index] = view;
                    view.gameObject.name = "DamagePopup_" + index;
                    if (!view.TryValidate(out error))
                    {
                        DestroyPreparedPool();
                        return false;
                    }
                    view.Release();
                }
            }
            catch (Exception exception)
            {
                DestroyPreparedPool();
                error = "Formal damage-popup pool preparation failed: "
                    + exception.Message;
                return false;
            }

            prepared = true;
            if (isActiveAndEnabled)
            {
                SubscribeToActions();
                SubscribeToLifecycle();
            }
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!prepared)
            {
                return TryPrepare(out error);
            }

            if (popupPool.Length != presentationProfile.PoolCapacities.HitTipCapacity
                || feedbackBuffer.Length != feedbackReadCapacity
                || enemyProjectileEvents.Length != feedbackReadCapacity
                || enemyProjectileStates.Length
                    != presentationProfile.PoolCapacities
                        .EnemyProjectileCapacity
                || enemyProjectileVisuals.Length
                    != presentationProfile.PoolCapacities
                        .EnemyProjectileCapacity
                || enemyProjectileBindings.Length
                    != presentationProfile.PoolCapacities
                        .EnemyProjectileCapacity
                || enemySkillWarnings.Length
                    != presentationProfile.PoolCapacities
                        .ThreatTelegraphCapacity)
            {
                error = "Formal combat feedback fixed capacities changed after preparation.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Clear()
        {
            ResetRuntimePresentationState();
            DroppedPoolCount = 0;
            DroppedProjectionCount = 0;
            FeedbackGapCount = 0;
            PresentationFaultCount = 0;
            ReticleFeedbackFaultCount = 0;
            PrepareFaultCount = 0;
            EnemySkillTimelineFaultCount = 0;
            EnemySkillWarningStartCount = 0;
            EnemySkillWarningEndCount = 0;
            EnemyActivePresentationCount = 0;
            presentationPaused = false;
            LastPrepareError = string.Empty;
        }

        private bool TryPrepareWithDiagnostics()
        {
            if (TryPrepare(out string error))
            {
                LastPrepareError = string.Empty;
                return true;
            }

            error = string.IsNullOrWhiteSpace(error)
                ? "Formal combat feedback bridge preparation failed."
                : error;
            if (!string.Equals(
                    LastPrepareError,
                    error,
                    StringComparison.Ordinal))
            {
                PrepareFaultCount++;
            }

            LastPrepareError = error;
            return false;
        }

        private void SubscribeToActions()
        {
            if (actionSubscribed || playerTickDriver == null)
            {
                return;
            }

            playerTickDriver.ActionCommitted += HandleActionCommitted;
            actionSubscribed = true;
        }

        private void UnsubscribeFromActions()
        {
            if (!actionSubscribed || playerTickDriver == null)
            {
                return;
            }

            playerTickDriver.ActionCommitted -= HandleActionCommitted;
            actionSubscribed = false;
        }

        private void SubscribeToLifecycle()
        {
            if (lifecycleSubscribed || encounterDirector == null)
            {
                return;
            }

            encounterDirector.LifecycleEvent += HandleEncounterLifecycle;
            encounterDirector.EnemySkillTimelineEvent +=
                HandleEnemySkillTimelineEvent;
            lifecycleSubscribed = true;
        }

        private void UnsubscribeFromLifecycle()
        {
            if (!lifecycleSubscribed)
            {
                return;
            }

            if (encounterDirector != null)
            {
                encounterDirector.LifecycleEvent -= HandleEncounterLifecycle;
                encounterDirector.EnemySkillTimelineEvent -=
                    HandleEnemySkillTimelineEvent;
            }

            lifecycleSubscribed = false;
        }

        private void HandleEncounterLifecycle(
            FpgEncounterLifecycleEvent lifecycle)
        {
            if (lifecycle.Type == FpgEncounterLifecycleEventType.Defeated)
            {
                ClearEnemySkillWarnings();
                return;
            }

            if (lifecycle.Type == FpgEncounterLifecycleEventType.Restarted
                || lifecycle.Type
                    == FpgEncounterLifecycleEventType.RoomCleared
                || lifecycle.Type
                    == FpgEncounterLifecycleEventType.Failed
                || lifecycle.Type
                    == FpgEncounterLifecycleEventType.Faulted
                || lifecycle.Type
                    == FpgEncounterLifecycleEventType.Disposed)
            {
                ResetRuntimePresentationState();
            }
        }

        private void HandleEnemySkillTimelineEvent(
            FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            if (skillEvent.Event.Kind
                    == FPG.Demo.Skills.FpgSkillEventKind.GameplayAction)
            {
                PresentEnemyAttackTrajectory(skillEvent);
                RegisterEnemySkillImpactPresentation(skillEvent);
                RegisterEnemyProjectileFlightPresentation(skillEvent);
                return;
            }

            if (skillEvent.Event.Kind
                    == FPG.Demo.Skills.FpgSkillEventKind.ActivePresentation)
            {
                PresentEnemyActivePresentation(skillEvent);
                return;
            }

            if (skillEvent.Event.Kind
                    == FPG.Demo.Skills.FpgSkillEventKind.WarningStarted
                || skillEvent.Event.Kind
                    == FPG.Demo.Skills.FpgSkillEventKind.WarningEnded)
            {
                PresentEnemySkillWarning(skillEvent);
            }
        }

        private void PresentEnemyAttackTrajectory(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            if (skillEvent.Outcome != FpgSkillEventOutcome.Triggered
                || !skillEvent.HasGameplayAction
                || skillEvent.Definition == null
                || !FpgSkillPresentationRegistry.TryResolveActionPresentation(
                    skillEvent.Definition,
                    skillEvent.Event.EventId,
                    out FpgCompiledSkillActionPresentation presentation)
                || presentation.ActionKind != FpgSkillActionKind.Attack
                || !presentation.TrajectoryVfx.IsValid)
            {
                return;
            }

            if (skillPresentationWorld == null && skillVfxWorld != null)
            {
                skillPresentationWorld = skillVfxWorld
                    .GetComponent<FpgSkillPresentationWorld>();
            }

            if (!skillEvent.SpatialContext.IsValid
                || skillPresentationWorld == null
                || !skillPresentationWorld.IsPrepared
                || !skillPresentationWorld.TryPresentTrajectory(
                    presentation.TrajectoryVfx,
                    ToWorldPosition(skillEvent.SpatialContext.Origin),
                    ToWorldPosition(skillEvent.SpatialContext.Target)))
            {
                PresentationFaultCount++;
            }
        }

        private void PresentEnemySkillWarning(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            if (!FpgEnemySkillPresentationResolver.TryResolveWarning(
                    skillEvent.Definition,
                    skillEvent.RuntimeEvent.SequenceKind,
                    skillEvent.Event,
                    out FpgResolvedEnemySkillWarning resolved))
            {
                IncrementEnemySkillTimelineFaultCount();
                return;
            }

            bool started = skillEvent.Event.Kind
                == FPG.Demo.Skills.FpgSkillEventKind.WarningStarted;
            if (started)
            {
                if (skillEvent.Outcome
                        != FPG.Demo.Skills.FpgSkillEventOutcome.Triggered)
                {
                    return;
                }

                if (!TryActivateEnemySkillWarning(skillEvent))
                {
                    IncrementEnemySkillTimelineFaultCount();
                    return;
                }
            }
            else if ((skillEvent.Outcome
                        != FPG.Demo.Skills.FpgSkillEventOutcome.Triggered
                    && skillEvent.Outcome
                        != FPG.Demo.Skills.FpgSkillEventOutcome.Canceled)
                || !TryReleaseEnemySkillWarning(skillEvent))
            {
                return;
            }

            FpgFormalEnemySkillWarningPresentationEvent presentationEvent =
                new FpgFormalEnemySkillWarningPresentationEvent(
                    skillEvent,
                    resolved,
                    started);
            if (!TrySetEnemySkillWarningThroughProduction(
                    presentationEvent))
            {
                if (started)
                {
                    TryReleaseEnemySkillWarning(skillEvent);
                }

                IncrementEnemySkillTimelineFaultCount();
                return;
            }

            if (started)
            {
                if (EnemySkillWarningStartCount < int.MaxValue)
                {
                    EnemySkillWarningStartCount++;
                }
            }
            else if (EnemySkillWarningEndCount < int.MaxValue)
            {
                EnemySkillWarningEndCount++;
            }

            try
            {
                EnemySkillWarningChanged?.Invoke(presentationEvent);
            }
            catch (Exception)
            {
                IncrementEnemySkillTimelineFaultCount();
            }
        }

        private bool TryActivateEnemySkillWarning(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            int existing = FindEnemySkillWarning(
                skillEvent.OwnerRuntimeId,
                skillEvent.SpawnSequence,
                skillEvent.RuntimeEvent.ExecutionId.Value,
                skillEvent.Event.WarningId);
            if (existing >= 0)
            {
                return true;
            }

            for (int index = 0;
                index < enemySkillWarnings.Length;
                index++)
            {
                if (enemySkillWarnings[index].IsActive)
                {
                    continue;
                }

                enemySkillWarnings[index] =
                    new EnemySkillWarningBinding(
                        skillEvent.OwnerRuntimeId,
                        skillEvent.SpawnSequence,
                        skillEvent.RuntimeEvent.ExecutionId.Value,
                        skillEvent.Event.WarningId);
                activeEnemySkillWarningCount++;
                return true;
            }

            return false;
        }

        private bool TryReleaseEnemySkillWarning(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            int index = FindEnemySkillWarning(
                skillEvent.OwnerRuntimeId,
                skillEvent.SpawnSequence,
                skillEvent.RuntimeEvent.ExecutionId.Value,
                skillEvent.Event.WarningId);
            if (index < 0)
            {
                return false;
            }

            enemySkillWarnings[index] =
                default(EnemySkillWarningBinding);
            activeEnemySkillWarningCount = Math.Max(
                0,
                activeEnemySkillWarningCount - 1);
            return true;
        }

        private int FindEnemySkillWarning(
            RuntimeId ownerRuntimeId,
            int spawnSequence,
            long executionId,
            int warningId)
        {
            for (int index = 0;
                index < enemySkillWarnings.Length;
                index++)
            {
                EnemySkillWarningBinding binding =
                    enemySkillWarnings[index];
                if (binding.IsActive
                    && binding.OwnerRuntimeId == ownerRuntimeId
                    && binding.SpawnSequence == spawnSequence
                    && binding.ExecutionId == executionId
                    && binding.WarningId == warningId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ClearEnemySkillWarnings()
        {
            TryClearEnemySkillWarningsThroughProduction();

            if (enemySkillWarnings.Length > 0)
            {
                Array.Clear(
                    enemySkillWarnings,
                    0,
                    enemySkillWarnings.Length);
            }

            activeEnemySkillWarningCount = 0;
        }

        private bool TrySetEnemySkillWarningThroughProduction(
            in FpgFormalEnemySkillWarningPresentationEvent presentationEvent)
        {
            IFpgFormalEnemySkillPresentationConsumer consumer =
                ResolveEnemySkillPresentationConsumer();
            if (consumer == null)
            {
                return false;
            }

            try
            {
                return consumer.TrySetEnemySkillWarning(
                    presentationEvent);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void TryClearEnemySkillWarningsThroughProduction()
        {
            IFpgFormalEnemySkillPresentationConsumer consumer =
                ResolveEnemySkillPresentationConsumer();
            if (consumer == null)
            {
                return;
            }

            try
            {
                consumer.ClearEnemySkillWarnings();
            }
            catch (Exception)
            {
                IncrementEnemySkillTimelineFaultCount();
            }
        }

        private IFpgFormalEnemySkillPresentationConsumer
            ResolveEnemySkillPresentationConsumer()
        {
            if (enemySkillPresentationConsumer != null)
            {
                return enemySkillPresentationConsumer;
            }

            return encounterDirector != null
                ? encounterDirector
                : null;
        }

        private void IncrementEnemySkillTimelineFaultCount()
        {
            if (EnemySkillTimelineFaultCount < int.MaxValue)
            {
                EnemySkillTimelineFaultCount++;
            }
        }

        private void ResetRuntimePresentationState()
        {
            observedRuntime = null;
            skillImpactPresentationConsumer.Clear();
            damageCursor = 0L;
            framePositionCount = 0;
            ClearFeedbackBuffer(feedbackBuffer.Length);
            ResetEnemyProjectilePresentation();
            ClearEnemySkillWarnings();
            ReleaseAll();
            TryResetReticle();
        }

        private void DiscardFeedbackBatch(
            IFpgResolvedDamageFeedbackView feed,
            int count)
        {
            FeedbackGapCount++;
            damageCursor = feed == null ? damageCursor : feed.LastSequence;
            framePositionCount = 0;
            ClearFeedbackBuffer(count);
        }

        private void ClearFeedbackBuffer(int count)
        {
            int clearCount = Math.Min(Math.Max(0, count), feedbackBuffer.Length);
            if (clearCount > 0)
            {
                Array.Clear(feedbackBuffer, 0, clearCount);
            }
        }

        private void HandleActionCommitted(FpgFormalPlayerActionEvent action)
        {
            if (action.Type != FpgFormalPlayerActionType.PrimaryReleaseCommitted
                && action.Type != FpgFormalPlayerActionType.SecondaryReleaseCommitted)
            {
                return;
            }

            try
            {
                aimReticle?.PresentShot();
            }
            catch (Exception)
            {
                ReticleFeedbackFaultCount++;
            }

            RegisterSkillImpactPresentation(action);
        }

        private void RegisterSkillImpactPresentation(
            in FpgFormalPlayerActionEvent action)
        {
            if (!action.HasSkillCorrelation || playerTickDriver == null
                || playerTickDriver.PlayerDefinition == null)
            {
                return;
            }

            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!EnsureSkillImpactConsumer(runtime))
            {
                PresentationFaultCount++;
                return;
            }

            D0WeaponDefinition weapon =
                playerTickDriver.PlayerDefinition.Weapon;
            FpgPlayerSkillDefinition skill;
            if (action.ReleaseKind == WeaponReleaseKind.Primary)
            {
                skill = weapon.PrimarySkill;
            }
            else if (action.ReleaseKind == WeaponReleaseKind.Secondary)
            {
                if (!weapon.TryResolveSecondarySkill(
                        playerTickDriver.PlayerSecondaryTriggerMode,
                        out skill,
                        out _))
                {
                    return;
                }
            }
            else
            {
                skill = weapon.ReloadSkill;
            }

            if (!FpgSkillPresentationRegistry.TryResolveActionPresentation(
                    skill,
                    action.GameplayEventId,
                    out FPG.Demo.Skills.FpgCompiledSkillActionPresentation
                        actionPresentation))
            {
                return;
            }

            FpgSkillImpactPresentationGroupKind groupKind;
            FPG.Demo.Skills.FpgCompiledImpactPresentation bundle;
            if (actionPresentation.ActionKind
                == FPG.Demo.Skills.FpgSkillActionKind.Attack)
            {
                groupKind =
                    FpgSkillImpactPresentationGroupKind.ImmediateAttack;
                bundle = actionPresentation.Impact;
            }
            else if (actionPresentation.ActionKind
                == FPG.Demo.Skills.FpgSkillActionKind.LaunchProjectile)
            {
                groupKind = FpgSkillImpactPresentationGroupKind.Projectile;
                bundle = actionPresentation.Collision;
            }
            else
            {
                return;
            }

            if (!bundle.HasAny)
            {
                return;
            }

            FpgSkillImpactCorrelation correlation =
                new FpgSkillImpactCorrelation(
                    runtime.Player.RuntimeId,
                    action.SkillExecutionId,
                    action.GameplayEventId);
            if (!skillImpactPresentationConsumer.TryRegister(
                correlation,
                groupKind,
                bundle))
            {
                PresentationFaultCount++;
            }
        }

        private void PresentEnemyActivePresentation(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            if (skillPresentationWorld == null && skillVfxWorld != null)
            {
                skillPresentationWorld = skillVfxWorld
                    .GetComponent<FpgSkillPresentationWorld>();
            }

            if (skillEvent.Outcome
                    != FPG.Demo.Skills.FpgSkillEventOutcome.Triggered
                || skillPresentationWorld == null
                || !skillPresentationWorld.IsPrepared
                || !skillPresentationWorld.Registry.TryResolve(
                    skillEvent.Event.PresentationHandle,
                    out FpgRegisteredPresentation registered))
            {
                if (skillEvent.Outcome
                    == FPG.Demo.Skills.FpgSkillEventOutcome.Triggered)
                {
                    IncrementEnemySkillTimelineFaultCount();
                }

                return;
            }

            string socketId = registered.Kind
                    == FpgRegisteredPresentationKind.Vfx
                && registered.Anchor == FpgVfxPresentationAnchor.OwnerSocket
                    ? registered.SocketId
                    : string.Empty;
            if (encounterDirector == null
                || !encounterDirector.TryResolveEnemyPresentationSource(
                    skillEvent.OwnerRuntimeId,
                    skillEvent.SpawnSequence,
                    socketId,
                    out Transform source)
                || !skillPresentationWorld.TryPresent(
                    skillEvent.Event.PresentationHandle,
                    source))
            {
                IncrementEnemySkillTimelineFaultCount();
                return;
            }

            if (EnemyActivePresentationCount < int.MaxValue)
            {
                EnemyActivePresentationCount++;
            }
        }

        private void RegisterEnemySkillImpactPresentation(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            if (skillEvent.Outcome
                    != FPG.Demo.Skills.FpgSkillEventOutcome.Triggered
                || !skillEvent.HasGameplayAction
                || skillEvent.Definition == null
                || !FpgSkillPresentationRegistry.TryResolveActionPresentation(
                    skillEvent.Definition,
                    skillEvent.Event.EventId,
                    out FPG.Demo.Skills.FpgCompiledSkillActionPresentation
                        actionPresentation))
            {
                return;
            }

            FpgSkillImpactPresentationGroupKind groupKind;
            FPG.Demo.Skills.FpgCompiledImpactPresentation bundle;
            if (actionPresentation.ActionKind
                == FPG.Demo.Skills.FpgSkillActionKind.Attack)
            {
                groupKind =
                    FpgSkillImpactPresentationGroupKind.ImmediateAttack;
                bundle = actionPresentation.Impact;
            }
            else if (actionPresentation.ActionKind
                == FPG.Demo.Skills.FpgSkillActionKind.LaunchProjectile)
            {
                groupKind = FpgSkillImpactPresentationGroupKind.Projectile;
                bundle = actionPresentation.Collision;
            }
            else
            {
                return;
            }

            if (!bundle.HasAny)
            {
                return;
            }

            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!EnsureSkillImpactConsumer(runtime)
                || !skillImpactPresentationConsumer.TryRegister(
                    new FpgSkillImpactCorrelation(
                        skillEvent.OwnerRuntimeId,
                        skillEvent.RuntimeEvent.ExecutionId,
                        skillEvent.Event.EventId),
                    groupKind,
                    bundle))
            {
                PresentationFaultCount++;
            }
        }

        private void RegisterEnemyProjectileFlightPresentation(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            if (skillEvent.Outcome != FpgSkillEventOutcome.Triggered
                || !skillEvent.HasGameplayAction
                || skillEvent.Action.Kind
                    != FpgEnemySkillActionKind.Projectile
                || skillEvent.Definition == null
                || !FpgSkillPresentationRegistry.TryResolveActionPresentation(
                    skillEvent.Definition,
                    skillEvent.Event.EventId,
                    out FpgCompiledSkillActionPresentation presentation)
                || presentation.ActionKind
                    != FpgSkillActionKind.LaunchProjectile
                || !presentation.FlightVfx.IsValid)
            {
                return;
            }

            int existing = FindEnemyProjectileBinding(
                skillEvent.OwnerRuntimeId,
                skillEvent.RuntimeEvent.ExecutionId,
                skillEvent.Event.EventId);
            if (existing >= 0)
            {
                EnemyProjectileFlightBinding binding =
                    enemyProjectileBindings[existing];
                binding.Handle = presentation.FlightVfx;
                enemyProjectileBindings[existing] = binding;
                return;
            }

            int slot = FindFreeEnemyProjectileBinding();
            if (slot < 0)
            {
                slot = FindOldestInactiveEnemyProjectileBinding();
            }

            if (slot < 0
                || nextEnemyProjectileBindingOrdinal == long.MaxValue)
            {
                PresentationFaultCount++;
                return;
            }

            enemyProjectileBindings[slot] =
                new EnemyProjectileFlightBinding
                {
                    IsUsed = true,
                    OwnerRuntimeId = skillEvent.OwnerRuntimeId,
                    ExecutionId = skillEvent.RuntimeEvent.ExecutionId,
                    GameplayEventId = skillEvent.Event.EventId,
                    Handle = presentation.FlightVfx,
                    Ordinal = nextEnemyProjectileBindingOrdinal++
                };
        }

        private void ConsumeEnemyProjectilePresentation(
            IProjectilePresentationFeed feed)
        {
            if (!TryBindEnemyProjectileFeed(feed))
            {
                PresentationFaultCount++;
                return;
            }

            if (feed == null)
            {
                return;
            }

            int count;
            bool hasGap;
            try
            {
                count = feed.CopyEventsAfter(
                    enemyProjectileCursor,
                    enemyProjectileEvents,
                    out hasGap);
            }
            catch (Exception)
            {
                enemyProjectileCursor = feed.LastSequence;
                ClearEnemyProjectileEventBuffer();
                SynchronizeEnemyProjectileVisuals(feed);
                PresentationFaultCount++;
                return;
            }

            if (hasGap)
            {
                enemyProjectileCursor = feed.LastSequence;
                ClearEnemyProjectileEventBuffer();
                SynchronizeEnemyProjectileVisuals(feed);
                PresentationFaultCount++;
                return;
            }

            for (int index = 0; index < count; index++)
            {
                ProjectilePresentationEvent presentationEvent =
                    enemyProjectileEvents[index];
                enemyProjectileEvents[index] =
                    default(ProjectilePresentationEvent);
                enemyProjectileCursor = Math.Max(
                    enemyProjectileCursor,
                    presentationEvent.Sequence);
                if (presentationEvent.State.Request.Team != Team.Enemy)
                {
                    continue;
                }

                if (presentationEvent.Type
                    == ProjectilePresentationEventType.Spawn)
                {
                    TryAcquireEnemyProjectileVisual(
                        presentationEvent.State,
                        out _);
                }
                else if (presentationEvent.Type
                    == ProjectilePresentationEventType.Terminal)
                {
                    PresentEnemyProjectileTerminal(presentationEvent.State);
                }
            }

            SynchronizeEnemyProjectileVisuals(feed);
        }

        private bool TryBindEnemyProjectileFeed(
            IProjectilePresentationFeed feed)
        {
            if (feed == null)
            {
                if (observedEnemyProjectileFeed != null)
                {
                    ResetEnemyProjectilePresentation();
                }
                return true;
            }

            if (feed.ActiveCapacity <= 0 || feed.EventCapacity <= 0
                || feed.ActiveCapacity > enemyProjectileStates.Length
                || feed.EventCapacity > enemyProjectileEvents.Length)
            {
                return false;
            }

            if (ReferenceEquals(feed, observedEnemyProjectileFeed))
            {
                return true;
            }

            ClearEnemyProjectileVisuals();
            observedEnemyProjectileFeed = feed;
            enemyProjectileCursor = feed.LastSequence;
            ClearEnemyProjectileStateBuffer();
            ClearEnemyProjectileEventBuffer();
            return true;
        }

        private void SynchronizeEnemyProjectileVisuals(
            IProjectilePresentationFeed feed)
        {
            int stateCount;
            try
            {
                stateCount = feed.CopyActiveStates(enemyProjectileStates);
            }
            catch (Exception)
            {
                PresentationFaultCount++;
                return;
            }

            for (int index = 0; index < enemyProjectileVisuals.Length; index++)
            {
                EnemyProjectileVisualSlot slot = enemyProjectileVisuals[index];
                slot.SeenInActiveSnapshot = false;
                enemyProjectileVisuals[index] = slot;
            }

            for (int index = 0; index < stateCount; index++)
            {
                ProjectilePresentationState state = enemyProjectileStates[index];
                enemyProjectileStates[index] =
                    default(ProjectilePresentationState);
                if (state.Request.Team != Team.Enemy
                    || !TryAcquireEnemyProjectileVisual(
                        state,
                        out int slotIndex))
                {
                    continue;
                }

                UpdateEnemyProjectileVisual(slotIndex, state);
                EnemyProjectileVisualSlot visual =
                    enemyProjectileVisuals[slotIndex];
                visual.SeenInActiveSnapshot = true;
                enemyProjectileVisuals[slotIndex] = visual;
            }

            for (int index = 0; index < enemyProjectileVisuals.Length; index++)
            {
                if (enemyProjectileVisuals[index].IsUsed
                    && !enemyProjectileVisuals[index].SeenInActiveSnapshot)
                {
                    ReleaseEnemyProjectileVisual(index);
                }
            }
        }

        private bool TryAcquireEnemyProjectileVisual(
            in ProjectilePresentationState state,
            out int slotIndex)
        {
            slotIndex = FindEnemyProjectileVisual(state);
            if (slotIndex >= 0)
            {
                return true;
            }

            ProjectileSpawnRequest request = state.Request;
            if (request.Team != Team.Enemy
                || !request.HasSkillCorrelation)
            {
                return false;
            }

            int bindingIndex = FindEnemyProjectileBinding(
                request.OwnerId,
                request.SkillExecutionId,
                request.GameplayEventId);
            if (bindingIndex < 0)
            {
                return false;
            }

            slotIndex = FindFreeEnemyProjectileVisual();
            if (slotIndex < 0)
            {
                PresentationFaultCount++;
                return false;
            }

            EnemyProjectileFlightBinding binding =
                enemyProjectileBindings[bindingIndex];
            binding.ActiveProjectileCount++;
            binding.HasSpawned = true;
            enemyProjectileBindings[bindingIndex] = binding;

            GameObject instance = null;
            if (skillPresentationWorld == null
                || !skillPresentationWorld.TryBorrowFlightVfx(
                    binding.Handle,
                    ToWorldPosition(state.LastPoint),
                    ResolveProjectileRotation(state, state.Path.Start),
                    out instance))
            {
                PresentationFaultCount++;
            }

            enemyProjectileVisuals[slotIndex] =
                new EnemyProjectileVisualSlot
                {
                    IsUsed = true,
                    BindingIndex = bindingIndex,
                    ProjectileId = request.ProjectileId,
                    RuntimeId = request.RuntimeId,
                    Handle = binding.Handle,
                    Instance = instance,
                    LastPoint = state.LastPoint
                };
            return true;
        }

        private void UpdateEnemyProjectileVisual(
            int slotIndex,
            in ProjectilePresentationState state)
        {
            EnemyProjectileVisualSlot slot =
                enemyProjectileVisuals[slotIndex];
            if (!slot.IsUsed)
            {
                return;
            }

            if (slot.Instance != null
                && (skillPresentationWorld == null
                    || !skillPresentationWorld.TryUpdateFlightVfx(
                        slot.Handle,
                        slot.Instance,
                        ToWorldPosition(state.LastPoint),
                        ResolveProjectileRotation(state, slot.LastPoint))))
            {
                PresentationFaultCount++;
            }

            slot.LastPoint = state.LastPoint;
            enemyProjectileVisuals[slotIndex] = slot;
        }

        private void PresentEnemyProjectileTerminal(
            in ProjectilePresentationState state)
        {
            int slotIndex = FindEnemyProjectileVisual(state);
            if (slotIndex < 0)
            {
                return;
            }

            UpdateEnemyProjectileVisual(slotIndex, state);
            ReleaseEnemyProjectileVisual(slotIndex);
        }

        private void ReleaseEnemyProjectileVisual(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= enemyProjectileVisuals.Length)
            {
                return;
            }

            EnemyProjectileVisualSlot slot =
                enemyProjectileVisuals[slotIndex];
            if (!slot.IsUsed)
            {
                return;
            }

            if (slot.Instance != null
                && (skillPresentationWorld == null
                    || !skillPresentationWorld.TryReleaseFlightVfx(
                        slot.Instance)))
            {
                PresentationFaultCount++;
            }

            if (slot.BindingIndex >= 0
                && slot.BindingIndex < enemyProjectileBindings.Length)
            {
                EnemyProjectileFlightBinding binding =
                    enemyProjectileBindings[slot.BindingIndex];
                if (binding.IsUsed && binding.ActiveProjectileCount > 0)
                {
                    binding.ActiveProjectileCount--;
                    if (binding.ActiveProjectileCount == 0
                        && binding.HasSpawned)
                    {
                        binding = default(EnemyProjectileFlightBinding);
                    }
                    enemyProjectileBindings[slot.BindingIndex] = binding;
                }
            }

            enemyProjectileVisuals[slotIndex] =
                default(EnemyProjectileVisualSlot);
        }

        private void ClearEnemyProjectileVisuals()
        {
            for (int index = 0; index < enemyProjectileVisuals.Length; index++)
            {
                ReleaseEnemyProjectileVisual(index);
            }
        }

        private void ResetEnemyProjectilePresentation()
        {
            ClearEnemyProjectileVisuals();
            observedEnemyProjectileFeed = null;
            enemyProjectileCursor = 0L;
            nextEnemyProjectileBindingOrdinal = 0L;
            ClearEnemyProjectileStateBuffer();
            ClearEnemyProjectileEventBuffer();
            if (enemyProjectileBindings.Length > 0)
            {
                Array.Clear(
                    enemyProjectileBindings,
                    0,
                    enemyProjectileBindings.Length);
            }
        }

        private void ClearEnemyProjectileStateBuffer()
        {
            if (enemyProjectileStates.Length > 0)
            {
                Array.Clear(
                    enemyProjectileStates,
                    0,
                    enemyProjectileStates.Length);
            }
        }

        private void ClearEnemyProjectileEventBuffer()
        {
            if (enemyProjectileEvents.Length > 0)
            {
                Array.Clear(
                    enemyProjectileEvents,
                    0,
                    enemyProjectileEvents.Length);
            }
        }

        private int FindEnemyProjectileBinding(
            RuntimeId ownerRuntimeId,
            SkillExecutionId executionId,
            int gameplayEventId)
        {
            for (int index = 0; index < enemyProjectileBindings.Length; index++)
            {
                EnemyProjectileFlightBinding binding =
                    enemyProjectileBindings[index];
                if (binding.IsUsed
                    && binding.OwnerRuntimeId == ownerRuntimeId
                    && binding.ExecutionId == executionId
                    && binding.GameplayEventId == gameplayEventId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeEnemyProjectileBinding()
        {
            for (int index = 0; index < enemyProjectileBindings.Length; index++)
            {
                if (!enemyProjectileBindings[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindOldestInactiveEnemyProjectileBinding()
        {
            int result = -1;
            long oldest = long.MaxValue;
            for (int index = 0; index < enemyProjectileBindings.Length; index++)
            {
                EnemyProjectileFlightBinding binding =
                    enemyProjectileBindings[index];
                if (binding.IsUsed
                    && binding.ActiveProjectileCount == 0
                    && binding.Ordinal < oldest)
                {
                    result = index;
                    oldest = binding.Ordinal;
                }
            }

            return result;
        }

        private int FindEnemyProjectileVisual(
            in ProjectilePresentationState state)
        {
            for (int index = 0; index < enemyProjectileVisuals.Length; index++)
            {
                EnemyProjectileVisualSlot slot = enemyProjectileVisuals[index];
                if (slot.IsUsed
                    && slot.ProjectileId == state.Request.ProjectileId
                    && slot.RuntimeId == state.Request.RuntimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeEnemyProjectileVisual()
        {
            for (int index = 0; index < enemyProjectileVisuals.Length; index++)
            {
                if (!enemyProjectileVisuals[index].IsUsed)
                {
                    return index;
                }
            }

            return -1;
        }

        private static Vector3 ToWorldPosition(SpatialVectorKey point)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(
                point.X * scale,
                point.Y * scale,
                point.Z * scale);
        }

        private static Quaternion ResolveProjectileRotation(
            in ProjectilePresentationState state,
            SpatialVectorKey previousPoint)
        {
            Vector3 direction = ToWorldPosition(state.LastPoint)
                - ToWorldPosition(previousPoint);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = ToWorldPosition(state.Path.End)
                    - ToWorldPosition(state.Path.Start);
            }

            return direction.sqrMagnitude <= 0.000001f
                ? Quaternion.identity
                : Quaternion.LookRotation(direction, Vector3.up);
        }

        private bool EnsureSkillImpactConsumer(
            FpgFormalCombatRuntimeBundle runtime)
        {
            if (runtime == null || runtime.IsDisposed
                || runtime.CombatPort == null)
            {
                return false;
            }

            if (skillPresentationWorld == null && skillVfxWorld != null)
            {
                skillPresentationWorld = skillVfxWorld
                    .GetComponent<FpgSkillPresentationWorld>();
            }

            if (skillPresentationWorld == null
                || !skillPresentationWorld.IsPrepared)
            {
                return false;
            }

            IFpgSkillImpactPresentationView feed =
                runtime.CombatPort.SkillImpactPresentation;
            if (ReferenceEquals(
                skillImpactPresentationConsumer.ObservedFeed,
                feed))
            {
                return true;
            }

            return skillImpactPresentationConsumer.TryPrepare(
                feed,
                skillPresentationWorld,
                feed.Capacity,
                out _);
        }

        private bool RebindSkillImpactConsumer(
            FpgFormalCombatRuntimeBundle runtime)
        {
            skillImpactPresentationConsumer.Clear();
            if (runtime == null || runtime.IsDisposed
                || runtime.CombatPort == null)
            {
                return false;
            }

            if (skillPresentationWorld == null && skillVfxWorld != null)
            {
                skillPresentationWorld = skillVfxWorld
                    .GetComponent<FpgSkillPresentationWorld>();
            }

            IFpgSkillImpactPresentationView feed =
                runtime.CombatPort.SkillImpactPresentation;
            return skillPresentationWorld != null
                && skillPresentationWorld.IsPrepared
                && skillImpactPresentationConsumer.TryPrepare(
                    feed,
                    skillPresentationWorld,
                    feed.Capacity,
                    out _);
        }

        private void TryPresentReticleHit()
        {
            try
            {
                aimReticle?.PresentHit();
            }
            catch (Exception)
            {
                ReticleFeedbackFaultCount++;
            }
        }

        private void TryResetReticle()
        {
            try
            {
                aimReticle?.ResetFeedback();
            }
            catch (Exception)
            {
                ReticleFeedbackFaultCount++;
            }
        }

        private void TryPresent(in FpgResolvedDamageFeedback feedback)
        {
            try
            {
                TryPresentCore(feedback);
            }
            catch (Exception)
            {
                PresentationFaultCount++;
            }
        }

        private void TryPresentCore(in FpgResolvedDamageFeedback feedback)
        {
            if (!feedback.SpatialContext.HasValue
                || !TryProject(feedback.SpatialContext.ImpactPointKey, out Vector2 position))
            {
                DroppedProjectionCount++;
                return;
            }

            FpgDamagePopupPresentation layout = presentationProfile.FormalDamagePopup;
            CombatHitPresentationKind kind = feedback.IsProjectile
                ? CombatHitPresentationKind.Intercept
                : feedback.IsWeakpoint
                    ? CombatHitPresentationKind.Weakpoint
                    : CombatHitPresentationKind.Body;
            if (!presentationProfile.TryGetHitDefinition(
                    kind,
                    out CombatHitPresentationDefinition feedbackStyle)
                || !layout.TryGetSpriteStyle(
                    kind,
                    out FpgDamagePopupSpriteStyle spriteStyle))
            {
                PresentationFaultCount++;
                return;
            }

            FpgDamagePopupView view = FindFreeView();
            if (view == null)
            {
                DroppedPoolCount++;
                return;
            }

            position.y += layout.ScreenVerticalOffset;
            int nearbyCount = 0;
            float nearbyDistanceSquared = layout.NearbyDistance * layout.NearbyDistance;
            for (int index = 0; index < framePositionCount; index++)
            {
                if ((framePositions[index] - position).sqrMagnitude
                    <= nearbyDistanceSquared)
                {
                    nearbyCount++;
                }
            }
            position.y += nearbyCount * layout.NearbyVerticalStep;
            if (!view.TryShow(
                    position,
                    feedback.AppliedDamage,
                    spriteStyle,
                    feedbackStyle.Duration))
            {
                PresentationFaultCount++;
                return;
            }

            if (framePositionCount < framePositions.Length)
            {
                framePositions[framePositionCount++] = position;
            }
        }

        private bool TryProject(SpatialVectorKey point, out Vector2 localPoint)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            Vector3 worldPoint = new Vector3(
                point.X * scale,
                point.Y * scale,
                point.Z * scale);
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f)
            {
                localPoint = default(Vector2);
                return false;
            }

            Camera canvasCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                popupRoot,
                screenPoint,
                canvasCamera,
                out localPoint);
        }

        private FpgDamagePopupView FindFreeView()
        {
            for (int index = 0; index < popupPool.Length; index++)
            {
                FpgDamagePopupView view = popupPool[index];
                if (view != null && !view.IsActive)
                {
                    return view;
                }
            }
            return null;
        }

        private void ReleaseAll()
        {
            for (int index = 0; index < popupPool.Length; index++)
            {
                popupPool[index]?.Release();
            }
        }

        private void DestroyPreparedPool()
        {
            for (int index = 0; index < popupPool.Length; index++)
            {
                FpgDamagePopupView view = popupPool[index];
                popupPool[index] = null;
                if (view == null)
                {
                    continue;
                }

                view.Release();
                if (Application.isPlaying)
                {
                    Destroy(view.gameObject);
                }
                else
                {
                    DestroyImmediate(view.gameObject);
                }
            }

            popupPool = Array.Empty<FpgDamagePopupView>();
            feedbackBuffer = Array.Empty<FpgResolvedDamageFeedback>();
            framePositions = Array.Empty<Vector2>();
            enemyProjectileStates =
                Array.Empty<ProjectilePresentationState>();
            enemyProjectileEvents =
                Array.Empty<ProjectilePresentationEvent>();
            enemyProjectileVisuals =
                Array.Empty<EnemyProjectileVisualSlot>();
            enemyProjectileBindings =
                Array.Empty<EnemyProjectileFlightBinding>();
            enemySkillWarnings =
                Array.Empty<EnemySkillWarningBinding>();
            activeEnemySkillWarningCount = 0;
            prepared = false;
        }

        private readonly struct EnemySkillWarningBinding
        {
            public EnemySkillWarningBinding(
                RuntimeId ownerRuntimeId,
                int spawnSequence,
                long executionId,
                int warningId)
            {
                OwnerRuntimeId = ownerRuntimeId;
                SpawnSequence = spawnSequence;
                ExecutionId = executionId;
                WarningId = warningId;
                IsActive = true;
            }

            public RuntimeId OwnerRuntimeId { get; }
            public int SpawnSequence { get; }
            public long ExecutionId { get; }
            public int WarningId { get; }
            public bool IsActive { get; }
        }

        private struct EnemyProjectileFlightBinding
        {
            public bool IsUsed;
            public RuntimeId OwnerRuntimeId;
            public SkillExecutionId ExecutionId;
            public int GameplayEventId;
            public FpgPresentationHandle Handle;
            public int ActiveProjectileCount;
            public bool HasSpawned;
            public long Ordinal;
        }

        private struct EnemyProjectileVisualSlot
        {
            public bool IsUsed;
            public int BindingIndex;
            public ProjectileId ProjectileId;
            public RuntimeId RuntimeId;
            public FpgPresentationHandle Handle;
            public GameObject Instance;
            public SpatialVectorKey LastPoint;
            public bool SeenInActiveSnapshot;
        }

        private void OnDisable()
        {
            UnsubscribeFromActions();
            UnsubscribeFromLifecycle();
            ClearEnemySkillWarnings();
            ResetEnemyProjectilePresentation();
            ReleaseAll();
        }

        private void OnDestroy()
        {
            Clear();
            DestroyPreparedPool();
        }
    }
}
