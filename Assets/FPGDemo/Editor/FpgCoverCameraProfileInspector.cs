using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    [CustomEditor(typeof(FpgCoverCameraProfile))]
    internal sealed class FpgCoverCameraProfileInspector
        : D0PlannerConfigurationInspector
    {
        private string clipboardStatus;
        private MessageType clipboardStatusType;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(
                        "Copy All Settings",
                        "Copy every camera profile setting to the system clipboard.")))
                {
                    CopyAllSettings();
                }

                if (GUILayout.Button(new GUIContent(
                        "Paste All Settings",
                        "Replace this profile's settings with compatible clipboard data.")))
                {
                    PasteAllSettings();
                }
            }

            if (!string.IsNullOrWhiteSpace(clipboardStatus))
            {
                EditorGUILayout.HelpBox(
                    clipboardStatus,
                    clipboardStatusType);
            }
        }

        private void CopyAllSettings()
        {
            if (!FpgCoverCameraProfileAuthoring.TryCreateClipboardText(
                    target as FpgCoverCameraProfile,
                    out string clipboardText,
                    out string error))
            {
                SetClipboardStatus(error, MessageType.Error);
                return;
            }

            GUIUtility.systemCopyBuffer = clipboardText;
            SetClipboardStatus(
                "Copied all camera profile settings.",
                MessageType.Info);
        }

        private void PasteAllSettings()
        {
            if (!FpgCoverCameraProfileAuthoring.TryPasteClipboardText(
                    GUIUtility.systemCopyBuffer,
                    target as FpgCoverCameraProfile,
                    out string error))
            {
                SetClipboardStatus(error, MessageType.Error);
                return;
            }

            serializedObject.Update();
            SceneView.RepaintAll();
            SetClipboardStatus(
                "Pasted all camera profile settings.",
                MessageType.Info);
        }

        private void SetClipboardStatus(string message, MessageType type)
        {
            clipboardStatus = message;
            clipboardStatusType = type;
            Repaint();
        }
    }
}
