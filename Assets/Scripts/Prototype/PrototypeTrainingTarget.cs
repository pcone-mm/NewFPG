using UnityEngine;
using UnityEngine.UI;
using NewFPG.Battle;

namespace NewFPG.Prototype
{
    public sealed class PrototypeTrainingTarget : MonoBehaviour
    {
        [SerializeField] private string enemyId = "training_target";
        [SerializeField] private string displayName = "训练假人";
        [SerializeField] private float maxHp = 180f;
        [SerializeField] private bool startsCharging;
        [SerializeField] private bool interruptible;
        [SerializeField] private bool startsSlowed;
        [SerializeField] private float threatScore = 1f;
        [SerializeField] private RangeBand rangeBand = RangeBand.Near;

        private EnemyCombatState state;
        private Canvas canvas;
        private Slider hpSlider;
        private Slider chargeSlider;
        private Text nameText;
        private Text markerText;
        private Image bodyImage;

        public EnemyCombatState State
        {
            get { return state; }
        }

        public void Initialize(
            string nextId,
            string nextName,
            float nextMaxHp,
            bool charging,
            bool canInterrupt,
            bool slowed,
            float nextThreat,
            RangeBand nextRangeBand)
        {
            enemyId = nextId;
            displayName = nextName;
            maxHp = nextMaxHp;
            startsCharging = charging;
            interruptible = canInterrupt;
            startsSlowed = slowed;
            threatScore = nextThreat;
            rangeBand = nextRangeBand;
            BuildState();
            BuildVisuals();
            RefreshVisuals();
        }

        private void Awake()
        {
            BuildState();
            BuildVisuals();
            RefreshVisuals();
        }

        private void Update()
        {
            if (state == null)
            {
                return;
            }

            state.position = transform.position;
            if (state.isCharging && state.chargeDuration > 0f)
            {
                state.chargeProgress = Mathf.Repeat(Time.time * 0.2f, 1f);
            }

            RefreshVisuals();
        }

        public void SyncFromState()
        {
            if (state == null)
            {
                return;
            }

            state.position = transform.position;
            RefreshVisuals();
        }

        public void SetFocused(bool focused)
        {
            if (state != null)
            {
                state.isHighlighted = focused;
            }

            RefreshVisuals();
        }

        private void BuildState()
        {
            state = new EnemyCombatState
            {
                enemyId = enemyId,
                displayName = displayName,
                hp = maxHp,
                maxHp = maxHp,
                position = transform.position,
                rangeBand = rangeBand,
                state = startsCharging ? EnemyState.Charging : EnemyState.Moving,
                isTargetable = true,
                isCharging = startsCharging,
                isInterruptible = interruptible,
                isSlowed = startsSlowed,
                chargeProgress = startsCharging ? 0.35f : 0f,
                chargeDuration = startsCharging ? 5f : 0f,
                moveSpeed = 0f,
                attackInterval = 0f,
                attackDamage = 0f,
                threatScore = threatScore,
                intent = startsCharging ? "蓄力" : startsSlowed ? "减速" : "待机",
                distanceToPlayer = 0f,
            };
        }

        private void BuildVisuals()
        {
            if (bodyImage != null)
            {
                return;
            }

            GameObject bodyObject = new GameObject("TargetBody", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bodyObject.transform.SetParent(transform, false);
            bodyImage = bodyObject.GetComponent<Image>();
            bodyImage.color = startsCharging
                ? new Color(0.55f, 0.22f, 0.18f, 0.95f)
                : startsSlowed
                    ? new Color(0.18f, 0.34f, 0.55f, 0.95f)
                    : new Color(0.2f, 0.22f, 0.24f, 0.95f);
            RectTransform bodyRect = bodyImage.rectTransform;
            bodyRect.sizeDelta = new Vector2(90f, 130f);

            GameObject canvasObject = new GameObject("TargetHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(220f, 96f);

            Font font = CreateChineseFont();
            nameText = CreateText(canvas.transform, "Name", font, 20, TextAnchor.MiddleCenter);
            SetRect(nameText.rectTransform, new Vector2(0f, 34f), new Vector2(220f, 24f));

            hpSlider = CreateSlider(canvas.transform, "HP", new Vector2(0f, 4f), new Vector2(180f, 16f), new Color(0.72f, 0.14f, 0.12f, 1f));
            chargeSlider = CreateSlider(canvas.transform, "Charge", new Vector2(0f, -20f), new Vector2(180f, 12f), new Color(0.92f, 0.58f, 0.1f, 1f));

            markerText = CreateText(canvas.transform, "Marker", font, 18, TextAnchor.MiddleCenter);
            SetRect(markerText.rectTransform, new Vector2(0f, -42f), new Vector2(220f, 22f));
        }

        private void RefreshVisuals()
        {
            if (state == null || bodyImage == null)
            {
                return;
            }

            bodyImage.enabled = !state.isDead;
            bodyImage.color = state.isDead
                ? new Color(0.08f, 0.08f, 0.08f, 0.5f)
                : state.isHighlighted
                    ? new Color(0.82f, 0.58f, 0.16f, 0.98f)
                    : state.isCharging
                        ? new Color(0.55f, 0.22f, 0.18f, 0.95f)
                        : state.isSlowed
                            ? new Color(0.18f, 0.34f, 0.55f, 0.95f)
                            : new Color(0.2f, 0.22f, 0.24f, 0.95f);

            if (nameText != null)
            {
                nameText.text = state.displayName;
            }

            if (hpSlider != null)
            {
                hpSlider.value = state.HpRatio;
            }

            if (chargeSlider != null)
            {
                chargeSlider.gameObject.SetActive(state.isCharging);
                chargeSlider.value = Mathf.Clamp01(state.chargeProgress);
            }

            if (markerText != null)
            {
                string marker = state.isHighlighted ? "集火 " : string.Empty;
                if (state.isCharging && state.isInterruptible)
                {
                    marker += "可打断";
                }
                else if (!string.IsNullOrWhiteSpace(state.intent))
                {
                    marker += state.intent;
                }

                markerText.text = marker;
            }
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 position, Vector2 size, Color fillColor)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), position, size);

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(root.transform, false);
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.05f, 0.045f, 0.04f, 0.9f);
            Stretch(background.GetComponent<RectTransform>());

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>());

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = fillColor;
            Stretch(fill.GetComponent<RectTransform>());

            Slider slider = root.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = fillImage;
            return slider;
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.96f, 0.9f, 0.72f, 1f);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font CreateChineseFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "SimHei", "Microsoft YaHei UI", "Microsoft YaHei", "Arial" },
                24);
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
