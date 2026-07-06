using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using TaskTooltip = BehaviorDesigner.Runtime.Tasks.TooltipAttribute;

namespace NewFPG.Monsters.BehaviorDesigner
{
    public sealed class MonsterSkillIdAttribute : ObjectDrawerAttribute
    {
    }

    public sealed class MonsterBattleZoneRowsAttribute : ObjectDrawerAttribute
    {
    }

    public sealed class MonsterBattleZoneColumnsAttribute : ObjectDrawerAttribute
    {
    }

    [System.Flags]
    public enum MonsterBattleZoneRows
    {
        [InspectorName("不选择")]
        None = 0,
        [InspectorName("前排 near")]
        Front = 1 << 0,
        [InspectorName("中排 mid")]
        Middle = 1 << 1,
        [InspectorName("后排 far")]
        Back = 1 << 2,
        [InspectorName("全部排")]
        All = Front | Middle | Back,
    }

    [System.Flags]
    public enum MonsterBattleZoneColumns
    {
        [InspectorName("不选择")]
        None = 0,
        [InspectorName("左列 left")]
        Left = 1 << 0,
        [InspectorName("中列 center")]
        Center = 1 << 1,
        [InspectorName("右列 right")]
        Right = 1 << 2,
        [InspectorName("全部列")]
        All = Left | Center | Right,
    }

    public static class MonsterBattleZoneSelectionUtility
    {
        public static bool TryBuildZoneIds(
            MonsterBattleZoneRows rows,
            MonsterBattleZoneColumns columns,
            List<string> results)
        {
            if (results == null)
            {
                return false;
            }

            results.Clear();
            rows &= MonsterBattleZoneRows.All;
            columns &= MonsterBattleZoneColumns.All;
            if (rows == MonsterBattleZoneRows.None || columns == MonsterBattleZoneColumns.None)
            {
                return false;
            }

            AddRow(rows, MonsterBattleZoneRows.Front, "front", columns, results);
            AddRow(rows, MonsterBattleZoneRows.Middle, "mid", columns, results);
            AddRow(rows, MonsterBattleZoneRows.Back, "back", columns, results);
            return results.Count > 0;
        }

        private static void AddRow(
            MonsterBattleZoneRows selectedRows,
            MonsterBattleZoneRows row,
            string rowId,
            MonsterBattleZoneColumns columns,
            List<string> results)
        {
            if ((selectedRows & row) == 0)
            {
                return;
            }

            if ((columns & MonsterBattleZoneColumns.Left) != 0)
            {
                results.Add("left_" + rowId);
            }

            if ((columns & MonsterBattleZoneColumns.Center) != 0)
            {
                results.Add("center_" + rowId);
            }

            if ((columns & MonsterBattleZoneColumns.Right) != 0)
            {
                results.Add("right_" + rowId);
            }
        }
    }

    public static class MonsterBehaviorTaskText
    {
        public const string Category = "NewFPG/怪物AI";
        public const string DefaultSkillId = "melee_bite";
        public const string DefaultTargetTag = "Player";
        public const string DefaultNearMidZoneGroups = "near,mid";
        public const string DefaultFarZoneGroup = "far";
        public const MonsterBattleZoneRows DefaultApproachRows = MonsterBattleZoneRows.Front | MonsterBattleZoneRows.Middle;
        public const MonsterBattleZoneRows DefaultRetreatRows = MonsterBattleZoneRows.Back;
        public const MonsterBattleZoneColumns DefaultColumns = MonsterBattleZoneColumns.All;
        public const float DefaultStuckSeconds = 2.5f;
        public const float DefaultMinProgressDistance = 0.1f;
        public const float DefaultMoveTimeoutSeconds = 8f;
    }

    public abstract class MonsterBehaviorAction : Action
    {
        private MonsterConfigBinding cachedBinding;

        protected MonsterConfigBinding Binding => cachedBinding;

        protected bool TryCacheBinding()
        {
            if (cachedBinding != null)
            {
                return true;
            }

            if (Owner == null)
            {
                return false;
            }

            GameObject ownerObject = Owner.gameObject;
            cachedBinding = ownerObject != null ? ownerObject.GetComponent<MonsterConfigBinding>() : null;
            return cachedBinding != null;
        }

        protected Transform ResolveTarget(SharedTransform targetOverride)
        {
            if (targetOverride != null && targetOverride.Value != null)
            {
                return targetOverride.Value;
            }

            return cachedBinding != null ? cachedBinding.Target : null;
        }

        protected static string ResolveSkillId(SharedString skillId)
        {
            return skillId != null && !string.IsNullOrWhiteSpace(skillId.Value)
                ? skillId.Value.Trim()
                : MonsterBehaviorTaskText.DefaultSkillId;
        }

        protected static void ParseCsv(string csv, List<string> results, string fallback)
        {
            results.Clear();
            string source = string.IsNullOrWhiteSpace(csv) ? fallback : csv;
            string[] parts = source.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value);
                }
            }
        }

        protected static bool HasMovementFailed(
            MonsterConfigBinding binding,
            SharedFloat stuckSeconds,
            SharedFloat minProgressDistance,
            SharedFloat moveTimeoutSeconds)
        {
            if (binding == null)
            {
                return true;
            }

            float resolvedStuckSeconds = stuckSeconds != null
                ? stuckSeconds.Value
                : MonsterBehaviorTaskText.DefaultStuckSeconds;
            float resolvedMinProgressDistance = minProgressDistance != null
                ? minProgressDistance.Value
                : MonsterBehaviorTaskText.DefaultMinProgressDistance;
            float resolvedMoveTimeoutSeconds = moveTimeoutSeconds != null
                ? moveTimeoutSeconds.Value
                : MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;

            return binding.IsCurrentMoveStuck(resolvedStuckSeconds, resolvedMinProgressDistance)
                || binding.HasCurrentMoveTimedOut(resolvedMoveTimeoutSeconds);
        }
    }

    public abstract class MonsterBehaviorConditional : Conditional
    {
        private MonsterConfigBinding cachedBinding;

        protected MonsterConfigBinding Binding => cachedBinding;

        protected bool TryCacheBinding()
        {
            if (cachedBinding != null)
            {
                return true;
            }

            if (Owner == null)
            {
                return false;
            }

            GameObject ownerObject = Owner.gameObject;
            cachedBinding = ownerObject != null ? ownerObject.GetComponent<MonsterConfigBinding>() : null;
            return cachedBinding != null;
        }

        protected Transform ResolveTarget(SharedTransform targetOverride)
        {
            if (targetOverride != null && targetOverride.Value != null)
            {
                return targetOverride.Value;
            }

            return cachedBinding != null ? cachedBinding.Target : null;
        }

        protected static string ResolveSkillId(SharedString skillId)
        {
            return skillId != null && !string.IsNullOrWhiteSpace(skillId.Value)
                ? skillId.Value.Trim()
                : MonsterBehaviorTaskText.DefaultSkillId;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("有目标")]
    [TaskDescription("检查 MonsterConfigBinding 当前是否保存了目标。没有 MonsterConfigBinding 时返回 Failure。")]
    public sealed class MonsterHasTarget : MonsterBehaviorConditional
    {
        public override TaskStatus OnUpdate()
        {
            return TryCacheBinding() && Binding.HasTarget() ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("目标有效")]
    [TaskDescription("检查当前目标是否还可被选中；如果目标带有 IDamageable，会同时检查存活和可被锁定状态。")]
    public sealed class MonsterTargetValid : MonsterBehaviorConditional
    {
        [TaskTooltip("可选覆盖目标；为空时使用 MonsterConfigBinding 当前目标。")]
        public SharedTransform targetOverride;

        public override TaskStatus OnUpdate()
        {
            if (!TryCacheBinding())
            {
                return TaskStatus.Failure;
            }

            Transform resolvedTarget = ResolveTarget(targetOverride);
            return Binding.IsValidTarget(resolvedTarget)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }

        public override void OnReset()
        {
            targetOverride = null;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("技能可用")]
    [TaskDescription("检查技能是否存在、没有处于冷却中，并且怪物当前没有正在施法。")]
    public sealed class MonsterSkillUsable : MonsterBehaviorConditional
    {
        [MonsterSkillId]
        [TaskTooltip("技能运行时 ID。下拉会显示中文名，但保存的仍是稳定 skillId。")]
        public SharedString skillId = MonsterBehaviorTaskText.DefaultSkillId;

        [TaskTooltip("可选覆盖目标；为空时使用当前目标。技能可用检查需要目标存在。")]
        public SharedTransform targetOverride;

        public override TaskStatus OnUpdate()
        {
            if (!TryCacheBinding())
            {
                return TaskStatus.Failure;
            }

            return Binding.IsSkillUsable(ResolveSkillId(skillId), ResolveTarget(targetOverride))
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }

        public override void OnReset()
        {
            skillId = MonsterBehaviorTaskText.DefaultSkillId;
            targetOverride = null;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("目标在技能范围内")]
    [TaskDescription("按技能配置里的施放距离判断目标是否已经进入可出手范围。")]
    public sealed class MonsterTargetInSkillRange : MonsterBehaviorConditional
    {
        [MonsterSkillId]
        [TaskTooltip("要检查的技能 ID。")]
        public SharedString skillId = MonsterBehaviorTaskText.DefaultSkillId;

        [TaskTooltip("可选覆盖目标；为空时使用当前目标。")]
        public SharedTransform targetOverride;

        public override TaskStatus OnUpdate()
        {
            if (!TryCacheBinding())
            {
                return TaskStatus.Failure;
            }

            return Binding.IsTargetInSkillRange(ResolveSkillId(skillId), ResolveTarget(targetOverride))
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }

        public override void OnReset()
        {
            skillId = MonsterBehaviorTaskText.DefaultSkillId;
            targetOverride = null;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("目标视线可见")]
    [TaskDescription("按技能配置里的视线开关、遮挡 Mask 和检测高度判断目标是否可见。")]
    public sealed class MonsterTargetLineOfSight : MonsterBehaviorConditional
    {
        [MonsterSkillId]
        [TaskTooltip("要检查的技能 ID。")]
        public SharedString skillId = MonsterBehaviorTaskText.DefaultSkillId;

        [TaskTooltip("可选覆盖目标；为空时使用当前目标。")]
        public SharedTransform targetOverride;

        public override TaskStatus OnUpdate()
        {
            if (!TryCacheBinding())
            {
                return TaskStatus.Failure;
            }

            return Binding.HasLineOfSightToTarget(ResolveSkillId(skillId), ResolveTarget(targetOverride))
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }

        public override void OnReset()
        {
            skillId = MonsterBehaviorTaskText.DefaultSkillId;
            targetOverride = null;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("已到达")]
    [TaskDescription("检查 A* AIPath 是否已经到达当前手动目标或路径终点。")]
    public sealed class MonsterHasArrived : MonsterBehaviorConditional
    {
        public override TaskStatus OnUpdate()
        {
            return TryCacheBinding() && Binding.HasArrived ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("正在施法")]
    [TaskDescription("检查怪物是否正处于技能施放流程中。")]
    public sealed class MonsterIsCasting : MonsterBehaviorConditional
    {
        public override TaskStatus OnUpdate()
        {
            return TryCacheBinding() && Binding.IsCasting ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("按 Tag 查找目标")]
    [TaskDescription("按 Unity Tag 搜索最近的有效目标，并写入 MonsterConfigBinding 当前目标。")]
    public sealed class MonsterFindTargetByTag : MonsterBehaviorAction
    {
        [TaskTooltip("Unity Tag，默认 Player。这个值必须和项目 Tag 一致，不要翻译成中文。")]
        public SharedString targetTag = MonsterBehaviorTaskText.DefaultTargetTag;

        [TaskTooltip("搜索半径。小于等于 0 时使用怪物移动配置里的侦测半径；移动配置为 0 表示不限距离。")]
        public SharedFloat searchRadius;

        [TaskTooltip("找不到目标时是否清空当前目标。")]
        public SharedBool clearWhenMissing = true;

        [TaskTooltip("可选：把找到的目标保存到行为树变量里，方便其它节点复用。")]
        public SharedTransform storeTarget;

        public override TaskStatus OnUpdate()
        {
            if (!TryCacheBinding())
            {
                return TaskStatus.Failure;
            }

            bool found = Binding.RefreshTargetByTag(
                targetTag != null ? targetTag.Value : MonsterBehaviorTaskText.DefaultTargetTag,
                searchRadius != null ? searchRadius.Value : 0f,
                clearWhenMissing == null || clearWhenMissing.Value,
                out Transform foundTarget);

            if (found && storeTarget != null)
            {
                storeTarget.Value = foundTarget;
            }

            return found ? TaskStatus.Success : TaskStatus.Failure;
        }

        public override void OnReset()
        {
            targetTag = MonsterBehaviorTaskText.DefaultTargetTag;
            searchRadius = 0f;
            clearWhenMissing = true;
            storeTarget = null;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("追踪目标")]
    [TaskDescription("让怪物使用 A* AIPath 追踪当前目标。默认只下发追踪指令后立即返回 Success，避免阻塞后续技能判断；需要等待到达时可开启等待。")]
    public sealed class MonsterChaseTarget : MonsterBehaviorAction
    {
        private bool startedMove;

        [TaskTooltip("可选覆盖目标；为空时使用当前目标。")]
        public SharedTransform targetOverride;

        [TaskTooltip("开启后，移动途中返回 Running，直到到达停止距离才返回 Success。鱼怪默认关闭，让行为树每轮都能重新判断技能。")]
        public SharedBool waitUntilArrived = false;

        [TaskTooltip("卡住判定秒数：等待到达时，如果这么久没有足够推进就返回 Failure。小于等于 0 表示不判定卡住。")]
        public SharedFloat stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;

        [TaskTooltip("最小推进距离：剩余路径距离至少减少这么多，才算本次移动有推进。")]
        public SharedFloat minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;

        [TaskTooltip("移动超时：等待到达时，单次追踪命令超过这个时间还没到就返回 Failure。小于等于 0 表示不超时。")]
        public SharedFloat moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;

        public override void OnStart()
        {
            startedMove = false;
            if (waitUntilArrived == null || !waitUntilArrived.Value)
            {
                return;
            }

            if (!TryCacheBinding())
            {
                return;
            }

            Transform resolvedTarget = ResolveTarget(targetOverride);
            startedMove = resolvedTarget != null && Binding.TryMoveToTarget(resolvedTarget);
        }

        public override TaskStatus OnUpdate()
        {
            if (!TryCacheBinding())
            {
                return TaskStatus.Failure;
            }

            Transform resolvedTarget = ResolveTarget(targetOverride);
            if (resolvedTarget == null)
            {
                return TaskStatus.Failure;
            }

            if (waitUntilArrived != null && waitUntilArrived.Value)
            {
                if (!startedMove)
                {
                    return TaskStatus.Failure;
                }

                if (Binding.HasArrived)
                {
                    return TaskStatus.Success;
                }

                if (HasMovementFailed(Binding, stuckSeconds, minProgressDistance, moveTimeoutSeconds))
                {
                    Binding.Stop(false);
                    return TaskStatus.Failure;
                }

                return TaskStatus.Running;
            }

            if (!Binding.TryMoveToTarget(resolvedTarget))
            {
                return Binding.HasArrived ? TaskStatus.Success : TaskStatus.Failure;
            }

            return TaskStatus.Success;
        }

        public override void OnReset()
        {
            startedMove = false;
            targetOverride = null;
            waitUntilArrived = false;
            stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;
            minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;
            moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("旧兼容：移动到区域组")]
    [TaskDescription("旧行为树兼容节点；现在会把 near/mid/far 当作 BattleArenaZoneMap 区域组处理，不再按主相机距离采样。新树请使用“移动到战斗区域组”。")]
    public sealed class MonsterMoveToVisibleCameraBand : MonsterBehaviorAction
    {
        private readonly List<string> parsedBands = new List<string>();
        private bool startedMove;

        [TaskTooltip("旧字段名。现在填的是区域组或区域 ID，例如 near,mid、far、left_front；具体格子在怪物移动配置里调。")]
        public SharedString distanceBands = MonsterBehaviorTaskText.DefaultNearMidZoneGroups;

        [TaskTooltip("采样尝试次数。小于等于 0 时使用怪物移动配置里的默认尝试次数。")]
        public SharedInt sampleAttempts;

        [TaskTooltip("旧字段名。可选技能目标；为空时使用当前目标，主要用于避开目标碰撞体。")]
        public SharedTransform observerOverride;

        [TaskTooltip("卡住判定秒数：区域移动中如果这么久没有足够推进就返回 Failure，让行为树重新采样。小于等于 0 表示不判定卡住。")]
        public SharedFloat stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;

        [TaskTooltip("最小推进距离：剩余路径距离至少减少这么多，才算本次移动有推进。")]
        public SharedFloat minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;

        [TaskTooltip("移动超时：单次区域移动超过这个时间还没到就返回 Failure。小于等于 0 表示不超时。")]
        public SharedFloat moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;

        public override void OnStart()
        {
            startedMove = false;
            if (!TryCacheBinding())
            {
                return;
            }

            ParseCsv(
                distanceBands != null ? distanceBands.Value : null,
                parsedBands,
                MonsterBehaviorTaskText.DefaultNearMidZoneGroups);
            startedMove = Binding.TryMoveToVisibleCameraBand(
                ResolveTarget(observerOverride),
                parsedBands,
                sampleAttempts != null ? sampleAttempts.Value : 0);
        }

        public override TaskStatus OnUpdate()
        {
            if (!startedMove)
            {
                return TaskStatus.Failure;
            }

            if (Binding.HasArrived)
            {
                return TaskStatus.Success;
            }

            if (HasMovementFailed(Binding, stuckSeconds, minProgressDistance, moveTimeoutSeconds))
            {
                Binding.Stop(false);
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }

        public override void OnReset()
        {
            distanceBands = MonsterBehaviorTaskText.DefaultNearMidZoneGroups;
            sampleAttempts = 0;
            observerOverride = null;
            stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;
            minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;
            moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("移动到战斗区域组")]
    [TaskDescription("用两个枚举维度选择 BattleArenaZoneMap 战斗站位：前/中/后排与左/中/右列；节点内部组合成稳定区域 ID 后采样可达点并移动过去。")]
    public sealed class MonsterMoveToBattleZoneGroup : MonsterBehaviorAction
    {
        private readonly List<string> selectedZoneIds = new List<string>();
        private bool startedMove;

        [MonsterBattleZoneRows]
        [TaskTooltip("选择战斗区域的排，可多选。前排=near/front，中排=mid，后排=far/back；至少选择一个。")]
        public MonsterBattleZoneRows rows = MonsterBehaviorTaskText.DefaultApproachRows;

        [MonsterBattleZoneColumns]
        [TaskTooltip("选择战斗区域的列，可多选。左列=left，中列=center，右列=right；至少选择一个。")]
        public MonsterBattleZoneColumns columns = MonsterBehaviorTaskText.DefaultColumns;

        [TaskTooltip("采样尝试次数。小于等于 0 时使用 BattleArenaZoneMap 或怪物移动配置里的默认尝试次数。")]
        public SharedInt sampleAttempts;

        [TaskTooltip("可选技能目标；为空时使用当前目标，主要用于避开目标碰撞体。")]
        public SharedTransform targetOverride;

        [TaskTooltip("卡住判定秒数：区域移动中如果这么久没有足够推进就返回 Failure，让行为树重新采样。小于等于 0 表示不判定卡住。")]
        public SharedFloat stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;

        [TaskTooltip("最小推进距离：剩余路径距离至少减少这么多，才算本次移动有推进。")]
        public SharedFloat minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;

        [TaskTooltip("移动超时：单次区域移动超过这个时间还没到就返回 Failure。小于等于 0 表示不超时。")]
        public SharedFloat moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;

        public override void OnStart()
        {
            startedMove = false;
            if (!TryCacheBinding())
            {
                return;
            }

            if (!MonsterBattleZoneSelectionUtility.TryBuildZoneIds(rows, columns, selectedZoneIds))
            {
                return;
            }

            startedMove = Binding.TryMoveToBattleZoneGroup(
                ResolveTarget(targetOverride),
                selectedZoneIds,
                sampleAttempts != null ? sampleAttempts.Value : 0);
        }

        public override TaskStatus OnUpdate()
        {
            if (!startedMove)
            {
                return TaskStatus.Failure;
            }

            if (Binding.HasArrived)
            {
                return TaskStatus.Success;
            }

            if (HasMovementFailed(Binding, stuckSeconds, minProgressDistance, moveTimeoutSeconds))
            {
                Binding.Stop(false);
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }

        public override void OnReset()
        {
            rows = MonsterBehaviorTaskText.DefaultApproachRows;
            columns = MonsterBehaviorTaskText.DefaultColumns;
            sampleAttempts = 0;
            targetOverride = null;
            stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;
            minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;
            moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("移动到战斗区域")]
    [TaskDescription("从 BattleArenaZoneMap 的指定区域里采样可达点，并移动过去。")]
    public sealed class MonsterMoveToBattleZone : MonsterBehaviorAction
    {
        private bool startedMove;

        [TaskTooltip("战斗区域 ID，例如 center、front_left 等，必须和 BattleArenaZoneMap 中的区域 ID 一致。")]
        public SharedString zoneId = "center";

        [TaskTooltip("采样模式。当前支持 random_reachable，表示在区域内随机找可到达点。")]
        public SharedString sampleMode = BattleZoneSampler.RandomReachableSampleMode;

        [TaskTooltip("采样尝试次数。小于等于 0 时使用区域图配置里的默认尝试次数。")]
        public SharedInt sampleAttempts;

        [TaskTooltip("可选技能目标；为空时使用当前目标，主要用于避开目标碰撞体。")]
        public SharedTransform targetOverride;

        [TaskTooltip("卡住判定秒数：区域移动中如果这么久没有足够推进就返回 Failure，让行为树重新采样。小于等于 0 表示不判定卡住。")]
        public SharedFloat stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;

        [TaskTooltip("最小推进距离：剩余路径距离至少减少这么多，才算本次移动有推进。")]
        public SharedFloat minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;

        [TaskTooltip("移动超时：单次区域移动超过这个时间还没到就返回 Failure。小于等于 0 表示不超时。")]
        public SharedFloat moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;

        public override void OnStart()
        {
            startedMove = TryCacheBinding()
                && Binding.TryMoveToBattleZone(
                    ResolveTarget(targetOverride),
                    zoneId != null ? zoneId.Value : null,
                    sampleMode != null ? sampleMode.Value : null,
                    sampleAttempts != null ? sampleAttempts.Value : 0);
        }

        public override TaskStatus OnUpdate()
        {
            if (!startedMove)
            {
                return TaskStatus.Failure;
            }

            if (Binding.HasArrived)
            {
                return TaskStatus.Success;
            }

            if (HasMovementFailed(Binding, stuckSeconds, minProgressDistance, moveTimeoutSeconds))
            {
                Binding.Stop(false);
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }

        public override void OnReset()
        {
            zoneId = "center";
            sampleMode = BattleZoneSampler.RandomReachableSampleMode;
            sampleAttempts = 0;
            targetOverride = null;
            stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;
            minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;
            moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("释放怪物技能")]
    [TaskDescription("启动指定怪物技能，并在技能施放结束后返回 Success；缺少目标、技能不存在或冷却未好时返回 Failure。")]
    public sealed class MonsterUseSkill : MonsterBehaviorAction
    {
        private bool startedCast;

        [MonsterSkillId]
        [TaskTooltip("要释放的技能 ID。下拉显示中文名，保存稳定 skillId。")]
        public SharedString skillId = MonsterBehaviorTaskText.DefaultSkillId;

        [TaskTooltip("可选覆盖目标；为空时使用当前目标。")]
        public SharedTransform targetOverride;

        [TaskTooltip("开启后，释放前会再次检查技能施放距离和视线条件。")]
        public SharedBool checkReleaseConditions = true;

        public override void OnStart()
        {
            startedCast = false;
            if (!TryCacheBinding())
            {
                return;
            }

            Transform resolvedTarget = ResolveTarget(targetOverride);
            string resolvedSkillId = ResolveSkillId(skillId);
            if (checkReleaseConditions == null || checkReleaseConditions.Value)
            {
                if (!Binding.CanReleaseSkill(resolvedSkillId, resolvedTarget))
                {
                    return;
                }
            }

            startedCast = Binding.TryUseSkill(resolvedSkillId, resolvedTarget);
        }

        public override TaskStatus OnUpdate()
        {
            if (!startedCast)
            {
                return TaskStatus.Failure;
            }

            return Binding.IsCasting ? TaskStatus.Running : TaskStatus.Success;
        }

        public override void OnReset()
        {
            skillId = MonsterBehaviorTaskText.DefaultSkillId;
            targetOverride = null;
            checkReleaseConditions = true;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("巡逻")]
    [TaskDescription("清空目标并启动出生点周围巡逻；启动成功后立即返回 Success，方便行为树下一轮继续找目标。")]
    public sealed class MonsterPatrol : MonsterBehaviorAction
    {
        private bool startedPatrol;

        [TaskTooltip("卡住判定秒数：如果上一条巡逻移动这么久没有足够推进，本节点返回 Failure 并停止当前移动。小于等于 0 表示不判定卡住。")]
        public SharedFloat stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;

        [TaskTooltip("最小推进距离：剩余路径距离至少减少这么多，才算本次巡逻移动有推进。")]
        public SharedFloat minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;

        [TaskTooltip("移动超时：单次巡逻移动超过这个时间还没到，本节点返回 Failure 并停止当前移动。小于等于 0 表示不超时。")]
        public SharedFloat moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;

        public override void OnStart()
        {
            startedPatrol = false;
            if (!TryCacheBinding())
            {
                return;
            }

            if (HasMovementFailed(Binding, stuckSeconds, minProgressDistance, moveTimeoutSeconds))
            {
                Binding.Stop(false);
                return;
            }

            startedPatrol = Binding.StartPatrol();
        }

        public override TaskStatus OnUpdate()
        {
            return startedPatrol ? TaskStatus.Success : TaskStatus.Failure;
        }

        public override void OnReset()
        {
            startedPatrol = false;
            stuckSeconds = MonsterBehaviorTaskText.DefaultStuckSeconds;
            minProgressDistance = MonsterBehaviorTaskText.DefaultMinProgressDistance;
            moveTimeoutSeconds = MonsterBehaviorTaskText.DefaultMoveTimeoutSeconds;
        }
    }

    [TaskCategory(MonsterBehaviorTaskText.Category)]
    [TaskName("停止移动")]
    [TaskDescription("清空当前 A* 路径并停止移动，可选同时清空目标。")]
    public sealed class MonsterStopMovement : MonsterBehaviorAction
    {
        [TaskTooltip("是否同时清空当前目标。")]
        public SharedBool clearTarget;

        public override TaskStatus OnUpdate()
        {
            if (!TryCacheBinding())
            {
                return TaskStatus.Failure;
            }

            Binding.Stop(clearTarget != null && clearTarget.Value);
            return TaskStatus.Success;
        }

        public override void OnReset()
        {
            clearTarget = false;
        }
    }
}
