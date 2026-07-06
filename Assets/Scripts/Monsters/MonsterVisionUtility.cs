using UnityEngine;

namespace NewFPG.Monsters
{
    public static class MonsterVisionUtility
    {
        private const float MinRayDistance = 0.05f;
        private const float LineOfSightProbeRadius = 0.12f;

        public static bool IsPointVisible(
            Camera camera,
            Vector3 point,
            LayerMask obstructionMask,
            Transform ignoredRoot = null,
            Transform ignoredObserverRoot = null)
        {
            if (camera == null)
            {
                camera = Camera.main;
            }

            if (camera == null || !IsPointInViewport(camera, point))
            {
                return false;
            }

            return HasLineOfSight(camera.transform.position, point, obstructionMask, ignoredRoot, ignoredObserverRoot);
        }

        public static bool IsTransformVisible(
            Camera camera,
            Transform target,
            float heightOffset,
            LayerMask obstructionMask,
            Transform ignoredObserverRoot = null)
        {
            if (target == null)
            {
                return false;
            }

            float sampleHeight = Mathf.Max(0f, heightOffset);
            Vector3 samplePoint = target.position + Vector3.up * sampleHeight;
            if (!IsPointVisible(camera, samplePoint, obstructionMask, target, ignoredObserverRoot))
            {
                return false;
            }

            if (sampleHeight <= MinRayDistance)
            {
                return true;
            }

            Vector3 bodyPoint = target.position + Vector3.up * (sampleHeight * 0.5f);
            return IsPointVisible(camera, bodyPoint, obstructionMask, target, ignoredObserverRoot);
        }

        public static bool IsPointInViewport(Camera camera, Vector3 point)
        {
            if (camera == null)
            {
                return false;
            }

            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > camera.nearClipPlane
                && viewport.x >= 0f
                && viewport.x <= 1f
                && viewport.y >= 0f
                && viewport.y <= 1f;
        }

        public static bool HasLineOfSight(
            Vector3 origin,
            Vector3 point,
            LayerMask obstructionMask,
            Transform ignoredRoot = null,
            Transform ignoredObserverRoot = null)
        {
            Vector3 delta = point - origin;
            float distance = delta.magnitude;
            if (distance <= MinRayDistance)
            {
                return true;
            }

            Vector3 direction = delta / distance;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            if (HasBlockingHit(hits, ignoredRoot, ignoredObserverRoot))
            {
                return false;
            }

            hits = Physics.SphereCastAll(
                origin,
                LineOfSightProbeRadius,
                direction,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            return !HasBlockingHit(hits, ignoredRoot, ignoredObserverRoot);
        }

        private static bool HasBlockingHit(RaycastHit[] hits, Transform ignoredRoot, Transform ignoredObserverRoot)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null)
                {
                    continue;
                }

                Transform hitTransform = hit.transform;
                if (IsIgnored(hitTransform, ignoredRoot) || IsIgnored(hitTransform, ignoredObserverRoot))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public static bool IsIgnored(Transform candidate, Transform ignoredRoot)
        {
            return candidate != null && ignoredRoot != null && candidate.IsChildOf(ignoredRoot);
        }
    }
}
