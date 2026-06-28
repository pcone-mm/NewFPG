using System;
using System.Collections.Generic;
using System.IO;
using NewFPG.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NewFPG.EditorTools
{
    public static class HuxinjingShieldAssetInstaller
    {
        private const string Root = "Assets/Art/HuxinjingShield";
        private const string LayoutPath = Root + "/huxinjing_layout.json";
        private const string MaterialFolder = Root + "/Materials";
        private const string PrefabFolder = "Assets/Prefabs/Effects";
        private const string PrefabPath = PrefabFolder + "/PF_HuxinjingShield.prefab";
        private const string PreviewScenePath = "Assets/Scenes/HuxinjingShieldPreview.unity";
        private const string PreviewScreenshotPath = "Assets/Screenshots/HuxinjingShieldPreview.png";
        private const float PixelsPerUnit = 512f;
        private const int GeneratedAssetVersion = 7;
        private const string GeneratedAssetVersionPath = "Library/HuxinjingShieldGeneratedAssetVersion.txt";

        [InitializeOnLoadMethod]
        private static void RefreshGeneratedAssetsAfterScriptReload()
        {
            if (!NeedsGeneratedAssetRefresh())
            {
                return;
            }

            EditorApplication.delayCall += RefreshGeneratedAssetsIfNeeded;
        }

        [MenuItem("NewFPG/Effects/Huxinjing PNG Shield/Install Assets And Prefab")]
        public static void InstallAssetsAndPrefab()
        {
            LayoutData layout = LoadLayout();
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            ConfigureTextureImporters(layout);
            CreatePrefab(layout);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteGeneratedAssetVersion();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            Debug.Log("Installed Huxinjing shield assets and prefab at " + PrefabPath + ".");
        }

        [MenuItem("NewFPG/Effects/Huxinjing PNG Shield/Create Preview Scene")]
        public static void CreatePreviewScene()
        {
            InstallAssetsAndPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "HuxinjingShieldPreview";

            Camera previewCamera = CreatePreviewCamera();
            CreatePreviewBackdrop();
            CreatePreviewTarget();
            CreatePreviewShield(previewCamera);

            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScenePath));
            Debug.Log("Created Huxinjing shield preview scene at " + PreviewScenePath + ".");
        }

        public static void InstallAssetsAndCreatePreviewScene()
        {
            CreatePreviewScene();
        }

        [MenuItem("NewFPG/Effects/Huxinjing PNG Shield/Render Preview Screenshot")]
        public static void RenderPreviewScreenshot()
        {
            if (!File.Exists(ProjectPath(PreviewScenePath)))
            {
                CreatePreviewScene();
            }

            EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            if (camera == null)
            {
                throw new InvalidOperationException("No camera found in Huxinjing shield preview scene.");
            }

            const int width = 1280;
            const int height = 720;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                screenshot.Apply();

                EnsureFolder("Assets/Screenshots");
                File.WriteAllBytes(ProjectPath(PreviewScreenshotPath), screenshot.EncodeToPNG());
                AssetDatabase.ImportAsset(PreviewScreenshotPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("Rendered Huxinjing shield preview screenshot at " + PreviewScreenshotPath + ".");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }

        private static LayoutData LoadLayout()
        {
            if (!File.Exists(ProjectPath(LayoutPath)))
            {
                throw new FileNotFoundException("Huxinjing PSD layout data is missing.", LayoutPath);
            }

            LayoutData layout = JsonUtility.FromJson<LayoutData>(File.ReadAllText(ProjectPath(LayoutPath)));
            if (layout == null || layout.layers == null || layout.layers.Length == 0)
            {
                throw new InvalidOperationException("Huxinjing PSD layout data did not contain any layers.");
            }

            return layout;
        }

        private static void ConfigureTextureImporters(LayoutData layout)
        {
            for (int i = 0; i < layout.layers.Length; i++)
            {
                ConfigureSpriteTextureImporter(LayerTexturePath(layout.layers[i]));
            }
        }

        private static void ConfigureSpriteTextureImporter(string texturePath)
        {
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("Huxinjing texture importer not found for " + texturePath + ".");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
            defaultSettings.maxTextureSize = 4096;
            defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(defaultSettings);

            TextureImporterPlatformSettings standaloneSettings = importer.GetPlatformTextureSettings("Standalone");
            standaloneSettings.overridden = true;
            standaloneSettings.maxTextureSize = 4096;
            standaloneSettings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(standaloneSettings);
            importer.SaveAndReimport();
        }

        private static void CreatePrefab(LayoutData layout)
        {
            GameObject root = new GameObject("PF_HuxinjingShield", typeof(HuxinjingShieldEffect));
            try
            {
                List<LayerAssignment> assignments = new List<LayerAssignment>();
                LayerData[] renderLayers = SortLayersBackToFront(layout.layers);
                for (int i = 0; i < renderLayers.Length; i++)
                {
                    LayerData layer = renderLayers[i];
                    int logicalLayer = ResolveLogicalLayerNumber(layer.name);
                    GameObject child = new GameObject(layer.name, typeof(SpriteRenderer));
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localRotation = Quaternion.identity;
                    child.transform.localScale = Vector3.one;

                    SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
                    spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LayerTexturePath(layer));
                    spriteRenderer.sortingOrder = ResolveSortingOrder(logicalLayer);
                    spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(layer.opacity));
                    spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
                    spriteRenderer.spriteSortPoint = SpriteSortPoint.Center;
                    assignments.Add(new LayerAssignment(layer, child.transform, spriteRenderer));
                }

                ConfigureEffect(root.GetComponent<HuxinjingShieldEffect>(), layout, assignments, null);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success)
                {
                    throw new InvalidOperationException("Unity failed to save prefab at " + PrefabPath + ".");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureEffect(HuxinjingShieldEffect effect, LayoutData layout, List<LayerAssignment> assignments, Renderer compositeRenderer)
        {
            assignments.Sort(CompareLogicalLayer);

            SerializedObject serializedEffect = new SerializedObject(effect);
            serializedEffect.FindProperty("compositeRenderer").objectReferenceValue = compositeRenderer;
            serializedEffect.FindProperty("baseOpacity").floatValue = 1f;
            serializedEffect.FindProperty("shieldRatio").floatValue = 1f;
            serializedEffect.FindProperty("halfFadeThreshold").floatValue = 0.5f;
            serializedEffect.FindProperty("opacityAtZeroShield").floatValue = 0.18f;
            serializedEffect.FindProperty("autoDissolveAtZeroShield").boolValue = true;
            serializedEffect.FindProperty("playReleaseOnEnable").boolValue = true;
            serializedEffect.FindProperty("releaseDuration").floatValue = 0.32f;
            serializedEffect.FindProperty("releaseStartScale").floatValue = 0.88f;
            serializedEffect.FindProperty("rotateWhenVisible").boolValue = true;
            serializedEffect.FindProperty("outerGlowColor").colorValue = new Color(1f, 1f, 1f, 0.85f);
            serializedEffect.FindProperty("outerGlowStrength").floatValue = 0.66f;
            serializedEffect.FindProperty("outerGlowRadiusPixels").floatValue = 65f;
            serializedEffect.FindProperty("outerGlowSpread").floatValue = 0.18f;
            serializedEffect.FindProperty("outerGlowFalloff").floatValue = 1.35f;
            serializedEffect.FindProperty("outerGlowNoise").floatValue = 0.16f;
            serializedEffect.FindProperty("glassColor").colorValue = new Color(1f, 1f, 1f, 0.28f);
            serializedEffect.FindProperty("glassStrength").floatValue = 0.16f;
            serializedEffect.FindProperty("dynamicMaskStrength").floatValue = 1f;
            serializedEffect.FindProperty("hitWaveDuration").floatValue = 0.46f;
            serializedEffect.FindProperty("hitWaveStrength").floatValue = 0.026f;
            serializedEffect.FindProperty("hitWaveFrequency").floatValue = 34f;
            serializedEffect.FindProperty("hitWaveTravel").floatValue = 1.25f;
            serializedEffect.FindProperty("hitScalePulse").floatValue = 0.034f;
            serializedEffect.FindProperty("dissolveDuration").floatValue = 0.72f;
            serializedEffect.FindProperty("dissolveScaleExpand").floatValue = 0.14f;
            serializedEffect.FindProperty("faceCamera").boolValue = true;
            serializedEffect.FindProperty("useUnscaledTime").boolValue = false;

            SerializedProperty layersProperty = serializedEffect.FindProperty("layers");
            layersProperty.arraySize = assignments.Count;
            for (int i = 0; i < assignments.Count; i++)
            {
                LayerAssignment assignment = assignments[i];
                int logicalLayer = ResolveLogicalLayerNumber(assignment.layer.name);
                SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
                layerProperty.FindPropertyRelative("label").stringValue = assignment.layer.name;
                layerProperty.FindPropertyRelative("layerTransform").objectReferenceValue = assignment.transform;
                layerProperty.FindPropertyRelative("layerRenderer").objectReferenceValue = assignment.renderer;
                layerProperty.FindPropertyRelative("rotationDegreesPerSecond").floatValue = ResolveRotationSpeed(logicalLayer);
                layerProperty.FindPropertyRelative("opacityMultiplier").floatValue = Mathf.Clamp01(assignment.layer.opacity);
            }

            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        private static Camera CreatePreviewCamera()
        {
            GameObject cameraObject = new GameObject("Huxinjing Preview Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -7f), Quaternion.identity);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.14f, 0.13f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 3.05f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 40f;
            camera.allowHDR = true;
            return camera;
        }

        private static void CreatePreviewBackdrop()
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Preview Dark Backdrop";
            backdrop.transform.position = new Vector3(0f, 0f, 2.25f);
            backdrop.transform.localScale = new Vector3(8.8f, 5.4f, 1f);
            UnityEngine.Object.DestroyImmediate(backdrop.GetComponent<Collider>());

            Renderer renderer = backdrop.GetComponent<Renderer>();
            renderer.sharedMaterial = CreatePreviewMaterial("M_HuxinjingPreview_Backdrop", new Color(0.15f, 0.15f, 0.13f, 1f));
        }

        private static void CreatePreviewTarget()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Preview Shield Target";
            target.transform.position = new Vector3(0f, -0.2f, 1.15f);
            target.transform.localScale = new Vector3(0.68f, 0.82f, 0.68f);
            target.GetComponent<Renderer>().sharedMaterial = CreatePreviewMaterial("M_HuxinjingPreview_Target", new Color(0.06f, 0.07f, 0.07f, 1f));
        }

        private static void CreatePreviewShield(Camera previewCamera)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Huxinjing shield prefab is missing: " + PrefabPath);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Huxinjing Shield Preview";
            instance.transform.position = Vector3.zero;

            HuxinjingShieldEffect effect = instance.GetComponent<HuxinjingShieldEffect>();
            if (effect != null)
            {
                SerializedObject serializedEffect = new SerializedObject(effect);
                serializedEffect.FindProperty("targetCamera").objectReferenceValue = previewCamera;
                serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Material CreatePreviewMaterial(string materialName, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + materialName + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static LayerData[] SortLayersBackToFront(LayerData[] layers)
        {
            LayerData[] copy = new LayerData[layers.Length];
            Array.Copy(layers, copy, layers.Length);
            Array.Sort(copy, CompareRenderLayer);
            return copy;
        }

        private static int CompareRenderLayer(LayerData left, LayerData right)
        {
            return ResolveSortingOrder(ResolveLogicalLayerNumber(left.name))
                .CompareTo(ResolveSortingOrder(ResolveLogicalLayerNumber(right.name)));
        }

        private static int CompareLogicalLayer(LayerAssignment left, LayerAssignment right)
        {
            return ResolveLogicalLayerNumber(left.layer.name).CompareTo(ResolveLogicalLayerNumber(right.layer.name));
        }

        private static int ResolveLogicalLayerNumber(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                return 1;
            }

            if (layerName.EndsWith("3", StringComparison.Ordinal))
            {
                return 3;
            }

            if (layerName.EndsWith("2", StringComparison.Ordinal))
            {
                return 2;
            }

            return 1;
        }

        private static int ResolveSortingOrder(int logicalLayer)
        {
            switch (logicalLayer)
            {
                case 1:
                    return 2;
                case 2:
                    return 1;
                default:
                    return 0;
            }
        }

        private static float ResolveRotationSpeed(int logicalLayer)
        {
            switch (logicalLayer)
            {
                case 1:
                    return -28f;
                case 2:
                    return 16f;
                default:
                    return -8f;
            }
        }

        private static LayerData RequireLayer(LayoutData layout, int logicalLayer)
        {
            for (int i = 0; i < layout.layers.Length; i++)
            {
                if (ResolveLogicalLayerNumber(layout.layers[i].name) == logicalLayer)
                {
                    return layout.layers[i];
                }
            }

            throw new InvalidOperationException("Huxinjing layout is missing logical layer " + logicalLayer.ToString() + ".");
        }

        private static RectInt ResolveGroupRect(LayoutData layout)
        {
            bool hasRect = false;
            int left = 0;
            int top = 0;
            int right = 0;
            int bottom = 0;

            for (int i = 0; i < layout.layers.Length; i++)
            {
                LayerData layer = layout.layers[i];
                if (layer.bbox == null || layer.bbox.Length < 4)
                {
                    continue;
                }

                if (!hasRect)
                {
                    left = layer.bbox[0];
                    top = layer.bbox[1];
                    right = layer.bbox[2];
                    bottom = layer.bbox[3];
                    hasRect = true;
                    continue;
                }

                left = Mathf.Min(left, layer.bbox[0]);
                top = Mathf.Min(top, layer.bbox[1]);
                right = Mathf.Max(right, layer.bbox[2]);
                bottom = Mathf.Max(bottom, layer.bbox[3]);
            }

            if (!hasRect)
            {
                throw new InvalidOperationException("Huxinjing layout is missing layer bounding boxes.");
            }

            return new RectInt(left, top, right - left, bottom - top);
        }

        private static Vector4 ResolveLayerRect(LayerData layer, RectInt groupRect)
        {
            if (layer.bbox == null || layer.bbox.Length < 4)
            {
                throw new InvalidOperationException("Huxinjing layer is missing bbox data: " + layer.name + ".");
            }

            int layerLeft = layer.bbox[0] - groupRect.xMin;
            int layerTop = layer.bbox[1] - groupRect.yMin;
            int layerWidth = layer.bbox[2] - layer.bbox[0];
            int layerHeight = layer.bbox[3] - layer.bbox[1];
            int layerBottom = groupRect.height - layerTop - layerHeight;
            return new Vector4(layerLeft, layerBottom, layerWidth, layerHeight);
        }

        private static Vector4 ResolveLayerPivot(LayerData layer, RectInt groupRect)
        {
            if (layer.pivotPixels != null && layer.pivotPixels.Length >= 2)
            {
                return new Vector4(layer.pivotPixels[0], layer.pivotPixels[1], 0f, 0f);
            }

            Vector4 rect = ResolveLayerRect(layer, groupRect);
            return new Vector4(rect.x + rect.z * 0.5f, rect.y + rect.w * 0.5f, 0f, 0f);
        }

        private static Vector3 ResolveGroupLocalPosition(LayoutData layout)
        {
            RectInt groupRect = ResolveGroupRect(layout);
            float x = (groupRect.xMin + groupRect.width * 0.5f - layout.canvasWidth * 0.5f) / PixelsPerUnit;
            float y = -(groupRect.yMin + groupRect.height * 0.5f - layout.canvasHeight * 0.5f) / PixelsPerUnit;
            return new Vector3(x, y, 0f);
        }

        private static Vector3 ResolveLayerLocalPosition(LayerData layer)
        {
            if (layer.centerOffsetPixels == null || layer.centerOffsetPixels.Length < 2)
            {
                return Vector3.zero;
            }

            return new Vector3(
                layer.centerOffsetPixels[0] / PixelsPerUnit,
                layer.centerOffsetPixels[1] / PixelsPerUnit,
                0f);
        }

        private static string LayerTexturePath(LayerData layer)
        {
            return Root + "/" + layer.file.Replace('\\', '/');
        }

        private static string ProjectPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, "..", assetPath).Replace('\\', '/');
        }

        private static bool NeedsGeneratedAssetRefresh()
        {
            string markerPath = ProjectPath(GeneratedAssetVersionPath);
            if (!File.Exists(markerPath))
            {
                return true;
            }

            string versionText = File.ReadAllText(markerPath).Trim();
            return !int.TryParse(versionText, out int version) || version < GeneratedAssetVersion;
        }

        private static void RefreshGeneratedAssetsIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RefreshGeneratedAssetsIfNeeded;
                return;
            }

            if (!NeedsGeneratedAssetRefresh())
            {
                return;
            }

            try
            {
                InstallAssetsAndPrefab();
            }
            catch (Exception exception)
            {
                Debug.LogError("Failed to refresh generated Huxinjing shield assets: " + exception);
            }
        }

        private static void WriteGeneratedAssetVersion()
        {
            string markerPath = ProjectPath(GeneratedAssetVersionPath);
            string directory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(markerPath, GeneratedAssetVersion.ToString());
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        [Serializable]
        private sealed class LayoutData
        {
            public int canvasWidth;
            public int canvasHeight;
            public float groupOpacity;
            public LayerData[] layers;
        }

        [Serializable]
        private sealed class LayerData
        {
            public string name;
            public string file;
            public float opacity;
            public int width;
            public int height;
            public int[] bbox;
            public float[] centerOffsetPixels;
            public float[] pivotPixels;
        }

        private readonly struct LayerAssignment
        {
            public readonly LayerData layer;
            public readonly Transform transform;
            public readonly SpriteRenderer renderer;

            public LayerAssignment(LayerData layer, Transform transform, SpriteRenderer renderer)
            {
                this.layer = layer;
                this.transform = transform;
                this.renderer = renderer;
            }
        }
    }

    [CustomEditor(typeof(HuxinjingShieldEffect))]
    public sealed class HuxinjingShieldEffectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Full"))
                {
                    ForEachEffect(effect => effect.SetShieldFull());
                }

                if (GUILayout.Button("Half"))
                {
                    ForEachEffect(effect => effect.SetShieldHalf());
                }

                if (GUILayout.Button("Empty"))
                {
                    ForEachEffect(effect => effect.SetShieldEmpty());
                }
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Release"))
                    {
                        ForEachEffect(effect => effect.PlayRelease());
                    }

                    if (GUILayout.Button("Hit Wave"))
                    {
                        ForEachEffect(effect => effect.PlayHit());
                    }

                    if (GUILayout.Button("Dissolve"))
                    {
                        ForEachEffect(effect => effect.PlayDissolve());
                    }
                }
            }

            if (GUILayout.Button("Reset Visual State"))
            {
                ForEachEffect(effect => effect.ResetVisualState());
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Release, hit wave, and dissolve animations tick in Play Mode. Full/Half/Empty update static opacity in Edit Mode.", MessageType.Info);
            }
        }

        private void ForEachEffect(Action<HuxinjingShieldEffect> action)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                HuxinjingShieldEffect effect = targets[i] as HuxinjingShieldEffect;
                if (effect == null)
                {
                    continue;
                }

                Undo.RecordObject(effect, "Preview Huxinjing Shield");
                action(effect);
                EditorUtility.SetDirty(effect);
            }
        }
    }
}
