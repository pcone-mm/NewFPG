using System;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public sealed class WarningViewPool
    {
        private const float VisualAnchorSurfaceOffset = 0.025f;

        private WarningView[] views;
        private RuntimeId[] runtimeIds;
        private int[] presentationKeys;
        private Color[] tints;
        private WarningAnchorKind[] anchorKinds;
        private bool[] activeSlots;
        private bool prepared;

        public bool IsPrepared => prepared;
        public int Capacity => views == null ? 0 : views.Length;
        public int WarningPoolRejectCount { get; private set; }

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
            out string error)
        {
            return TryPrepare(definition, catalog, root, null, out error);
        }

        public bool TryPrepare(
            ScenarioDefinition definition,
            BattlePresentationCatalog catalog,
            Transform root,
            Camera billboardCamera,
            out string error)
        {
            if (definition == null || catalog == null || root == null)
            {
                error = "ScenarioDefinition, BattlePresentationCatalog and warning root are required.";
                return false;
            }

            if (prepared)
            {
                if (Capacity < definition.ThreatCapacity)
                {
                    error = "Prepared warning view pool does not satisfy the scenario threat capacity.";
                    return false;
                }

                if (catalog.UsesWarningAnchorKind(WarningAnchorKind.EnemyWeakpoint)
                    && billboardCamera == null)
                {
                    error = "EnemyWeakpoint warning presentation requires an explicit billboard camera.";
                    return false;
                }

                for (int index = 0; index < Capacity; index++)
                {
                    WarningView view = views[index];
                    if (view == null)
                    {
                        error = $"Prepared warning view {index} is missing.";
                        return false;
                    }

                    if (!view.TryPrepare(billboardCamera, out string viewError))
                    {
                        error = $"Prepared warning view {index} could not receive the billboard camera: {viewError}";
                        return false;
                    }
                }

                error = string.Empty;
                return true;
            }

            if (!catalog.TryValidateWarningCoverage(definition, out error))
            {
                return false;
            }

            if (catalog.UsesWarningAnchorKind(WarningAnchorKind.EnemyWeakpoint)
                && billboardCamera == null)
            {
                error = "EnemyWeakpoint warning presentation requires an explicit billboard camera.";
                return false;
            }

            int requestedCapacity = 0;
            for (int entryIndex = 0; entryIndex < catalog.WarningEntryCount; entryIndex++)
            {
                requestedCapacity = checked(requestedCapacity + catalog.GetWarningEntry(entryIndex).PrewarmCapacity);
            }

            views = new WarningView[requestedCapacity];
            runtimeIds = new RuntimeId[requestedCapacity];
            presentationKeys = new int[requestedCapacity];
            tints = new Color[requestedCapacity];
            anchorKinds = new WarningAnchorKind[requestedCapacity];
            activeSlots = new bool[requestedCapacity];
            int slot = 0;
            try
            {
                for (int entryIndex = 0; entryIndex < catalog.WarningEntryCount; entryIndex++)
                {
                    WarningPresentationCatalogEntry entry = catalog.GetWarningEntry(entryIndex);
                    for (int copyIndex = 0; copyIndex < entry.PrewarmCapacity; copyIndex++)
                    {
                        WarningView view = UnityEngine.Object.Instantiate(entry.ViewPrefab, root);
                        view.name = $"WarningView_{entry.PresentationKey}_{copyIndex}";
                        if (!view.TryPrepare(billboardCamera, out string viewError))
                        {
                            UnityEngine.Object.Destroy(view.gameObject);
                            error = $"Unable to prepare warning view {slot}: {viewError}";
                            Dispose();
                            return false;
                        }

                        views[slot] = view;
                        presentationKeys[slot] = entry.PresentationKey;
                        tints[slot] = entry.Tint;
                        anchorKinds[slot] = entry.AnchorKind;
                        slot++;
                    }
                }
            }
            catch (Exception exception)
            {
                error = $"Unable to prewarm warning views: {exception.Message}";
                Dispose();
                return false;
            }

            prepared = true;
            error = string.Empty;
            return true;
        }

        public void Reconcile(
            ThreatSnapshot[] snapshots,
            int snapshotCount,
            Vector3 playerGroundPosition,
            Vector3 enemyWeakpointPosition)
        {
            if (!prepared || snapshots == null || snapshotCount < 0 || snapshotCount > snapshots.Length)
            {
                return;
            }

            for (int index = 0; index < snapshotCount; index++)
            {
                ThreatSnapshot snapshot = snapshots[index];
                if (!RequiresWarning(snapshot))
                {
                    continue;
                }

                if (!TryGet(snapshot.RuntimeId, out WarningView view)
                    && !TryAcquire(snapshot, out view))
                {
                    continue;
                }

                int slot = FindSlot(snapshot.RuntimeId);
                if (slot < 0)
                {
                    continue;
                }

                Vector3 position = ResolveAnchorPosition(
                    anchorKinds[slot],
                    playerGroundPosition,
                    enemyWeakpointPosition);
                view.SetState(
                    snapshot,
                    position + Vector3.up * VisualAnchorSurfaceOffset,
                    tints[slot],
                    anchorKinds[slot]);
            }

            for (int slot = 0; slot < Capacity; slot++)
            {
                if (!activeSlots[slot] || IsStillRequired(runtimeIds[slot], snapshots, snapshotCount))
                {
                    continue;
                }

                ReleaseSlot(slot);
            }
        }

        public bool TryGet(RuntimeId runtimeId, out WarningView view)
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
                    ReleaseSlot(index);
                }
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
            tints = null;
            anchorKinds = null;
            activeSlots = null;
            prepared = false;
        }

        private bool TryAcquire(in ThreatSnapshot snapshot, out WarningView view)
        {
            view = null;
            int slot = FindFreeSlot(snapshot.PresentationKey);
            if (slot < 0)
            {
                WarningPoolRejectCount++;
                return false;
            }

            activeSlots[slot] = true;
            runtimeIds[slot] = snapshot.RuntimeId;
            view = views[slot];
            view.Activate(snapshot, Vector3.zero, tints[slot], anchorKinds[slot]);
            return true;
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

        private static bool RequiresWarning(in ThreatSnapshot snapshot)
        {
            return !snapshot.IsTerminal
                && (snapshot.State == ThreatState.Telegraph || snapshot.State == ThreatState.Windup);
        }

        private static Vector3 ResolveAnchorPosition(
            WarningAnchorKind anchorKind,
            Vector3 playerGroundPosition,
            Vector3 enemyWeakpointPosition)
        {
            return anchorKind == WarningAnchorKind.EnemyWeakpoint
                ? enemyWeakpointPosition
                : playerGroundPosition;
        }

        private static bool IsStillRequired(
            RuntimeId runtimeId,
            ThreatSnapshot[] snapshots,
            int snapshotCount)
        {
            for (int index = 0; index < snapshotCount; index++)
            {
                if (snapshots[index].RuntimeId == runtimeId && RequiresWarning(snapshots[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private void ReleaseSlot(int slot)
        {
            views[slot].Deactivate();
            activeSlots[slot] = false;
            runtimeIds[slot] = RuntimeId.Invalid;
        }
    }
}
