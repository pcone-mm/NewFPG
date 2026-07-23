using System;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgOverheadHealthBarView : MonoBehaviour
    {
        [SerializeField] private FpgFormalBarView lifeBar;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

        private Transform followAnchor;
        private Camera facingCamera;

        public RuntimeId RuntimeId { get; private set; } = RuntimeId.Invalid;
        public bool IsBound => RuntimeId.IsValid;
        public FpgFormalBarView LifeBar => lifeBar;

        public bool TryValidate(out string error)
        {
            if (lifeBar == null)
            {
                error = "Formal overhead health bar requires a bar view.";
                return false;
            }

            return lifeBar.TryValidate(out error);
        }

        public bool TryBind(
            RuntimeId runtimeId,
            Transform anchor,
            Camera camera,
            int life,
            int maxLife)
        {
            if (!runtimeId.IsValid || anchor == null || maxLife <= 0
                || !TryValidate(out _))
            {
                return false;
            }

            RuntimeId = runtimeId;
            followAnchor = anchor;
            facingCamera = camera;
            if (!lifeBar.SetValue(Math.Max(0, life), maxLife, immediate: true))
            {
                RuntimeId = RuntimeId.Invalid;
                followAnchor = null;
                facingCamera = null;
                return false;
            }
            gameObject.SetActive(true);
            RefreshTransform();
            return true;
        }

        public bool SetLife(int life, int maxLife)
        {
            if (!IsBound || maxLife <= 0)
            {
                return false;
            }

            return lifeBar != null && lifeBar.SetValue(Math.Max(0, life), maxLife);
        }

        public void SetPaused(bool paused)
        {
            lifeBar?.SetPaused(paused);
        }

        public void Release()
        {
            RuntimeId = RuntimeId.Invalid;
            followAnchor = null;
            facingCamera = null;
            lifeBar?.SetNormalizedValue(0f);
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (IsBound)
            {
                RefreshTransform();
            }
        }

        private void RefreshTransform()
        {
            if (followAnchor == null)
            {
                return;
            }

            transform.position = followAnchor.position + worldOffset;
            if (facingCamera != null)
            {
                transform.rotation = facingCamera.transform.rotation;
            }
        }
    }
}
