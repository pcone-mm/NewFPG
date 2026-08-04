using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgRoomInstance : MonoBehaviour, IFpgCoverGeometryResolver
    {
        private const int CoverGeometryDomain = 1 << 29;
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private readonly List<GameObject> ownedInstances = new List<GameObject>();
        private readonly List<GameObject> destructibleInstances = new List<GameObject>();
        private readonly Dictionary<string, GameObject> destructiblesBySlotId =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<GameObject> coverInstances = new List<GameObject>();
        private readonly Dictionary<string, FpgCoverEntityView> coversBySlotId =
            new Dictionary<string, FpgCoverEntityView>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> coverIdByGeometryId =
            new Dictionary<int, string>();
        private readonly List<Collider> registeredCoverBlockers =
            new List<Collider>();

        private HitboxRegistry coverHitboxRegistry;

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
            UnregisterCoverBlockers();
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

        public bool TryRegisterCoverBlockers(
            HitboxRegistry registry,
            UnityAttackQuerySettings settings,
            out string error)
        {
            if (!IsInitialized || registry == null
                || !registry.IsReadyForQueries || !settings.IsValid)
            {
                error = "Room cover blockers require an initialized room, registry and query settings.";
                return false;
            }

            UnregisterCoverBlockers();
            IReadOnlyList<FpgRoomCoverSlot> slots = roomDefinition.CoverSlots;
            int blockerCount = 0;
            HashSet<int> colliderIds = new HashSet<int>();
            HashSet<int> geometryIds = new HashSet<int>();
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                FpgRoomCoverSlot slot = slots[slotIndex];
                if (!TryGetCoverView(
                        slot.MarkerId,
                        out FpgCoverEntityView view))
                {
                    error = $"Cover '{slot.MarkerId}' is missing its runtime view.";
                    return false;
                }

                if (!view.TryValidate(out string viewError))
                {
                    error = $"Cover '{slot.MarkerId}' is invalid: {viewError}";
                    return false;
                }

                for (int colliderIndex = 0;
                    colliderIndex < view.BlockingColliderCount;
                    colliderIndex++)
                {
                    if (!view.TryGetBlockingCollider(
                            colliderIndex,
                            out Collider collider))
                    {
                        error = $"Cover '{slot.MarkerId}' blocker {colliderIndex} is missing.";
                        return false;
                    }

                    int layer = collider.gameObject.layer;
                    if (layer < 0 || layer >= 32
                        || (settings.BlockerLayerMask & (1 << layer)) == 0)
                    {
                        error = $"Cover '{slot.MarkerId}' blocker {colliderIndex} uses the wrong physics layer.";
                        return false;
                    }

                    GeometryId geometryId = DeriveCoverGeometryId(
                        roomDefinition.RoomId,
                        slot.MarkerId,
                        colliderIndex);
                    if (!geometryId.IsValid
                        || !colliderIds.Add(collider.GetInstanceID())
                        || !geometryIds.Add(geometryId.Value)
                        || registry.TryResolve(collider, out _)
                        || registry.TryResolve(geometryId, out _))
                    {
                        error = $"Cover '{slot.MarkerId}' blocker {colliderIndex} duplicates a Collider or GeometryId.";
                        return false;
                    }

                    blockerCount++;
                }
            }

            if (slots.Count == 0 || blockerCount == 0)
            {
                error = "Room requires at least one registered cover blocker.";
                return false;
            }

            coverHitboxRegistry = registry;
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                FpgRoomCoverSlot slot = slots[slotIndex];
                TryGetCoverView(slot.MarkerId, out FpgCoverEntityView view);
                for (int colliderIndex = 0;
                    colliderIndex < view.BlockingColliderCount;
                    colliderIndex++)
                {
                    view.TryGetBlockingCollider(colliderIndex, out Collider collider);
                    GeometryId geometryId = DeriveCoverGeometryId(
                        roomDefinition.RoomId,
                        slot.MarkerId,
                        colliderIndex);
                    DomainResult registered = registry.Register(
                        new HitboxBinding(
                            collider,
                            HitboxTargetReference.Environment,
                            QueryTargetKind.EnvironmentBlocker,
                            HitPart.Body,
                            geometryId));
                    if (!registered.IsSuccess)
                    {
                        UnregisterCoverBlockers();
                        error = $"Cover '{slot.MarkerId}' blocker {colliderIndex} registration failed: {registered.RejectReason}.";
                        return false;
                    }

                    registeredCoverBlockers.Add(collider);
                    coverIdByGeometryId.Add(geometryId.Value, slot.MarkerId);
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryResolveCoverId(
            GeometryId geometryId,
            out string coverId)
        {
            if (geometryId.IsValid
                && coverIdByGeometryId.TryGetValue(
                    geometryId.Value,
                    out coverId))
            {
                return true;
            }

            coverId = string.Empty;
            return false;
        }

        public static GeometryId DeriveCoverGeometryId(
            string roomId,
            string markerId,
            int colliderOrdinal)
        {
            if (string.IsNullOrWhiteSpace(roomId)
                || string.IsNullOrWhiteSpace(markerId)
                || colliderOrdinal < 0)
            {
                return GeometryId.Invalid;
            }

            uint hash = FnvOffsetBasis;
            AppendStableHash(ref hash, roomId);
            AppendStableHash(ref hash, markerId);
            unchecked
            {
                hash = (hash ^ (uint)colliderOrdinal) * FnvPrime;
            }

            return new GeometryId(
                CoverGeometryDomain | (int)(hash & (CoverGeometryDomain - 1)));
        }

        private void UnregisterCoverBlockers()
        {
            if (coverHitboxRegistry != null)
            {
                for (int index = registeredCoverBlockers.Count - 1;
                    index >= 0;
                    index--)
                {
                    Collider collider = registeredCoverBlockers[index];
                    if (collider != null
                        && coverHitboxRegistry.TryResolve(collider, out _))
                    {
                        coverHitboxRegistry.Unregister(collider);
                    }
                }
            }

            coverHitboxRegistry = null;
            registeredCoverBlockers.Clear();
            coverIdByGeometryId.Clear();
        }

        private static void AppendStableHash(ref uint hash, string value)
        {
            unchecked
            {
                for (int index = 0; index < value.Length; index++)
                {
                    char character = value[index];
                    hash = (hash ^ (byte)character) * FnvPrime;
                    hash = (hash ^ (byte)(character >> 8)) * FnvPrime;
                }
            }
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

        public bool TryResolveCoverPeekPosition(
            string markerId,
            FpgPlayerFacingDirection direction,
            out Vector3 worldPosition)
        {
            if (roomDefinition != null
                && Enum.IsDefined(typeof(FpgPlayerFacingDirection), direction)
                && roomDefinition.TryGetCoverSlot(
                    markerId,
                    out FpgRoomCoverSlot slot))
            {
                Vector3 localPosition = direction
                    == FpgPlayerFacingDirection.Left
                        ? slot.PlayerLeftPeekLocalPosition
                        : slot.PlayerRightPeekLocalPosition;
                worldPosition = transform.TransformPoint(localPosition);
                return FpgRoomValidationUtility.IsFinite(worldPosition);
            }

            worldPosition = default(Vector3);
            return false;
        }

        public bool TryResolveCoverCameraProfile(
            string markerId,
            out FpgCoverCameraProfile profile)
        {
            if (roomDefinition != null
                && !string.IsNullOrWhiteSpace(markerId)
                && roomDefinition.TryGetCoverSlot(
                    markerId,
                    out FpgRoomCoverSlot slot)
                && slot.CameraProfile != null)
            {
                profile = slot.CameraProfile;
                return true;
            }

            profile = null;
            return false;
        }

        public bool TryResolveCoverCameraShot(
            string markerId,
            Pose playerWorldPose,
            out FpgResolvedCameraShot shot,
            out string error)
        {
            if (!TryResolveCoverCameraProfile(markerId, out var profile))
            {
                shot = default;
                error = $"Cover '{markerId}' has no camera profile.";
                return false;
            }

            return FpgFormalCameraPoseUtility.TryResolveShot(
                playerWorldPose,
                profile,
                out shot,
                out error);
        }

        public bool TryResolveCoverReachablePoseAndCameraShot(
            string markerId,
            out Pose worldPose,
            out FpgCoverCameraProfile profile,
            out FpgResolvedCameraShot shot,
            out string error)
        {
            if (!TryResolveCoverReachablePose(markerId, out worldPose))
            {
                profile = null;
                shot = default;
                error = $"Cover destination '{markerId}' is unavailable.";
                return false;
            }

            if (!TryResolveCoverCameraProfile(markerId, out profile))
            {
                shot = default;
                error = $"Cover '{markerId}' has no camera profile.";
                return false;
            }

            return FpgFormalCameraPoseUtility.TryResolveShot(
                worldPose,
                profile,
                out shot,
                out error);
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
