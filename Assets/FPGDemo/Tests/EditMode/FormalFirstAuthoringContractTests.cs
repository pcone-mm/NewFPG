using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FormalFirstAuthoringContractTests
    {
        private const string CatalogPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_PlayableCharacterCatalog.asset";
        private const string BootScenePath =
            "Assets/FPGDemo/Scenes/Boot.unity";
        private const string RoomEditorLayoutPath =
            "Assets/FPGDemo/Editor/LevelAuthoring/FpgRoomEditor.uxml";
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

        [Test]
        public void FormalLuanSummonChainTargetsHudie()
        {
            const string attackPath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_Attack_Summon.asset";
            const string summonPath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_SummonHudie.asset";
            const string hudiePath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Enemy.asset";

            FpgEnemyAttackDefinition attack =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(attackPath);
            FpgSummonActionDefinition summon =
                AssetDatabase.LoadAssetAtPath<FpgSummonActionDefinition>(summonPath);
            FpgEnemyDefinition hudie =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(hudiePath);

            Assert.That(attack, Is.Not.Null, attackPath);
            Assert.That(summon, Is.Not.Null, summonPath);
            Assert.That(hudie, Is.Not.Null, hudiePath);
            Assert.That(attack.TryValidate(out string attackError), Is.True, attackError);
            Assert.That(summon.TryValidate(out string summonError), Is.True, summonError);
            Assert.That(hudie.TryValidate(out string hudieError), Is.True, hudieError);
            Assert.That(attack.Summon, Is.SameAs(summon));
            Assert.That(
                attack.SummonOwnerOutcome,
                Is.EqualTo(FpgSummonOwnerOutcome.DieAfterSuccessfulSummon));
            Assert.That(summon.MaxSummonsPerOwner, Is.EqualTo(1));
            Assert.That(summon.CandidateEnemies, Has.Length.EqualTo(1));
            Assert.That(summon.CandidateEnemies[0], Is.SameAs(hudie));
            Assert.That(hudie.EnemyDefinitionId, Is.EqualTo("hudie"));
        }

        [Test]
        public void RoomEditorDoesNotExposeLegacyD0ScenarioControls()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                RoomEditorLayoutPath);
            Assert.That(layout, Is.Not.Null, RoomEditorLayoutPath);

            VisualElement root = new VisualElement();
            layout.CloneTree(root);

            Assert.That(root.Q("scenario-field"), Is.Null);
            Assert.That(root.Q("play-room-button"), Is.Null);
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
