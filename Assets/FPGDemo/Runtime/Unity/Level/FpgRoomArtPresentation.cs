using UnityEngine;

namespace FPG.Demo.Unity
{
    public sealed class FpgRoomArtPresentationContext
    {
        public FpgRoomArtPresentationContext(
            Camera formalCamera,
            Transform cameraTransform,
            Light mainLight,
            ICombatAimViewportSource aimViewportSource)
        {
            FormalCamera = formalCamera;
            CameraTransform = cameraTransform;
            MainLight = mainLight;
            AimViewportSource = aimViewportSource;
        }

        public FpgRoomArtPresentationContext(
            Camera formalCamera,
            Light mainLight,
            ICombatAimViewportSource aimViewportSource)
            : this(
                formalCamera,
                formalCamera != null ? formalCamera.transform : null,
                mainLight,
                aimViewportSource)
        {
        }

        public Camera FormalCamera { get; }
        public Transform CameraTransform { get; }
        public Light MainLight { get; }
        public ICombatAimViewportSource AimViewportSource { get; }

        public bool TryValidate(out string error)
        {
            if (FormalCamera == null)
            {
                error = "Room art presentation requires an explicit formal Camera.";
                return false;
            }

            if (CameraTransform == null)
            {
                error = "Room art presentation requires an explicit Camera transform.";
                return false;
            }

            if (FormalCamera.transform != CameraTransform)
            {
                error = "Room art presentation Camera and Camera transform must reference the same object.";
                return false;
            }

            if (MainLight == null || MainLight.type != LightType.Directional)
            {
                error = "Room art presentation requires an explicit directional main light.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    public interface IFpgRoomArtPresentationBinding
    {
        bool TryBind(
            FpgRoomArtPresentationContext context,
            out string error);

        void Unbind();
    }
}
