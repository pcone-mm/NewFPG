using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NewFPG.Battle;

namespace NewFPG.Prototype
{
    public sealed class PrototypeArtifactBattleHud : MonoBehaviour
    {
        private sealed class ArtifactView
        {
            public ArtifactRuntimeState runtime;
            public Image panel;
            public Image cooldownMask;
            public Text nameText;
            public Text cooldownText;
            public Text autoText;
            public Button autoButton;
            public float pulseUntil;
        }

        private Canvas canvas;
        private Text hpText;
        private Text focusText;
        private Text feedbackText;
        private Button totalAutoButton;
        private Text totalAutoText;
        private RectTransform queueRoot;
        private readonly List<ArtifactView> artifactViews = new List<ArtifactView>();
        private Font font;
        private BattleSessionContext context;
        private float feedbackUntil;

        public event Action<bool> TotalAutoChanged;
        public event Action<ArtifactRuntimeState, bool> ArtifactAutoChanged;

        public void Initialize(BattleSessionContext nextContext)
        {
            context = nextContext;
            BuildUi();
            RebuildArtifactViews();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void ShowRelease(ArtifactReleaseReport report)
        {
            if (report == null)
            {
                return;
            }

            ArtifactView view = FindView(report.artifact);
            if (view != null)
            {
                view.pulseUntil = Time.time + 0.28f;
            }

            feedbackText.text = report.message;
            feedbackUntil = Time.time + 1.4f;
        }

        private void BuildUi()
        {
            if (canvas != null)
            {
                return;
            }

            font = CreateChineseFont();
            GameObject canvasObject = new GameObject("PrototypeArtifactBattleHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            hpText = CreateText(canvas.transform, "PlayerState", 24, TextAnchor.MiddleLeft);
            SetRect(hpText.rectTransform, new Vector2(40f, -38f), new Vector2(520f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            focusText = CreateText(canvas.transform, "FocusState", 24, TextAnchor.MiddleRight);
            SetRect(focusText.rectTransform, new Vector2(-40f, -38f), new Vector2(620f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            feedbackText = CreateText(canvas.transform, "Feedback", 30, TextAnchor.MiddleCenter);
            feedbackText.color = new Color(1f, 0.86f, 0.42f, 1f);
            SetRect(feedbackText.rectTransform, new Vector2(0f, -132f), new Vector2(900f, 56f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            GameObject totalAutoObject = CreateButton(canvas.transform, "TotalAutoButton", "总自动：开");
            totalAutoButton = totalAutoObject.GetComponent<Button>();
            totalAutoText = totalAutoObject.GetComponentInChildren<Text>();
            SetRect(totalAutoObject.GetComponent<RectTransform>(), new Vector2(40f, 130f), new Vector2(180f, 46f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            totalAutoButton.onClick.AddListener(ToggleTotalAuto);

            queueRoot = CreateRectObject("ArtifactQueueHud", canvas.transform, typeof(Image)).GetComponent<RectTransform>();
            Image queueImage = queueRoot.GetComponent<Image>();
            queueImage.color = new Color(0.045f, 0.04f, 0.036f, 0.86f);
            SetRect(queueRoot, new Vector2(0f, 40f), new Vector2(1520f, 128f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        }

        private void RebuildArtifactViews()
        {
            ClearChildren(queueRoot);
            artifactViews.Clear();

            if (context == null || context.artifactQueue == null || context.artifactQueue.equippedArtifacts == null)
            {
                return;
            }

            int count = context.artifactQueue.equippedArtifacts.Count;
            float width = 132f;
            float gap = 12f;
            float totalWidth = count * width + Mathf.Max(0, count - 1) * gap;
            float left = -totalWidth * 0.5f + width * 0.5f;

            for (int i = 0; i < count; i++)
            {
                ArtifactRuntimeState runtime = context.artifactQueue.equippedArtifacts[i];
                GameObject panelObject = CreateRectObject("ArtifactHud_" + i, queueRoot, typeof(Image));
                Image panel = panelObject.GetComponent<Image>();
                panel.color = new Color(0.12f, 0.105f, 0.088f, 0.95f);
                SetRect(panelObject.GetComponent<RectTransform>(), new Vector2(left + i * (width + gap), 0f), new Vector2(width, 96f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                Text name = CreateText(panelObject.transform, "Name", 17, TextAnchor.MiddleCenter);
                SetRect(name.rectTransform, new Vector2(0f, 22f), new Vector2(width - 12f, 40f), Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f);

                Image cooldown = CreateRectObject("CooldownMask", panelObject.transform, typeof(Image)).GetComponent<Image>();
                cooldown.color = new Color(0f, 0f, 0f, 0.62f);
                Stretch(cooldown.rectTransform);

                Text cooldownText = CreateText(panelObject.transform, "Cooldown", 18, TextAnchor.MiddleCenter);
                SetRect(cooldownText.rectTransform, new Vector2(0f, -15f), new Vector2(width - 12f, 26f), Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f);

                GameObject autoObject = CreateButton(panelObject.transform, "AutoButton", "自动");
                SetRect(autoObject.GetComponent<RectTransform>(), new Vector2(0f, -38f), new Vector2(88f, 26f), Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f);
                Button autoButton = autoObject.GetComponent<Button>();
                Text autoText = autoObject.GetComponentInChildren<Text>();
                ArtifactRuntimeState capturedRuntime = runtime;
                autoButton.onClick.AddListener(delegate { ToggleArtifactAuto(capturedRuntime); });

                artifactViews.Add(new ArtifactView
                {
                    runtime = runtime,
                    panel = panel,
                    cooldownMask = cooldown,
                    nameText = name,
                    cooldownText = cooldownText,
                    autoText = autoText,
                    autoButton = autoButton,
                });
            }
        }

        private void Refresh()
        {
            if (context == null)
            {
                return;
            }

            hpText.text = "生命 " + context.playerHp.ToString("0") + "/" + context.playerMaxHp.ToString("0") + "　护盾 " + context.playerShield.ToString("0");
            focusText.text = context.focusTarget != null && context.focusTarget.CanBeTargeted
                ? "集火：" + context.focusTarget.displayName
                : "集火：无";
            totalAutoText.text = context.totalAutoEnabled ? "总自动：开" : "总自动：关";

            for (int i = 0; i < artifactViews.Count; i++)
            {
                RefreshArtifactView(artifactViews[i]);
            }

            if (Time.time > feedbackUntil)
            {
                feedbackText.text = string.Empty;
            }
        }

        private static void RefreshArtifactView(ArtifactView view)
        {
            if (view == null || view.runtime == null || view.runtime.profile == null)
            {
                return;
            }

            ArtifactRuntimeState runtime = view.runtime;
            ArtifactCombatProfile profile = runtime.profile;
            view.nameText.text = profile.displayName;
            view.autoText.text = runtime.autoEnabled ? "自动" : "手动";

            float cooldown = Mathf.Max(0f, profile.cooldown);
            float ratio = cooldown <= 0f ? 0f : Mathf.Clamp01(runtime.cooldownRemaining / cooldown);
            view.cooldownMask.fillAmount = ratio;
            view.cooldownMask.type = Image.Type.Filled;
            view.cooldownMask.fillMethod = Image.FillMethod.Vertical;
            view.cooldownMask.fillOrigin = (int)Image.OriginVertical.Bottom;
            view.cooldownText.text = runtime.isReady ? "就绪" : runtime.cooldownRemaining.ToString("0.0") + "秒";

            if (Time.time < view.pulseUntil)
            {
                view.panel.color = new Color(0.72f, 0.48f, 0.16f, 0.98f);
            }
            else if (!runtime.autoEnabled)
            {
                view.panel.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            }
            else if (runtime.shengStacks > 0)
            {
                view.panel.color = new Color(0.18f, 0.28f, 0.16f, 0.96f);
            }
            else
            {
                view.panel.color = new Color(0.12f, 0.105f, 0.088f, 0.95f);
            }
        }

        private ArtifactView FindView(ArtifactRuntimeState runtime)
        {
            for (int i = 0; i < artifactViews.Count; i++)
            {
                if (artifactViews[i].runtime == runtime)
                {
                    return artifactViews[i];
                }
            }

            return null;
        }

        private void ToggleTotalAuto()
        {
            bool next = context == null || !context.totalAutoEnabled;
            TotalAutoChanged?.Invoke(next);
        }

        private void ToggleArtifactAuto(ArtifactRuntimeState runtime)
        {
            if (runtime == null)
            {
                return;
            }

            ArtifactAutoChanged?.Invoke(runtime, !runtime.autoEnabled);
        }

        private GameObject CreateButton(Transform parent, string name, string label)
        {
            GameObject buttonObject = CreateRectObject(name, parent, typeof(Image), typeof(Button));
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.13f, 0.1f, 0.95f);
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.27f, 0.21f, 0.14f, 1f);
            colors.pressedColor = new Color(0.09f, 0.075f, 0.06f, 1f);
            button.colors = colors;

            Text text = CreateText(buttonObject.transform, "Label", 18, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = new Color(0.96f, 0.89f, 0.72f, 1f);
            Stretch(text.rectTransform);
            return buttonObject;
        }

        private Text CreateText(Transform parent, string name, int size, TextAnchor alignment)
        {
            GameObject textObject = CreateRectObject(name, parent, typeof(Text));
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(0.94f, 0.9f, 0.82f, 1f);
            return text;
        }

        private static GameObject CreateRectObject(string name, Transform parent, params Type[] components)
        {
            Type[] allComponents = new Type[components.Length + 1];
            allComponents[0] = typeof(RectTransform);
            for (int i = 0; i < components.Length; i++)
            {
                allComponents[i + 1] = components[i];
            }

            GameObject gameObject = new GameObject(name, allComponents);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
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

        private static void ClearChildren(Transform parent)
        {
            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                children.Add(parent.GetChild(i).gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                Destroy(children[i]);
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
