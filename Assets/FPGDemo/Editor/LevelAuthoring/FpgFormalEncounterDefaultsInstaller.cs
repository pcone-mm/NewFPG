using System;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgFormalEncounterDefaultsInstaller
    {
        private const string Root = "Assets/FPGDemo/Config/FormalEncounter";
        private const int SweptProjectilePayloadKind = 0;
        private const int TimedImpactPayloadKind = 1;
        private const string LevelOneRoot = Root + "/Level1";
        private const string PrefabRoot = "Assets/FPGDemo/Presentation/FormalEncounter";
        private const string BurstbugAttackFastSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Fast.asset";
        private const string BurstbugAttackVolleySourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Volley.asset";
        private const string BurstbugAttackHeavySourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_HeavyBreak.asset";
        private const string BurstbugBehaviorSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Behavior.asset";
        private const string BurstbugPresentationSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Presentation.asset";
        private const string BurstbugTrainingSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Training.asset";
        private const string HudieAttackSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/Attacks/D0_Hudie_Attack_Bullet.asset";
        private const string HudiePresentationSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Presentation.asset";
        private const string LuanPresentationSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Presentation.asset";
        private const string LuanSummonSourcePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_SummonHudie.asset";

        [MenuItem(
            "FPG Demo/Formal Encounter/Install Burstbug Luan Hudie Defaults",
            priority = 130)]
        public static void Install()
        {
            EnsureFolder(Root);
            EnsureFolder(LevelOneRoot);
            EnsureFolder(PrefabRoot);
            try
            {
                D0ActorPresentationDefinition burstbugPresentationSource =
                    LoadRequired<D0ActorPresentationDefinition>(
                        BurstbugPresentationSourcePath);
                D0EnemyBehaviorProfile burstbugBehaviorSource =
                    LoadRequired<D0EnemyBehaviorProfile>(
                        BurstbugBehaviorSourcePath);
                D0EncounterDefinition burstbugTrainingSource =
                    LoadRequired<D0EncounterDefinition>(
                        BurstbugTrainingSourcePath);
                D0EnemyAttackDefinition burstbugFastSource =
                    LoadRequired<D0EnemyAttackDefinition>(
                        BurstbugAttackFastSourcePath);
                D0EnemyAttackDefinition burstbugVolleySource =
                    LoadRequired<D0EnemyAttackDefinition>(
                        BurstbugAttackVolleySourcePath);
                D0EnemyAttackDefinition burstbugHeavySource =
                    LoadRequired<D0EnemyAttackDefinition>(
                        BurstbugAttackHeavySourcePath);
                D0ActorPresentationDefinition hudiePresentationSource =
                    LoadRequired<D0ActorPresentationDefinition>(
                        HudiePresentationSourcePath);
                D0ActorPresentationDefinition luanPresentationSource =
                    LoadRequired<D0ActorPresentationDefinition>(
                        LuanPresentationSourcePath);
                D0EnemyAttackDefinition hudieAttackSource =
                    LoadRequired<D0EnemyAttackDefinition>(
                        HudieAttackSourcePath);
                D0LuanSummonHudieDefinition luanSummonSource =
                    LoadRequired<D0LuanSummonHudieDefinition>(
                        LuanSummonSourcePath);
                ValidateImportedSources(
                    hudiePresentationSource,
                    luanPresentationSource,
                    hudieAttackSource,
                    luanSummonSource,
                    out EnemyActorPresentationDefinition hudiePresentation,
                    out EnemyActorPresentationDefinition luanPresentation);
                ValidateBurstbugSources(
                    burstbugPresentationSource,
                    burstbugBehaviorSource,
                    burstbugTrainingSource,
                    burstbugFastSource,
                    burstbugVolleySource,
                    burstbugHeavySource,
                    out EnemyActorPresentationDefinition burstbugPresentation,
                    out AttackCadence burstbugFastCadence,
                    out AttackCadence burstbugVolleyCadence,
                    out AttackCadence burstbugHeavyCadence);

                GameObject burstbugPrefab = CreateFormalPrefab(
                    "Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab",
                    PrefabRoot + "/PF_FPG_BurstbugEntity.prefab");
                GameObject hudiePrefab = CreateFormalPrefab(
                    "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab",
                    PrefabRoot + "/PF_FPG_HudieEntity.prefab");
                GameObject luanPrefab = CreateFormalPrefab(
                    "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab",
                    PrefabRoot + "/PF_FPG_LuanEntity.prefab");

                FpgEnemyBehaviorDefinition burstbugBehavior = CreateBehavior(
                    "Burstbug",
                    (int)MapBehaviorMode(burstbugBehaviorSource.BehaviorMode),
                    burstbugBehaviorSource.EntrySpeed,
                    burstbugBehaviorSource.PatrolSpeed,
                    burstbugPresentation,
                    burstbugBehaviorSource.StopDuringThreat);
                FpgEnemyBehaviorDefinition hudieBehavior = CreateBehavior(
                    "Hudie",
                    2,
                    3f,
                    2f,
                    hudiePresentation,
                    true);
                FpgEnemyBehaviorDefinition luanBehavior = CreateBehavior(
                    "Luan",
                    0,
                    2.5f,
                    0f,
                    luanPresentation,
                    true);

                FpgEnemyAttackDefinition burstbugFast =
                    CreateAttackFromSource(
                        Root + "/FPG_Burstbug_Attack.asset",
                        burstbugFastSource.AttackId,
                        burstbugFastSource,
                        burstbugFastCadence);
                FpgEnemyAttackDefinition burstbugVolley =
                    CreateAttackFromSource(
                        Root + "/FPG_Burstbug_Attack_Volley.asset",
                        burstbugVolleySource.AttackId,
                        burstbugVolleySource,
                        burstbugVolleyCadence);
                FpgEnemyAttackDefinition burstbugHeavy =
                    CreateAttackFromSource(
                        Root + "/FPG_Burstbug_Attack_HeavyBreak.asset",
                        burstbugHeavySource.AttackId,
                        burstbugHeavySource,
                        burstbugHeavyCadence);
                FpgEnemyAttackDefinition hudieAttack =
                    CreateAttackFromSource(
                        Root + "/FPG_Hudie_Attack.asset",
                        "hudie-projectile",
                        hudieAttackSource,
                        new AttackCadence(
                            60,
                            Math.Max(
                                1,
                                hudieAttackSource.WindupTicks
                                    + hudieAttackSource.RecoveryTicks)));

                FpgEnemyDefinition burstbug = CreateEnemy(
                    "burstbug", "Burstbug", (int)FpgEnemyRole.Melee,
                    120, 30, 2, 1,
                    burstbugPrefab,
                    burstbugBehavior,
                    new[] { burstbugFast, burstbugVolley, burstbugHeavy });
                FpgEnemyDefinition hudie = CreateEnemy(
                    "hudie", "Hudie", (int)FpgEnemyRole.Ranged, 90, 20, 2, 1,
                    hudiePrefab, hudieBehavior, new[] { hudieAttack });

                FpgSummonActionDefinition summonHudie = LoadOrCreate<FpgSummonActionDefinition>(
                    Root + "/FPG_Luan_SummonHudie.asset");
                SerializedObject summon = new SerializedObject(summonHudie);
                SetString(summon, "actionId", "luan-summon-hudie");
                SetString(summon, "displayName", "Summon Hudie");
                SetObjectArray(summon, "candidateEnemies", new UnityEngine.Object[] { hudie });
                SetIntArray(summon, "candidateWeights", new[] { 1 });
                SetInt(summon, "maxSummonsPerOwner", 1);
                SetInt(summon, "maxTotalSummonsPerEncounter", 6);
                SetInt(summon, "maxRecursionDepth", 1);
                SetInt(
                    summon,
                    "cooldownTicks",
                    Math.Max(1, luanSummonSource.SummonTick));
                summon.ApplyModifiedPropertiesWithoutUndo();

                FpgEnemyAttackDefinition luanAttack = LoadOrCreate<FpgEnemyAttackDefinition>(
                    Root + "/FPG_Luan_Attack_Summon.asset");
                SerializedObject luanAttackData = new SerializedObject(luanAttack);
                SetString(luanAttackData, "attackId", "luan-summon");
                SetString(luanAttackData, "displayName", "Luan Summon");
                SetInt(luanAttackData, "kind", 2);
                SetInt(
                    luanAttackData,
                    "firstReadyOffsetTicks",
                    luanSummonSource.SummonTick);
                SetInt(
                    luanAttackData,
                    "cooldownTicks",
                    Math.Max(1, luanSummonSource.SummonTick));
                SetInt(luanAttackData, "telegraphTicks", 0);
                SetInt(
                    luanAttackData,
                    "windupTicks",
                    Math.Max(
                        0,
                        luanSummonSource.AppearanceTick
                            - luanSummonSource.SummonTick));
                SetInt(luanAttackData, "damage", 0);
                SetInt(luanAttackData, "breakDamage", 0);
                SetBool(luanAttackData, "interceptable", false);
                SetInt(luanAttackData, "recoveryTicks", 0);
                SetString(
                    luanAttackData,
                    "animationSlot",
                    luanSummonSource.SummonAnimation);
                SetString(luanAttackData, "warningSlot", "enemy-summon-warning");
                SetObject(luanAttackData, "summon", summonHudie);
                SetInt(
                    luanAttackData,
                    "summonOwnerOutcome",
                    (int)FpgSummonOwnerOutcome.DieAfterSuccessfulSummon);
                luanAttackData.ApplyModifiedPropertiesWithoutUndo();

                FpgEnemyDefinition luan = CreateEnemy(
                    "luan", "Luan", (int)FpgEnemyRole.Support, 180, 45, 4, 2,
                    luanPrefab, luanBehavior, new[] { luanAttack });

                FpgFormalAttackRuntimeCatalog attackRuntimeCatalog =
                    LoadOrCreate<FpgFormalAttackRuntimeCatalog>(
                        Root + "/FPG_NormalRoom_AttackRuntimeCatalog.asset");
                SerializedObject attackRuntimeData = new SerializedObject(attackRuntimeCatalog);
                SerializedProperty attackRuntimeEntries = attackRuntimeData.FindProperty("entries");
                attackRuntimeEntries.arraySize = 5;
                ConfigureAttackRuntimeEntryFromSource(
                    attackRuntimeEntries.GetArrayElementAtIndex(0),
                    burstbugFast,
                    burstbugFastSource);
                ConfigureAttackRuntimeEntryFromSource(
                    attackRuntimeEntries.GetArrayElementAtIndex(1),
                    burstbugVolley,
                    burstbugVolleySource);
                ConfigureAttackRuntimeEntryFromSource(
                    attackRuntimeEntries.GetArrayElementAtIndex(2),
                    burstbugHeavy,
                    burstbugHeavySource);
                ConfigureAttackRuntimeEntryFromSource(
                    attackRuntimeEntries.GetArrayElementAtIndex(3),
                    hudieAttack,
                    hudieAttackSource);
                ConfigureAttackRuntimeEntry(
                    attackRuntimeEntries.GetArrayElementAtIndex(4),
                    luanAttack, 1003, 0, 1, 103, 103, 0, 103);
                attackRuntimeData.ApplyModifiedPropertiesWithoutUndo();

                FpgEnemyPoolDefinition pool = LoadOrCreate<FpgEnemyPoolDefinition>(
                    Root + "/FPG_NormalRoom_EnemyPool.asset");
                SerializedObject poolData = new SerializedObject(pool);
                SetString(poolData, "poolId", "normal-room-core");
                SetString(poolData, "displayName", "Normal Room Core");
                SerializedProperty entries = poolData.FindProperty("entries");
                entries.arraySize = 3;
                ConfigurePoolEntry(entries.GetArrayElementAtIndex(0), burstbug, 5, 0, 99, 8, 16, true);
                ConfigurePoolEntry(entries.GetArrayElementAtIndex(1), hudie, 4, 0, 99, 8, 24, true);
                ConfigurePoolEntry(entries.GetArrayElementAtIndex(2), luan, 2, 1, 99, 2, 4, true);
                poolData.ApplyModifiedPropertiesWithoutUndo();

                FpgEnemyDefinitionCatalog catalog = LoadOrCreate<FpgEnemyDefinitionCatalog>(
                    Root + "/FPG_NormalRoom_EnemyCatalog.asset");
                SerializedObject catalogData = new SerializedObject(catalog);
                SetObjectArray(
                    catalogData,
                    "definitions",
                    new UnityEngine.Object[] { burstbug, hudie, luan });
                catalogData.ApplyModifiedPropertiesWithoutUndo();

                FpgEncounterProfile profile = LoadOrCreate<FpgEncounterProfile>(
                    Root + "/FPG_NormalRoom_Profile.asset");
                SerializedObject profileData = new SerializedObject(profile);
                SetString(profileData, "profileId", "normal-room-v1");
                SetString(profileData, "displayName", "Formal Normal Room v1");
                SetInt(profileData, "baseBudget", 8);
                SetInt(profileData, "depthRamp", 2);
                SetInt(profileData, "minBudget", 8);
                SetInt(profileData, "waveBudgetTemplate", 2);
                ConfigureHadesWaveLayouts(profileData);
                SetInt(profileData, "maxConcurrentCapWeight", 5);
                SetInt(profileData, "maxConcurrentEntities", 4);
                SetInt(profileData, "spawnIntervalTicks", 18);
                SetInt(profileData, "warningDurationTicks", 30);
                SetInt(profileData, "waveIntervalTicks", 60);
                SetInt(profileData, "enemyRosterCapacity", 24);
                SetInt(profileData, "entityPoolCapacity", 24);
                SetInt(profileData, "hitboxCapacity", 64);
                SetInt(profileData, "threatCapacity", 48);
                SetInt(profileData, "projectileCapacity", 32);
                SetInt(profileData, "warningCapacity", 8);
                SetInt(profileData, "overheadHealthBarCapacity", 8);
                SetObject(profileData, "enemyPool", pool);
                profileData.ApplyModifiedPropertiesWithoutUndo();

                FpgEncounterProfile levelOneProfile = CreateLevelOnePresets(
                    burstbug,
                    hudie,
                    luan);

                ValidateInstalledAssets(
                    burstbug,
                    hudie,
                    luan,
                    pool,
                    catalog,
                    attackRuntimeCatalog,
                    profile,
                    levelOneProfile);
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(pool);
                EditorUtility.SetDirty(catalog);
                EditorUtility.SetDirty(attackRuntimeCatalog);
                AssetDatabase.SaveAssets();
                Selection.activeObject = levelOneProfile;
                Debug.Log(
                    "[FPG Formal Encounter] Installed Burstbug, Luan and "
                    + "Hudie formal prefabs, attacks, presentation mappings "
                    + "and four L1_01 fixed-wave presets.");
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        private static FpgEnemyBehaviorDefinition CreateBehavior(
            string name, int mode, float entrySpeed, float moveSpeed,
            EnemyActorPresentationDefinition presentation,
            bool stopDuringAttack)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            FpgEnemyBehaviorDefinition asset = LoadOrCreate<FpgEnemyBehaviorDefinition>(
                Root + $"/FPG_{name}_Behavior.asset");
            SerializedObject data = new SerializedObject(asset);
            SetString(data, "behaviorId", name.ToLowerInvariant() + "-behavior");
            SetString(data, "displayName", name + " Behavior");
            SetInt(data, "mode", mode);
            SetFloat(data, "entrySpeed", entrySpeed);
            SetFloat(data, "moveSpeed", moveSpeed);
            SetBool(data, "stopDuringAttack", stopDuringAttack);
            SetString(data, "entryAnimation", presentation.EnterAnimation);
            SetString(data, "idleAnimation", presentation.IdleAnimation);
            SetString(data, "deathAnimation", presentation.DeathAnimation);
            data.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static FpgEnemyAttackDefinition CreateAttackFromSource(
            string targetPath,
            string attackId,
            D0EnemyAttackDefinition source,
            AttackCadence cadence)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            FpgEnemyAttackDefinition asset = LoadOrCreate<FpgEnemyAttackDefinition>(
                targetPath);
            SerializedObject data = new SerializedObject(asset);
            SerializedObject sourceData = new SerializedObject(source);
            FpgEnemyAttackKind kind;
            int damage;
            int breakDamage;
            switch (GetInt(sourceData, "payloadKind"))
            {
                case SweptProjectilePayloadKind:
                    kind = FpgEnemyAttackKind.Projectile;
                    damage = GetInt(sourceData, "projectileDamage");
                    breakDamage = GetInt(sourceData, "projectileBreakDamage");
                    break;

                case TimedImpactPayloadKind:
                    kind = FpgEnemyAttackKind.TimedImpact;
                    damage = GetInt(sourceData, "timedImpactDamage");
                    breakDamage = GetInt(sourceData, "timedImpactBreakDamage");
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported formal attack source '{source.name}'.");
            }

            SetString(data, "attackId", attackId);
            SetString(data, "displayName", source.DisplayName);
            SetInt(data, "priority", 0);
            SetInt(data, "kind", (int)kind);
            SetInt(data, "firstReadyOffsetTicks", cadence.FirstReadyTick);
            SetInt(data, "cooldownTicks", cadence.CooldownTicks);
            SetInt(data, "telegraphTicks", source.TelegraphTicks);
            SetInt(data, "windupTicks", source.WindupTicks);
            SetInt(data, "recoveryTicks", source.RecoveryTicks);
            SetInt(data, "damage", damage);
            SetInt(data, "breakDamage", breakDamage);
            SetInt(data, "projectileCount", source.PayloadCount);
            SetInt(
                data,
                "projectileDefinitionId",
                GetInt(sourceData, "projectileDefinitionId"));
            SetInt(
                data,
                "projectileFlightTicks",
                GetInt(sourceData, "projectileFlightTicks"));
            SetInt(
                data,
                "projectileLifetimeTicks",
                GetInt(sourceData, "projectileExpireTicks"));
            SetBool(data, "interceptable", source.ProjectileInterceptable);
            SetObject(data, "summon", null);
            SetString(data, "animationSlot", source.AttackAnimation);
            SetString(data, "warningSlot", source.WarningSlot);
            data.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static FpgEnemyDefinition CreateEnemy(
            string id, string displayName, int role, int life, int breakValue,
            int spawnCost, int capWeight, GameObject prefab,
            FpgEnemyBehaviorDefinition behavior, FpgEnemyAttackDefinition[] attacks)
        {
            FpgEnemyDefinition asset = LoadOrCreate<FpgEnemyDefinition>(
                Root + $"/FPG_{displayName}_Enemy.asset");
            SerializedObject data = new SerializedObject(asset);
            SetString(data, "enemyDefinitionId", id);
            SetString(data, "displayName", displayName);
            SetInt(data, "role", role);
            SetInt(data, "life", life);
            SetInt(data, "breakValue", breakValue);
            SetInt(data, "spawnCost", spawnCost);
            SetInt(data, "capWeight", capWeight);
            SetObject(data, "entityViewPrefab", prefab);
            SetObject(data, "behavior", behavior);
            SetObjectArray(data, "attackPatterns", attacks);
            data.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static FpgEncounterProfile CreateLevelOnePresets(
            FpgEnemyDefinition burstbug,
            FpgEnemyDefinition hudie,
            FpgEnemyDefinition luan)
        {
            FpgEnemyPoolDefinition pool = LoadOrCreate<FpgEnemyPoolDefinition>(
                LevelOneRoot + "/FPG_L1_01_EnemyPool.asset");
            SerializedObject poolData = new SerializedObject(pool);
            SetString(poolData, "poolId", "l1-01-core");
            SetString(poolData, "displayName", "L1_01 Core");
            SerializedProperty entries = poolData.FindProperty("entries");
            entries.arraySize = 3;
            ConfigurePoolEntry(entries.GetArrayElementAtIndex(0), burstbug, 5, 0, 99, 8, 16, true);
            ConfigurePoolEntry(entries.GetArrayElementAtIndex(1), hudie, 4, 0, 99, 16, 32, true);
            ConfigurePoolEntry(entries.GetArrayElementAtIndex(2), luan, 2, 0, 99, 1, 1, true);
            poolData.ApplyModifiedPropertiesWithoutUndo();

            FpgEncounterProfile profile = LoadOrCreate<FpgEncounterProfile>(
                LevelOneRoot + "/FPG_L1_01_Profile.asset");
            SerializedObject profileData = new SerializedObject(profile);
            SetString(profileData, "profileId", "l1-01-fixed-three-wave-v1");
            SetString(profileData, "displayName", "L1_01 Fixed Three-Wave v1");
            SetString(
                profileData,
                "designerNotes",
                "Shared by the four L1_01 fixed-wave presets. Two simultaneous enemies match the current CombatLab marker capacity.");
            SetInt(profileData, "baseBudget", 12);
            SetInt(profileData, "depthRamp", 0);
            SetInt(profileData, "minBudget", 12);
            SetInt(profileData, "defaultDifficultyMultiplierBasisPoints", 10000);
            SetInt(profileData, "waveBudgetTemplate", 3);
            ConfigureWaveShares(profileData, "customWaveShares", new[] { 3334, 3333, 3333 });
            ConfigureSingleWaveLayout(
                profileData,
                "l1-01-triple-equal",
                new[] { 3334, 3333, 3333 });
            SetInt(profileData, "maxConcurrentCapWeight", 3);
            SetInt(profileData, "maxConcurrentEntities", 2);
            SetInt(profileData, "spawnIntervalTicks", 18);
            SetInt(profileData, "warningDurationTicks", 30);
            SetInt(profileData, "waveIntervalTicks", 60);
            SetInt(profileData, "spawnSafetyDistanceUnits", 4);
            SetInt(profileData, "entrySafetyDistanceUnits", 2);
            SetInt(profileData, "softDistanceRelaxationStepUnits", 1);
            SetInt(profileData, "softDistanceRelaxationAttempts", 4);
            SetInt(profileData, "maxSpawnWaitTicks", 120);
            SetInt(profileData, "enemyRosterCapacity", 24);
            SetInt(profileData, "entityPoolCapacity", 24);
            SetInt(profileData, "hitboxCapacity", 64);
            SetInt(profileData, "threatCapacity", 48);
            SetInt(profileData, "projectileCapacity", 32);
            SetInt(profileData, "warningCapacity", 8);
            SetInt(profileData, "overheadHealthBarCapacity", 8);
            SetObject(profileData, "enemyPool", pool);
            profileData.ApplyModifiedPropertiesWithoutUndo();

            CreateFixedWavesOverride(
                "FPG_L1_01_01_Intro",
                "l1-01-intro",
                12,
                new PresetSpawn(0, burstbug, 2),
                new PresetSpawn(1, burstbug, 1),
                new PresetSpawn(1, hudie, 1),
                new PresetSpawn(2, luan, 1));
            CreateFixedWavesOverride(
                "FPG_L1_01_02_Mixed",
                "l1-01-mixed",
                18,
                new PresetSpawn(0, burstbug, 2),
                new PresetSpawn(0, hudie, 1),
                new PresetSpawn(1, burstbug, 1),
                new PresetSpawn(1, hudie, 2),
                new PresetSpawn(2, luan, 1),
                new PresetSpawn(2, burstbug, 1));
            CreateFixedWavesOverride(
                "FPG_L1_01_03_RangedPressure",
                "l1-01-ranged-pressure",
                18,
                new PresetSpawn(0, burstbug, 1),
                new PresetSpawn(0, hudie, 2),
                new PresetSpawn(1, hudie, 3),
                new PresetSpawn(2, luan, 1),
                new PresetSpawn(2, hudie, 1));
            CreateFixedWavesOverride(
                "FPG_L1_01_04_Challenge",
                "l1-01-challenge",
                24,
                new PresetSpawn(0, burstbug, 3),
                new PresetSpawn(0, hudie, 1),
                new PresetSpawn(1, burstbug, 2),
                new PresetSpawn(1, hudie, 2),
                new PresetSpawn(2, luan, 1),
                new PresetSpawn(2, hudie, 2));

            EditorUtility.SetDirty(pool);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void CreateFixedWavesOverride(
            string assetName,
            string overrideId,
            int lockedBudget,
            params PresetSpawn[] spawns)
        {
            FpgEncounterOverrideDefinition asset = LoadOrCreate<FpgEncounterOverrideDefinition>(
                LevelOneRoot + "/" + assetName + ".asset");
            SerializedObject data = new SerializedObject(asset);
            SetString(data, "overrideId", overrideId);
            SetInt(data, "mode", (int)FpgEncounterOverrideMode.FixedWaves);
            data.FindProperty("forcedEnemies").arraySize = 0;
            data.FindProperty("excludedEnemyDefinitionIds").arraySize = 0;
            data.FindProperty("excludedEnemies").arraySize = 0;
            SetBool(data, "lockBudget", true);
            SetInt(data, "lockedBudget", lockedBudget);

            SerializedProperty fixedSpawns = data.FindProperty("fixedSpawns");
            fixedSpawns.arraySize = spawns.Length;
            for (int index = 0; index < spawns.Length; index++)
            {
                SerializedProperty entry = fixedSpawns.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("waveIndex").intValue = spawns[index].WaveIndex;
                entry.FindPropertyRelative("enemy").objectReferenceValue = spawns[index].Enemy;
                entry.FindPropertyRelative("count").intValue = spawns[index].Count;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private readonly struct PresetSpawn
        {
            public PresetSpawn(int waveIndex, FpgEnemyDefinition enemy, int count)
            {
                WaveIndex = waveIndex;
                Enemy = enemy;
                Count = count;
            }

            public int WaveIndex { get; }
            public FpgEnemyDefinition Enemy { get; }
            public int Count { get; }
        }

        private static GameObject CreateFormalPrefab(string sourcePath, string targetPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing source prefab: " + sourcePath);

            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Cannot instantiate source prefab: " + sourcePath);
            instance.name = System.IO.Path.GetFileNameWithoutExtension(targetPath);

            D0EnemyEntityView legacy = instance.GetComponent<D0EnemyEntityView>();
            UnityEngine.Object skeletonAnimation = null;
            Component[] components =
                instance.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component candidate = components[index];
                if (candidate != null
                    && string.Equals(
                        candidate.GetType().FullName,
                        "Spine.Unity.SkeletonAnimation",
                        StringComparison.Ordinal))
                {
                    skeletonAnimation = candidate;
                    break;
                }
            }
            if (legacy == null
                || legacy.BodyHitbox == null
                || skeletonAnimation == null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                throw new InvalidOperationException(
                    "Formal enemy source is missing its legacy entity or "
                    + "SkeletonAnimation binding: "
                    + sourcePath);
            }

            Transform gameplayAnchor = legacy.GameplayAnchor;
            Transform projectileAnchor = legacy.ProjectileSpawnAnchor;
            Transform weakpointAnchor = legacy.WeakpointAnchor;
            Collider[] colliders = legacy.HasWeakpoint && legacy.WeakpointHitbox != null
                ? new[] { legacy.BodyHitbox, legacy.WeakpointHitbox }
                : new[] { legacy.BodyHitbox };
            int[] hitPartKinds = colliders.Length == 2
                ? new[] { 0, 1 }
                : new[] { 0 };

            UnityEngine.Object.DestroyImmediate(legacy);
            Actor2DPresenter[] legacyPresenters =
                instance.GetComponentsInChildren<Actor2DPresenter>(true);
            for (int index = 0; index < legacyPresenters.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(legacyPresenters[index]);
            }

            FpgEnemyEntityView formal = instance.GetComponent<FpgEnemyEntityView>();
            if (formal == null) formal = instance.AddComponent<FpgEnemyEntityView>();
            SerializedObject view = new SerializedObject(formal);
            SetObject(view, "gameplayAnchor", gameplayAnchor);
            SetObject(view, "projectileAnchor", projectileAnchor);
            SetObject(view, "weakpointAnchor", weakpointAnchor);
            SetObject(view, "overheadHealthBarAnchor", weakpointAnchor);
            SetObjectArray(view, "hitParts", colliders);
            SetIntArray(view, "hitPartKinds", hitPartKinds);
            SetObject(view, "skeletonAnimation", skeletonAnimation);
            view.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = null;
            try
            {
                saved = PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            if (saved == null)
            {
                throw new InvalidOperationException("Cannot save formal prefab: " + targetPath);
            }

            return saved;
        }

        private static void ValidateImportedSources(
            D0ActorPresentationDefinition hudiePresentationSource,
            D0ActorPresentationDefinition luanPresentationSource,
            D0EnemyAttackDefinition hudieAttackSource,
            D0LuanSummonHudieDefinition luanSummonSource,
            out EnemyActorPresentationDefinition hudiePresentation,
            out EnemyActorPresentationDefinition luanPresentation)
        {
            hudiePresentation = null;
            luanPresentation = null;
            string error = string.Empty;
            if (!hudiePresentationSource.TryGetEnemy(
                    out hudiePresentation)
                || !hudiePresentation.TryValidate(out error))
            {
                throw new InvalidOperationException(
                    "Hudie presentation source is invalid: " + error);
            }

            if (!luanPresentationSource.TryGetEnemy(
                    out luanPresentation)
                || !luanPresentation.TryValidate(out error))
            {
                throw new InvalidOperationException(
                    "Luan presentation source is invalid: " + error);
            }

            SerializedObject attackData =
                new SerializedObject(hudieAttackSource);
            if (!hudieAttackSource.TryValidate(out error)
                || GetInt(attackData, "payloadKind") != 0)
            {
                throw new InvalidOperationException(
                    "Hudie projectile source is invalid: " + error);
            }

            if (luanSummonSource.SummonTick < 0
                || luanSummonSource.AppearanceTick
                    < luanSummonSource.SummonTick
                || string.IsNullOrWhiteSpace(
                    luanSummonSource.SummonAnimation)
                || string.IsNullOrWhiteSpace(
                    luanSummonSource.AppearanceAnimation)
                || !string.Equals(
                    luanSummonSource.AppearanceAnimation,
                    hudiePresentation.EnterAnimation,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Luan summon source timing or animation mapping does "
                    + "not match the imported Hudie presentation.");
            }
        }

        private static void ValidateBurstbugSources(
            D0ActorPresentationDefinition presentationSource,
            D0EnemyBehaviorProfile behaviorSource,
            D0EncounterDefinition trainingSource,
            D0EnemyAttackDefinition fastSource,
            D0EnemyAttackDefinition volleySource,
            D0EnemyAttackDefinition heavySource,
            out EnemyActorPresentationDefinition presentation,
            out AttackCadence fastCadence,
            out AttackCadence volleyCadence,
            out AttackCadence heavyCadence)
        {
            presentation = null;
            string error = string.Empty;
            if (!presentationSource.TryValidate(out error)
                || !presentationSource.TryGetEnemy(out presentation)
                || !presentation.TryValidate(out error))
            {
                throw new InvalidOperationException(
                    "Burstbug presentation source is invalid: " + error);
            }

            if (!behaviorSource.TryValidate(out error)
                || !string.Equals(
                    behaviorSource.EntryAnimationSlot,
                    presentation.EnterAnimation,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Burstbug behavior source is invalid or its entry "
                    + "animation does not match the presentation: " + error);
            }

            if (!trainingSource.TryValidate(out error))
            {
                throw new InvalidOperationException(
                    "Burstbug training source is invalid: " + error);
            }

            ValidateAttackSource(fastSource, SweptProjectilePayloadKind);
            ValidateAttackSource(volleySource, SweptProjectilePayloadKind);
            ValidateAttackSource(heavySource, TimedImpactPayloadKind);
            fastCadence = ResolveAttackCadence(trainingSource, fastSource);
            volleyCadence = ResolveAttackCadence(trainingSource, volleySource);
            heavyCadence = ResolveAttackCadence(trainingSource, heavySource);
        }

        private static void ValidateAttackSource(
            D0EnemyAttackDefinition source,
            int expectedKind)
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Burstbug attack source is missing.");
            }

            SerializedObject sourceData = new SerializedObject(source);
            if (!source.TryValidate(out string error)
                || GetInt(sourceData, "payloadKind") != expectedKind)
            {
                throw new InvalidOperationException(
                    $"Burstbug attack source '{source.name}' is invalid or "
                    + $"has the wrong payload kind: {error}");
            }

            if (expectedKind == TimedImpactPayloadKind)
            {
                if (GetInt(sourceData, "timedImpactTargetPolicy") != 0)
                {
                    throw new InvalidOperationException(
                        $"Burstbug timed-impact source '{source.name}' must "
                        + "target the player combatant.");
                }
            }
        }

        private static AttackCadence ResolveAttackCadence(
            D0EncounterDefinition training,
            D0EnemyAttackDefinition attack)
        {
            int firstReadyTick = -1;
            int previousTick = -1;
            int cooldownTicks = -1;
            int occurrenceCount = 0;
            for (int index = 0; index < training.AttackScheduleCount; index++)
            {
                D0EncounterAttackScheduleEntry entry =
                    training.GetAttackScheduleEntry(index);
                if (entry.Attack != attack)
                {
                    continue;
                }

                occurrenceCount++;
                if (firstReadyTick < 0)
                {
                    firstReadyTick = entry.DueTick;
                }
                else
                {
                    int interval = entry.DueTick - previousTick;
                    if (interval <= 0
                        || (cooldownTicks > 0 && cooldownTicks != interval))
                    {
                        throw new InvalidOperationException(
                            $"Burstbug training cadence for '{attack.name}' "
                            + "must repeat at one positive interval.");
                    }

                    cooldownTicks = interval;
                }

                previousTick = entry.DueTick;
            }

            if (occurrenceCount < 2
                || firstReadyTick < 0
                || cooldownTicks <= 0)
            {
                throw new InvalidOperationException(
                    $"Burstbug training must schedule '{attack.name}' at "
                    + "least twice so formal cadence can be derived.");
            }

            return new AttackCadence(firstReadyTick, cooldownTicks);
        }

        private static FpgEnemyBehaviorMode MapBehaviorMode(
            D0EnemyBehaviorMode mode)
        {
            switch (mode)
            {
                case D0EnemyBehaviorMode.Patrol:
                    return FpgEnemyBehaviorMode.Patrol;
                case D0EnemyBehaviorMode.FixedPosition:
                    return FpgEnemyBehaviorMode.FixedPosition;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported D0 behavior mode '{mode}'.");
            }
        }


        private static void ValidateInstalledAssets(
            FpgEnemyDefinition burstbug,
            FpgEnemyDefinition hudie,
            FpgEnemyDefinition luan,
            FpgEnemyPoolDefinition normalPool,
            FpgEnemyDefinitionCatalog catalog,
            FpgFormalAttackRuntimeCatalog attackCatalog,
            FpgEncounterProfile normalProfile,
            FpgEncounterProfile levelOneProfile)
        {
            RequireValid(
                burstbug.TryValidate(out string error),
                burstbug.name,
                error);
            RequireValid(hudie.TryValidate(out error), hudie.name, error);
            RequireValid(luan.TryValidate(out error), luan.name, error);
            RequireValid(
                normalPool.TryValidate(out error),
                normalPool.name,
                error);
            RequireValid(
                catalog.TryValidate(out error),
                catalog.name,
                error);
            RequireValid(
                attackCatalog.TryValidate(out error),
                attackCatalog.name,
                error);
            RequireValid(
                normalProfile.TryValidate(out error),
                normalProfile.name,
                error);
            RequireValid(
                levelOneProfile.TryValidate(out error),
                levelOneProfile.name,
                error);

            if (normalPool.EntryCount != 3
                || catalog.Count != 3
                || attackCatalog.EntryCount != 5
                || levelOneProfile.EnemyPool.EntryCount != 3)
            {
                throw new InvalidOperationException(
                    "Formal NormalRoom/L1_01 catalogs must contain Burstbug, "
                    + "Hudie and Luan with five runtime attack entries.");
            }

            string[] overrideNames =
            {
                "FPG_L1_01_01_Intro",
                "FPG_L1_01_02_Mixed",
                "FPG_L1_01_03_RangedPressure",
                "FPG_L1_01_04_Challenge"
            };
            for (int index = 0; index < overrideNames.Length; index++)
            {
                FpgEncounterOverrideDefinition preset =
                    LoadRequired<FpgEncounterOverrideDefinition>(
                        LevelOneRoot + "/" + overrideNames[index] + ".asset");
                RequireValid(
                    preset.TryValidate(out error),
                    preset.name,
                    error);
                for (int spawnIndex = 0;
                     spawnIndex < preset.FixedSpawns.Count;
                     spawnIndex++)
                {
                    FpgEnemyDefinition enemy =
                        preset.FixedSpawns[spawnIndex].Enemy;
                    if (enemy != burstbug && enemy != hudie && enemy != luan)
                    {
                        throw new InvalidOperationException(
                            $"Preset '{preset.name}' references non-formal "
                            + $"enemy '{enemy?.name ?? "<missing>"}'.");
                    }
                }
            }
        }

        private static void RequireValid(
            bool valid,
            string assetName,
            string error)
        {
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"Formal asset '{assetName}' is invalid: {error}");
            }
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Missing required imported asset: " + path);
            }

            return asset;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            if (System.IO.File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureAttackRuntimeEntry(
            SerializedProperty entry,
            FpgEnemyAttackDefinition attack,
            int threatDefinitionId,
            int projectileMaxHitPoints,
            int projectileBudgetUnits,
            int projectilePresentationKey,
            int projectileSweepRadiusKey,
            int timedImpactDelayTicks,
            int timedImpactPresentationKey)
        {
            entry.FindPropertyRelative("attack").objectReferenceValue = attack;
            entry.FindPropertyRelative("threatDefinitionId").intValue = threatDefinitionId;
            entry.FindPropertyRelative("weakpointDamageMultiplierBasisPoints").intValue = 10000;
            entry.FindPropertyRelative("weakpointBreakMultiplierBasisPoints").intValue = 10000;
            entry.FindPropertyRelative("projectileMaxHitPoints").intValue = projectileMaxHitPoints;
            entry.FindPropertyRelative("projectileBudgetUnits").intValue = projectileBudgetUnits;
            entry.FindPropertyRelative("projectilePresentationKey").intValue = projectilePresentationKey;
            entry.FindPropertyRelative("projectileSweepRadiusKey").intValue = projectileSweepRadiusKey;
            entry.FindPropertyRelative("timedImpactDelayTicks").intValue = timedImpactDelayTicks;
            entry.FindPropertyRelative("timedImpactPresentationKey").intValue = timedImpactPresentationKey;
        }

        private static void ConfigureAttackRuntimeEntryFromSource(
            SerializedProperty entry,
            FpgEnemyAttackDefinition attack,
            D0EnemyAttackDefinition source)
        {
            SerializedObject sourceData = new SerializedObject(source);
            ConfigureAttackRuntimeEntry(
                entry,
                attack,
                source.DefinitionId,
                GetInt(sourceData, "projectileHitPoints"),
                GetInt(sourceData, "projectileBudgetUnits"),
                source.PresentationKey,
                GetInt(sourceData, "sweepRadiusKey"),
                GetInt(sourceData, "timedImpactDelayTicks"),
                source.PresentationKey);
        }


        private static void ConfigurePoolEntry(
            SerializedProperty entry, FpgEnemyDefinition enemy, int weight,
            int minDepth, int maxDepth, int maxPerWave, int maxPerRoom, bool theme)
        {
            entry.FindPropertyRelative("enemy").objectReferenceValue = enemy;
            entry.FindPropertyRelative("selectionWeight").intValue = weight;
            entry.FindPropertyRelative("minDepth").intValue = minDepth;
            entry.FindPropertyRelative("maxDepth").intValue = maxDepth;
            entry.FindPropertyRelative("maxPerWave").intValue = maxPerWave;
            entry.FindPropertyRelative("maxPerRoom").intValue = maxPerRoom;
            entry.FindPropertyRelative("themeEligible").boolValue = theme;
        }

        private static void ConfigureHadesWaveLayouts(SerializedObject profile)
        {
            SerializedProperty layouts = profile.FindProperty("weightedWaveLayouts");
            layouts.arraySize = 3;
            ConfigureWaveLayout(
                layouts.GetArrayElementAtIndex(0),
                "single-100",
                1,
                new[] { 10000 });
            ConfigureWaveLayout(
                layouts.GetArrayElementAtIndex(1),
                "double-50-50",
                1,
                new[] { 5000, 5000 });
            ConfigureWaveLayout(
                layouts.GetArrayElementAtIndex(2),
                "triple-30-15-55",
                1,
                new[] { 3000, 1500, 5500 });
        }

        private static void ConfigureSingleWaveLayout(
            SerializedObject profile,
            string layoutId,
            int[] basisPoints)
        {
            SerializedProperty layouts = profile.FindProperty("weightedWaveLayouts");
            layouts.arraySize = 1;
            ConfigureWaveLayout(layouts.GetArrayElementAtIndex(0), layoutId, 1, basisPoints);
        }

        private static void ConfigureWaveShares(
            SerializedObject profile,
            string propertyName,
            int[] basisPoints)
        {
            SerializedProperty shares = profile.FindProperty(propertyName);
            shares.arraySize = basisPoints.Length;
            for (int index = 0; index < basisPoints.Length; index++)
            {
                shares.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("basisPoints").intValue = basisPoints[index];
            }
        }

        private static void ConfigureWaveLayout(
            SerializedProperty layout,
            string layoutId,
            int selectionWeight,
            int[] basisPoints)
        {
            layout.FindPropertyRelative("layoutId").stringValue = layoutId;
            layout.FindPropertyRelative("selectionWeight").intValue = selectionWeight;
            SerializedProperty shares = layout.FindPropertyRelative("waveShares");
            shares.arraySize = basisPoints.Length;
            for (int index = 0; index < basisPoints.Length; index++)
            {
                shares.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("basisPoints").intValue = basisPoints[index];
            }
        }

        private readonly struct AttackCadence
        {
            public AttackCadence(int firstReadyTick, int cooldownTicks)
            {
                FirstReadyTick = firstReadyTick;
                CooldownTicks = cooldownTicks;
            }

            public int FirstReadyTick { get; }
            public int CooldownTicks { get; }
        }


        private static int GetInt(SerializedObject data, string name)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized integer property '{name}' is missing.");
            }

            return property.intValue;
        }

        private static void SetString(SerializedObject data, string name, string value) =>
            data.FindProperty(name).stringValue = value;
        private static void SetInt(SerializedObject data, string name, int value) =>
            data.FindProperty(name).intValue = value;
        private static void SetFloat(SerializedObject data, string name, float value) =>
            data.FindProperty(name).floatValue = value;
        private static void SetBool(SerializedObject data, string name, bool value) =>
            data.FindProperty(name).boolValue = value;
        private static void SetObject(SerializedObject data, string name, UnityEngine.Object value) =>
            data.FindProperty(name).objectReferenceValue = value;

        private static void SetIntArray(SerializedObject data, string name, int[] values)
        {
            SerializedProperty array = data.FindProperty(name);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                array.GetArrayElementAtIndex(index).intValue = values[index];
        }

        private static void SetObjectArray(SerializedObject data, string name, UnityEngine.Object[] values)
        {
            SerializedProperty array = data.FindProperty(name);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}

