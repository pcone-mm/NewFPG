using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    [InitializeOnLoad]
    public static class FpgRoomPlaytestController
    {
        public const string RoomGuidSessionKey = "FPGDemo.RoomAuthoring.Playtest.RoomGuid";
        public const string ScenarioGuidSessionKey = "FPGDemo.RoomAuthoring.Playtest.ScenarioGuid";

        private const string RestoreSetupSessionKey = "FPGDemo.RoomAuthoring.Playtest.RestoreSetup";
        private const string RestorePendingSessionKey = "FPGDemo.RoomAuthoring.Playtest.RestorePending";
        private const string BootScenePath = "Assets/FPGDemo/Scenes/Boot.unity";
        private static bool restoreQueued;


        [Serializable]
        private sealed class SceneSetupData
        {
            public List<SceneSetupEntry> scenes = new List<SceneSetupEntry>();
        }

        [Serializable]
        private sealed class SceneSetupEntry
        {
            public string path;
            public bool isLoaded;
            public bool isActive;
        }

        static FpgRoomPlaytestController()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= PollPlaytestState;
            EditorApplication.update += PollPlaytestState;
        }

        public static bool TryStart(
            ScriptableObject room,
            ScriptableObject scenario,
            out string error)
        {
            error = string.Empty;
            if (SessionState.GetBool(RestorePendingSessionKey, false)
                && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RestorePreviousSetup();
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "试玩只能从 Edit Mode 启动。";
                return false;
            }

            if (SessionState.GetBool(RestorePendingSessionKey, false))
            {
                error = "已有试玩正在恢复，请等待场景恢复完成后再试。";
                return false;
            }
            FPG.Demo.Unity.FpgRoomDefinition roomDefinition =
                room as FPG.Demo.Unity.FpgRoomDefinition;
            FPG.Demo.Unity.D0CombatScenarioDefinition scenarioDefinition =
                scenario as FPG.Demo.Unity.D0CombatScenarioDefinition;
            if (roomDefinition == null || scenarioDefinition == null)
            {
                error = "一键试玩需要当前房间和 D0 遭遇配置。";
                return false;
            }

            if (!TryValidateScenarioBinding(scenarioDefinition, out error))
            {
                return false;
            }

            if (!FPG.Demo.Unity.FpgRoomEncounterValidator.TryValidate(
                    roomDefinition,
                    scenarioDefinition,
                    out FPG.Demo.Unity.FpgRoomEncounterValidationResult validation))
            {
                error = validation.FirstError == null
                    ? "当前房间与 D0 遭遇配置不兼容。"
                    : validation.FirstError.Message;
                return false;
            }

            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath))
            {
                error = $"找不到 Boot 场景：{BootScenePath}";
                return false;
            }

            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    FpgRoomAuthoringSchema.CombatLabScenePath))
            {
                error = $"找不到 CombatLab 场景：{FpgRoomAuthoringSchema.CombatLabScenePath}";
                return false;
            }

            string roomGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(roomDefinition));
            string scenarioGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(scenarioDefinition));
            if (string.IsNullOrWhiteSpace(roomGuid)
                || string.IsNullOrWhiteSpace(scenarioGuid))
            {
                error = "一键试玩只能使用已保存的 RoomDefinition 和 D0 遭遇资产。";
                return false;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                error = "一键试玩不能在 Prefab Mode 中启动；请先返回场景模式。";
                return false;
            }

            if (EditorUtility.IsDirty(roomDefinition)
                || EditorUtility.IsDirty(scenarioDefinition)
                || IsDirtyTaxonomy(roomDefinition)
                || (roomDefinition.EnvironmentPrefab != null
                    && EditorUtility.IsDirty(roomDefinition.EnvironmentPrefab)))
            {
                error = "一键试玩前必须保存当前 RoomDefinition、主分组/标签、D0 遭遇和环境 Prefab；试玩不会自动保存资产。";
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                error = "已取消试玩；当前场景没有被修改。";
                return false;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene openScene = SceneManager.GetSceneAt(index);
                if (!openScene.IsValid() || !openScene.isLoaded)
                {
                    continue;
                }

                if (openScene.isDirty || string.IsNullOrWhiteSpace(openScene.path))
                {
                    error = "一键试玩前必须保存所有已打开场景；选择不保存不会关闭或丢弃当前工作。";
                    return false;
                }
            }
            if (!TryValidateCombatLabBindings(
                    roomDefinition,
                    scenarioDefinition,
                    out error))
            {
                return false;
            }

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            SessionState.SetString(RestoreSetupSessionKey, SerializeSetup(setup));
            SessionState.SetBool(RestorePendingSessionKey, true);
            SessionState.SetString(RoomGuidSessionKey, roomGuid);
            SessionState.SetString(ScenarioGuidSessionKey, scenarioGuid);

            try
            {
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
                FPG.Demo.Unity.FpgRoomPlaytestOverrides.Set(
                    roomDefinition,
                    scenarioDefinition);
                EditorApplication.isPlaying = true;
                return true;
            }
            catch (Exception exception)
            {
                FPG.Demo.Unity.FpgRoomPlaytestOverrides.Clear();
                RestorePreviousSetup();
                error = "启动 CombatLab 试玩失败：" + exception.Message;
                return false;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if ((state == PlayModeStateChange.ExitingEditMode
                    || state == PlayModeStateChange.EnteredPlayMode)
                && SessionState.GetBool(RestorePendingSessionKey, false))
            {
                if (!TryApplyPlaytestOverrides(out string error))
                {
                    Debug.LogError("[FPG Room Editor] 无法应用试玩覆盖：" + error);
                    EditorApplication.isPlaying = false;
                }

                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode
                && SessionState.GetBool(RestorePendingSessionKey, false))
            {
                FPG.Demo.Unity.FpgRoomPlaytestOverrides.Clear();
                QueueRestore();
            }
        }

        private static void PollPlaytestState()
        {
            if (!SessionState.GetBool(RestorePendingSessionKey, false))
            {
                return;
            }

            if (EditorApplication.isPlaying
                && !FPG.Demo.Unity.FpgRoomPlaytestOverrides.IsActive
                && !TryApplyPlaytestOverrides(out string error))
            {
                Debug.LogError("[FPG Room Editor] 无法恢复试玩覆盖：" + error);
                EditorApplication.isPlaying = false;
                return;
            }

            if (!EditorApplication.isPlaying
                && !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling)
            {
                RestorePreviousSetup();
            }
        }

        private static void QueueRestore()
        {
            if (restoreQueued)
            {
                return;
            }

            restoreQueued = true;
            EditorApplication.delayCall += RestorePreviousSetup;
        }

        private static bool TryApplyPlaytestOverrides(out string error)
        {
            string roomPath = AssetDatabase.GUIDToAssetPath(
                SessionState.GetString(RoomGuidSessionKey, string.Empty));
            string scenarioPath = AssetDatabase.GUIDToAssetPath(
                SessionState.GetString(ScenarioGuidSessionKey, string.Empty));
            FPG.Demo.Unity.FpgRoomDefinition room =
                AssetDatabase.LoadAssetAtPath<FPG.Demo.Unity.FpgRoomDefinition>(
                    roomPath);
            FPG.Demo.Unity.D0CombatScenarioDefinition scenario =
                AssetDatabase.LoadAssetAtPath<FPG.Demo.Unity.D0CombatScenarioDefinition>(
                    scenarioPath);
            if (room == null || scenario == null)
            {
                error = "无法从 SessionState 恢复房间或遭遇资产。";
                return false;
            }

            if (!FPG.Demo.Unity.FpgRoomEncounterValidator.TryValidate(
                    room,
                    scenario,
                    out FPG.Demo.Unity.FpgRoomEncounterValidationResult validation))
            {
                error = validation.FirstError == null
                    ? "恢复后的房间与遭遇配置不兼容。"
                    : validation.FirstError.Message;
                return false;
            }

            FPG.Demo.Unity.FpgRoomPlaytestOverrides.Set(room, scenario);
            error = string.Empty;
            return true;
        }

        private static bool IsDirtyTaxonomy(FPG.Demo.Unity.FpgRoomDefinition room)
        {
            if (room == null)
            {
                return false;
            }

            if (room.MainGroup != null && EditorUtility.IsDirty(room.MainGroup))
            {
                return true;
            }

            IReadOnlyList<FPG.Demo.Unity.FpgRoomTagDefinition> tags = room.Tags;
            for (int index = 0; index < tags.Count; index++)
            {
                if (tags[index] != null && EditorUtility.IsDirty(tags[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryValidateScenarioBinding(
            FPG.Demo.Unity.D0CombatScenarioDefinition scenario,
            out string error)
        {
            error = string.Empty;
            FPG.Demo.Unity.BattleScenarioConfig config =
                AssetDatabase.LoadAssetAtPath<FPG.Demo.Unity.BattleScenarioConfig>(
                    "Assets/FPGDemo/Config/BattleScenarioConfig.asset");
            if (config == null)
            {
                error = "CombatLab 缺少 BattleScenarioConfig，无法确定表现绑定。";
                return false;
            }

            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty authoredScenario =
                serialized.FindProperty("authoredScenario");
            FPG.Demo.Unity.D0CombatScenarioDefinition installedScenario =
                authoredScenario == null
                    ? null
                    : authoredScenario.objectReferenceValue
                        as FPG.Demo.Unity.D0CombatScenarioDefinition;
            if (installedScenario != null && installedScenario != scenario)
            {
                error = "当前 CombatLab 的表现绑定只支持遭遇 '"
                    + installedScenario.DisplayName
                    + "'；请先安装或切换匹配的 D0 表现配置。";
                return false;
            }

            return true;
        }

        private static bool TryValidateCombatLabBindings(
            FPG.Demo.Unity.FpgRoomDefinition room,
            FPG.Demo.Unity.D0CombatScenarioDefinition scenario,
            out string error)
        {
            error = string.Empty;
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            FPG.Demo.Unity.BattleSceneContext context = null;
            try
            {
                Scene combatLab = EditorSceneManager.OpenScene(
                    FpgRoomAuthoringSchema.CombatLabScenePath,
                    OpenSceneMode.Single);
                FPG.Demo.Unity.FpgRoomPlaytestOverrides.Set(room, scenario);

                List<FPG.Demo.Unity.BattleSceneContext> contexts =
                    new List<FPG.Demo.Unity.BattleSceneContext>();
                foreach (GameObject root in combatLab.GetRootGameObjects())
                {
                    contexts.AddRange(
                        root.GetComponentsInChildren<FPG.Demo.Unity.BattleSceneContext>(true));
                }

                if (contexts.Count != 1)
                {
                    error = "CombatLab 必须只包含一个 BattleSceneContext。当前数量："
                        + contexts.Count;
                    return false;
                }

                context = contexts[0];
                if (context.RoomBinding == null)
                {
                    error = "CombatLab 缺少 FpgRoomCombatLabBinding；一键试玩不会回退到旧 Stage。";
                    return false;
                }

                if (context.RoomBinding.ConfiguredRoomDefinition == null
                    || context.RoomBinding.ConfiguredScenarioDefinition == null)
                {
                    error = "CombatLab 的 RoomBinding 缺少序列化房间或遭遇引用。";
                    return false;
                }

                if (!context.TryInitializeRoom(out string roomError))
                {
                    error = "CombatLab 房间实例化预检失败：" + roomError;
                    return false;
                }

                if (!context.TryValidate(out string contextError))
                {
                    error = "CombatLab 场景预检失败：" + contextError;
                    return false;
                }

                if (context.ScenarioConfig != null
                    && context.ScenarioConfig.UsesAuthoredScenario
                    && !context.TryValidateD0RuntimeBindings(
                        out string d0BindingError))
                {
                    error = "CombatLab D0 运行时预检失败：" + d0BindingError;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "CombatLab 场景预检异常：" + exception.Message;
                return false;
            }
            finally
            {
                if (context != null && context.RoomBinding != null)
                {
                    context.RoomBinding.ClearRuntimeRoom();
                }

                FPG.Demo.Unity.FpgRoomPlaytestOverrides.Clear();
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        private static string SerializeSetup(IEnumerable<SceneSetup> setup)
        {
            SceneSetupData data = new SceneSetupData();
            foreach (SceneSetup item in setup)
            {
                data.scenes.Add(new SceneSetupEntry
                {
                    path = item.path,
                    isLoaded = item.isLoaded,
                    isActive = item.isActive
                });
            }

            return JsonUtility.ToJson(data);
        }

        private static SceneSetup[] DeserializeSetup(string json)
        {
            SceneSetupData data = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<SceneSetupData>(json);
            if (data?.scenes == null)
            {
                return Array.Empty<SceneSetup>();
            }

            SceneSetup[] setup = new SceneSetup[data.scenes.Count];
            for (int index = 0; index < setup.Length; index++)
            {
                SceneSetupEntry entry = data.scenes[index];
                setup[index] = new SceneSetup
                {
                    path = entry.path,
                    isLoaded = entry.isLoaded,
                    isActive = entry.isActive
                };
            }

            return setup;
        }

        private static void RestorePreviousSetup()
        {
            restoreQueued = false;
            if (!SessionState.GetBool(RestorePendingSessionKey, false))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                QueueRestore();
                return;
            }

            SessionState.SetBool(RestorePendingSessionKey, false);
            FPG.Demo.Unity.FpgRoomPlaytestOverrides.Clear();
            SceneSetup[] setup = DeserializeSetup(
                SessionState.GetString(RestoreSetupSessionKey, string.Empty));
            SessionState.EraseString(RestoreSetupSessionKey);
            SessionState.EraseString(RoomGuidSessionKey);
            SessionState.EraseString(ScenarioGuidSessionKey);
            if (setup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
