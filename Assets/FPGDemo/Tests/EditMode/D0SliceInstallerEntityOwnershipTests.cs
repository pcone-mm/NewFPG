using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0SliceInstallerEntityOwnershipTests
    {
        private const string InstallerSourcePath =
            "Assets/FPGDemo/Editor/FpgDemoD0SliceInstaller.cs";

        private static readonly string[] EntityPrefabPaths =
        {
            "Assets/FPGDemo/Presentation/Actors/Fei/PF_D0_FeiEntity.prefab",
            "Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab",
            "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab",
            "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab"
        };

        [Test]
        public void GeneratedActorRefreshTwiceDoesNotModifyAuthoredEntityPrefabsOrMetadata()
        {
            byte[][] prefabBefore = new byte[EntityPrefabPaths.Length][];
            byte[][] metaBefore = new byte[EntityPrefabPaths.Length][];
            string[] guidBefore = new string[EntityPrefabPaths.Length];
            for (int index = 0; index < EntityPrefabPaths.Length; index++)
            {
                string prefabAbsolutePath =
                    GetAbsoluteProjectPath(EntityPrefabPaths[index]);
                prefabBefore[index] = File.ReadAllBytes(prefabAbsolutePath);
                metaBefore[index] = File.ReadAllBytes(prefabAbsolutePath + ".meta");
                guidBefore[index] = AssetDatabase.AssetPathToGUID(
                    EntityPrefabPaths[index]);
                Assert.That(guidBefore[index], Is.Not.Empty);
            }

            MethodInfo refresh = FindInstallerType().GetMethod(
                "EnsureGeneratedActorRenderPrefabs",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo validate = FindInstallerType().GetMethod(
                "ValidateAuthoredEntityPrefabs",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(refresh, Is.Not.Null);
            Assert.That(validate, Is.Not.Null);

            for (int pass = 0; pass < 2; pass++)
            {
                Assert.That(() => refresh.Invoke(null, null), Throws.Nothing);
                Assert.That(() => validate.Invoke(null, null), Throws.Nothing);
            }

            for (int index = 0; index < EntityPrefabPaths.Length; index++)
            {
                string prefabAbsolutePath =
                    GetAbsoluteProjectPath(EntityPrefabPaths[index]);
                CollectionAssert.AreEqual(
                    prefabBefore[index],
                    File.ReadAllBytes(prefabAbsolutePath),
                    EntityPrefabPaths[index]);
                CollectionAssert.AreEqual(
                    metaBefore[index],
                    File.ReadAllBytes(prefabAbsolutePath + ".meta"),
                    EntityPrefabPaths[index] + ".meta");
                Assert.That(
                    AssetDatabase.AssetPathToGUID(EntityPrefabPaths[index]),
                    Is.EqualTo(guidBefore[index]),
                    EntityPrefabPaths[index]);
            }
        }

        [Test]
        public void GeneratedActorRefreshDoesNotBindCombatProfile()
        {
            string source = File.ReadAllText(
                GetAbsoluteProjectPath(InstallerSourcePath));
            string method = ExtractMethod(
                source,
                "private static void EnsureGeneratedActorRenderPrefabs()");

            Assert.That(method, Does.Contain("FeiPrefabPath"));
            Assert.That(method, Does.Contain("BurstbugPrefabPath"));
            Assert.That(method, Does.Contain("EnsureD0SpineActorPrefab"));
            Assert.That(method, Does.Contain("LuanGeneratedRenderPrefabPath"));
            Assert.That(method, Does.Contain("HudieGeneratedRenderPrefabPath"));
            Assert.That(method, Does.Not.Contain("CombatPresentationProfile"));
            Assert.That(method, Does.Not.Contain("SerializedObject"));
            Assert.That(source, Does.Not.Contain("EnsureDerivedPrefabBinding"));
        }
        [Test]
        public void ExistingLuanHudieSkeletonDataOnlyRebuildsGeneratedRenderPrefabs()
        {
            string source = File.ReadAllText(
                GetAbsoluteProjectPath(InstallerSourcePath));
            string method = ExtractMethod(
                source,
                "private static void EnsureGeneratedActorRenderPrefab(");
            string prefabBuilder = ExtractMethod(
                source,
                "private static GameObject EnsureD0SpineActorPrefabAsset(");

            Assert.That(method, Does.Contain("LoadRequiredAsset<SkeletonDataAsset>"));
            Assert.That(method, Does.Contain("EnsureD0SpineActorPrefabAsset"));
            Assert.That(method, Does.Not.Contain("EnsureD0SpineSkeletonData"));
            Assert.That(method, Does.Not.Contain("EnsureD0SpineAtlas"));
            Assert.That(method, Does.Not.Contain("EnsureD0SpineTexture"));
            Assert.That(prefabBuilder, Does.Not.Contain("D0ActorEntityView"));
            Assert.That(prefabBuilder, Does.Not.Contain("D0ActorSocketRegistry"));
            Assert.That(prefabBuilder, Does.Not.Contain("Actor2DPresenter"));
        }

        [Test]
        public void OwnedRootsDoNotRecreateLegacyActorPresentationRoot()
        {
            string source = File.ReadAllText(
                GetAbsoluteProjectPath(InstallerSourcePath));
            string method = ExtractMethod(
                source,
                "private static void EnsureOwnedRoots(Transform root)");

            Assert.That(method, Does.Not.Contain("\"D0Actors\""));
            Assert.That(method, Does.Contain("\"D0Stage\""));
            Assert.That(method, Does.Contain("\"D0WorldFx\""));
        }

        [Test]
        public void AuthoredEntityValidationContainsNoPrefabAuthoringPath()
        {
            string source = File.ReadAllText(
                GetAbsoluteProjectPath(InstallerSourcePath));
            string validation = ExtractMethod(
                    source,
                    "private static void ValidateAuthoredEntityPrefabs()")
                + ExtractMethod(
                    source,
                    "private static void ValidatePlayerEntityPrefab(")
                + ExtractMethod(
                    source,
                    "private static void ValidateEnemyEntityPrefab(");

            Assert.That(validation, Does.Not.Contain("SaveAsPrefabAsset"));
            Assert.That(validation, Does.Not.Contain("InstantiatePrefab"));
            Assert.That(validation, Does.Not.Contain("new GameObject"));
            Assert.That(validation, Does.Not.Contain("SerializedObject"));
            Assert.That(validation, Does.Not.Contain("EditorUtility.SetDirty"));
            Assert.That(validation, Does.Contain("character.EntityPrefab != entityView"));
            Assert.That(validation, Does.Contain("enemy.EntityPrefab != entityView"));
            Assert.That(validation, Does.Contain(
                "GetComponentsInChildren<D0ActorEntityView>(true)"));
            Assert.That(validation, Does.Contain("entityViews.Length != 1"));
            Assert.That(validation, Does.Contain("\"Fei\""));
            Assert.That(validation, Does.Contain("\"Burstbug\""));
            Assert.That(validation, Does.Contain("\"Luan\""));
            Assert.That(validation, Does.Contain("\"Hudie\""));
        }

        private static Type FindInstallerType()
        {
            Type installer = Type.GetType(
                "FPG.Demo.Editor.FpgDemoD0SliceInstaller, Assembly-CSharp-Editor");
            Assert.That(installer, Is.Not.Null);
            return installer;
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int openingBrace = source.IndexOf('{', start);
            Assert.That(openingBrace, Is.GreaterThanOrEqualTo(0));

            int depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}' && --depth == 0)
                {
                    return source.Substring(start, index - start + 1);
                }
            }

            Assert.Fail("Installer validation method has an unmatched brace.");
            return string.Empty;
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
