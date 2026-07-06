using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Level
{
    public enum LevelRouteId
    {
        UndergroundFirstFloor,
    }

    public enum LevelRoomType
    {
        Combat,
        Blessing,
        StoryEvent,
        EliteCombat,
        Shop,
        Rest,
        Boss,
    }

    public enum LevelRewardPool
    {
        None,
        MajorFind,
        MinorFind,
        SpecialDoor,
        PostClearAddOn,
    }

    public enum LevelFlowState
    {
        Idle,
        EnteringRoom,
        AwaitingRoomInteraction,
        AwaitingEventChoice,
        InCombat,
        ResolvingRoom,
        ChoosingNextRoom,
        Complete,
    }

    public enum LevelRoomTriggerMode
    {
        OnEnter,
        OnInteract,
    }

    public enum LevelRoomCompletionMode
    {
        ResolveRoom,
        StartEncounter,
        CompleteRoute,
    }

    [Serializable]
    public sealed class LevelRoomDefinition
    {
        [Tooltip("房间内部 id。startRoomId、门的 targetRoomId 会用这个 id 查找房间。")]
        public string roomId;

        [Tooltip("房间显示名称，会显示在关卡 HUD 中。")]
        public string displayName;

        [Tooltip("房间类型，用于表现和门预告；是否开战由 completionMode 决定。")]
        public LevelRoomType roomType = LevelRoomType.Combat;

        [Tooltip("奖励池标记，目前用于 HUD/门预告和后续奖励系统接入。")]
        public LevelRewardPool rewardPool = LevelRewardPool.MajorFind;

        [Tooltip("触发方式：OnEnter 进房后自动继续；OnInteract 会先生成交互物，玩家交互后才继续。")]
        public LevelRoomTriggerMode triggerMode = LevelRoomTriggerMode.OnEnter;

        [Tooltip("房间完成方式：直接结算房间、启动 encounter，或结束整条路线。")]
        public LevelRoomCompletionMode completionMode = LevelRoomCompletionMode.ResolveRoom;

        [Tooltip("默认 encounter id。completionMode 为 StartEncounter 时，Director 会用它去 LevelEncounterTable 查找刷怪配置。")]
        public string encounterId;

        [Tooltip("奖励预告文本，会显示在房间摘要和门选项里。")]
        public string rewardPreview;

        [TextArea, Tooltip("房间策划备注，会显示在房间摘要里，也方便说明触发和刷怪意图。")]
        public string roomNote;

        [Tooltip("房间内可选项。存在选项时，交互或进房触发后会先让玩家选择。")]
        public List<LevelRoomChoiceDefinition> choices = new List<LevelRoomChoiceDefinition>();

        [Tooltip("清房或结算后可选择的出口门。没有出口时会结束当前路线。")]
        public List<LevelDoorDefinition> exits = new List<LevelDoorDefinition>();

        public bool StartsEncounter => completionMode == LevelRoomCompletionMode.StartEncounter;
        public bool IsCombatRoom => StartsEncounter;
    }

    [Serializable]
    public sealed class LevelDoorDefinition
    {
        [Tooltip("目标房间 id，必须能在同一张 LevelRouteTable 的 rooms 中找到。")]
        public string targetRoomId;

        [Tooltip("门显示名称，会显示在下一房间选择按钮上。")]
        public string displayName;

        [Tooltip("门预告的房间类型，不直接改变目标房间配置，只用于展示和策划标记。")]
        public LevelRoomType roomType = LevelRoomType.Combat;

        [Tooltip("门预告的奖励池，不直接结算奖励，只用于展示和后续奖励系统接入。")]
        public LevelRewardPool rewardPool = LevelRewardPool.MajorFind;

        [Tooltip("门上的奖励预告文本。")]
        public string rewardPreview;

        [Tooltip("是否允许后续接入换门/重随机逻辑。当前只作为配置标记保留。")]
        public bool canReroll = true;

        [Tooltip("是否为风险门。当前会在门按钮文本中追加风险标记。")]
        public bool isRiskDoor;

        public string BuildLabel()
        {
            string risk = isRiskDoor ? " [Risk]" : string.Empty;
            return string.IsNullOrWhiteSpace(rewardPreview)
                ? displayName + risk
                : displayName + " - " + rewardPreview + risk;
        }
    }

    [Serializable]
    public sealed class LevelRoomChoiceDefinition
    {
        [Tooltip("选项内部 id，仅用于配置辨识。")]
        public string choiceId;

        [Tooltip("选项显示名称，会显示在 HUD 选择按钮上。")]
        public string displayName;

        [TextArea, Tooltip("选项说明文本，会和显示名称一起显示在 HUD 选择按钮上。")]
        public string description;

        [Tooltip("可选：选择该项后覆盖房间默认 encounterId，用于同一房间不同选择触发不同刷怪。留空则使用房间默认 encounterId。")]
        public string encounterIdOverride;

        [Tooltip("选择后增加的本局伤害加成，小数表示。例如 0.2 表示 +20%。")]
        public float damageBonus;

        [Tooltip("选择后增加或减少的金币数量。")]
        public int goldDelta;

        public string BuildLabel()
        {
            return string.IsNullOrWhiteSpace(description)
                ? displayName
                : displayName + "\n" + description;
        }
    }

    public readonly struct LevelHudChoice
    {
        public readonly string label;
        public readonly Action selected;

        public LevelHudChoice(string label, Action selected)
        {
            this.label = label;
            this.selected = selected;
        }
    }
}
