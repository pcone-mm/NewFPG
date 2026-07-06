using Pathfinding;
using UnityEngine;

namespace NewFPG.Monsters
{
    internal static class MonsterAstarNavigation
    {
        private const float DefaultSampleDistance = 1.5f;

        public static bool HasActiveGraph
        {
            get
            {
                AstarPath astar = AstarPath.active;
                return astar != null
                    && astar.data != null
                    && astar.data.graphs != null
                    && astar.data.graphs.Length > 0;
            }
        }

        public static bool TryProjectReachable(
            Vector3 start,
            Vector3 candidate,
            MonsterMovementDefinition movement,
            out Vector3 position)
        {
            position = default;
            if (!TryGetNearestWalkable(start, movement, out NNInfo startInfo)
                || !TryGetNearestWalkable(candidate, movement, out NNInfo endInfo))
            {
                return false;
            }

            if (!PathUtilities.IsPathPossible(startInfo.node, endInfo.node))
            {
                return false;
            }

            if (!IsFinite(endInfo.position))
            {
                return false;
            }

            position = endInfo.position;
            return true;
        }

        public static bool TryProjectWalkable(
            Vector3 candidate,
            MonsterMovementDefinition movement,
            out Vector3 position)
        {
            position = default;
            if (!TryGetNearestWalkable(candidate, movement, out NNInfo info) || !IsFinite(info.position))
            {
                return false;
            }

            position = info.position;
            return true;
        }

        private static bool TryGetNearestWalkable(
            Vector3 point,
            MonsterMovementDefinition movement,
            out NNInfo info)
        {
            info = default;
            AstarPath astar = AstarPath.active;
            if (astar == null)
            {
                return false;
            }

            NearestNodeConstraint constraint = NearestNodeConstraint.Walkable;
            constraint.maxDistance = SampleDistance(movement);
            info = astar.GetNearest(point, constraint);
            return info.node != null;
        }

        private static float SampleDistance(MonsterMovementDefinition movement)
        {
            return Mathf.Max(0.05f, movement != null ? movement.navMeshSampleDistance : DefaultSampleDistance);
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
