using System;
using FPG.Demo.Core;
using FPG.Demo.Run;
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
        private long damageCursor;
        private int framePositionCount;
        private bool prepared;
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
        public int EnemySkillCueCount { get; private set; }
        public int EnemySkillWarningStartCount { get; private set; }
        public int EnemySkillWarningEndCount { get; private set; }
        public int ActiveEnemySkillWarningCount =>
            activeEnemySkillWarningCount;
        public string LastPrepareError { get; private set; } = string.Empty;
        public event Action<FpgFormalEnemySkillCuePresentationEvent>
            EnemySkillCuePresented;
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
                return;
            }

            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!ReferenceEquals(runtime, observedRuntime))
            {
                observedRuntime = runtime;
                damageCursor = 0L;
                ReleaseAll();
                TryResetReticle();
            }

            if (runtime == null || runtime.IsDisposed)
            {
                return;
            }

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
            EnemySkillCueCount = 0;
            EnemySkillWarningStartCount = 0;
            EnemySkillWarningEndCount = 0;
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
                    == FPG.Demo.Skills.FpgSkillEventKind.PresentationCue)
            {
                PresentEnemySkillCue(skillEvent);
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

        private void PresentEnemySkillCue(
            in FpgFormalEnemySkillTimelineEvent skillEvent)
        {
            if (skillEvent.Outcome
                    != FPG.Demo.Skills.FpgSkillEventOutcome.Triggered)
            {
                return;
            }

            if (!FpgEnemySkillPresentationResolver.TryResolveCue(
                    skillEvent.Definition,
                    skillEvent.RuntimeEvent.SequenceKind,
                    skillEvent.Event,
                    out FpgResolvedEnemySkillCue resolved))
            {
                IncrementEnemySkillTimelineFaultCount();
                return;
            }

            FpgFormalEnemySkillCuePresentationEvent presentationEvent =
                new FpgFormalEnemySkillCuePresentationEvent(
                    skillEvent,
                    resolved);
            if (!TryPresentEnemySkillCueThroughProduction(
                    presentationEvent))
            {
                IncrementEnemySkillTimelineFaultCount();
            }

            if (EnemySkillCueCount < int.MaxValue)
            {
                EnemySkillCueCount++;
            }

            try
            {
                EnemySkillCuePresented?.Invoke(presentationEvent);
            }
            catch (Exception)
            {
                IncrementEnemySkillTimelineFaultCount();
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

        private bool TryPresentEnemySkillCueThroughProduction(
            in FpgFormalEnemySkillCuePresentationEvent presentationEvent)
        {
            IFpgFormalEnemySkillPresentationConsumer consumer =
                ResolveEnemySkillPresentationConsumer();
            if (consumer == null)
            {
                return false;
            }

            try
            {
                return consumer.TryPresentEnemySkillCue(
                    presentationEvent);
            }
            catch (Exception)
            {
                return false;
            }
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
            damageCursor = 0L;
            framePositionCount = 0;
            ClearFeedbackBuffer(feedbackBuffer.Length);
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
        private void OnDisable()
        {
            UnsubscribeFromActions();
            UnsubscribeFromLifecycle();
            ClearEnemySkillWarnings();
            ReleaseAll();
        }

        private void OnDestroy()
        {
            Clear();
            DestroyPreparedPool();
        }
    }
}
