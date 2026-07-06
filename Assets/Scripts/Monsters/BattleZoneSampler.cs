using NewFPG.Combat;
using UnityEngine;

namespace NewFPG.Monsters
{
    public static class BattleZoneSampler
    {
        public const string RandomReachableSampleMode = "random_reachable";

        private const float DefaultOccupancyRadius = 0.45f;
        private const float DefaultAgentHeight = 1.2f;

        public static bool TryFindZonePosition(
            Transform monster,
            Transform skillTarget,
            MonsterMovementDefinition movement,
            BattleArenaZoneMap zoneMap,
            string zoneId,
            string sampleMode,
            int sampleAttemptsOverride,
            out Vector3 position)
        {
            position = default;
            if (monster == null || zoneMap == null || movement == null)
            {
                return false;
            }

            if (NormalizeSampleMode(sampleMode) != RandomReachableSampleMode)
            {
                return false;
            }

            string normalizedZoneId = BattleArenaZoneMap.NormalizeZoneId(zoneId);
            int attempts = Mathf.Max(1, sampleAttemptsOverride > 0 ? sampleAttemptsOverride : zoneMap.SampleAttempts);
            for (int i = 0; i < attempts; i++)
            {
                if (!zoneMap.TrySampleZonePoint(normalizedZoneId, out Vector3 candidate))
                {
                    return false;
                }

                if (!TryProjectReachable(monster.position, candidate, movement, out Vector3 sampledPosition))
                {
                    continue;
                }

                if (!zoneMap.ContainsWorldPoint(normalizedZoneId, sampledPosition))
                {
                    continue;
                }

                if (IsOccupied(sampledPosition, monster, skillTarget, movement, zoneMap))
                {
                    continue;
                }

                position = sampledPosition;
                return true;
            }

            return false;
        }

        public static string NormalizeSampleMode(string sampleMode)
        {
            return string.IsNullOrWhiteSpace(sampleMode) ? RandomReachableSampleMode : sampleMode.Trim();
        }

        private static bool TryProjectReachable(
            Vector3 start,
            Vector3 candidate,
            MonsterMovementDefinition movement,
            out Vector3 position)
        {
            return MonsterAstarNavigation.TryProjectReachable(start, candidate, movement, out position);
        }

        private static bool IsOccupied(
            Vector3 position,
            Transform monster,
            Transform skillTarget,
            MonsterMovementDefinition movement,
            BattleArenaZoneMap zoneMap)
        {
            int mask = movement != null
                ? movement.visiblePositionOccupancyMask
                : zoneMap != null ? zoneMap.OccupancyMask.value : 0;
            if (mask == 0)
            {
                return false;
            }

            float radius = Mathf.Max(0.05f, movement != null ? movement.visiblePositionOccupancyRadius : DefaultOccupancyRadius);
            float height = Mathf.Max(radius * 2f, movement != null ? movement.navMeshAgentHeight : DefaultAgentHeight);
            Vector3 bottom = position + Vector3.up * Mathf.Max(0.05f, radius);
            Vector3 top = position + Vector3.up * Mathf.Max(radius + 0.05f, height);
            Collider[] hits = Physics.OverlapCapsule(
                bottom,
                top,
                radius,
                mask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                Transform hitTransform = hit.transform;
                if (MonsterVisionUtility.IsIgnored(hitTransform, monster)
                    || MonsterVisionUtility.IsIgnored(hitTransform, skillTarget))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

    }
}
