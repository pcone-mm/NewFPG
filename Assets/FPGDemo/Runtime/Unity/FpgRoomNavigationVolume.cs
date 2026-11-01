using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Scene-authored bounds and collision policy for enemy position queries.
    /// Bounds are expressed in this component's local space; blockers are
    /// authored as 3D colliders on the configured layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgRoomNavigationVolume : MonoBehaviour
    {
        [SerializeField]
        private Vector3 boundsCenter = new Vector3(0f, 4f, 8f);

        [SerializeField, Min(0.1f)]
        private Vector3 boundsSize = new Vector3(20f, 16f, 22f);

        [SerializeField]
        private LayerMask obstacleLayers;

        [SerializeField, Min(0f)]
        private float obstacleClearance = 0.2f;

        [SerializeField]
        private bool drawBounds = true;

        public LayerMask ObstacleLayers => obstacleLayers;
        public float ObstacleClearance => Mathf.Max(0f, obstacleClearance);
        public Vector3 BoundsCenter => transform.TransformPoint(boundsCenter);
        public Vector3 BoundsSize => Vector3.Scale(boundsSize, transform.lossyScale);

        public bool Contains(Vector3 worldPosition, float margin = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            Vector3 half = boundsSize * 0.5f;
            Vector3 localMargin = InverseScaleMargin(Mathf.Max(0f, margin));
            return Mathf.Abs(local.x - boundsCenter.x) <= half.x - localMargin.x
                && Mathf.Abs(local.y - boundsCenter.y) <= half.y - localMargin.y
                && Mathf.Abs(local.z - boundsCenter.z) <= half.z - localMargin.z;
        }

        public bool CanOccupy(Vector3 worldPosition, float radius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            return Contains(worldPosition, safeRadius)
                && !Physics.CheckSphere(
                    worldPosition,
                    safeRadius + ObstacleClearance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore);
        }

        public bool CanOccupyGroundPosition(
            Vector3 worldPosition,
            float radius)
        {
            return CanOccupyGround(worldPosition, radius);
        }

        public bool HasClearGroundPathForPositionQuery(
            Vector3 from,
            Vector3 to,
            float radius)
        {
            return HasClearGroundPath(from, to, radius);
        }

        public bool HasReachableGroundPathForPositionQuery(
            Vector3 from,
            Vector3 to,
            float radius)
        {
            if (HasClearGroundPath(from, to, radius))
            {
                return true;
            }

            Vector3 delta = to - from;
            delta.y = 0f;
            return TryGetSafeGroundStep(
                from,
                to,
                Mathf.Max(0.2f, delta.magnitude),
                radius,
                out _);
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            return distance <= 0.001f
                || !Physics.Raycast(
                    from,
                    delta / distance,
                    distance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore);
        }

        public bool HasClearPath(Vector3 from, Vector3 to, float radius)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            return distance <= 0.001f
                || !Physics.SphereCast(
                    from,
                    Mathf.Max(0f, radius),
                    delta / distance,
                    out _,
                    distance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore);
        }

        public bool HasReachablePath(Vector3 from, Vector3 to, float radius)
        {
            if (HasClearPath(from, to, radius))
            {
                return true;
            }

            if (!TryFindDetourWaypoint(from, to, radius, out Vector3 first))
            {
                return false;
            }

            if (HasClearPath(first, to, radius))
            {
                return true;
            }

            return TryFindDetourWaypoint(
                    first,
                    to,
                    radius,
                    out Vector3 second)
                && Vector3.Distance(first, second) > 0.05f
                && Contains(second, radius)
                && CanOccupy(second, radius)
                && HasClearPath(first, second, radius)
                && HasClearPath(second, to, radius);
        }

        public bool TryGetSafeStep(
            Vector3 from,
            Vector3 to,
            float distance,
            float radius,
            out Vector3 nextPosition,
            out Vector3 localWaypoint)
        {
            localWaypoint = to;
            if (HasClearPath(from, to, radius))
            {
                Vector3 directStep = Vector3.MoveTowards(from, to, distance);
                return TryAcceptStep(
                    from,
                    directStep,
                    radius,
                    out nextPosition);
            }

            if (!TryFindDetourWaypoint(from, to, radius, out localWaypoint))
            {
                nextPosition = from;
                return false;
            }

            Vector3 detourStep = Vector3.MoveTowards(
                from,
                localWaypoint,
                distance);
            return TryAcceptStep(
                from,
                detourStep,
                radius,
                out nextPosition);
        }

        /// <summary>
        /// Advances a ground-plane actor toward a target. Detours are sampled
        /// in XZ so a vertical collider normal cannot create an unusable
        /// waypoint for a character that must keep its authored height.
        /// When the target is behind an uncircumventable blocker, the actor
        /// advances to the blocker edge instead of freezing at its spawn.
        /// </summary>
        public bool TryGetSafeGroundStep(
            Vector3 from,
            Vector3 to,
            float distance,
            float radius,
            out Vector3 nextPosition)
        {
            nextPosition = from;
            Vector3 flatDelta = to - from;
            flatDelta.y = 0f;
            float targetDistance = flatDelta.magnitude;
            if (targetDistance <= 0.001f || distance <= 0f)
            {
                return false;
            }

            Vector3 flatTarget = from + flatDelta;
            if (!CanOccupyGround(from, radius))
            {
                return TryGetGroundEscapeStep(
                    from,
                    flatTarget,
                    distance,
                    radius,
                    out nextPosition);
            }

            Vector3 direction = flatDelta / targetDistance;
            if (HasClearGroundPath(from, flatTarget, radius))
            {
                return TryAcceptGroundStep(
                    from,
                    Vector3.MoveTowards(from, flatTarget, distance),
                    radius,
                    out nextPosition);
            }

            Vector3 castOrigin = GetGroundQueryPosition(from, radius);
            float castRadius = Mathf.Max(0f, radius)
                + ObstacleClearance;
            if (Physics.SphereCast(
                    castOrigin,
                    castRadius,
                    direction,
                    out RaycastHit hit,
                    targetDistance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore))
            {
                Vector3 horizontalNormal = hit.normal;
                horizontalNormal.y = 0f;
                if (horizontalNormal.sqrMagnitude <= 0.0001f)
                {
                    horizontalNormal = -direction;
                }
                horizontalNormal.Normalize();

                Vector3 tangent = new Vector3(
                    -horizontalNormal.z,
                    0f,
                    horizontalNormal.x);
                float clearance = Mathf.Max(
                    0.2f,
                    radius + ObstacleClearance);
                float[] offsets = { 0.75f, 1.5f, 3f, 6f, 12f, 20f };
                float bestScore = float.PositiveInfinity;
                bool foundDetour = false;
                Vector3 bestWaypoint = from;
                Vector3 surface = hit.point;
                surface.y = from.y;
                surface += horizontalNormal * clearance;

                for (int index = 0; index < offsets.Length; index++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 candidate = surface
                            + tangent * (offsets[index] * side);
                        candidate.y = from.y;
                        if (!Contains(candidate, radius)
                            || !CanOccupyGround(candidate, radius)
                            || !HasClearGroundPath(from, candidate, radius))
                        {
                            continue;
                        }

                        float score = Vector3.Distance(from, candidate)
                            + Vector3.Distance(candidate, flatTarget) * 0.2f;
                        if (score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestWaypoint = candidate;
                        foundDetour = true;
                    }
                }

                if (foundDetour)
                {
                    Vector3 detourStep = Vector3.MoveTowards(
                        from,
                        bestWaypoint,
                        distance);
                    if (TryAcceptGroundStep(
                            from,
                            detourStep,
                            radius,
                            out nextPosition))
                    {
                        nextPosition.y = from.y;
                        return true;
                    }
                }

                // The target can be behind a cover wall with no legal route.
                // Move up to the first contact point so the enemy still
                // approaches the player's cover and can attack from its edge.
                float safeDistance = Mathf.Max(
                    0f,
                    hit.distance - ObstacleClearance - 0.05f);
                if (safeDistance > 0.001f)
                {
                    Vector3 edgeStep = Vector3.MoveTowards(
                        from,
                        from + direction * safeDistance,
                        distance);
                    edgeStep.y = from.y;
                    if (TryAcceptGroundStep(
                            from,
                            edgeStep,
                            radius,
                            out nextPosition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanOccupyGround(Vector3 worldPosition, float radius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            return Contains(worldPosition, safeRadius)
                && !Physics.CheckSphere(
                    GetGroundQueryPosition(worldPosition, safeRadius),
                    safeRadius + ObstacleClearance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore);
        }

        private bool HasClearGroundPath(
            Vector3 from,
            Vector3 to,
            float radius)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            float castRadius = Mathf.Max(0f, radius)
                + ObstacleClearance;
            return distance <= 0.001f
                || !Physics.SphereCast(
                    GetGroundQueryPosition(from, radius),
                    castRadius,
                    delta / distance,
                    out _,
                    distance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetGroundQueryPosition(
            Vector3 groundPosition,
            float radius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            // Ground-plane anchors sit at the feet, so lift the probe clear of
            // floor contact while keeping walls inside the query volume.
            return groundPosition + Vector3.up
                * (safeRadius + ObstacleClearance + 0.05f);
        }

        private bool TryAcceptGroundStep(
            Vector3 from,
            Vector3 candidate,
            float radius,
            out Vector3 nextPosition)
        {
            if (!CanOccupyGround(candidate, radius)
                || !HasClearGroundPath(from, candidate, radius))
            {
                nextPosition = from;
                return false;
            }

            nextPosition = candidate;
            return true;
        }

        private bool TryGetGroundEscapeStep(
            Vector3 from,
            Vector3 target,
            float distance,
            float radius,
            out Vector3 nextPosition)
        {
            const float sampleSpacing = 0.5f;
            const float maxSearchDistance = 8f;
            const int directionCount = 16;
            float bestScore = float.PositiveInfinity;
            Vector3 bestPosition = from;

            for (float searchDistance = sampleSpacing;
                searchDistance <= maxSearchDistance;
                searchDistance += sampleSpacing)
            {
                for (int directionIndex = 0;
                    directionIndex < directionCount;
                    directionIndex++)
                {
                    float angle = directionIndex
                        * (Mathf.PI * 2f / directionCount);
                    Vector3 candidate = from + new Vector3(
                        Mathf.Cos(angle) * searchDistance,
                        0f,
                        Mathf.Sin(angle) * searchDistance);
                    if (!CanOccupyGround(candidate, radius))
                    {
                        continue;
                    }

                    float score = searchDistance
                        + Vector3.Distance(candidate, target) * 0.2f;
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestPosition = candidate;
                }

                if (!float.IsPositiveInfinity(bestScore))
                {
                    break;
                }
            }

            if (float.IsPositiveInfinity(bestScore))
            {
                nextPosition = from;
                return false;
            }

            Vector3 escapeStep = Vector3.MoveTowards(
                from,
                bestPosition,
                distance);
            if (!Contains(escapeStep, radius))
            {
                nextPosition = from;
                return false;
            }

            nextPosition = escapeStep;
            return true;
        }

        public bool TryFindDetourWaypoint(
            Vector3 from,
            Vector3 to,
            float radius,
            out Vector3 waypoint)
        {
            waypoint = from;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.001f
                || !Physics.SphereCast(
                    from,
                    Mathf.Max(0f, radius),
                    delta / distance,
                    out RaycastHit hit,
                    distance,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore))
            {
                waypoint = to;
                return distance > 0.001f;
            }

            Vector3 normal = hit.normal.sqrMagnitude > 0.001f
                ? hit.normal.normalized
                : -delta.normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.9f
                ? Vector3.up
                : Vector3.right;
            Vector3 tangentA = Vector3.Cross(normal, reference).normalized;
            Vector3 tangentB = Vector3.Cross(normal, tangentA).normalized;
            float clearance = Mathf.Max(0.15f, radius + ObstacleClearance);
            Vector3 surfacePoint = hit.point + normal * clearance;
            float bestScore = float.PositiveInfinity;
            bool found = false;

            for (int index = 0; index < 8; index++)
            {
                float angle = index * (Mathf.PI * 0.25f);
                Vector3 tangent = tangentA * Mathf.Cos(angle)
                    + tangentB * Mathf.Sin(angle);
                Vector3 candidate = surfacePoint + tangent * clearance;
                if (!Contains(candidate, radius)
                    || !CanOccupy(candidate, radius)
                    || !HasClearPath(from, candidate, radius))
                {
                    continue;
                }

                float candidateDistance = Vector3.Distance(from, candidate);
                if (candidateDistance >= distance + clearance)
                {
                    continue;
                }

                float score = candidateDistance
                    + Vector3.Distance(candidate, to) * 0.25f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                waypoint = candidate;
                found = true;
            }

            return found;
        }

        private Vector3 InverseScaleMargin(float margin)
        {
            Vector3 scale = transform.lossyScale;
            return new Vector3(
                margin / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                margin / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                margin / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

        private bool TryAcceptStep(
            Vector3 from,
            Vector3 candidate,
            float radius,
            out Vector3 nextPosition)
        {
            if (!Contains(candidate, radius)
                || !CanOccupy(candidate, radius)
                || !HasClearPath(from, candidate, radius))
            {
                nextPosition = from;
                return false;
            }

            nextPosition = candidate;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawBounds)
            {
                return;
            }

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.25f, 0.85f, 0.8f, 0.7f);
            Gizmos.DrawWireCube(boundsCenter, boundsSize);
            Gizmos.matrix = previous;
        }
    }
}
