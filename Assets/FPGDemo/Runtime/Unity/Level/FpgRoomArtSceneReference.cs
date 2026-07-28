using System;
using System.IO;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public sealed class FpgRoomArtSceneReference
    {
        [SerializeField]
        private string sceneGuid;

        [SerializeField]
        private string scenePath;

        public string SceneGuid => sceneGuid ?? string.Empty;
        public string ScenePath => scenePath ?? string.Empty;
        public string SceneName => string.IsNullOrEmpty(ScenePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(ScenePath);
        public bool IsAssigned => !string.IsNullOrWhiteSpace(SceneGuid)
            && !string.IsNullOrWhiteSpace(ScenePath);

        public bool TryValidate(out string error)
        {
            bool hasGuid = !string.IsNullOrWhiteSpace(SceneGuid);
            bool hasPath = !string.IsNullOrWhiteSpace(ScenePath);
            if (!hasGuid && !hasPath)
            {
                error = "Art Scene reference is missing.";
                return false;
            }

            if (!hasGuid || !hasPath)
            {
                error = "Art Scene reference requires both a stable GUID and runtime path.";
                return false;
            }

            if (!IsUnityAssetGuid(SceneGuid))
            {
                error = $"Art Scene GUID '{SceneGuid}' is not a 32-character Unity asset GUID.";
                return false;
            }

            if (!ScenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || ScenePath.IndexOf('\\') >= 0
                || !ScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Art Scene path '{ScenePath}' must be an Assets-relative .unity path using forward slashes.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsUnityAssetGuid(string value)
        {
            if (value == null || value.Length != 32)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isDigit = character >= '0' && character <= '9';
                bool isLowerHex = character >= 'a' && character <= 'f';
                bool isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
