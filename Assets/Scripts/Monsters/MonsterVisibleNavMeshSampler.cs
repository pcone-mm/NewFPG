using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Monsters
{
    public static class MonsterVisibleNavMeshSampler
    {
        private const float MinDirectionLengthSqr = 0.0001f;

        public static bool TryFindVisiblePosition(
            Transform monster,
            Transform observerRoot,
            MonsterMovementDefinition movement,
            IReadOnlyList<string> distanceBands,
            int sampleAttemptsOverride,
            out Vector3 position)
        {
            position = default;
            if (monster == null || movement == null)
            {
                return false;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            int attempts = Mathf.Max(1, sampleAttemptsOverride > 0 ? sampleAttemptsOverride : movement.visiblePositionSampleAttempts);
            for (int i = 0; i < attempts; i++)
            {
                MonsterCameraDistanceBandDefinition band = movement.ResolveCameraDistanceBand(PickBand(distanceBands, i));
                if (band == null || !band.HasValidRange)
                {
                    continue;
                }

                Vector3 candidate = SampleAroundCamera(camera, band);
                if (!MonsterAstarNavigation.TryProjectReachable(
                        monster.position,
                        candidate,
                        movement,
                        out Vector3 samplePosition))
                {
                    continue;
                }

                if (!band.ContainsHorizontalDistance(camera.transform.position, samplePosition))
                {
                    continue;
                }

                Vector3 visiblePoint = samplePosition + Vector3.up * movement.visibilitySampleHeight;
                if (!MonsterVisionUtility.IsPointVisible(
                    camera,
                    visiblePoint,
                    movement.visiblePositionLineOfSightMask,
                    monster,
                    observerRoot))
                {
                    continue;
                }

                if (IsOccupied(samplePosition, monster, observerRoot, movement))
                {
                    continue;
                }

                position = samplePosition;
                return true;
            }

            return false;
        }

        private static string PickBand(IReadOnlyList<string> distanceBands, int attemptIndex)
        {
            if (distanceBands == null || distanceBands.Count == 0)
            {
                return MonsterCameraDistanceBandDefinition.NearBandId;
            }

            int index = attemptIndex % distanceBands.Count;
            return distanceBands[index];
        }

        private static Vector3 SampleAroundCamera(Camera camera, MonsterCameraDistanceBandDefinition band)
        {
            Vector3 flatForward = camera.transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude <= MinDirectionLengthSqr)
            {
                flatForward = camera.transform.up;
                flatForward.y = 0f;
            }

            if (flatForward.sqrMagnitude <= MinDirectionLengthSqr)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();
            float yaw = Random.Range(-camera.fieldOfView * 0.42f, camera.fieldOfView * 0.42f);
            Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * flatForward;
            float distance = Random.Range(band.minDistance, band.maxDistance);
            Vector3 origin = camera.transform.position;
            return new Vector3(origin.x, 0f, origin.z) + direction * distance;
        }

        private static bool IsOccupied(
            Vector3 position,
            Transform monster,
            Transform observerRoot,
            MonsterMovementDefinition movement)
        {
            float radius = Mathf.Max(0.05f, movement.visiblePositionOccupancyRadius);
            Vector3 bottom = position + Vector3.up * Mathf.Max(0.05f, radius);
            Vector3 top = position + Vector3.up * Mathf.Max(radius + 0.05f, movement.navMeshAgentHeight);
            Collider[] hits = Physics.OverlapCapsule(
                bottom,
                top,
                radius,
                movement.visiblePositionOccupancyMask,
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
                    || MonsterVisionUtility.IsIgnored(hitTransform, observerRoot))
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
