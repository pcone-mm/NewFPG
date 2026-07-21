using System;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public sealed class ImpactViewPool
    {
        private ImpactView[] views;
        private bool[] activeSlots;
        private long[] startTicks;
        private long[] expireTicks;
        private int[] lifetimeTicks;
        private long[] activationSequences;
        private long nextActivationSequence;
        private bool prepared;

        public bool IsPrepared => prepared;
        public int Capacity => views == null ? 0 : views.Length;
        public int ImpactPoolRejectCount { get; private set; }
        public int RecycledImpactCount { get; private set; }

        public int ActiveViewCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < Capacity; index++)
                {
                    if (activeSlots[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool TryPrepare(
            BattlePresentationCatalog catalog,
            Transform root,
            Camera billboardCamera,
            out string error)
        {
            if (catalog == null || root == null || billboardCamera == null)
            {
                error = "BattlePresentationCatalog, impact root and billboard camera are required.";
                return false;
            }

            if (prepared)
            {
                for (int index = 0; index < views.Length; index++)
                {
                    if (!views[index].TrySetBillboardCamera(billboardCamera, out error))
                    {
                        return false;
                    }
                }

                error = string.Empty;
                return true;
            }

            if (!catalog.TryGetImpactEntry(out ImpactPresentationCatalogEntry entry, out error))
            {
                return false;
            }

            views = new ImpactView[entry.PrewarmCapacity];
            activeSlots = new bool[entry.PrewarmCapacity];
            startTicks = new long[entry.PrewarmCapacity];
            expireTicks = new long[entry.PrewarmCapacity];
            lifetimeTicks = new int[entry.PrewarmCapacity];
            activationSequences = new long[entry.PrewarmCapacity];
            try
            {
                for (int index = 0; index < views.Length; index++)
                {
                    ImpactView view = UnityEngine.Object.Instantiate(entry.ViewPrefab, root);
                    view.name = $"ImpactView_{index}";
                    if (!view.TryPrepare(billboardCamera, out string viewError))
                    {
                        UnityEngine.Object.Destroy(view.gameObject);
                        error = $"Unable to prepare impact view {index}: {viewError}";
                        Dispose();
                        return false;
                    }

                    views[index] = view;
                }
            }
            catch (Exception exception)
            {
                error = $"Unable to prewarm impact views: {exception.Message}";
                Dispose();
                return false;
            }

            prepared = true;
            error = string.Empty;
            return true;
        }

        public bool TrySpawn(
            Vector3 position,
            Color color,
            float scale,
            TickIndex currentTick,
            int lifetimeTicks)
        {
            return TrySpawn(
                position,
                color,
                scale,
                currentTick,
                lifetimeTicks,
                ImpactView.DefaultCameraFacingOffset);
        }

        /// <summary>
        /// Activates a short feedback effect with an explicit camera-facing
        /// surface offset. Gameplay remains unaware of this value; it exists
        /// only to keep presentation visible when the third-person avatar is
        /// itself the feedback target.
        /// </summary>
        public bool TrySpawn(
            Vector3 position,
            Color color,
            float scale,
            TickIndex currentTick,
            int lifetimeTicks,
            float cameraFacingOffset)
        {
            return TrySpawn(
                position,
                color,
                scale,
                currentTick,
                lifetimeTicks,
                cameraFacingOffset,
                CombatHitFeedbackShape.Burst);
        }

        public bool TrySpawn(
            Vector3 position,
            Color color,
            float scale,
            TickIndex currentTick,
            int lifetimeTicks,
            float cameraFacingOffset,
            CombatHitFeedbackShape feedbackShape)
        {
            if (!prepared || !currentTick.IsValid || lifetimeTicks <= 0)
            {
                ImpactPoolRejectCount++;
                return false;
            }

            int slot = FindFreeSlot();
            if (slot < 0)
            {
                slot = FindOldestSlot();
                if (slot < 0)
                {
                    ImpactPoolRejectCount++;
                    return false;
                }

                RecycledImpactCount++;
            }

            activeSlots[slot] = true;
            activationSequences[slot] = nextActivationSequence == long.MaxValue
                ? 1L
                : ++nextActivationSequence;
            startTicks[slot] = currentTick.Value;
            expireTicks[slot] = AddSaturating(currentTick.Value, lifetimeTicks);
            this.lifetimeTicks[slot] = lifetimeTicks;
            views[slot].Activate(
                position,
                color,
                scale,
                cameraFacingOffset,
                feedbackShape);
            views[slot].SetLifetimeVisual(0, lifetimeTicks);
            return true;
        }

        public void Advance(TickIndex currentTick)
        {
            if (!prepared || !currentTick.IsValid)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (!activeSlots[index])
                {
                    continue;
                }

                long expireTick = expireTicks[index];
                if (currentTick.Value >= expireTick)
                {
                    views[index].Deactivate();
                    activeSlots[index] = false;
                    startTicks[index] = 0L;
                    expireTicks[index] = 0L;
                    lifetimeTicks[index] = 0;
                    activationSequences[index] = 0L;
                    continue;
                }

                long elapsed = Math.Max(0L, currentTick.Value - startTicks[index]);
                views[index].SetLifetimeVisual(
                    elapsed > int.MaxValue ? int.MaxValue : (int)elapsed,
                    lifetimeTicks[index]);
            }
        }

        public void Clear()
        {
            if (!prepared)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index])
                {
                    views[index].Deactivate();
                }

                activeSlots[index] = false;
                startTicks[index] = 0L;
                expireTicks[index] = 0L;
                lifetimeTicks[index] = 0;
                activationSequences[index] = 0L;
            }

            nextActivationSequence = 0L;
        }

        public void Dispose()
        {
            if (views != null)
            {
                for (int index = 0; index < views.Length; index++)
                {
                    if (views[index] == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(views[index].gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(views[index].gameObject);
                    }
                }
            }

            views = null;
            activeSlots = null;
            startTicks = null;
            expireTicks = null;
            lifetimeTicks = null;
            activationSequences = null;
            nextActivationSequence = 0L;
            prepared = false;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < Capacity; index++)
            {
                if (!activeSlots[index])
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindOldestSlot()
        {
            int oldestSlot = -1;
            long oldestSequence = long.MaxValue;
            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index] && activationSequences[index] < oldestSequence)
                {
                    oldestSlot = index;
                    oldestSequence = activationSequences[index];
                }
            }

            return oldestSlot;
        }

        private static long AddSaturating(long startTick, int lifetimeTicks)
        {
            return startTick > long.MaxValue - lifetimeTicks
                ? long.MaxValue
                : startTick + lifetimeTicks;
        }

    }
}
