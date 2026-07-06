using System.Collections.Generic;
using NewFPG.Level;
using NewFPG.Monsters;
using UnityEngine;
using UnityEngine.UI;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class MonsterCombatHud : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Font font;
        [SerializeField, Min(0)] private int sortingOrder = 85;
        [SerializeField] private Vector2 smallHealthBarSize = new Vector2(124f, 14f);
        [SerializeField] private Vector2 bossHealthBarSize = new Vector2(720f, 72f);
        [SerializeField] private Vector3 smallHealthBarWorldOffset = new Vector3(0f, 0.34f, 0f);
        [SerializeField] private Color healthFillColor = new Color(0.86f, 0.12f, 0.13f, 1f);
        [SerializeField] private Color bossHealthFillColor = new Color(0.95f, 0.19f, 0.13f, 1f);
        [SerializeField] private Color shieldFillColor = new Color(0.28f, 0.75f, 1f, 0.7f);
        [SerializeField] private Color damageTextColor = new Color(1f, 0.86f, 0.36f, 1f);
        [SerializeField, Min(0.05f)] private float damageNumberLifetime = 0.85f;
        [SerializeField] private HitTipCatalog hitTipCatalog;

        private readonly Dictionary<LevelCombatant, TrackedCombatant> trackedCombatants = new Dictionary<LevelCombatant, TrackedCombatant>();
        private readonly List<LevelCombatant> removeBuffer = new List<LevelCombatant>();
        private readonly List<MonsterHealthBarView> smallHealthBarPool = new List<MonsterHealthBarView>();
        private readonly List<DamageNumberView> damageNumberPool = new List<DamageNumberView>();
        private readonly List<DamageNumberView> activeDamageNumbers = new List<DamageNumberView>();

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform smallBarsRoot;
        private RectTransform bossRoot;
        private RectTransform damageNumbersRoot;
        private MonsterHealthBarView bossHealthBar;

        public int TrackedCount => trackedCombatants.Count;
        public int ActiveDamageNumberCount => activeDamageNumbers.Count;
        public bool BossHealthBarVisible => bossHealthBar != null && bossHealthBar.gameObject.activeSelf;

        private void Awake()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            Initialize();
            Camera camera = ResolveCamera();
            float deltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f)
            {
                deltaTime = 1f / 60f;
            }

            removeBuffer.Clear();
            foreach (KeyValuePair<LevelCombatant, TrackedCombatant> pair in trackedCombatants)
            {
                TrackedCombatant tracked = pair.Value;
                if (pair.Key == null || tracked.Vitals == null || tracked.Target == null)
                {
                    removeBuffer.Add(pair.Key);
                    continue;
                }

                tracked.View.SetTargetCamera(camera);
                tracked.View.Refresh();
                tracked.View.UpdatePosition(camera, canvasRect);
            }

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                Untrack(removeBuffer[i]);
            }

            for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
            {
                DamageNumberView view = activeDamageNumbers[i];
                if (view == null || !view.Tick(deltaTime))
                {
                    activeDamageNumbers.RemoveAt(i);
                    if (view != null && !damageNumberPool.Contains(view))
                    {
                        damageNumberPool.Add(view);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
            foreach (KeyValuePair<LevelCombatant, TrackedCombatant> pair in trackedCombatants)
            {
                pair.Value.View.SetTargetCamera(camera);
            }
        }

        public void SetHitTipCatalog(HitTipCatalog catalog)
        {
            hitTipCatalog = catalog;
        }

        public void Track(LevelCombatant combatant, bool isBoss, string displayName)
        {
            Initialize();
            if (combatant == null)
            {
                return;
            }

            CombatVitals vitals = combatant.GetComponent<CombatVitals>();
            if (vitals == null)
            {
                return;
            }

            Untrack(combatant);
            if (isBoss)
            {
                LevelCombatant currentBoss = FindCurrentBoss();
                if (currentBoss != null && currentBoss != combatant)
                {
                    Untrack(currentBoss);
                }
            }

            MonsterHealthBarView view = isBoss ? EnsureBossHealthBar() : RentSmallHealthBar();
            string resolvedName = ResolveDisplayName(combatant, displayName);
            view.Bind(vitals, combatant.transform, ResolveCamera(), isBoss, resolvedName);
            view.Refresh();

            trackedCombatants[combatant] = new TrackedCombatant(combatant, vitals, view, isBoss);
            vitals.Changed += OnVitalsChanged;
            vitals.Damaged += OnVitalsDamaged;
            vitals.Died += OnVitalsDied;
        }

        public void Untrack(LevelCombatant combatant)
        {
            if (ReferenceEquals(combatant, null) || !trackedCombatants.TryGetValue(combatant, out TrackedCombatant tracked))
            {
                return;
            }

            if (tracked.Vitals != null)
            {
                tracked.Vitals.Changed -= OnVitalsChanged;
                tracked.Vitals.Damaged -= OnVitalsDamaged;
                tracked.Vitals.Died -= OnVitalsDied;
            }

            trackedCombatants.Remove(combatant);

            if (tracked.IsBoss)
            {
                if (tracked.View != null)
                {
                    tracked.View.Unbind();
                }
            }
            else
            {
                ReleaseSmallHealthBar(tracked.View);
            }
        }

        public void Clear()
        {
            removeBuffer.Clear();
            foreach (KeyValuePair<LevelCombatant, TrackedCombatant> pair in trackedCombatants)
            {
                removeBuffer.Add(pair.Key);
            }

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                Untrack(removeBuffer[i]);
            }

            for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
            {
                if (activeDamageNumbers[i] != null)
                {
                    activeDamageNumbers[i].Stop();
                    if (!damageNumberPool.Contains(activeDamageNumbers[i]))
                    {
                        damageNumberPool.Add(activeDamageNumbers[i]);
                    }
                }
            }

            activeDamageNumbers.Clear();
        }

        public void ShowHitTip(float amount, Vector3 worldPosition, HitTipStyleId style = HitTipStyleId.Normal)
        {
            ShowHitTip(new HitTipRequest(amount, worldPosition, style));
        }

        public void ShowHitTip(HitTipRequest request)
        {
            Initialize();
            if (request.Amount <= 0f)
            {
                return;
            }

            Vector2 screenPosition = request.HasScreenPosition
                ? request.ScreenPosition
                : ResolveHitTipScreenPosition(request.WorldPosition);

            HitTipCatalog catalog = ResolveHitTipCatalog();
            HitTipStyleConfig style = catalog != null ? catalog.GetStyle(request.StyleId) : null;
            HitTipAnimationConfig animation = catalog != null ? catalog.GetAnimation(style, request.AnimationOverride) : request.AnimationOverride;
            if (style == null || !style.IsValid)
            {
                SpawnDamageNumber(request.Amount.ToString("0"), screenPosition, request.HasColorOverride ? request.ColorOverride : damageTextColor);
                return;
            }

            DamageNumberView view = RentDamageNumber();
            view.Play(request, screenPosition, style, animation);
            if (!activeDamageNumbers.Contains(view))
            {
                activeDamageNumbers.Add(view);
            }
        }

        private void OnVitalsChanged(CombatVitals changedVitals)
        {
            TrackedCombatant tracked = FindTracked(changedVitals);
            if (tracked.View != null)
            {
                tracked.View.Refresh();
            }
        }

        private void OnVitalsDamaged(CombatVitals changedVitals, DamagePayload payload)
        {
            if (payload.Amount <= 0f)
            {
                return;
            }

            TrackedCombatant tracked = FindTracked(changedVitals);
            if (tracked.Vitals == null)
            {
                return;
            }

            Vector3 hitPoint = payload.HitPoint == default
                ? ResolveFallbackHitPoint(tracked.Target)
                : payload.HitPoint;
            ShowHitTip(payload.Amount, hitPoint, HitTipStyleId.Normal);
        }

        private void OnVitalsDied(CombatVitals changedVitals)
        {
            LevelCombatant combatant = FindCombatant(changedVitals);
            if (combatant != null)
            {
                Untrack(combatant);
            }
        }

        private void SpawnDamageNumber(string text, Vector2 screenPosition, Color color)
        {
            DamageNumberView view = RentDamageNumber();
            view.Play(text, screenPosition, color, damageNumberLifetime);
            if (!activeDamageNumbers.Contains(view))
            {
                activeDamageNumbers.Add(view);
            }
        }

        private MonsterHealthBarView RentSmallHealthBar()
        {
            for (int i = 0; i < smallHealthBarPool.Count; i++)
            {
                MonsterHealthBarView pooled = smallHealthBarPool[i];
                if (pooled != null && !pooled.gameObject.activeSelf)
                {
                    return pooled;
                }
            }

            MonsterHealthBarView created = CreateHealthBar(
                "SmallMonsterHealthBar",
                smallBarsRoot,
                smallHealthBarSize,
                healthFillColor,
                false);
            smallHealthBarPool.Add(created);
            return created;
        }

        private void ReleaseSmallHealthBar(MonsterHealthBarView view)
        {
            if (view == null)
            {
                return;
            }

            view.Unbind();
            if (!smallHealthBarPool.Contains(view))
            {
                smallHealthBarPool.Add(view);
            }
        }

        private MonsterHealthBarView EnsureBossHealthBar()
        {
            if (bossHealthBar != null)
            {
                return bossHealthBar;
            }

            bossHealthBar = CreateHealthBar(
                "BossHealthBar",
                bossRoot,
                bossHealthBarSize,
                bossHealthFillColor,
                true);

            RectTransform rect = bossHealthBar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -32f);
            return bossHealthBar;
        }

        private DamageNumberView RentDamageNumber()
        {
            for (int i = damageNumberPool.Count - 1; i >= 0; i--)
            {
                DamageNumberView pooled = damageNumberPool[i];
                damageNumberPool.RemoveAt(i);
                if (pooled != null)
                {
                    return pooled;
                }
            }

            return CreateDamageNumber();
        }

        private void Initialize()
        {
            if (canvas != null)
            {
                return;
            }

            font = font != null ? font : CreateChineseFont();

            GameObject canvasObject = new GameObject("MonsterCombatHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder <= 80 ? 90 : sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            bossRoot = CreateRoot("BossBars", canvasObject.transform);
            smallBarsRoot = CreateRoot("SmallBars", canvasObject.transform);
            damageNumbersRoot = CreateRoot("DamageNumbers", canvasObject.transform);
        }

        private RectTransform CreateRoot(string name, Transform parent)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        private MonsterHealthBarView CreateHealthBar(
            string objectName,
            Transform parent,
            Vector2 size,
            Color fillColor,
            bool isBoss)
        {
            GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasGroup));
            barObject.transform.SetParent(parent, false);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.sizeDelta = size;
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);

            Text name = null;
            Text value = null;
            RectTransform meterParent = barRect;

            if (isBoss)
            {
                name = CreateText(barRect, "Name", 24, TextAnchor.MiddleLeft, new Color(1f, 0.94f, 0.76f, 1f));
                SetStretch(name.rectTransform, new Vector2(0f, 38f), new Vector2(-220f, 0f));

                value = CreateText(barRect, "Value", 20, TextAnchor.MiddleRight, Color.white);
                SetStretch(value.rectTransform, new Vector2(220f, 38f), new Vector2(0f, 0f));

                GameObject meterObject = new GameObject("Meter", typeof(RectTransform));
                meterObject.transform.SetParent(barRect, false);
                meterParent = meterObject.GetComponent<RectTransform>();
                SetStretch(meterParent, new Vector2(0f, 0f), new Vector2(0f, -38f));
            }

            Image background = CreateImage(meterParent, "Background", new Color(0f, 0f, 0f, 0.64f));
            SetStretch(background.rectTransform, Vector2.zero, Vector2.zero);

            Image shield = CreateImage(meterParent, "ShieldFill", shieldFillColor);
            SetStretch(shield.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            Image fill = CreateImage(meterParent, "HealthFill", fillColor);
            SetStretch(fill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            Image rim = CreateImage(meterParent, "Rim", new Color(1f, 1f, 1f, isBoss ? 0.2f : 0.15f));
            rim.raycastTarget = false;
            SetStretch(rim.rectTransform, Vector2.zero, Vector2.zero);

            CanvasGroup group = barObject.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            MonsterHealthBarView view = barObject.AddComponent<MonsterHealthBarView>();
            view.Initialize(fill.rectTransform, shield.rectTransform, name, value, group, smallHealthBarWorldOffset);
            view.Unbind();
            return view;
        }

        private DamageNumberView CreateDamageNumber()
        {
            GameObject numberObject = new GameObject("DamageNumber", typeof(RectTransform), typeof(CanvasGroup));
            numberObject.transform.SetParent(damageNumbersRoot, false);
            RectTransform rect = numberObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 52f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image background = CreateImage(rect, "Background", Color.white);
            background.type = Image.Type.Sliced;
            SetStretch(background.rectTransform, Vector2.zero, Vector2.zero);

            GameObject digitsObject = new GameObject("Digits", typeof(RectTransform));
            digitsObject.transform.SetParent(rect, false);
            RectTransform digits = digitsObject.GetComponent<RectTransform>();
            digits.anchorMin = new Vector2(0.5f, 0.5f);
            digits.anchorMax = new Vector2(0.5f, 0.5f);
            digits.pivot = new Vector2(0.5f, 0.5f);
            digits.anchoredPosition = Vector2.zero;

            Text text = CreateText(rect, "FallbackValue", 32, TextAnchor.MiddleCenter, damageTextColor);
            text.fontStyle = FontStyle.Bold;
            SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);

            CanvasGroup group = numberObject.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            DamageNumberView view = numberObject.AddComponent<DamageNumberView>();
            view.Initialize(background, digits, group, text);
            return view;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private TrackedCombatant FindTracked(CombatVitals vitals)
        {
            foreach (KeyValuePair<LevelCombatant, TrackedCombatant> pair in trackedCombatants)
            {
                if (pair.Value.Vitals == vitals)
                {
                    return pair.Value;
                }
            }

            return default;
        }

        private LevelCombatant FindCombatant(CombatVitals vitals)
        {
            foreach (KeyValuePair<LevelCombatant, TrackedCombatant> pair in trackedCombatants)
            {
                if (pair.Value.Vitals == vitals)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private LevelCombatant FindCurrentBoss()
        {
            foreach (KeyValuePair<LevelCombatant, TrackedCombatant> pair in trackedCombatants)
            {
                if (pair.Value.IsBoss)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
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

        private static Vector3 ResolveFallbackHitPoint(Transform target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + Vector3.up * 0.25f;
            }

            return target.position + Vector3.up * 1.5f;
        }

        private static bool TryWorldToScreenPoint(Camera camera, Vector3 worldPosition, out Vector2 screenPosition)
        {
            if (camera == null)
            {
                screenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return Screen.width > 0 && Screen.height > 0;
            }

            Vector3 projected = camera.WorldToScreenPoint(worldPosition);
            screenPosition = projected;
            return projected.z > camera.nearClipPlane;
        }

        private Vector2 ResolveHitTipScreenPosition(Vector3 worldPosition)
        {
            if (TryWorldToScreenPoint(ResolveCamera(), worldPosition, out Vector2 screenPosition))
            {
                return screenPosition;
            }

            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private HitTipCatalog ResolveHitTipCatalog()
        {
            if (hitTipCatalog != null)
            {
                return hitTipCatalog;
            }

            hitTipCatalog = Resources.Load<HitTipCatalog>("HitTips/SO_HTC_Default");
            return hitTipCatalog;
        }

        private static string ResolveDisplayName(LevelCombatant combatant, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            MonsterConfigBinding binding = combatant != null ? combatant.GetComponent<MonsterConfigBinding>() : null;
            if (binding != null && !string.IsNullOrWhiteSpace(binding.MonsterId))
            {
                return binding.MonsterId;
            }

            return combatant != null ? combatant.name : "Monster";
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

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

        private readonly struct TrackedCombatant
        {
            public TrackedCombatant(LevelCombatant combatant, CombatVitals vitals, MonsterHealthBarView view, bool isBoss)
            {
                Combatant = combatant;
                Vitals = vitals;
                View = view;
                IsBoss = isBoss;
            }

            public readonly LevelCombatant Combatant;
            public readonly CombatVitals Vitals;
            public readonly MonsterHealthBarView View;
            public readonly bool IsBoss;
            public Transform Target => Combatant != null ? Combatant.transform : null;
        }
    }
}
