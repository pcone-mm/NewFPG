using UnityEngine;

namespace FPG.Demo.Unity
{
    public static class FpgFormalCameraPoseUtility
    {
        public static bool TryApplyFixedPose(
            D0ThreeCProfile profile,
            Transform playerRoot,
            Transform cameraRig,
            Camera targetCamera,
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

            if (playerRoot == null)
            {
                error = "Formal camera feedback requires the placed player root.";
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

            if (cameraRig == playerRoot || cameraRig.IsChildOf(playerRoot))
            {
                error = "Formal camera rig must remain scene-owned and cannot be under the player root.";
                return false;
            }

            cameraRig.SetPositionAndRotation(
                playerRoot.TransformPoint(profile.CameraPivotLocalPosition),
                playerRoot.rotation * Quaternion.Euler(profile.CameraPivotLocalEulerAngles));
            targetCamera.transform.localPosition = profile.CameraLocalPosition;
            targetCamera.transform.localRotation = Quaternion.Euler(profile.CameraLocalEulerAngles);
            targetCamera.fieldOfView = profile.CameraFieldOfView;
            targetCamera.nearClipPlane = profile.CameraNearClipPlane;
            targetCamera.farClipPlane = profile.CameraFarClipPlane;
            error = string.Empty;
            return true;
        }
    }
}
