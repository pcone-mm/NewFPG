using System;
using UnityEngine;
using VolumetricFogAndMist2;
using VolumetricLights;

namespace FPG.Demo.Unity
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class FpgVolumetricRoomArtBinding : MonoBehaviour,
        IFpgRoomArtPresentationBinding
    {
        private VolumetricFogManager[] fogManagers =
            Array.Empty<VolumetricFogManager>();
        private VolumetricFog[] fogVolumes = Array.Empty<VolumetricFog>();
        private VolumetricLight[] volumetricLights =
            Array.Empty<VolumetricLight>();
        private VolumetricLightDirectionalSync[] directionalSyncs =
            Array.Empty<VolumetricLightDirectionalSync>();

        private Camera boundCamera;
        private Transform boundCameraTransform;
        private Light boundSun;
        private bool bound;

        public bool IsBound => bound;

        public bool TryBind(
            FpgRoomArtPresentationContext context,
            out string error)
        {
            if (context == null)
            {
                error = "Volumetric room art requires a presentation context.";
                return false;
            }

            if (!context.TryValidate(out error))
            {
                return false;
            }

            Unbind();
            DiscoverComponents();
            boundCamera = context.FormalCamera;
            boundCameraTransform = context.CameraTransform;
            boundSun = context.MainLight;
            bound = true;

            for (int index = 0; index < fogManagers.Length; index++)
            {
                fogManagers[index].sun = boundSun;
            }

            for (int index = 0; index < fogVolumes.Length; index++)
            {
                fogVolumes[index].updateModeCamera = boundCamera;
                fogVolumes[index].fadeController = boundCameraTransform;
            }

            for (int index = 0; index < volumetricLights.Length; index++)
            {
                volumetricLights[index].targetCamera = boundCameraTransform;
            }

            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                directionalSyncs[index].directionalLight = boundSun;
                directionalSyncs[index].follow = boundCameraTransform;
            }

            if (volumetricLights.Length > 0)
            {
                VolumetricLight.mainCamera = boundCameraTransform;
            }

            error = string.Empty;
            return true;
        }

        public void Unbind()
        {
            if (!bound)
            {
                return;
            }

            for (int index = 0; index < fogManagers.Length; index++)
            {
                VolumetricFogManager manager = fogManagers[index];
                if (manager != null && manager.sun == boundSun)
                {
                    manager.sun = null;
                }
            }

            for (int index = 0; index < fogVolumes.Length; index++)
            {
                VolumetricFog fog = fogVolumes[index];
                if (fog == null)
                {
                    continue;
                }

                if (fog.updateModeCamera == boundCamera)
                {
                    fog.updateModeCamera = null;
                }

                if (fog.fadeController == boundCameraTransform)
                {
                    fog.fadeController = null;
                }
            }

            for (int index = 0; index < volumetricLights.Length; index++)
            {
                VolumetricLight volumetricLight = volumetricLights[index];
                if (volumetricLight != null
                    && volumetricLight.targetCamera == boundCameraTransform)
                {
                    volumetricLight.targetCamera = null;
                }
            }

            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                VolumetricLightDirectionalSync sync = directionalSyncs[index];
                if (sync == null)
                {
                    continue;
                }

                if (sync.directionalLight == boundSun)
                {
                    sync.directionalLight = null;
                }

                if (sync.follow == boundCameraTransform)
                {
                    sync.follow = null;
                }
            }

            if (VolumetricLight.mainCamera == boundCameraTransform)
            {
                VolumetricLight.mainCamera = null;
            }

            fogManagers = Array.Empty<VolumetricFogManager>();
            fogVolumes = Array.Empty<VolumetricFog>();
            volumetricLights = Array.Empty<VolumetricLight>();
            directionalSyncs = Array.Empty<VolumetricLightDirectionalSync>();
            boundCamera = null;
            boundCameraTransform = null;
            boundSun = null;
            bound = false;
        }

        private void DiscoverComponents()
        {
            FpgRoomArtRoot artRoot = GetComponentInParent<FpgRoomArtRoot>();
            Transform searchRoot = artRoot == null
                ? transform.root
                : artRoot.transform;
            fogManagers = searchRoot
                .GetComponentsInChildren<VolumetricFogManager>(true);
            fogVolumes = searchRoot.GetComponentsInChildren<VolumetricFog>(true);
            volumetricLights = searchRoot
                .GetComponentsInChildren<VolumetricLight>(true);
            directionalSyncs = searchRoot
                .GetComponentsInChildren<VolumetricLightDirectionalSync>(true);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
