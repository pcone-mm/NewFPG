using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPG.Demo.Presentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class GenshinPlanarReflection : MonoBehaviour
    {
        private enum ResolutionMultiplier
        {
            Full = 1,
            Half = 2,
            Third = 3,
            Quarter = 4
        }

        private static readonly int ReflectionTextureId = Shader.PropertyToID("_ReflectionTex");

        [Header("Reflection")]
        [SerializeField] private ResolutionMultiplier resolution = ResolutionMultiplier.Half;
        [SerializeField, Min(0f)] private float clipPlaneOffset = 0.07f;
        [SerializeField] private LayerMask reflectedLayers = ~0;
        [SerializeField] private bool renderShadows;
        [SerializeField, Min(0)] private int rendererIndex;

        [Header("Water Plane")]
        [Tooltip("The imported water mesh faces Local Forward, which maps to World Up in this scene.")]
        [SerializeField] private Vector3 localPlaneNormal = Vector3.forward;
        [SerializeField] private float planeOffset;

        private Camera reflectionCamera;
        private RenderTexture reflectionTexture;
        private Renderer waterRenderer;
        private bool isRendering;

        private void OnEnable()
        {
            waterRenderer = GetComponent<Renderer>();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

            if (reflectionCamera != null)
            {
                reflectionCamera.targetTexture = null;
                SafeDestroy(reflectionCamera.gameObject);
                reflectionCamera = null;
            }

            if (reflectionTexture != null)
            {
                reflectionTexture.Release();
                SafeDestroy(reflectionTexture);
                reflectionTexture = null;
            }
        }

        private static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera sourceCamera)
        {
            if (isRendering || sourceCamera == null || sourceCamera == reflectionCamera)
                return;

            if (sourceCamera.cameraType == CameraType.Preview || sourceCamera.cameraType == CameraType.Reflection)
                return;

            if (UniversalRenderPipeline.asset == null || !isActiveAndEnabled)
                return;

            isRendering = true;
            try
            {
                EnsureReflectionCamera(sourceCamera);
                EnsureReflectionTexture(sourceCamera);
                UpdateReflectionCamera(sourceCamera);
                RenderReflection(context);
                Shader.SetGlobalTexture(ReflectionTextureId, reflectionTexture);
            }
            finally
            {
                isRendering = false;
            }
        }

        private void EnsureReflectionCamera(Camera sourceCamera)
        {
            if (reflectionCamera == null)
            {
                var cameraObject = new GameObject($"{name} Planar Reflection Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                reflectionCamera = cameraObject.AddComponent<Camera>();
                reflectionCamera.enabled = false;

                var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderType = CameraRenderType.Base;
                cameraData.renderPostProcessing = false;
                cameraData.renderShadows = renderShadows;
                cameraData.requiresColorTexture = false;
                cameraData.requiresDepthTexture = false;
                cameraData.allowXRRendering = false;
                cameraData.SetRenderer(rendererIndex);
            }

            reflectionCamera.CopyFrom(sourceCamera);
            reflectionCamera.enabled = false;
            reflectionCamera.useOcclusionCulling = false;
            reflectionCamera.clearFlags = sourceCamera.clearFlags;
            if (reflectionCamera.clearFlags == CameraClearFlags.Depth ||
                reflectionCamera.clearFlags == CameraClearFlags.Nothing)
            {
                reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            Color reflectionBackground = sourceCamera.backgroundColor;
            reflectionBackground.a = 1f;
            reflectionCamera.backgroundColor = reflectionBackground;
            reflectionCamera.cullingMask = reflectedLayers;

            if (reflectionCamera.TryGetComponent(out UniversalAdditionalCameraData reflectionData))
            {
                reflectionData.renderType = CameraRenderType.Base;
                reflectionData.renderPostProcessing = false;
                reflectionData.renderShadows = renderShadows;
                reflectionData.requiresColorTexture = false;
                reflectionData.requiresDepthTexture = false;
                reflectionData.allowXRRendering = false;
                reflectionData.SetRenderer(rendererIndex);
            }
        }

        private void EnsureReflectionTexture(Camera sourceCamera)
        {
            float scale = UniversalRenderPipeline.asset.renderScale / (int)resolution;
            int width = Mathf.Max(64, Mathf.RoundToInt(sourceCamera.pixelWidth * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(sourceCamera.pixelHeight * scale));

            if (reflectionTexture != null && reflectionTexture.width == width && reflectionTexture.height == height)
                return;

            if (reflectionTexture != null)
            {
                reflectionTexture.Release();
                SafeDestroy(reflectionTexture);
            }

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.DefaultHDR, 24)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };

            reflectionTexture = new RenderTexture(descriptor)
            {
                name = $"{name} Planar Reflection",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            reflectionTexture.Create();
        }

        private void UpdateReflectionCamera(Camera sourceCamera)
        {
            Vector3 normal = transform.TransformDirection(localPlaneNormal).normalized;
            if (normal.sqrMagnitude < 0.001f)
                normal = Vector3.up;

            Vector3 planePosition = transform.position + normal * planeOffset;
            float planeDistance = -Vector3.Dot(normal, planePosition) - clipPlaneOffset;
            var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, planeDistance);
            Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(reflectionPlane);

            reflectionCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * reflectionMatrix;
            reflectionCamera.transform.position = ReflectPoint(sourceCamera.transform.position, reflectionPlane);
            reflectionCamera.transform.forward = Vector3.Reflect(sourceCamera.transform.forward, normal);
            reflectionCamera.transform.up = Vector3.Reflect(sourceCamera.transform.up, normal);

            Vector4 clipPlane = CameraSpacePlane(reflectionCamera, planePosition, normal, 1f);
            reflectionCamera.projectionMatrix = sourceCamera.CalculateObliqueMatrix(clipPlane);
            reflectionCamera.targetTexture = reflectionTexture;
        }

        private void RenderReflection(ScriptableRenderContext context)
        {
            bool previousInvertCulling = GL.invertCulling;
            bool previousRendererState = waterRenderer != null && waterRenderer.enabled;

            try
            {
                GL.invertCulling = true;
                if (waterRenderer != null)
                    waterRenderer.enabled = false;

#pragma warning disable CS0618
                UniversalRenderPipeline.RenderSingleCamera(context, reflectionCamera);
#pragma warning restore CS0618
            }
            finally
            {
                GL.invertCulling = previousInvertCulling;
                if (waterRenderer != null)
                    waterRenderer.enabled = previousRendererState;
            }
        }

        private Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign)
        {
            Vector3 offsetPosition = position + normal * clipPlaneOffset;
            Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
            Vector3 cameraPosition = worldToCamera.MultiplyPoint(offsetPosition);
            Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
                -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private static Vector3 ReflectPoint(Vector3 point, Vector4 plane)
        {
            float distance = Vector3.Dot(new Vector3(plane.x, plane.y, plane.z), point) + plane.w;
            return point - 2f * distance * new Vector3(plane.x, plane.y, plane.z);
        }

        private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = 1f - 2f * plane.x * plane.x;
            matrix.m01 = -2f * plane.x * plane.y;
            matrix.m02 = -2f * plane.x * plane.z;
            matrix.m03 = -2f * plane.w * plane.x;
            matrix.m10 = -2f * plane.y * plane.x;
            matrix.m11 = 1f - 2f * plane.y * plane.y;
            matrix.m12 = -2f * plane.y * plane.z;
            matrix.m13 = -2f * plane.w * plane.y;
            matrix.m20 = -2f * plane.z * plane.x;
            matrix.m21 = -2f * plane.z * plane.y;
            matrix.m22 = 1f - 2f * plane.z * plane.z;
            matrix.m23 = -2f * plane.w * plane.z;
            return matrix;
        }
    }
}
