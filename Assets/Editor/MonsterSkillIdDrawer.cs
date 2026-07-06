using System.Collections.Generic;
using BehaviorDesigner.Editor;
using BehaviorDesigner.Runtime;
using NewFPG.Monsters;
using NewFPG.Monsters.BehaviorDesigner;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    [CustomObjectDrawer(typeof(MonsterSkillIdAttribute))]
    public sealed class MonsterSkillIdDrawer : ObjectDrawer
    {
        private static readonly List<MonsterSkillDefinition> Skills = new List<MonsterSkillDefinition>();
        private static readonly List<string> SkillIds = new List<string>();
        private static readonly List<string> SkillLabels = new List<string>();

        public override void OnGUI(GUIContent label)
        {
            string currentSkillId = ReadCurrentSkillId();
            CollectSkillOptions(currentSkillId);

            GUIContent skillLabel = new GUIContent("技能", label.tooltip);
            if (SkillIds.Count == 0)
            {
                WriteSkillId(EditorGUILayout.TextField(skillLabel, currentSkillId));
                return;
            }

            int selectedIndex = Mathf.Max(0, SkillIds.IndexOf(currentSkillId));
            int nextIndex = EditorGUILayout.Popup(skillLabel, selectedIndex, SkillLabels.ToArray());
            WriteSkillId(SkillIds[Mathf.Clamp(nextIndex, 0, SkillIds.Count - 1)]);
        }

        private string ReadCurrentSkillId()
        {
            if (value is SharedString sharedString)
            {
                return string.IsNullOrWhiteSpace(sharedString.Value) ? "melee_bite" : sharedString.Value;
            }

            return value is string text && !string.IsNullOrWhiteSpace(text) ? text : "melee_bite";
        }

        private void WriteSkillId(string skillId)
        {
            string resolvedSkillId = string.IsNullOrWhiteSpace(skillId) ? "melee_bite" : skillId.Trim();
            if (value is SharedString sharedString)
            {
                sharedString.Value = resolvedSkillId;
                return;
            }

            value = resolvedSkillId;
        }

        private void CollectSkillOptions(string currentSkillId)
        {
            SkillIds.Clear();
            SkillLabels.Clear();

            MonsterConfigBinding binding = ResolveBinding();
            if (binding != null)
            {
                binding.GetKnownSkills(Skills);
                for (int i = 0; i < Skills.Count; i++)
                {
                    MonsterSkillDefinition skill = Skills[i];
                    if (skill == null || string.IsNullOrWhiteSpace(skill.skillId) || SkillIds.Contains(skill.skillId))
                    {
                        continue;
                    }

                    SkillIds.Add(skill.skillId);
                    SkillLabels.Add(string.IsNullOrWhiteSpace(skill.displayName)
                        ? skill.skillId
                        : $"{skill.displayName} ({skill.skillId})");
                }
            }

            if (!string.IsNullOrWhiteSpace(currentSkillId) && !SkillIds.Contains(currentSkillId))
            {
                SkillIds.Add(currentSkillId);
                SkillLabels.Add(currentSkillId == "melee_bite" ? "近身咬击 (melee_bite)" : currentSkillId);
            }
        }

        private MonsterConfigBinding ResolveBinding()
        {
            if (Task == null || Task.Owner == null)
            {
                return null;
            }

            return Task.Owner.GetComponent<MonsterConfigBinding>();
        }
    }

    [CustomObjectDrawer(typeof(MonsterBattleZoneRowsAttribute))]
    public sealed class MonsterBattleZoneRowsDrawer : ObjectDrawer
    {
        private static readonly string[] RowLabels = { "前排 near", "中排 mid", "后排 far" };

        public override void OnGUI(GUIContent label)
        {
            MonsterBattleZoneRows current = value is MonsterBattleZoneRows rows
                ? rows
                : MonsterBehaviorTaskText.DefaultApproachRows;
            int mask = ToMask(current);
            int nextMask = EditorGUILayout.MaskField(new GUIContent("战斗排", label.tooltip), mask, RowLabels);
            value = FromMask(nextMask);
        }

        private static int ToMask(MonsterBattleZoneRows rows)
        {
            int mask = 0;
            if ((rows & MonsterBattleZoneRows.Front) != 0)
            {
                mask |= 1 << 0;
            }

            if ((rows & MonsterBattleZoneRows.Middle) != 0)
            {
                mask |= 1 << 1;
            }

            if ((rows & MonsterBattleZoneRows.Back) != 0)
            {
                mask |= 1 << 2;
            }

            return mask;
        }

        private static MonsterBattleZoneRows FromMask(int mask)
        {
            MonsterBattleZoneRows rows = MonsterBattleZoneRows.None;
            if ((mask & (1 << 0)) != 0)
            {
                rows |= MonsterBattleZoneRows.Front;
            }

            if ((mask & (1 << 1)) != 0)
            {
                rows |= MonsterBattleZoneRows.Middle;
            }

            if ((mask & (1 << 2)) != 0)
            {
                rows |= MonsterBattleZoneRows.Back;
            }

            return rows;
        }
    }

    [CustomObjectDrawer(typeof(MonsterBattleZoneColumnsAttribute))]
    public sealed class MonsterBattleZoneColumnsDrawer : ObjectDrawer
    {
        private static readonly string[] ColumnLabels = { "左列 left", "中列 center", "右列 right" };

        public override void OnGUI(GUIContent label)
        {
            MonsterBattleZoneColumns current = value is MonsterBattleZoneColumns columns
                ? columns
                : MonsterBehaviorTaskText.DefaultColumns;
            int mask = ToMask(current);
            int nextMask = EditorGUILayout.MaskField(new GUIContent("战斗列", label.tooltip), mask, ColumnLabels);
            value = FromMask(nextMask);
        }

        private static int ToMask(MonsterBattleZoneColumns columns)
        {
            int mask = 0;
            if ((columns & MonsterBattleZoneColumns.Left) != 0)
            {
                mask |= 1 << 0;
            }

            if ((columns & MonsterBattleZoneColumns.Center) != 0)
            {
                mask |= 1 << 1;
            }

            if ((columns & MonsterBattleZoneColumns.Right) != 0)
            {
                mask |= 1 << 2;
            }

            return mask;
        }

        private static MonsterBattleZoneColumns FromMask(int mask)
        {
            MonsterBattleZoneColumns columns = MonsterBattleZoneColumns.None;
            if ((mask & (1 << 0)) != 0)
            {
                columns |= MonsterBattleZoneColumns.Left;
            }

            if ((mask & (1 << 1)) != 0)
            {
                columns |= MonsterBattleZoneColumns.Center;
            }

            if ((mask & (1 << 2)) != 0)
            {
                columns |= MonsterBattleZoneColumns.Right;
            }

            return columns;
        }
    }
}
