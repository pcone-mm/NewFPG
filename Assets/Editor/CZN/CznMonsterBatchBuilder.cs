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
    public static class CznMonsterBatchBuilder
    {
        private const string ImportRoot = "Assets/Imported/CZN/Monsters";
        private const string PreviewRoot = ImportRoot + "/Preview";
        private const string PrefabRoot = PreviewRoot + "/Prefabs";
        private const string MetadataRoot = ImportRoot + "/Metadata";
        private const string PreviewScenePath = PreviewRoot + "/CZN_Monsters_8_Preview.unity";
        private const string IntegrationReportPath = MetadataRoot + "/spine-unity-integration-report.md";
        private const string IdleAnimation = "normal_idle";

        private static readonly MonsterSpec[] Monsters =
        {
            new MonsterSpec("1001005", "KillerFly", "killer_fly", "main", 15),
            new MonsterSpec("1001023", "BareBeetle", "bare_beetle", "main", 8),
            new MonsterSpec("1001016", "HoneyJarPorte", "honey_jar_porte", "main", 17),
            new MonsterSpec("1001003", "Burstbug", "burstbug", "main", 8),
            new MonsterSpec("1004002", "PowerTaker", "power_taker", "main", 7),
            new MonsterSpec("1004020", "MiniBite", "mini_bite", "shadow", 12),
            new MonsterSpec("1006002", "SpawnInsect", "spawn_insect", "main", 13),
            new MonsterSpec("1006018", "DustInsect", "dust_insect", "shadow", 9),
        };

        [MenuItem("Tools/CZN/Monsters/Build Selected 8 Models")]
        public static void BuildSelectedMonsterImport()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder(PreviewRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(MetadataRoot);
            EnsureOrderedSpineImport();
            ValidateAllConvertedSkeletons();

            List<ImportedMonster> imported = LoadAndValidateMonsters();
            foreach (ImportedMonster monster in imported)
            {
                BuildSkeletonPrefab(monster);
            }

            AssetDatabase.SaveAssets();
            CreatePreviewScene(imported);
            WriteIntegrationReport(imported);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"[CZN] Imported {imported.Count} monster models and built preview scene at " +
                PreviewScenePath + ".");
        }

        [MenuItem("Tools/CZN/Monsters/Validate Selected 8 Models")]
        public static void ValidateSelectedMonsterImport()
        {
            List<ImportedMonster> imported = LoadAndValidateMonsters();
            int animationCount = imported.Sum(item => item.SkeletonData.Animations.Count);
            Debug.Log(
                $"[CZN] Monster model validation passed: {imported.Count} skeletons, " +
                $"{animationCount} animations, 0 unreadable assets.");
        }

        [MenuItem("Tools/CZN/Monsters/Open Selected 8 Preview")]
        public static void OpenPreviewScene()
        {
            if (!File.Exists(AbsolutePath(PreviewScenePath)))
            {
                throw new FileNotFoundException(
                    "Build the selected monster import before opening its preview.",
                    PreviewScenePath);
            }

            EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
        }

        // Public command-line entry point used by Unity -executeMethod.
        public static void BuildBatchImport()
        {
            BuildSelectedMonsterImport();
        }

        private static void EnsureOrderedSpineImport()
        {
            if (AllConvertedSkeletonsAreReadable(out _, out _))
            {
                return;
            }

            foreach (MonsterSpec spec in Monsters)
            {
                string spineRoot = SpineRoot(spec);
                string absoluteRoot = AbsolutePath(spineRoot);
                if (!Directory.Exists(absoluteRoot))
                {
                    throw new DirectoryNotFoundException(
                        $"Missing converted Spine source for monster {spec.Id}: {absoluteRoot}");
                }

                ImportFilesInOrder(absoluteRoot, "*.png");
                ImportFilesInOrder(absoluteRoot, "*.atlas.txt");
                ImportFilesInOrder(absoluteRoot, "*.json");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ValidateAllConvertedSkeletons()
        {
            if (AllConvertedSkeletonsAreReadable(out int expected, out List<string> failures))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Converted CZN skeleton validation failed (expected {expected}):\n" +
                string.Join("\n", failures));
        }

        private static bool AllConvertedSkeletonsAreReadable(
            out int expected,
            out List<string> failures)
        {
            expected = 0;
            failures = new List<string>();
            string[] searchFolders = Monsters.Select(SpineRoot).ToArray();
            foreach (string spineRoot in searchFolders)
            {
                string absoluteRoot = AbsolutePath(spineRoot);
                if (!Directory.Exists(absoluteRoot))
                {
                    failures.Add("Missing SpineSource folder: " + spineRoot);
                    continue;
                }

                expected += Directory.GetFiles(
                    absoluteRoot,
                    "*.json",
                    SearchOption.AllDirectories).Length;
            }

            string[] guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", searchFolders);
            if (guids.Length != expected)
            {
                failures.Add(
                    $"SkeletonDataAsset count mismatch: expected {expected}, found {guids.Length}.");
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                if (asset == null || asset.GetSkeletonData(true) == null)
                {
                    failures.Add("Missing or unreadable SkeletonDataAsset: " + path);
                }
            }

            return expected > 0 && failures.Count == 0;
        }

        private static void ImportFilesInOrder(string absoluteRoot, string pattern)
        {
            foreach (string absolutePath in Directory
                         .GetFiles(absoluteRoot, pattern, SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                AssetDatabase.ImportAsset(
                    ToAssetPath(absolutePath),
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static List<ImportedMonster> LoadAndValidateMonsters()
        {
            List<ImportedMonster> imported = new List<ImportedMonster>(Monsters.Length);
            List<string> failures = new List<string>();

            foreach (MonsterSpec spec in Monsters)
            {
                string skeletonPath = SkeletonPath(spec);
                SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(skeletonPath);
                SkeletonData data = asset != null ? asset.GetSkeletonData(true) : null;
                if (asset == null || data == null)
                {
                    failures.Add("Missing or unreadable SkeletonDataAsset: " + skeletonPath);
                    continue;
                }

                if (data.FindAnimation(IdleAnimation) == null)
                {
                    failures.Add($"{spec.Id} has no '{IdleAnimation}' animation: {skeletonPath}");
                    continue;
                }

                if (data.Animations.Count != spec.ExpectedAnimationCount)
                {
                    failures.Add(
                        $"{spec.Id} animation count mismatch: expected " +
                        $"{spec.ExpectedAnimationCount}, loaded {data.Animations.Count}.");
                    continue;
                }

                imported.Add(new ImportedMonster(spec, asset, data));
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "CZN monster import validation failed:\n" + string.Join("\n", failures));
            }

            return imported;
        }

        private static void BuildSkeletonPrefab(ImportedMonster monster)
        {
            SkeletonAnimation skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(
                monster.SkeletonAsset,
                true);
            if (skeleton == null)
            {
                throw new InvalidOperationException(
                    "Could not create SkeletonAnimation for monster " + monster.Spec.Id + ".");
            }

            GameObject owner = skeleton.gameObject;
            try
            {
                owner.name = "CZN_" + monster.Spec.Id + "_" + monster.Spec.DisplayName;
                owner.hideFlags = HideFlags.None;
                skeleton.loop = true;
                skeleton.timeScale = 1f;
                skeleton.AnimationName = IdleAnimation;
                skeleton.Initialize(true);
                skeleton.AnimationName = IdleAnimation;
                skeleton.Update(0f);
                skeleton.LateUpdate();

                MeshRenderer renderer = owner.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = 0;
                }

                PrefabUtility.SaveAsPrefabAsset(owner, PrefabPath(monster.Spec));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void CreatePreviewScene(IReadOnlyList<ImportedMonster> imported)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty &&
                !string.Equals(activeScene.path, PreviewScenePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Save or close the unrelated dirty scene before rebuilding the CZN monster preview: " +
                    (string.IsNullOrEmpty(activeScene.path) ? "<Untitled>" : activeScene.path));
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("CZN Selected Monster Preview (8)");
            List<SkeletonAnimation> previewBindings = new List<SkeletonAnimation>(imported.Count);
            List<string> previewLabels = new List<string>(imported.Count);

            const float columnSpacing = 3.5f;
            const float rowSpacing = 3.8f;
            for (int index = 0; index < imported.Count; index++)
            {
                int column = index % 4;
                int row = index / 4;
                Vector3 position = new Vector3(
                    (column - 1.5f) * columnSpacing,
                    (0.5f - row) * rowSpacing,
                    0f);
                previewBindings.Add(CreatePreviewCell(imported[index], root.transform, position));
                previewLabels.Add(
                    imported[index].Spec.Id + " " + imported[index].Spec.InternalAlias);
            }

            CznMonsterModelPreviewController controller =
                root.AddComponent<CznMonsterModelPreviewController>();
            controller.Configure(previewBindings.ToArray(), previewLabels.ToArray());

            GameObject cameraObject = new GameObject("CZN Monster Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.045f, 1f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.tag = "MainCamera";

            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            SceneManager.SetActiveScene(scene);
            Selection.activeGameObject = root;
        }

        private static SkeletonAnimation CreatePreviewCell(
            ImportedMonster monster,
            Transform parent,
            Vector3 localPosition)
        {
            GameObject cell = new GameObject(monster.Spec.Id + " " + monster.Spec.InternalAlias);
            cell.transform.SetParent(parent, false);
            cell.transform.localPosition = localPosition;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(monster.Spec));
            GameObject instance = prefab != null
                ? PrefabUtility.InstantiatePrefab(prefab) as GameObject
                : null;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate " + PrefabPath(monster.Spec));
            }

            instance.transform.SetParent(cell.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            FitSkeletonInsideCell(instance, 2.8f, 2.7f);
            SkeletonAnimation skeleton = instance.GetComponent<SkeletonAnimation>();
            if (skeleton == null)
            {
                throw new InvalidOperationException(
                    "Preview prefab has no SkeletonAnimation: " + PrefabPath(monster.Spec));
            }

            GameObject labelObject = new GameObject("Label", typeof(TextMesh));
            labelObject.transform.SetParent(cell.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, -1.62f, -0.25f);
            TextMesh label = labelObject.GetComponent<TextMesh>();
            label.text = monster.Spec.Id + "\n" + monster.Spec.InternalAlias;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.055f;
            label.fontSize = 48;
            label.color = new Color(0.82f, 0.9f, 1f, 1f);
            MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 100;
            }

            return skeleton;
        }

        private static void FitSkeletonInsideCell(GameObject instance, float maxWidth, float maxHeight)
        {
            SkeletonAnimation skeleton = instance.GetComponent<SkeletonAnimation>();
            if (skeleton == null)
            {
                return;
            }

            skeleton.Initialize(true);
            skeleton.loop = true;
            skeleton.AnimationName = IdleAnimation;
            skeleton.Update(0f);
            skeleton.LateUpdate();

            MeshFilter filter = instance.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.bounds.size.x <= 0.0001f || mesh.bounds.size.y <= 0.0001f)
            {
                return;
            }

            Bounds bounds = mesh.bounds;
            float scale = Mathf.Min(maxWidth / bounds.size.x, maxHeight / bounds.size.y);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = new Vector3(
                -bounds.center.x * scale,
                -bounds.center.y * scale + 0.05f,
                0f);
        }

        private static void WriteIntegrationReport(IReadOnlyList<ImportedMonster> imported)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("# CZN selected monster Spine Unity integration report");
            report.AppendLine();
            report.AppendLine($"- Imported models: {imported.Count}");
            report.AppendLine(
                $"- Loaded core model animations: {imported.Sum(item => item.SkeletonData.Animations.Count)}");
            string[] allSkeletonGuids = AssetDatabase.FindAssets(
                "t:SkeletonDataAsset",
                Monsters.Select(SpineRoot).ToArray());
            int allAnimationCount = allSkeletonGuids.Sum(guid =>
            {
                SkeletonDataAsset asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));
                SkeletonData data = asset != null ? asset.GetSkeletonData(true) : null;
                return data != null ? data.Animations.Count : 0;
            });
            report.AppendLine($"- Imported SkeletonDataAssets: {allSkeletonGuids.Length}");
            report.AppendLine($"- Loaded animations including effects: {allAnimationCount}");
            report.AppendLine("- Skeleton load failures: 0");
            report.AppendLine($"- Preview scene: `{PreviewScenePath}`");
            report.AppendLine();
            report.AppendLine("| ID | Alias | Model branch | Animations | Prefab |");
            report.AppendLine("|---|---|---:|---:|---|");
            foreach (ImportedMonster monster in imported)
            {
                report.AppendLine(
                    $"| `{monster.Spec.Id}` | `{monster.Spec.InternalAlias}` | " +
                    $"`{monster.Spec.ModelBranch}` | {monster.SkeletonData.Animations.Count} | " +
                    $"`{PrefabPath(monster.Spec)}` |");
            }

            report.AppendLine();
            report.AppendLine("Every prefab starts on looping `normal_idle`. Select its SkeletonAnimation " +
                              "component to choose any other recovered animation.");
            report.AppendLine("In Play Mode the preview overlay supports 1-8/arrow-key model selection, " +
                              "animation cycling and replay. Non-idle animations return to `normal_idle`.");
            File.WriteAllText(
                AbsolutePath(IntegrationReportPath),
                report.ToString(),
                new UTF8Encoding(false));
        }

        private static string SpineRoot(MonsterSpec spec)
        {
            return ImportRoot + "/" + spec.Id + "/SpineSource";
        }

        private static string SkeletonPath(MonsterSpec spec)
        {
            return SpineRoot(spec) + "/model/" + spec.Id + "_SkeletonData.asset";
        }

        private static string PrefabPath(MonsterSpec spec)
        {
            return PrefabRoot + "/CZN_" + spec.Id + "_" + spec.DisplayName + ".prefab";
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
            return Path.GetFullPath(
                Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
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

        private sealed class MonsterSpec
        {
            public MonsterSpec(
                string id,
                string displayName,
                string internalAlias,
                string modelBranch,
                int expectedAnimationCount)
            {
                Id = id;
                DisplayName = displayName;
                InternalAlias = internalAlias;
                ModelBranch = modelBranch;
                ExpectedAnimationCount = expectedAnimationCount;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string InternalAlias { get; }
            public string ModelBranch { get; }
            public int ExpectedAnimationCount { get; }
        }

        private sealed class ImportedMonster
        {
            public ImportedMonster(MonsterSpec spec, SkeletonDataAsset skeletonAsset, SkeletonData skeletonData)
            {
                Spec = spec;
                SkeletonAsset = skeletonAsset;
                SkeletonData = skeletonData;
            }

            public MonsterSpec Spec { get; }
            public SkeletonDataAsset SkeletonAsset { get; }
            public SkeletonData SkeletonData { get; }
        }
    }
}
