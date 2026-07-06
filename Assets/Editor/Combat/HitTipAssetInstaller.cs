using System.Collections.Generic;
using NewFPG.Combat;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    public static class HitTipAssetInstaller
    {
        private const string SourceFolder = "Assets/Art/HUD/Hit_tip";
        private const string ResourcesFolder = "Assets/Resources";
        private const string HitTipsResourcesFolder = "Assets/Resources/HitTips";
        private const string AnimationPath = HitTipsResourcesFolder + "/SO_HTA_Default.asset";
        private const string CatalogPath = HitTipsResourcesFolder + "/SO_HTC_Default.asset";
        private const float BackgroundBorderX = 24f;

        [MenuItem("NewFPG/Combat/Install Hit Tip Assets")]
        public static void Install()
        {
            EnsureFolder(ResourcesFolder);
            EnsureFolder(HitTipsResourcesFolder);
            ConfigureSprites();
            HitTipAnimationConfig animation = LoadOrCreateAnimation();
            HitTipCatalog catalog = LoadOrCreateCatalog(animation);
            EditorUtility.SetDirty(animation);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(catalog);
            Debug.Log("Installed hit tip sprites and default catalog at " + CatalogPath + ".");
        }

        private static void ConfigureSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                TextureImporterSettings textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(textureSettings);
                importer.spriteBorder = IsBackground(path)
                    ? new Vector4(BackgroundBorderX, 0f, BackgroundBorderX, 0f)
                    : Vector4.zero;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        private static HitTipAnimationConfig LoadOrCreateAnimation()
        {
            HitTipAnimationConfig animation = AssetDatabase.LoadAssetAtPath<HitTipAnimationConfig>(AnimationPath);
            if (animation == null)
            {
                animation = ScriptableObject.CreateInstance<HitTipAnimationConfig>();
                animation.ResetToDefaults();
                AssetDatabase.CreateAsset(animation, AnimationPath);
            }

            return animation;
        }

        private static HitTipCatalog LoadOrCreateCatalog(HitTipAnimationConfig animation)
        {
            HitTipCatalog catalog = AssetDatabase.LoadAssetAtPath<HitTipCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HitTipCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            Sprite normalBackground = LoadSprite(SourceFolder + "/di_nomal&critical.png");
            Sprite elementalBackground = LoadSprite(SourceFolder + "/di_elemental.png");
            catalog.SetDefaultAnimation(animation);
            catalog.SetStyles(new[]
            {
                CreateStyle(
                    HitTipStyleId.Normal,
                    normalBackground,
                    LoadDigits(SourceFolder + "/zi_normal"),
                    animation,
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 0.95f, 0.66f, 1f)),
                CreateStyle(
                    HitTipStyleId.Critical,
                    normalBackground,
                    LoadDigits(SourceFolder + "/zi_critcal"),
                    animation,
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 0.98f, 0.5f, 1f)),
                CreateStyle(
                    HitTipStyleId.Elemental,
                    elementalBackground,
                    LoadDigits(SourceFolder + "/zi_elemental"),
                    animation,
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 0.72f, 0.78f, 1f)),
            });
            return catalog;
        }

        private static HitTipStyleConfig CreateStyle(
            HitTipStyleId styleId,
            Sprite background,
            Sprite[] digits,
            HitTipAnimationConfig animation,
            Color baseColor,
            Color highlightColor)
        {
            HitTipStyleConfig style = new HitTipStyleConfig();
            style.Configure(
                styleId,
                background,
                digits,
                animation,
                -2f,
                34f,
                new Vector2(133f, 50f),
                60f,
                baseColor,
                highlightColor);
            return style;
        }

        private static Sprite[] LoadDigits(string folder)
        {
            Sprite[] digits = new Sprite[10];
            for (int i = 0; i < digits.Length; i++)
            {
                digits[i] = LoadSprite(folder + "/" + i.ToString() + ".png");
            }

            return digits;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool IsBackground(string path)
        {
            return path.EndsWith("di_elemental.png", System.StringComparison.Ordinal)
                || path.EndsWith("di_nomal&critical.png", System.StringComparison.Ordinal);
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
    }
}
