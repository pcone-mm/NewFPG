using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class D0SpawnPoint : MonoBehaviour
    {
        [SerializeField]
        private string spawnPointId;

        public string SpawnPointId => spawnPointId;

        public void Configure(string stableSpawnPointId)
        {
            spawnPointId = stableSpawnPointId;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(spawnPointId))
            {
                error = "Spawn point requires a stable id.";
                return false;
            }

            Vector3 scale = transform.localScale;
            if ((scale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                error = $"Spawn point '{spawnPointId}' must use unit scale.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
