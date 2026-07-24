using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only temporary spirit barrier for Fei. The component reads
    /// already committed legacy or formal player state, then fades a small
    /// camera-facing arc. It has no colliders, combat state or damage logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgPlayerBarrierPresentationController : MonoBehaviour
    {
        private const int ArcPointCount = 17;
        private IFpgFormalPlayerPresentationSource formalSource;

        [SerializeField] private Material lineMaterial;
        [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.12f;
        [SerializeField, Range(0f, 1f)] private float maximumOpacity = 0.72f;
        [SerializeField] private Color barrierColor =
            new Color(0.34f, 0.88f, 1f, 1f);

        private readonly Vector3[] arcPoints = new Vector3[ArcPointCount];
        private LineRenderer lineRenderer;
        private float currentOpacity;
        public IFpgFormalPlayerPresentationSource FormalSource => formalSource;
        public bool IsFormalSourceBound => formalSource != null;
        public float CurrentOpacity => currentOpacity;
        public bool IsVisible => currentOpacity > 0.001f;

        public static bool ShouldShowBarrier(
            in FpgFormalPlayerPresentationSnapshot snapshot)
        {
            return snapshot.IsCombatActive
                && snapshot.ExposureState == PlayerExposureState.Withdrawn
                && snapshot.Barrier > 0;
        }

        /// <summary>
        /// Binds the formal committed player presentation source.
        /// </summary>
        public bool TryBindFormalSource(
            IFpgFormalPlayerPresentationSource nextSource,
            out string error)
        {
            if (nextSource == null)
            {
                error = "D0 player barrier formal binding requires a presentation source.";
                return false;
            }

            formalSource = nextSource;
            SetOpacity(0f);
            error = string.Empty;
            return true;
        }

        public void UnbindFormalSource()
        {
            formalSource = null;
            SetOpacity(0f);
        }

        public bool TrySetThreeCProfile(D0ThreeCProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "D0 player barrier presentation requires a D0 3C profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                return false;
            }

            fadeInSeconds = Mathf.Max(
                profile.BarrierFadeInSeconds,
                profile.RetractTransitionSeconds);
            fadeOutSeconds = Mathf.Max(
                profile.BarrierFadeOutSeconds,
                profile.PeekTransitionSeconds);
            maximumOpacity = profile.BarrierMaximumOpacity;
            barrierColor = profile.BarrierColor;
            currentOpacity = Mathf.Min(currentOpacity, maximumOpacity);
            SetOpacity(currentOpacity);
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (lineMaterial == null)
            {
                error = "D0 player barrier presentation requires a transparent line material.";
                return false;
            }

            if (lineRenderer == null)
            {
                error = "D0 player barrier presentation requires a LineRenderer.";
                return false;
            }

            if (GetComponentsInChildren<Collider>(true).Length > 0
                || GetComponentsInChildren<Collider2D>(true).Length > 0
                || GetComponentsInChildren<Rigidbody>(true).Length > 0
                || GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                error = "D0 player barrier presentation must not contain Collider or Rigidbody components.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void Awake()
        {
            EnsureLineRenderer();
            SetOpacity(0f);
        }

        private void OnDisable()
        {
            SetOpacity(0f);
        }

        private void LateUpdate()
        {
            if (formalSource == null
                || !formalSource.TryGetPlayerPresentationSnapshot(
                    out FpgFormalPlayerPresentationSnapshot snapshot))
            {
                AdvanceOpacity(false);
                return;
            }

            if (snapshot.IsPaused)
            {
                return;
            }

            AdvanceOpacity(ShouldShowBarrier(snapshot));
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

            lineRenderer.useWorldSpace = false;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.loop = false;
            lineRenderer.positionCount = ArcPointCount;
            lineRenderer.startWidth = 0.055f;
            lineRenderer.endWidth = 0.035f;
            lineRenderer.numCornerVertices = 3;
            lineRenderer.numCapVertices = 2;
            lineRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            if (lineMaterial != null)
            {
                lineRenderer.sharedMaterial = lineMaterial;
            }

            for (int index = 0; index < ArcPointCount; index++)
            {
                float normalized = index / (float)(ArcPointCount - 1);
                float angle = Mathf.Lerp(-1.18f, 1.18f, normalized);
                arcPoints[index] = new Vector3(
                    Mathf.Sin(angle) * 0.92f,
                    Mathf.Cos(angle) * 1.05f - 0.18f,
                    0f);
            }

            lineRenderer.SetPositions(arcPoints);
        }

        private void AdvanceOpacity(bool targetVisible)
        {
            float targetOpacity = targetVisible ? maximumOpacity : 0f;
            float duration = targetOpacity > currentOpacity
                ? fadeInSeconds
                : fadeOutSeconds;
            float delta = duration <= 0f
                ? Mathf.Abs(targetOpacity - currentOpacity)
                : Time.unscaledDeltaTime / duration;
            SetOpacity(Mathf.MoveTowards(currentOpacity, targetOpacity, delta));
        }

        private void SetOpacity(float opacity)
        {
            currentOpacity = Mathf.Clamp01(opacity);
            if (lineRenderer == null)
            {
                return;
            }

            Color color = barrierColor;
            color.a *= currentOpacity;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.enabled = currentOpacity > 0.001f;
        }
    }
}
