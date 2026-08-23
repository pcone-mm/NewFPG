using System;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class FpgFeelEnemyHitRouter : MonoBehaviour
    {
        [SerializeField] private FpgFormalCombatFeedbackBridge combatFeedbackBridge;
        [SerializeField] private FpgEnemyEntityPool enemyEntityPool;

        private GameObject[] cachedInstances = Array.Empty<GameObject>();
        private FpgFeelEnemyHitFeedback[] cachedFeedbacks =
            Array.Empty<FpgFeelEnemyHitFeedback>();
        private bool subscribed;

        public int RoutedHitCount { get; private set; }
        public int SuppressedHitCount { get; private set; }
        public int RoutingFaultCount { get; private set; }
        public bool IsSubscribed => subscribed;

        public bool TryValidate(out string error)
        {
            if (combatFeedbackBridge == null || enemyEntityPool == null)
            {
                error =
                    "Feel enemy-hit router requires the combat feedback bridge and enemy entity pool.";
                return false;
            }

            if (enemyEntityPool.Capacity <= 0)
            {
                error = "Feel enemy-hit router requires a positive enemy pool capacity.";
                return false;
            }

            EnsureCacheCapacity();
            error = string.Empty;
            return true;
        }

        public void StopAndRestoreAll()
        {
            for (int index = 0; index < cachedFeedbacks.Length; index++)
            {
                FpgFeelEnemyHitFeedback feedback = cachedFeedbacks[index];
                if (feedback == null)
                {
                    continue;
                }

                try
                {
                    feedback.StopAndRestore();
                }
                catch (Exception)
                {
                    RoutingFaultCount++;
                }
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopAndRestoreAll();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            StopAndRestoreAll();
        }

        private void Subscribe()
        {
            if (subscribed || combatFeedbackBridge == null)
            {
                return;
            }

            EnsureCacheCapacity();
            combatFeedbackBridge.SupplementalFeedback +=
                HandleSupplementalFeedback;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (subscribed && combatFeedbackBridge != null)
            {
                combatFeedbackBridge.SupplementalFeedback -=
                    HandleSupplementalFeedback;
            }

            subscribed = false;
        }

        private void HandleSupplementalFeedback(
            FpgSupplementalFeedbackEvent feedbackEvent)
        {
            if (feedbackEvent.Kind == FpgSupplementalFeedbackKind.StopAll)
            {
                StopAndRestoreAll();
                return;
            }

            if (!IsEnemyHit(feedbackEvent.Kind))
            {
                return;
            }

            if (!feedbackEvent.HasTarget
                || !TryResolveFeedback(
                    feedbackEvent.TargetId,
                    out FpgFeelEnemyHitFeedback feedback))
            {
                SuppressedHitCount++;
                return;
            }

            try
            {
                if (feedback.TryPlayHit())
                {
                    RoutedHitCount++;
                }
                else
                {
                    SuppressedHitCount++;
                }
            }
            catch (Exception)
            {
                RoutingFaultCount++;
            }
        }

        private bool TryResolveFeedback(
            FPG.Demo.Core.RuntimeId targetId,
            out FpgFeelEnemyHitFeedback feedback)
        {
            feedback = null;
            if (enemyEntityPool == null
                || !enemyEntityPool.TryGet(targetId, out FpgEnemyEntityHandle handle)
                || !handle.IsValid
                || handle.Instance == null
                || !handle.Instance.activeInHierarchy
                || !(handle.Binder is FpgEnemyEntityView entityView)
                || !entityView.GameplayEnabled)
            {
                return false;
            }

            EnsureCacheCapacity();
            if (handle.PoolSlot < 0 || handle.PoolSlot >= cachedFeedbacks.Length)
            {
                return false;
            }

            if (cachedInstances[handle.PoolSlot] != handle.Instance
                || cachedFeedbacks[handle.PoolSlot] == null)
            {
                cachedInstances[handle.PoolSlot] = handle.Instance;
                cachedFeedbacks[handle.PoolSlot] =
                    handle.Instance.GetComponentInChildren<
                        FpgFeelEnemyHitFeedback>(true);
            }

            feedback = cachedFeedbacks[handle.PoolSlot];
            return feedback != null && feedback.isActiveAndEnabled;
        }

        private void EnsureCacheCapacity()
        {
            int capacity = enemyEntityPool == null
                ? 0
                : Math.Max(0, enemyEntityPool.Capacity);
            if (cachedFeedbacks.Length == capacity)
            {
                return;
            }

            StopAndRestoreAll();
            cachedInstances = capacity == 0
                ? Array.Empty<GameObject>()
                : new GameObject[capacity];
            cachedFeedbacks = capacity == 0
                ? Array.Empty<FpgFeelEnemyHitFeedback>()
                : new FpgFeelEnemyHitFeedback[capacity];
        }

        internal static bool IsEnemyHit(FpgSupplementalFeedbackKind kind)
        {
            return kind == FpgSupplementalFeedbackKind.BodyHit
                || kind == FpgSupplementalFeedbackKind.WeakpointHit;
        }
    }
}
