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
        private const string LevelOneRoot = Root + "/Level1";
        private const string PrefabRoot = "Assets/FPGDemo/Presentation/FormalEncounter";

        [MenuItem("FPG Demo/Formal Encounter/Install Burstbug Hudie Luan Defaults", priority = 130)]
        public static void Install()
        {
            EnsureFolder(Root);
            EnsureFolder(LevelOneRoot);
            EnsureFolder(PrefabRoot);
            try
            {
                GameObject burstbugPrefab = CreateFormalPrefab(
                    "Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab",
                    PrefabRoot + "/PF_FPG_BurstbugEntity.prefab");
                GameObject hudiePrefab = CreateFormalPrefab(
                    "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab",
                    PrefabRoot + "/PF_FPG_HudieEntity.prefab");
                GameObject luanPrefab = CreateFormalPrefab(
                    "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab",
                    PrefabRoot + "/PF_FPG_LuanEntity.prefab");

                FpgEnemyBehaviorDefinition burstbugBehavior = CreateBehavior("Burstbug", 2, 3.5f, 2.5f);
                FpgEnemyBehaviorDefinition hudieBehavior = CreateBehavior("Hudie", 2, 3f, 2f);
                FpgEnemyBehaviorDefinition luanBehavior = CreateBehavior("Luan", 0, 2.5f, 0f);

                FpgEnemyAttackDefinition burstbugAttack = CreateProjectileAttack(
                    "Burstbug", 12, 0, 1, 101, 45, 80);
                FpgEnemyAttackDefinition hudieAttack = CreateProjectileAttack(
                    "Hudie", 8, 0, 3, 102, 60, 105);

                FpgEnemyDefinition burstbug = CreateEnemy(
                    "burstbug", "Burstbug", (int)FpgEnemyRole.Melee, 120, 30, 2, 1,
                    burstbugPrefab, burstbugBehavior, new[] { burstbugAttack });
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
                SetInt(summon, "maxSummonsPerOwner", 2);
                SetInt(summon, "maxTotalSummonsPerEncounter", 6);
                SetInt(summon, "maxRecursionDepth", 1);
                SetInt(summon, "cooldownTicks", 120);
                summon.ApplyModifiedPropertiesWithoutUndo();

                FpgEnemyAttackDefinition luanAttack = LoadOrCreate<FpgEnemyAttackDefinition>(
                    Root + "/FPG_Luan_Attack_Summon.asset");
                SerializedObject luanAttackData = new SerializedObject(luanAttack);
                SetString(luanAttackData, "attackId", "luan-summon");
                SetString(luanAttackData, "displayName", "Luan Summon");
                SetInt(luanAttackData, "kind", 2);
                SetInt(luanAttackData, "firstReadyOffsetTicks", 90);
                SetInt(luanAttackData, "cooldownTicks", 150);
                SetInt(luanAttackData, "telegraphTicks", 30);
                SetInt(luanAttackData, "windupTicks", 15);
                SetInt(luanAttackData, "recoveryTicks", 30);
                SetString(luanAttackData, "animationSlot", "summon");
                SetString(luanAttackData, "warningSlot", "enemy-summon-warning");
                SetObject(luanAttackData, "summon", summonHudie);
                luanAttackData.ApplyModifiedPropertiesWithoutUndo();

                FpgEnemyDefinition luan = CreateEnemy(
                    "luan", "Luan", (int)FpgEnemyRole.Support, 180, 45, 4, 2,
                    luanPrefab, luanBehavior, new[] { luanAttack });

                FpgFormalAttackRuntimeCatalog attackRuntimeCatalog =
                    LoadOrCreate<FpgFormalAttackRuntimeCatalog>(
                        Root + "/FPG_NormalRoom_AttackRuntimeCatalog.asset");
                SerializedObject attackRuntimeData = new SerializedObject(attackRuntimeCatalog);
                SerializedProperty attackRuntimeEntries = attackRuntimeData.FindProperty("entries");
                attackRuntimeEntries.arraySize = 3;
                ConfigureAttackRuntimeEntry(
                    attackRuntimeEntries.GetArrayElementAtIndex(0),
                    burstbugAttack, 1001, 0, 1, 101, 101, 0, 101);
                ConfigureAttackRuntimeEntry(
                    attackRuntimeEntries.GetArrayElementAtIndex(1),
                    hudieAttack, 1002, 0, 1, 102, 102, 0, 102);
                ConfigureAttackRuntimeEntry(
                    attackRuntimeEntries.GetArrayElementAtIndex(2),
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
                ConfigurePoolEntry(entries.GetArrayElementAtIndex(1), hudie, 4, 0, 99, 8, 16, true);
                ConfigurePoolEntry(entries.GetArrayElementAtIndex(2), luan, 2, 1, 99, 2, 4, true);
                poolData.ApplyModifiedPropertiesWithoutUndo();

                FpgEnemyDefinitionCatalog catalog = LoadOrCreate<FpgEnemyDefinitionCatalog>(
                    Root + "/FPG_NormalRoom_EnemyCatalog.asset");
                SerializedObject catalogData = new SerializedObject(catalog);
                SetObjectArray(catalogData, "definitions", new UnityEngine.Object[] { burstbug, hudie, luan });
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

                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(pool);
                EditorUtility.SetDirty(catalog);
                EditorUtility.SetDirty(attackRuntimeCatalog);
                AssetDatabase.SaveAssets();
                Selection.activeObject = levelOneProfile;
                Debug.Log(
                    "[FPG Formal Encounter] Installed Burstbug, Hudie and Luan defaults "
                    + "with four L1_01 fixed-wave presets.");
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        private static FpgEnemyBehaviorDefinition CreateBehavior(
            string name, int mode, float entrySpeed, float moveSpeed)
        {
            FpgEnemyBehaviorDefinition asset = LoadOrCreate<FpgEnemyBehaviorDefinition>(
                Root + $"/FPG_{name}_Behavior.asset");
            SerializedObject data = new SerializedObject(asset);
            SetString(data, "behaviorId", name.ToLowerInvariant() + "-behavior");
            SetString(data, "displayName", name + " Behavior");
            SetInt(data, "mode", mode);
            SetFloat(data, "entrySpeed", entrySpeed);
            SetFloat(data, "moveSpeed", moveSpeed);
            data.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static FpgEnemyAttackDefinition CreateProjectileAttack(
            string name, int damage, int breakDamage, int projectileCount,
            int projectileDefinitionId,
            int firstReadyTick, int cooldownTicks)
        {
            FpgEnemyAttackDefinition asset = LoadOrCreate<FpgEnemyAttackDefinition>(
                Root + $"/FPG_{name}_Attack.asset");
            SerializedObject data = new SerializedObject(asset);
            SetString(data, "attackId", name.ToLowerInvariant() + "-projectile");
            SetString(data, "displayName", name + " Projectile");
            SetInt(data, "kind", 0);
            SetInt(data, "damage", damage);
            SetInt(data, "breakDamage", breakDamage);
            SetInt(data, "projectileCount", projectileCount);
            SetInt(data, "projectileDefinitionId", projectileDefinitionId);
            SetInt(data, "firstReadyOffsetTicks", firstReadyTick);
            SetInt(data, "cooldownTicks", cooldownTicks);
            SetString(data, "animationSlot", "attack");
            SetString(data, "warningSlot", "enemy-warning");
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
            ConfigurePoolEntry(entries.GetArrayElementAtIndex(1), hudie, 4, 0, 99, 8, 16, true);
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
            if (legacy == null || legacy.BodyHitbox == null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                throw new InvalidOperationException("Formal enemy source is missing its legacy entity binding: " + sourcePath);
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
            FpgEnemyEntityView formal = instance.GetComponent<FpgEnemyEntityView>();
            if (formal == null) formal = instance.AddComponent<FpgEnemyEntityView>();
            SerializedObject view = new SerializedObject(formal);
            SetObject(view, "gameplayAnchor", gameplayAnchor);
            SetObject(view, "projectileAnchor", projectileAnchor);
            SetObject(view, "weakpointAnchor", weakpointAnchor);
            SetObject(view, "overheadHealthBarAnchor", weakpointAnchor);
            SetObjectArray(view, "hitParts", colliders);
            SetIntArray(view, "hitPartKinds", hitPartKinds);
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

