using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgRoomInstance : MonoBehaviour
    {
        private readonly List<GameObject> ownedInstances = new List<GameObject>();
        private readonly List<GameObject> destructibleInstances = new List<GameObject>();
        private readonly Dictionary<string, GameObject> destructiblesBySlotId =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<GameObject> coverInstances = new List<GameObject>();
        private readonly Dictionary<string, FpgCoverEntityView> coversBySlotId =
            new Dictionary<string, FpgCoverEntityView>(StringComparer.Ordinal);

        private FpgRoomDefinition roomDefinition;
        public FpgRoomDefinition RoomDefinition => roomDefinition;
        public bool IsInitialized => roomDefinition != null;
        public bool Initialized => IsInitialized;
        public IReadOnlyList<GameObject> DestructibleInstances => destructibleInstances;
        public IReadOnlyList<GameObject> CoverInstances => coverInstances;

        public void Initialize(FpgRoomDefinition definition)
        {
            if (!TryInitialize(definition, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public bool TryInitialize(FpgRoomDefinition definition, out string error)
        {
            if (definition == null)
            {
                error = "Room instance requires a room definition.";
                return false;
            }

            FpgRoomValidationResult validation = definition.Validate();
            if (!validation.IsValid)
            {
                error = validation.FirstError != null
                    ? validation.FirstError.Message
                    : $"Room '{definition.RoomId}' is invalid.";
                return false;
            }

            Clear();
            try
            {
                roomDefinition = definition;
                IReadOnlyList<FpgRoomDestructibleSlot> slots = definition.DestructibleSlots;
                for (int index = 0; index < slots.Count; index++)
                {
                    FpgRoomDestructibleSlot slot = slots[index];
                    GameObject instance = InstantiateOwned(
                        slot.Prefab,
                        $"{slot.Prefab.name} [{slot.MarkerId}]",
                        slot.LocalPosition,
                        slot.LocalRotation,
                        preservePrefabScale: true);
                    destructibleInstances.Add(instance);
                    destructiblesBySlotId.Add(slot.MarkerId, instance);
                }

                IReadOnlyList<FpgRoomCoverSlot> covers = definition.CoverSlots;
                for (int index = 0; index < covers.Count; index++)
                {
                    FpgRoomCoverSlot slot = covers[index];
                    GameObject instance = InstantiateOwned(
                        slot.Prefab,
                        $"{slot.Prefab.name} [{slot.MarkerId}]",
                        slot.LocalPosition,
                        slot.LocalRotation,
                        preservePrefabScale: true);
                    FpgCoverEntityView view =
                        instance.GetComponent<FpgCoverEntityView>();
                    view.ApplyDestroyed(false);
                    coverInstances.Add(instance);
                    coversBySlotId.Add(slot.MarkerId, view);
                }
            }
            catch (Exception exception)
            {
                Clear();
                error = $"Failed to initialize room '{definition.RoomId}': {exception.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Clear()
        {
            roomDefinition = null;
            destructibleInstances.Clear();
            destructiblesBySlotId.Clear();
            coverInstances.Clear();
            coversBySlotId.Clear();

            for (int index = ownedInstances.Count - 1; index >= 0; index--)
            {
                DestroyOwnedObject(ownedInstances[index]);
            }

            ownedInstances.Clear();
        }

        public bool TryGetPlayerEntryPoint(
            string markerId,
            out FpgRoomPlayerEntryPoint point)
        {
            if (roomDefinition != null)
            {
                return roomDefinition.TryGetPlayerEntryPoint(markerId, out point);
            }

            point = null;
            return false;
        }

        public bool TryGetEnemySpawnPoint(
            string markerId,
            out FpgRoomEnemySpawnPoint point)
        {
            if (roomDefinition != null)
            {
                return roomDefinition.TryGetEnemySpawnPoint(markerId, out point);
            }

            point = null;
            return false;
        }

        public bool TryResolvePlayerEntryPose(string markerId, out Pose worldPose)
        {
            if (TryGetPlayerEntryPoint(markerId, out FpgRoomPlayerEntryPoint point))
            {
                worldPose = ResolveWorldPose(point.LocalPose);
                return true;
            }

            worldPose = default;
            return false;
        }

        public bool TryResolveEnemySpawnPose(string markerId, out Pose worldPose)
        {
            if (TryGetEnemySpawnPoint(markerId, out FpgRoomEnemySpawnPoint point))
            {
                worldPose = ResolveWorldPose(point.LocalPose);
                return true;
            }

            worldPose = default;
            return false;
        }

        public bool TryResolveExitPose(string markerId, out Pose worldPose)
        {
            if (roomDefinition != null
                && roomDefinition.TryGetExitSlot(markerId, out FpgRoomExitSlot slot))
            {
                worldPose = ResolveWorldPose(slot.LocalPose);
                return true;
            }

            worldPose = default;
            return false;
        }

        public bool TryResolveDestructiblePose(string markerId, out Pose worldPose)
        {
            if (roomDefinition != null
                && roomDefinition.TryGetDestructibleSlot(
                    markerId,
                    out FpgRoomDestructibleSlot slot))
            {
                worldPose = ResolveWorldPose(slot.LocalPose);
                return true;
            }

            worldPose = default;
            return false;
        }

        public bool TryResolveCoverReachablePose(
            string markerId,
            out Pose worldPose)
        {
            if (roomDefinition != null
                && roomDefinition.TryGetCoverSlot(
                    markerId,
                    out FpgRoomCoverSlot slot))
            {
                worldPose = ResolveWorldPose(slot.PlayerReachableLocalPose);
                return true;
            }

            worldPose = default;
            return false;
        }

        public bool TryGetMarkerPose(string markerId, out Pose worldPose)
        {
            if (roomDefinition != null
                && roomDefinition.TryGetMarker(markerId, out FpgRoomMarker marker))
            {
                worldPose = ResolveWorldPose(marker.LocalPose);
                return true;
            }

            worldPose = default;
            return false;
        }

        public bool TryGetMarkerPose(
            string markerId,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            if (TryGetMarkerPose(markerId, out Pose worldPose))
            {
                worldPosition = worldPose.position;
                worldRotation = worldPose.rotation;
                return true;
            }

            worldPosition = default;
            worldRotation = default;
            return false;
        }

        public bool TryGetDestructibleInstance(
            string markerId,
            out GameObject instance)
        {
            if (!string.IsNullOrEmpty(markerId)
                && destructiblesBySlotId.TryGetValue(markerId, out instance)
                && instance != null)
            {
                return true;
            }

            instance = null;
            return false;
        }

        public bool TryGetCoverView(
            string markerId,
            out FpgCoverEntityView view)
        {
            if (!string.IsNullOrEmpty(markerId)
                && coversBySlotId.TryGetValue(markerId, out view)
                && view != null)
            {
                return true;
            }

            view = null;
            return false;
        }

        public void RefreshCoverViews(FPG.Demo.Run.FpgCoverRuntime covers)
        {
            if (covers == null)
            {
                return;
            }

            for (int index = 0; index < covers.Count; index++)
            {
                FPG.Demo.Run.FpgCoverSnapshot snapshot =
                    covers.GetSnapshot(index);
                if (TryGetCoverView(snapshot.CoverId, out FpgCoverEntityView view))
                {
                    view.ApplySnapshot(snapshot);
                }
            }
        }

        private GameObject InstantiateOwned(
            GameObject prefab,
            string instanceName,
            Vector3 localPosition,
            Quaternion localRotation,
            bool preservePrefabScale)
        {
            GameObject instance = Instantiate(prefab, transform, false);
            if (!Application.isPlaying)
            {
                instance.hideFlags |= HideFlags.DontSaveInEditor;
            }
            Vector3 prefabScale = instance.transform.localScale;
            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = preservePrefabScale
                ? prefabScale
                : Vector3.one;
            ownedInstances.Add(instance);
            return instance;
        }

        private Pose ResolveWorldPose(Pose localPose)
        {
            return new Pose(
                transform.TransformPoint(localPose.position),
                transform.rotation * localPose.rotation);
        }

        private static void DestroyOwnedObject(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
