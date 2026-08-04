using System.Collections.Generic;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.LevelAuthoring
{
    [InitializeOnLoad]
    public sealed class FpgBattleGmEditorWindow : EditorWindow
    {
        public const string MenuPath = "FPG Demo/战斗 GM 工具";
        public const string WindowTitle = "战斗 GM 工具";

        private const int MaxLogEntries = 32;
        private static readonly HashSet<FpgBattleGmEditorWindow> OpenWindows =
            new HashSet<FpgBattleGmEditorWindow>();
        private static FpgBattleTestBootstrap latestReadyBootstrap;

        [SerializeField]
        private FpgBattleTestBootstrap bootstrap;

        [SerializeField]
        private string enemyDefinitionId = "burstbug";

        [SerializeField]
        private int spawnCount = 1;

        [SerializeField]
        private string spawnPointId = string.Empty;

        [SerializeField]
        private string commandLine = "gm.spawn burstbug 1 enemy-any-01";

        [SerializeField]
        private List<string> resultLog = new List<string>(MaxLogEntries);

        private Vector2 windowScroll;
        private Vector2 logScroll;
        private double nextRepaintTime;

        static FpgBattleGmEditorWindow()
        {
            FpgBattleTestBootstrap.EditorReady += OnBootstrapReady;
            FpgBattleTestBootstrap.EditorUnavailable += OnBootstrapUnavailable;
        }

        public FpgBattleTestBootstrap BoundBootstrap => bootstrap;

        [MenuItem(MenuPath, priority = 115)]
        public static void Open()
        {
            FpgBattleGmEditorWindow window =
                GetWindow<FpgBattleGmEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(440f, 520f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(440f, 520f);
            OpenWindows.Add(this);
            EditorApplication.update += RepaintWhilePlaying;

            if (IsReady(latestReadyBootstrap))
            {
                bootstrap = latestReadyBootstrap;
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintWhilePlaying;
            OpenWindows.Remove(this);
        }

        private void OnGUI()
        {
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            DrawConnection();

            FpgBattleGmRuntime runtime = GetReadyRuntime();
            DrawRuntimeState(runtime);
            using (new EditorGUI.DisabledScope(runtime == null))
            {
                DrawSwitches(runtime);
                DrawSpawner(runtime);
                DrawCommand(runtime);
            }

            DrawResultLog();
            EditorGUILayout.EndScrollView();
        }

        private void DrawConnection()
        {
            EditorGUILayout.LabelField("BattleTest 连接", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                FpgBattleTestBootstrap selected =
                    (FpgBattleTestBootstrap)EditorGUILayout.ObjectField(
                        "BattleTest 启动器",
                        bootstrap,
                        typeof(FpgBattleTestBootstrap),
                        true);
                if (EditorGUI.EndChangeCheck())
                {
                    bootstrap = selected;
                }

                if (!EditorApplication.isPlaying)
                {
                    EditorGUILayout.HelpBox(
                        "尚未进入播放模式。请从顶部启动场景选择 Battle Test 后开始运行。",
                        MessageType.Info);
                    return;
                }

                if (bootstrap == null)
                {
                    EditorGUILayout.HelpBox(
                        "正在等待 BattleTest 启动器。也可以把场景中的启动器拖到上方字段。",
                        MessageType.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(bootstrap.LastError))
                {
                    EditorGUILayout.HelpBox(
                        "BattleTest 启动失败：" + bootstrap.LastError,
                        MessageType.Error);
                    return;
                }

                if (!IsReady(bootstrap))
                {
                    EditorGUILayout.HelpBox(
                        "BattleTest 正在初始化战斗沙盒。",
                        MessageType.Info);
                    return;
                }

                EditorGUILayout.HelpBox(
                    "已连接到 BattleTest 战斗沙盒。",
                    MessageType.Info);
            }
        }

        private static void DrawRuntimeState(FpgBattleGmRuntime runtime)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("战斗状态", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (runtime == null || runtime.Host == null
                    || runtime.Host.EncounterDirector == null)
                {
                    EditorGUILayout.LabelField("状态：未连接");
                    return;
                }

                FpgRoomEncounterDirector director =
                    runtime.Host.EncounterDirector;
                EditorGUILayout.LabelField(
                    "阶段：" + TranslatePhase(director.Phase));
                EditorGUILayout.LabelField(
                    "已激活敌人：" + director.ActiveEnemyCount
                    + "    等待入场：" + director.PendingEntryCount);
                EditorGUILayout.LabelField(
                    "玩家无敌：" + TranslateSwitch(runtime.IsPlayerInvincible)
                    + "    怪物 AI：" + TranslateSwitch(runtime.IsEnemyAiEnabled));
            }
        }

        private void DrawSwitches(FpgBattleGmRuntime runtime)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("战斗开关", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool invincible = EditorGUILayout.Toggle(
                    "玩家无敌",
                    runtime != null && runtime.IsPlayerInvincible);
                if (runtime != null
                    && invincible != runtime.IsPlayerInvincible)
                {
                    Record(
                        runtime.TrySetPlayerInvincible(
                            invincible,
                            out string result),
                        result);
                }

                bool enemyAiEnabled = EditorGUILayout.Toggle(
                    "怪物 AI",
                    runtime != null && runtime.IsEnemyAiEnabled);
                if (runtime != null
                    && enemyAiEnabled != runtime.IsEnemyAiEnabled)
                {
                    Record(
                        runtime.TrySetEnemyAiEnabled(
                            enemyAiEnabled,
                            out string result),
                        result);
                }
            }
        }

        private void DrawSpawner(FpgBattleGmRuntime runtime)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("召唤敌人", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                enemyDefinitionId = EditorGUILayout.TextField(
                    "敌人配置 ID",
                    enemyDefinitionId ?? string.Empty);
                spawnCount = EditorGUILayout.IntField("数量", spawnCount);
                spawnPointId = EditorGUILayout.TextField(
                    "出生点 ID（留空轮询）",
                    spawnPointId ?? string.Empty);

                if (GUILayout.Button("召唤敌人", GUILayout.Height(26f))
                    && runtime != null)
                {
                    Record(
                        runtime.TrySpawn(
                            enemyDefinitionId,
                            spawnCount,
                            spawnPointId,
                            out string result),
                        result);
                }
            }
        }

        private void DrawCommand(FpgBattleGmRuntime runtime)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("GM 命令", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                commandLine = EditorGUILayout.TextField(
                    "命令",
                    commandLine ?? string.Empty);
                EditorGUILayout.LabelField(
                    "支持 gm.god、gm.ai 和 gm.spawn 命令。",
                    EditorStyles.miniLabel);
                if (GUILayout.Button("执行命令", GUILayout.Height(24f))
                    && runtime != null)
                {
                    Record(
                        runtime.TryExecute(commandLine, out string result),
                        result);
                }
            }
        }

        private void DrawResultLog()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("执行日志", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(resultLog.Count == 0))
                {
                    if (GUILayout.Button("清空", GUILayout.Width(60f)))
                    {
                        resultLog.Clear();
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (resultLog.Count == 0)
                {
                    EditorGUILayout.LabelField("暂无执行记录。");
                    return;
                }

                logScroll = EditorGUILayout.BeginScrollView(
                    logScroll,
                    GUILayout.MinHeight(120f),
                    GUILayout.MaxHeight(220f));
                for (int index = resultLog.Count - 1; index >= 0; index--)
                {
                    EditorGUILayout.LabelField(
                        resultLog[index],
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void Record(bool succeeded, string result)
        {
            if (resultLog == null)
            {
                resultLog = new List<string>(MaxLogEntries);
            }

            if (resultLog.Count >= MaxLogEntries)
            {
                resultLog.RemoveAt(0);
            }

            resultLog.Add(
                (succeeded ? "成功：" : "失败：")
                + (string.IsNullOrWhiteSpace(result) ? "没有返回信息。" : result));
            Repaint();
        }

        private FpgBattleGmRuntime GetReadyRuntime()
        {
            if (!IsReady(bootstrap))
            {
                return null;
            }

            FpgBattleGmRuntime runtime = bootstrap.GmRuntime;
            return runtime != null && !runtime.IsDisposed ? runtime : null;
        }

        private void RepaintWhilePlaying()
        {
            if (!EditorApplication.isPlaying
                || EditorApplication.timeSinceStartup < nextRepaintTime)
            {
                return;
            }

            nextRepaintTime = EditorApplication.timeSinceStartup + 0.25d;
            Repaint();
        }

        private static void OnBootstrapReady(FpgBattleTestBootstrap source)
        {
            latestReadyBootstrap = source;
            foreach (FpgBattleGmEditorWindow window in OpenWindows)
            {
                if (window == null)
                {
                    continue;
                }

                window.bootstrap = source;
                window.Repaint();
            }
        }

        private static void OnBootstrapUnavailable(
            FpgBattleTestBootstrap source)
        {
            if (latestReadyBootstrap == source)
            {
                latestReadyBootstrap = null;
            }

            foreach (FpgBattleGmEditorWindow window in OpenWindows)
            {
                if (window == null || window.bootstrap != source)
                {
                    continue;
                }

                window.bootstrap = null;
                window.Repaint();
            }
        }

        private static bool IsReady(FpgBattleTestBootstrap source)
        {
            return source != null && source.IsReady
                && source.GmRuntime != null && !source.GmRuntime.IsDisposed;
        }

        private static string TranslateSwitch(bool enabled)
        {
            return enabled ? "开启" : "关闭";
        }

        private static string TranslatePhase(FpgEncounterPhase phase)
        {
            switch (phase)
            {
                case FpgEncounterPhase.None:
                    return "未开始";
                case FpgEncounterPhase.Preparing:
                    return "准备中";
                case FpgEncounterPhase.Warning:
                    return "波次预警";
                case FpgEncounterPhase.Spawning:
                    return "敌人入场中";
                case FpgEncounterPhase.Combat:
                    return "战斗中";
                case FpgEncounterPhase.WaveDelay:
                    return "波次间隔";
                case FpgEncounterPhase.Cleared:
                    return "已清场";
                case FpgEncounterPhase.Failed:
                    return "失败";
                case FpgEncounterPhase.Paused:
                    return "已暂停";
                case FpgEncounterPhase.Faulted:
                    return "运行故障";
                case FpgEncounterPhase.Disposed:
                    return "已关闭";
                case FpgEncounterPhase.Defeated:
                    return "玩家战败";
                default:
                    return "未知";
            }
        }
    }
}
