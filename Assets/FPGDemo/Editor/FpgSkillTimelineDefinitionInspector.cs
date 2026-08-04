using FPG.Demo.Editor.SkillAuthoring;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace FPG.Demo.Editor
{
    [CustomEditor(typeof(FpgSkillTimelineDefinition), true)]
    internal sealed class FpgSkillTimelineDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            FpgSkillTimelineDefinition skill =
                (FpgSkillTimelineDefinition)target;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "脚本",
                    MonoScript.FromScriptableObject(skill),
                    typeof(MonoScript),
                    false);
            }

            serializedObject.Update();
            if (skill is FpgPlayerSkillDefinition player)
            {
                if (player.UsesSecondaryTriggerMode)
                {
                    DrawPlayerSkillActivationProperties();
                }

                DrawPropertiesExcluding(
                    serializedObject,
                    "m_Script",
                    "secondaryTriggerMode",
                    "minimumChargeTicks",
                    "sequenceCooldownTicks",
                    "chargeProgressTicks");
            }
            else
            {
                DrawPropertiesExcluding(serializedObject, "m_Script");
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "统一技能时间轴",
                EditorStyles.boldLabel);

            if (TryCompileSkill(
                    skill,
                    out int sequenceCount,
                    out ulong gameplayHash,
                    out string error))
            {
                EditorGUILayout.HelpBox(
                    "60 Hz 校验通过。序列数："
                        + sequenceCount
                        + " | Gameplay 哈希：0x"
                        + gameplayHash.ToString("X16"),
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(error)
                        ? "技能校验失败。"
                        : error,
                    MessageType.Error);
            }

            if (GUILayout.Button("打开技能编辑器"))
            {
                FpgSkillEditorWindow.OpenAsset(skill);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPlayerSkillActivationProperties()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "玩家技能激活",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "该模式只声明副射技能资产的触发契约；实际启用的副射由武器槽位和角色目录中的模式选择决定。",
                MessageType.Info);

            SerializedProperty mode = serializedObject.FindProperty(
                "secondaryTriggerMode");
            if (mode != null)
            {
                EditorGUI.BeginChangeCheck();
                int selectedIndex = EditorGUILayout.Popup(
                    new GUIContent("副射触发模式"),
                    Mathf.Clamp(mode.enumValueIndex, 0, 1),
                    new[] { "蓄力释放", "按住时立即重复" });
                if (EditorGUI.EndChangeCheck())
                {
                    mode.enumValueIndex = selectedIndex;
                }
            }

            DrawLocalizedActivationProperty(
                "minimumChargeTicks",
                "最小蓄力 Tick");
            DrawLocalizedActivationProperty(
                "sequenceCooldownTicks",
                "序列冷却 Tick");
            DrawLocalizedActivationProperty(
                "chargeProgressTicks",
                "蓄力进度 Tick");
        }

        private void DrawLocalizedActivationProperty(
            string propertyName,
            string label)
        {
            SerializedProperty property = serializedObject.FindProperty(
                propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(
                    property,
                    new GUIContent(label),
                    includeChildren: false);
            }
        }

        private static bool TryCompileSkill(
            FpgSkillTimelineDefinition skill,
            out int sequenceCount,
            out ulong gameplayHash,
            out string error)
        {
            if (skill is FpgPlayerSkillDefinition player)
            {
                bool success = player.TryCompile(
                    out FpgCompiledPlayerSkillDefinition compiled,
                    out error);
                sequenceCount = success
                    ? compiled.Timeline.SequenceCount
                    : 0;
                gameplayHash = success ? compiled.GameplayHash : 0UL;
                return success;
            }

            if (skill is FpgEnemyAttackDefinition enemy)
            {
                bool success = enemy.TryCompile(
                    out FpgCompiledEnemySkillDefinition compiled,
                    out error);
                sequenceCount = success
                    ? compiled.Timeline.SequenceCount
                    : 0;
                gameplayHash = success ? compiled.GameplayHash : 0UL;
                return success;
            }

            bool baseSuccess = skill.TryCompile(
                out FpgCompiledSkillDefinition timeline,
                out error);
            sequenceCount = baseSuccess ? timeline.SequenceCount : 0;
            gameplayHash = baseSuccess ? timeline.GameplayHash : 0UL;
            return baseSuccess;
        }


        [OnOpenAsset]
        private static bool OpenSkillAsset(int instanceId, int line)
        {
            FpgSkillTimelineDefinition skill =
                EditorUtility.InstanceIDToObject(instanceId)
                    as FpgSkillTimelineDefinition;
            if (skill == null)
            {
                return false;
            }

            FpgSkillEditorWindow.OpenAsset(skill);
            return true;
        }
    }

    [CustomPropertyDrawer(typeof(SecondaryTriggerMode))]
    internal sealed class SecondaryTriggerModeDrawer : PropertyDrawer
    {
        private static readonly string[] Labels =
        {
            "蓄力释放",
            "按住时立即重复"
        };

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUI.Popup(
                position,
                label.text,
                Mathf.Clamp(property.enumValueIndex, 0, Labels.Length - 1),
                Labels);
            if (EditorGUI.EndChangeCheck())
            {
                property.enumValueIndex = selectedIndex;
            }

            EditorGUI.EndProperty();
        }
    }
}
