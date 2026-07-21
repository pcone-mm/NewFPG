using FPG.Demo.Player;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Scene-owned formal camera rig. It applies the selected 3C pose only
    /// after the player has been placed and keeps recoil as a visual-only local
    /// offset, leaving the deterministic aim origin untouched.
    /// </summary>
    [DefaultExecutionOrder(920)]
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerCameraFeedback : MonoBehaviour
    {
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Camera targetCamera;

        private D0ThreeCProfile threeCProfile;
        private Vector3 baselineLocalPosition;
        private Vector3 authoredRigPosition;
        private Quaternion authoredRigRotation;
        private Vector3 authoredCameraLocalPosition;
        private Quaternion authoredCameraLocalRotation;
        private float authoredFieldOfView;
        private float authoredNearClipPlane;
        private float authoredFarClipPlane;
        private float currentKick;
        private bool hasBaseline;
        private bool hasAuthoredPose;
        private bool rigApplied;
        private bool paused;

        public Transform CameraRig => cameraRig;
        public Camera TargetCamera => targetCamera;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public float CurrentKick => currentKick;
        public bool IsPrepared => threeCProfile != null && hasBaseline;
        public bool IsRigApplied => rigApplied;

        /// <summary>
        /// World-space offset introduced by recoil for diagnostics. Gameplay
        /// aiming uses the rendered camera ray directly so off-center reticles stay aligned.
        /// </summary>
        public Vector3 CurrentWorldPresentationOffset
        {
            get
            {
                if (targetCamera == null || !hasBaseline)
                {
                    return Vector3.zero;
                }

                Transform parent = targetCamera.transform.parent;
                Vector3 baselineWorldPosition = parent == null
                    ? baselineLocalPosition
                    : parent.TransformPoint(baselineLocalPosition);
                return targetCamera.transform.position - baselineWorldPosition;
            }
        }

        private void Awake()
        {
            CaptureAuthoringPose();
        }

        private void OnDisable()
        {
            RestoreBaseline();
            currentKick = 0f;
            paused = false;
        }

        private void LateUpdate()
        {
            if (!IsPrepared || !rigApplied)
            {
                return;
            }

            if (!paused)
            {
                float recovery = threeCProfile.ShotCameraKickRecoverySeconds;
                if (recovery > 0f)
                {
                    float maximumKick = Mathf.Max(
                        0.001f,
                        Mathf.Max(
                            threeCProfile.PrimaryShotCameraKick,
                            threeCProfile.SecondaryShotCameraKick));
                    currentKick = Mathf.MoveTowards(
                        currentKick,
                        0f,
                        maximumKick * Time.unscaledDeltaTime / recovery);
                }
            }

            ApplyCameraLocalOffset();
        }

        public bool TryPrepare(
            D0ThreeCProfile profile,
            Camera nextTargetCamera,
            Transform nextCameraRig,
            out string error)
        {
            if (profile == null)
            {
                error = "Formal camera feedback requires a D0 3C profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                return false;
            }

            if (nextTargetCamera == null || nextCameraRig == null)
            {
                error = "Formal camera feedback requires an explicit camera and scene-owned rig.";
                return false;
            }

            if (nextTargetCamera.transform.parent != nextCameraRig)
            {
                error = "Formal camera must be a direct child of the scene-owned camera rig.";
                return false;
            }

            threeCProfile = profile;
            targetCamera = nextTargetCamera;
            cameraRig = nextCameraRig;
            rigApplied = false;
            currentKick = 0f;
            paused = false;
            hasAuthoredPose = false;
            CaptureAuthoringPose();
            baselineLocalPosition = profile.CameraLocalPosition;
            hasBaseline = true;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Applies the fixed rig pose relative to the placed player. The rig
        /// remains scene-owned; the player entity is never reparented to it.
        /// </summary>
        public bool TryApplyFixedSceneRig(
            Transform playerRoot,
            out string error)
        {
            if (!IsPrepared)
            {
                error = "Formal camera feedback must be prepared before applying its rig.";
                return false;
            }

            if (playerRoot == null)
            {
                error = "Formal camera feedback requires the placed player root.";
                return false;
            }

            if (cameraRig == playerRoot || cameraRig.IsChildOf(playerRoot))
            {
                error = "Formal camera rig must remain scene-owned and cannot be under the player root.";
                return false;
            }

            cameraRig.SetPositionAndRotation(
                playerRoot.TransformPoint(threeCProfile.CameraPivotLocalPosition),
                playerRoot.rotation
                    * Quaternion.Euler(threeCProfile.CameraPivotLocalEulerAngles));
            baselineLocalPosition = threeCProfile.CameraLocalPosition;
            hasBaseline = true;
            targetCamera.transform.localPosition = baselineLocalPosition;
            targetCamera.transform.localRotation =
                Quaternion.Euler(threeCProfile.CameraLocalEulerAngles);
            targetCamera.fieldOfView = threeCProfile.CameraFieldOfView;
            targetCamera.nearClipPlane = threeCProfile.CameraNearClipPlane;
            targetCamera.farClipPlane = threeCProfile.CameraFarClipPlane;
            currentKick = 0f;
            rigApplied = true;
            ApplyCameraLocalOffset();
            error = string.Empty;
            return true;
        }

        public void PresentCommittedAction(in FpgFormalPlayerActionEvent action)
        {
            if (!IsPrepared || !rigApplied)
            {
                return;
            }

            switch (action.Type)
            {
                case FpgFormalPlayerActionType.PrimaryReleaseCommitted:
                    currentKick = Mathf.Max(
                        currentKick,
                        threeCProfile.PrimaryShotCameraKick);
                    break;
                case FpgFormalPlayerActionType.SecondaryReleaseCommitted:
                    currentKick = Mathf.Max(
                        currentKick,
                        threeCProfile.SecondaryShotCameraKick);
                    break;
            }

            ApplyCameraLocalOffset();
        }

        public void ResetRuntimeFeedback()
        {
            currentKick = 0f;
            paused = false;
            if (rigApplied)
            {
                ApplyCameraLocalOffset();
            }
        }

        public void SetPaused(bool nextPaused)
        {
            paused = nextPaused;
        }

        public bool TryValidate(out string error)
        {
            if (cameraRig == null || targetCamera == null)
            {
                error = "Formal camera feedback requires a scene-owned rig and target camera.";
                return false;
            }

            if (targetCamera.transform.parent != cameraRig)
            {
                error = "Formal camera must be a direct child of the scene-owned camera rig.";
                return false;
            }

            if (threeCProfile != null && !threeCProfile.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Clear()
        {
            RestoreAuthoringPose();
            currentKick = 0f;
            rigApplied = false;
            paused = false;
            threeCProfile = null;
            hasBaseline = false;
        }

        private void CaptureAuthoringPose()
        {
            if (targetCamera == null || cameraRig == null || hasAuthoredPose)
            {
                return;
            }

            authoredRigPosition = cameraRig.position;
            authoredRigRotation = cameraRig.rotation;
            authoredCameraLocalPosition = targetCamera.transform.localPosition;
            authoredCameraLocalRotation = targetCamera.transform.localRotation;
            authoredFieldOfView = targetCamera.fieldOfView;
            authoredNearClipPlane = targetCamera.nearClipPlane;
            authoredFarClipPlane = targetCamera.farClipPlane;
            hasAuthoredPose = true;
        }

        private void RestoreAuthoringPose()
        {
            if (!hasAuthoredPose || targetCamera == null || cameraRig == null)
            {
                return;
            }

            cameraRig.SetPositionAndRotation(authoredRigPosition, authoredRigRotation);
            targetCamera.transform.localPosition = authoredCameraLocalPosition;
            targetCamera.transform.localRotation = authoredCameraLocalRotation;
            targetCamera.fieldOfView = authoredFieldOfView;
            targetCamera.nearClipPlane = authoredNearClipPlane;
            targetCamera.farClipPlane = authoredFarClipPlane;
        }

        private void RestoreBaseline()
        {
            if (targetCamera != null && hasBaseline)
            {
                targetCamera.transform.localPosition = baselineLocalPosition;
            }
        }

        private void ApplyCameraLocalOffset()
        {
            if (targetCamera == null || !hasBaseline)
            {
                return;
            }

            targetCamera.transform.localPosition = baselineLocalPosition
                + Vector3.back * currentKick;
        }
    }
}




