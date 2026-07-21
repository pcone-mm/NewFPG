using UnityEngine;

namespace NewFPG.Prototype
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TongQianJianFloatingBody : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer outerGlowRenderer;
        [SerializeField] private SpriteRenderer innerGlowRenderer;

        [Header("Floating Motion")]
        [SerializeField, Min(0f)] private float hoverAmplitude = 0.12f;
        [SerializeField, Min(0.01f)] private float hoverCyclesPerSecond = 0.55f;
        [SerializeField, Range(0f, 20f)] private float depthSwayDegrees = 7.5f;
        [SerializeField, Range(0f, 10f)] private float pitchSwayDegrees = 1.2f;
        [SerializeField, Range(0f, 10f)] private float rollSwayDegrees = 2f;
        [SerializeField, Range(0f, 6.2831855f)] private float phaseOffset = 0.35f;

        [Header("Glow Pulse")]
        [SerializeField, Min(0.01f)] private float glowCyclesPerSecond = 0.8f;
        [SerializeField, Range(0f, 0.2f)] private float glowScaleAmount = 0.035f;
        [SerializeField, Range(0f, 0.8f)] private float glowAlphaAmount = 0.18f;

        private Vector3 baseVisualPosition;
        private Quaternion baseVisualRotation;
        private Vector3 baseOuterGlowScale;
        private Color baseOuterGlowColor;
        private Color baseInnerGlowColor;
        private bool stateCached;

        private void Reset()
        {
            TryAutoBind();
            CacheBaseState();
        }

        private void OnEnable()
        {
            stateCached = false;
            TryAutoBind();
            CacheBaseState();
        }

        private void OnDisable()
        {
            RestoreBaseState();
            stateCached = false;
        }

        private void OnValidate()
        {
            hoverAmplitude = Mathf.Max(0f, hoverAmplitude);
            hoverCyclesPerSecond = Mathf.Max(0.01f, hoverCyclesPerSecond);
            glowCyclesPerSecond = Mathf.Max(0.01f, glowCyclesPerSecond);
            TryAutoBind();
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (!stateCached)
            {
                CacheBaseState();
            }

            double time = Application.isPlaying ? Time.timeAsDouble : Time.realtimeSinceStartupAsDouble;
            float floatingPhase = (float)(time * hoverCyclesPerSecond * Mathf.PI * 2.0) + phaseOffset;
            float secondaryPhase = floatingPhase * 0.73f + 1.1f;

            visualRoot.localPosition = baseVisualPosition + Vector3.up * (Mathf.Sin(floatingPhase) * hoverAmplitude);
            visualRoot.localRotation = baseVisualRotation * Quaternion.Euler(
                Mathf.Sin(secondaryPhase) * pitchSwayDegrees,
                Mathf.Sin(floatingPhase) * depthSwayDegrees,
                Mathf.Cos(secondaryPhase) * rollSwayDegrees);

            UpdateGlow(time);
        }

        private void TryAutoBind()
        {
            if (visualRoot == null)
            {
                Transform candidate = transform.Find("Visual");
                visualRoot = candidate != null ? candidate : transform;
            }

            if (outerGlowRenderer == null && visualRoot != null)
            {
                Transform candidate = visualRoot.Find("OuterGlow");
                if (candidate != null)
                {
                    outerGlowRenderer = candidate.GetComponent<SpriteRenderer>();
                }
            }

            if (innerGlowRenderer == null && visualRoot != null)
            {
                Transform candidate = visualRoot.Find("InnerGlow");
                if (candidate != null)
                {
                    innerGlowRenderer = candidate.GetComponent<SpriteRenderer>();
                }
            }
        }

        private void CacheBaseState()
        {
            if (visualRoot == null)
            {
                return;
            }

            baseVisualPosition = visualRoot.localPosition;
            baseVisualRotation = visualRoot.localRotation;

            if (outerGlowRenderer != null)
            {
                baseOuterGlowScale = outerGlowRenderer.transform.localScale;
                baseOuterGlowColor = outerGlowRenderer.color;
            }

            if (innerGlowRenderer != null)
            {
                baseInnerGlowColor = innerGlowRenderer.color;
            }

            stateCached = true;
        }

        private void UpdateGlow(double time)
        {
            float glowPhase = (float)(time * glowCyclesPerSecond * Mathf.PI * 2.0) + phaseOffset * 1.37f;
            float normalizedPulse = Mathf.Sin(glowPhase) * 0.5f + 0.5f;
            float centeredPulse = normalizedPulse * 2f - 1f;

            if (outerGlowRenderer != null)
            {
                outerGlowRenderer.transform.localScale = baseOuterGlowScale * (1f + centeredPulse * glowScaleAmount);
                Color color = baseOuterGlowColor;
                color.a *= 1f + centeredPulse * glowAlphaAmount;
                outerGlowRenderer.color = color;
            }

            if (innerGlowRenderer != null)
            {
                Color color = baseInnerGlowColor;
                color.a *= 1f + centeredPulse * glowAlphaAmount * 0.65f;
                innerGlowRenderer.color = color;
            }
        }

        private void RestoreBaseState()
        {
            if (!stateCached || visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = baseVisualPosition;
            visualRoot.localRotation = baseVisualRotation;

            if (outerGlowRenderer != null)
            {
                outerGlowRenderer.transform.localScale = baseOuterGlowScale;
                outerGlowRenderer.color = baseOuterGlowColor;
            }

            if (innerGlowRenderer != null)
            {
                innerGlowRenderer.color = baseInnerGlowColor;
            }
        }
    }
}
