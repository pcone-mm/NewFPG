using UnityEngine;
using NewFPG.Battle;

namespace NewFPG.Prototype
{
    public static class PrototypeSceneTransit
    {
        public const string ExplorationSceneName = "SampleScene";
        public const string CaveBattleSceneName = "PrototypeCaveBattleScene";

        private static bool hasReturnPoint;
        private static Vector3 returnPoint;
        private static string resultMessage;
        private static ArtifactQueueState pendingArtifactQueue;

        public static void PrepareCaveBattleReturn(Vector3 safePoint, ArtifactQueueState artifactQueue = null)
        {
            returnPoint = safePoint;
            hasReturnPoint = true;
            resultMessage = string.Empty;
            pendingArtifactQueue = artifactQueue;
        }

        public static void SetBattleResultMessage(string message)
        {
            resultMessage = message ?? string.Empty;
        }

        public static bool TryConsumeReturnPoint(out Vector3 safePoint, out string message)
        {
            safePoint = returnPoint;
            message = resultMessage;

            if (!hasReturnPoint)
            {
                return false;
            }

            hasReturnPoint = false;
            resultMessage = string.Empty;
            return true;
        }

        public static bool TryPeekPendingArtifactQueue(out ArtifactQueueState artifactQueue)
        {
            artifactQueue = pendingArtifactQueue;
            return artifactQueue != null;
        }

        public static bool TryConsumePendingArtifactQueue(out ArtifactQueueState artifactQueue)
        {
            artifactQueue = pendingArtifactQueue;
            if (artifactQueue == null)
            {
                return false;
            }

            pendingArtifactQueue = null;
            return true;
        }
    }
}
