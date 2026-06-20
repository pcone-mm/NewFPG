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

    [Serializable]
    public sealed class LevelRoomDefinition
    {
        public string roomId;
        public string displayName;
        public LevelRoomType roomType = LevelRoomType.Combat;
        public LevelRewardPool rewardPool = LevelRewardPool.MajorFind;
        public string encounterId;
        public string rewardPreview;
        public string roomNote;
        [Min(0)] public int enemyCount = 1;
        public bool startsCombatAfterChoice;
        public List<LevelRoomChoiceDefinition> choices = new List<LevelRoomChoiceDefinition>();
        public List<LevelDoorDefinition> exits = new List<LevelDoorDefinition>();

        public bool IsCombatRoom
        {
            get
            {
                return roomType == LevelRoomType.Combat
                    || roomType == LevelRoomType.EliteCombat
                    || roomType == LevelRoomType.Boss
                    || startsCombatAfterChoice;
            }
        }
    }

    [Serializable]
    public sealed class LevelDoorDefinition
    {
        public string targetRoomId;
        public string displayName;
        public LevelRoomType roomType = LevelRoomType.Combat;
        public LevelRewardPool rewardPool = LevelRewardPool.MajorFind;
        public string rewardPreview;
        public bool canReroll = true;
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
        public string choiceId;
        public string displayName;
        [TextArea] public string description;
        public float damageBonus;
        public float healAmount;
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
