using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fully resolved formal-camera state. Rig pose is world-space while the
    /// Camera pose remains local to the scene-owned rig.
    /// </summary>
    public readonly struct FpgResolvedCameraShot
    {
        public FpgResolvedCameraShot(
            Pose rigWorldPose,
            Pose cameraLocalPose,
            float fieldOfView,
            float nearClipPlane,
            float farClipPlane)
        {
            RigWorldPose = rigWorldPose;
            CameraLocalPose = cameraLocalPose;
            FieldOfView = fieldOfView;
            NearClipPlane = nearClipPlane;
            FarClipPlane = farClipPlane;
        }

        public Pose RigWorldPose { get; }
        public Pose CameraLocalPose { get; }
        public float FieldOfView { get; }
        public float NearClipPlane { get; }
        public float FarClipPlane { get; }

        public bool IsValid => TryValidate(out _);

        public bool TryValidate(out string error)
        {
            if (!IsFinite(RigWorldPose.position)
                || !IsValidRotation(RigWorldPose.rotation)
                || !IsFinite(CameraLocalPose.position)
                || !IsValidRotation(CameraLocalPose.rotation))
            {
                error = "Resolved formal camera shot requires finite poses and valid rotations.";
                return false;
            }

            if (!IsFinite(FieldOfView)
                || FieldOfView <= 1f
                || FieldOfView >= 179f
                || !IsFinite(NearClipPlane)
                || !IsFinite(FarClipPlane)
                || NearClipPlane <= 0f
                || FarClipPlane <= NearClipPlane)
            {
                error = "Resolved formal camera shot requires a valid perspective lens and clip planes.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsValidRotation(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w)
                && value.x * value.x
                    + value.y * value.y
                    + value.z * value.z
                    + value.w * value.w > 0.000001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
