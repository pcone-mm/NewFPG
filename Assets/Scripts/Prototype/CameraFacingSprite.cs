using UnityEngine;

namespace NewFPG.Prototype
{
    public sealed class CameraFacingSprite : MonoBehaviour
    {
        private const float MinCameraDistanceSqr = 0.0001f;

        [SerializeField] private Camera targetCamera;

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return;
            }

            Vector3 toCamera = targetCamera.transform.position - transform.position;
            if (toCamera.sqrMagnitude <= MinCameraDistanceSqr)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, targetCamera.transform.up);
        }
    }
}
