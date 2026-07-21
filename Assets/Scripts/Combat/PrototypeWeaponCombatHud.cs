using System.Collections.Generic;
using NewFPG.Prototype;
using NewFPG.Combat.SkillIndicators;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeFirstPersonWeaponView))]
    public sealed class PrototypeWeaponCombatHud : MonoBehaviour
    {
#if UNITY_EDITOR
        private const string HudBackdropSpritePath = "Assets/Art/Weapons/HUD/2d_di.png";
        private const string HudResourceBaseSpritePath = "Assets/Art/Weapons/HUD/2d_dou.png";
#endif

        [SerializeField] private PrototypeFirstPersonWeaponView weaponView;
        [SerializeField] private CombatVitals vitals;
        [SerializeField] private CombatResourcePool resourcePool;
        [SerializeField] private PlayerWeaponCaster weaponCaster;
        [SerializeField] private AbilityInputController abilityInputController;
        [SerializeField] private CombatDodgePresentationController dodgePresentation;
        [SerializeField] private SkillIndicatorTemporaryArtIndex temporaryArtIndex;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Font font;

        [Header("Layout")]
        [SerializeField] private Vector2 healthBarSize = new Vector2(300f, 20f);
        [SerializeField] private Vector2 shieldBarSize = new Vector2(230f, 16f);
        [SerializeField] private Vector2 dodgeCooldownSize = new Vector2(54f, 54f);
        [SerializeField] private Vector2 dodgeCooldownOffset = new Vector2(-82f, 52f);
        [SerializeField] private Vector2 hudBackdropSize = new Vector2(1320f, 260f);
        [SerializeField] private Vector2 hudBackdropOffset = new Vector2(0f, 0f);
        [SerializeField] private Vector2 resourceBaseSize = new Vector2(760f, 136f);
        [SerializeField] private Vector2 resourceBaseOffset = new Vector2(0f, -10f);
        [SerializeField, Min(1)] private int maxResourcePips = 10;
        [SerializeField] private Vector2 resourcePipArcSize = new Vector2(650f, 58f);
        [SerializeField] private Vector2 resourcePipArcOffset = new Vector2(-4f, 30f);
        [SerializeField] private Vector2 resourcePipSize = new Vector2(46f, 12f);

        [Header("Art")]
        [SerializeField] private Sprite hudBackdropSprite;
        [SerializeField] private Sprite resourceBaseSprite;

        private Canvas canvas;
        private RectTransform root;
        private RectTransform healthFill;
        private RectTransform shieldFill;
        private RectTransform resourcePipRoot;
        private Image hudBackdropImage;
        private Image resourceBaseImage;
        private CanvasGroup dodgeCooldownGroup;
        private Image dodgeCooldownBackground;
        private Image dodgeCooldownFill;
        private Image dodgeCooldownCenter;
        private RectTransform dodgeCooldownMarker;
        private Text dodgeCooldownText;
        private Sprite dodgeRingSprite;
        private Sprite dodgeDotSprite;
        private Sprite resourcePipSprite;
        private Text healthText;
        private Text shieldText;
        private readonly List<Image> resourcePips = new List<Image>();
        private bool visible;
        private bool interceptAttacks;
        private float failedCastFlashRemaining;

        private void Reset()
        {
            weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
            abilityInputController = GetComponent<AbilityInputController>();
            dodgePresentation = GetComponent<CombatDodgePresentationController>();
            aimCamera = Camera.main;
        }

        private void Awake()
        {
            ResolveWeaponView();
            ResolveDodgePresentation();
            Initialize();
        }

        private void OnValidate()
        {
            maxResourcePips = Mathf.Max(1, maxResourcePips);
            resourcePipSize.x = Mathf.Max(1f, resourcePipSize.x);
            resourcePipSize.y = Mathf.Max(1f, resourcePipSize.y);
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnsubscribeCombat();
        }

        private void Update()
        {
            if (!visible)
            {
                return;
            }

            if (failedCastFlashRemaining > 0f)
            {
                failedCastFlashRemaining -= Time.deltaTime;
            }

            Refresh();
        }

        public void Bind(CombatVitals nextVitals, CombatResourcePool nextResourcePool, PlayerWeaponCaster nextCaster)
        {
            Initialize();
            UnsubscribeCombat();
            vitals = nextVitals;
            resourcePool = nextResourcePool;
            weaponCaster = nextCaster;
            ConfigureCasterOriginOverride();
            SubscribeCombat();
            ConfigureWeaponViewPresentations();
            ConfigureAbilityInputController();
            ResolveDodgePresentation();
            Refresh();
        }

        public void SetVisible(bool nextVisible)
        {
            Initialize();
            visible = nextVisible;
            if (root != null)
            {
                root.gameObject.SetActive(nextVisible);
            }

            if (nextVisible)
            {
                Refresh();
            }
        }

        public void SetCombatEnabled(bool enabled)
        {
            interceptAttacks = enabled;
            if (weaponCaster != null)
            {
                weaponCaster.SetCombatEnabled(enabled);
                ConfigureCasterOriginOverride();
            }

            ConfigureAbilityInputController();
            if (abilityInputController != null)
            {
                abilityInputController.SetInputEnabled(enabled);
            }

            SetVisible(enabled);
        }

        public void SetAimCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            aimCamera = camera;
            ConfigureAbilityInputController();
        }

        private void FlashFailedCast()
        {
            failedCastFlashRemaining = 0.18f;
            Refresh();
        }

        private void Initialize()
        {
            if (canvas != null)
            {
                return;
            }

            font = font != null ? font : CreateChineseFont();
            ResolveDefaultHudSprites();

            GameObject canvasObject = new GameObject("PrototypeWeaponCombatCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject rootObject = new GameObject("CombatBars", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0f, 300f);

            CreateBottomHudArt(root);

            RectTransform healthBar = CreateBar(root, "HealthBar", new Vector2(30f, 188f), healthBarSize, new Color(0.92f, 0.16f, 0.18f, 1f), out healthFill);
            healthBar.anchorMin = new Vector2(0f, 0f);
            healthBar.anchorMax = new Vector2(0f, 0f);
            healthBar.pivot = new Vector2(0f, 0f);
            healthText = CreateText(healthBar, "HealthText", 18, TextAnchor.MiddleCenter, Color.white);
            Stretch(healthText.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform shieldBar = CreateBar(root, "ShieldBar", new Vector2(30f, 162f), shieldBarSize, new Color(0.32f, 0.78f, 1f, 1f), out shieldFill);
            shieldBar.anchorMin = new Vector2(0f, 0f);
            shieldBar.anchorMax = new Vector2(0f, 0f);
            shieldBar.pivot = new Vector2(0f, 0f);
            shieldText = CreateText(shieldBar, "ShieldText", 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(shieldText.rectTransform, Vector2.zero, Vector2.zero);

            CreateResourcePipHud(root);

            CreateDodgeCooldownWidget(root);

            SetVisible(false);
        }

        private void ResolveDefaultHudSprites()
        {
#if UNITY_EDITOR
            if (hudBackdropSprite == null)
            {
                hudBackdropSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudBackdropSpritePath);
            }

            if (resourceBaseSprite == null)
            {
                resourceBaseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudResourceBaseSpritePath);
            }
#endif
        }

        private void CreateBottomHudArt(RectTransform parent)
        {
            if (hudBackdropSprite != null)
            {
                hudBackdropImage = CreateHudImage(parent, "HudBottomBackdrop", hudBackdropSprite, Color.white);
                hudBackdropImage.preserveAspect = false;
                SetBottomCenterRect(hudBackdropImage.rectTransform, hudBackdropSize, hudBackdropOffset);
            }

            if (resourceBaseSprite != null)
            {
                resourceBaseImage = CreateHudImage(parent, "HudResourceBase", resourceBaseSprite, Color.white);
                resourceBaseImage.preserveAspect = false;
                SetBottomCenterRect(resourceBaseImage.rectTransform, resourceBaseSize, resourceBaseOffset);
            }
        }

        private void CreateResourcePipHud(RectTransform parent)
        {
            resourcePipSprite = CreateCapsuleSprite(96, 28);

            GameObject pipsObject = new GameObject("ResourcePips", typeof(RectTransform));
            pipsObject.transform.SetParent(parent, false);
            resourcePipRoot = pipsObject.GetComponent<RectTransform>();
            resourcePipRoot.anchorMin = new Vector2(0.5f, 0f);
            resourcePipRoot.anchorMax = new Vector2(0.5f, 0f);
            resourcePipRoot.pivot = new Vector2(0.5f, 0f);
            resourcePipRoot.anchoredPosition = resourcePipArcOffset;
            resourcePipRoot.sizeDelta = resourcePipArcSize;

            RebuildResourcePips();
        }

        private void RebuildResourcePips()
        {
            if (resourcePipRoot == null)
            {
                return;
            }

            ClearChildren(resourcePipRoot);
            resourcePips.Clear();

            int capacity = ResolveResourcePipCapacity();
            for (int i = 0; i < capacity; i++)
            {
                Image pip = CreateHudImage(resourcePipRoot, "ResourcePip_" + i.ToString(), resourcePipSprite, Color.white);
                pip.type = Image.Type.Sliced;
                RectTransform pipRect = pip.rectTransform;
                pipRect.anchorMin = new Vector2(0.5f, 0f);
                pipRect.anchorMax = new Vector2(0.5f, 0f);
                pipRect.pivot = new Vector2(0.5f, 0.5f);
                pipRect.sizeDelta = resourcePipSize;
                pipRect.anchoredPosition = ResourcePipPosition(i, capacity);
                pipRect.localEulerAngles = new Vector3(0f, 0f, ResourcePipAngle(i, capacity));
                resourcePips.Add(pip);
            }
        }

        private RectTransform CreateBar(RectTransform parent, string name, Vector2 position, Vector2 size, Color fillColor, out RectTransform fill)
        {
            GameObject barObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barObject.transform.SetParent(parent, false);
            Image background = barObject.GetComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color(0f, 0f, 0f, 0.52f);

            RectTransform rect = background.rectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(rect, false);
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.raycastTarget = false;
            fillImage.color = fillColor;

            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            return rect;
        }

        private Text CreateText(Transform parent, string name, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.raycastTarget = false;
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void Refresh()
        {
            RefreshBar(healthFill, vitals != null ? vitals.HealthRatio : 0f);
            RefreshBar(shieldFill, vitals != null ? vitals.ShieldRatio : 0f);
            RefreshResourcePips();

            if (healthText != null)
            {
                healthText.text = vitals != null
                    ? vitals.CurrentHealth.ToString("0") + "/" + vitals.MaxHealth.ToString("0")
                    : "0/0";
            }

            if (shieldText != null)
            {
                shieldText.text = vitals != null ? "Shield " + vitals.CurrentShield.ToString("0") : "Shield 0";
            }

            RefreshDodgeCooldown();
        }

        private void RefreshResourcePips()
        {
            if (resourcePipRoot == null)
            {
                return;
            }

            int capacity = ResolveResourcePipCapacity();
            if (resourcePips.Count != capacity)
            {
                RebuildResourcePips();
            }

            int activeCount = resourcePool != null
                ? ResolveActiveResourcePipCount(resourcePool.Ratio, capacity)
                : 0;
            Color pipColor = failedCastFlashRemaining > 0f
                ? new Color(1f, 0.42f, 0.32f, 1f)
                : Color.white;

            for (int i = 0; i < resourcePips.Count; i++)
            {
                Image pip = resourcePips[i];
                if (pip == null)
                {
                    continue;
                }

                bool active = i < activeCount;
                pip.gameObject.SetActive(active);
                pip.color = pipColor;
            }
        }

        private int ResolveResourcePipCapacity()
        {
            int configuredCapacity = Mathf.Max(1, maxResourcePips);
            if (resourcePool == null)
            {
                return configuredCapacity;
            }

            return Mathf.Clamp(Mathf.CeilToInt(resourcePool.Max), 1, configuredCapacity);
        }

        private static int ResolveActiveResourcePipCount(float resourceRatio, int capacity)
        {
            int resolvedCapacity = Mathf.Max(1, capacity);
            float normalizedRatio = Mathf.Clamp01(resourceRatio);
            return Mathf.Clamp(
                Mathf.FloorToInt(normalizedRatio * resolvedCapacity + 0.0001f),
                0,
                resolvedCapacity);
        }

        private Vector2 ResourcePipPosition(int index, int capacity)
        {
            if (capacity <= 1)
            {
                return new Vector2(0f, resourcePipArcSize.y);
            }

            float t = index / (capacity - 1f);
            float x = Mathf.Lerp(-resourcePipArcSize.x * 0.5f, resourcePipArcSize.x * 0.5f, t);
            float y = Mathf.Sin(t * Mathf.PI) * resourcePipArcSize.y;
            return new Vector2(x, y);
        }

        private float ResourcePipAngle(int index, int capacity)
        {
            if (capacity <= 1)
            {
                return 0f;
            }

            float t = index / (capacity - 1f);
            float slope = Mathf.PI * resourcePipArcSize.y * Mathf.Cos(t * Mathf.PI) / Mathf.Max(1f, resourcePipArcSize.x);
            return Mathf.Atan(slope) * Mathf.Rad2Deg;
        }

        private static void RefreshBar(RectTransform fill, float ratio)
        {
            if (fill == null)
            {
                return;
            }

            Vector2 anchorMax = fill.anchorMax;
            anchorMax.x = Mathf.Clamp01(ratio);
            fill.anchorMax = anchorMax;
        }

        private void CreateDodgeCooldownWidget(RectTransform parent)
        {
            dodgeRingSprite = CreateCircleSprite(96, 14f, true);
            dodgeDotSprite = CreateCircleSprite(32, 0f, false);

            GameObject rootObject = new GameObject("DodgeCooldown", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(parent, false);
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = dodgeCooldownOffset;
            rect.sizeDelta = dodgeCooldownSize;

            dodgeCooldownGroup = rootObject.GetComponent<CanvasGroup>();
            dodgeCooldownGroup.alpha = 1f;
            dodgeCooldownGroup.interactable = false;
            dodgeCooldownGroup.blocksRaycasts = false;

            dodgeCooldownBackground = CreateHudImage(rect, "RingBackground", dodgeRingSprite, new Color(0f, 0f, 0f, 0.48f));
            Stretch(dodgeCooldownBackground.rectTransform, Vector2.zero, Vector2.zero);

            dodgeCooldownFill = CreateHudImage(rect, "RingFill", dodgeRingSprite, new Color(0.42f, 0.86f, 1f, 1f));
            dodgeCooldownFill.type = Image.Type.Filled;
            dodgeCooldownFill.fillMethod = Image.FillMethod.Radial360;
            dodgeCooldownFill.fillOrigin = (int)Image.Origin360.Top;
            dodgeCooldownFill.fillClockwise = true;
            dodgeCooldownFill.fillAmount = 1f;
            Stretch(dodgeCooldownFill.rectTransform, Vector2.zero, Vector2.zero);

            dodgeCooldownCenter = CreateHudImage(rect, "ReadyGlow", dodgeDotSprite, new Color(0.42f, 0.86f, 1f, 0.12f));
            RectTransform centerRect = dodgeCooldownCenter.rectTransform;
            centerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.pivot = new Vector2(0.5f, 0.5f);
            centerRect.anchoredPosition = Vector2.zero;
            centerRect.sizeDelta = dodgeCooldownSize * 0.62f;

            GameObject markerObject = new GameObject("CooldownMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerObject.transform.SetParent(rect, false);
            Image markerImage = markerObject.GetComponent<Image>();
            markerImage.raycastTarget = false;
            markerImage.sprite = dodgeDotSprite;
            markerImage.color = new Color(1f, 0.86f, 0.4f, 1f);
            dodgeCooldownMarker = markerImage.rectTransform;
            dodgeCooldownMarker.anchorMin = new Vector2(0.5f, 0.5f);
            dodgeCooldownMarker.anchorMax = new Vector2(0.5f, 0.5f);
            dodgeCooldownMarker.pivot = new Vector2(0.5f, 0.5f);
            dodgeCooldownMarker.sizeDelta = new Vector2(12f, 12f);

            dodgeCooldownText = CreateText(rect, "DodgeText", 22, TextAnchor.MiddleCenter, Color.white);
            dodgeCooldownText.text = "闪";
            dodgeCooldownText.fontStyle = FontStyle.Bold;
            Stretch(dodgeCooldownText.rectTransform, Vector2.zero, Vector2.zero);

            RefreshDodgeCooldown();
        }

        private Image CreateHudImage(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private void RefreshDodgeCooldown()
        {
            if (dodgeCooldownGroup == null)
            {
                return;
            }

            ResolveDodgePresentation();
            bool hasDodge = dodgePresentation != null;
            dodgeCooldownGroup.alpha = hasDodge ? 1f : 0f;
            if (!hasDodge)
            {
                return;
            }

            float progress = dodgePresentation.DodgeCooldownProgress;
            bool ready = dodgePresentation.IsDodgeReady;
            Color readyColor = new Color(0.42f, 0.86f, 1f, 1f);
            Color coolingColor = new Color(1f, 0.74f, 0.3f, 1f);

            if (dodgeCooldownFill != null)
            {
                dodgeCooldownFill.fillAmount = ready ? 1f : progress;
                dodgeCooldownFill.color = ready ? readyColor : coolingColor;
            }

            if (dodgeCooldownBackground != null)
            {
                dodgeCooldownBackground.color = ready
                    ? new Color(0.42f, 0.86f, 1f, 0.28f)
                    : new Color(0f, 0f, 0f, 0.48f);
            }

            if (dodgeCooldownCenter != null)
            {
                dodgeCooldownCenter.color = ready
                    ? new Color(0.42f, 0.86f, 1f, 0.16f)
                    : new Color(1f, 0.74f, 0.3f, 0.08f);
            }

            if (dodgeCooldownText != null)
            {
                dodgeCooldownText.color = ready ? Color.white : new Color(1f, 0.86f, 0.56f, 1f);
            }

            UpdateDodgeCooldownMarker(progress, ready);
        }

        private void UpdateDodgeCooldownMarker(float progress, bool ready)
        {
            if (dodgeCooldownMarker == null)
            {
                return;
            }

            dodgeCooldownMarker.gameObject.SetActive(!ready);
            if (ready)
            {
                return;
            }

            float radius = Mathf.Max(8f, Mathf.Min(dodgeCooldownSize.x, dodgeCooldownSize.y) * 0.5f - 7f);
            float angle = Mathf.Lerp(90f, -270f, Mathf.Clamp01(progress)) * Mathf.Deg2Rad;
            dodgeCooldownMarker.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Sprite CreateCircleSprite(int size, float ringThickness, bool ringOnly)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;

            float center = (size - 1) * 0.5f;
            float outerRadius = size * 0.5f - 1f;
            float innerRadius = ringOnly ? Mathf.Max(0f, outerRadius - ringThickness) : 0f;
            Color clear = Color.clear;
            Color white = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01(outerRadius - distance + 1f);
                    float innerAlpha = ringOnly ? Mathf.Clamp01(distance - innerRadius + 1f) : 1f;
                    float alpha = Mathf.Clamp01(outerAlpha * innerAlpha);
                    texture.SetPixel(x, y, alpha > 0.001f ? new Color(white.r, white.g, white.b, alpha) : clear);
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateCapsuleSprite(int width, int height)
        {
            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;

            float radius = (height - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float leftCenter = radius;
            float rightCenter = width - 1 - radius;
            Color clear = Color.clear;
            Color white = Color.white;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float capsuleX = Mathf.Clamp(x, leftCenter, rightCenter);
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(capsuleX, centerY));
                    float alpha = Mathf.Clamp01(radius - distance + 1f);
                    texture.SetPixel(x, y, alpha > 0.001f ? new Color(white.r, white.g, white.b, alpha) : clear);
                }
            }

            texture.Apply();
            Vector4 border = new Vector4(radius, radius, radius, radius);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                height,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void Subscribe()
        {
            ResolveWeaponView();
            ResolveDodgePresentation();
            ResolveAbilityInputController();
            ConfigureAbilityInputController();
        }

        private void Unsubscribe()
        {
            if (abilityInputController != null)
            {
                abilityInputController.SetInputEnabled(false);
            }
        }

        private void SubscribeCombat()
        {
            if (vitals != null)
            {
                vitals.Changed -= OnVitalsChanged;
                vitals.Changed += OnVitalsChanged;
            }

            if (resourcePool != null)
            {
                resourcePool.Changed -= OnResourceChanged;
                resourcePool.Changed += OnResourceChanged;
            }
        }

        private void UnsubscribeCombat()
        {
            if (vitals != null)
            {
                vitals.Changed -= OnVitalsChanged;
            }

            if (resourcePool != null)
            {
                resourcePool.Changed -= OnResourceChanged;
            }
        }

        private void OnVitalsChanged(CombatVitals changedVitals)
        {
            Refresh();
        }

        private void OnResourceChanged(CombatResourcePool changedResource)
        {
            Refresh();
        }

        private void ResolveWeaponView()
        {
            if (weaponView == null)
            {
                weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
            }
        }

        private void ResolveAbilityInputController()
        {
            if (abilityInputController == null)
            {
                abilityInputController = GetComponent<AbilityInputController>();
                if (abilityInputController == null)
                {
                    abilityInputController = gameObject.AddComponent<AbilityInputController>();
                }
            }
        }

        private void ResolveDodgePresentation()
        {
            if (dodgePresentation == null)
            {
                dodgePresentation = GetComponent<CombatDodgePresentationController>();
            }
        }

        private void ResolveTemporaryArtIndex()
        {
            if (temporaryArtIndex != null)
            {
                return;
            }

#if UNITY_EDITOR
            temporaryArtIndex = AssetDatabase.LoadAssetAtPath<SkillIndicatorTemporaryArtIndex>(
                "Assets/Art/SkillIndicators/Temporary/SO_IND_TemporaryArtIndex.asset");
#endif
        }

        private void ConfigureAbilityInputController()
        {
            ResolveWeaponView();
            ResolveAbilityInputController();
            if (abilityInputController == null)
            {
                return;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            ResolveTemporaryArtIndex();
            abilityInputController.Bind(weaponView, weaponCaster, aimCamera, temporaryArtIndex);
            abilityInputController.SetInputEnabled(interceptAttacks);
        }

        private void ConfigureCasterOriginOverride()
        {
            if (weaponCaster == null)
            {
                return;
            }

            ResolveWeaponView();
            weaponCaster.SetRuntimeCastOriginOverride(
                interceptAttacks && weaponView != null ? weaponView.transform : null);
        }

        private void ConfigureWeaponViewPresentations()
        {
            ResolveWeaponView();
            if (weaponView == null)
            {
                return;
            }

            if (weaponCaster == null || weaponCaster.WeaponCount <= 0)
            {
                weaponView.SetWeaponPresentations(null);
                return;
            }

            var presentations = new PrototypeFirstPersonWeaponView.WeaponPresentation[weaponCaster.WeaponCount];
            for (int i = 0; i < presentations.Length; i++)
            {
                WeaponRuntimeStats stats = weaponCaster.GetRuntimeStats(i);
                WeaponDefinition weapon = stats != null ? stats.Definition : weaponCaster.GetWeapon(i);
                presentations[i] = new PrototypeFirstPersonWeaponView.WeaponPresentation(
                    stats != null ? stats.DisplayName : weapon != null ? weapon.DisplayName : "Weapon " + (i + 1).ToString(),
                    stats != null ? stats.Icon : weapon != null ? weapon.Icon : null);
            }

            weaponView.SetWeaponPresentations(presentations);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetBottomCenterRect(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private static Font CreateChineseFont()
        {
            Font createdFont = Font.CreateDynamicFontFromOSFont(
                new[] { "SimHei", "Microsoft YaHei UI", "Microsoft YaHei", "Arial" },
                24);
            return createdFont != null ? createdFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
