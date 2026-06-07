using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NewFPG.Battle;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Prototype
{
    public sealed class PrototypeArtifactBattleController : MonoBehaviour
    {
        [SerializeField] private string explorationSceneName = PrototypeSceneTransit.ExplorationSceneName;
        [SerializeField] private Vector3 playerPosition = Vector3.zero;
        [SerializeField] private float playerMaxHp = 120f;
        [SerializeField] private bool createDefaultQueueWhenMissing = true;

        private readonly ArtifactAutoReleaseSystem autoReleaseSystem = new ArtifactAutoReleaseSystem();
        private readonly List<PrototypeTrainingTarget> trainingTargets = new List<PrototypeTrainingTarget>();
        private BattleSessionContext context;
        private PrototypeArtifactBattleHud hud;
        private Font font;
        private bool isLoadingScene;

        private void Awake()
        {
            font = CreateChineseFont();
            InitializeContext();
            EnsureHud();
            EnsureTrainingTargets();
            autoReleaseSystem.Released += OnArtifactReleased;
        }

        private void OnDestroy()
        {
            autoReleaseSystem.Released -= OnArtifactReleased;
        }

        private void Update()
        {
            if (context == null || isLoadingScene)
            {
                return;
            }

            context.elapsedSeconds += Time.deltaTime;
            SyncTrainingTargets();
            autoReleaseSystem.Tick(context, Time.deltaTime);

            if (WasReturnPressed())
            {
                ReturnToExploration();
            }
        }

        public void ReturnToExploration()
        {
            if (isLoadingScene)
            {
                return;
            }

            isLoadingScene = true;
            PrototypeSceneTransit.SetBattleResultMessage("法宝训练结束：已返回洞口");
            SceneManager.LoadScene(explorationSceneName, LoadSceneMode.Single);
        }

        private void InitializeContext()
        {
            context = new BattleSessionContext
            {
                sessionId = "artifact_training_" + System.Guid.NewGuid().ToString("N"),
                sceneName = SceneManager.GetActiveScene().name,
                isBattleRunning = true,
                totalAutoEnabled = true,
                playerPosition = playerPosition,
                playerMaxHp = playerMaxHp,
                playerHp = playerMaxHp,
                playerShield = 0f,
                playerControlLocked = true,
            };

            ArtifactQueueState queue;
            if (!PrototypeSceneTransit.TryConsumePendingArtifactQueue(out queue) || queue == null || queue.equippedArtifacts == null || queue.equippedArtifacts.Count == 0)
            {
                queue = createDefaultQueueWhenMissing ? BuildDefaultQueue() : new ArtifactQueueState();
            }

            context.artifactQueue = queue;
        }

        private static ArtifactQueueState BuildDefaultQueue()
        {
            ArtifactCatalog catalog = ArtifactCatalog.CreateDefault();
            PrepLoadoutState loadout = new PrepLoadoutState();
            loadout.Capacity = 10;
            loadout.SetArtifacts(new[]
            {
                "zhanfeng_short_blade",
                "shuangshui_needle",
                "baoyan_talisman",
                "jingshui_amulet",
                "fumu_bell",
                "duanjin_ring",
                "qingmu_heal_orb",
            });

            ArtifactQueueState queue = loadout.ToQueueState(catalog);
            for (int i = 0; i < queue.equippedArtifacts.Count; i++)
            {
                queue.equippedArtifacts[i].cooldownRemaining = i * 0.35f;
                queue.equippedArtifacts[i].isReady = queue.equippedArtifacts[i].cooldownRemaining <= 0f;
            }

            return queue;
        }

        private void EnsureHud()
        {
            GameObject hudObject = new GameObject("PrototypeArtifactBattleHud");
            hudObject.transform.SetParent(transform, false);
            hud = hudObject.AddComponent<PrototypeArtifactBattleHud>();
            hud.Initialize(context);
            hud.TotalAutoChanged += OnTotalAutoChanged;
            hud.ArtifactAutoChanged += OnArtifactAutoChanged;
        }

        private void EnsureTrainingTargets()
        {
            if (trainingTargets.Count > 0)
            {
                return;
            }

            CreateTarget("near_dummy", "近身假人", new Vector3(-1.8f, 0.8f, 5.2f), 200f, false, false, false, 1.5f, RangeBand.Melee);
            CreateTarget("low_hp_dummy", "残血假人", new Vector3(0f, 0.8f, 6.2f), 90f, false, false, true, 0.9f, RangeBand.Near);
            PrototypeTrainingTarget charger = CreateTarget("charging_dummy", "蓄力假人", new Vector3(1.8f, 0.8f, 5.8f), 160f, true, true, false, 2.5f, RangeBand.Near);
            context.focusTarget = trainingTargets.Count > 0 ? trainingTargets[0].State : null;
            if (charger != null && context.focusTarget == null)
            {
                context.focusTarget = charger.State;
            }

            SyncTrainingTargets();
        }

        private PrototypeTrainingTarget CreateTarget(
            string id,
            string displayName,
            Vector3 position,
            float hp,
            bool charging,
            bool interruptible,
            bool slowed,
            float threat,
            RangeBand rangeBand)
        {
            GameObject targetObject = new GameObject(displayName);
            targetObject.transform.SetParent(transform, false);
            targetObject.transform.position = position;
            PrototypeTrainingTarget target = targetObject.AddComponent<PrototypeTrainingTarget>();
            target.Initialize(id, displayName, hp, charging, interruptible, slowed, threat, rangeBand);
            trainingTargets.Add(target);
            return target;
        }

        private void SyncTrainingTargets()
        {
            context.enemies.Clear();
            EnemyCombatState nextFocus = null;

            for (int i = 0; i < trainingTargets.Count; i++)
            {
                PrototypeTrainingTarget target = trainingTargets[i];
                if (target == null || target.State == null)
                {
                    continue;
                }

                target.SyncFromState();
                EnemyCombatState state = target.State;
                state.distanceToPlayer = Vector3.Distance(playerPosition, state.position);
                context.enemies.Add(state);

                bool isCurrentFocus = context.focusTarget == state && state.CanBeTargeted;
                if (isCurrentFocus)
                {
                    nextFocus = state;
                }
            }

            if (nextFocus == null)
            {
                nextFocus = TargetSelector.SelectTarget(TargetSelectorType.nearest, context.enemies, playerPosition, null);
            }

            context.focusTarget = nextFocus;
            for (int i = 0; i < trainingTargets.Count; i++)
            {
                trainingTargets[i].SetFocused(trainingTargets[i].State == context.focusTarget);
            }
        }

        private void OnTotalAutoChanged(bool enabled)
        {
            autoReleaseSystem.SetTotalAuto(context, enabled);
        }

        private void OnArtifactAutoChanged(ArtifactRuntimeState runtime, bool enabled)
        {
            autoReleaseSystem.SetArtifactAuto(runtime, enabled);
        }

        private void OnArtifactReleased(ArtifactReleaseReport report)
        {
            hud.ShowRelease(report);
            ShowSceneEffect(report);
            SyncTrainingTargets();
        }

        private void ShowSceneEffect(ArtifactReleaseReport report)
        {
            Vector3 position = report.effectPosition;
            position.y += 1.1f;
            GameObject effectObject = new GameObject("ArtifactSceneEffect");
            effectObject.transform.position = position;
            ArtifactSceneEffect effect = effectObject.AddComponent<ArtifactSceneEffect>();
            effect.Initialize(report.message, font, ElementColor(report.profile != null ? report.profile.element : Element.None));
        }

        private static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Metal:
                    return new Color(0.82f, 0.76f, 0.5f, 1f);
                case Element.Water:
                    return new Color(0.22f, 0.48f, 0.86f, 1f);
                case Element.Wood:
                    return new Color(0.28f, 0.66f, 0.34f, 1f);
                case Element.Fire:
                    return new Color(0.9f, 0.28f, 0.12f, 1f);
                case Element.Earth:
                    return new Color(0.62f, 0.46f, 0.22f, 1f);
                default:
                    return new Color(0.9f, 0.82f, 0.62f, 1f);
            }
        }

        private static Font CreateChineseFont()
        {
            Font createdFont = Font.CreateDynamicFontFromOSFont(
                new[] { "SimHei", "Microsoft YaHei UI", "Microsoft YaHei", "Arial" },
                24);
            return createdFont != null ? createdFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static bool WasReturnPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }

        private sealed class ArtifactSceneEffect : MonoBehaviour
        {
            private Canvas canvas;
            private Text text;
            private float destroyAt;

            public void Initialize(string message, Font font, Color color)
            {
                GameObject canvasObject = new GameObject("EffectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(transform, false);
                canvasObject.transform.localScale = Vector3.one * 0.01f;
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 35;
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(520f, 96f);

                GameObject textObject = new GameObject("EffectText", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(canvasObject.transform, false);
                text = textObject.GetComponent<Text>();
                text.font = font;
                text.fontSize = 30;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = color;
                text.text = message;
                RectTransform textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                destroyAt = Time.time + 1.25f;
            }

            private void Update()
            {
                transform.position += Vector3.up * Time.deltaTime * 0.45f;
                if (text != null)
                {
                    Color color = text.color;
                    color.a = Mathf.Clamp01((destroyAt - Time.time) / 1.25f);
                    text.color = color;
                }

                if (Time.time >= destroyAt)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
