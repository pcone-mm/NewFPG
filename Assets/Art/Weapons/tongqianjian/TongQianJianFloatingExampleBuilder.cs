#if UNITY_EDITOR
using NewFPG.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NewFPG.EditorTools
{
    [InitializeOnLoad]
    internal static class TongQianJianFloatingExampleBuilder
    {
        private const string RootFolder = "Assets/Art/Weapons/tongqianjian";
        private const string CoreTexturePath = RootFolder + "/tongqianjian_floating_core.png";
        private const string GlowTexturePath = RootFolder + "/tongqianjian_floating_glow.png";
        private const string CoreMaterialPath = RootFolder + "/M_TongQianJian_FloatingCore.mat";
        private const string GlowMaterialPath = RootFolder + "/M_TongQianJian_FloatingGlow.mat";
        private const string PrefabPath = RootFolder + "/TongQianJian_Floating_Example.prefab";
        private const string CoreShaderName = "NewFPG/VFX/TongQianJian Floating Core";
        private const string GlowShaderName = "NewFPG/VFX/TongQianJian Floating Glow";
        private const int MaxAutomaticRetries = 3;

        private static bool isBuilding;
        private static int automaticRetryCount;
        private static bool retryLimitWarningLogged;

        static TongQianJianFloatingExampleBuilder()
        {
            EditorApplication.delayCall += BuildWhenMissing;
        }

        [MenuItem("NewFPG/VFX/Rebuild TongQianJian Floating Example")]
        private static void RebuildFromMenu()
        {
            automaticRetryCount = 0;
            retryLimitWarningLogged = false;
            BuildExample(false);
        }

        private static void BuildWhenMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            if (automaticRetryCount >= MaxAutomaticRetries)
            {
                LogRetryLimitWarning();
                return;
            }

            automaticRetryCount++;
            BuildExample(true);
        }

        private static void BuildExample(bool allowAutomaticRetry)
        {
            if (isBuilding || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (allowAutomaticRetry)
                {
                    ScheduleAutomaticRetry();
                }
                else
                {
                    Debug.LogWarning("TongQianJian floating example cannot rebuild while Unity is compiling or importing assets.");
                }

                return;
            }

            isBuilding = true;
            GameObject root = null;

            try
            {
                ConfigureSpriteImport(CoreTexturePath);
                ConfigureSpriteImport(GlowTexturePath);

                Sprite coreSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoreTexturePath);
                Sprite glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowTexturePath);
                Shader coreShader = Shader.Find(CoreShaderName);
                Shader glowShader = Shader.Find(GlowShaderName);

                if (coreSprite == null || glowSprite == null || coreShader == null || glowShader == null)
                {
                    if (allowAutomaticRetry)
                    {
                        ScheduleAutomaticRetry();
                    }
                    else
                    {
                        Debug.LogError("TongQianJian floating example is missing a required texture or shader.");
                    }

                    return;
                }

                Material coreMaterial = CreateOrUpdateCoreMaterial(coreShader, coreSprite.texture);
                Material glowMaterial = CreateOrUpdateGlowMaterial(glowShader, glowSprite.texture);

                root = new GameObject("TongQianJian_Floating_Example");
                Transform visual = CreateChild(root.transform, "Visual");
                visual.localRotation = Quaternion.Euler(0f, 0f, -2.5f);

                SortingGroup sortingGroup = visual.gameObject.AddComponent<SortingGroup>();
                sortingGroup.sortingLayerName = "Default";
                sortingGroup.sortingOrder = 0;

                SpriteRenderer depthBack = CreateSpriteLayer(
                    visual,
                    "DepthBack",
                    coreSprite,
                    coreMaterial,
                    new Color(0.11f, 0.035f, 0.008f, 1f),
                    0);
                depthBack.transform.localPosition = new Vector3(0.035f, -0.025f, 0.05f);
                depthBack.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
                depthBack.transform.localScale = Vector3.one * 0.998f;

                SpriteRenderer outerGlow = CreateSpriteLayer(
                    visual,
                    "OuterGlow",
                    glowSprite,
                    glowMaterial,
                    new Color(1f, 0.63f, 0.16f, 0.58f),
                    5);
                outerGlow.transform.localPosition = new Vector3(0f, 0f, 0.025f);
                outerGlow.transform.localScale = Vector3.one * 1.075f;

                SpriteRenderer core = CreateSpriteLayer(
                    visual,
                    "Core",
                    coreSprite,
                    coreMaterial,
                    Color.white,
                    10);
                core.transform.localPosition = Vector3.zero;

                SpriteRenderer innerGlow = CreateSpriteLayer(
                    visual,
                    "InnerGlow",
                    coreSprite,
                    glowMaterial,
                    new Color(1f, 0.92f, 0.56f, 0.48f),
                    15);
                innerGlow.transform.localPosition = new Vector3(0f, 0f, -0.012f);
                innerGlow.transform.localScale = Vector3.one * 1.008f;

                TongQianJianFloatingBody motion = root.AddComponent<TongQianJianFloatingBody>();
                SerializedObject motionObject = new SerializedObject(motion);
                motionObject.FindProperty("visualRoot").objectReferenceValue = visual;
                motionObject.FindProperty("outerGlowRenderer").objectReferenceValue = outerGlow;
                motionObject.FindProperty("innerGlowRenderer").objectReferenceValue = innerGlow;
                motionObject.FindProperty("hoverAmplitude").floatValue = 0.12f;
                motionObject.FindProperty("hoverCyclesPerSecond").floatValue = 0.55f;
                motionObject.FindProperty("depthSwayDegrees").floatValue = 7.5f;
                motionObject.FindProperty("pitchSwayDegrees").floatValue = 1.2f;
                motionObject.FindProperty("rollSwayDegrees").floatValue = 2f;
                motionObject.FindProperty("phaseOffset").floatValue = 0.35f;
                motionObject.FindProperty("glowCyclesPerSecond").floatValue = 0.8f;
                motionObject.FindProperty("glowScaleAmount").floatValue = 0.035f;
                motionObject.FindProperty("glowAlphaAmount").floatValue = 0.18f;
                motionObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Created floating weapon example: {PrefabPath}");
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }

                isBuilding = false;
            }
        }

        private static void ConfigureSpriteImport(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Single
                           || Mathf.Abs(importer.spritePixelsPerUnit - 512f) > 0.01f
                           || !importer.alphaIsTransparency
                           || !importer.mipmapEnabled
                           || !importer.mipMapsPreserveCoverage
                           || Mathf.Abs(importer.alphaTestReferenceValue - 0.14f) > 0.001f
                           || importer.wrapMode != TextureWrapMode.Clamp
                           || importer.filterMode != FilterMode.Bilinear
                           || importer.maxTextureSize != 2048;

            if (!changed)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.14f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ScheduleAutomaticRetry()
        {
            if (automaticRetryCount >= MaxAutomaticRetries)
            {
                LogRetryLimitWarning();
                return;
            }

            EditorApplication.delayCall -= BuildWhenMissing;
            EditorApplication.delayCall += BuildWhenMissing;
        }

        private static void LogRetryLimitWarning()
        {
            if (retryLimitWarningLogged)
            {
                return;
            }

            retryLimitWarningLogged = true;
            Debug.LogWarning(
                $"TongQianJian floating example stopped rebuilding after {MaxAutomaticRetries} automatic attempts. " +
                "Fix missing assets, then run NewFPG/VFX/Rebuild TongQianJian Floating Example.");
        }

        private static Material CreateOrUpdateCoreMaterial(Shader shader, Texture texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_TongQianJian_FloatingCore" };
                AssetDatabase.CreateAsset(material, CoreMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_MainTex", texture);
            material.SetColor("_Tint", Color.white);
            material.SetFloat("_Cutoff", 0.14f);
            material.SetFloat("_EmissionStrength", 1.65f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.enableInstancing = true;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateGlowMaterial(Shader shader, Texture texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_TongQianJian_FloatingGlow" };
                AssetDatabase.CreateAsset(material, GlowMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_MainTex", texture);
            material.SetColor("_Tint", new Color(1f, 0.62f, 0.16f, 1f));
            material.SetFloat("_Intensity", 1.8f);
            material.SetFloat("_Power", 0.85f);
            material.renderQueue = (int)RenderQueue.Transparent + 5;
            material.enableInstancing = true;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static SpriteRenderer CreateSpriteLayer(
            Transform parent,
            string name,
            Sprite sprite,
            Material material,
            Color color,
            int sortingOrder)
        {
            Transform child = CreateChild(parent, name);
            SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = sortingOrder;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }
    }
}
#endif
