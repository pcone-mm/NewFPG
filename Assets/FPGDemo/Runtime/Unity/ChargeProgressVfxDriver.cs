using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only progress adapter for a pooled held charge effect.
    /// It restores authored particle values before the instance returns to the
    /// shared pool, so progress never accumulates across borrows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChargeProgressVfxDriver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Root scaled by normalized charge progress. Defaults to this transform.")]
        private Transform particleRoot;

        [SerializeField, Min(0.01f)]
        private float minimumScaleMultiplier = 0.6f;

        [SerializeField, Min(0.01f)]
        private float maximumScaleMultiplier = 1.2f;

        [SerializeField, Min(0f)]
        private float minimumEmissionMultiplier = 0.25f;

        [SerializeField, Min(0f)]
        private float maximumEmissionMultiplier = 1.5f;

        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
        private float[] authoredEmissionRates = Array.Empty<float>();
        private Vector3 activationRootScale = Vector3.one;
        private bool authoringCached;
        private bool poolUseActive;

        public float Progress { get; private set; }

        public Transform ParticleRoot => particleRoot == null
            ? transform
            : particleRoot;

        private void Awake()
        {
            CacheAuthoringState();
        }

        private void OnEnable()
        {
            BeginPoolUse();
        }

        private void OnDisable()
        {
            ResetForPool();
        }

        public void SetProgress(float normalized)
        {
            if (!poolUseActive)
            {
                BeginPoolUse();
            }

            Progress = IsFinite(normalized) ? Mathf.Clamp01(normalized) : 0f;
            float scaleMultiplier = Mathf.Lerp(
                minimumScaleMultiplier,
                maximumScaleMultiplier,
                Progress);
            ParticleRoot.localScale = activationRootScale * scaleMultiplier;

            float emissionMultiplier = Mathf.Lerp(
                minimumEmissionMultiplier,
                maximumEmissionMultiplier,
                Progress);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.rateOverTimeMultiplier =
                    authoredEmissionRates[index] * emissionMultiplier;
            }
        }

        public void ResetForPool()
        {
            if (!authoringCached || !poolUseActive)
            {
                Progress = 0f;
                return;
            }

            ParticleRoot.localScale = activationRootScale;
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.rateOverTimeMultiplier = authoredEmissionRates[index];
            }

            Progress = 0f;
            poolUseActive = false;
        }

        public bool TryValidate(out string error)
        {
            if (!IsFinitePositive(minimumScaleMultiplier)
                || !IsFinitePositive(maximumScaleMultiplier)
                || maximumScaleMultiplier < minimumScaleMultiplier
                || !IsFiniteNonNegative(minimumEmissionMultiplier)
                || !IsFiniteNonNegative(maximumEmissionMultiplier)
                || maximumEmissionMultiplier < minimumEmissionMultiplier)
            {
                error = "Charge progress VFX requires ordered finite scale and emission ranges.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void BeginPoolUse()
        {
            CacheAuthoringState();
            activationRootScale = ParticleRoot.localScale;
            poolUseActive = true;
            SetProgress(0f);
        }

        private void CacheAuthoringState()
        {
            if (authoringCached)
            {
                return;
            }

            if (particleRoot == null)
            {
                particleRoot = transform;
            }

            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            authoredEmissionRates = new float[particleSystems.Length];
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem particleSystem = particleSystems[index];
                authoredEmissionRates[index] = particleSystem == null
                    ? 0f
                    : particleSystem.emission.rateOverTimeMultiplier;
            }

            authoringCached = true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }
    }
}
