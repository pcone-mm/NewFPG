using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0CombatScenarioDefinitionTests
    {
        private const string ScenarioConfigPath =
            "Assets/FPGDemo/Config/BattleScenarioConfig.asset";

        private const string ScenarioDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsBurstbug.asset";

        private const string BurstbugScenarioDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsBurstbug.asset";

        private const string HudieScenarioDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsHudie.asset";

        private const string RoomDefinitionPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_combatlab-forest.asset";

        private const string FeiPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Presentation.asset";

        private const string FeiDefinitionPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei.asset";

        private const string BurstbugPresentationPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Presentation.asset";

        private const string PresentationProfilePath =
            "Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset";

        private const string BurstbugEnemyPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug.asset";

        private const string HudieEnemyPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Enemy.asset";

        private const string LuanEnemyPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Enemy.asset";

        private const string BurstbugEntityPrefabPath =
            "Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab";

        private const string HudieEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab";

        private const string LuanEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab";

        [Test]
        public void InstalledFeiBurstbugSliceBuildsTheExpectedDomainDefinition()
        {
            BattleScenarioConfig config = LoadRequired<BattleScenarioConfig>(ScenarioConfigPath);
            D0CombatScenarioDefinition authored =
                LoadRequired<D0CombatScenarioDefinition>(ScenarioDefinitionPath);

            Assert.That(config.UsesAuthoredScenario, Is.True);
            Assert.That(config.AuthoredScenario, Is.SameAs(authored));
            Assert.That(authored.ScenarioId, Is.EqualTo("combatlab-fei-vs-burstbug"));
            Assert.That(authored.EncounterContract,
                Is.EqualTo(D0EncounterContract.BurstbugStandard));
            Assert.That(authored.Encounter.EncounterId, Is.EqualTo("burstbug-training"));
            Assert.That(authored.Encounter.Enemy.EnemyId, Is.EqualTo("burstbug"));
            Assert.That(authored.Encounter.SpawnSlotCount, Is.EqualTo(1));

            D0EncounterSpawnSlot burstbugSlot = authored.Encounter.InitialSpawnSlot;
            Assert.That(burstbugSlot, Is.SameAs(authored.Encounter.GetSpawnSlot(0)));
            Assert.That(burstbugSlot.DefinitionId, Is.EqualTo(1));
            Assert.That(burstbugSlot.Enemy.EnemyId, Is.EqualTo("burstbug"));
            Assert.That(burstbugSlot.SpawnPointId, Is.EqualTo("enemy-main"));
            Assert.That(burstbugSlot.SpawnTick, Is.Zero);
            Assert.That(
                burstbugSlot.PosePolicy,
                Is.EqualTo(D0EncounterSpawnPosePolicy.AtSpawnPoint));
            Assert.That(
                authored.Encounter.TryGetSpawnSlot(1, out D0EncounterSpawnSlot resolvedBurstbug),
                Is.True);
            Assert.That(resolvedBurstbug, Is.SameAs(burstbugSlot));
            Assert.That(authored.Encounter.TryGetSpawnSlot(2, out _), Is.False);
            Assert.That(authored.Encounter.AttackScheduleCount, Is.EqualTo(6));
            Assert.That(authored.Encounter.GetAttackScheduleEntry(0).DueTick, Is.EqualTo(120));
            Assert.That(authored.Encounter.GetAttackScheduleEntry(5).DueTick, Is.EqualTo(1200));
            Assert.That(authored.LuanSummonHudie, Is.Null);
            Assert.That(authored.ThreeCProfile, Is.Not.Null);
            Assert.That(authored.Encounter.UsesReusableAttackDefinitions, Is.True);
            Assert.That(authored.TryValidate(out string authoredError), Is.True, authoredError);
            Assert.That(config.TryValidateSpatialConfiguration(out string spatialError), Is.True, spatialError);
            Assert.That(config.TryCreateDefinition(
                out ScenarioDefinition definition,
                out string error), Is.True, error);

            Assert.That(definition.PlayerLife, Is.EqualTo(100));
            Assert.That(definition.PlayerBarrier, Is.EqualTo(100));
            Assert.That(definition.EnemyLife, Is.EqualTo(800));
            Assert.That(definition.EnemyBreak, Is.EqualTo(160));
            Assert.That(definition.EnemySpawnCount, Is.Zero);
            Assert.That(definition.PlayerWeapon.MagazineCapacity, Is.EqualTo(8));
            Assert.That(definition.PlayerWeapon.PrimaryAmmoCost, Is.EqualTo(1));
            Assert.That(definition.PlayerWeapon.PrimaryInterval.Value, Is.EqualTo(12));
            Assert.That(definition.PlayerWeapon.SecondaryAmmoCost, Is.EqualTo(2));
            Assert.That(definition.PlayerWeapon.SecondaryMinimumCharge.Value, Is.Zero);
            Assert.That(definition.PlayerWeapon.ReloadDuration.Value, Is.EqualTo(84));
            Assert.That(definition.ThreatScheduleCount, Is.EqualTo(6));
            Assert.That(config.ThreatScheduleCount, Is.EqualTo(6));
        }

        [Test]
        public void InstalledDefinitionsSeparateGlobalStateSkillAndEntityStructure()
        {
            D0ActorPresentationDefinition fei =
                LoadRequired<D0ActorPresentationDefinition>(FeiPresentationPath);
            D0ActorPresentationDefinition burstbug =
                LoadRequired<D0ActorPresentationDefinition>(BurstbugPresentationPath);
            CombatPresentationProfile profile =
                LoadRequired<CombatPresentationProfile>(PresentationProfilePath);
            D0CharacterDefinition character =
                LoadRequired<D0CharacterDefinition>(FeiDefinitionPath);

            Assert.That(fei.TryValidate(out string feiError), Is.True, feiError);
            Assert.That(burstbug.TryValidate(out string burstbugError), Is.True, burstbugError);
            Assert.That(profile.TryValidate(out string profileError), Is.True, profileError);
            Assert.That(fei.TryGetPlayer(out PlayerActorPresentationDefinition feiState), Is.True);
            Assert.That(
                burstbug.TryGetEnemy(out EnemyActorPresentationDefinition burstbugState),
                Is.True);
            Assert.That(feiState.IdleAnimation, Is.EqualTo("b_idle"));
            Assert.That(burstbugState.IdleAnimation, Is.EqualTo("normal_idle"));

            D0WeaponDefinition weapon = character.Weapon;
            D0PlayerEntityView entity = character.EntityPrefab;
            Assert.That(weapon, Is.Not.Null);
            Assert.That(entity, Is.Not.Null);
            Assert.That(weapon.SecondaryPresentation.ReleaseAnimation, Is.EqualTo("defense_play"));
            Assert.That(
                entity.SocketRegistry.TryResolve(
                    weapon.PrimaryPresentation.SocketId,
                    out Transform primaryMuzzle),
                Is.True);
            Assert.That(
                entity.SocketRegistry.TryResolve(
                    weapon.SecondaryPresentation.Shot.SocketId,
                    out Transform secondaryMuzzle),
                Is.True);
            Assert.That(primaryMuzzle.localPosition,
                Is.EqualTo(new Vector3(0.72f, 0.42f, -0.06f)));
            Assert.That(secondaryMuzzle.localPosition,
                Is.EqualTo(new Vector3(0.72f, 0.42f, -0.06f)));
            Assert.That(primaryMuzzle, Is.Not.SameAs(secondaryMuzzle));
        }

        [TestCase(BurstbugEnemyPath, BurstbugEntityPrefabPath, "burstbug")]
        [TestCase(HudieEnemyPath, HudieEntityPrefabPath, "hudie")]
        [TestCase(LuanEnemyPath, LuanEntityPrefabPath, "luan")]
        public void InstalledEnemyDefinitionsOwnValidatedEntityPrefabs(
            string enemyPath,
            string expectedPrefabPath,
            string expectedEnemyId)
        {
            D0EnemyDefinition enemy = LoadRequired<D0EnemyDefinition>(enemyPath);

            Assert.That(enemy.TryValidate(out string enemyError), Is.True, enemyError);
            Assert.That(enemy.EntityPrefab, Is.Not.Null);
            Assert.That(enemy.EnemyId, Is.EqualTo(expectedEnemyId));
            Assert.That(
                AssetDatabase.GetAssetPath(enemy.EntityPrefab),
                Is.EqualTo(expectedPrefabPath));
            Assert.That(
                AssetDatabase.GetAssetPath(enemy.EntityPrefab),
                Is.EqualTo(expectedPrefabPath));
            Assert.That(
                enemy.EntityPrefab.TryValidate(out string prefabError),
                Is.True,
                prefabError);
        }

        [Test]
        public void EnemyDefinitionWithoutAnEntityPrefabFailsClosed()
        {
            D0EnemyDefinition clone = Object.Instantiate(
                LoadRequired<D0EnemyDefinition>(BurstbugEnemyPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty entityPrefab = serialized.FindProperty("entityPrefab");
                Assert.That(entityPrefab, Is.Not.Null);
                entityPrefab.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string error), Is.False);
                Assert.That(error, Is.EqualTo("Enemy definition requires an entity prefab."));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void ScenarioRejectsPlayerSpawnPointMissingFromItsStage()
        {
            D0CombatScenarioDefinition clone = Object.Instantiate(
                LoadRequired<D0CombatScenarioDefinition>(ScenarioDefinitionPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                serialized.FindProperty("playerSpawnPointId").stringValue = "missing-player";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string error), Is.False);
                Assert.That(
                    error,
                    Is.EqualTo(
                        "Combat scenario player spawn point 'missing-player' "
                        + "is not defined by stage 'combatlab-forest'."));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void ScenarioRejectsEncounterSpawnPointMissingFromItsStage()
        {
            D0CombatScenarioDefinition clone = Object.Instantiate(
                LoadRequired<D0CombatScenarioDefinition>(ScenarioDefinitionPath));
            D0EncounterDefinition encounterClone = Object.Instantiate(clone.Encounter);
            try
            {
                SerializedObject encounterSerialized = new SerializedObject(encounterClone);
                SerializedProperty spawnSlots = encounterSerialized.FindProperty("spawnSlots");
                Assert.That(spawnSlots, Is.Not.Null);
                spawnSlots.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("spawnPointId").stringValue = "missing-enemy";
                encounterSerialized.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject scenarioSerialized = new SerializedObject(clone);
                scenarioSerialized.FindProperty("encounter").objectReferenceValue = encounterClone;
                scenarioSerialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string error), Is.False);
                Assert.That(
                    error,
                    Is.EqualTo(
                        "Encounter spawn point 'missing-enemy' "
                        + "is not defined by stage 'combatlab-forest'."));
            }
            finally
            {
                Object.DestroyImmediate(encounterClone);
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void InvalidAuthoredScenarioFailsClosedInsteadOfUsingLegacyGameplayValues()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();
            D0CombatScenarioDefinition invalid =
                ScriptableObject.CreateInstance<D0CombatScenarioDefinition>();

            try
            {
                SerializedObject serialized = new SerializedObject(config);
                serialized.FindProperty("authoredScenario").objectReferenceValue = invalid;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(config.UsesAuthoredScenario, Is.True);
                Assert.That(config.TryCreateDefinition(out ScenarioDefinition definition, out string error), Is.False);
                Assert.That(definition, Is.Null);
                Assert.That(error, Does.Contain("player definition"));
                Assert.That(config.TryValidateSpatialConfiguration(out string spatialError), Is.False);
                Assert.That(spatialError, Does.Contain("combat-feel profile"));
            }
            finally
            {
                Object.DestroyImmediate(invalid);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EmptyAuthoredBridgePreservesLegacyFallbackForNewConfigs()
        {
            BattleScenarioConfig config = ScriptableObject.CreateInstance<BattleScenarioConfig>();

            try
            {
                Assert.That(config.UsesAuthoredScenario, Is.False);
                Assert.That(config.TryCreateDefinition(out ScenarioDefinition definition, out string error), Is.True, error);
                Assert.That(definition.PlayerWeapon.PrimaryInterval.Value, Is.EqualTo(39));
                Assert.That(definition.PlayerWeapon.SecondaryMinimumCharge.Value, Is.Zero);
                Assert.That(definition.PlayerWeapon.ReloadDuration.Value, Is.EqualTo(84));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [TestCase(BurstbugScenarioDefinitionPath)]
        [TestCase(HudieScenarioDefinitionPath)]
        public void InstalledScenarioSpawnIdsResolveAgainstMigratedRoom(
            string scenarioPath)
        {
            FpgRoomDefinition room =
                LoadRequired<FpgRoomDefinition>(RoomDefinitionPath);
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(scenarioPath);

            Assert.That(scenario.TryValidateForRoom(out string roomError), Is.True, roomError);
            Assert.That(
                FpgRoomEncounterValidator.TryValidate(
                    room,
                    scenario,
                    out FpgRoomEncounterValidationResult validation),
                Is.True,
                validation.FirstError == null
                    ? string.Empty
                    : validation.FirstError.Message);
            Assert.That(validation.ErrorCount, Is.Zero);
            Assert.That(
                room.TryGetPlayerEntryPoint(scenario.PlayerSpawnPointId, out _),
                Is.True);

            for (int index = 0; index < scenario.Encounter.SpawnSlotCount; index++)
            {
                D0EncounterSpawnSlot slot =
                    scenario.Encounter.GetSpawnSlot(index);
                Assert.That(
                    room.TryGetEnemySpawnPoint(slot.SpawnPointId, out _),
                    Is.True,
                    slot.SpawnPointId);
            }
        }

        [Test]
        public void RoomModeScenarioCompositionDoesNotRequireLegacyStage()
        {
            FpgRoomDefinition room =
                LoadRequired<FpgRoomDefinition>(RoomDefinitionPath);
            D0CombatScenarioDefinition clone = Object.Instantiate(
                LoadRequired<D0CombatScenarioDefinition>(ScenarioDefinitionPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                serialized.FindProperty("stageDefinition").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string legacyError), Is.False);
                Assert.That(legacyError, Is.EqualTo(
                    "Combat scenario requires a stage definition."));
                Assert.That(
                    clone.TryValidateForRoom(out string roomError),
                    Is.True,
                    roomError);
                Assert.That(
                    FpgRoomEncounterValidator.TryValidate(
                        room,
                        clone,
                        out FpgRoomEncounterValidationResult validation),
                    Is.True,
                    validation.FirstError == null
                        ? string.Empty
                        : validation.FirstError.Message);

                D0CombatScenarioTechnicalSettings settings =
                    new D0CombatScenarioTechnicalSettings(128, 128, 8, 32, 32);
                Assert.That(
                    clone.TryCreateDefinitionForRoom(
                        settings,
                        out ScenarioDefinition definition,
                        out string createError),
                    Is.True,
                    createError);
                Assert.That(definition, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void RoomEncounterValidationRejectsMissingPlayerEntryPoint()
        {
            FpgRoomDefinition room = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomDefinitionPath));
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(ScenarioDefinitionPath);
            try
            {
                SerializedObject serialized = new SerializedObject(room);
                serialized.FindProperty("playerEntryPoints")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("markerId").stringValue = "player-other";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgRoomEncounterValidator.TryValidate(
                        room,
                        scenario,
                        out FpgRoomEncounterValidationResult validation),
                    Is.False);
                FpgRoomEncounterValidationIssue issue = AssertEncounterIssue(
                    validation,
                    FpgRoomEncounterValidationCode.MissingPlayerEntryPoint);
                Assert.That(issue.MarkerId, Is.EqualTo("player-main"));
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void RoomEncounterValidationRejectsMissingEnemySpawnPoint()
        {
            FpgRoomDefinition room = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomDefinitionPath));
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(ScenarioDefinitionPath);
            try
            {
                SerializedObject serialized = new SerializedObject(room);
                serialized.FindProperty("enemySpawnPoints")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("markerId").stringValue = "enemy-other";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    FpgRoomEncounterValidator.TryValidate(
                        room,
                        scenario,
                        out FpgRoomEncounterValidationResult validation),
                    Is.False);
                FpgRoomEncounterValidationIssue issue = AssertEncounterIssue(
                    validation,
                    FpgRoomEncounterValidationCode.MissingEnemySpawnPoint);
                Assert.That(issue.MarkerId, Is.EqualTo("enemy-main"));
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        private static FpgRoomEncounterValidationIssue AssertEncounterIssue(
            FpgRoomEncounterValidationResult result,
            FpgRoomEncounterValidationCode expectedCode)
        {
            for (int index = 0; index < result.Issues.Count; index++)
            {
                FpgRoomEncounterValidationIssue issue = result.Issues[index];
                if (issue.Code == expectedCode)
                {
                    return issue;
                }
            }

            Assert.Fail($"Expected room/encounter validation issue '{expectedCode}'.");
            return null;
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Required authored asset is missing: {path}");
            return asset;
        }
    }
}
