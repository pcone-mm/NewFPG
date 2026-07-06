using NewFPG.Combat;
using NewFPG.Level;
using NewFPG.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewFPG.EditorTools
{
    public static class LevelFlowSceneInstaller
    {
        private const string FishPrefabPath = "Assets/Prefabs/Monster/Fish.prefab";

        [MenuItem("NewFPG/Level/Install Underground First Floor Prototype")]
        public static void InstallUndergroundFirstFloorPrototype()
        {
            GameObject directorObject = GameObject.Find("LevelFlowDirector");
            if (directorObject == null)
            {
                directorObject = new GameObject("LevelFlowDirector");
                Undo.RegisterCreatedObjectUndo(directorObject, "Create Level Flow Director");
            }

            LevelFlowDirector director = directorObject.GetComponent<LevelFlowDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<LevelFlowDirector>(directorObject);
            }

            LevelRouteTable routeTable = EnsureDefaultRouteTable();
            LevelEncounterTable encounterTable = EnsureDefaultEncounterTable();
            Undo.RecordObject(director, "Assign Level Tables");
            SerializedObject serializedDirector = new SerializedObject(director);
            serializedDirector.FindProperty("routeTable").objectReferenceValue = routeTable;
            serializedDirector.FindProperty("encounterTable").objectReferenceValue = encounterTable;
            serializedDirector.ApplyModifiedProperties();

            PrototypeFirstPersonWeaponView weaponView = Object.FindFirstObjectByType<PrototypeFirstPersonWeaponView>();
            if (weaponView != null && weaponView.GetComponent<PrototypeWeaponCombatHud>() == null)
            {
                Undo.AddComponent<PrototypeWeaponCombatHud>(weaponView.gameObject);
            }

            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(directorObject.scene);
            EditorSceneManager.SaveScene(directorObject.scene);
            Debug.Log("Installed Underground First Floor level prototype in scene: " + directorObject.scene.path, directorObject);
        }

        private static LevelRouteTable EnsureDefaultRouteTable()
        {
            EnsureLevelSettingsFolder();
            LevelRouteTable table = AssetDatabase.LoadAssetAtPath<LevelRouteTable>(LevelRouteTable.DefaultAssetPath);
            if (table != null)
            {
                return table;
            }

            table = ScriptableObject.CreateInstance<LevelRouteTable>();
            table.SetRouteNote("地下第一层原型路线：这里配置房间触发、房间选择和出口门；刷怪内容通过 encounterId 到 LevelEncounterTable 查询。");
            table.Configure(LevelRouteId.UndergroundFirstFloor, "b1_entry_combat", CreateDefaultRooms());
            AssetDatabase.CreateAsset(table, LevelRouteTable.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return table;
        }

        private static LevelEncounterTable EnsureDefaultEncounterTable()
        {
            EnsureLevelSettingsFolder();
            LevelEncounterTable table = AssetDatabase.LoadAssetAtPath<LevelEncounterTable>(LevelEncounterTable.DefaultAssetPath);
            if (table != null)
            {
                return table;
            }

            table = ScriptableObject.CreateInstance<LevelEncounterTable>();
            table.SetTableNote("地下第一层原型刷怪表：路线房间通过 encounterId 引用这里；每个 encounter 内的 waves 按顺序执行。");
            table.SetEncounters(CreateDefaultEncounters());
            AssetDatabase.CreateAsset(table, LevelEncounterTable.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return table;
        }

        private static void EnsureLevelSettingsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Settings/Level"))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Level");
            }
        }

        private static LevelEncounterDefinition[] CreateDefaultEncounters()
        {
            GameObject fishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FishPrefabPath);
            return new[]
            {
                PresetEncounter("fish_intro", "fish_intro_single", fishPrefab, 1, false, 80f),
                PresetEncounter("fish_pair", "fish_pair_default", fishPrefab, 2, false, 80f),
                PresetEncounter("elite_fish", "elite_fish_single", fishPrefab, 1, true, 150f),
            };
        }

        private static LevelEncounterDefinition PresetEncounter(
            string encounterId,
            string groupId,
            GameObject fishPrefab,
            int count,
            bool overrideMaxHealth,
            float maxHealth)
        {
            var encounter = new LevelEncounterDefinition
            {
                encounterId = encounterId,
                encounterNote = EncounterNote(encounterId),
            };
            var wave = new LevelEncounterWave
            {
                waveId = groupId + "_wave",
                waveNote = WaveNote(count, overrideMaxHealth, maxHealth),
                selectionMode = LevelSpawnSelectionMode.PresetGroupRandom,
            };
            var group = new LevelSpawnGroup
            {
                groupId = groupId,
                weight = 1f,
            };
            group.entries.Add(FishEntry(fishPrefab, count, 1f, overrideMaxHealth, maxHealth));
            wave.presetGroups.Add(group);
            encounter.waves.Add(wave);
            return encounter;
        }

        private static string EncounterNote(string encounterId)
        {
            switch (encounterId)
            {
                case "fish_intro":
                    return "基础鱼怪 encounter，当前只有 1 波；由入口祝福、潮火祝福、井边残影等房间引用。";
                case "fish_pair":
                    return "普通双鱼 encounter，当前只有 1 波；由交错水廊引用，用于测试多目标和清房结算。";
                case "elite_fish":
                    return "精英鱼 encounter，当前只有 1 波；通过生命值覆盖把鱼怪最大生命值设为 150。";
                default:
                    return string.Empty;
            }
        }

        private static string WaveNote(int count, bool overrideMaxHealth, float maxHealth)
        {
            string note = "单波刷怪：从固定组中刷出 " + count + " 条鱼。";
            if (overrideMaxHealth)
            {
                note += "显式覆盖生命值为 " + Mathf.RoundToInt(maxHealth) + "。";
            }

            return note;
        }

        private static LevelRoomDefinition[] CreateDefaultRooms()
        {
            return new[]
            {
                Room(
                    "b1_entry_combat",
                    "潮湿石门",
                    LevelRoomType.Blessing,
                    LevelRewardPool.MajorFind,
                    "fish_intro",
                    "初始祝福",
                    "第一间固定为战前祝福选择，选择后才生成鱼怪。",
                    new[]
                    {
                        Choice("entry_blade_flame", "灵火入刃", "本房开始前获得 20% 子弹伤害。", 0.2f, 0),
                        Choice("entry_gold_echo", "碎金试炼", "获得 20 金币，然后触发战斗。", 0f, 20),
                    },
                    new[]
                    {
                        Door("b1_blessing", "泛光符门", LevelRoomType.Blessing, LevelRewardPool.MajorFind, "三选一祝福", true, false),
                        Door("b1_story_event", "低语侧室", LevelRoomType.StoryEvent, LevelRewardPool.SpecialDoor, "NPC/事件", false, false),
                    }),
                Room(
                    "b1_blessing",
                    "潮火祝福",
                    LevelRoomType.Blessing,
                    LevelRewardPool.MajorFind,
                    "fish_intro",
                    "本局强化",
                    "先选择 Major Find 式强化，再生成怪物进入战斗。",
                    new[]
                    {
                        Choice("blade_heat", "剑火入脉", "武器子弹伤害提高 25%。", 0.25f, 0),
                        Choice("quick_gold", "碎金回响", "获得 30 金币，用于后续商店原型。", 0f, 30),
                    },
                    new[]
                    {
                        Door("b1_cross_combat", "兽影甬道", LevelRoomType.Combat, LevelRewardPool.MinorFind, "局外材料", true, false),
                        Door("b1_elite_combat", "刻痕石门", LevelRoomType.EliteCombat, LevelRewardPool.SpecialDoor, "精英奖励", false, true),
                    }),
                Room(
                    "b1_story_event",
                    "井边残影",
                    LevelRoomType.StoryEvent,
                    LevelRewardPool.SpecialDoor,
                    "fish_intro",
                    "事件/代价",
                    "先处理事件/代价选择，再生成怪物；后续可挂 NPC 对话或限时宝箱。",
                    new[]
                    {
                        Choice("listen", "听完低语", "获得 15 金币。", 0f, 15),
                        Choice("take_mark", "触碰刻印", "武器子弹伤害提高 15%。", 0.15f, 0),
                    },
                    new[]
                    {
                        Door("b1_cross_combat", "回到主路", LevelRoomType.Combat, LevelRewardPool.MajorFind, "战斗奖励", true, false),
                    }),
                Room(
                    "b1_cross_combat",
                    "交错水廊",
                    LevelRoomType.Combat,
                    LevelRewardPool.MinorFind,
                    "fish_pair",
                    "材料/金币",
                    "战前选择局外收益倾向，然后测试多目标和清房结算。",
                    new[]
                    {
                        Choice("minor_bones", "拾取残骨", "获得 20 金币作为局外资源占位。", 0f, 20),
                        Choice("minor_focus", "凝神进击", "本局子弹伤害提高 10%。", 0.1f, 0),
                    },
                    new[]
                    {
                        Door("b1_elite_combat", "下沉斗室", LevelRoomType.EliteCombat, LevelRewardPool.SpecialDoor, "精英/小 Boss", false, true),
                        Door("b1_rest", "浅光泉眼", LevelRoomType.Rest, LevelRewardPool.SpecialDoor, "休整", false, false),
                    }),
                Room(
                    "b1_elite_combat",
                    "下沉斗室",
                    LevelRoomType.EliteCombat,
                    LevelRewardPool.SpecialDoor,
                    "elite_fish",
                    "高粹有度奖励",
                    "先确认高风险奖励，再触发地下第一层的小强度巅峰。",
                    new[]
                    {
                        Choice("elite_risk", "接下刻痕", "高风险门：伤害提高 20%，随后生成精英怪。", 0.2f, 0),
                        Choice("elite_gold", "稳取供品", "获得 35 金币，随后生成精英怪。", 0f, 35),
                    },
                    new[]
                    {
                        Door("b1_rest", "泉眼出口", LevelRoomType.Rest, LevelRewardPool.SpecialDoor, "休整", false, false),
                    }),
                new LevelRoomDefinition
                {
                    roomId = "b1_rest",
                    displayName = "浅光泉眼",
                    roomType = LevelRoomType.Rest,
                    rewardPool = LevelRewardPool.SpecialDoor,
                    triggerMode = LevelRoomTriggerMode.OnEnter,
                    completionMode = LevelRoomCompletionMode.CompleteRoute,
                    rewardPreview = "休整完成",
                    roomNote = "区域之间的休整/中转节点占位。",
                },
            };
        }

        private static LevelRoomDefinition Room(
            string roomId,
            string displayName,
            LevelRoomType roomType,
            LevelRewardPool rewardPool,
            string encounterId,
            string rewardPreview,
            string roomNote,
            LevelRoomChoiceDefinition[] choices,
            LevelDoorDefinition[] exits)
        {
            var room = new LevelRoomDefinition
            {
                roomId = roomId,
                displayName = displayName,
                roomType = roomType,
                rewardPool = rewardPool,
                triggerMode = LevelRoomTriggerMode.OnInteract,
                completionMode = LevelRoomCompletionMode.StartEncounter,
                encounterId = encounterId,
                rewardPreview = rewardPreview,
                roomNote = roomNote,
            };
            room.choices.AddRange(choices);
            room.exits.AddRange(exits);
            return room;
        }

        private static LevelRoomChoiceDefinition Choice(string choiceId, string displayName, string description, float damageBonus, int goldDelta)
        {
            return new LevelRoomChoiceDefinition
            {
                choiceId = choiceId,
                displayName = displayName,
                description = description,
                damageBonus = damageBonus,
                goldDelta = goldDelta,
            };
        }

        private static LevelDoorDefinition Door(
            string targetRoomId,
            string displayName,
            LevelRoomType roomType,
            LevelRewardPool rewardPool,
            string rewardPreview,
            bool canReroll,
            bool risk)
        {
            return new LevelDoorDefinition
            {
                targetRoomId = targetRoomId,
                displayName = displayName,
                roomType = roomType,
                rewardPool = rewardPool,
                rewardPreview = rewardPreview,
                canReroll = canReroll,
                isRiskDoor = risk,
            };
        }

        private static LevelSpawnEntry FishEntry(
            GameObject fishPrefab,
            int count,
            float weight,
            bool overrideMaxHealth,
            float maxHealth)
        {
            return new LevelSpawnEntry
            {
                monsterId = "fish",
                monsterPrefab = fishPrefab,
                count = count,
                weight = weight,
                overrideMaxHealth = overrideMaxHealth,
                maxHealthOverride = maxHealth,
            };
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Select First Choice")]
        public static void RuntimeProbeSelectFirstChoice()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            bool selected = director != null && director.SelectChoice(0);
            Debug.Log(FormatProbe("SelectFirstChoice", director, selected), director);
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Kill Active Enemies")]
        public static void RuntimeProbeKillActiveEnemies()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            if (director != null)
            {
                director.DebugKillActiveEnemies();
            }

            Debug.Log(FormatProbe("KillActiveEnemies", director, director != null), director);
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Select First Door")]
        public static void RuntimeProbeSelectFirstDoor()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            bool selected = director != null && director.SelectDoor(0);
            Debug.Log(FormatProbe("SelectFirstDoor", director, selected), director);
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Print State")]
        public static void RuntimeProbePrintState()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            Debug.Log(FormatProbe("PrintState", director, director != null), director);
        }

        private static string FormatProbe(string action, LevelFlowDirector director, bool result)
        {
            if (director == null)
            {
                return "[LevelFlowProbe] " + action + " result=" + result + " director=null";
            }

            string roomId = director.CurrentRoom != null ? director.CurrentRoom.roomId : "null";
            return "[LevelFlowProbe] "
                + action
                + " result=" + result
                + " state=" + director.State
                + " room=" + roomId
                + " enemies=" + director.GetActiveEnemyCount()
                + " gold=" + director.Gold
                + " damageBonus=" + director.DamageBonus.ToString("0.##");
        }
    }
}
