using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0StageInstallerEntityOwnershipTests
    {
        private const string InstallerSourcePath =
            "Assets/FPGDemo/Editor/FpgDemoD0StageInstaller.cs";

        [Test]
        public void PlayerInstallationUsesCharacterDefinitionEntityPrefab()
        {
            string source = ReadInstallerSource();
            string composition = ExtractMethod(
                source,
                "private static D0ActorPresentationBindings EnsureActorPresentation(");
            string instance = ExtractMethod(
                source,
                "private static D0PlayerEntityView EnsurePlayerEntityInstance(");

            Assert.That(composition, Does.Contain("playerDefinition.EntityPrefab"));
            Assert.That(composition, Does.Contain("EnsurePlayerEntityInstance"));
            Assert.That(composition, Does.Not.Contain("VisualPrefab"));
            Assert.That(instance, Does.Contain("sourcePrefab.gameObject"));
            Assert.That(instance, Does.Contain("context.ActorsRoot"));
            Assert.That(instance, Does.Contain("PrefabUtility.RevertPrefabInstance"));
            Assert.That(instance, Does.Contain("mainCameraTransform.SetParent(context.transform, true)"));
            Assert.That(instance, Does.Contain("\"playerEntity\""));
            Assert.That(instance, Does.Not.Contain("SaveAsPrefabAsset"));
        }

        [Test]
        public void PlayerPresentationLeavesEntityWeaponAndServicesForRuntimeBinding()
        {
            string source = ReadInstallerSource();
            string method = ExtractMethod(
                source,
                "private static void ConfigurePlayerShotPresentation(");

            Assert.That(method, Does.Contain("weaponDefinition.TryValidatePresentation"));
            Assert.That(method, Does.Contain("playerEntity.TryValidate"));
            Assert.That(method, Does.Contain("playerEntity.SocketRegistry.TryResolve"));
            Assert.That(method, Does.Contain("\"presentationProfile\""));
            Assert.That(method, Does.Contain("profile.PoolCapacities.PlayerShotCapacity"));
            Assert.That(method, Does.Not.Contain("\"playerEntity\""));
            Assert.That(method, Does.Not.Contain("\"weaponDefinition\""));
            Assert.That(method, Does.Not.Contain("\"actorSockets\""));
            Assert.That(method, Does.Not.Contain("\"sessionHost\""));
            Assert.That(method, Does.Not.Contain("\"presentationCamera\""));
            Assert.That(method, Does.Not.Contain("\"actorPresenter\""));
        }

        [Test]
        public void LegacyActorScaffoldingIsRemovedAndNeverRecreated()
        {
            string source = ReadInstallerSource();
            string cleanup = ExtractMethod(
                source,
                "private static void RemoveLegacyActorScaffolding(");

            Assert.That(cleanup, Does.Contain("RemoveDirectChildIfPresent(d0SliceRoot, \"D0Actors\")"));
            Assert.That(source, Does.Not.Contain("\"enemyAnchor\""));
            Assert.That(source, Does.Not.Contain("\"luanHudiePresentationController\""));
            Assert.That(source, Does.Not.Contain("RequireDirectChild(d0SliceRoot, \"D0Actors\")"));
            Assert.That(source, Does.Not.Contain("EnsureOwnedVisualActor"));
            Assert.That(source, Does.Not.Contain("RebindLuanHudieBackendPresentation"));
        }

        [Test]
        public void EnemyEntityWorldIsSavedWithoutAuthoredEnemyInstances()
        {
            string source = ReadInstallerSource();
            string method = ExtractMethod(
                source,
                "private static void EnsureEncounterSpawning(");

            Assert.That(method, Does.Contain("RemoveAllDirectChildren(entityRoot)"));
            Assert.That(method, Does.Contain("entityRoot"));
            Assert.That(method, Does.Not.Contain("\"legacyEnemyAnchor\""));
            Assert.That(method, Does.Not.Contain("context.EnemyAnchor);"));
        }

        [Test]
        public void GenericCombatVfxWorldReplacesBurstbugScenePools()
        {
            string source = ReadInstallerSource();
            string method = ExtractMethod(
                source,
                "private static D0G3PresentationBindings EnsureG3Presentation(");

            Assert.That(method, Does.Contain("GetOrAddComponent<D0CombatVfxWorld>"));
            Assert.That(method, Does.Contain("RemoveDirectChildIfPresent(worldFxRoot, \"D0ActorEffects\")"));
            Assert.That(method, Does.Contain("\"poolRoot\""));
            Assert.That(method, Does.Contain("worldFxRoot"));
            Assert.That(method, Does.Not.Contain("actorEffectsRoot"));
            Assert.That(source, Does.Not.Contain("EnsureBurstbugCznFxPresentation"));
            Assert.That(source, Does.Not.Contain("EnsureCznPoolViews"));
            Assert.That(source, Does.Not.Contain("FastThreatPool"));
            Assert.That(source, Does.Not.Contain("InterceptableVolleyPool"));
            Assert.That(source, Does.Not.Contain("D0BurstbugCznFxPresenter"));
        }

        private static string ReadInstallerSource()
        {
            return File.ReadAllText(
                GetAbsoluteProjectPath(InstallerSourcePath));
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

            Assert.Fail("Installer method has an unmatched brace.");
            return string.Empty;
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);
            return Path.Combine(
                projectRoot,
                assetPath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
        }
    }
}
