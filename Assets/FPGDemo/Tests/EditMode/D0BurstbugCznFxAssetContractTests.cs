using System.IO;
using System.Collections.Generic;
using FPG.Demo.Unity;
using NUnit.Framework;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    /// <summary>
    /// Guards the asset boundary for Burstbug attack-owned and state-owned G3 FX
    /// views. The local-only canonical JSON remains the read-only skeleton
    /// input; all renderable atlas, texture and material assets must be
    /// owned by the D0 presentation folder and use a Linear-safe straight
    /// alpha workflow in this project.
    /// </summary>
    public sealed class D0BurstbugCznFxAssetContractTests
    {
        private const string D0SpineFolder =
            "Assets/FPGDemo/Presentation/D0Slice/Spine/";
        private const string CanonicalEffectFolder =
            "Assets/Imported/CZN/Monsters/1001003/SpineSource/effect/";
        private const string CanonicalActorFolder =
            "Assets/Imported/CZN/Monsters/1001003/SpineSource/model/";
        private const string BurstbugStraightAlphaPrefabPath =
            D0SpineFolder + "D0_Burstbug_1001003_StraightAlpha.prefab";
        private const string BurstbugStraightAlphaTexturePath =
            D0SpineFolder + "D0_Burstbug_1001003_StraightAlpha.png";
        private const string CanonicalBurstbugTexturePath =
            CanonicalActorFolder + "1001003.png";
        private const string BurstbugPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Presentation.asset";
        private const string BurstbugFastAttackPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Fast.asset";
        private const string BurstbugVolleyAttackPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Volley.asset";
        private const string BurstbugHeavyAttackPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_HeavyBreak.asset";

        private static readonly FxExpectation[] Expectations =
        {
            new FxExpectation("Skill1", "burstbug_skill1.json", true),
            new FxExpectation("Skill2", "burstbug_skill2.json", true),
            new FxExpectation("Death1", "invader_death_01_f1.json", true),
            new FxExpectation("Death2", "invader_death_01_f2.json", false),
            new FxExpectation("Death3", "invader_death_01_f3.json", true),
            new FxExpectation("Death4", "invader_death_01_f4.json", false),
        };

        [Test]
        public void BurstbugAttacksOwnSkillVfxAndActorPresentationOwnsDeathStateVfx()
        {
            D0ActorPresentationDefinition presentation =
                AssetDatabase.LoadAssetAtPath<D0ActorPresentationDefinition>(BurstbugPresentationPath);
            Assert.That(presentation, Is.Not.Null, BurstbugPresentationPath);
            Assert.That(presentation.TryGetEnemyEffects(out D0EnemyEffectPresentationDefinition effects), Is.True);
            Assert.That(effects.TryValidate(out string effectsError), Is.True, effectsError);
            Assert.That(effects.PoolCount, Is.EqualTo(4));

            AssertAttackVfx(
                BurstbugFastAttackPath,
                "burstbug-fast-threat",
                "D0_Burstbug_1001003_Fx_Skill1.prefab",
                2,
                1.20f);
            AssertAttackVfx(
                BurstbugVolleyAttackPath,
                "burstbug-interceptable-volley",
                "D0_Burstbug_1001003_Fx_Skill2.prefab",
                2,
                1.0334f);
            AssertAttackVfx(
                BurstbugHeavyAttackPath,
                "burstbug-heavy-weakpoint",
                "D0_Burstbug_1001003_Fx_Skill2.prefab",
                2,
                1.0334f);

            AssertPool(
                effects,
                D0EnemyEffectSlot.DeathLayerF4,
                "D0_Burstbug_1001003_Fx_Death4.prefab",
                1,
                0.1667f,
                2);
            AssertPool(
                effects,
                D0EnemyEffectSlot.DeathLayerF3,
                "D0_Burstbug_1001003_Fx_Death3.prefab",
                1,
                0.90f,
                4);
            AssertPool(
                effects,
                D0EnemyEffectSlot.DeathLayerF2,
                "D0_Burstbug_1001003_Fx_Death2.prefab",
                1,
                0.90f,
                6);
            AssertPool(
                effects,
                D0EnemyEffectSlot.DeathLayerF1,
                "D0_Burstbug_1001003_Fx_Death1.prefab",
                1,
                0.90f,
                8);
        }

        [Test]
        public void DerivedFxPrefabsKeepOnlyTheirCanonicalJsonInputAndOwnEveryRenderableAsset()
        {
            for (int index = 0; index < Expectations.Length; index++)
            {
                FxExpectation expectation = Expectations[index];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(expectation.PrefabPath);
                Assert.That(prefab, Is.Not.Null, expectation.PrefabPath);
                Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Regular));
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);

                SkeletonAnimation skeleton = prefab.GetComponentInChildren<SkeletonAnimation>(true);
                Assert.That(skeleton, Is.Not.Null, expectation.PrefabPath);
                Assert.That(skeleton.pmaVertexColors, Is.True);
                SkeletonDataAsset data = skeleton.SkeletonDataAsset;
                Assert.That(data, Is.Not.Null, expectation.PrefabPath);
                Assert.That(AssetDatabase.GetAssetPath(data), Is.EqualTo(expectation.SkeletonDataPath));
                Assert.That(AssetDatabase.GetAssetPath(data.skeletonJSON), Is.EqualTo(expectation.CanonicalJsonPath));
                Assert.That(data.GetSkeletonData(true).FindAnimation("animation"), Is.Not.Null);

                Assert.That(data.atlasAssets, Has.Length.EqualTo(1));
                SpineAtlasAsset atlas = data.atlasAssets[0] as SpineAtlasAsset;
                Assert.That(atlas, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(atlas), Is.EqualTo(expectation.AtlasPath));
                Assert.That(atlas.PrimaryMaterial, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(atlas.PrimaryMaterial), Is.EqualTo(expectation.MainMaterialPath));
                AssertStraightAlphaMaterial(atlas.PrimaryMaterial, expectation.MainMaterialPath);
                Assert.That(
                    AssetDatabase.GetAssetPath(atlas.PrimaryMaterial.mainTexture),
                    Is.EqualTo(expectation.TexturePath));
                AssertStraightAlphaTexture(expectation.TexturePath);

                AssertDerivedAdditiveMapping(data, expectation);
                AssertOnlyExpectedCanonicalInput(expectation);
            }
        }

        private static void AssertDerivedAdditiveMapping(
            SkeletonDataAsset data,
            FxExpectation expectation)
        {
            List<BlendModeMaterials.ReplacementMaterial> additive =
                data.blendModeMaterials.additiveMaterials;
            int expectedCount = expectation.HasAdditiveMaterial ? 1 : 0;
            Assert.That(additive, Is.Not.Null);
            Assert.That(additive.Count, Is.EqualTo(expectedCount));
            if (!expectation.HasAdditiveMaterial)
            {
                return;
            }

            BlendModeMaterials.ReplacementMaterial replacement = additive[0];
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.pageName, Is.EqualTo(expectation.TextureFileName));
            Assert.That(replacement.material, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(replacement.material),
                Is.EqualTo(expectation.AdditiveMaterialPath));
            AssertStraightAlphaMaterial(replacement.material, expectation.AdditiveMaterialPath);
            Assert.That(
                AssetDatabase.GetAssetPath(replacement.material.mainTexture),
                Is.EqualTo(expectation.TexturePath));
        }

        [Test]
        public void BurstbugActorUsesALinearSafeOwnedStraightAlphaTextureWithoutCanonicalRenderDependencies()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BurstbugStraightAlphaPrefabPath);
            Assert.That(prefab, Is.Not.Null, BurstbugStraightAlphaPrefabPath);

            SkeletonAnimation skeleton = prefab.GetComponentInChildren<SkeletonAnimation>(true);
            Assert.That(skeleton, Is.Not.Null);
            Assert.That(skeleton.pmaVertexColors, Is.True);
            SkeletonDataAsset data = skeleton.SkeletonDataAsset;
            Assert.That(data, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(data),
                Is.EqualTo(D0SpineFolder + "D0_Burstbug_1001003_StraightAlpha_SkeletonData.asset"));
            Assert.That(AssetDatabase.GetAssetPath(data.skeletonJSON),
                Is.EqualTo(CanonicalActorFolder + "1001003.json"));

            SpineAtlasAsset atlas = data.atlasAssets[0] as SpineAtlasAsset;
            Assert.That(atlas, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(atlas),
                Is.EqualTo(D0SpineFolder + "D0_Burstbug_1001003_StraightAlpha_Atlas.asset"));
            AssertStraightAlphaMaterial(
                atlas.PrimaryMaterial,
                D0SpineFolder + "D0_Burstbug_1001003_StraightAlpha_Material.mat");
            Assert.That(AssetDatabase.GetAssetPath(atlas.PrimaryMaterial.mainTexture),
                Is.EqualTo(BurstbugStraightAlphaTexturePath));
            AssertStraightAlphaTexture(BurstbugStraightAlphaTexturePath);

            AssertLinearStraightAlphaConversion(
                CanonicalBurstbugTexturePath,
                BurstbugStraightAlphaTexturePath);

            string[] dependencies = AssetDatabase.GetDependencies(BurstbugStraightAlphaPrefabPath, true);
            List<string> canonicalInputs = new List<string>();
            for (int index = 0; index < dependencies.Length; index++)
            {
                string dependency = dependencies[index];
                if (dependency.StartsWith(CanonicalActorFolder, System.StringComparison.Ordinal))
                {
                    canonicalInputs.Add(dependency);
                }
            }

            CollectionAssert.AreEquivalent(
                new[] { CanonicalActorFolder + "1001003.json" },
                canonicalInputs);
        }

        private static void AssertStraightAlphaMaterial(Material material, string expectedPath)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(expectedPath));
            Assert.That(material.HasProperty("_StraightAlphaInput"), Is.True);
            Assert.That(material.GetFloat("_StraightAlphaInput"), Is.EqualTo(1f));
            Assert.That(material.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT"), Is.True);
        }

        private static void AssertAttackVfx(
            string attackPath,
            string expectedKey,
            string expectedPrefabFileName,
            int expectedCapacity,
            float expectedDuration)
        {
            D0EnemyAttackDefinition attack =
                AssetDatabase.LoadAssetAtPath<D0EnemyAttackDefinition>(attackPath);
            Assert.That(attack, Is.Not.Null, attackPath);
            Assert.That(attack.TryValidatePresentation(out string error), Is.True, error);
            Assert.That(attack.EffectiveVisualEffectKey, Is.EqualTo(expectedKey));
            Assert.That(attack.VisualEffectPrefab, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(attack.VisualEffectPrefab),
                Is.EqualTo(D0SpineFolder + expectedPrefabFileName));
            Assert.That(attack.VfxPrewarmCapacity, Is.EqualTo(expectedCapacity));
            Assert.That(attack.VfxDuration, Is.EqualTo(expectedDuration).Within(0.0001f));
        }

        private static void AssertPool(
            D0EnemyEffectPresentationDefinition effects,
            D0EnemyEffectSlot slot,
            string expectedPrefabFileName,
            int expectedCapacity,
            float expectedDuration,
            int expectedSortingOffset)
        {
            Assert.That(effects.TryGet(slot, out D0EnemyEffectPoolDefinition pool), Is.True);
            Assert.That(pool.VisualPrefab, Is.Not.Null, slot.ToString());
            string prefabPath = AssetDatabase.GetAssetPath(pool.VisualPrefab);
            Assert.That(prefabPath, Is.EqualTo(D0SpineFolder + expectedPrefabFileName));
            Assert.That(prefabPath.StartsWith(D0SpineFolder, System.StringComparison.Ordinal), Is.True);
            Assert.That(pool.PrewarmCapacity, Is.EqualTo(expectedCapacity));
            Assert.That(pool.AnimationName, Is.EqualTo("animation"));
            Assert.That(pool.Duration, Is.EqualTo(expectedDuration).Within(0.0001f));
            Assert.That(pool.SortingOrderOffset, Is.EqualTo(expectedSortingOffset));
        }

        private static void AssertStraightAlphaTexture(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, texturePath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        private static void AssertLinearStraightAlphaConversion(
            string canonicalTexturePath,
            string derivedTexturePath)
        {
            Texture2D canonical = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            Texture2D derived = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(
                    canonical,
                    File.ReadAllBytes(GetAbsoluteProjectPath(canonicalTexturePath)),
                    false), Is.True);
                Assert.That(ImageConversion.LoadImage(
                    derived,
                    File.ReadAllBytes(GetAbsoluteProjectPath(derivedTexturePath)),
                    false), Is.True);
                Assert.That(derived.width, Is.EqualTo(canonical.width));
                Assert.That(derived.height, Is.EqualTo(canonical.height));

                Color32[] canonicalPixels = canonical.GetPixels32();
                Color32[] derivedPixels = derived.GetPixels32();
                bool foundTranslucentPixel = false;
                for (int index = 0; index < canonicalPixels.Length; index++)
                {
                    Color32 source = canonicalPixels[index];
                    Color32 output = derivedPixels[index];
                    Assert.That(output.a, Is.EqualTo(source.a));
                    if (source.a == 0)
                    {
                        Assert.That(output.r, Is.Zero);
                        Assert.That(output.g, Is.Zero);
                        Assert.That(output.b, Is.Zero);
                        continue;
                    }

                    Assert.That(output.r, Is.EqualTo(ExpectedStraightAlphaByte(source.r, source.a)).Within(1));
                    Assert.That(output.g, Is.EqualTo(ExpectedStraightAlphaByte(source.g, source.a)).Within(1));
                    Assert.That(output.b, Is.EqualTo(ExpectedStraightAlphaByte(source.b, source.a)).Within(1));
                    foundTranslucentPixel |= source.a < byte.MaxValue;
                }

                Assert.That(foundTranslucentPixel, Is.True,
                    "The representative CZN texture must contain translucent pixels to guard the Linear conversion path.");
            }
            finally
            {
                Object.DestroyImmediate(canonical);
                Object.DestroyImmediate(derived);
            }
        }

        private static byte ExpectedStraightAlphaByte(byte gammaPremultiplied, byte alpha)
        {
            float alpha01 = alpha / 255f;
            float premultipliedLinear = Mathf.GammaToLinearSpace(gammaPremultiplied / 255f);
            float straightLinear = Mathf.Clamp01(premultipliedLinear / alpha01);
            float straightGamma = Mathf.LinearToGammaSpace(straightLinear);
            return (byte)Mathf.Clamp(Mathf.RoundToInt(straightGamma * 255f), 0, 255);
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            const string Prefix = "Assets/";
            Assert.That(assetPath.StartsWith(Prefix, System.StringComparison.Ordinal), Is.True);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void AssertOnlyExpectedCanonicalInput(FxExpectation expectation)
        {
            string[] dependencies = AssetDatabase.GetDependencies(expectation.PrefabPath, true);
            List<string> canonicalInputs = new List<string>();
            for (int index = 0; index < dependencies.Length; index++)
            {
                string dependency = dependencies[index];
                if (dependency.StartsWith(CanonicalEffectFolder, System.StringComparison.Ordinal))
                {
                    canonicalInputs.Add(dependency);
                }
            }

            CollectionAssert.AreEquivalent(
                new[] { expectation.CanonicalJsonPath },
                canonicalInputs,
                "The D0 derived prefab may read only its matching canonical Spine JSON; atlas, texture, and materials must remain D0-owned.");
        }

        private sealed class FxExpectation
        {
            private const string Prefix = "D0_Burstbug_1001003_Fx_";

            public FxExpectation(string label, string canonicalJsonFileName, bool hasAdditiveMaterial)
            {
                Label = label;
                HasAdditiveMaterial = hasAdditiveMaterial;
                string derivedPrefix = Prefix + label;
                PrefabPath = D0SpineFolder + derivedPrefix + ".prefab";
                SkeletonDataPath = D0SpineFolder + derivedPrefix + "_SkeletonData.asset";
                AtlasPath = D0SpineFolder + derivedPrefix + "_Atlas.asset";
                MainMaterialPath = D0SpineFolder + derivedPrefix + "_Material.mat";
                TexturePath = D0SpineFolder + derivedPrefix + ".png";
                TextureFileName = derivedPrefix + ".png";
                AdditiveMaterialPath = D0SpineFolder + derivedPrefix + "_Additive_0.mat";
                CanonicalJsonPath = CanonicalEffectFolder + canonicalJsonFileName;
            }

            public string Label { get; }
            public string PrefabPath { get; }
            public string SkeletonDataPath { get; }
            public string AtlasPath { get; }
            public string MainMaterialPath { get; }
            public string TexturePath { get; }
            public string TextureFileName { get; }
            public string AdditiveMaterialPath { get; }
            public string CanonicalJsonPath { get; }
            public bool HasAdditiveMaterial { get; }
        }
    }
}
