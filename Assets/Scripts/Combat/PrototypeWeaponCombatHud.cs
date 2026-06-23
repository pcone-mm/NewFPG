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
        [SerializeField] private SkillIndicatorTemporaryArtIndex temporaryArtIndex;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Font font;

        [Header("Layout")]
        [SerializeField] private Vector2 resourceBarSize = new Vector2(760f, 30f);
        [SerializeField] private Vector2 healthBarSize = new Vector2(360f, 26f);
        [SerializeField] private Vector2 shieldBarSize = new Vector2(270f, 20f);

        private Canvas canvas;
        private RectTransform root;
        private RectTransform healthFill;
        private RectTransform shieldFill;
        private RectTransform resourceFill;
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
            aimCamera = Camera.main;
        }

        private void Awake()
        {
            ResolveWeaponView();
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

        private void Subscribe()
        {
            ResolveWeaponView();
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
                WeaponDefinition weapon = weaponCaster.GetWeapon(i);
                presentations[i] = new PrototypeFirstPersonWeaponView.WeaponPresentation(
                    weapon != null ? weapon.DisplayName : "Weapon " + (i + 1).ToString(),
                    weapon != null ? weapon.Icon : null);
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
