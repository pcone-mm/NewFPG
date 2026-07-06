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
        [SerializeField] private Vector2 resourceBarSize = new Vector2(760f, 30f);
        [SerializeField] private Vector2 healthBarSize = new Vector2(360f, 26f);
        [SerializeField] private Vector2 shieldBarSize = new Vector2(270f, 20f);
        [SerializeField] private Vector2 dodgeCooldownSize = new Vector2(76f, 76f);
        [SerializeField] private Vector2 dodgeCooldownOffset = new Vector2(-52f, 32f);

        private Canvas canvas;
        private RectTransform root;
        private RectTransform healthFill;
        private RectTransform shieldFill;
        private RectTransform resourceFill;
        private CanvasGroup dodgeCooldownGroup;
        private Image dodgeCooldownBackground;
        private Image dodgeCooldownFill;
        private Image dodgeCooldownCenter;
        private RectTransform dodgeCooldownMarker;
        private Text dodgeCooldownText;
        private Sprite dodgeRingSprite;
        private Sprite dodgeDotSprite;
        private Text healthText;
        private Text shieldText;
        private Text resourceText;
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
            root.sizeDelta = new Vector2(0f, 150f);

            RectTransform healthBar = CreateBar(root, "HealthBar", new Vector2(28f, 92f), healthBarSize, new Color(0.92f, 0.16f, 0.18f, 1f), out healthFill);
            healthBar.anchorMin = new Vector2(0f, 0f);
            healthBar.anchorMax = new Vector2(0f, 0f);
            healthBar.pivot = new Vector2(0f, 0f);
            healthText = CreateText(healthBar, "HealthText", 18, TextAnchor.MiddleCenter, Color.white);
            Stretch(healthText.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform shieldBar = CreateBar(root, "ShieldBar", new Vector2(28f, 62f), shieldBarSize, new Color(0.32f, 0.78f, 1f, 1f), out shieldFill);
            shieldBar.anchorMin = new Vector2(0f, 0f);
            shieldBar.anchorMax = new Vector2(0f, 0f);
            shieldBar.pivot = new Vector2(0f, 0f);
            shieldText = CreateText(shieldBar, "ShieldText", 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(shieldText.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform resourceBar = CreateBar(root, "ResourceBar", new Vector2(0f, 24f), resourceBarSize, new Color(0.08f, 0.5f, 1f, 1f), out resourceFill);
            resourceBar.anchorMin = new Vector2(0.5f, 0f);
            resourceBar.anchorMax = new Vector2(0.5f, 0f);
            resourceBar.pivot = new Vector2(0.5f, 0f);
            resourceText = CreateText(resourceBar, "ResourceText", 18, TextAnchor.MiddleCenter, Color.white);
            Stretch(resourceText.rectTransform, Vector2.zero, Vector2.zero);

            CreateDodgeCooldownWidget(root);

            SetVisible(false);
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
            RefreshBar(resourceFill, resourcePool != null ? resourcePool.Ratio : 0f);

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

            if (resourceText != null)
            {
                resourceText.text = resourcePool != null
                    ? resourcePool.Current.ToString("0.0") + "/" + resourcePool.Max.ToString("0")
                    : "0/0";
                resourceText.color = failedCastFlashRemaining > 0f ? new Color(1f, 0.42f, 0.32f, 1f) : Color.white;
            }

            RefreshDodgeCooldown();
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

        private static Font CreateChineseFont()
        {
            Font createdFont = Font.CreateDynamicFontFromOSFont(
                new[] { "SimHei", "Microsoft YaHei UI", "Microsoft YaHei", "Arial" },
                24);
            return createdFont != null ? createdFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
