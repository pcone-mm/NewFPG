using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NewFPG.EditorTools
{
    public static class ShulinBlendSceneImporter
    {
        private const string ScenePath = "Assets/Scenes/ShulinDemoScene.unity";
        private const string BlendPath = "Assets/Art/Scenes/shulin\u6f14\u793a.blend";
        private const string ExportedFbxPath = "Assets/Art/Scenes/shulin\u6f14\u793a_unity.fbx";
        private const string FbxFallbackPath = "Assets/Art/Scenes/shulin\u6f14\u793a.fbx";
        private const string RootName = "ShulinDemoScene_Root";
        private const string ImportedModelName = "Imported_shulin_blend";

        [MenuItem("Tools/NewFPG/Create Shulin Demo Scene From Blend", false, 2110)]
        public static void BuildScene()
        {
            BuildSceneInternal();
        }

        public static void BuildFromCommandLine()
        {
            BuildSceneInternal();
        }

        private static void BuildSceneInternal()
        {
            string modelPath = ResolveModelPath();
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException($"Could not load a model asset at {modelPath}.");
            }

            EnsureTargetSceneIsNotDirty();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                GameObject root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);

                GameObject importedModel = InstantiateModel(modelAsset, scene, root.transform);
                importedModel.name = ImportedModelName;

                Bounds bounds = CalculateBounds(root);
                ConfigureLighting(scene);
                ConfigureCamera(scene, bounds);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new InvalidOperationException($"Unity failed to save scene to {ScenePath}.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[ShulinBlendSceneImporter] Created {ScenePath} from {modelPath}.");
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static string ResolveModelPath()
        {
            if (TryLoadModel(BlendPath))
            {
                return BlendPath;
            }

            if (TryLoadModel(ExportedFbxPath))
            {
                Debug.LogWarning(
                    $"[ShulinBlendSceneImporter] Blend model could not be loaded as a GameObject. Falling back to {ExportedFbxPath}.");
                return ExportedFbxPath;
            }

            if (TryLoadModel(FbxFallbackPath))
            {
                Debug.LogWarning(
                    $"[ShulinBlendSceneImporter] Exported FBX could not be loaded as a GameObject. Falling back to {FbxFallbackPath}.");
                return FbxFallbackPath;
            }

            throw new FileNotFoundException(
                "Could not load either the shulin blend asset or its FBX fallback as a Unity model.");
        }

        private static bool TryLoadModel(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
        }

        private static void EnsureTargetSceneIsNotDirty()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (!string.Equals(loadedScene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (loadedScene.isDirty)
                {
                    throw new InvalidOperationException(
                        $"{ScenePath} is already open with unsaved changes. Save or close it before regenerating.");
                }

                EditorSceneManager.CloseScene(loadedScene, true);
                return;
            }
        }

        private static GameObject InstantiateModel(GameObject modelAsset, Scene scene, Transform parent)
        {
            UnityEngine.Object instanceObject = PrefabUtility.InstantiatePrefab(modelAsset, scene);
            GameObject instance = instanceObject as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(modelAsset);
                SceneManager.MoveGameObjectToScene(instance, scene);
            }

            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 10f);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void ConfigureLighting(Scene scene)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.64f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.41f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.20f, 0.18f);

            GameObject lightObject = new GameObject("Sun Key Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.95f, 0.86f);
        }

        private static void ConfigureCamera(Scene scene, Bounds bounds)
        {
            Vector3 center = bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 6f);
            float distance = radius * 2.25f;

            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = center + new Vector3(0.72f, 0.45f, -0.7f).normalized * distance;
            cameraObject.transform.LookAt(center);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(1000f, distance + radius * 4f);
            camera.clearFlags = CameraClearFlags.Skybox;

            cameraObject.AddComponent<AudioListener>();
        }
    }
}
