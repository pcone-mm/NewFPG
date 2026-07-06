using System;
using System.Collections.Generic;
using NewFPG.Combat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NewFPG.Monsters
{
    public static class MonsterLayerMasks
    {
        public const string DefaultObstructionLayerName = "AstarBlocking";

        public const int DefaultObstructionMaskValue = 1 << 11;

        public static int DefaultObstructionMask => DefaultObstructionMaskValue;

        public static int MaskForLayer(string layerName, int fallbackMask)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return fallbackMask;
            }

            int layer = LayerMask.NameToLayer(layerName.Trim());
            return layer >= 0 ? 1 << layer : fallbackMask;
        }
    }

    [Serializable]
    public sealed class MonsterCatalog
    {
        public const string DefaultCatalogPath = "Assets/Settings/Monsters/monster_catalog.json";
        public const string DefaultAuthoringPath = "Assets/Settings/Monsters/MonsterCatalogAuthoring.asset";

        [InspectorName("配置版本")]
        [Tooltip("给策划和工具识别用的版本号，不参与运行时逻辑。")]
        public string version;

        [InspectorName("配置来源")]
        [Tooltip("说明这份配置由哪里维护；运行时不会读取这个字段。")]
        public string source;

        [InspectorName("中文备注")]
        [Tooltip("写给策划和程序的总备注。JSON 不支持注释，所以用这个字段保存可同步的中文说明。")]
        [TextArea(2, 5)]
        [JsonProperty("中文备注")]
        public string designerNote = "运行时默认从 monster_catalog.json 读取；策划可在 MonsterCatalogAuthoring.asset 里编辑后导出 JSON。怪物 AI 流程在 Behavior Designer 行为树中配置，本表只保存数值、技能和机制效果。";

        [InspectorName("怪物列表")]
        [Tooltip("所有可由 catalog 管理的怪物配置。")]
        public List<MonsterDefinition> monsters = new List<MonsterDefinition>();

        [JsonIgnore]
        public bool IsEmpty => monsters == null || monsters.Count == 0;

        public static MonsterCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new MonsterCatalog();
            }

            MonsterCatalog catalog = JsonConvert.DeserializeObject<MonsterCatalog>(json, MonsterJson.Settings) ?? new MonsterCatalog();
            catalog.Normalize();
            return catalog;
        }

        public string ToJson()
        {
            Normalize();
            return JsonConvert.SerializeObject(this, Formatting.Indented, MonsterJson.Settings);
        }

        public void Normalize()
        {
            if (monsters == null)
            {
                monsters = new List<MonsterDefinition>();
            }

            for (int i = 0; i < monsters.Count; i++)
            {
                monsters[i]?.Normalize();
            }
        }

        public MonsterDefinition FindMonster(string monsterId)
        {
            if (string.IsNullOrWhiteSpace(monsterId) || monsters == null)
            {
                return null;
            }

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterDefinition definition = monsters[i];
                if (definition != null && definition.monsterId == monsterId)
                {
                    return definition;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class MonsterDefinition
    {
        [Header("Identity")]
        [InspectorName("怪物ID")]
        [Tooltip("运行时绑定键，Fish.prefab 的 MonsterConfigBinding.monsterId 会用它查配置；不要只为了显示中文而改这个值。")]
        public string monsterId = "fish";

        [InspectorName("显示名称")]
        [Tooltip("给策划、调试面板或未来 UI 显示用，可以使用中文。")]
        public string displayName = "鱼怪";

        [InspectorName("中文备注")]
        [Tooltip("说明这个怪物的整体定位，以及哪些字段是运行时绑定键。")]
        [TextArea(2, 5)]
        [JsonProperty("中文备注")]
        public string designerNote = "鱼怪配置：Prefab 保留 MonsterConfigBinding 和 BehaviorTree；移动、生命、技能和机制从本 catalog 读取，AI 流程由外部行为树控制。";

        [InspectorName("预制体路径")]
        [Tooltip("刷新 prefab JSON 绑定工具会按这个路径找到怪物预制体。")]
        public string prefabPath = "Assets/Prefabs/Monster/Fish.prefab";

        [Header("Movement")]
        [InspectorName("移动参数")]
        public MonsterMovementDefinition movement = new MonsterMovementDefinition();

        [Header("Vitals")]
        [InspectorName("生命参数")]
        public MonsterVitalsDefinition vitals = new MonsterVitalsDefinition();

        [Header("Attack")]
        [InspectorName("旧攻击兼容参数")]
        public MonsterAttackDefinition attack = new MonsterAttackDefinition();

        [Header("AI")]
        [InspectorName("行为树配置")]
        public MonsterAiDefinition ai = new MonsterAiDefinition();

        [Header("Skills")]
        [InspectorName("技能列表")]
        public List<MonsterSkillDefinition> skills = new List<MonsterSkillDefinition>();

        [Header("Presentation")]
        [InspectorName("表现参数")]
        public MonsterPresentationDefinition presentation = new MonsterPresentationDefinition();

        public void Normalize()
        {
            if (movement == null)
            {
                movement = new MonsterMovementDefinition();
            }

            if (vitals == null)
            {
                vitals = new MonsterVitalsDefinition();
            }

            if (attack == null)
            {
                attack = new MonsterAttackDefinition();
            }

            if (ai == null)
            {
                ai = new MonsterAiDefinition();
            }

            if (skills == null)
            {
                skills = new List<MonsterSkillDefinition>();
            }

            if (presentation == null)
            {
                presentation = new MonsterPresentationDefinition();
            }

            movement.Normalize();
            vitals.Normalize();
            attack.Normalize();
            ai.Normalize(movement, attack);
            presentation.Normalize(attack);
            NormalizeSkills();
        }

        private void NormalizeSkills()
        {
            for (int i = skills.Count - 1; i >= 0; i--)
            {
                if (skills[i] == null)
                {
                    skills.RemoveAt(i);
                }
            }

            if (skills.Count == 0)
            {
                skills.Add(MonsterSkillDefinition.FromLegacyAttack("melee_bite", attack, presentation));
            }

            for (int i = 0; i < skills.Count; i++)
            {
                skills[i].Normalize(attack, presentation);
            }
        }
    }

    [Serializable]
    public sealed class MonsterDefinitionV2
    {
        public MonsterAiDefinition ai = new MonsterAiDefinition();
        public List<MonsterSkillDefinition> skills = new List<MonsterSkillDefinition>();
        public MonsterPresentationDefinition presentation = new MonsterPresentationDefinition();

        public void Normalize(MonsterMovementDefinition movement, MonsterAttackDefinition attack)
        {
            if (ai == null)
            {
                ai = new MonsterAiDefinition();
            }

            if (skills == null)
            {
                skills = new List<MonsterSkillDefinition>();
            }

            if (presentation == null)
            {
                presentation = new MonsterPresentationDefinition();
            }

            ai.Normalize(movement, attack);
            presentation.Normalize(attack);
            for (int i = 0; i < skills.Count; i++)
            {
                skills[i]?.Normalize(attack, presentation);
            }
        }
    }

    [Serializable]
    public sealed class MonsterMovementDefinition
    {
        [InspectorName("中文备注")]
        [Tooltip("说明移动、巡逻、可见位置采样和动画参数的整体用途。")]
        [TextArea(2, 6)]
        [JsonProperty("中文备注")]
        public string designerNote = "控制 A* AIPath 移动、巡逻、BattleArenaZoneMap 区域组站位采样、碰撞体自动适配和移动动画参数；Tag、Animator 参数名和区域组 ID 必须和项目约定保持一致。";

        [InspectorName("移动速度")]
        [Tooltip("写入 AIPath.maxSpeed；速度机制会在运行时再乘以 SpeedMultiplier。")]
        public float moveSpeed = 2.5f;

        [InspectorName("加速度")]
        [Tooltip("写入 AIPath.maxAcceleration，数值越大起步越快。")]
        public float acceleration = 16f;

        [InspectorName("减速度")]
        [Tooltip("预留给移动手感配置，目前主要保留为数据字段。")]
        public float deceleration = 20f;

        [InspectorName("启用移动")]
        [Tooltip("关闭后会停止 A* 路径并保持 Idle；技能施放时也会临时关闭移动。")]
        public bool movementEnabled = true;

        [InspectorName("按 Tag 自动找目标")]
        [Tooltip("AI 关闭时用于普通追踪；AI 开启时优先看 AI 分组里的同名配置。")]
        public bool autoFindTargetByTag = true;

        [InspectorName("目标 Tag")]
        [Tooltip("必须对应 Unity Tag，当前默认找 Player；不要翻译成中文，除非项目 Tag 也同步改名。")]
        public string targetTag = "Player";

        [InspectorName("侦测半径")]
        [Tooltip("自动找目标时的水平距离上限；0 表示不限制距离。")]
        public float detectionRadius = 7f;

        [InspectorName("停下距离")]
        [Tooltip("追目标时距离小于这个值就不再继续贴近，也会影响 AIPath.endReachedDistance。")]
        public float stoppingDistance = 1.2f;

        [InspectorName("A* 代理半径")]
        [Tooltip("写入 AIPath.radius，用于寻路代理尺寸。")]
        public float navMeshAgentRadius = 0.35f;

        [InspectorName("A* 代理高度")]
        [Tooltip("写入 AIPath.height，用于寻路代理高度。")]
        public float navMeshAgentHeight = 1.2f;

        [InspectorName("旋转速度")]
        [Tooltip("写入 AIPath.rotationSpeed；当前鱼怪禁用自动旋转，主要保留给后续扩展。")]
        public float navMeshAgentAngularSpeed = 720f;

        [InspectorName("基础高度偏移")]
        [Tooltip("预留的代理高度偏移字段；当前 A* 移动逻辑没有主动消费。")]
        public float navMeshAgentBaseOffset;

        [InspectorName("寻路区域 Mask")]
        [Tooltip("保留给 A* 最近点采样的区域限制；-1 表示默认全开。")]
        public int navMeshAreaMask = ~0;

        [InspectorName("寻路采样距离")]
        [Tooltip("把候选点投影到可走点时允许搜索的最大距离。")]
        public float navMeshSampleDistance = 1.5f;

        [InspectorName("可见采样高度")]
        [Tooltip("检查候选站位是否在相机可见范围内时，向上抬高的采样点高度。")]
        public float visibilitySampleHeight = 1f;

        [InspectorName("区域站位视线遮挡 Mask")]
        [Tooltip("找战斗区域站位时用于视线检查的遮挡层。默认只使用 AstarBlocking，普通场景层、玩家和武器不会阻挡。")]
        public int visiblePositionLineOfSightMask = MonsterLayerMasks.DefaultObstructionMask;

        [InspectorName("站位占用 Mask")]
        [Tooltip("检测区域候选点是否被占用的层。默认只检查 AstarBlocking，避免玩家、武器或普通装饰层把站位误判为不可用。")]
        public int visiblePositionOccupancyMask = MonsterLayerMasks.DefaultObstructionMask;

        [InspectorName("区域站位尝试次数")]
        [Tooltip("行为树节点“移动到战斗区域组”每次最多随机尝试多少个候选点。")]
        public int visiblePositionSampleAttempts = 24;

        [InspectorName("站位占用半径")]
        [Tooltip("检测候选点是否被占用时使用的胶囊半径。")]
        public float visiblePositionOccupancyRadius = 0.45f;

        [InspectorName("前排区域组 near")]
        public MonsterBattleZoneGroupDefinition nearZoneGroup =
            MonsterBattleZoneGroupDefinition.Near();

        [InspectorName("中排区域组 mid")]
        public MonsterBattleZoneGroupDefinition midZoneGroup =
            MonsterBattleZoneGroupDefinition.Mid();

        [InspectorName("后排区域组 far")]
        public MonsterBattleZoneGroupDefinition farZoneGroup =
            MonsterBattleZoneGroupDefinition.Far();

        [InspectorName("左列区域组 left")]
        public MonsterBattleZoneGroupDefinition leftZoneGroup =
            MonsterBattleZoneGroupDefinition.Left();

        [InspectorName("中列区域组 center")]
        public MonsterBattleZoneGroupDefinition centerZoneGroup =
            MonsterBattleZoneGroupDefinition.Center();

        [InspectorName("右列区域组 right")]
        public MonsterBattleZoneGroupDefinition rightZoneGroup =
            MonsterBattleZoneGroupDefinition.Right();

        [InspectorName("目标刷新间隔")]
        [Tooltip("自动找目标的刷新周期，越小越灵敏但更频繁调用查找。")]
        public float targetRefreshInterval = 0.25f;

        [InspectorName("无目标时巡逻")]
        [Tooltip("没有目标时是否在出生点周围随机巡逻。")]
        public bool patrolWhenNoTarget = true;

        [InspectorName("巡逻半径")]
        [Tooltip("以出生点为中心随机找巡逻点的半径。")]
        public float patrolRadius = 3f;

        [InspectorName("巡逻到点容差")]
        [Tooltip("距离巡逻点小于这个值时认为到达。")]
        public float patrolPointTolerance = 0.2f;

        [InspectorName("巡逻停顿时间")]
        [Tooltip("每次重新选巡逻点前停顿多久。")]
        public float patrolPauseDuration = 1f;

        [InspectorName("按横向移动翻转 Sprite")]
        [Tooltip("根据水平速度自动设置 SpriteRenderer.flipX。")]
        public bool flipSpriteWithHorizontalMovement = true;

        [InspectorName("Sprite 默认朝右")]
        [Tooltip("如果原图默认朝右，向右移动时不翻转；如果原图默认朝左则相反。")]
        public bool spriteFacesRightByDefault = true;

        [InspectorName("自动配置碰撞体")]
        [Tooltip("按 Sprite 尺寸自动调整 BoxCollider。")]
        public bool autoConfigureCollider = true;

        [InspectorName("碰撞体宽度系数")]
        [Tooltip("BoxCollider 宽度 = Sprite 宽度 * 该系数。")]
        public float colliderWidthScale = 0.8f;

        [InspectorName("碰撞体高度系数")]
        [Tooltip("BoxCollider 高度 = Sprite 高度 * 该系数。")]
        public float colliderHeightScale = 0.75f;

        [InspectorName("碰撞体深度")]
        [Tooltip("BoxCollider 在 Z 轴上的厚度。")]
        public float colliderDepth = 0.75f;

        [InspectorName("动画参数 MoveX")]
        [Tooltip("Animator Float 参数名，写入水平移动方向；必须和 Animator Controller 一致。")]
        public string moveXParameter = "MoveX";

        [InspectorName("动画参数 MoveZ")]
        [Tooltip("Animator Float 参数名，写入前后移动方向；必须和 Animator Controller 一致。")]
        public string moveZParameter = "MoveZ";

        [InspectorName("动画参数 Speed")]
        [Tooltip("Animator Float 参数名，写入当前速度；必须和 Animator Controller 一致。")]
        public string speedParameter = "Speed";

        [InspectorName("动画参数 IsMoving")]
        [Tooltip("Animator Bool 参数名，写入是否移动；必须和 Animator Controller 一致。")]
        public string isMovingParameter = "IsMoving";

        [InspectorName("动画参数 MovementState")]
        [Tooltip("Animator Int 参数名，写入 Idle/Move 状态枚举值；必须和 Animator Controller 一致。")]
        public string movementStateParameter = "MovementState";

        public void Normalize()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            detectionRadius = Mathf.Max(0f, detectionRadius);
            stoppingDistance = Mathf.Max(0f, stoppingDistance);
            navMeshAgentRadius = Mathf.Max(0.05f, navMeshAgentRadius);
            navMeshAgentHeight = Mathf.Max(navMeshAgentRadius * 2f, navMeshAgentHeight);
            navMeshAgentAngularSpeed = Mathf.Max(0f, navMeshAgentAngularSpeed);
            navMeshSampleDistance = Mathf.Max(0.05f, navMeshSampleDistance);
            visibilitySampleHeight = Mathf.Max(0f, visibilitySampleHeight);
            visiblePositionSampleAttempts = Mathf.Max(1, visiblePositionSampleAttempts);
            visiblePositionOccupancyRadius = Mathf.Max(0.05f, visiblePositionOccupancyRadius);
            EnsureBattleZoneGroups();
            targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
            patrolRadius = Mathf.Max(0f, patrolRadius);
            patrolPointTolerance = Mathf.Max(0f, patrolPointTolerance);
            patrolPauseDuration = Mathf.Max(0f, patrolPauseDuration);
            colliderWidthScale = Mathf.Max(0.05f, colliderWidthScale);
            colliderHeightScale = Mathf.Max(0.05f, colliderHeightScale);
            colliderDepth = Mathf.Max(0.05f, colliderDepth);
        }

        public bool TryExpandBattleZoneGroups(IReadOnlyList<string> zoneGroupsOrIds, List<string> results)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();
            if (zoneGroupsOrIds == null || zoneGroupsOrIds.Count == 0)
            {
                AddResolvedBattleZones(MonsterBattleZoneGroupDefinition.NearGroupId, results);
                return results.Count > 0;
            }

            for (int i = 0; i < zoneGroupsOrIds.Count; i++)
            {
                AddResolvedBattleZones(zoneGroupsOrIds[i], results);
            }

            return results.Count > 0;
        }

        public void AddResolvedBattleZones(string groupOrZoneId, List<string> results)
        {
            if (results == null)
            {
                return;
            }

            string normalized = MonsterBattleZoneGroupDefinition.NormalizeId(groupOrZoneId);
            if (string.IsNullOrEmpty(normalized))
            {
                normalized = MonsterBattleZoneGroupDefinition.NearGroupId;
            }

            MonsterBattleZoneGroupDefinition group = ResolveBattleZoneGroup(normalized);
            if (group != null)
            {
                group.AddZoneIds(results);
                return;
            }

            AddUnique(results, BattleArenaZoneMap.NormalizeZoneId(normalized));
        }

        private MonsterBattleZoneGroupDefinition ResolveBattleZoneGroup(string groupId)
        {
            switch (MonsterBattleZoneGroupDefinition.NormalizeId(groupId))
            {
                case MonsterBattleZoneGroupDefinition.MidGroupId:
                    return midZoneGroup;
                case MonsterBattleZoneGroupDefinition.FarGroupId:
                    return farZoneGroup;
                case MonsterBattleZoneGroupDefinition.LeftGroupId:
                    return leftZoneGroup;
                case MonsterBattleZoneGroupDefinition.CenterGroupId:
                    return centerZoneGroup;
                case MonsterBattleZoneGroupDefinition.RightGroupId:
                    return rightZoneGroup;
                case MonsterBattleZoneGroupDefinition.NearGroupId:
                    return nearZoneGroup;
                default:
                    return null;
            }
        }

        private void EnsureBattleZoneGroups()
        {
            if (nearZoneGroup == null)
            {
                nearZoneGroup = MonsterBattleZoneGroupDefinition.Near();
            }

            if (midZoneGroup == null)
            {
                midZoneGroup = MonsterBattleZoneGroupDefinition.Mid();
            }

            if (farZoneGroup == null)
            {
                farZoneGroup = MonsterBattleZoneGroupDefinition.Far();
            }

            if (leftZoneGroup == null)
            {
                leftZoneGroup = MonsterBattleZoneGroupDefinition.Left();
            }

            if (centerZoneGroup == null)
            {
                centerZoneGroup = MonsterBattleZoneGroupDefinition.Center();
            }

            if (rightZoneGroup == null)
            {
                rightZoneGroup = MonsterBattleZoneGroupDefinition.Right();
            }

            nearZoneGroup.Normalize(MonsterBattleZoneGroupDefinition.Near());
            midZoneGroup.Normalize(MonsterBattleZoneGroupDefinition.Mid());
            farZoneGroup.Normalize(MonsterBattleZoneGroupDefinition.Far());
            leftZoneGroup.Normalize(MonsterBattleZoneGroupDefinition.Left());
            centerZoneGroup.Normalize(MonsterBattleZoneGroupDefinition.Center());
            rightZoneGroup.Normalize(MonsterBattleZoneGroupDefinition.Right());
        }

        private static void AddUnique(List<string> results, string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                if (string.Equals(results[i], zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            results.Add(zoneId);
        }

        public MonsterCameraDistanceBandDefinition ResolveCameraDistanceBand(string bandId)
        {
            switch (MonsterCameraDistanceBandDefinition.NormalizeBandId(bandId))
            {
                case MonsterCameraDistanceBandDefinition.MidBandId:
                    return new MonsterCameraDistanceBandDefinition(MonsterCameraDistanceBandDefinition.MidBandId, 4f, 6f);
                case MonsterCameraDistanceBandDefinition.FarBandId:
                    return new MonsterCameraDistanceBandDefinition(MonsterCameraDistanceBandDefinition.FarBandId, 6f, 9f);
                default:
                    return new MonsterCameraDistanceBandDefinition(MonsterCameraDistanceBandDefinition.NearBandId, 2f, 4f);
            }
        }
    }

    [Serializable]
    public sealed class MonsterBattleZoneGroupDefinition
    {
        public const string NearGroupId = "near";
        public const string MidGroupId = "mid";
        public const string FarGroupId = "far";
        public const string LeftGroupId = "left";
        public const string CenterGroupId = "center";
        public const string RightGroupId = "right";

        [InspectorName("中文备注")]
        [Tooltip("说明这个战斗区域组如何被行为树站位节点使用。")]
        [TextArea(2, 4)]
        [JsonProperty("中文备注")]
        public string designerNote = "战斗区域组；行为树节点会按 near/mid/far 或 left/center/right 选择 BattleArenaZoneMap 中的具体格子。";

        [InspectorName("区域组ID")]
        [Tooltip("运行时稳定 ID，支持 near、mid、far、left、center、right；不要翻译这个值。")]
        public string groupId = NearGroupId;

        [InspectorName("区域ID列表")]
        [Tooltip("逗号分隔的 BattleArenaZoneMap 区域 ID，例如 left_front,center_front,right_front。")]
        public string zoneIds = string.Empty;

        public MonsterBattleZoneGroupDefinition()
        {
        }

        public MonsterBattleZoneGroupDefinition(string groupId, string zoneIds, string designerNote)
        {
            this.groupId = groupId;
            this.zoneIds = zoneIds;
            this.designerNote = designerNote;
        }

        public static MonsterBattleZoneGroupDefinition Near()
        {
            return new MonsterBattleZoneGroupDefinition(
                NearGroupId,
                "left_front,center_front,right_front",
                "近距离/前排区域组：对应 BattleArenaZoneMap 的左前、中前、右前三个格子。鱼怪靠近目标时可优先在这里找站位。");
        }

        public static MonsterBattleZoneGroupDefinition Mid()
        {
            return new MonsterBattleZoneGroupDefinition(
                MidGroupId,
                "left_mid,center_mid,right_mid",
                "中距离/中排区域组：对应 BattleArenaZoneMap 的左中、中间、右中三个格子。near 不合适时可作为靠近备选。");
        }

        public static MonsterBattleZoneGroupDefinition Far()
        {
            return new MonsterBattleZoneGroupDefinition(
                FarGroupId,
                "left_back,center_back,right_back",
                "远距离/后排区域组：对应 BattleArenaZoneMap 的左后、中后、右后三个格子。鱼怪释放技能后拉远时使用。");
        }

        public static MonsterBattleZoneGroupDefinition Left()
        {
            return new MonsterBattleZoneGroupDefinition(
                LeftGroupId,
                "left_front,left_mid,left_back",
                "左列区域组：对应 BattleArenaZoneMap 的左前、左中、左后三个格子。用于需要控制横向站位的行为树分支。");
        }

        public static MonsterBattleZoneGroupDefinition Center()
        {
            return new MonsterBattleZoneGroupDefinition(
                CenterGroupId,
                "center_front,center_mid,center_back",
                "中列区域组：对应 BattleArenaZoneMap 的中前、中间、中后三个格子。用于需要控制横向站位的行为树分支。");
        }

        public static MonsterBattleZoneGroupDefinition Right()
        {
            return new MonsterBattleZoneGroupDefinition(
                RightGroupId,
                "right_front,right_mid,right_back",
                "右列区域组：对应 BattleArenaZoneMap 的右前、右中、右后三个格子。用于需要控制横向站位的行为树分支。");
        }

        public void Normalize(MonsterBattleZoneGroupDefinition fallback)
        {
            fallback ??= Near();
            groupId = NormalizeId(string.IsNullOrWhiteSpace(groupId) ? fallback.groupId : groupId);
            if (string.IsNullOrWhiteSpace(zoneIds))
            {
                zoneIds = fallback.zoneIds;
            }

            if (string.IsNullOrWhiteSpace(designerNote))
            {
                designerNote = fallback.designerNote;
            }
        }

        public void AddZoneIds(List<string> results)
        {
            if (results == null)
            {
                return;
            }

            string[] parts = string.IsNullOrWhiteSpace(zoneIds)
                ? Array.Empty<string>()
                : zoneIds.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string normalizedZoneId = BattleArenaZoneMap.NormalizeZoneId(parts[i]);
                if (!string.IsNullOrWhiteSpace(normalizedZoneId))
                {
                    AddUnique(results, normalizedZoneId);
                }
            }
        }

        public static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            return id.Trim().ToLowerInvariant()
                .Replace('-', '_')
                .Replace(' ', '_')
                .Replace('.', '_');
        }

        private static void AddUnique(List<string> results, string zoneId)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (string.Equals(results[i], zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            results.Add(zoneId);
        }
    }

    [Serializable]
    public sealed class MonsterCameraDistanceBandDefinition
    {
        public const string NearBandId = "near";
        public const string MidBandId = "mid";
        public const string FarBandId = "far";

        public string bandId = NearBandId;
        public float minDistance = 2f;
        public float maxDistance = 4f;

        public MonsterCameraDistanceBandDefinition()
        {
        }

        public MonsterCameraDistanceBandDefinition(string bandId, float minDistance, float maxDistance)
        {
            this.bandId = NormalizeBandId(bandId);
            this.minDistance = minDistance;
            this.maxDistance = maxDistance;
        }

        [JsonIgnore]
        public bool HasValidRange => maxDistance > minDistance;

        public bool ContainsHorizontalDistance(Vector3 origin, Vector3 point)
        {
            Vector3 delta = point - origin;
            delta.y = 0f;
            float distance = delta.magnitude;
            return distance >= minDistance && distance <= maxDistance;
        }

        public static string NormalizeBandId(string bandId)
        {
            if (string.IsNullOrWhiteSpace(bandId))
            {
                return NearBandId;
            }

            switch (bandId.Trim().ToLowerInvariant())
            {
                case MidBandId:
                    return MidBandId;
                case FarBandId:
                    return FarBandId;
                default:
                    return NearBandId;
            }
        }
    }

    [Serializable]
    public sealed class MonsterVitalsDefinition
    {
        [InspectorName("中文备注")]
        [Tooltip("说明生命、受击和死亡表现的用途。")]
        [TextArea(2, 5)]
        [JsonProperty("中文备注")]
        public string designerNote = "套用到 CombatVitals：控制最大生命、初始生命、护盾、死亡销毁和受击表现。";

        [InspectorName("最大生命")]
        public float maxHealth = 80f;

        [InspectorName("初始生命")]
        [Tooltip("小于等于 0 时会回填为最大生命。")]
        public float startingHealth = 80f;

        [InspectorName("最大护盾")]
        public float maxShield;

        [InspectorName("初始护盾")]
        public float startingShield;

        [InspectorName("死亡后销毁")]
        public bool destroyOnDeath = true;

        [InspectorName("死亡延迟")]
        [Tooltip("死亡后延迟多少秒销毁。")]
        public float deathDelay = 0.25f;

        [InspectorName("受击动画 Trigger")]
        [Tooltip("Animator Trigger 参数名，必须和 Animator Controller 一致。")]
        public string hitTriggerParameter = "Hit";

        [InspectorName("受击染色")]
        [JsonConverter(typeof(MonsterColorJsonConverter))]
        public Color hitTint = new Color(1f, 0.65f, 0.55f, 1f);

        [InspectorName("受击染色时长")]
        public float hitTintSeconds = 0.12f;

        public void Normalize()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            startingHealth = Mathf.Clamp(startingHealth <= 0f ? maxHealth : startingHealth, 1f, maxHealth);
            maxShield = Mathf.Max(0f, maxShield);
            startingShield = Mathf.Clamp(startingShield, 0f, maxShield);
            deathDelay = Mathf.Max(0f, deathDelay);
            hitTintSeconds = Mathf.Max(0.02f, hitTintSeconds);
        }
    }

    [Serializable]
    public sealed class MonsterAttackDefinition
    {
        [InspectorName("中文备注")]
        [Tooltip("旧攻击兼容说明。当前鱼怪实际施放走 skills 和 mechanics，这组字段主要给默认技能生成和旧逻辑兼容。")]
        [TextArea(2, 5)]
        [JsonProperty("中文备注")]
        public string designerNote = "旧攻击兼容参数：当前实际攻击由 skills/mechanics 执行；这里的数值主要用于缺省技能生成和旧工具兼容。";

        [InspectorName("自动找玩家")]
        public bool autoFindPlayer = true;

        [InspectorName("玩家 Tag")]
        [Tooltip("必须对应 Unity Tag，当前默认 Player。")]
        public string playerTag = "Player";

        [InspectorName("攻击距离")]
        public float attackRange = 2.2f;

        [InspectorName("攻击请求间隔")]
        [Tooltip("旧攻击逻辑的请求间隔，也会作为缺省技能冷却。")]
        public float requestInterval = 2f;

        [InspectorName("攻击前摇")]
        [Tooltip("旧攻击逻辑的准备时间，也会作为缺省技能 windup。")]
        public float attackPrepareTime = 0.8f;

        [InspectorName("伤害")]
        public float damage = 12f;

        [InspectorName("伤害半径")]
        public float damageRadius = 1.35f;

        [InspectorName("预警高度偏移")]
        public float warningHeightOffset = 1.2f;

        [InspectorName("目标 LayerMask")]
        [Tooltip("伤害检测的目标层。")]
        public int targetMask = ~0;

        [InspectorName("攻击动画 Trigger")]
        [Tooltip("Animator Trigger 参数名，必须和 Animator Controller 一致。")]
        public string attackTriggerParameter = "Attack";

        public void Normalize()
        {
            attackRange = Mathf.Max(0.1f, attackRange);
            requestInterval = Mathf.Max(0f, requestInterval);
            attackPrepareTime = Mathf.Max(0.05f, attackPrepareTime);
            damage = Mathf.Max(0f, damage);
            damageRadius = Mathf.Max(0.05f, damageRadius);
        }
    }

    [Serializable]
    public sealed class MonsterAiDefinition
    {
        [InspectorName("中文备注")]
        [Tooltip("说明这只怪物使用哪棵 Behavior Designer 行为树，以及 catalog 和行为树之间的职责边界。")]
        [TextArea(2, 6)]
        [JsonProperty("中文备注")]
        public string designerNote = "AI 流程由 Behavior Designer 外部行为树配置；catalog 只保存技能、机制和移动数值。行为树节点会读取 MonsterConfigBinding 暴露的原子能力。";

        public const string DefaultFishBehaviorTreePath = "Assets/Settings/Monsters/BehaviorTrees/BT_Fish.asset";

        [InspectorName("启用行为树")]
        [Tooltip("关闭后不会由刷新工具自动绑定 BehaviorTree；运行时仍会应用技能和移动数值。")]
        public bool enabled = true;

        [InspectorName("外部行为树路径")]
        [Tooltip("刷新工具会把这个 ExternalBehaviorTree 绑定到怪物 prefab 的 BehaviorTree 组件。")]
        public string behaviorTreePath = DefaultFishBehaviorTreePath;

        public void Normalize(MonsterMovementDefinition movement, MonsterAttackDefinition legacyAttack)
        {
            if (string.IsNullOrWhiteSpace(behaviorTreePath))
            {
                behaviorTreePath = DefaultFishBehaviorTreePath;
            }

            behaviorTreePath = behaviorTreePath.Trim().Replace('\\', '/');
        }
    }

    [Serializable]
    public sealed class MonsterSkillDefinition
    {
        [InspectorName("技能ID")]
        [Tooltip("运行时查找键，Behavior Designer 技能节点会引用它；不建议中文化。")]
        public string skillId = "melee_bite";

        [InspectorName("显示名称")]
        [Tooltip("给策划、调试或未来 UI 显示用，可以使用中文。")]
        public string displayName = "近身咬击";

        [InspectorName("中文备注")]
        [Tooltip("说明技能释放流程和机制组合。")]
        [TextArea(2, 6)]
        [JsonProperty("中文备注")]
        public string designerNote = "技能流程：行为树先用施放距离和视线条件判断能否出手；进入施放后可停止移动并显示预警，等待 windup 后触发动画，再按 mechanics 顺序执行效果，最后进入 recovery 并刷新冷却。";

        [InspectorName("冷却时间")]
        public float cooldown = 2f;

        [InspectorName("前摇时间")]
        [Tooltip("从开始施放到真正触发动画和机制前等待多久。")]
        public float windup = 0.8f;

        [InspectorName("持续时间")]
        [Tooltip("机制执行后保持技能激活状态的时间，目前鱼怪咬击为瞬时效果。")]
        public float activeDuration;

        [InspectorName("后摇时间")]
        [Tooltip("技能结束前的恢复时间。")]
        public float recovery;

        [InspectorName("施放距离")]
        [Tooltip("目标和怪物的水平距离小于等于该值时，行为树才应允许释放该技能。")]
        public float castRange = 2.2f;

        [InspectorName("需要视线")]
        [Tooltip("开启后释放前会检查怪物到目标之间是否被遮挡；适合近战咬击、射线和投射物。")]
        public bool requireLineOfSight = true;

        [InspectorName("视线遮挡 Mask")]
        [Tooltip("技能释放前视线检测使用的遮挡层。默认只使用 AstarBlocking，武器、玩家和普通场景层不会阻挡怪物视线判断。")]
        public int lineOfSightMask = MonsterLayerMasks.DefaultObstructionMask;

        [InspectorName("视线检测高度")]
        [Tooltip("从怪物位置向上抬高多少再做视线检测，避免射线贴地被地面或低矮碰撞误挡。")]
        public float lineOfSightHeightOffset = 1f;

        [InspectorName("施放时停止移动")]
        public bool stopMovementDuringCast = true;

        [InspectorName("技能动画 Trigger")]
        [Tooltip("Animator Trigger 参数名，必须和 Animator Controller 一致。")]
        public string animationTriggerParameter = "Attack";

        [InspectorName("显示攻击预警")]
        public bool showWarning = true;

        [InspectorName("预警高度偏移")]
        public float warningHeightOffset = 1.2f;

        [InspectorName("机制列表")]
        public List<MonsterMechanicDefinition> mechanics = new List<MonsterMechanicDefinition>();

        public static MonsterSkillDefinition FromLegacyAttack(
            string skillId,
            MonsterAttackDefinition legacyAttack,
            MonsterPresentationDefinition presentation)
        {
            MonsterAttackDefinition attack = legacyAttack ?? new MonsterAttackDefinition();
            attack.Normalize();

            MonsterPresentationDefinition visuals = presentation ?? new MonsterPresentationDefinition();
            visuals.Normalize(attack);

            return new MonsterSkillDefinition
            {
                skillId = string.IsNullOrWhiteSpace(skillId) ? "melee_bite" : skillId,
                displayName = "近身咬击",
                cooldown = attack.requestInterval,
                windup = attack.attackPrepareTime,
                activeDuration = 0f,
                recovery = 0f,
                castRange = attack.attackRange,
                requireLineOfSight = true,
                lineOfSightMask = MonsterLayerMasks.DefaultObstructionMask,
                lineOfSightHeightOffset = 1f,
                stopMovementDuringCast = true,
                animationTriggerParameter = attack.attackTriggerParameter,
                showWarning = true,
                warningHeightOffset = attack.warningHeightOffset,
                mechanics = new List<MonsterMechanicDefinition>
                {
                    MonsterMechanicDefinition.DamageArea(attack),
                },
            };
        }

        public void Normalize(MonsterAttackDefinition legacyAttack, MonsterPresentationDefinition presentation)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                skillId = "melee_bite";
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = skillId;
            }

            if (legacyAttack != null)
            {
                if (cooldown <= 0f)
                {
                    cooldown = legacyAttack.requestInterval;
                }

                if (windup <= 0f)
                {
                    windup = legacyAttack.attackPrepareTime;
                }

                if (castRange <= 0f)
                {
                    castRange = legacyAttack.attackRange;
                }

                if (string.IsNullOrWhiteSpace(animationTriggerParameter))
                {
                    animationTriggerParameter = legacyAttack.attackTriggerParameter;
                }

                if (warningHeightOffset <= 0f)
                {
                    warningHeightOffset = legacyAttack.warningHeightOffset;
                }
            }

            cooldown = Mathf.Max(0f, cooldown);
            windup = Mathf.Max(0f, windup);
            activeDuration = Mathf.Max(0f, activeDuration);
            recovery = Mathf.Max(0f, recovery);
            castRange = Mathf.Max(0.01f, castRange);
            lineOfSightHeightOffset = Mathf.Max(0f, lineOfSightHeightOffset);

            if (mechanics == null)
            {
                mechanics = new List<MonsterMechanicDefinition>();
            }

            for (int i = mechanics.Count - 1; i >= 0; i--)
            {
                if (mechanics[i] == null)
                {
                    mechanics.RemoveAt(i);
                }
            }

            if (mechanics.Count == 0 && legacyAttack != null)
            {
                mechanics.Add(MonsterMechanicDefinition.DamageArea(legacyAttack));
            }

            for (int i = 0; i < mechanics.Count; i++)
            {
                mechanics[i].Normalize(legacyAttack);
            }
        }
    }

    [Serializable]
    public sealed class MonsterMechanicDefinition
    {
        public const string DamageAreaType = "damage_area";
        public const string InvincibleType = "invincible";
        public const string InvisibleType = "invisible";
        public const string ScaleModifierType = "scale_modifier";
        public const string SpeedModifierType = "speed_modifier";

        [InspectorName("机制ID")]
        [Tooltip("给人读和调试用的稳定标识；不建议只为了显示中文而改。")]
        public string mechanicId;

        [InspectorName("机制类型")]
        [Tooltip("代码只识别 damage_area、invincible、invisible、scale_modifier、speed_modifier；不要翻译这个值。")]
        public string type = DamageAreaType;

        [InspectorName("中文备注")]
        [Tooltip("说明这个机制实际执行的效果。")]
        [TextArea(2, 6)]
        [JsonProperty("中文备注")]
        public string designerNote = "机制效果：damage_area 会以怪物位置加 heightOffset 为圆心做半径伤害；invincible/invisible/scale_modifier/speed_modifier 会在 duration 内修改怪物状态。";

        [InspectorName("延迟")]
        [Tooltip("进入机制协程后等待多久再执行效果。")]
        public float delay;

        [InspectorName("持续时间")]
        [Tooltip("对无敌、隐身、缩放、速度机制有效；伤害区域当前为瞬时检测。")]
        public float duration;

        [InspectorName("数值")]
        [Tooltip("damage_area 表示伤害；scale_modifier/speed_modifier 表示倍率。")]
        public float value;

        [InspectorName("半径")]
        [Tooltip("damage_area 的水平伤害半径。")]
        public float radius = 1f;

        [InspectorName("高度偏移")]
        [Tooltip("以怪物位置为基础向上偏移后作为机制中心。")]
        public float heightOffset;

        [InspectorName("目标 LayerMask")]
        [Tooltip("damage_area 检测可命中的目标层。")]
        public int targetMask = ~0;

        [InspectorName("是否影响自己")]
        [Tooltip("false 时会排除自己和其他怪物，避免友伤。")]
        public bool affectSelf = true;

        public static MonsterMechanicDefinition DamageArea(MonsterAttackDefinition attack)
        {
            MonsterAttackDefinition legacyAttack = attack ?? new MonsterAttackDefinition();
            legacyAttack.Normalize();
            return new MonsterMechanicDefinition
            {
                mechanicId = DamageAreaType,
                type = DamageAreaType,
                delay = 0f,
                duration = 0f,
                value = legacyAttack.damage,
                radius = legacyAttack.damageRadius,
                heightOffset = legacyAttack.warningHeightOffset,
                targetMask = legacyAttack.targetMask,
                affectSelf = false,
            };
        }

        public void Normalize(MonsterAttackDefinition legacyAttack)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                type = DamageAreaType;
            }

            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                mechanicId = type;
            }

            delay = Mathf.Max(0f, delay);
            duration = Mathf.Max(0f, duration);
            radius = Mathf.Max(0.01f, radius);

            if (type == DamageAreaType && legacyAttack != null)
            {
                if (value <= 0f)
                {
                    value = legacyAttack.damage;
                }

                if (targetMask == 0)
                {
                    targetMask = legacyAttack.targetMask;
                }
            }
        }
    }

    [Serializable]
    public sealed class MonsterPresentationDefinition
    {
        [InspectorName("中文备注")]
        [Tooltip("说明表现层参数如何绑定 Animator 和预警高度。")]
        [TextArea(2, 5)]
        [JsonProperty("中文备注")]
        public string designerNote = "表现参数：保存动画 Trigger 和预警高度的默认值；Trigger 名必须和 Animator Controller 一致。";

        [InspectorName("攻击动画 Trigger")]
        public string attackTriggerParameter = "Attack";

        [InspectorName("受击动画 Trigger")]
        public string hitTriggerParameter = "Hit";

        [InspectorName("预警高度偏移")]
        public float warningHeightOffset = 1.2f;

        public void Normalize(MonsterAttackDefinition legacyAttack)
        {
            if (legacyAttack != null)
            {
                if (string.IsNullOrWhiteSpace(attackTriggerParameter))
                {
                    attackTriggerParameter = legacyAttack.attackTriggerParameter;
                }

                if (warningHeightOffset <= 0f)
                {
                    warningHeightOffset = legacyAttack.warningHeightOffset;
                }
            }

            if (string.IsNullOrWhiteSpace(attackTriggerParameter))
            {
                attackTriggerParameter = "Attack";
            }

            if (string.IsNullOrWhiteSpace(hitTriggerParameter))
            {
                hitTriggerParameter = "Hit";
            }

            warningHeightOffset = Mathf.Max(0f, warningHeightOffset);
        }
    }

    public enum MonsterMechanicKind
    {
        Unknown,
        DamageArea,
        Invincible,
        Invisible,
        ScaleModifier,
        SpeedModifier,
    }

    public static class MonsterMechanicTypes
    {
        public static MonsterMechanicKind Parse(string type)
        {
            switch (type)
            {
                case MonsterMechanicDefinition.DamageAreaType:
                    return MonsterMechanicKind.DamageArea;
                case MonsterMechanicDefinition.InvincibleType:
                    return MonsterMechanicKind.Invincible;
                case MonsterMechanicDefinition.InvisibleType:
                    return MonsterMechanicKind.Invisible;
                case MonsterMechanicDefinition.ScaleModifierType:
                    return MonsterMechanicKind.ScaleModifier;
                case MonsterMechanicDefinition.SpeedModifierType:
                    return MonsterMechanicKind.SpeedModifier;
                default:
                    return MonsterMechanicKind.Unknown;
            }
        }
    }

    internal static class MonsterJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new MonsterColorJsonConverter() },
        };
    }

    internal sealed class MonsterColorJsonConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(value.r);
            writer.WritePropertyName("g");
            writer.WriteValue(value.g);
            writer.WritePropertyName("b");
            writer.WriteValue(value.b);
            writer.WritePropertyName("a");
            writer.WriteValue(value.a);
            writer.WriteEndObject();
        }

        public override Color ReadJson(
            JsonReader reader,
            Type objectType,
            Color existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return Color.white;
            }

            JObject value = JObject.Load(reader);
            return new Color(
                ReadFloat(value, "r", existingValue.r),
                ReadFloat(value, "g", existingValue.g),
                ReadFloat(value, "b", existingValue.b),
                ReadFloat(value, "a", existingValue.a <= 0f ? 1f : existingValue.a));
        }

        private static float ReadFloat(JObject value, string propertyName, float fallback)
        {
            JToken token = value[propertyName];
            return token != null && token.Type != JTokenType.Null ? token.Value<float>() : fallback;
        }
    }
}
