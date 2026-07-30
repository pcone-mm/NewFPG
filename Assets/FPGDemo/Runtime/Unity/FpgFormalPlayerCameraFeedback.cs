using FPG.Demo.Player;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Owns the complete state of the scene formal camera. Cover composition
    /// is represented by resolved shots; recoil and shake are presentation-only
    /// offsets applied after the current base shot every rendered frame.
    /// </summary>
    [DefaultExecutionOrder(920)]
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerCameraFeedback : MonoBehaviour
    {
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Camera targetCamera;

        private D0ThreeCProfile threeCProfile;
        private Vector3 authoredRigPosition;
        private Quaternion authoredRigRotation;
        private Vector3 authoredCameraLocalPosition;
        private Quaternion authoredCameraLocalRotation;
        private float authoredFieldOfView;
        private float authoredNearClipPlane;
        private float authoredFarClipPlane;
        private bool hasAuthoredPose;

        private FpgResolvedCameraShot committedShot;
        private FpgResolvedCameraShot currentBaseShot;
        private FpgResolvedCameraShot transitionSourceShot;
        private FpgResolvedCameraShot transitionTargetShot;
        private float transitionDuration;
        private float transitionElapsed;
        private bool hasCommittedShot;
        private bool hasCurrentBaseShot;
        private bool isTransitioning;

        private float currentKick;
        private CombatCameraShakePresentation shakePresentation;
        private ShakeImpulse[] shakeImpulses = System.Array.Empty<ShakeImpulse>();
        private float shakeClock;
        private Vector3 currentShakePosition;
        private float currentShakeRotation;
        private bool rigApplied;
        private bool paused;
        private int lastSynchronizedFrame = -1;

        public Transform CameraRig => cameraRig;
        public Camera TargetCamera => targetCamera;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public float CurrentKick => currentKick;
        public float CurrentShakeStrength { get; private set; }
        public int ShakeRejectCount { get; private set; }
        public bool IsPrepared => threeCProfile != null
            && cameraRig != null
            && targetCamera != null
            && shakePresentation != null
            && shakeImpulses.Length > 0;
        public bool IsRigApplied => rigApplied;
        public bool HasCommittedShot => hasCommittedShot;
        public bool IsTransitioning => isTransitioning;
        public bool IsPaused => paused;
        public FpgResolvedCameraShot CommittedShot => committedShot;
        public FpgResolvedCameraShot CurrentBaseShot => currentBaseShot;
        public FpgResolvedCameraShot TransitionTargetShot =>
            transitionTargetShot;
        public float TransitionProgress => !isTransitioning
            ? 0f
            : transitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(transitionElapsed / transitionDuration);

        /// <summary>
        /// World-space offset introduced by recoil and shake for diagnostics.
        /// Gameplay aiming continues to use the rendered Camera ray.
        /// </summary>
        public Vector3 CurrentWorldPresentationOffset
        {
            get
            {
                if (targetCamera == null || cameraRig == null
                    || !hasCurrentBaseShot)
                {
                    return Vector3.zero;
                }

                Vector3 baselineWorldPosition = cameraRig.TransformPoint(
                    currentBaseShot.CameraLocalPose.position);
                return targetCamera.transform.position
                    - baselineWorldPosition;
            }
        }

        private void Awake()
        {
            CaptureAuthoringPose();
        }

        private void OnDisable()
        {
            currentKick = 0f;
            ClearShakes();
            paused = false;
            ApplyCurrentShotWithFeedback();
        }

        private void LateUpdate()
        {
            SynchronizeForCurrentFrame();
        }

        public void SynchronizeForAimSampling()
        {
            SynchronizeForCurrentFrame();
        }

        private void SynchronizeForCurrentFrame()
        {
            if (!IsPrepared || !rigApplied || !hasCurrentBaseShot)
            {
                return;
            }

            int frame = Time.frameCount;
            if (lastSynchronizedFrame != frame)
            {
                if (!paused)
                {
                    AdvanceShotTransition(Time.deltaTime);
                    AdvanceKick(Time.unscaledDeltaTime);
                    AdvanceShakes(Time.unscaledDeltaTime);
                }

                lastSynchronizedFrame = frame;
            }

            ApplyCurrentShotWithFeedback();
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
                error = "Formal camera feedback requires a D0 3C profile for recoil settings.";
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
            hasAuthoredPose = false;
            CaptureAuthoringPose();

            currentKick = 0f;
            shakeClock = 0f;
            currentShakePosition = Vector3.zero;
            currentShakeRotation = 0f;
            CurrentShakeStrength = 0f;
            ShakeRejectCount = 0;
            rigApplied = false;
            paused = false;
            hasCommittedShot = false;
            hasCurrentBaseShot = false;
            isTransitioning = false;
            transitionDuration = 0f;
            transitionElapsed = 0f;
            committedShot = default;
            currentBaseShot = default;
            transitionSourceShot = default;
            transitionTargetShot = default;
            lastSynchronizedFrame = -1;
            error = string.Empty;
            return true;
        }

        public bool TryApplyImmediateShot(
            in FpgResolvedCameraShot shot,
            out string error)
        {
            if (!IsPrepared)
            {
                error = "Formal camera feedback must be prepared before applying a shot.";
                return false;
            }

            if (!shot.TryValidate(out error)
                || !FpgFormalCameraPoseUtility.TryApplyShot(
                    shot,
                    cameraRig,
                    targetCamera,
                    out error))
            {
                return false;
            }

            committedShot = shot;
            currentBaseShot = shot;
            transitionSourceShot = default;
            transitionTargetShot = default;
            transitionDuration = 0f;
            transitionElapsed = 0f;
            hasCommittedShot = true;
            hasCurrentBaseShot = true;
            isTransitioning = false;
            rigApplied = true;
            ApplyCurrentShotWithFeedback();
            error = string.Empty;
            return true;
        }

        public bool TryBeginShotTransition(
            in FpgResolvedCameraShot source,
            in FpgResolvedCameraShot target,
            float durationSeconds,
            out string error)
        {
            if (!IsPrepared)
            {
                error = "Formal camera feedback must be prepared before starting a shot transition.";
                return false;
            }

            if (isTransitioning)
            {
                error = "Formal camera shot transition is already active.";
                return false;
            }

            if (!source.TryValidate(out error)
                || !target.TryValidate(out error))
            {
                return false;
            }

            if (float.IsNaN(durationSeconds)
                || float.IsInfinity(durationSeconds)
                || durationSeconds <= 0f)
            {
                error = "Formal camera shot transition requires a finite positive duration.";
                return false;
            }

            if (!FpgFormalCameraPoseUtility.TryApplyShot(
                    source,
                    cameraRig,
                    targetCamera,
                    out error))
            {
                return false;
            }

            committedShot = source;
            currentBaseShot = source;
            transitionSourceShot = source;
            transitionTargetShot = target;
            transitionDuration = durationSeconds;
            transitionElapsed = 0f;
            hasCommittedShot = true;
            hasCurrentBaseShot = true;
            isTransitioning = true;
            rigApplied = true;
            ApplyCurrentShotWithFeedback();
            error = string.Empty;
            return true;
        }

        public bool TryBeginShotTransition(
            in FpgResolvedCameraShot target,
            float durationSeconds,
            out string error)
        {
            if (!hasCommittedShot)
            {
                error = "Formal camera shot transition requires a committed source shot.";
                return false;
            }

            return TryBeginShotTransition(
                committedShot,
                target,
                durationSeconds,
                out error);
        }

        public bool TryCommitShotTransition(out string error)
        {
            if (!IsPrepared || !isTransitioning)
            {
                error = "Formal camera has no active shot transition to commit.";
                return false;
            }

            if (!FpgFormalCameraPoseUtility.TryApplyShot(
                    transitionTargetShot,
                    cameraRig,
                    targetCamera,
                    out error))
            {
                return false;
            }

            currentBaseShot = transitionTargetShot;
            committedShot = transitionTargetShot;
            transitionSourceShot = default;
            transitionTargetShot = default;
            transitionDuration = 0f;
            transitionElapsed = 0f;
            hasCommittedShot = true;
            hasCurrentBaseShot = true;
            isTransitioning = false;
            ApplyCurrentShotWithFeedback();
            error = string.Empty;
            return true;
        }

        public void CancelShotTransition()
        {
            isTransitioning = false;
            transitionSourceShot = default;
            transitionTargetShot = default;
            transitionDuration = 0f;
            transitionElapsed = 0f;
            if (hasCommittedShot)
            {
                currentBaseShot = committedShot;
                hasCurrentBaseShot = true;
                rigApplied = true;
            }

            ApplyCurrentShotWithFeedback();
        }

        public void AdvanceShotTransition(float deltaTime)
        {
            if (!isTransitioning || paused
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime)
                || deltaTime <= 0f)
            {
                return;
            }

            transitionElapsed = Mathf.Min(
                transitionDuration,
                transitionElapsed + deltaTime);
            float progress = transitionDuration <= 0f
                ? 1f
                : transitionElapsed / transitionDuration;
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            currentBaseShot = FpgFormalCameraPoseUtility.Interpolate(
                transitionSourceShot,
                transitionTargetShot,
                eased);
            hasCurrentBaseShot = true;
        }

        public void PresentCommittedAction(
            in FpgFormalPlayerActionEvent action)
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

            ApplyCurrentShotWithFeedback();
        }

        public void ResetRuntimeFeedback()
        {
            currentKick = 0f;
            ClearShakes();
            paused = false;
            CancelShotTransition();
        }

        public void SetPaused(bool nextPaused)
        {
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
                    Mathf.Min(
                        strength,
                        shakePresentation.MaxCombinedStrength),
                    duration);
                return true;
            }

            ShakeRejectCount++;
            return false;
        }

        public void ClearPresentationShake()
        {
            ClearShakes();
            ApplyCurrentShotWithFeedback();
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

            if (threeCProfile != null
                && !threeCProfile.TryValidate(out error))
            {
                return false;
            }

            if (hasCurrentBaseShot
                && !currentBaseShot.TryValidate(out error))
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
            hasCommittedShot = false;
            hasCurrentBaseShot = false;
            isTransitioning = false;
            transitionDuration = 0f;
            transitionElapsed = 0f;
            committedShot = default;
            currentBaseShot = default;
            transitionSourceShot = default;
            transitionTargetShot = default;
            lastSynchronizedFrame = -1;
        }

        private void CaptureAuthoringPose()
        {
            if (targetCamera == null || cameraRig == null
                || hasAuthoredPose)
            {
                return;
            }

            authoredRigPosition = cameraRig.position;
            authoredRigRotation = cameraRig.rotation;
            authoredCameraLocalPosition =
                targetCamera.transform.localPosition;
            authoredCameraLocalRotation =
                targetCamera.transform.localRotation;
            authoredFieldOfView = targetCamera.fieldOfView;
            authoredNearClipPlane = targetCamera.nearClipPlane;
            authoredFarClipPlane = targetCamera.farClipPlane;
            hasAuthoredPose = true;
        }

        private void RestoreAuthoringPose()
        {
            if (!hasAuthoredPose || targetCamera == null
                || cameraRig == null)
            {
                return;
            }

            cameraRig.SetPositionAndRotation(
                authoredRigPosition,
                authoredRigRotation);
            targetCamera.transform.localPosition =
                authoredCameraLocalPosition;
            targetCamera.transform.localRotation =
                authoredCameraLocalRotation;
            targetCamera.fieldOfView = authoredFieldOfView;
            targetCamera.nearClipPlane = authoredNearClipPlane;
            targetCamera.farClipPlane = authoredFarClipPlane;
        }

        private void ApplyCurrentShotWithFeedback()
        {
            if (targetCamera == null || cameraRig == null
                || !hasCurrentBaseShot)
            {
                return;
            }

            if (!FpgFormalCameraPoseUtility.TryApplyShot(
                    currentBaseShot,
                    cameraRig,
                    targetCamera,
                    out _))
            {
                return;
            }

            Vector3 cameraSpaceOffset = Vector3.back * currentKick
                + currentShakePosition;
            targetCamera.transform.localPosition =
                currentBaseShot.CameraLocalPose.position
                + currentBaseShot.CameraLocalPose.rotation
                    * cameraSpaceOffset;
            targetCamera.transform.localRotation =
                currentBaseShot.CameraLocalPose.rotation
                * Quaternion.Euler(0f, 0f, currentShakeRotation);
        }

        private void AdvanceKick(float deltaTime)
        {
            if (deltaTime <= 0f || currentKick <= 0f)
            {
                return;
            }

            float recovery = threeCProfile.ShotCameraKickRecoverySeconds;
            if (recovery <= 0f)
            {
                currentKick = 0f;
                return;
            }

            float maximumKick = Mathf.Max(
                0.001f,
                Mathf.Max(
                    threeCProfile.PrimaryShotCameraKick,
                    threeCProfile.SecondaryShotCameraKick));
            currentKick = Mathf.MoveTowards(
                currentKick,
                0f,
                maximumKick * deltaTime / recovery);
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
                impulse.Remaining = Mathf.Max(
                    0f,
                    impulse.Remaining - deltaTime);
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
                System.Array.Clear(
                    shakeImpulses,
                    0,
                    shakeImpulses.Length);
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
