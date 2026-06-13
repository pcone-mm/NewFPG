using System.Collections.Generic;
using System.Text;

namespace NewFPG.Battle
{
    public static class BattleDisplayText
    {
        public static string ElementName(Element element)
        {
            switch (element)
            {
                case Element.Metal:
                    return "金";
                case Element.Water:
                    return "水";
                case Element.Wood:
                    return "木";
                case Element.Fire:
                    return "火";
                case Element.Earth:
                    return "土";
                default:
                    return "无";
            }
        }

        public static string CategoryName(ArtifactCategory category)
        {
            switch (category)
            {
                case ArtifactCategory.Attack:
                    return "攻击";
                case ArtifactCategory.Defense:
                    return "防御";
                case ArtifactCategory.Control:
                    return "控制";
                case ArtifactCategory.Support:
                    return "辅助";
                default:
                    return "无";
            }
        }

        public static string SupplyDirectionName(SupplyDirection direction)
        {
            switch (direction)
            {
                case SupplyDirection.Left:
                    return "向左";
                case SupplyDirection.Right:
                    return "向右";
                case SupplyDirection.Both:
                    return "双向";
                default:
                    return "无";
            }
        }

        public static string TargetSelectorName(TargetSelectorType selectorType)
        {
            switch (selectorType)
            {
                case TargetSelectorType.focus_then_nearest:
                    return "集火优先";
                case TargetSelectorType.nearest:
                    return "最近敌人";
                case TargetSelectorType.lowest_hp:
                    return "残血优先";
                case TargetSelectorType.charging_then_near_player:
                    return "蓄力优先";
                case TargetSelectorType.interruptible_charging_only:
                    return "可打断蓄力";
                case TargetSelectorType.high_threat_near_player:
                    return "近身高威胁";
                case TargetSelectorType.slowed_then_focus:
                    return "减速优先";
                default:
                    return "不选目标";
            }
        }

        public static string JoinedElements(IReadOnlyList<Element> elements)
        {
            if (elements == null || elements.Count == 0)
            {
                return "无";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < elements.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('、');
                }

                builder.Append(ElementName(elements[i]));
            }

            return builder.ToString();
        }

        public static string CooldownName(float cooldown)
        {
            if (cooldown <= 0f)
            {
                return "无";
            }

            if (cooldown < 2f)
            {
                return "短";
            }

            if (cooldown < 5f)
            {
                return "中";
            }

            return "长";
        }

        public static string FormatCooldownSeconds(float cooldown)
        {
            if (cooldown <= 0f)
            {
                return "0秒";
            }

            return cooldown.ToString("0.##") + "秒";
        }

        public static string FormatBuffSummary(ElementSupplyBuff buff)
        {
            if (buff == null)
            {
                return "无";
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(buff.buffName))
            {
                parts.Add(buff.buffName);
            }

            if (!string.IsNullOrWhiteSpace(buff.buffType))
            {
                parts.Add(buff.buffType);
            }

            if (!string.IsNullOrWhiteSpace(buff.trigger))
            {
                parts.Add("触发：" + buff.trigger);
            }

            if (buff.value != 0f)
            {
                parts.Add("数值：" + buff.value.ToString("0.##"));
            }

            if (buff.duration != 0f)
            {
                parts.Add("持续：" + FormatCooldownSeconds(buff.duration));
            }

            return parts.Count == 0 ? "无" : string.Join("，", parts);
        }
    }
}
