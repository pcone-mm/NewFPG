using System;
using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class HuxinjingShieldEffect : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ShieldFadeId = Shader.PropertyToID("_ShieldFade");
        private static readonly int WaveAmountId = Shader.PropertyToID("_WaveAmount");
        private static readonly int WavePhaseId = Shader.PropertyToID("_WavePhase");
        private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int Layer1AngleId = Shader.PropertyToID("_Layer1Angle");
        private static readonly int Layer2AngleId = Shader.PropertyToID("_Layer2Angle");
        private static readonly int Layer3AngleId = Shader.PropertyToID("_Layer3Angle");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int GlowSpreadId = Shader.PropertyToID("_GlowSpread");
        private static readonly int GlowFalloffId = Shader.PropertyToID("_GlowFalloff");
        private static readonly int GlowNoiseId = Shader.PropertyToID("_GlowNoise");
        private static readonly int GlassColorId = Shader.PropertyToID("_GlassColor");
        private static readonly int GlassStrengthId = Shader.PropertyToID("_GlassStrength");
        private static readonly int DynamicMaskStrengthId = Shader.PropertyToID("_DynamicMaskStrength");

        [Header("Layer References")]
        [SerializeField] private Renderer compositeRenderer;
        [SerializeField] private ShieldLayer[] layers = Array.Empty<ShieldLayer>();

        [Header("PSD Opacity")]
        [SerializeField, Range(0f, 1f)] private float baseOpacity = 1f;
        [SerializeField, Range(0f, 1f)] private float shieldRatio = 1f;
        [SerializeField, Range(0.01f, 1f)] private float halfFadeThreshold = 0.5f;
        [SerializeField, Range(0f, 1f)] private float opacityAtZeroShield = 0.18f;
        [SerializeField] private bool autoDissolveAtZeroShield = true;

        [Header("Release")]
        [SerializeField] private bool playReleaseOnEnable = true;
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.32f;
        [SerializeField, Range(0.1f, 1f)] private float releaseStartScale = 0.88f;
        [SerializeField] private AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Steady Rotation")]
        [SerializeField] private bool rotateWhenVisible = true;

        [SerializeField, HideInInspector] private Color outerGlowColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField, HideInInspector, Range(0f, 4f)] private float outerGlowStrength = 0.66f;
        [SerializeField, HideInInspector, Range(0f, 128f)] private float outerGlowRadiusPixels = 65f;
        [SerializeField, HideInInspector, Range(0f, 1f)] private float outerGlowSpread = 0.18f;
        [SerializeField, HideInInspector, Range(0.25f, 4f)] private float outerGlowFalloff = 1.35f;
        [SerializeField, HideInInspector, Range(0f, 0.5f)] private float outerGlowNoise = 0.16f;
        [SerializeField, HideInInspector] private Color glassColor = new Color(1f, 1f, 1f, 0.28f);
        [SerializeField, HideInInspector, Range(0f, 1f)] private float glassStrength = 0.16f;
        [SerializeField, HideInInspector, Range(0f, 1f)] private float dynamicMaskStrength = 1f;

        [Header("Hit Wave")]
        [SerializeField, Min(0.01f)] private float hitWaveDuration = 0.46f;
        [SerializeField, Range(0f, 0.12f)] private float hitWaveStrength = 0.026f;
        [SerializeField, Min(1f)] private float hitWaveFrequency = 34f;
        [SerializeField, Min(0f)] private float hitWaveTravel = 1.25f;
        [SerializeField, Range(0f, 0.12f)] private float hitScalePulse = 0.034f;

        [Header("Dissolve")]
        [SerializeField, Min(0.01f)] private float dissolveDuration = 0.72f;
        [SerializeField, Range(0f, 0.35f)] private float dissolveScaleExpand = 0.14f;
        [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Facing")]
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool useUnscaledTime;

        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseLocalScale = Vector3.one;
        private float releaseElapsed = 1f;
        private float hitElapsed = 1f;
        private float hitIntensity = 1f;
        private float dissolveElapsed = 1f;
        private bool releasePlaying;
        private bool hitPlaying;
        private bool dissolvePlaying;
        private bool hiddenAfterDissolve;
        private bool zeroShieldDissolvePlayed;
        private float[] layerAngles = new float[3];

        public float ShieldRatio => shieldRatio;
        public bool IsDissolving => dissolvePlaying;
        public bool IsHidden => hiddenAfterDissolve;

        private void Reset()
        {
            CacheLayerRenderers();
        }

        private void Awake()
        {
            baseLocalScale = transform.localScale;
            EnsureLayerAngles();
            CacheLayerRenderers();
            ApplyMaterialState();
        }

        private void OnEnable()
        {
            baseLocalScale = transform.localScale;
            SetRenderersEnabled(true);

            if (playReleaseOnEnable)
            {
                PlayRelease();
            }
            else
            {
                releasePlaying = false;
                hiddenAfterDissolve = false;
                ApplyPose(1f, 0f, 0f);
                ApplyMaterialState();
            }
        }

        private void OnDisable()
        {
            transform.localScale = baseLocalScale;
        }

        private void OnValidate()
        {
            baseOpacity = Mathf.Clamp01(baseOpacity);
            shieldRatio = Mathf.Clamp01(shieldRatio);
            halfFadeThreshold = Mathf.Clamp(halfFadeThreshold, 0.01f, 1f);
            opacityAtZeroShield = Mathf.Clamp01(opacityAtZeroShield);
            releaseDuration = Mathf.Max(0.01f, releaseDuration);
            releaseStartScale = Mathf.Clamp(releaseStartScale, 0.1f, 1f);
            outerGlowStrength = Mathf.Clamp(outerGlowStrength, 0f, 4f);
            outerGlowRadiusPixels = Mathf.Clamp(outerGlowRadiusPixels, 0f, 128f);
            outerGlowSpread = Mathf.Clamp01(outerGlowSpread);
            outerGlowFalloff = Mathf.Clamp(outerGlowFalloff, 0.25f, 4f);
            outerGlowNoise = Mathf.Clamp(outerGlowNoise, 0f, 0.5f);
            glassStrength = Mathf.Clamp01(glassStrength);
            dynamicMaskStrength = Mathf.Clamp01(dynamicMaskStrength);
            hitWaveDuration = Mathf.Max(0.01f, hitWaveDuration);
            hitWaveStrength = Mathf.Clamp(hitWaveStrength, 0f, 0.12f);
            hitWaveFrequency = Mathf.Max(1f, hitWaveFrequency);
            hitWaveTravel = Mathf.Max(0f, hitWaveTravel);
            hitScalePulse = Mathf.Clamp(hitScalePulse, 0f, 0.12f);
            dissolveDuration = Mathf.Max(0.01f, dissolveDuration);
            dissolveScaleExpand = Mathf.Clamp(dissolveScaleExpand, 0f, 0.35f);
            EnsureCurves();
            EnsureLayerAngles();
            CacheLayerRenderers();
            ApplyMaterialState();
        }

        private void Update()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f)
            {
                ApplyMaterialState();
                return;
            }

            if (autoDissolveAtZeroShield && !zeroShieldDissolvePlayed && shieldRatio <= 0.0001f)
            {
                PlayDissolve();
                zeroShieldDissolvePlayed = true;
            }

            AdvanceTimers(deltaTime);
            RotateLayers(deltaTime);

            float releaseAmount = ResolveReleaseAmount();
            float hitAmount = ResolveHitAmount();
            float dissolveAmount = ResolveDissolveAmount();
            ApplyPose(releaseAmount, hitAmount, dissolveAmount);
            ApplyMaterialState();
        }

        [ContextMenu("Huxinjing/Play Release")]
        public void PlayRelease()
        {
            releaseElapsed = 0f;
            dissolveElapsed = 0f;
            releasePlaying = true;
            dissolvePlaying = false;
            hiddenAfterDissolve = false;
            SetRenderersEnabled(true);
            ApplyMaterialState();
        }

        [ContextMenu("Huxinjing/Play Hit Wave")]
        public void PlayHit()
        {
            PlayHit(1f);
        }

        public void PlayHit(float intensity)
        {
            hitElapsed = 0f;
            hitIntensity = Mathf.Max(0f, intensity);
            hitPlaying = hitIntensity > 0f;
            SetRenderersEnabled(true);
            ApplyMaterialState();
        }

        [ContextMenu("Huxinjing/Play Dissolve")]
        public void PlayDissolve()
        {
            dissolveElapsed = 0f;
            dissolvePlaying = true;
            releasePlaying = false;
            hiddenAfterDissolve = false;
            SetRenderersEnabled(true);
            ApplyMaterialState();
        }

        [ContextMenu("Huxinjing/Reset Visual State")]
        public void ResetVisualState()
        {
            shieldRatio = 1f;
            releaseElapsed = releaseDuration;
            hitElapsed = hitWaveDuration;
            dissolveElapsed = dissolveDuration;
            releasePlaying = false;
            hitPlaying = false;
            dissolvePlaying = false;
            hiddenAfterDissolve = false;
            zeroShieldDissolvePlayed = false;
            ResetLayerAngles();
            ResetLayerTransforms();
            transform.localScale = baseLocalScale;
            SetRenderersEnabled(true);
            ApplyMaterialState();
        }

        [ContextMenu("Huxinjing/Set Shield Full")]
        public void SetShieldFull()
        {
            SetShieldRatio(1f);
        }

        [ContextMenu("Huxinjing/Set Shield Half")]
        public void SetShieldHalf()
        {
            SetShieldRatio(0.5f);
        }

        [ContextMenu("Huxinjing/Set Shield Empty")]
        public void SetShieldEmpty()
        {
            SetShieldRatio(0f);
        }

        public void SetShieldRatio(float ratio)
        {
            float previousRatio = shieldRatio;
            shieldRatio = Mathf.Clamp01(ratio);
            if (shieldRatio > 0.0001f)
            {
                zeroShieldDissolvePlayed = false;
                if (hiddenAfterDissolve)
                {
                    hiddenAfterDissolve = false;
                    dissolvePlaying = false;
                    SetRenderersEnabled(true);
                }
            }
            else if (autoDissolveAtZeroShield && previousRatio > 0.0001f)
            {
                PlayDissolve();
                zeroShieldDissolvePlayed = true;
            }

            ApplyMaterialState();
        }

        public void SetShieldValues(float current, float maximum)
        {
            SetShieldRatio(maximum <= 0f ? 0f : current / maximum);
        }

        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
        }

        private void AdvanceTimers(float deltaTime)
        {
            if (releasePlaying)
            {
                releaseElapsed += deltaTime;
                if (releaseElapsed >= releaseDuration)
                {
                    releaseElapsed = releaseDuration;
                    releasePlaying = false;
                }
            }

            if (hitPlaying)
            {
                hitElapsed += deltaTime;
                if (hitElapsed >= hitWaveDuration)
                {
                    hitElapsed = hitWaveDuration;
                    hitPlaying = false;
                }
            }

            if (!dissolvePlaying)
            {
                return;
            }

            dissolveElapsed += deltaTime;
            if (dissolveElapsed >= dissolveDuration)
            {
                dissolveElapsed = dissolveDuration;
                dissolvePlaying = false;
                hiddenAfterDissolve = true;
                SetRenderersEnabled(false);
            }
        }

        private void RotateLayers(float deltaTime)
        {
            if (!rotateWhenVisible || hiddenAfterDissolve || layers == null)
            {
                return;
            }

            EnsureLayerAngles();
            for (int i = 0; i < layers.Length; i++)
            {
                ShieldLayer layer = layers[i];
                if (layer == null)
                {
                    continue;
                }

                float speed = layer.RotationDegreesPerSecond;
                if (Mathf.Approximately(speed, 0f))
                {
                    continue;
                }

                if (i < layerAngles.Length)
                {
                    layerAngles[i] = Mathf.Repeat(layerAngles[i] + speed * deltaTime, 360f);
                }

                if (layer.LayerTransform != null)
                {
                    layer.LayerTransform.Rotate(0f, 0f, speed * deltaTime, Space.Self);
                }
            }
        }

        private float ResolveReleaseAmount()
        {
            if (!releasePlaying)
            {
                return 1f;
            }

            return EvaluateCurve(releaseCurve, releaseElapsed / releaseDuration);
        }

        private float ResolveHitAmount()
        {
            if (!hitPlaying)
            {
                return 0f;
            }

            float normalized = Mathf.Clamp01(hitElapsed / hitWaveDuration);
            float envelope = 1f - normalized;
            return envelope * envelope * hitIntensity;
        }

        private float ResolveDissolveAmount()
        {
            if (!dissolvePlaying && !hiddenAfterDissolve)
            {
                return 0f;
            }

            return hiddenAfterDissolve ? 1f : EvaluateCurve(dissolveCurve, dissolveElapsed / dissolveDuration);
        }

        private void ApplyPose(float releaseAmount, float hitAmount, float dissolveAmount)
        {
            float releaseScale = Mathf.Lerp(releaseStartScale, 1f, releaseAmount);
            float hitScale = 1f;
            if (hitAmount > 0f)
            {
                float wave = Mathf.Sin(Mathf.Clamp01(hitElapsed / hitWaveDuration) * Mathf.PI * 4f);
                hitScale += wave * hitScalePulse * hitAmount;
            }

            float dissolveScale = 1f + dissolveScaleExpand * dissolveAmount;
            transform.localScale = baseLocalScale * releaseScale * hitScale * dissolveScale;
            FaceCameraIfNeeded();
        }

        private void ApplyMaterialState()
        {
            float shieldFade = ResolveShieldFade();
            float releaseAlpha = releasePlaying ? ResolveReleaseAmount() : 1f;
            float dissolveAmount = ResolveDissolveAmount();
            float dissolveAlpha = 1f - dissolveAmount;
            float hitAmount = ResolveHitAmount();
            float wavePhase = hitPlaying ? Mathf.Clamp01(hitElapsed / hitWaveDuration) * hitWaveTravel : 0f;

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            if (compositeRenderer != null)
            {
                ApplyRendererVisualState(
                    compositeRenderer,
                    baseOpacity,
                    shieldFade * releaseAlpha * dissolveAlpha,
                    hitAmount,
                    wavePhase,
                    dissolveAmount,
                    true);
            }

            if (layers == null)
            {
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                ShieldLayer layer = layers[i];
                if (layer == null || layer.LayerRenderer == null)
                {
                    continue;
                }

                ApplyRendererVisualState(
                    layer.LayerRenderer,
                    baseOpacity * Mathf.Clamp01(layer.OpacityMultiplier),
                    shieldFade * releaseAlpha * dissolveAlpha,
                    hitAmount,
                    wavePhase,
                    dissolveAmount,
                    false);
            }
        }

        private void ApplyRendererVisualState(Renderer renderer, float opacity, float shieldFade, float hitAmount, float wavePhase, float dissolveAmount, bool applyLayerAngles)
        {
            if (renderer is SpriteRenderer spriteRenderer)
            {
                Color color = spriteRenderer.color;
                color.a = Mathf.Clamp01(opacity * shieldFade);
                spriteRenderer.color = color;
                return;
            }

            renderer.GetPropertyBlock(propertyBlock);
            ApplyCommonMaterialProperties(propertyBlock, opacity, shieldFade, hitAmount, wavePhase, dissolveAmount);
            if (applyLayerAngles)
            {
                ApplyLayerAngles(propertyBlock);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyCommonMaterialProperties(MaterialPropertyBlock block, float opacity, float shieldFade, float hitAmount, float wavePhase, float dissolveAmount)
        {
            block.SetFloat(OpacityId, opacity);
            block.SetFloat(ShieldFadeId, shieldFade);
            block.SetFloat(WaveAmountId, hitWaveStrength * hitAmount);
            block.SetFloat(WavePhaseId, wavePhase);
            block.SetFloat(WaveFrequencyId, hitWaveFrequency);
            block.SetFloat(DissolveAmountId, dissolveAmount);
            block.SetColor(GlowColorId, outerGlowColor);
            block.SetFloat(GlowStrengthId, outerGlowStrength);
            block.SetFloat(GlowRadiusId, outerGlowRadiusPixels);
            block.SetFloat(GlowSpreadId, outerGlowSpread);
            block.SetFloat(GlowFalloffId, outerGlowFalloff);
            block.SetFloat(GlowNoiseId, outerGlowNoise);
            block.SetColor(GlassColorId, glassColor);
            block.SetFloat(GlassStrengthId, glassStrength);
            block.SetFloat(DynamicMaskStrengthId, dynamicMaskStrength);
        }

        private void ApplyLayerAngles(MaterialPropertyBlock block)
        {
            EnsureLayerAngles();
            block.SetFloat(Layer1AngleId, layerAngles.Length > 0 ? layerAngles[0] : 0f);
            block.SetFloat(Layer2AngleId, layerAngles.Length > 1 ? layerAngles[1] : 0f);
            block.SetFloat(Layer3AngleId, layerAngles.Length > 2 ? layerAngles[2] : 0f);
        }

        private float ResolveShieldFade()
        {
            if (shieldRatio >= halfFadeThreshold)
            {
                return 1f;
            }

            float normalized = Mathf.Clamp01(shieldRatio / halfFadeThreshold);
            return Mathf.Lerp(opacityAtZeroShield, 1f, normalized);
        }

        private void FaceCameraIfNeeded()
        {
            if (!faceCamera)
            {
                return;
            }

            Camera cameraToFace = targetCamera != null ? targetCamera : Camera.main;
            if (cameraToFace == null)
            {
                return;
            }

            Vector3 toCamera = cameraToFace.transform.position - transform.position;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, cameraToFace.transform.up);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (compositeRenderer != null)
            {
                compositeRenderer.enabled = enabled;
            }

            if (layers == null)
            {
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                ShieldLayer layer = layers[i];
                if (layer != null && layer.LayerRenderer != null)
                {
                    layer.LayerRenderer.enabled = enabled;
                }
            }
        }

        private void EnsureLayerAngles()
        {
            int count = layers == null ? 0 : layers.Length;
            if (count < 3)
            {
                count = 3;
            }

            if (layerAngles != null && layerAngles.Length == count)
            {
                return;
            }

            float[] previousAngles = layerAngles;
            layerAngles = new float[count];
            if (previousAngles == null)
            {
                return;
            }

            Array.Copy(previousAngles, layerAngles, Mathf.Min(previousAngles.Length, layerAngles.Length));
        }

        private void ResetLayerAngles()
        {
            EnsureLayerAngles();
            for (int i = 0; i < layerAngles.Length; i++)
            {
                layerAngles[i] = 0f;
            }
        }

        private void ResetLayerTransforms()
        {
            if (layers == null)
            {
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                ShieldLayer layer = layers[i];
                if (layer != null && layer.LayerTransform != null)
                {
                    layer.LayerTransform.localRotation = Quaternion.identity;
                }
            }
        }

        private void CacheLayerRenderers()
        {
            if (layers == null)
            {
                return;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                ShieldLayer layer = layers[i];
                if (layer == null)
                {
                    continue;
                }

                layer.CacheRenderer();
            }
        }

        private void EnsureCurves()
        {
            if (releaseCurve == null || releaseCurve.length == 0)
            {
                releaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (dissolveCurve == null || dissolveCurve.length == 0)
            {
                dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }

        private static float EvaluateCurve(AnimationCurve curve, float normalizedTime)
        {
            if (curve == null || curve.length == 0)
            {
                return Mathf.Clamp01(normalizedTime);
            }

            return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(normalizedTime)));
        }

        [Serializable]
        public sealed class ShieldLayer
        {
            [SerializeField] private string label;
            [SerializeField] private Transform layerTransform;
            [SerializeField] private Renderer layerRenderer;
            [SerializeField] private float rotationDegreesPerSecond;
            [SerializeField, Range(0f, 1f)] private float opacityMultiplier = 1f;

            public string Label => label;
            public Transform LayerTransform => layerTransform;
            public Renderer LayerRenderer => layerRenderer;
            public float RotationDegreesPerSecond => rotationDegreesPerSecond;
            public float OpacityMultiplier => opacityMultiplier;

            public void CacheRenderer()
            {
                if (layerRenderer == null && layerTransform != null)
                {
                    layerRenderer = layerTransform.GetComponent<Renderer>();
                    if (layerRenderer == null)
                    {
                        layerRenderer = layerTransform.GetComponentInChildren<Renderer>(true);
                    }
                }
            }
        }
    }
}
