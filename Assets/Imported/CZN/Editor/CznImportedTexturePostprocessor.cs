#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools.CZN
{
    internal sealed class CznImportedTexturePostprocessor : AssetPostprocessor
    {
        private const string ImportedRoot = "Assets/Imported/CZN/";
        private const string SpineSourceSegment = "/SpineSource/";

        private void OnPreprocessTexture()
        {
            if (!IsCznSpineTexture(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;

            // The complete effect library is about 1.2 GiB as uncompressed RGBA.
            // Keep the two core character sheets lossless for study and use
            // high-quality platform compression for the remaining effect sheets.
            importer.textureCompression = IsCoreCharacterSheet(assetPath)
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;
        }

        private static bool IsCznSpineTexture(string path)
        {
            return path.StartsWith(ImportedRoot, StringComparison.Ordinal)
                && path.IndexOf(SpineSourceSegment, StringComparison.Ordinal) >= 0
                && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCoreCharacterSheet(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/SpineSource/model/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            string name = Path.GetFileNameWithoutExtension(normalized);
            const string battleReadySuffix = "_battle_ready";
            if (name.EndsWith(battleReadySuffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - battleReadySuffix.Length);
            }
            return int.TryParse(name, out _);
        }
    }
}
#endif
