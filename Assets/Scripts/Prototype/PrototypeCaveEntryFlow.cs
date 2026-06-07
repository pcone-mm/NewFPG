using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NewFPG.Battle;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Prototype
{
    public sealed class PrototypeCaveEntryFlow : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerMover playerMover;
        [SerializeField] private Transform entranceCenter;
        [SerializeField] private Transform returnPoint;
        [SerializeField] private PrototypePrepHud prepHud;
        [SerializeField] private ArtifactCatalog artifactCatalog = ArtifactCatalog.CreateDefault();
        [SerializeField] private PrepLoadoutState prepLoadoutState = new PrepLoadoutState();
        [SerializeField] private float triggerRadius = 1.85f;
        [SerializeField] private string battleSceneName = PrototypeSceneTransit.CaveBattleSceneName;
        [SerializeField] private string returnPointName = "CaveReturnPoint";

        private Canvas toastCanvas;
        private Text resultToast;
        private bool isPrepOpen;
        private bool dismissedUntilExit;
        private bool wasInRange;
        private bool isLoadingScene;
        private float toastHideAt;

        private void Awake()
        {
            ResolveReferences();
            EnsureRuntimeUi();
        }

        private void Start()
        {
            ApplyPendingReturnPoint();
        }

        private void Update()
        {
            UpdateResultToast();

            if (isLoadingScene)
            {
                return;
            }

            if (isPrepOpen)
            {
                if (WasConfirmPressed())
                {
                    StartBattle();
                }
                else if (WasCancelPressed())
                {
                    CancelPrep();
                }

                return;
            }

            ResolveReferences();
            if (playerMover == null)
            {
                return;
            }

            bool isInRange = IsPlayerInRange();
            if (!isInRange)
            {
                dismissedUntilExit = false;
            }

            if (isInRange && !wasInRange && !dismissedUntilExit)
            {
                OpenPrep();
            }

            wasInRange = isInRange;
        }

        public void OpenPrep()
        {
            if (isPrepOpen || isLoadingScene)
            {
                return;
            }

            ResolveReferences();
            EnsureRuntimeUi();
            isPrepOpen = true;
            prepHud.Open(artifactCatalog, prepLoadoutState);

            if (playerMover != null)
            {
                playerMover.SetMovementEnabled(false);
            }
        }

        public void StartBattle()
        {
            if (isLoadingScene)
            {
                return;
            }

            EnsureRuntimeUi();
            if (prepHud == null || !prepHud.CanStart)
            {
                if (prepHud != null)
                {
                    prepHud.SetStatus("请先组成合法队列。");
                }

                return;
            }

            ResolveReferences();
            Vector3 safePoint = returnPoint != null ? returnPoint.position : transform.position + Vector3.back * 2f;
            PrototypeSceneTransit.PrepareCaveBattleReturn(safePoint, prepHud.BuildQueueState());

            isLoadingScene = true;
            isPrepOpen = false;
            prepHud.Close();
            SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
        }

        public void CancelPrep()
        {
            if (!isPrepOpen)
            {
                return;
            }

            isPrepOpen = false;
            dismissedUntilExit = true;
            if (prepHud != null)
            {
                prepHud.Close();
            }

            if (playerMover != null)
            {
                playerMover.SetMovementEnabled(true);
            }
        }

        private void ApplyPendingReturnPoint()
        {
            if (!PrototypeSceneTransit.TryConsumeReturnPoint(out Vector3 safePoint, out string message))
            {
                return;
            }

            ResolveReferences();
            if (playerMover != null)
            {
                playerMover.transform.position = safePoint;
                playerMover.SetMovementEnabled(true);
            }

            HorizontalCameraFollowTarget followTarget = FindFirstObjectByType<HorizontalCameraFollowTarget>();
            if (followTarget != null)
            {
                followTarget.SnapToTarget();
            }

            if (!string.IsNullOrEmpty(message))
            {
                ShowResultToast(message);
            }
        }

        private bool IsPlayerInRange()
        {
            Transform playerTransform = playerMover.transform;
            Transform center = entranceCenter != null ? entranceCenter : transform;

            Vector3 playerPosition = playerTransform.position;
            Vector3 centerPosition = center.position;
            playerPosition.y = 0f;
            centerPosition.y = 0f;

            return Vector3.Distance(playerPosition, centerPosition) <= triggerRadius;
        }

        private void ResolveReferences()
        {
            if (artifactCatalog == null || artifactCatalog.Count == 0)
            {
                artifactCatalog = ArtifactCatalog.CreateDefault();
            }

            if (prepLoadoutState == null)
            {
                prepLoadoutState = new PrepLoadoutState();
            }

            prepLoadoutState.Capacity = 10;

            if (playerMover == null)
            {
                playerMover = FindFirstObjectByType<PrototypePlayerMover>();
            }

            if (entranceCenter == null)
            {
                entranceCenter = transform;
            }

            if (returnPoint == null && transform.parent != null)
            {
                returnPoint = transform.parent.Find(returnPointName);
            }
        }

        private void EnsureRuntimeUi()
        {
            if (prepHud == null)
            {
                prepHud = FindFirstObjectByType<PrototypePrepHud>();
            }

            if (prepHud == null)
            {
                GameObject prepHudObject = new GameObject("PrototypePrepHud");
                prepHud = prepHudObject.AddComponent<PrototypePrepHud>();
            }

            prepHud.StartRequested -= StartBattle;
            prepHud.StartRequested += StartBattle;
            prepHud.CancelRequested -= CancelPrep;
            prepHud.CancelRequested += CancelPrep;

            if (toastCanvas != null)
            {
                return;
            }

            Font font = CreateChineseFont();
            GameObject canvasObject = new GameObject("CaveResultToastCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            toastCanvas = canvasObject.GetComponent<Canvas>();
            toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            toastCanvas.sortingOrder = 60;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            resultToast = CreateText(toastCanvas.transform, "CaveResultToast", font, 30, TextAnchor.MiddleCenter);
            RectTransform toastRect = resultToast.rectTransform;
            toastRect.anchorMin = new Vector2(0.5f, 1f);
            toastRect.anchorMax = new Vector2(0.5f, 1f);
            toastRect.anchoredPosition = new Vector2(0f, -96f);
            toastRect.sizeDelta = new Vector2(960f, 72f);
            resultToast.color = new Color(0.95f, 0.9f, 0.72f, 1f);
            resultToast.gameObject.SetActive(false);
        }

        private void ShowResultToast(string message)
        {
            if (resultToast == null)
            {
                EnsureRuntimeUi();
            }

            resultToast.text = message;
            resultToast.gameObject.SetActive(true);
            toastHideAt = Time.time + 3f;
        }

        private void UpdateResultToast()
        {
            if (resultToast == null || !resultToast.gameObject.activeSelf)
            {
                return;
            }

            if (Time.time >= toastHideAt)
            {
                resultToast.gameObject.SetActive(false);
            }
        }

        private static Text CreateText(Transform parent, string name, Font font, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font CreateChineseFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "SimHei", "Microsoft YaHei UI", "Microsoft YaHei", "Arial" },
                26);

            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static bool WasConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
            return false;
#endif
        }

        private static bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
