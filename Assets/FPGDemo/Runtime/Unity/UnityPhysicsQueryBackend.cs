using System;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public readonly struct UnityPhysicsHit
    {
        public UnityPhysicsHit(Collider collider, Vector3 point, Vector3 normal, float distance)
        {
            Collider = collider;
            Point = point;
            Normal = normal;
            Distance = distance;
        }

        public Collider Collider { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public float Distance { get; }
    }

    public readonly struct NonAllocPhysicsQueryResult
    {
        public NonAllocPhysicsQueryResult(int count, bool mayBeTruncated)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Count = count;
            MayBeTruncated = mayBeTruncated;
        }

        public int Count { get; }
        public bool MayBeTruncated { get; }
    }

    public interface IUnityPhysicsQueryBackend
    {
        int Capacity { get; }

        void SyncTransforms();

        NonAllocPhysicsQueryResult RaycastNonAlloc(
            Vector3 origin,
            Vector3 direction,
            UnityPhysicsHit[] output,
            float maxDistance,
            int layerMask,
            QueryTriggerInteraction triggerInteraction);

        NonAllocPhysicsQueryResult SphereCastNonAlloc(
            Vector3 origin,
            float radius,
            Vector3 direction,
            UnityPhysicsHit[] output,
            float maxDistance,
            int layerMask,
            QueryTriggerInteraction triggerInteraction);

        NonAllocPhysicsQueryResult OverlapSphereNonAlloc(
            Vector3 position,
            float radius,
            Collider[] output,
            int layerMask,
            QueryTriggerInteraction triggerInteraction);
    }

    public sealed class UnityPhysicsQueryBackend : IUnityPhysicsQueryBackend
    {
        private readonly RaycastHit[] raycastHits;
        private readonly Collider[] overlapColliders;

        public UnityPhysicsQueryBackend(int capacity = SpatialContract.AttackQueryCandidateCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            raycastHits = new RaycastHit[capacity];
            overlapColliders = new Collider[capacity];
        }

        public int Capacity => raycastHits.Length;

        public void SyncTransforms()
        {
            Physics.SyncTransforms();
        }

        public NonAllocPhysicsQueryResult RaycastNonAlloc(
            Vector3 origin,
            Vector3 direction,
            UnityPhysicsHit[] output,
            float maxDistance,
            int layerMask,
            QueryTriggerInteraction triggerInteraction)
        {
            ValidateHitQuery(direction, output, maxDistance, triggerInteraction);
            int count = Physics.RaycastNonAlloc(
                origin,
                direction,
                raycastHits,
                maxDistance,
                layerMask,
                triggerInteraction);
            return CopyHits(output, count);
        }

        public NonAllocPhysicsQueryResult SphereCastNonAlloc(
            Vector3 origin,
            float radius,
            Vector3 direction,
            UnityPhysicsHit[] output,
            float maxDistance,
            int layerMask,
            QueryTriggerInteraction triggerInteraction)
        {
            ValidateHitQuery(direction, output, maxDistance, triggerInteraction);
            if (!IsFinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            int count = Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                raycastHits,
                maxDistance,
                layerMask,
                triggerInteraction);
            return CopyHits(output, count);
        }

        public NonAllocPhysicsQueryResult OverlapSphereNonAlloc(
            Vector3 position,
            float radius,
            Collider[] output,
            int layerMask,
            QueryTriggerInteraction triggerInteraction)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (!IsFinite(position) || !IsFinite(radius) || radius <= 0f
                || triggerInteraction == QueryTriggerInteraction.UseGlobal)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            int count = Physics.OverlapSphereNonAlloc(
                position,
                radius,
                overlapColliders,
                layerMask,
                triggerInteraction);
            int copiedCount = Math.Min(count, output.Length);
            for (int index = 0; index < copiedCount; index++)
            {
                output[index] = overlapColliders[index];
            }

            return new NonAllocPhysicsQueryResult(
                copiedCount,
                count >= overlapColliders.Length || count > output.Length);
        }

        private NonAllocPhysicsQueryResult CopyHits(UnityPhysicsHit[] output, int count)
        {
            int copiedCount = Math.Min(count, output.Length);
            for (int index = 0; index < copiedCount; index++)
            {
                RaycastHit hit = raycastHits[index];
                output[index] = new UnityPhysicsHit(
                    hit.collider,
                    hit.point,
                    hit.normal,
                    hit.distance);
            }

            return new NonAllocPhysicsQueryResult(
                copiedCount,
                count >= raycastHits.Length || count > output.Length);
        }

        private static void ValidateHitQuery(
            Vector3 direction,
            UnityPhysicsHit[] output,
            float maxDistance,
            QueryTriggerInteraction triggerInteraction)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (!IsFinite(direction) || direction.sqrMagnitude <= 0f
                || !IsFinite(maxDistance) || maxDistance <= 0f
                || triggerInteraction == QueryTriggerInteraction.UseGlobal)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
