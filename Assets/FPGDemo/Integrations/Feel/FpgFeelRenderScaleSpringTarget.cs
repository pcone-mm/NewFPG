using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPG.Demo.Unity
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class FpgFeelRenderScaleSpringTarget :
        MMSpringFloatComponent<FpgFeelRenderScaleSpringTarget>
    {
        [SerializeField] private float minimumScale = 0.985f;
        [SerializeField] private float maximumScale = 1.035f;

        private Transform visualRoot;
        private float scaleMultiplier = 1f;
        private Vector3 scaleBeforeRendering;
        private int cameraRenderDepth;
        private bool renderScaleApplied;
        private bool renderingSubscribed;

        public Transform VisualRoot => visualRoot;
        public float ScaleMultiplier => scaleMultiplier;
        public float MinimumScale => minimumScale;
        public float MaximumScale => maximumScale;
        public bool IsRenderScaleApplied => renderScaleApplied;

        public void BindVisualRoot(Transform nextVisualRoot)
        {
            if (visualRoot == nextVisualRoot)
            {
                return;
            }

            ForceRestoreRenderedScale();
            visualRoot = nextVisualRoot;
            Target = this;
        }

        public void StopAndRestore()
        {
            Stop();
            FloatSpring.RestoreInitialValue();
            ApplyValue(FloatSpring.CurrentValue);
            ForceRestoreRenderedScale();
        }

        public void ForceRestoreRenderedScale()
        {
            if (renderScaleApplied && visualRoot != null)
            {
                visualRoot.localScale = scaleBeforeRendering;
            }

            renderScaleApplied = false;
            cameraRenderDepth = 0;
        }

        protected override void ApplyValue(float newValue)
        {
            scaleMultiplier = Mathf.Clamp(
                newValue,
                Mathf.Min(minimumScale, maximumScale),
                Mathf.Max(minimumScale, maximumScale));
        }

        protected override void GrabCurrentValue()
        {
            FloatSpring.CurrentValue = scaleMultiplier;
        }

        private void OnEnable()
        {
            if (renderingSubscribed)
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering +=
                HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering +=
                HandleEndCameraRendering;
            renderingSubscribed = true;
        }

        private void OnDisable()
        {
            if (renderingSubscribed)
            {
                RenderPipelineManager.beginCameraRendering -=
                    HandleBeginCameraRendering;
                RenderPipelineManager.endCameraRendering -=
                    HandleEndCameraRendering;
                renderingSubscribed = false;
            }

            ForceRestoreRenderedScale();
            if (FloatSpring != null)
            {
                FloatSpring.RestoreInitialValue();
                ApplyValue(FloatSpring.CurrentValue);
            }
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            cameraRenderDepth++;
            if (cameraRenderDepth != 1
                || visualRoot == null
                || renderScaleApplied)
            {
                return;
            }

            scaleBeforeRendering = visualRoot.localScale;
            visualRoot.localScale = scaleBeforeRendering * scaleMultiplier;
            renderScaleApplied = true;
        }

        private void HandleEndCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (cameraRenderDepth > 0)
            {
                cameraRenderDepth--;
            }

            if (cameraRenderDepth == 0)
            {
                ForceRestoreRenderedScale();
            }
        }
    }
}
