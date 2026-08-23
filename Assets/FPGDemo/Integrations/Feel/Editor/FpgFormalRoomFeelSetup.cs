using System;
using System.Collections.Generic;
using FPG.Demo.Unity;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.Feel
{
    public static class FpgFormalRoomFeelSetup
    {
        private const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";
        private const string FeelFolderPath =
            "Assets/FPGDemo/Integrations/Feel";
        private const string FeelPrefabFolderPath = FeelFolderPath + "/Prefabs";
        private const string EnemyHitPrefabPath =
            FeelPrefabFolderPath + "/PF_FPG_EnemyHitFeel.prefab";
        private const string LegacyVolumeProfilePath =
            FeelFolderPath + "/FPG_FormalRoom_Feel_URPProfile.asset";

        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/FPGDemo/Presentation/Characters/Enemies/Burstbug/Prefabs/PF_FPG_BurstbugEntity.prefab",
            "Assets/FPGDemo/Presentation/Characters/Enemies/Luan/Prefabs/PF_FPG_LuanEntity.prefab",
            "Assets/FPGDemo/Presentation/Characters/Enemies/Hudie/Prefabs/PF_FPG_HudieEntity.prefab"
        };

        private static readonly string[] LegacyScaleShakerPaths =
        {
            "__FormalRoom/Presentation/FormalPlayerHud/CombatAimReticle",
            "__FormalRoom/Presentation/FormalPlayerHud/PlayerValues/LifeBar",
            "__FormalRoom/Presentation/FormalPlayerHud/PlayerValues/BarrierBar",
            "__FormalRoom/Presentation/FormalPlayerHud/PlayerValues/AmmoBar"
        };

        [MenuItem("FPG/Integrations/Feel/Rebuild FormalRoom Feel Setup")]
        public static void RebuildFormalRoomFeelSetupMenu()
        {
            try
            {
                Debug.Log(RebuildFormalRoomFeelSetup());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("FPG/Integrations/Feel/Refresh Enemy Hit Prefab")]
        public static void RefreshEnemyHitPrefabMenu()
        {
            try
            {
                EnsureAssetFolder(FeelPrefabFolderPath);
                CreateOrUpdateEnemyHitPrefab();
                AssetDatabase.SaveAssets();
                Debug.Log("Feel enemy-hit prefab refreshed: "
                    + EnemyHitPrefabPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static string RebuildFormalRoomFeelSetup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != FormalRoomScenePath)
            {
                return "Wrong active scene: " + scene.path;
            }

            EnsureAssetFolder(FeelPrefabFolderPath);
            GameObject sharedPrefab = CreateOrUpdateEnemyHitPrefab();

            int attachedPrefabCount = 0;
            for (int index = 0; index < EnemyPrefabPaths.Length; index++)
            {
                if (AttachSharedFeedbackPrefab(
                        EnemyPrefabPaths[index],
                        sharedPrefab))
                {
                    attachedPrefabCount++;
                }
            }

            GameObject presentation = RequireSceneObject(
                scene,
                "__FormalRoom/Presentation");
            FpgFormalCombatFeedbackBridge combatBridge =
                presentation.GetComponent<FpgFormalCombatFeedbackBridge>();
            FpgEnemyEntityPool enemyPool = FindSingleSceneComponent<
                FpgEnemyEntityPool>(scene);
            if (combatBridge == null || enemyPool == null)
            {
                throw new InvalidOperationException(
                    "FormalRoom requires one combat feedback bridge and one enemy entity pool.");
            }

            GameObject feedbackRoot = EnsureChild(
                presentation.transform,
                "FeelFeedbackRoot");
            feedbackRoot.SetActive(true);
            ClearChildren(feedbackRoot.transform);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(feedbackRoot);
            RemoveUnexpectedRootComponents(feedbackRoot);

            FpgFeelEnemyHitRouter router =
                feedbackRoot.GetComponent<FpgFeelEnemyHitRouter>();
            if (router == null)
            {
                router = feedbackRoot.AddComponent<FpgFeelEnemyHitRouter>();
            }

            SerializedObject routerData = new SerializedObject(router);
            routerData.FindProperty("combatFeedbackBridge").objectReferenceValue =
                combatBridge;
            routerData.FindProperty("enemyEntityPool").objectReferenceValue =
                enemyPool;
            routerData.ApplyModifiedPropertiesWithoutUndo();

            GameObject flashOverlay = FindSceneObject(
                scene,
                "__FormalRoom/Presentation/FormalPlayerHud/FeelFlashOverlay");
            if (flashOverlay != null)
            {
                UnityEngine.Object.DestroyImmediate(flashOverlay);
            }

            int removedScaleShakers = 0;
            for (int index = 0; index < LegacyScaleShakerPaths.Length; index++)
            {
                GameObject target = FindSceneObject(
                    scene,
                    LegacyScaleShakerPaths[index]);
                if (target == null)
                {
                    continue;
                }

                MMScaleShaker[] shakers = target.GetComponents<MMScaleShaker>();
                for (int shakerIndex = 0;
                    shakerIndex < shakers.Length;
                    shakerIndex++)
                {
                    UnityEngine.Object.DestroyImmediate(shakers[shakerIndex]);
                    removedScaleShakers++;
                }
            }

            if (AssetDatabase.LoadMainAssetAtPath(LegacyVolumeProfilePath)
                != null)
            {
                AssetDatabase.DeleteAsset(LegacyVolumeProfilePath);
            }

            EditorUtility.SetDirty(router);
            EditorUtility.SetDirty(feedbackRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!router.TryValidate(out string routerError))
            {
                throw new InvalidOperationException(routerError);
            }

            return "Feel enemy-hit setup rebuilt; enemyPrefabs="
                + attachedPrefabCount
                + "; removedScaleShakers="
                + removedScaleShakers
                + "; sharedPrefab="
                + EnemyHitPrefabPath;
        }

        private static GameObject CreateOrUpdateEnemyHitPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                EnemyHitPrefabPath);
            GameObject root = existing == null
                ? new GameObject("PF_FPG_EnemyHitFeel")
                : PrefabUtility.LoadPrefabContents(EnemyHitPrefabPath);

            try
            {
                root.name = "PF_FPG_EnemyHitFeel";
                FpgFeelEnemyHitFeedback feedback =
                    EnsureComponent<FpgFeelEnemyHitFeedback>(root);
                FpgFeelRenderScaleSpringTarget springTarget =
                    EnsureComponent<FpgFeelRenderScaleSpringTarget>(root);
                springTarget.Target = springTarget;
                springTarget.TimeScaleMode =
                    MMSpringComponentBase.TimeScaleModes.Scaled;
                springTarget.FloatSpring.Damping = 0.82f;
                springTarget.FloatSpring.Frequency = 14f;
                springTarget.FloatSpring.ClampSettings.ClampMin = true;
                springTarget.FloatSpring.ClampSettings.ClampMinValue = 0.985f;
                springTarget.FloatSpring.ClampSettings.ClampMinInitial = false;
                springTarget.FloatSpring.ClampSettings.ClampMinBounce = false;
                springTarget.FloatSpring.ClampSettings.ClampMax = true;
                springTarget.FloatSpring.ClampSettings.ClampMaxValue = 1.035f;
                springTarget.FloatSpring.ClampSettings.ClampMaxInitial = false;
                springTarget.FloatSpring.ClampSettings.ClampMaxBounce = false;

                SerializedObject springData = new SerializedObject(springTarget);
                springData.FindProperty("minimumScale").floatValue = 0.985f;
                springData.FindProperty("maximumScale").floatValue = 1.035f;
                springData.ApplyModifiedPropertiesWithoutUndo();

                GameObject playerObject = EnsureChild(
                    root.transform,
                    "MMF_Player");
                MMF_Player player = EnsureComponent<MMF_Player>(playerObject);
                ConfigurePlayer(player, springTarget);

                SerializedObject feedbackData = new SerializedObject(feedback);
                feedbackData.FindProperty("hitPlayer").objectReferenceValue =
                    player;
                feedbackData.FindProperty("renderScaleSpring")
                    .objectReferenceValue = springTarget;
                feedbackData.FindProperty("oneShotDuration").floatValue =
                    0.06f;
                feedbackData.FindProperty("cooldownDuration").floatValue =
                    0.06f;
                feedbackData.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(feedback);
                EditorUtility.SetDirty(springTarget);
                EditorUtility.SetDirty(player);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    EnemyHitPrefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Could not save shared enemy-hit Feel prefab.");
                }
            }
            finally
            {
                if (existing == null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                else
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(EnemyHitPrefabPath);
        }

        private static void ConfigurePlayer(
            MMF_Player player,
            FpgFeelRenderScaleSpringTarget springTarget)
        {
            player.PlayMode = MMF_Player.PlayModes.Parallel;
            player.AutoPlayOnStart = false;
            player.AutoPlayOnEnable = false;
            player.AutoInitialization = true;
            player.InitializationMode = MMFeedbacks.InitializationModes.Awake;
            player.CanPlayWhileAlreadyPlaying = true;
            player.CooldownDuration = 0.06f;
            player.ForceTimescaleMode = true;
            player.ForcedTimescaleMode = TimescaleModes.Scaled;
            player.StopFeedbacksOnDisable = true;
            player.RestoreInitialValuesOnDisable = true;
            player.FeedbacksIntensity = 1f;

            if (player.FeedbacksList == null)
            {
                player.FeedbacksList = new List<MMF_Feedback>();
            }
            else
            {
                player.FeedbacksList.Clear();
            }

            MMF_SpringFloat spring = (MMF_SpringFloat)player.AddFeedback(
                typeof(MMF_SpringFloat),
                true);
            spring.Label = "Enemy render scale spring";
            spring.TargetSpring = springTarget;
            spring.DeclaredDuration = 0.06f;
            spring.Command = SpringCommands.Bump;
            spring.BumpAmount = 2f;
            spring.OverrideDamping = true;
            spring.NewDamping = 0.82f;
            spring.OverrideFrequency = true;
            spring.NewFrequency = 14f;
            spring.RandomizeOutput = false;
            spring.RandomizeDuration = false;
            spring.Timing.TimescaleMode = TimescaleModes.Scaled;
            spring.Timing.InitialDelay = 0f;
            spring.Timing.CooldownDuration = 0f;
            spring.Timing.InterruptsOnStop = true;

            player.RefreshCache();
            player.ComputeCachedTotalDuration();
        }

        private static bool AttachSharedFeedbackPrefab(
            string enemyPrefabPath,
            GameObject sharedPrefab)
        {
            if (sharedPrefab == null)
            {
                throw new ArgumentNullException(nameof(sharedPrefab));
            }

            GameObject enemyRoot = PrefabUtility.LoadPrefabContents(
                enemyPrefabPath);
            try
            {
                Transform existing = enemyRoot.transform.Find(
                    sharedPrefab.name);
                bool correctNestedPrefab = existing != null
                    && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        existing.gameObject) == EnemyHitPrefabPath;
                if (!correctNestedPrefab)
                {
                    if (existing != null)
                    {
                        UnityEngine.Object.DestroyImmediate(existing.gameObject);
                    }

                    GameObject instance = PrefabUtility.InstantiatePrefab(
                        sharedPrefab,
                        enemyRoot.transform) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            "Could not nest Feel prefab in " + enemyPrefabPath);
                    }

                    instance.name = sharedPrefab.name;
                    instance.transform.SetLocalPositionAndRotation(
                        Vector3.zero,
                        Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    enemyRoot,
                    enemyPrefabPath);
                return saved != null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(enemyRoot);
            }
        }

        private static void RemoveUnexpectedRootComponents(GameObject root)
        {
            Component[] components = root.GetComponents<Component>();
            for (int index = components.Length - 1; index >= 0; index--)
            {
                Component component = components[index];
                if (component == null
                    || component is Transform
                    || component is FpgFeelEnemyHitRouter)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static T FindSingleSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            T match = null;
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index].gameObject.scene != scene)
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        "FormalRoom contains multiple " + typeof(T).Name + ".");
                }

                match = components[index];
            }

            return match;
        }

        private static GameObject RequireSceneObject(Scene scene, string path)
        {
            GameObject value = FindSceneObject(scene, path);
            if (value == null)
            {
                throw new InvalidOperationException(
                    "Missing scene object: " + path);
            }

            return value;
        }

        private static GameObject FindSceneObject(Scene scene, string path)
        {
            string[] segments = path.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (roots[rootIndex].name != segments[0])
                {
                    continue;
                }

                Transform current = roots[rootIndex].transform;
                for (int segmentIndex = 1;
                    segmentIndex < segments.Length && current != null;
                    segmentIndex++)
                {
                    current = current.Find(segments[segmentIndex]);
                }

                return current == null ? null : current.gameObject;
            }

            return null;
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static T EnsureComponent<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            return component == null ? target.AddComponent<T>() : component;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(
                    parent.GetChild(index).gameObject);
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
