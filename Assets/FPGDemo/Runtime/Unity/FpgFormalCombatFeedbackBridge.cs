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

        public int DroppedPoolCount { get; private set; }
        public int DroppedProjectionCount { get; private set; }
        public int FeedbackGapCount { get; private set; }
        public int PresentationFaultCount { get; private set; }
        public int ReticleFeedbackFaultCount { get; private set; }
        public int PrepareFaultCount { get; private set; }
        public string LastPrepareError { get; private set; } = string.Empty;
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
                || feedbackBuffer.Length != feedbackReadCapacity)
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
            }

            lifecycleSubscribed = false;
        }

        private void HandleEncounterLifecycle(
            FpgEncounterLifecycleEvent lifecycle)
        {
            if (lifecycle.Type != FpgEncounterLifecycleEventType.Restarted)
            {
                return;
            }

            ResetRuntimePresentationState();
        }

        private void ResetRuntimePresentationState()
        {
            observedRuntime = null;
            damageCursor = 0L;
            framePositionCount = 0;
            ClearFeedbackBuffer(feedbackBuffer.Length);
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
            prepared = false;
        }

        private void OnDisable()
        {
            UnsubscribeFromActions();
            UnsubscribeFromLifecycle();
            ReleaseAll();
        }

        private void OnDestroy()
        {
            Clear();
            DestroyPreparedPool();
        }
    }
}
