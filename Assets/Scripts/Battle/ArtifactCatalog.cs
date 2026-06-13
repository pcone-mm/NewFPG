using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Battle
{
    [Serializable]
    public sealed class ArtifactCatalog
    {
        [SerializeField] private List<ArtifactCombatProfile> artifacts = new List<ArtifactCombatProfile>();

        public IReadOnlyList<ArtifactCombatProfile> Artifacts
        {
            get { return artifacts; }
        }

        public int Count
        {
            get { return artifacts != null ? artifacts.Count : 0; }
        }

        public ArtifactCombatProfile GetAt(int index)
        {
            if (artifacts == null || index < 0 || index >= artifacts.Count)
            {
                return null;
            }

            return artifacts[index];
        }

        public ArtifactCombatProfile FindById(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId) || artifacts == null)
            {
                return null;
            }

            for (int i = 0; i < artifacts.Count; i++)
            {
                ArtifactCombatProfile profile = artifacts[i];
                if (profile != null && string.Equals(profile.artifactId, artifactId, StringComparison.Ordinal))
                {
                    return profile;
                }
            }

            return null;
        }

        public int IndexOf(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId) || artifacts == null)
            {
                return -1;
            }

            for (int i = 0; i < artifacts.Count; i++)
            {
                ArtifactCombatProfile profile = artifacts[i];
                if (profile != null && string.Equals(profile.artifactId, artifactId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public bool Contains(string artifactId)
        {
            return IndexOf(artifactId) >= 0;
        }

        public static ArtifactCatalog CreateDefault()
        {
            ArtifactCatalog catalog = new ArtifactCatalog();
            catalog.artifacts.Add(CreateAttack(
                "zhanfeng_short_blade",
                "斩风短刃",
                Element.Metal,
                1,
                1.5f,
                TargetSelectorType.focus_then_nearest,
                SupplyDirection.Right,
                new[] { Element.Earth },
                "破甲增强",
                "damage_boost",
                "相生后下一次",
                0.2f,
                3f,
                "对集火目标或最近敌人造成20伤害。"));

            catalog.artifacts.Add(CreateAttack(
                "shuangshui_needle",
                "霜水飞针",
                Element.Water,
                1,
                1.5f,
                TargetSelectorType.slowed_then_focus,
                SupplyDirection.Left,
                new[] { Element.Metal },
                "减速强化",
                "slow_boost",
                "相生后下一次",
                0.1f,
                3f,
                "造成15伤害并减速30%/2秒。"));

            catalog.artifacts.Add(CreateAttack(
                "baoyan_talisman",
                "爆炎符",
                Element.Fire,
                2,
                4f,
                TargetSelectorType.focus_then_nearest,
                SupplyDirection.Both,
                new[] { Element.Wood },
                "爆燃强化",
                "aoe_boost",
                "相生后下一次",
                0.4f,
                0f,
                "主目标35伤害，小范围溅射20伤害。"));

            catalog.artifacts.Add(CreateAttack(
                "kunyan_seal",
                "坤岩重印",
                Element.Earth,
                3,
                8f,
                TargetSelectorType.high_threat_near_player,
                SupplyDirection.Left,
                new[] { Element.Fire },
                "压制强化",
                "stun_boost",
                "相生后下一次",
                0.3f,
                1.5f,
                "对最近高威胁敌人造成70伤害并击退/眩晕1秒。"));

            catalog.artifacts.Add(CreateDefense(
                "jingshui_amulet",
                "净水护符",
                Element.Water,
                1,
                4f,
                TargetSelectorType.none,
                SupplyDirection.Right,
                new[] { Element.Metal },
                "护盾强化",
                "shield_boost",
                "相生后下一次",
                20f,
                0f,
                "玩家护盾低或HP低于80%时获得50护盾。"));

            catalog.artifacts.Add(CreateDefense(
                "houtu_barrier",
                "厚土壁垒",
                Element.Earth,
                2,
                4f,
                TargetSelectorType.none,
                SupplyDirection.Both,
                new[] { Element.Fire },
                "减伤强化",
                "mitigation_boost",
                "相生后下一次",
                0.25f,
                3f,
                "敌人出现攻击意图时获得40护盾和25%/3秒减伤。"));

            catalog.artifacts.Add(CreateControl(
                "fumu_bell",
                "缚木铃",
                Element.Wood,
                2,
                4f,
                TargetSelectorType.charging_then_near_player,
                SupplyDirection.Right,
                new[] { Element.Water },
                "定身强化",
                "control_boost",
                "相生后下一次",
                2f,
                0f,
                "优先蓄力敌人，定身2秒；若目标蓄力则延迟蓄力1.5秒。"));

            catalog.artifacts.Add(CreateControl(
                "duanjin_ring",
                "断金环",
                Element.Metal,
                2,
                8f,
                TargetSelectorType.interruptible_charging_only,
                SupplyDirection.Left,
                new[] { Element.Earth },
                "破防强化",
                "interrupt_boost",
                "相生后下一次",
                0.2f,
                1f,
                "只在存在可打断蓄力敌人时释放，打断并造成20伤害。"));

            catalog.artifacts.Add(CreateSupport(
                "qingmu_heal_orb",
                "青木回春珠",
                Element.Wood,
                1,
                4f,
                TargetSelectorType.none,
                SupplyDirection.Both,
                new[] { Element.Water },
                "回复强化",
                "heal_boost",
                "相生后下一次",
                5f,
                0f,
                "玩家HP低于80%时回复35HP。"));

            catalog.artifacts.Add(CreateSupport(
                "lianhuo_catalyst_lamp",
                "炼火催灵灯",
                Element.Fire,
                2,
                8f,
                TargetSelectorType.none,
                SupplyDirection.Right,
                new[] { Element.Wood },
                "急速强化",
                "haste_boost",
                "相生后下一次",
                0.25f,
                3f,
                "给剩余CD最高的相邻法宝充能2秒，并急速25%/3秒。"));

            return catalog;
        }

        private static ArtifactCombatProfile CreateAttack(
            string artifactId,
            string displayName,
            Element element,
            int size,
            float cooldown,
            TargetSelectorType targetSelectorType,
            SupplyDirection supplyDirection,
            Element[] acceptedElements,
            string buffName,
            string buffType,
            string trigger,
            float buffValue,
            float buffDuration,
            string note)
        {
            ArtifactCombatProfile profile = CreateBaseProfile(
                artifactId,
                displayName,
                element,
                ArtifactCategory.Attack,
                size,
                cooldown,
                targetSelectorType,
                supplyDirection,
                acceptedElements,
                buffName,
                buffType,
                trigger,
                buffValue,
                buffDuration,
                note);

            profile.damage = GetDamageHint(displayName);
            return profile;
        }

        private static ArtifactCombatProfile CreateDefense(
            string artifactId,
            string displayName,
            Element element,
            int size,
            float cooldown,
            TargetSelectorType targetSelectorType,
            SupplyDirection supplyDirection,
            Element[] acceptedElements,
            string buffName,
            string buffType,
            string trigger,
            float buffValue,
            float buffDuration,
            string note)
        {
            ArtifactCombatProfile profile = CreateBaseProfile(
                artifactId,
                displayName,
                element,
                ArtifactCategory.Defense,
                size,
                cooldown,
                targetSelectorType,
                supplyDirection,
                acceptedElements,
                buffName,
                buffType,
                trigger,
                buffValue,
                buffDuration,
                note);

            profile.shield = GetShieldHint(displayName);
            return profile;
        }

        private static ArtifactCombatProfile CreateControl(
            string artifactId,
            string displayName,
            Element element,
            int size,
            float cooldown,
            TargetSelectorType targetSelectorType,
            SupplyDirection supplyDirection,
            Element[] acceptedElements,
            string buffName,
            string buffType,
            string trigger,
            float buffValue,
            float buffDuration,
            string note)
        {
            ArtifactCombatProfile profile = CreateBaseProfile(
                artifactId,
                displayName,
                element,
                ArtifactCategory.Control,
                size,
                cooldown,
                targetSelectorType,
                supplyDirection,
                acceptedElements,
                buffName,
                buffType,
                trigger,
                buffValue,
                buffDuration,
                note);

            profile.controlDuration = Mathf.Max(0f, buffValue);
            return profile;
        }

        private static ArtifactCombatProfile CreateSupport(
            string artifactId,
            string displayName,
            Element element,
            int size,
            float cooldown,
            TargetSelectorType targetSelectorType,
            SupplyDirection supplyDirection,
            Element[] acceptedElements,
            string buffName,
            string buffType,
            string trigger,
            float buffValue,
            float buffDuration,
            string note)
        {
            ArtifactCombatProfile profile = CreateBaseProfile(
                artifactId,
                displayName,
                element,
                ArtifactCategory.Support,
                size,
                cooldown,
                targetSelectorType,
                supplyDirection,
                acceptedElements,
                buffName,
                buffType,
                trigger,
                buffValue,
                buffDuration,
                note);

            profile.heal = GetHealHint(displayName);
            return profile;
        }

        private static ArtifactCombatProfile CreateBaseProfile(
            string artifactId,
            string displayName,
            Element element,
            ArtifactCategory category,
            int size,
            float cooldown,
            TargetSelectorType targetSelectorType,
            SupplyDirection supplyDirection,
            Element[] acceptedElements,
            string buffName,
            string buffType,
            string trigger,
            float buffValue,
            float buffDuration,
            string note)
        {
            ArtifactCombatProfile profile = new ArtifactCombatProfile();
            profile.artifactId = artifactId;
            profile.displayName = displayName;
            profile.element = element;
            profile.category = category;
            profile.size = size;
            profile.cooldown = cooldown;
            profile.autoEnabled = true;
            profile.targetSelectorType = targetSelectorType;
            profile.supplyDirection = supplyDirection;
            profile.acceptedElements = new List<Element>(acceptedElements ?? Array.Empty<Element>());
            profile.shengBuff = new ElementSupplyBuff
            {
                buffId = artifactId + "_sheng",
                buffName = buffName,
                buffType = buffType,
                trigger = trigger,
                value = buffValue,
                duration = buffDuration,
                maxStacks = 1,
                canStack = false,
                consumeRule = "next_release",
                sourceRelation = note,
            };
            profile.tags = new List<string> { "样例池", CategoryDisplayName(category) };
            profile.effectLogicId = "prototype_sample";
            profile.castConditionId = "prototype_sample";
            profile.canProcSupply = true;
            return profile;
        }

        private static string CategoryDisplayName(ArtifactCategory category)
        {
            return BattleDisplayText.CategoryName(category);
        }

        private static float GetDamageHint(string displayName)
        {
            switch (displayName)
            {
                case "斩风短刃":
                    return 20f;
                case "霜水飞针":
                    return 15f;
                case "爆炎符":
                    return 35f;
                case "坤岩重印":
                    return 70f;
                default:
                    return 0f;
            }
        }

        private static float GetShieldHint(string displayName)
        {
            switch (displayName)
            {
                case "净水护符":
                    return 50f;
                case "厚土壁垒":
                    return 40f;
                default:
                    return 0f;
            }
        }

        private static float GetHealHint(string displayName)
        {
            if (displayName == "青木回春珠")
            {
                return 35f;
            }

            return 0f;
        }
    }
}
