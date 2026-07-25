using FPG.Demo.Editor.SkillAuthoring;
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
                    "Script",
                    MonoScript.FromScriptableObject(skill),
                    typeof(MonoScript),
                    false);
            }

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Unified Skill Timeline",
                EditorStyles.boldLabel);

            if (TryCompileSkill(
                    skill,
                    out int sequenceCount,
                    out ulong gameplayHash,
                    out string error))
            {
                EditorGUILayout.HelpBox(
                    "60 Hz validation passed. Sequences: "
                        + sequenceCount
                        + " | Gameplay hash: 0x"
                        + gameplayHash.ToString("X16"),
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(error)
                        ? "Skill validation failed."
                        : error,
                    MessageType.Error);
            }

            if (GUILayout.Button("Open Skill Editor"))
            {
                FpgSkillEditorWindow.OpenAsset(skill);
            }

            EditorGUILayout.EndVertical();
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
}
