using System;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgCoverEntityView : MonoBehaviour
    {
        [SerializeField]
        private GameObject intactRoot;

        [SerializeField]
        private GameObject destroyedRoot;

        [SerializeField]
        private Collider[] blockingColliders = Array.Empty<Collider>();

        public bool IsDestroyed { get; private set; }

        public void ApplySnapshot(in FpgCoverSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                throw new ArgumentException(
                    "Cover view requires a valid snapshot.",
                    nameof(snapshot));
            }

            ApplyDestroyed(snapshot.IsDestroyed);
        }

        public void ApplyDestroyed(bool destroyed)
        {
            IsDestroyed = destroyed;
            if (intactRoot != null)
            {
                intactRoot.SetActive(!destroyed);
            }

            if (destroyedRoot != null)
            {
                destroyedRoot.SetActive(destroyed);
            }

            Collider[] colliders = blockingColliders ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = !destroyed;
                }
            }
        }

        public bool TryValidate(out string error)
        {
            if (intactRoot == null || destroyedRoot == null)
            {
                error = "Cover view requires intact and destroyed visual roots.";
                return false;
            }

            Collider[] colliders = blockingColliders ?? Array.Empty<Collider>();
            if (colliders.Length == 0)
            {
                error = "Cover view requires at least one blocking collider.";
                return false;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null)
                {
                    error = $"Cover view blocker entry {index} is missing.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void Awake()
        {
            ApplyDestroyed(false);
        }
    }
}
