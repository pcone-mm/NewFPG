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
        private Quaternion baselineLocalRotation;
        private CombatCameraShakePresentation shakePresentation;
        private ShakeImpulse[] shakeImpulses = System.Array.Empty<ShakeImpulse>();
        private float shakeClock;
        private Vector3 currentShakePosition;
        private float currentShakeRotation;
        private bool hasBaseline;
        private bool hasAuthoredPose;
        private bool rigApplied;
        private bool paused;

        public Transform CameraRig => cameraRig;
        public Camera TargetCamera => targetCamera;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public float CurrentKick => currentKick;
        public float CurrentShakeStrength { get; private set; }
        public int ShakeRejectCount { get; private set; }
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
            ClearShakes();
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

                AdvanceShakes(Time.unscaledDeltaTime);
            }

            ApplyCameraLocalOffset();
        }

        public bool TryPrepare(
            D0ThreeCProfile profile,
            Camera nextTargetCamera,
            Transform nextCameraRig,
            CombatCameraShakePresentation nextShakePresentation,
            int shakeCapacity,
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

            if (nextShakePresentation == null || shakeCapacity <= 0)
            {
                error = "Formal camera feedback requires camera shake settings and a positive fixed capacity.";
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
            shakePresentation = nextShakePresentation;
            shakeImpulses = new ShakeImpulse[shakeCapacity];
            rigApplied = false;
            currentKick = 0f;
            shakeClock = 0f;
            currentShakePosition = Vector3.zero;
            currentShakeRotation = 0f;
            CurrentShakeStrength = 0f;
            ShakeRejectCount = 0;
            paused = false;
            hasAuthoredPose = false;
            CaptureAuthoringPose();
            baselineLocalPosition = profile.CameraLocalPosition;
            baselineLocalRotation = Quaternion.Euler(
                profile.CameraLocalEulerAngles);
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
            baselineLocalRotation = targetCamera.transform.localRotation;
            targetCamera.fieldOfView = threeCProfile.CameraFieldOfView;
            targetCamera.nearClipPlane = threeCProfile.CameraNearClipPlane;
            targetCamera.farClipPlane = threeCProfile.CameraFarClipPlane;
            currentKick = 0f;
            ClearShakes();
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
            ClearShakes();
            paused = false;
            if (rigApplied)
            {
                ApplyCameraLocalOffset();
            }
        }

        public void SetPaused(bool nextPaused)
        {
            if (nextPaused && !paused)
            {
                ClearPresentationShake();
            }

            paused = nextPaused;
        }

        public bool TryAddShake(float strength, float duration)
        {
            if (!IsPrepared || shakePresentation == null
                || float.IsNaN(strength) || float.IsInfinity(strength)
                || float.IsNaN(duration) || float.IsInfinity(duration)
                || strength <= 0f || duration <= 0f)
            {
                ShakeRejectCount++;
                return false;
            }

            for (int index = 0; index < shakeImpulses.Length; index++)
            {
                if (shakeImpulses[index].Remaining > 0f)
                {
                    continue;
                }

                shakeImpulses[index] = new ShakeImpulse(
                    Mathf.Min(strength, shakePresentation.MaxCombinedStrength),
                    duration);
                return true;
            }

            ShakeRejectCount++;
            return false;
        }

        public void ClearPresentationShake()
        {
            ClearShakes();
            ApplyCameraLocalOffset();
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
            ClearShakes();
            rigApplied = false;
            paused = false;
            threeCProfile = null;
            shakePresentation = null;
            shakeImpulses = System.Array.Empty<ShakeImpulse>();
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
                targetCamera.transform.localRotation = baselineLocalRotation;
            }
        }

        private void ApplyCameraLocalOffset()
        {
            if (targetCamera == null || !hasBaseline)
            {
                return;
            }

            targetCamera.transform.localPosition = baselineLocalPosition
                + Vector3.back * currentKick
                + currentShakePosition;
            targetCamera.transform.localRotation = baselineLocalRotation
                * Quaternion.Euler(0f, 0f, currentShakeRotation);
        }

        private void AdvanceShakes(float deltaTime)
        {
            if (shakePresentation == null || deltaTime <= 0f)
            {
                return;
            }

            shakeClock += deltaTime;
            float combinedStrength = 0f;
            for (int index = 0; index < shakeImpulses.Length; index++)
            {
                ShakeImpulse impulse = shakeImpulses[index];
                if (impulse.Remaining <= 0f)
                {
                    continue;
                }

                combinedStrength += impulse.Strength
                    * Mathf.Clamp01(impulse.Remaining / impulse.Duration);
                impulse.Remaining = Mathf.Max(0f, impulse.Remaining - deltaTime);
                shakeImpulses[index] = impulse;
            }

            CurrentShakeStrength = Mathf.Min(
                combinedStrength,
                shakePresentation.MaxCombinedStrength);
            if (CurrentShakeStrength <= 0f)
            {
                currentShakePosition = Vector3.zero;
                currentShakeRotation = 0f;
                return;
            }

            float phase = shakeClock * shakePresentation.FrequencyHz
                * Mathf.PI * 2f;
            float normalized = CurrentShakeStrength
                / shakePresentation.MaxCombinedStrength;
            Vector3 shakeDirection = new Vector3(
                Mathf.Sin(phase),
                Mathf.Cos(phase * 1.173f),
                0f);
            if (shakeDirection.sqrMagnitude > 1f)
            {
                shakeDirection.Normalize();
            }

            currentShakePosition = shakeDirection
                * (shakePresentation.MaximumPositionOffset * normalized);
            currentShakeRotation = Mathf.Sin(phase * 0.917f)
                * shakePresentation.MaximumRotationDegrees
                * normalized;
        }

        private void ClearShakes()
        {
            if (shakeImpulses.Length > 0)
            {
                System.Array.Clear(shakeImpulses, 0, shakeImpulses.Length);
            }

            shakeClock = 0f;
            currentShakePosition = Vector3.zero;
            currentShakeRotation = 0f;
            CurrentShakeStrength = 0f;
        }

        private struct ShakeImpulse
        {
            public ShakeImpulse(float strength, float duration)
            {
                Strength = strength;
                Duration = duration;
                Remaining = duration;
            }

            public float Strength;
            public float Duration;
            public float Remaining;
        }
    }
}




