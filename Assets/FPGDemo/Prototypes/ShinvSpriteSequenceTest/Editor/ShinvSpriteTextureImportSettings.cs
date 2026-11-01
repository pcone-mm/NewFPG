using System;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Prototypes.ShinvSpriteSequenceTest.Editor
{
    internal sealed class ShinvSpriteTextureImportSettings : AssetPostprocessor
    {
        private const string ArtPath = "Assets/FPGDemo/Prototypes/ShinvSpriteSequenceTest/Art/";

        public override int GetPostprocessOrder()
        {
            return 1000;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtPath, StringComparison.OrdinalIgnoreCase) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 4096;
            // Keep compression user-configurable in the Inspector and per-platform overrides.
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
