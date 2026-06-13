using UnityEngine;
using Unity.Cinemachine;

namespace NewFPG.Prototype
{
    public sealed class FixedAngleHorizontalCameraFollow : MonoBehaviour
    {
        private const string CinemachineConflictWarning =
            "FixedAngleHorizontalCameraFollow is disabled because CinemachineBrain controls this camera transform.";

        [SerializeField] private Transform target;
        [SerializeField] private float pitchDegrees = 35f;
        [SerializeField] private Vector3 basePosition = new Vector3(0f, 10f, -14f);
        [SerializeField] private float followSmoothTime = 0.12f;

        private Vector3 velocity;
        private bool warnedAboutCinemachine;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void OnEnable()
        {
            DisableIfCinemachineBrainPresent();
        }

        private void Reset()
        {
            ApplyFixedRotation();
        }

        private void LateUpdate()
        {
            ApplyFixedRotation();

            if (target == null)
            {
                return;
            }

            Vector3 desired = basePosition;
            desired.x += target.position.x;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, followSmoothTime);
        }

        private void ApplyFixedRotation()
        {
            transform.rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
        }

        private void DisableIfCinemachineBrainPresent()
        {
            if (!TryGetComponent(out CinemachineBrain _))
            {
                return;
            }

            if (!warnedAboutCinemachine)
            {
                Debug.LogWarning(CinemachineConflictWarning, this);
                warnedAboutCinemachine = true;
            }

            enabled = false;
        }
    }
}
