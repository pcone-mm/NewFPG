using System;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public sealed class ProjectileViewPool
    {
        private ProjectileView[] views;
        private RuntimeId[] runtimeIds;
        private int[] presentationKeys;
        private bool[] activeSlots;
        private bool prepared;

        public bool IsPrepared => prepared;
        public int Capacity => views == null ? 0 : views.Length;
        public int ViewPoolRejectCount { get; private set; }

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
            ScenarioDefinition definition,
            BattlePresentationCatalog catalog,
            Transform root,
            Camera billboardCamera,
            out string error)
        {
            if (definition == null || catalog == null || root == null || billboardCamera == null)
            {
                error = "ScenarioDefinition, BattlePresentationCatalog, projectile view root and billboard camera are required.";
                return false;
            }

            if (prepared)
            {
                if (Capacity < definition.ProjectileCapacity)
                {
                    error = "Prepared projectile view pool does not satisfy the scenario projectile capacity.";
                    return false;
                }

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

            if (!catalog.TryValidateProjectileCoverage(definition, out error))
            {
                return false;
            }

            int requestedCapacity = 0;
            for (int entryIndex = 0; entryIndex < catalog.ProjectileEntryCount; entryIndex++)
            {
                requestedCapacity = checked(requestedCapacity + catalog.GetProjectileEntry(entryIndex).PrewarmCapacity);
            }

            if (requestedCapacity < definition.ProjectileCapacity)
            {
                error = "Projectile catalog prewarm capacity is lower than the scenario projectile capacity.";
                return false;
            }

            views = new ProjectileView[requestedCapacity];
            runtimeIds = new RuntimeId[requestedCapacity];
            presentationKeys = new int[requestedCapacity];
            activeSlots = new bool[requestedCapacity];
            int slot = 0;
            try
            {
                for (int entryIndex = 0; entryIndex < catalog.ProjectileEntryCount; entryIndex++)
                {
                    ProjectilePresentationCatalogEntry entry = catalog.GetProjectileEntry(entryIndex);
                    for (int copyIndex = 0; copyIndex < entry.PrewarmCapacity; copyIndex++)
                    {
                        ProjectileView view = UnityEngine.Object.Instantiate(entry.ViewPrefab, root);
                        view.name = $"ProjectileView_{entry.PresentationKey}_{copyIndex}";
                        if (!view.TryPrepare(billboardCamera, out string viewError))
                        {
                            UnityEngine.Object.Destroy(view.gameObject);
                            error = $"Unable to prepare projectile view {slot}: {viewError}";
                            Dispose();
                            return false;
                        }

                        views[slot] = view;
                        presentationKeys[slot] = entry.PresentationKey;
                        slot++;
                    }
                }
            }
            catch (Exception exception)
            {
                error = $"Unable to prewarm projectile views: {exception.Message}";
                Dispose();
                return false;
            }

            prepared = true;
            error = string.Empty;
            return true;
        }

        public bool TryAcquire(in ProjectilePresentationState state, Vector3 position, out ProjectileView view)
        {
            view = null;
            if (!prepared)
            {
                ViewPoolRejectCount++;
                return false;
            }

            int existingSlot = FindSlot(state.Request.RuntimeId);
            if (existingSlot >= 0)
            {
                view = views[existingSlot];
                return true;
            }

            int freeSlot = FindFreeSlot(state.Request.PresentationKey);
            if (freeSlot < 0)
            {
                ViewPoolRejectCount++;
                return false;
            }

            activeSlots[freeSlot] = true;
            runtimeIds[freeSlot] = state.Request.RuntimeId;
            view = views[freeSlot];
            view.Activate(state, position);
            return true;
        }

        public bool TryGet(RuntimeId runtimeId, out ProjectileView view)
        {
            int slot = FindSlot(runtimeId);
            if (slot < 0)
            {
                view = null;
                return false;
            }

            view = views[slot];
            return true;
        }

        public bool TryRelease(RuntimeId runtimeId)
        {
            int slot = FindSlot(runtimeId);
            if (slot < 0)
            {
                return false;
            }

            views[slot].Deactivate();
            activeSlots[slot] = false;
            runtimeIds[slot] = RuntimeId.Invalid;
            return true;
        }

        public void ClearBindings()
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
                runtimeIds[index] = RuntimeId.Invalid;
            }
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
            runtimeIds = null;
            presentationKeys = null;
            activeSlots = null;
            prepared = false;
        }

        private int FindSlot(RuntimeId runtimeId)
        {
            if (!runtimeId.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index] && runtimeIds[index] == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeSlot(int presentationKey)
        {
            for (int index = 0; index < Capacity; index++)
            {
                if (!activeSlots[index] && presentationKeys[index] == presentationKey)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
