using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace NewFPG.Level
{
    public sealed class LevelFlowHud : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private Vector2 panelSize = new Vector2(520f, 180f);

        private Canvas canvas;
        private Text titleText;
        private Text bodyText;
        private RectTransform choicesRoot;
        private readonly List<Button> buttons = new List<Button>();

        public void Initialize()
        {
            if (canvas != null)
            {
                return;
            }

            font = font != null ? font : CreateChineseFont();
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("LevelFlowCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.04f, 0.045f, 0.05f, 0.78f);
            RectTransform panelRect = panelImage.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(28f, -28f);
            panelRect.sizeDelta = panelSize;

            titleText = CreateText(panelRect, "Title", 26, TextAnchor.UpperLeft, new Color(1f, 0.92f, 0.68f, 1f));
            SetRect(titleText.rectTransform, new Vector2(20f, -18f), new Vector2(panelSize.x - 40f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            bodyText = CreateText(panelRect, "Body", 21, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.9f, 1f));
            SetRect(bodyText.rectTransform, new Vector2(20f, -58f), new Vector2(panelSize.x - 40f, 88f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            GameObject choicesObject = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            choicesObject.transform.SetParent(canvasObject.transform, false);
            choicesRoot = choicesObject.GetComponent<RectTransform>();
            choicesRoot.anchorMin = new Vector2(0f, 0f);
            choicesRoot.anchorMax = new Vector2(0f, 0f);
            choicesRoot.pivot = new Vector2(0f, 0f);
            choicesRoot.anchoredPosition = new Vector2(28f, 32f);
            choicesRoot.sizeDelta = new Vector2(560f, 320f);

            VerticalLayoutGroup layout = choicesObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = choicesObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            HideChoices();
        }

        private void Update()
        {
            if (choicesRoot == null || !choicesRoot.gameObject.activeInHierarchy)
            {
                return;
            }

            int choiceIndex = ReadChoiceShortcut();
            if (choiceIndex < 0 || choiceIndex >= buttons.Count || buttons[choiceIndex] == null)
            {
                return;
            }

            if (buttons[choiceIndex].interactable)
            {
                buttons[choiceIndex].onClick.Invoke();
            }
        }

        public void SetStatus(string title, string body)
        {
            Initialize();
            titleText.text = title;
            bodyText.text = body;
        }

        public void ShowChoices(IReadOnlyList<LevelHudChoice> choices)
        {
            Initialize();
            ClearChoices();
            choicesRoot.gameObject.SetActive(true);

            if (choices == null)
            {
                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                LevelHudChoice choice = choices[i];
                Button button = CreateButton(choicesRoot, choice.label, () => choice.selected?.Invoke());
                buttons.Add(button);
            }
        }

        public void HideChoices()
        {
            Initialize();
            ClearChoices();
            choicesRoot.gameObject.SetActive(false);
        }

        private void ClearChoices()
        {
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                if (buttons[i] != null)
                {
                    DestroyObject(buttons[i].gameObject);
                }
            }

            buttons.Clear();
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject("ChoiceButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 68f;
            layout.minHeight = 56f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.18f, 0.2f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.28f, 0.24f, 0.16f, 0.98f);
            colors.pressedColor = new Color(0.52f, 0.38f, 0.16f, 1f);
            button.colors = colors;

            Text text = CreateText(buttonObject.transform, "Label", 20, TextAnchor.MiddleLeft, new Color(0.96f, 0.9f, 0.74f, 1f));
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetStretch(text.rectTransform, new Vector2(18f, 6f), new Vector2(-18f, -6f));
            return button;
        }

        private Text CreateText(Transform parent, string name, int size, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
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

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            }

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("LevelFlowEventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            if (!eventSystem.gameObject.activeSelf)
            {
                eventSystem.gameObject.SetActive(true);
            }

#if ENABLE_INPUT_SYSTEM
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private static int ReadChoiceShortcut()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    return 0;
                }

                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    return 1;
                }

                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                {
                    return 2;
                }

                if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
                {
                    return 3;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                return 0;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                return 1;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                return 2;
            }

            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                return 3;
            }
#endif

            return -1;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
