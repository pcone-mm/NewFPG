using System;
using UnityEngine;
using VolumetricFogAndMist2;
using VolumetricLights;
#if UNITY_EDITOR
using System.Reflection;
#endif

namespace FPG.Demo.Unity
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class FpgVolumetricRoomArtBinding : MonoBehaviour,
        IFpgRoomArtPresentationBinding
    {
#if UNITY_EDITOR
        private const BindingFlags PluginMessageFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo DirectionalStartMethod =
            typeof(VolumetricLightDirectionalSync).GetMethod(
                "Start",
                PluginMessageFlags);
        private static readonly MethodInfo DirectionalLateUpdateMethod =
            typeof(VolumetricLightDirectionalSync).GetMethod(
                "LateUpdate",
                PluginMessageFlags);
        private static readonly MethodInfo VolumetricLightLateUpdateMethod =
            typeof(VolumetricLight).GetMethod(
                "LateUpdate",
                PluginMessageFlags);
#endif

        [SerializeField]
        private VolumetricFogManager fogManager;

        [SerializeField]
        private VolumetricFog[] fogVolumes = Array.Empty<VolumetricFog>();

        [SerializeField]
        private VolumetricLight[] volumetricLights =
            Array.Empty<VolumetricLight>();

        [SerializeField]
        private VolumetricLightDirectionalSync[] directionalSyncs =
            Array.Empty<VolumetricLightDirectionalSync>();

        private bool authoredStateCaptured;
        private bool bound;
        private bool fogManagerEnabled;
        private bool[] fogVolumeEnabled = Array.Empty<bool>();
        private bool[] volumetricLightEnabled = Array.Empty<bool>();
        private bool[] directionalSyncEnabled = Array.Empty<bool>();
        private Light authoredFogSun;
        private Camera[] authoredFogCameras = Array.Empty<Camera>();
        private Transform[] authoredFadeControllers = Array.Empty<Transform>();
        private Transform[] authoredVolumetricLightCameras = Array.Empty<Transform>();
        private Light[] authoredDirectionalLights = Array.Empty<Light>();
        private Transform[] authoredDirectionalFollows = Array.Empty<Transform>();
        private Vector3[] authoredDirectionalPositions = Array.Empty<Vector3>();
        private Quaternion[] authoredDirectionalRotations = Array.Empty<Quaternion>();
        private bool[] authoredProxyLightEnabled = Array.Empty<bool>();
        private Color[] authoredProxyLightColors = Array.Empty<Color>();
        private float[] authoredProxyLightIntensities = Array.Empty<float>();
        private Transform boundCameraTransform;

        public VolumetricFogManager FogManager => fogManager;
        public bool IsBound => bound;

        private void Awake()
        {
            CaptureAuthoredState();
            if (Application.isPlaying)
            {
                SetEffectsEnabled(false);
            }
        }

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

            if (!TryValidateReferences(out error))
            {
                return false;
            }

            CaptureAuthoredState();
            if (bound)
            {
                Unbind();
                CaptureAuthoredState();
            }

            SetEffectsEnabled(false);
            fogManager.sun = context.MainLight;
            for (int index = 0; index < fogVolumes.Length; index++)
            {
                fogVolumes[index].updateModeCamera = context.FormalCamera;
                fogVolumes[index].fadeController = context.CameraTransform;
            }

            for (int index = 0; index < volumetricLights.Length; index++)
            {
                volumetricLights[index].targetCamera = context.CameraTransform;
            }

            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                directionalSyncs[index].directionalLight = context.MainLight;
                directionalSyncs[index].follow = context.CameraTransform;
            }

            boundCameraTransform = context.CameraTransform;
            VolumetricLight.mainCamera = boundCameraTransform;
            bound = true;
            RestoreAuthoredEnabledState();
            if (!TrySynchronizeBoundEffects(out error))
            {
                Unbind();
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Unbind()
        {
            if (!authoredStateCaptured)
            {
                return;
            }

            SetEffectsEnabled(false);
            if (bound && VolumetricLight.mainCamera == boundCameraTransform)
            {
                VolumetricLight.mainCamera = null;
            }

            fogManager.sun = Application.isPlaying ? null : authoredFogSun;
            for (int index = 0; index < fogVolumes.Length; index++)
            {
                fogVolumes[index].updateModeCamera = Application.isPlaying
                    ? null
                    : authoredFogCameras[index];
                fogVolumes[index].fadeController = Application.isPlaying
                    ? null
                    : authoredFadeControllers[index];
            }

            for (int index = 0; index < volumetricLights.Length; index++)
            {
                volumetricLights[index].targetCamera = Application.isPlaying
                    ? null
                    : authoredVolumetricLightCameras[index];
            }

            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                directionalSyncs[index].directionalLight = Application.isPlaying
                    ? null
                    : authoredDirectionalLights[index];
                directionalSyncs[index].follow = Application.isPlaying
                    ? null
                    : authoredDirectionalFollows[index];
            }

            RestoreAuthoredDirectionalState();
            boundCameraTransform = null;
            bound = false;
            if (!Application.isPlaying)
            {
                RestoreAuthoredEnabledState();
                authoredStateCaptured = false;
            }
        }

        public bool TryValidateReferences(out string error)
        {
            if (fogManager == null)
            {
                error =
                    "Volumetric room art binding requires an explicit Fog Manager.";
                return false;
            }

            if (!ContainsOnlyAssigned(fogVolumes)
                || !ContainsOnlyAssigned(volumetricLights)
                || !ContainsOnlyAssigned(directionalSyncs))
            {
                error =
                    "Volumetric room art binding arrays cannot contain missing components.";
                return false;
            }

            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                VolumetricLight volumetricLight =
                    directionalSyncs[index].GetComponent<VolumetricLight>();
                Light proxyLight = directionalSyncs[index].GetComponent<Light>();
                if (volumetricLight == null || proxyLight == null
                    || Array.IndexOf(volumetricLights, volumetricLight) < 0)
                {
                    error =
                        "Each directional sync requires a co-located Light and "
                        + "a VolumetricLight listed by the room art binding.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void CaptureAuthoredState()
        {
            if (authoredStateCaptured || !TryValidateReferences(out _))
            {
                return;
            }

            fogManagerEnabled = fogManager.enabled;
            authoredFogSun = fogManager.sun;
            fogVolumeEnabled = new bool[fogVolumes.Length];
            authoredFogCameras = new Camera[fogVolumes.Length];
            authoredFadeControllers = new Transform[fogVolumes.Length];
            for (int index = 0; index < fogVolumes.Length; index++)
            {
                fogVolumeEnabled[index] = fogVolumes[index].enabled;
                authoredFogCameras[index] = fogVolumes[index].updateModeCamera;
                authoredFadeControllers[index] = fogVolumes[index].fadeController;
            }

            volumetricLightEnabled = new bool[volumetricLights.Length];
            authoredVolumetricLightCameras =
                new Transform[volumetricLights.Length];
            for (int index = 0; index < volumetricLights.Length; index++)
            {
                volumetricLightEnabled[index] = volumetricLights[index].enabled;
                authoredVolumetricLightCameras[index] =
                    volumetricLights[index].targetCamera;
            }

            directionalSyncEnabled = new bool[directionalSyncs.Length];
            authoredDirectionalLights = new Light[directionalSyncs.Length];
            authoredDirectionalFollows = new Transform[directionalSyncs.Length];
            authoredDirectionalPositions = new Vector3[directionalSyncs.Length];
            authoredDirectionalRotations = new Quaternion[directionalSyncs.Length];
            authoredProxyLightEnabled = new bool[directionalSyncs.Length];
            authoredProxyLightColors = new Color[directionalSyncs.Length];
            authoredProxyLightIntensities = new float[directionalSyncs.Length];
            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                directionalSyncEnabled[index] = directionalSyncs[index].enabled;
                authoredDirectionalLights[index] =
                    directionalSyncs[index].directionalLight;
                authoredDirectionalFollows[index] = directionalSyncs[index].follow;
                authoredDirectionalPositions[index] =
                    directionalSyncs[index].transform.position;
                authoredDirectionalRotations[index] =
                    directionalSyncs[index].transform.rotation;
                Light proxyLight = directionalSyncs[index].GetComponent<Light>();
                authoredProxyLightEnabled[index] = proxyLight.enabled;
                authoredProxyLightColors[index] = proxyLight.color;
                authoredProxyLightIntensities[index] = proxyLight.intensity;
            }

            authoredStateCaptured = true;
        }

        private bool TrySynchronizeBoundEffects(out string error)
        {
            if (Application.isPlaying)
            {
                error = string.Empty;
                return true;
            }

#if UNITY_EDITOR
            if (DirectionalStartMethod == null
                || DirectionalLateUpdateMethod == null
                || VolumetricLightLateUpdateMethod == null)
            {
                error =
                    "The installed volumetric-light plugin no longer exposes "
                    + "the editor update messages required for formal preview.";
                return false;
            }

            try
            {
                for (int index = 0; index < fogVolumes.Length; index++)
                {
                    if (fogVolumes[index].enabled)
                    {
                        fogVolumes[index].UpdateMaterialPropertiesNow();
                    }
                }

                for (int index = 0; index < volumetricLights.Length; index++)
                {
                    if (volumetricLights[index].enabled)
                    {
                        volumetricLights[index].Refresh();
                    }
                }

                for (int index = 0; index < directionalSyncs.Length; index++)
                {
                    VolumetricLightDirectionalSync sync =
                        directionalSyncs[index];
                    if (!sync.enabled)
                    {
                        continue;
                    }

                    DirectionalStartMethod.Invoke(sync, null);
                    DirectionalLateUpdateMethod.Invoke(sync, null);
                }

                for (int index = 0; index < volumetricLights.Length; index++)
                {
                    if (volumetricLights[index].enabled)
                    {
                        VolumetricLightLateUpdateMethod.Invoke(
                            volumetricLights[index],
                            null);
                    }
                }
            }
            catch (Exception exception)
            {
                error =
                    "Could not synchronize volumetric effects for formal "
                    + "editor preview: " + exception.GetBaseException().Message;
                return false;
            }
#endif

            error = string.Empty;
            return true;
        }

        private void RestoreAuthoredDirectionalState()
        {
            if (authoredDirectionalPositions.Length != directionalSyncs.Length
                || authoredDirectionalRotations.Length != directionalSyncs.Length
                || authoredProxyLightEnabled.Length != directionalSyncs.Length
                || authoredProxyLightColors.Length != directionalSyncs.Length
                || authoredProxyLightIntensities.Length != directionalSyncs.Length)
            {
                return;
            }

            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                VolumetricLightDirectionalSync sync = directionalSyncs[index];
                sync.transform.SetPositionAndRotation(
                    authoredDirectionalPositions[index],
                    authoredDirectionalRotations[index]);
                Light proxyLight = sync.GetComponent<Light>();
                proxyLight.color = authoredProxyLightColors[index];
                proxyLight.intensity = authoredProxyLightIntensities[index];
                proxyLight.enabled = authoredProxyLightEnabled[index];
            }
        }

        private void SetEffectsEnabled(bool enabled)
        {
            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                if (directionalSyncs[index] != null)
                {
                    directionalSyncs[index].enabled = enabled;
                }
            }

            for (int index = 0; index < volumetricLights.Length; index++)
            {
                if (volumetricLights[index] != null)
                {
                    volumetricLights[index].enabled = enabled;
                }
            }

            for (int index = 0; index < fogVolumes.Length; index++)
            {
                if (fogVolumes[index] != null)
                {
                    fogVolumes[index].enabled = enabled;
                }
            }

            if (fogManager != null)
            {
                fogManager.enabled = enabled;
            }
        }

        private void RestoreAuthoredEnabledState()
        {
            fogManager.enabled = fogManagerEnabled;
            for (int index = 0; index < fogVolumes.Length; index++)
            {
                fogVolumes[index].enabled = fogVolumeEnabled[index];
            }

            for (int index = 0; index < volumetricLights.Length; index++)
            {
                volumetricLights[index].enabled = volumetricLightEnabled[index];
            }

            for (int index = 0; index < directionalSyncs.Length; index++)
            {
                directionalSyncs[index].enabled = directionalSyncEnabled[index];
            }
        }

        private static bool ContainsOnlyAssigned<T>(T[] values)
            where T : UnityEngine.Object
        {
            if (values == null)
            {
                return false;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
