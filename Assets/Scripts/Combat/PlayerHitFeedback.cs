using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatVitals))]
    public sealed class PlayerHitFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatVitals vitals;
        [SerializeField] private Camera targetCamera;

        [Header("Camera Shake")]
        [SerializeField, Min(0f)] private float shakeDuration = 0.18f;
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.12f;
        [SerializeField, Min(1f)] private float shakeFrequency = 32f;
        [SerializeField] private bool useCinemachineImpulse = true;
        [SerializeField, Min(1)] private int impulseChannelMask = 1;
        [SerializeField, Min(0f)] private float impulseGain = 1.25f;

        [Header("Screen Edge Flash")]
        [SerializeField] private Color edgeFlashColor = new Color(1f, 0.05f, 0.02f, 0.55f);
        [SerializeField, Min(0f)] private float edgeFlashDuration = 0.35f;
        [SerializeField, Min(0f)] private float edgeThickness = 96f;
        [SerializeField, Min(0f)] private float edgeFadePower = 1.8f;

        private Canvas feedbackCanvas;
        private CanvasGroup edgeGroup;
        private RectTransform[] edgeRects;
        private float shakeRemaining;
        private float shakeSeed;
        private float flashRemaining;
        private Transform shakeCameraTransform;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private bool hasBaseCameraPose;
        private float cinemachineShakeRemaining;
        private CinemachineImpulseSource impulseSource;

        public bool IsFeedbackActive => shakeRemaining > 0f || cinemachineShakeRemaining > 0f || flashRemaining > 0f;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            EnsureEdgeOverlay();
        }

        private void OnEnable()
        {
            CacheReferences();
            if (vitals != null)
            {
                vitals.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (vitals != null)
            {
                vitals.Damaged -= OnDamaged;
            }

            RestoreCameraPose();
            SetEdgeAlpha(0f);
            shakeRemaining = 0f;
            cinemachineShakeRemaining = 0f;
            flashRemaining = 0f;
        }

        private void OnValidate()
        {
            shakeDuration = Mathf.Max(0f, shakeDuration);
            shakeAmplitude = Mathf.Max(0f, shakeAmplitude);
            shakeFrequency = Mathf.Max(1f, shakeFrequency);
            impulseChannelMask = Mathf.Max(1, impulseChannelMask);
            impulseGain = Mathf.Max(0f, impulseGain);
            edgeFlashDuration = Mathf.Max(0f, edgeFlashDuration);
            edgeThickness = Mathf.Max(0f, edgeThickness);
            edgeFadePower = Mathf.Max(0f, edgeFadePower);
            CacheReferences();
        }

        private void LateUpdate()
        {
            UpdateCinemachineShakeTimer();
            UpdateCameraShake();
            UpdateEdgeFlash();
        }

        public void SetTargetCamera(Camera camera)
        {
            if (targetCamera == camera)
            {
                return;
            }

            RestoreCameraPose();
            targetCamera = camera;
        }

        public void PlayFeedback(float intensity = 1f)
        {
            float clampedIntensity = Mathf.Max(0f, intensity);
            if (shakeDuration > 0f && shakeAmplitude > 0f)
            {
                if (TryPlayCinemachineShake(clampedIntensity))
                {
                    RestoreCameraPose();
                    shakeRemaining = 0f;
                }
                else
                {
                    shakeRemaining = Mathf.Max(shakeRemaining, shakeDuration);
                    shakeSeed = Random.value * 1000f;
                    CaptureCameraPose();
                }
            }

            if (edgeFlashDuration > 0f)
            {
                flashRemaining = Mathf.Max(flashRemaining, edgeFlashDuration * Mathf.Max(0.2f, clampedIntensity));
                EnsureEdgeOverlay();
                SetEdgeAlpha(edgeFlashColor.a);
            }
        }

        private void OnDamaged(CombatVitals changedVitals, DamagePayload payload)
        {
            if (changedVitals == null || changedVitals != vitals || payload.Amount <= 0f)
            {
                return;
            }

            PlayFeedback(Mathf.Clamp01(payload.Amount / Mathf.Max(1f, vitals.MaxHealth) * 4f));
        }

        private bool TryPlayCinemachineShake(float intensity)
        {
            if (!useCinemachineImpulse)
            {
                return false;
            }

            Camera camera = ResolveCamera();
            if (camera == null || camera.GetComponent<CinemachineBrain>() == null)
            {
                return false;
            }

            EnsureCinemachineImpulseListener(camera);
            CinemachineImpulseSource source = EnsureCinemachineImpulseSource();
            if (source == null)
            {
                return false;
            }

            float strength = shakeAmplitude * Mathf.Max(0.2f, intensity) * Mathf.Max(0f, impulseGain);
            float horizontalSign = Random.value < 0.5f ? -1f : 1f;
            Vector3 velocity = new Vector3(horizontalSign * strength, strength * 0.65f, 0f);
            source.GenerateImpulseAtPositionWithVelocity(camera.transform.position, velocity);
            cinemachineShakeRemaining = Mathf.Max(cinemachineShakeRemaining, shakeDuration);
            return true;
        }

        private void EnsureCinemachineImpulseListener(Camera camera)
        {
            CinemachineExternalImpulseListener listener = camera.GetComponent<CinemachineExternalImpulseListener>();
            if (listener == null)
            {
                listener = camera.gameObject.AddComponent<CinemachineExternalImpulseListener>();
            }

            listener.ChannelMask |= impulseChannelMask;
            listener.Gain = Mathf.Max(listener.Gain, impulseGain);
            listener.UseLocalSpace = true;
        }

        private CinemachineImpulseSource EnsureCinemachineImpulseSource()
        {
            if (impulseSource == null)
            {
                impulseSource = GetComponent<CinemachineImpulseSource>();
            }

            if (impulseSource == null)
            {
                impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            }

            if (impulseSource.ImpulseDefinition == null)
            {
                impulseSource.ImpulseDefinition = new CinemachineImpulseDefinition();
            }

            CinemachineImpulseDefinition definition = impulseSource.ImpulseDefinition;
            definition.ImpulseChannel = impulseChannelMask;
            definition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Recoil;
            definition.ImpulseDuration = Mathf.Max(0.01f, shakeDuration);
            definition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            definition.DissipationDistance = 100f;
            definition.DissipationRate = 0.25f;
            impulseSource.DefaultVelocity = new Vector3(shakeAmplitude, shakeAmplitude * 0.65f, 0f);
            return impulseSource;
        }

        private void UpdateCinemachineShakeTimer()
        {
            if (cinemachineShakeRemaining <= 0f)
            {
                return;
            }

            cinemachineShakeRemaining = Mathf.Max(0f, cinemachineShakeRemaining - Time.unscaledDeltaTime);
        }

        private void UpdateCameraShake()
        {
            if (shakeRemaining <= 0f)
            {
                RestoreCameraPose();
                return;
            }

            Camera camera = ResolveCamera();
            if (camera == null)
            {
                shakeRemaining = 0f;
                return;
            }

            if (shakeCameraTransform != camera.transform)
            {
                RestoreCameraPose();
                CaptureCameraPose(camera);
            }

            float ratio = shakeDuration <= 0f ? 0f : Mathf.Clamp01(shakeRemaining / shakeDuration);
            float envelope = ratio * ratio;
            float time = Time.unscaledTime * shakeFrequency + shakeSeed;
            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(time, 0.17f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0.41f, time) - 0.5f) * 2f,
                0f) * (shakeAmplitude * envelope);

            shakeCameraTransform.localPosition = baseLocalPosition + offset;
            shakeRemaining -= Time.unscaledDeltaTime;
            if (shakeRemaining <= 0f)
            {
                RestoreCameraPose();
            }
        }

        private void UpdateEdgeFlash()
        {
            if (flashRemaining <= 0f)
            {
                SetEdgeAlpha(0f);
                return;
            }

            flashRemaining -= Time.unscaledDeltaTime;
            float ratio = edgeFlashDuration <= 0f ? 0f : Mathf.Clamp01(flashRemaining / edgeFlashDuration);
            float alpha = edgeFlashColor.a * Mathf.Pow(ratio, edgeFadePower);
            SetEdgeAlpha(alpha);
        }

        private void CaptureCameraPose()
        {
            CaptureCameraPose(ResolveCamera());
        }

        private void CaptureCameraPose(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            shakeCameraTransform = camera.transform;
            baseLocalPosition = shakeCameraTransform.localPosition;
            baseLocalRotation = shakeCameraTransform.localRotation;
            hasBaseCameraPose = true;
        }

        private void RestoreCameraPose()
        {
            if (!hasBaseCameraPose || shakeCameraTransform == null)
            {
                return;
            }

            shakeCameraTransform.localPosition = baseLocalPosition;
            shakeCameraTransform.localRotation = baseLocalRotation;
            hasBaseCameraPose = false;
            shakeCameraTransform = null;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                if (useCinemachineImpulse && targetCamera.GetComponent<CinemachineBrain>() == null)
                {
                    Camera cinemachineCamera = ResolveCinemachineBrainCamera();
                    if (cinemachineCamera != null)
                    {
                        targetCamera = cinemachineCamera;
                        return targetCamera;
                    }
                }

                return targetCamera;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                targetCamera = mainCamera;
                return targetCamera;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled && cameras[i].cameraType == CameraType.Game)
                {
                    targetCamera = cameras[i];
                    return targetCamera;
                }
            }

            return null;
        }

        private static Camera ResolveCinemachineBrainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null
                && mainCamera.isActiveAndEnabled
                && mainCamera.GetComponent<CinemachineBrain>() != null)
            {
                return mainCamera;
            }

            CinemachineBrain[] brains = FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < brains.Length; i++)
            {
                if (brains[i] == null || !brains[i].isActiveAndEnabled)
                {
                    continue;
                }

                Camera camera = brains[i].GetComponent<Camera>();
                if (camera != null && camera.isActiveAndEnabled)
                {
                    return camera;
                }
            }

            return null;
        }

        private void EnsureEdgeOverlay()
        {
            if (feedbackCanvas != null && edgeGroup != null && edgeRects != null && edgeRects.Length == 4)
            {
                return;
            }

            GameObject canvasObject = new GameObject("PlayerHitFeedbackCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            feedbackCanvas = canvasObject.GetComponent<Canvas>();
            feedbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            feedbackCanvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            edgeGroup = canvasObject.AddComponent<CanvasGroup>();
            edgeGroup.alpha = 0f;
            edgeGroup.interactable = false;
            edgeGroup.blocksRaycasts = false;

            edgeRects = new RectTransform[4];
            edgeRects[0] = CreateEdgeImage("HitEdgeTop", canvasObject.transform, RectAnchor.Top);
            edgeRects[1] = CreateEdgeImage("HitEdgeBottom", canvasObject.transform, RectAnchor.Bottom);
            edgeRects[2] = CreateEdgeImage("HitEdgeLeft", canvasObject.transform, RectAnchor.Left);
            edgeRects[3] = CreateEdgeImage("HitEdgeRight", canvasObject.transform, RectAnchor.Right);
        }

        private RectTransform CreateEdgeImage(string name, Transform parent, RectAnchor anchor)
        {
            GameObject edgeObject = new GameObject(name, typeof(Image));
            edgeObject.transform.SetParent(parent, false);

            Image image = edgeObject.GetComponent<Image>();
            image.color = edgeFlashColor;
            image.raycastTarget = false;

            RectTransform rect = edgeObject.GetComponent<RectTransform>();
            switch (anchor)
            {
                case RectAnchor.Top:
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(0f, edgeThickness);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case RectAnchor.Bottom:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(0f, edgeThickness);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case RectAnchor.Left:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.sizeDelta = new Vector2(edgeThickness, 0f);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case RectAnchor.Right:
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 0.5f);
                    rect.sizeDelta = new Vector2(edgeThickness, 0f);
                    rect.anchoredPosition = Vector2.zero;
                    break;
            }

            return rect;
        }

        private void SetEdgeAlpha(float alpha)
        {
            if (edgeGroup != null)
            {
                edgeGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private void CacheReferences()
        {
            if (vitals == null)
            {
                vitals = GetComponent<CombatVitals>();
            }
        }

        private enum RectAnchor
        {
            Top,
            Bottom,
            Left,
            Right,
        }
    }
}
