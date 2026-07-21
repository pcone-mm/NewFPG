using System;
using System.Collections.Generic;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgRoomCombatLabInstaller
    {
        private const string StagePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_Stage.asset";
        private const string ScenarioConfigPath =
            "Assets/FPGDemo/Config/BattleScenarioConfig.asset";
        private const string ConfigRoot = "Assets/FPGDemo/Config/Level";
        private const string EnvironmentFolder =
            "Assets/FPGDemo/Presentation/Level/Environment";
        private const string MigratedRoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_combatlab-forest.asset";

        [MenuItem(
            "FPG Demo/Room Migration/Migrate CombatLab And Install Default Room",
            priority = 141)]
        public static void MigrateAndInstallMenu()
        {
            if (TryMigrateAndInstall(out FpgRoomDefinition room, out string error))
            {
                Selection.activeObject = room;
                EditorGUIUtility.PingObject(room);
                Debug.Log(
                    $"[FPG Room Editor] CombatLab now uses room '{room.RoomId}'.");
            }
            else
            {
                Debug.LogError("[FPG Room Editor] " + error);
            }
        }

        public static bool TryMigrateAndInstall(
            out FpgRoomDefinition room,
            out string error)
        {
            room = null;
            error = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Cannot install the CombatLab room while entering or running Play Mode.";
                return false;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene openScene = SceneManager.GetSceneAt(index);
                if (openScene.IsValid()
                    && openScene.isLoaded
                    && (openScene.isDirty || string.IsNullOrWhiteSpace(openScene.path)))
                {
                    error = "Save all open scenes before installing the CombatLab room.";
                    return false;
                }
            }

            room = AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                MigratedRoomPath);
            if (room == null)
            {
                D0StageDefinition stage =
                    AssetDatabase.LoadAssetAtPath<D0StageDefinition>(StagePath);
                if (!FpgRoomStageMigrationTool.TryMigrate(
                        stage,
                        ConfigRoot,
                        EnvironmentFolder,
                        out _,
                        out error))
                {
                    return false;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    MigratedRoomPath,
                    ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);
                room = AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                    MigratedRoomPath);
                if (room == null)
                {
                    error = "D0 Stage migration did not create a persistent room definition.";
                    return false;
                }
            }

            BattleScenarioConfig scenarioConfig =
                AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(
                    ScenarioConfigPath);
            D0CombatScenarioDefinition scenario =
                scenarioConfig == null ? null : scenarioConfig.AuthoredScenario;
            if (!FpgRoomEncounterValidator.TryValidate(
                    room,
                    scenario,
                    out FpgRoomEncounterValidationResult validation))
            {
                error = validation.FirstError == null
                    ? "Migrated room and default scenario are incompatible."
                    : validation.FirstError.Message;
                return false;
            }

            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene combatLab = EditorSceneManager.OpenScene(
                    FpgRoomAuthoringSchema.CombatLabScenePath,
                    OpenSceneMode.Single);
                BattleSceneContext context = FindContext(combatLab);
                if (context == null || context.WorldRoot == null)
                {
                    error = "CombatLab requires one BattleSceneContext with WorldRoot.";
                    return false;
                }

                FpgRoomInstance roomInstance =
                    context.WorldRoot.GetComponent<FpgRoomInstance>();
                if (roomInstance == null)
                {
                    roomInstance = Undo.AddComponent<FpgRoomInstance>(
                        context.WorldRoot.gameObject);
                }

                FpgRoomCombatLabBinding binding =
                    context.GetComponent<FpgRoomCombatLabBinding>();
                if (binding == null)
                {
                    binding = Undo.AddComponent<FpgRoomCombatLabBinding>(
                        context.gameObject);
                }

                GameObject legacyEnvironment = null;
                if (context.PresentationRoot != null)
                {
                    Transform legacy = context.PresentationRoot.Find(
                        "D0Slice2D/D0Stage");
                    legacyEnvironment = legacy == null ? null : legacy.gameObject;
                }

                binding.Configure(
                    room,
                    scenario,
                    roomInstance,
                    legacyEnvironment);
                if (binding.RoomDefinition != room)
                {
                    error = "Unable to assign the migrated room to CombatLab.";
                    return false;
                }
                SerializedObject serializedContext =
                    new SerializedObject(context);
                SerializedProperty roomBinding =
                    serializedContext.FindProperty("roomBinding");
                if (roomBinding == null)
                {
                    error = "BattleSceneContext does not expose the roomBinding integration field.";
                    return false;
                }

                roomBinding.objectReferenceValue = binding;
                serializedContext.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(binding);
                EditorUtility.SetDirty(roomInstance);
                EditorUtility.SetDirty(context);
                EditorSceneManager.MarkSceneDirty(combatLab);
                if (!EditorSceneManager.SaveScene(combatLab))
                {
                    error = "Unable to save the CombatLab room binding.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "CombatLab room installation failed: "
                    + exception.Message;
                return false;
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(
                        previousSetup);
                }
            }
        }

        private static BattleSceneContext FindContext(Scene scene)
        {
            List<BattleSceneContext> contexts = new List<BattleSceneContext>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                contexts.AddRange(
                    roots[index].GetComponentsInChildren<BattleSceneContext>(true));
            }

            return contexts.Count == 1 ? contexts[0] : null;
        }
    }
}
