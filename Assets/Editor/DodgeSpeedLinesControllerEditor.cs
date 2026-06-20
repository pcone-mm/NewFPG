using NewFPG.Rendering;
using UnityEditor;
using UnityEngine;

namespace NewFPG.Editor
{
    [CustomEditor(typeof(DodgeSpeedLinesController))]
    internal sealed class DodgeSpeedLinesControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty targetVolume;

        private void OnEnable()
        {
            targetVolume = serializedObject.FindProperty("targetVolume");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Attach this to the scene's Global Volume object. The renderer feature reads the Dodge Speed Lines override from that Volume Profile; it never changes a character material.",
                MessageType.Info);

            EditorGUILayout.PropertyField(targetVolume, new GUIContent("Target Volume"));
            EditorGUILayout.HelpBox(
                "For Animation Events: call EnableEffect on the dodge start frame and DisableEffect on the end frame.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                return;
            }

            EditorGUILayout.Space();
            DodgeSpeedLinesController controller = (DodgeSpeedLinesController)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable Effect"))
                {
                    controller.EnableEffect();
                }

                if (GUILayout.Button("Disable Effect"))
                {
                    controller.DisableEffect();
                }
            }
        }
    }
}
