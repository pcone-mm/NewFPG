using System;
using FPG.Demo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgOverheadHealthBarView : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

        private Transform followAnchor;
        private Camera facingCamera;

        public RuntimeId RuntimeId { get; private set; } = RuntimeId.Invalid;
        public bool IsBound => RuntimeId.IsValid;

        public bool TryBind(
            RuntimeId runtimeId,
            Transform anchor,
            Camera camera,
            int life,
            int maxLife)
        {
            if (!runtimeId.IsValid || anchor == null || maxLife <= 0)
            {
                return false;
            }

            RuntimeId = runtimeId;
            followAnchor = anchor;
            facingCamera = camera;
            SetLife(life, maxLife);
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

            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01((float)Math.Max(0, life) / maxLife);
            }

            return true;
        }

        public void Release()
        {
            RuntimeId = RuntimeId.Invalid;
            followAnchor = null;
            facingCamera = null;
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
