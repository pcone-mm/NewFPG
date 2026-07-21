using System;
using System.Collections.Generic;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FormalFirstAuthoringContractTests
    {
        private const string CatalogPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_PlayableCharacterCatalog.asset";
        private const string BootScenePath =
            "Assets/FPGDemo/Scenes/Boot.unity";
        private const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";

        [Test]
        public void PlayableCharacterCatalogContainsOneCompleteDefaultFei()
        {
            FpgPlayableCharacterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(
                catalog.TryValidate(out string catalogError),
                Is.True,
                catalogError);
            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog.DefaultCharacter, Is.Not.Null);
            Assert.That(catalog.DefaultCharacter.CharacterId, Is.EqualTo("fei"));

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Entries.Count; index++)
            {
                FpgPlayableCharacterCatalogEntry entry = catalog.Entries[index];
                Assert.That(entry, Is.Not.Null);
                Assert.That(
                    entry.TryCreateSelection(
                        out FpgPlayableCharacterSelection selection,
                        out string entryError),
                    Is.True,
                    entryError);
                Assert.That(ids.Add(selection.CharacterId), Is.True,
                    $"Duplicate playable character ID '{selection.CharacterId}'.");
                Assert.That(selection.CharacterDefinition.EntityPrefab, Is.Not.Null);
                Assert.That(selection.CharacterDefinition.Weapon, Is.Not.Null);
                Assert.That(selection.CharacterDefinition.ActorPresentation, Is.Not.Null);
                Assert.That(selection.ThreeCProfile, Is.Not.Null);
                Assert.That(selection.SelectionPreviewPrefab, Is.Not.Null);
                Assert.That(
                    selection.SelectionPreviewPrefab.GetComponentInChildren<
                        D0ActorEntityView>(true),
                    Is.Null,
                    "A Boot preview prefab must not contain a gameplay Entity.");
            }

            Assert.That(
                catalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection defaultSelection,
                    out string defaultError),
                Is.True,
                defaultError);
            Assert.That(defaultSelection.CharacterId, Is.EqualTo("fei"));
            Assert.That(
                defaultSelection.CharacterDefinition,
                Is.SameAs(catalog.DefaultCharacter));
        }

        [Test]
        public void BootAuthorsOnlyOneVisualCharacterChoice()
        {
            WithPreviewScene(
                BootScenePath,
                scene =>
                {
                    Assert.That(
                        FindComponents<D0PlayerEntityView>(scene),
                        Is.Empty,
                        "Boot must not contain a gameplay player Entity.");
                    Assert.That(
                        FindComponents<FpgBootCharacterChoice>(scene),
                        Has.Count.EqualTo(1));
                    Assert.That(
                        FindComponents<GameBootstrap>(scene),
                        Has.Count.EqualTo(1));
                });
        }

        [Test]
        public void FormalRoomAuthorsComposerHostAndNoGameplayPlayer()
        {
            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    Assert.That(
                        FindComponents<D0PlayerEntityView>(scene),
                        Is.Empty,
                        "FormalRoom player Entity must be composed at runtime.");
                    Assert.That(
                        FindComponents<BattleSessionHost>(scene),
                        Is.Empty,
                        "Legacy BattleSessionHost must not enter FormalRoom.");
                    Assert.That(
                        FindComponents<BattleSceneContext>(scene),
                        Is.Empty,
                        "Legacy BattleSceneContext must not enter FormalRoom.");

                    List<FpgFormalPlayerComposer> composers =
                        FindComponents<FpgFormalPlayerComposer>(scene);
                    List<FpgFormalEncounterHost> formalHosts =
                        FindComponents<FpgFormalEncounterHost>(scene);
                    Assert.That(composers, Has.Count.EqualTo(1));
                    Assert.That(formalHosts, Has.Count.EqualTo(1));
                    Assert.That(
                        formalHosts[0].PlayerComposer,
                        Is.SameAs(composers[0]));
                    Assert.That(
                        formalHosts[0].TryValidateAuthoring(
                            out string authoringError),
                        Is.True,
                        authoringError);
                });
        }

        private static List<T> FindComponents<T>(Scene scene)
            where T : Component
        {
            List<T> values = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                values.AddRange(
                    roots[rootIndex].GetComponentsInChildren<T>(true));
            }

            return values;
        }

        private static void WithPreviewScene(
            string scenePath,
            Action<Scene> assertion)
        {
            SceneAsset sceneAsset =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            Assert.That(sceneAsset, Is.Not.Null, scenePath);

            Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
            Assert.That(scene.IsValid(), Is.True, scenePath);
            try
            {
                assertion(scene);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
