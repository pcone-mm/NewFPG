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

            if (intactRoot == destroyedRoot)
            {
                error = "Cover view requires distinct intact and destroyed visual roots.";
                return false;
            }

            if (!IsVisualRootOwnedByView(intactRoot.transform))
            {
                error = "Cover view intact visual root must belong to the cover Prefab.";
                return false;
            }

            if (!IsVisualRootOwnedByView(destroyedRoot.transform))
            {
                error = "Cover view destroyed visual root must belong to the cover Prefab.";
                return false;
            }

            Collider[] colliders = blockingColliders ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null)
                {
                    error = $"Cover view blocker entry {index} is missing.";
                    return false;
                }

                if (!IsOwnedByView(colliders[index].transform))
                {
                    error = $"Cover view blocker entry {index} must belong to the cover Prefab.";
                    return false;
                }

                if (colliders[index].isTrigger)
                {
                    error = $"Cover view blocker entry {index} must not be a Trigger.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool IsOwnedByView(Transform candidate)
        {
            return candidate != null && candidate.IsChildOf(transform);
        }

        private bool IsVisualRootOwnedByView(Transform candidate)
        {
            return candidate != transform && IsOwnedByView(candidate);
        }

        private void Awake()
        {
            ApplyDestroyed(false);
        }
    }
}
