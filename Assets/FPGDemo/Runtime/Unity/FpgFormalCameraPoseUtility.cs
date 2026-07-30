using UnityEngine;

namespace FPG.Demo.Unity
{
    public static class FpgFormalCameraPoseUtility
    {
        public static bool TryResolveShot(
            Pose playerWorldPose,
            FpgCoverCameraProfile profile,
            out FpgResolvedCameraShot shot,
            out string error)
        {
            shot = default;
            if (profile == null)
            {
                error = "Formal camera shot requires a cover camera profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                return false;
            }

            if (!IsFinite(playerWorldPose.position)
                || !IsValidRotation(playerWorldPose.rotation))
            {
                error = "Formal camera shot requires a finite player world pose.";
                return false;
            }

            Pose rigWorldPose = new Pose(
                playerWorldPose.position
                    + playerWorldPose.rotation
                        * profile.CameraRigLocalPosition,
                playerWorldPose.rotation
                    * Quaternion.Euler(
                        profile.CameraRigLocalEulerAngles));
            Pose cameraLocalPose = new Pose(
                profile.CameraLocalPosition,
                Quaternion.Euler(profile.CameraLocalEulerAngles));
            shot = new FpgResolvedCameraShot(
                rigWorldPose,
                cameraLocalPose,
                profile.FieldOfView,
                profile.NearClipPlane,
                profile.FarClipPlane);
            return shot.TryValidate(out error);
        }

        public static bool TryResolveShot(
            Transform playerRoot,
            FpgCoverCameraProfile profile,
            out FpgResolvedCameraShot shot,
            out string error)
        {
            if (playerRoot == null)
            {
                shot = default;
                error = "Formal camera shot requires the placed player root.";
                return false;
            }

            return TryResolveShot(
                new Pose(playerRoot.position, playerRoot.rotation),
                profile,
                out shot,
                out error);
        }

        public static bool TryApplyShot(
            in FpgResolvedCameraShot shot,
            Transform cameraRig,
            Camera targetCamera,
            out string error)
        {
            if (!shot.TryValidate(out error))
            {
                return false;
            }

            if (targetCamera == null || cameraRig == null)
            {
                error = "Formal camera feedback requires an explicit camera and scene-owned rig.";
                return false;
            }

            if (targetCamera.transform.parent != cameraRig)
            {
                error = "Formal camera must be a direct child of the scene-owned camera rig.";
                return false;
            }

            cameraRig.SetPositionAndRotation(
                shot.RigWorldPose.position,
                shot.RigWorldPose.rotation);
            targetCamera.transform.localPosition =
                shot.CameraLocalPose.position;
            targetCamera.transform.localRotation =
                shot.CameraLocalPose.rotation;
            targetCamera.fieldOfView = shot.FieldOfView;
            targetCamera.nearClipPlane = shot.NearClipPlane;
            targetCamera.farClipPlane = shot.FarClipPlane;
            error = string.Empty;
            return true;
        }

        public static FpgResolvedCameraShot Interpolate(
            in FpgResolvedCameraShot source,
            in FpgResolvedCameraShot target,
            float progress)
        {
            float t = Mathf.Clamp01(progress);
            return new FpgResolvedCameraShot(
                new Pose(
                    Vector3.LerpUnclamped(
                        source.RigWorldPose.position,
                        target.RigWorldPose.position,
                        t),
                    Quaternion.SlerpUnclamped(
                        source.RigWorldPose.rotation,
                        target.RigWorldPose.rotation,
                        t)),
                new Pose(
                    Vector3.LerpUnclamped(
                        source.CameraLocalPose.position,
                        target.CameraLocalPose.position,
                        t),
                    Quaternion.SlerpUnclamped(
                        source.CameraLocalPose.rotation,
                        target.CameraLocalPose.rotation,
                        t)),
                Mathf.LerpUnclamped(
                    source.FieldOfView,
                    target.FieldOfView,
                    t),
                Mathf.LerpUnclamped(
                    source.NearClipPlane,
                    target.NearClipPlane,
                    t),
                Mathf.LerpUnclamped(
                    source.FarClipPlane,
                    target.FarClipPlane,
                    t));
        }

        public static bool TryInterpolate(
            in FpgResolvedCameraShot source,
            in FpgResolvedCameraShot target,
            float progress,
            out FpgResolvedCameraShot shot,
            out string error)
        {
            shot = default;
            if (!source.TryValidate(out error)
                || !target.TryValidate(out error)
                || float.IsNaN(progress)
                || float.IsInfinity(progress))
            {
                error = string.IsNullOrEmpty(error)
                    ? "Formal camera shot interpolation requires a finite progress value."
                    : error;
                return false;
            }

            shot = Interpolate(source, target, progress);
            return shot.TryValidate(out error);
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
