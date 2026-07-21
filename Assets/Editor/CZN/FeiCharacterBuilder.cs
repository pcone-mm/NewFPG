using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NewFPG.CZN.Editor
{
    public static class FeiCharacterBuilder
    {
        private const string CharacterRoot = "Assets/Imported/CZN/Fei_30048";
        private const string SpineRoot = CharacterRoot + "/SpineSource";
        private const string PreviewRoot = CharacterRoot + "/Preview";
        private const string PrefabRoot = PreviewRoot + "/Prefabs";
        private const string MainSkeletonPath = SpineRoot + "/model/30048_SkeletonData.asset";
        private const string BattleReadySkeletonPath = SpineRoot + "/model/30048_battle_ready_SkeletonData.asset";
        private const string MainPrefabPath = PrefabRoot + "/Fei_30048_Main.prefab";
        private const string BattleReadyPrefabPath = PrefabRoot + "/Fei_30048_BattleReady.prefab";
        private const string PreviewScenePath = PreviewRoot + "/Fei_30048_Preview.unity";
        private const string IntegrationReportPath = CharacterRoot + "/Metadata/spine-unity-integration-report.md";

        [MenuItem("Tools/CZN/Fei 30048/Build Complete Import")]
        public static void BuildCompleteImport()
        {
            BuildModelPrefabsAndPreview();
            FeiSkillComposer.BuildSkillCompositions();
        }

        [MenuItem("Tools/CZN/Fei 30048/Build Model Prefabs and Preview")]
        public static void BuildModelPrefabsAndPreview()
        {
            EnsureFolder(PreviewRoot);
            EnsureFolder(PrefabRoot);
            EnsureOrderedSpineImport();

            SkeletonDataAsset mainAsset = RequireSkeleton(MainSkeletonPath);
            SkeletonDataAsset battleReadyAsset = RequireSkeleton(BattleReadySkeletonPath);
            ValidateAllSkeletonAssets();

            BuildSkeletonPrefab(mainAsset, MainPrefabPath, "Fei_30048_Main", "idle");
            BuildSkeletonPrefab(
                battleReadyAsset,
                BattleReadyPrefabPath,
                "Fei_30048_BattleReady",
                "b_idle");
            AssetDatabase.SaveAssets();
            CreatePreviewScene();
            WriteIntegrationReport(mainAsset, battleReadyAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CZN] Built Fei 30048 model prefabs and preview scene at " + PreviewScenePath + ".");
        }

        private static SkeletonDataAsset RequireSkeleton(string path)
        {
            SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
            if (asset == null || asset.GetSkeletonData(true) == null)
            {
                throw new InvalidOperationException("Missing or unreadable SkeletonDataAsset: " + path);
            }
            return asset;
        }

        private static void EnsureOrderedSpineImport()
        {
            string absoluteRoot = AbsolutePath(SpineRoot);
            int expected = Directory.GetFiles(absoluteRoot, "*.atlas.txt", SearchOption.AllDirectories).Length;
            string[] skeletonGuids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { SpineRoot });
            bool allReadable = expected > 0 && skeletonGuids.Length == expected;
            if (allReadable)
            {
                foreach (string guid in skeletonGuids)
                {
                    SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null || asset.GetSkeletonData(true) == null)
                    {
                        allReadable = false;
                        break;
                    }
                }
            }
            if (allReadable)
            {
                return;
            }

            ImportFilesInOrder(absoluteRoot, "*.png");
            ImportFilesInOrder(absoluteRoot, "*.atlas.txt");
            ImportFilesInOrder(absoluteRoot, "*.json");
            AssetDatabase.SaveAssets();
        }

        private static void ImportFilesInOrder(string absoluteRoot, string pattern)
        {
            foreach (string absolutePath in Directory.GetFiles(absoluteRoot, pattern, SearchOption.AllDirectories))
            {
                AssetDatabase.ImportAsset(ToAssetPath(absolutePath), ImportAssetOptions.ForceUpdate);
            }
        }

        private static void BuildSkeletonPrefab(
            SkeletonDataAsset asset,
            string prefabPath,
            string objectName,
            string animationName)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            SkeletonData data = asset.GetSkeletonData(true);
            if (data.FindAnimation(animationName) == null)
            {
                throw new InvalidOperationException(
                    $"Animation '{animationName}' is missing from {AssetDatabase.GetAssetPath(asset)}.");
            }

            SkeletonAnimation skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(asset, true);
            if (skeleton == null)
            {
                throw new InvalidOperationException("Could not create SkeletonAnimation for " + objectName);
            }

            GameObject owner = skeleton.gameObject;
            owner.name = objectName;
            owner.hideFlags = HideFlags.None;
            skeleton.AnimationName = animationName;
            skeleton.loop = true;
            skeleton.timeScale = 1f;
            skeleton.Initialize(true);
            MeshRenderer renderer = owner.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 0;
            }

            PrefabUtility.SaveAsPrefabAsset(owner, prefabPath);
            UnityEngine.Object.DestroyImmediate(owner);
        }

        private static void CreatePreviewScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty && activeScene.path != PreviewScenePath)
            {
                if (!string.IsNullOrEmpty(activeScene.path) &&
                    activeScene.path.StartsWith(PreviewRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    EditorSceneManager.SaveScene(activeScene);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Save or close the unrelated dirty scene before rebuilding the Fei preview: " +
                        (string.IsNullOrEmpty(activeScene.path) ? "<Untitled>" : activeScene.path));
                }
            }

            Scene scene;
            if (File.Exists(AbsolutePath(PreviewScenePath)))
            {
                scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            GameObject rootObject = new GameObject("Fei 30048 Model Preview");
            InstantiatePreviewPrefab(MainPrefabPath, rootObject.transform, new Vector3(-2.4f, 0f, 0f), 1f);
            InstantiatePreviewPrefab(
                BattleReadyPrefabPath,
                rootObject.transform,
                new Vector3(2.5f, -2.1f, 0f),
                0.45f);

            GameObject cameraObject = new GameObject("Fei Model Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(rootObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.5f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.055f, 1f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.tag = "MainCamera";

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.SetParent(rootObject.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            SceneManager.SetActiveScene(scene);
            Selection.activeGameObject = rootObject;
        }

        private static void InstantiatePreviewPrefab(
            string prefabPath,
            Transform parent,
            Vector3 localPosition,
            float scale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = prefab != null ? PrefabUtility.InstantiatePrefab(prefab) as GameObject : null;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate " + prefabPath);
            }
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale;
        }

        private static void ValidateAllSkeletonAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { SpineRoot });
            List<string> failures = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                if (asset == null || asset.GetSkeletonData(true) == null)
                {
                    failures.Add(path);
                }
            }
            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Unreadable Fei SkeletonDataAssets:\n" + string.Join("\n", failures));
            }
        }

        private static void WriteIntegrationReport(
            SkeletonDataAsset mainAsset,
            SkeletonDataAsset battleReadyAsset)
        {
            string[] skeletonGuids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { SpineRoot });
            string[] atlasGuids = AssetDatabase.FindAssets("t:SpineAtlasAsset", new[] { SpineRoot });
            int totalAnimations = 0;
            foreach (string guid in skeletonGuids)
            {
                SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                SkeletonData data = asset != null ? asset.GetSkeletonData(true) : null;
                totalAnimations += data != null ? data.Animations.Count : 0;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("# 绯（30048）Spine Unity integration report");
            report.AppendLine();
            report.AppendLine($"- SkeletonDataAsset: {skeletonGuids.Length}");
            report.AppendLine($"- SpineAtlasAsset: {atlasGuids.Length}");
            report.AppendLine($"- Loaded animation total: {totalAnimations}");
            report.AppendLine($"- Main animations: {mainAsset.GetSkeletonData(true).Animations.Count}");
            report.AppendLine($"- BattleReady animations: {battleReadyAsset.GetSkeletonData(true).Animations.Count}");
            report.AppendLine("- Skeleton load failures: 0");
            report.AppendLine();
            report.AppendLine("Main prefab: `Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_Main.prefab`");
            report.AppendLine("BattleReady prefab: `Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_BattleReady.prefab`");
            report.AppendLine("Model preview: `Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_Preview.unity`");
            File.WriteAllText(AbsolutePath(IntegrationReportPath), report.ToString(), new UTF8Encoding(false));
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string name = Path.GetFileName(assetFolder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Invalid asset folder: " + assetFolder);
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string AbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string projectRoot = (Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
            return normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length + 1)
                : normalized;
        }
    }
}
