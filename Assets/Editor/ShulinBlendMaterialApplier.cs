using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NewFPG.EditorTools
{
    public static class ShulinBlendMaterialApplier
    {
        private const string ScenePath = "Assets/Scenes/ShulinDemoScene.unity";
        private const string ModelPath = "Assets/Art/Scenes/shulin\u6f14\u793a_unity.fbx";
        private const string ManifestPath = "Assets/Art/Scenes/ShulinBlendMaterials/blender_materials.json";
        private const string GeneratedMapDir = "Assets/Art/Scenes/ShulinBlendMaterials/GeneratedMaps";
        private const string BlenderKitGrassSourceDir = "Assets/Art/Scenes/ShulinBlendTextures/BlenderKitGrass/Source";
        private const string ImportedModelName = "Imported_shulin_blend";

        private static readonly HashSet<string> CutoutMaterialNames = new HashSet<string>
        {
            "di",
            "di\u4e0a",
            "QQ\u56fe\u724720260605200243",
            "\u4e2d\u77f3\u5934",
            "\u5927\u77f3\u5934",
            "\u5929",
            "\u623f\u5b501",
            "\u623f\u5b502",
            "\u6811",
            "\u704c\u6728-\u4e2d",
            "\u704c\u6728\u4f4e",
            "\u704c\u6728\u4f4e-\u6697",
            "\u77ee\u77f3\u5934",
            "\u8fdc\u5c71",
        };

        private static readonly HashSet<string> SoftTransparentMaterialNames = new HashSet<string>
        {
            "\u96fe",
        };

        private static readonly Dictionary<string, BlenderKitGrassTextureSet> BlenderKitGrassMaterials =
            new Dictionary<string, BlenderKitGrassTextureSet>
            {
                {
                    "M Grass Generic",
                    new BlenderKitGrassTextureSet(
                        "T_Grass_Generic_a1_BC.jpg",
                        "T_Grass_Generic_a1_OP.jpg",
                        "T_Grass_Generic_a1_NM.jpg",
                        "T_Grass_Generic_a1_ORS.jpg")
                },
                {
                    "M Grass Small Cover",
                    new BlenderKitGrassTextureSet(
                        "T_Grass_Small_Cover_BC.jpg",
                        "T_Grass_Small_Cover_OP.jpg",
                        "T_Grass_Small_Cover_NM.jpg",
                        "T_Grass_Small_Cover_ORM.jpg")
                },
                {
                    "M Grass Tabosa",
                    new BlenderKitGrassTextureSet(
                        "T_Grass_Tabosa_BC.jpg",
                        "T_Grass_Tabosa_OP.jpg",
                        "T_Grass_Tabosa_NM.jpg",
                        "T_Grass_Tabosa_ORS.jpg")
                },
            };

        [MenuItem("Tools/NewFPG/Apply Shulin Blend Materials", false, 2111)]
        public static void ApplyMaterials()
        {
            MaterialManifest manifest = LoadManifest();
            if (manifest.materials == null || manifest.materials.Count == 0)
            {
                throw new InvalidOperationException($"No materials were found in {ManifestPath}.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AssetDatabase.StartAssetEditing();
            try
            {
                EnsureTextureImportSettings(manifest);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Dictionary<string, Material> materialsByBlenderName = CreateOrUpdateMaterials(manifest);
            ApplyMaterialsToOpenScene(manifest, materialsByBlenderName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[ShulinBlendMaterialApplier] Applied {materialsByBlenderName.Count} Blender material(s) to {ScenePath}.");
        }

        public static void ApplyFromCommandLine()
        {
            ApplyMaterials();
        }

        private static MaterialManifest LoadManifest()
        {
            TextAsset manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (manifestAsset == null)
            {
                throw new FileNotFoundException($"Could not load Blender material manifest at {ManifestPath}.");
            }

            MaterialManifest manifest = JsonUtility.FromJson<MaterialManifest>(manifestAsset.text);
            if (manifest == null)
            {
                throw new InvalidOperationException($"Could not parse {ManifestPath}.");
            }

            return manifest;
        }

        private static void EnsureTextureImportSettings(MaterialManifest manifest)
        {
            foreach (MaterialRecord record in manifest.materials)
            {
                if (TryGetBlenderKitGrassTextures(record, out BlenderKitGrassTextureSet grassTextures))
                {
                    ConfigureTexture(grassTextures.BaseColorPath, TextureRole.BaseColor, false);
                    ConfigureTexture(grassTextures.OpacityPath, TextureRole.LinearData, false);
                    ConfigureTexture(grassTextures.NormalPath, TextureRole.Normal, false);
                    ConfigureTexture(grassTextures.OrmPath, TextureRole.LinearData, false);
                }

                string baseMap = ResolveTexturePath(record.baseMap, record.name);
                if (!string.IsNullOrEmpty(baseMap))
                {
                    ConfigureTexture(baseMap, TextureRole.BaseColor, IsAlphaMaterial(record));
                }

                string normalMap = ResolveTexturePath(record.normalMap, record.name);
                if (!string.IsNullOrEmpty(normalMap))
                {
                    ConfigureTexture(normalMap, TextureRole.Normal, false);
                }

                string metallicMap = ResolveTexturePath(record.metallicMap, record.name);
                if (!string.IsNullOrEmpty(metallicMap))
                {
                    ConfigureTexture(metallicMap, TextureRole.LinearData, false);
                }

                string roughnessMap = ResolveTexturePath(record.roughnessMap, record.name);
                if (!string.IsNullOrEmpty(roughnessMap))
                {
                    ConfigureTexture(roughnessMap, TextureRole.LinearData, false);
                }

                string occlusionMap = ResolveTexturePath(record.occlusionMap, record.name);
                if (!string.IsNullOrEmpty(occlusionMap))
                {
                    ConfigureTexture(occlusionMap, TextureRole.LinearData, false);
                }
            }
        }

        private static Dictionary<string, Material> CreateOrUpdateMaterials(MaterialManifest manifest)
        {
            var materials = new Dictionary<string, Material>();
            Directory.CreateDirectory(ProjectPath("Assets/Art/Scenes/ShulinBlendMaterials"));
            Directory.CreateDirectory(ProjectPath(GeneratedMapDir));

            foreach (MaterialRecord record in manifest.materials)
            {
                string materialPath = string.IsNullOrEmpty(record.materialPath)
                    ? $"Assets/Art/Scenes/ShulinBlendMaterials/{SanitizeAssetName(record.name)}.mat"
                    : record.materialPath;

                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(ChooseShader(record));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else
                {
                    material.shader = ChooseShader(record);
                }

                ConfigureMaterial(material, record);
                materials[record.name] = material;
            }

            return materials;
        }

        private static Shader ChooseShader(MaterialRecord record)
        {
            bool unlit = ShouldUseUnlit(record);
            string shaderName = unlit
                ? "Universal Render Pipeline/Unlit"
                : "Universal Render Pipeline/Lit";

            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find(unlit ? "Unlit/Transparent" : "Standard");
        }

        private static void ConfigureMaterial(Material material, MaterialRecord record)
        {
            bool alpha = IsAlphaMaterial(record);
            bool transparent = IsTransparentMaterial(record);
            bool unlit = ShouldUseUnlit(record);

            bool blenderKitGrass = TryGetBlenderKitGrassTextures(record, out BlenderKitGrassTextureSet grassTextures);
            Texture2D baseMap = blenderKitGrass
                ? BuildBlenderKitGrassBaseAlphaMap(record, grassTextures)
                : LoadTexture(ResolveTexturePath(record.baseMap, record.name));
            Texture2D normalMap = blenderKitGrass
                ? LoadTexture(grassTextures.NormalPath)
                : LoadTexture(ResolveTexturePath(record.normalMap, record.name));
            Texture2D occlusionMap = blenderKitGrass
                ? BuildBlenderKitGrassOcclusionMap(record, grassTextures)
                : LoadTexture(ResolveTexturePath(record.occlusionMap, record.name));

            Color baseColor = ColorFromArray(record.baseColor, Color.white);
            if (IsSoftTransparentMaterial(record))
            {
                baseColor.a = 1f;
            }

            float smoothness = Mathf.Clamp01(1f - Mathf.Clamp01(record.roughness));

            SetTexture(material, "_BaseMap", baseMap);
            SetTexture(material, "_MainTex", baseMap);
            SetColor(material, "_BaseColor", baseColor);
            SetColor(material, "_Color", baseColor);

            if (!unlit)
            {
                SetFloat(material, "_Metallic", Mathf.Clamp01(record.metallic));
                SetFloat(material, "_Smoothness", smoothness);

                Texture2D metallicSmoothness = blenderKitGrass
                    ? BuildBlenderKitGrassMetallicSmoothnessMap(record, grassTextures)
                    : BuildMetallicSmoothnessMap(record);
                SetTexture(material, "_MetallicGlossMap", metallicSmoothness);
                SetTexture(material, "_BumpMap", normalMap);
                SetFloat(material, "_BumpScale", normalMap != null ? 1f : 0f);
                SetTexture(material, "_OcclusionMap", occlusionMap);
                SetFloat(material, "_OcclusionStrength", occlusionMap != null ? 1f : 0f);

                SetKeyword(material, "_NORMALMAP", normalMap != null);
                SetKeyword(material, "_METALLICSPECGLOSSMAP", metallicSmoothness != null);
                SetKeyword(material, "_OCCLUSIONMAP", occlusionMap != null);
            }

            ConfigureSurface(material, record, alpha, transparent);
            ConfigureEmission(material, record);

            material.doubleSidedGI = alpha;
            material.renderQueue = transparent
                ? (int)UnityEngine.Rendering.RenderQueue.Transparent
                : alpha ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;

            EditorUtility.SetDirty(material);
        }

        private static void ConfigureSurface(Material material, MaterialRecord record, bool alpha, bool transparent)
        {
            if (transparent)
            {
                SetFloat(material, "_Surface", 1f);
                SetFloat(material, "_Blend", 0f);
                SetFloat(material, "_AlphaClip", 0f);
                SetFloat(material, "_Cutoff", 0.01f);
                SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                SetFloat(material, "_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                SetFloat(material, "_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                SetFloat(material, "_ZWrite", 0f);
                SetFloat(material, "_Cull", 0f);
                SetFloat(material, "_AlphaToMask", 0f);
                SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", true);
                SetKeyword(material, "_ALPHATEST_ON", false);
                material.SetOverrideTag("RenderType", "Transparent");
                return;
            }

            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", false);

            if (alpha)
            {
                SetFloat(material, "_AlphaClip", 1f);
                SetFloat(material, "_Cutoff", GetAlphaCutoff(record));
                SetFloat(material, "_Cull", 0f);
                SetFloat(material, "_AlphaToMask", 1f);
                SetKeyword(material, "_ALPHATEST_ON", true);
                material.SetOverrideTag("RenderType", "TransparentCutout");
            }
            else
            {
                SetFloat(material, "_AlphaClip", 0f);
                SetFloat(material, "_Cutoff", 0.5f);
                SetFloat(material, "_Cull", 2f);
                SetFloat(material, "_AlphaToMask", 0f);
                SetKeyword(material, "_ALPHATEST_ON", false);
                material.SetOverrideTag("RenderType", "Opaque");
            }
        }

        private static void ConfigureEmission(Material material, MaterialRecord record)
        {
            Color emission = ColorFromArray(record.emissionColor, Color.black);
            float strength = Mathf.Max(0f, record.emissionStrength);
            bool enabled = strength > 0.0001f && emission.maxColorComponent > 0.0001f;
            Color finalEmission = enabled ? emission.linear * strength : Color.black;

            SetColor(material, "_EmissionColor", finalEmission);
            SetKeyword(material, "_EMISSION", enabled);
            if (enabled)
            {
                material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            else
            {
                material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        private static void ApplyMaterialsToOpenScene(
            MaterialManifest manifest,
            Dictionary<string, Material> materialsByBlenderName)
        {
            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool openedScene = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                openedScene = true;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"Could not open {ScenePath}.");
            }

            GameObject importedRoot = FindInScene(scene, ImportedModelName);
            if (importedRoot == null)
            {
                throw new InvalidOperationException($"Could not find {ImportedModelName} in {ScenePath}.");
            }

            PromoteBlenderCamera(scene);

            Dictionary<string, ObjectRecord> objectByName = manifest.objects
                .Where(record => !string.IsNullOrEmpty(record.name))
                .GroupBy(record => record.name)
                .ToDictionary(group => group.Key, group => group.First());

            Dictionary<string, ObjectRecord> objectByMesh = manifest.objects
                .Where(record => !string.IsNullOrEmpty(record.mesh))
                .GroupBy(record => record.mesh)
                .ToDictionary(group => group.Key, group => group.First());

            int rendererCount = 0;
            int slotCount = 0;
            foreach (Renderer renderer in importedRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material[] assigned = renderer.sharedMaterials;
                ObjectRecord objectRecord = ResolveObjectRecord(renderer, objectByName, objectByMesh);

                for (int i = 0; i < assigned.Length; i++)
                {
                    string blenderMaterialName = null;
                    if (objectRecord != null &&
                        objectRecord.materials != null &&
                        i < objectRecord.materials.Count)
                    {
                        blenderMaterialName = objectRecord.materials[i];
                    }

                    if (string.IsNullOrEmpty(blenderMaterialName) && assigned[i] != null)
                    {
                        blenderMaterialName = NormalizeMaterialName(assigned[i].name);
                    }

                    if (!string.IsNullOrEmpty(blenderMaterialName) &&
                        materialsByBlenderName.TryGetValue(blenderMaterialName, out Material material))
                    {
                        assigned[i] = material;
                        slotCount++;
                    }
                }

                renderer.sharedMaterials = assigned;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                rendererCount++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[ShulinBlendMaterialApplier] Updated {slotCount} material slot(s) across {rendererCount} renderer(s).");

            if (openedScene)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private static ObjectRecord ResolveObjectRecord(
            Renderer renderer,
            Dictionary<string, ObjectRecord> objectByName,
            Dictionary<string, ObjectRecord> objectByMesh)
        {
            string objectName = NormalizeObjectName(renderer.gameObject.name);
            if (objectByName.TryGetValue(objectName, out ObjectRecord byName))
            {
                return byName;
            }

            Mesh mesh = null;
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                mesh = meshFilter.sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }

            if (mesh != null && objectByMesh.TryGetValue(NormalizeObjectName(mesh.name), out ObjectRecord byMesh))
            {
                return byMesh;
            }

            return null;
        }

        private static Texture2D BuildBlenderKitGrassBaseAlphaMap(
            MaterialRecord record,
            BlenderKitGrassTextureSet grassTextures)
        {
            string outputPath = $"{GeneratedMapDir}/{SanitizeAssetName(record.name)}_BaseAlpha.png";
            string outputFullPath = ProjectPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? ProjectPath(GeneratedMapDir));

            Texture2D baseTexture = ReadTexture(grassTextures.BaseColorPath);
            Texture2D opacityTexture = ReadTexture(grassTextures.OpacityPath);
            if (baseTexture == null || opacityTexture == null)
            {
                DestroyTexture(baseTexture);
                DestroyTexture(opacityTexture);
                return null;
            }

            int width = baseTexture.width;
            int height = baseTexture.height;
            Texture2D packed = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    Color color = baseTexture.GetPixelBilinear(u, v);
                    float alpha = opacityTexture.GetPixelBilinear(u, v).grayscale;
                    packed.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
            }

            packed.Apply();
            File.WriteAllBytes(outputFullPath, packed.EncodeToPNG());
            DestroyTexture(packed);
            DestroyTexture(baseTexture);
            DestroyTexture(opacityTexture);

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(outputPath, TextureRole.BaseColor, true);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        private static Texture2D BuildBlenderKitGrassMetallicSmoothnessMap(
            MaterialRecord record,
            BlenderKitGrassTextureSet grassTextures)
        {
            string outputPath = $"{GeneratedMapDir}/{SanitizeAssetName(record.name)}_MetallicSmoothness.png";
            string outputFullPath = ProjectPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? ProjectPath(GeneratedMapDir));

            Texture2D ormTexture = ReadTexture(grassTextures.OrmPath);
            if (ormTexture == null)
            {
                return null;
            }

            Texture2D packed = new Texture2D(ormTexture.width, ormTexture.height, TextureFormat.RGBA32, true, true);
            for (int y = 0; y < ormTexture.height; y++)
            {
                for (int x = 0; x < ormTexture.width; x++)
                {
                    Color orm = ormTexture.GetPixel(x, y);
                    float roughness = Mathf.Clamp01(orm.g);
                    packed.SetPixel(x, y, new Color(0f, 0f, 0f, 1f - roughness));
                }
            }

            packed.Apply();
            File.WriteAllBytes(outputFullPath, packed.EncodeToPNG());
            DestroyTexture(packed);
            DestroyTexture(ormTexture);

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(outputPath, TextureRole.LinearData, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        private static Texture2D BuildBlenderKitGrassOcclusionMap(
            MaterialRecord record,
            BlenderKitGrassTextureSet grassTextures)
        {
            string outputPath = $"{GeneratedMapDir}/{SanitizeAssetName(record.name)}_Occlusion.png";
            string outputFullPath = ProjectPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? ProjectPath(GeneratedMapDir));

            Texture2D ormTexture = ReadTexture(grassTextures.OrmPath);
            if (ormTexture == null)
            {
                return null;
            }

            Texture2D packed = new Texture2D(ormTexture.width, ormTexture.height, TextureFormat.RGBA32, true, true);
            for (int y = 0; y < ormTexture.height; y++)
            {
                for (int x = 0; x < ormTexture.width; x++)
                {
                    float occlusion = ormTexture.GetPixel(x, y).r;
                    packed.SetPixel(x, y, new Color(occlusion, occlusion, occlusion, 1f));
                }
            }

            packed.Apply();
            File.WriteAllBytes(outputFullPath, packed.EncodeToPNG());
            DestroyTexture(packed);
            DestroyTexture(ormTexture);

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(outputPath, TextureRole.LinearData, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        private static void PromoteBlenderCamera(Scene scene)
        {
            Camera preferredCamera = FindSceneCamera(scene, "Camera") ?? FindSceneCamera(scene, "Camera.001");
            if (preferredCamera == null)
            {
                Debug.LogWarning("[ShulinBlendMaterialApplier] Could not find a Blender camera to promote.");
                return;
            }

            foreach (Camera camera in GetSceneCameras(scene))
            {
                bool isPreferred = camera == preferredCamera;
                camera.gameObject.SetActive(isPreferred);
                camera.enabled = isPreferred;

                if (camera.gameObject.CompareTag("MainCamera"))
                {
                    camera.gameObject.tag = "Untagged";
                }

                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = isPreferred;
                }
            }

            preferredCamera.gameObject.SetActive(true);
            preferredCamera.enabled = true;
            preferredCamera.gameObject.tag = "MainCamera";
            preferredCamera.clearFlags = CameraClearFlags.Skybox;
            preferredCamera.nearClipPlane = Mathf.Max(0.01f, preferredCamera.nearClipPlane);
            preferredCamera.farClipPlane = Mathf.Max(1000f, preferredCamera.farClipPlane);

            AudioListener preferredListener = preferredCamera.GetComponent<AudioListener>();
            if (preferredListener == null)
            {
                preferredListener = preferredCamera.gameObject.AddComponent<AudioListener>();
            }

            preferredListener.enabled = true;
        }

        private static Camera FindSceneCamera(Scene scene, string cameraName)
        {
            return GetSceneCameras(scene)
                .FirstOrDefault(camera => string.Equals(camera.name, cameraName, StringComparison.Ordinal));
        }

        private static IEnumerable<Camera> GetSceneCameras(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    yield return camera;
                }
            }
        }

        private static Texture2D BuildMetallicSmoothnessMap(MaterialRecord record)
        {
            string metallicPath = ResolveTexturePath(record.metallicMap, record.name);
            string roughnessPath = ResolveTexturePath(record.roughnessMap, record.name);
            if (string.IsNullOrEmpty(metallicPath) && string.IsNullOrEmpty(roughnessPath))
            {
                return null;
            }

            string outputPath = $"{GeneratedMapDir}/{SanitizeAssetName(record.name)}_MetallicSmoothness.png";
            string outputFullPath = ProjectPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? ProjectPath(GeneratedMapDir));

            Texture2D metallicTexture = ReadTexture(metallicPath);
            Texture2D roughnessTexture = ReadTexture(roughnessPath);
            int width = Mathf.Max(metallicTexture != null ? metallicTexture.width : 0, roughnessTexture != null ? roughnessTexture.width : 0);
            int height = Mathf.Max(metallicTexture != null ? metallicTexture.height : 0, roughnessTexture != null ? roughnessTexture.height : 0);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            Texture2D packed = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            float scalarMetallic = Mathf.Clamp01(record.metallic);
            float scalarSmoothness = Mathf.Clamp01(1f - Mathf.Clamp01(record.roughness));
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float metallic = metallicTexture != null
                        ? metallicTexture.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height).grayscale
                        : scalarMetallic;
                    float roughness = roughnessTexture != null
                        ? roughnessTexture.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height).grayscale
                        : 1f - scalarSmoothness;
                    float smoothness = 1f - roughness;
                    packed.SetPixel(x, y, new Color(metallic, metallic, metallic, smoothness));
                }
            }

            packed.Apply();
            File.WriteAllBytes(outputFullPath, packed.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(packed);
            if (metallicTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(metallicTexture);
            }
            if (roughnessTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(roughnessTexture);
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(outputPath, TextureRole.LinearData, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        private static Texture2D ReadTexture(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            string fullPath = ProjectPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ConfigureTexture(string assetPath, TextureRole role, bool alpha)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool dirty = false;
            TextureImporterType type = role == TextureRole.Normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            if (importer.textureType != type)
            {
                importer.textureType = type;
                dirty = true;
            }

            bool srgb = role == TextureRole.BaseColor;
            if (importer.sRGBTexture != srgb)
            {
                importer.sRGBTexture = srgb;
                dirty = true;
            }

            if (importer.alphaIsTransparency != alpha)
            {
                importer.alphaIsTransparency = alpha;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static string ResolveTexturePath(string path, string materialName)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
            {
                return path;
            }

            string fileName = Path.GetFileName(path);
            string stem = Path.GetFileNameWithoutExtension(path);
            foreach (string key in CandidateNames(fileName, stem, materialName))
            {
                string found = FindTextureAsset(key);
                if (!string.IsNullOrEmpty(found))
                {
                    return found;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<string> CandidateNames(params string[] values)
        {
            foreach (string value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                yield return value;
                yield return Regex.Replace(value, "\\.\\d+$", string.Empty);
                yield return value.Replace("_", " ");
            }
        }

        private static string FindTextureAsset(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            string query = $"{Path.GetFileNameWithoutExtension(key)} t:Texture2D";
            foreach (string guid in AssetDatabase.FindAssets(query, new[] { "Assets/Art/Scenes" }))
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guid);
                string candidateName = Path.GetFileNameWithoutExtension(candidate);
                if (string.Equals(candidateName, Path.GetFileNameWithoutExtension(key), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(candidate), key, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static bool ShouldUseUnlit(MaterialRecord record)
        {
            if (IsBlenderKitGrassMaterial(record))
            {
                return false;
            }

            return record.unlit ||
                   IsTransparentMaterial(record) ||
                   CutoutMaterialNames.Contains(record.name);
        }

        private static bool IsAlphaMaterial(MaterialRecord record)
        {
            return string.Equals(record.alphaMode, "Cutout", StringComparison.OrdinalIgnoreCase) ||
                   IsTransparentMaterial(record) ||
                   IsBlenderKitGrassMaterial(record) ||
                   CutoutMaterialNames.Contains(record.name);
        }

        private static bool IsTransparentMaterial(MaterialRecord record)
        {
            return string.Equals(record.alphaMode, "Transparent", StringComparison.OrdinalIgnoreCase) ||
                   IsSoftTransparentMaterial(record);
        }

        private static bool IsSoftTransparentMaterial(MaterialRecord record)
        {
            return record != null && SoftTransparentMaterialNames.Contains(record.name);
        }

        private static bool IsBlenderKitGrassMaterial(MaterialRecord record)
        {
            return record != null && BlenderKitGrassMaterials.ContainsKey(record.name);
        }

        private static bool TryGetBlenderKitGrassTextures(
            MaterialRecord record,
            out BlenderKitGrassTextureSet grassTextures)
        {
            if (record != null && BlenderKitGrassMaterials.TryGetValue(record.name, out grassTextures))
            {
                return true;
            }

            grassTextures = null;
            return false;
        }

        private static float GetAlphaCutoff(MaterialRecord record)
        {
            return IsBlenderKitGrassMaterial(record) ? 0.35f : 0.12f;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == name);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static string NormalizeMaterialName(string name)
        {
            return string.IsNullOrEmpty(name)
                ? string.Empty
                : name.Replace(" (Instance)", string.Empty).Trim();
        }

        private static string NormalizeObjectName(string name)
        {
            return string.IsNullOrEmpty(name)
                ? string.Empty
                : name.Replace(" (Instance)", string.Empty).Trim();
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unnamed";
            }

            string sanitized = Regex.Replace(value, "[<>:\"/\\\\|?*\\x00-\\x1F]", "_");
            sanitized = Regex.Replace(sanitized.Trim().Trim('.'), "\\s+", "_");
            return string.IsNullOrEmpty(sanitized) ? "Unnamed" : sanitized;
        }

        private static Color ColorFromArray(IReadOnlyList<float> values, Color fallback)
        {
            if (values == null || values.Count < 3)
            {
                return fallback;
            }

            return new Color(
                values[0],
                values[1],
                values[2],
                values.Count > 3 ? values[3] : fallback.a);
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        private static void SetColor(Material material, string property, Color color)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static string ProjectPath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private enum TextureRole
        {
            BaseColor,
            Normal,
            LinearData,
        }

        private sealed class BlenderKitGrassTextureSet
        {
            public BlenderKitGrassTextureSet(
                string baseColorFile,
                string opacityFile,
                string normalFile,
                string ormFile)
            {
                BaseColorPath = $"{BlenderKitGrassSourceDir}/{baseColorFile}";
                OpacityPath = $"{BlenderKitGrassSourceDir}/{opacityFile}";
                NormalPath = $"{BlenderKitGrassSourceDir}/{normalFile}";
                OrmPath = $"{BlenderKitGrassSourceDir}/{ormFile}";
            }

            public string BaseColorPath { get; }
            public string OpacityPath { get; }
            public string NormalPath { get; }
            public string OrmPath { get; }
        }

        [Serializable]
        private sealed class MaterialManifest
        {
            public string blendFile;
            public string scene;
            public List<MaterialRecord> materials = new List<MaterialRecord>();
            public List<ObjectRecord> objects = new List<ObjectRecord>();
        }

        [Serializable]
        private sealed class MaterialRecord
        {
            public string name;
            public string materialPath;
            public bool unlit;
            public string alphaMode;
            public string blendMethod;
            public string baseMap;
            public string normalMap;
            public string metallicMap;
            public string roughnessMap;
            public string occlusionMap;
            public List<float> baseColor;
            public List<float> diffuseColor;
            public List<float> emissionColor;
            public float emissionStrength;
            public float metallic;
            public float roughness = 0.5f;
        }

        [Serializable]
        private sealed class ObjectRecord
        {
            public string name;
            public string mesh;
            public List<string> materials = new List<string>();
        }
    }
}
