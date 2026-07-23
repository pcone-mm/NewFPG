using System;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed overhead-bar pool. Creation is confined to Preparing; runtime
    /// bind/update/release paths use RuntimeId and never grow the pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgOverheadHealthBarPool : MonoBehaviour
    {
        [SerializeField] private FpgOverheadHealthBarView viewPrefab;
        [SerializeField] private Transform viewRoot;
        [SerializeField, Min(1)] private int capacity = 16;

        private FpgOverheadHealthBarView[] views = Array.Empty<FpgOverheadHealthBarView>();
        private Camera facingCamera;
        private bool prepared;
        private bool combatLocked;

        public int Capacity => capacity;
        public bool IsPrepared => prepared;

        public bool TryPrewarm(int requiredCount, Camera camera, out string error)
        {
            error = string.Empty;
            if (combatLocked || viewPrefab == null || requiredCount <= 0 || requiredCount > capacity)
            {
                error = "Overhead health-bar prewarm request is invalid or exceeds fixed capacity.";
                return false;
            }

            DisposeViews();
            views = new FpgOverheadHealthBarView[capacity];
            facingCamera = camera;
            Transform parent = viewRoot == null ? transform : viewRoot;
            for (int index = 0; index < requiredCount; index++)
            {
                FpgOverheadHealthBarView view = Instantiate(viewPrefab, parent, false);
                view.name = $"FormalOverheadHealthBar[{index}]";
                view.Release();
                views[index] = view;
            }

            prepared = true;
            return true;
        }

        public void BeginCombat()
        {
            if (!prepared)
            {
                throw new InvalidOperationException("Overhead health-bar pool must be prepared before combat.");
            }
            combatLocked = true;
        }

        public bool TryBind(RuntimeId runtimeId, Transform anchor, int life, int maxLife)
        {
            if (!combatLocked || !runtimeId.IsValid || Find(runtimeId) >= 0)
            {
                return false;
            }

            for (int index = 0; index < views.Length; index++)
            {
                if (views[index] != null && !views[index].IsBound)
                {
                    return views[index].TryBind(runtimeId, anchor, facingCamera, life, maxLife);
                }
            }
            return false;
        }

        public bool TryUpdate(RuntimeId runtimeId, int life, int maxLife)
        {
            int index = Find(runtimeId);
            return index >= 0 && views[index].SetLife(life, maxLife);
        }

        public bool TryRelease(RuntimeId runtimeId)
        {
            int index = Find(runtimeId);
            if (index < 0) return false;
            views[index].Release();
            return true;
        }

        public void ClearActive()
        {
            for (int index = 0; index < views.Length; index++)
            {
                if (views[index] != null && views[index].IsBound) views[index].Release();
            }
        }

        public void SetPaused(bool paused)
        {
            for (int index = 0; index < views.Length; index++)
            {
                views[index]?.SetPaused(paused);
            }
        }

        public void EndCombat()
        {
            ClearActive();
            combatLocked = false;
        }

        public void Dispose()
        {
            combatLocked = false;
            prepared = false;
            DisposeViews();
        }

        private int Find(RuntimeId runtimeId)
        {
            for (int index = 0; index < views.Length; index++)
            {
                if (views[index] != null && views[index].RuntimeId == runtimeId) return index;
            }
            return -1;
        }

        private void DisposeViews()
        {
            for (int index = views.Length - 1; index >= 0; index--)
            {
                if (views[index] == null) continue;
                if (Application.isPlaying) Destroy(views[index].gameObject);
                else DestroyImmediate(views[index].gameObject);
            }
            views = Array.Empty<FpgOverheadHealthBarView>();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
