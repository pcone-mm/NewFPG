using System;
using System.Collections.Generic;
using System.IO;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0GeneratedActorPrefabReferenceContractTests
    {
        private sealed class ActorContract
        {
            public ActorContract(
                string actorName,
                string definitionPath,
                string entityPrefabPath,
                string entityPrefabGuid,
                string generatedPrefabGuid,
                bool isPlayer)
            {
                ActorName = actorName;
                DefinitionPath = definitionPath;
                EntityPrefabPath = entityPrefabPath;
                EntityPrefabGuid = entityPrefabGuid;
                GeneratedPrefabGuid = generatedPrefabGuid;
                IsPlayer = isPlayer;
            }

            public string ActorName { get; }
            public string DefinitionPath { get; }
            public string EntityPrefabPath { get; }
            public string EntityPrefabGuid { get; }
            public string GeneratedPrefabGuid { get; }
            public bool IsPlayer { get; }
        }

        private static readonly ActorContract[] ActorContracts =
        {
            new ActorContract(
                "Fei",
                "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei.asset",
                "Assets/FPGDemo/Presentation/Actors/Fei/PF_D0_FeiEntity.prefab",
                "e8f2a7c4d9b1465e8f3a2c7d1b6e904f",
                "b0b355a0cb746b440960d44a01eaff5a",
                true),
            new ActorContract(
                "Burstbug",
                "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug.asset",
                "Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab",
                "6e1d4d0d52084ec4a3920dd04273f5c7",
                "6f3245acedeea494b8f516ca846ad2d8",
                false),
            new ActorContract(
                "Luan",
                "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Enemy.asset",
                "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab",
                "9b55eda7a9914224a9614df4da5e60f8",
                "0158606413463a24bb7c81abe0f8634d",
                false),
            new ActorContract(
                "Hudie",
                "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Enemy.asset",
                "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab",
                "c02f8f1b840951447a366680c93218f5",
                "9a99484419b01d1478b7a68d1619438b",
                false)
        };

        [Test]
        public void ScenesAndScriptableObjectsDoNotReferenceGeneratedActorPrefabs()
        {
            List<string> violations = new List<string>();
            List<string> paths = EnumerateAssetPaths(".asset", ".unity");
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                string path = paths[pathIndex];
                string contents = ReadAssetText(path);
                for (int actorIndex = 0;
                     actorIndex < ActorContracts.Length;
                     actorIndex++)
                {
                    ActorContract actor = ActorContracts[actorIndex];
                    if (ContainsGuid(contents, actor.GeneratedPrefabGuid))
                    {
                        violations.Add(
                            path + " directly references " + actor.ActorName
                            + " Generated Prefab "
                            + actor.GeneratedPrefabGuid + ".");
                    }
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "Scenes and ScriptableObjects must select complete Entity Prefabs; "
                + "Generated actor prefabs are render-only implementation details.\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void GeneratedActorPrefabsAreNestedOnlyByTheirOwningEntityPrefabs()
        {
            List<string> violations = new List<string>();
            List<string> prefabPaths = EnumerateAssetPaths(".prefab");
            for (int pathIndex = 0;
                 pathIndex < prefabPaths.Count;
                 pathIndex++)
            {
                string path = prefabPaths[pathIndex];
                string contents = ReadAssetText(path);
                for (int actorIndex = 0;
                     actorIndex < ActorContracts.Length;
                     actorIndex++)
                {
                    ActorContract actor = ActorContracts[actorIndex];
                    if (ContainsGuid(contents, actor.GeneratedPrefabGuid)
                        && !string.Equals(
                            path,
                            actor.EntityPrefabPath,
                            StringComparison.Ordinal))
                    {
                        violations.Add(
                            path + " bypasses " + actor.ActorName
                            + " Entity Prefab and directly references Generated Prefab "
                            + actor.GeneratedPrefabGuid + ".");
                    }
                }
            }

            for (int actorIndex = 0;
                 actorIndex < ActorContracts.Length;
                 actorIndex++)
            {
                ActorContract actor = ActorContracts[actorIndex];
                string contents = ReadAssetText(actor.EntityPrefabPath);
                if (!ContainsGuid(contents, actor.GeneratedPrefabGuid))
                {
                    violations.Add(
                        actor.EntityPrefabPath + " does not nest its expected "
                        + actor.ActorName + " Generated Prefab "
                        + actor.GeneratedPrefabGuid + ".");
                }

                for (int otherIndex = 0;
                     otherIndex < ActorContracts.Length;
                     otherIndex++)
                {
                    ActorContract other = ActorContracts[otherIndex];
                    if (otherIndex != actorIndex
                        && ContainsGuid(contents, other.GeneratedPrefabGuid))
                    {
                        violations.Add(
                            actor.EntityPrefabPath + " nests " + other.ActorName
                            + " Generated Prefab instead of owning only its render dependency.");
                    }
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "Only the four complete Entity Prefabs may nest Generated actor prefabs.\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void ActorDefinitionsReferenceCompleteValidatedEntityPrefabs()
        {
            for (int index = 0; index < ActorContracts.Length; index++)
            {
                ActorContract actor = ActorContracts[index];
                Assert.That(
                    AssetDatabase.AssetPathToGUID(actor.EntityPrefabPath),
                    Is.EqualTo(actor.EntityPrefabGuid),
                    actor.ActorName + " Entity Prefab GUID changed unexpectedly.");

                string definitionText = ReadAssetText(actor.DefinitionPath);
                Assert.That(
                    ContainsGuid(definitionText, actor.EntityPrefabGuid),
                    Is.True,
                    actor.DefinitionPath + " must serialize its complete Entity Prefab.");
                Assert.That(
                    ContainsGuid(definitionText, actor.GeneratedPrefabGuid),
                    Is.False,
                    actor.DefinitionPath + " must not serialize a Generated Prefab.");

                if (actor.IsPlayer)
                {
                    AssertPlayerDefinition(actor);
                }
                else
                {
                    AssertEnemyDefinition(actor);
                }
            }
        }

        private static void AssertPlayerDefinition(ActorContract actor)
        {
            D0CharacterDefinition definition =
                AssetDatabase.LoadAssetAtPath<D0CharacterDefinition>(
                    actor.DefinitionPath);
            Assert.That(definition, Is.Not.Null, actor.DefinitionPath);
            Assert.That(
                definition.TryValidate(out string definitionError),
                Is.True,
                actor.DefinitionPath + ": " + definitionError);

            D0PlayerEntityView entity = definition.EntityPrefab;
            Assert.That(entity, Is.Not.Null, actor.DefinitionPath);
            AssertEntityPrefab(actor, entity);
        }

        private static void AssertEnemyDefinition(ActorContract actor)
        {
            D0EnemyDefinition definition =
                AssetDatabase.LoadAssetAtPath<D0EnemyDefinition>(
                    actor.DefinitionPath);
            Assert.That(definition, Is.Not.Null, actor.DefinitionPath);
            Assert.That(
                definition.TryValidate(out string definitionError),
                Is.True,
                actor.DefinitionPath + ": " + definitionError);

            D0EnemyEntityView entity = definition.EntityPrefab;
            Assert.That(entity, Is.Not.Null, actor.DefinitionPath);
            AssertEntityPrefab(actor, entity);
        }

        private static void AssertEntityPrefab(
            ActorContract actor,
            D0ActorEntityView entity)
        {
            Assert.That(
                AssetDatabase.GetAssetPath(entity).Replace('\\', '/'),
                Is.EqualTo(actor.EntityPrefabPath),
                actor.ActorName + " Definition must reference its complete Entity Prefab.");
            Assert.That(
                entity.transform.parent,
                Is.Null,
                actor.ActorName + " EntityView must live on the prefab root.");
            Assert.That(
                entity.TryValidate(out string entityError),
                Is.True,
                actor.EntityPrefabPath + ": " + entityError);
        }

        private static List<string> EnumerateAssetPaths(
            params string[] extensions)
        {
            HashSet<string> acceptedExtensions = new HashSet<string>(
                extensions,
                StringComparer.OrdinalIgnoreCase);
            string fpgDemoRoot = Path.Combine(Application.dataPath, "FPGDemo");
            string[] absolutePaths = Directory.GetFiles(
                fpgDemoRoot,
                "*",
                SearchOption.AllDirectories);
            List<string> assetPaths = new List<string>();
            for (int index = 0; index < absolutePaths.Length; index++)
            {
                string absolutePath = absolutePaths[index];
                if (!acceptedExtensions.Contains(Path.GetExtension(absolutePath)))
                {
                    continue;
                }

                string relativeToAssets = absolutePath.Substring(
                    Application.dataPath.Length + 1);
                assetPaths.Add(
                    "Assets/" + relativeToAssets.Replace('\\', '/'));
            }

            assetPaths.Sort(StringComparer.Ordinal);
            return assetPaths;
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            Assert.That(projectRoot, Is.Not.Null);
            string absolutePath = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(absolutePath), Is.True, assetPath);
            return File.ReadAllText(absolutePath);
        }

        private static bool ContainsGuid(string contents, string guid)
        {
            return contents.IndexOf(guid, StringComparison.Ordinal) >= 0;
        }
    }
}
