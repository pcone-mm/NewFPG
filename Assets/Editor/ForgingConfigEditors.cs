using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    [CustomEditor(typeof(NewFPG.Forging.ForgingMaterialConfig))]
    public sealed class ForgingMaterialConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "cells");
            EditorGUILayout.Space(6f);
            ForgingShapeGridDrawer.Draw(
                serializedObject.FindProperty("shapeWidth"),
                serializedObject.FindProperty("shapeHeight"),
                serializedObject.FindProperty("cells"),
                true);
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(NewFPG.Forging.ForgingWeaponBlueprintConfig))]
    public sealed class ForgingWeaponBlueprintConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "cells");
            EditorGUILayout.Space(6f);
            ForgingShapeGridDrawer.Draw(
                serializedObject.FindProperty("width"),
                serializedObject.FindProperty("height"),
                serializedObject.FindProperty("cells"),
                false);
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(NewFPG.Forging.ForgingWorkbenchController))]
    public sealed class ForgingWorkbenchControllerEditor : UnityEditor.Editor
    {
        private bool includeGeneratedContent;
        private NewFPG.Forging.ForgingLayoutDrawerStateCapture drawerStateCapture =
            NewFPG.Forging.ForgingLayoutDrawerStateCapture.Auto;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            NewFPG.Forging.ForgingWorkbenchController controller =
                (NewFPG.Forging.ForgingWorkbenchController)target;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Runtime Layout Tools", EditorStyles.boldLabel);

            if (controller.LayoutPreset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a ForgingUILayoutPreset before saving runtime layout changes.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Rebuild UI Preview"))
                {
                    controller.RebuildRuntimeUiPreview();
                    EditorUtility.SetDirty(controller);
                }
            }

            includeGeneratedContent = EditorGUILayout.ToggleLeft(
                "Include generated board cells/previews",
                includeGeneratedContent);
            drawerStateCapture = (NewFPG.Forging.ForgingLayoutDrawerStateCapture)EditorGUILayout.EnumPopup(
                "Save Drawer State As",
                drawerStateCapture);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || controller.LayoutPreset == null))
            {
                if (GUILayout.Button("Save Current Runtime Layout To Preset"))
                {
                    if (controller.SaveCurrentRuntimeLayoutToPreset(includeGeneratedContent, drawerStateCapture))
                    {
                        Debug.Log("[ForgingWorkbenchControllerEditor] Saved current runtime UI layout to preset.");
                    }
                    else
                    {
                        Debug.LogWarning("[ForgingWorkbenchControllerEditor] Could not save runtime UI layout.");
                    }
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode, move ForgingRuntimeUI child RectTransforms, then press Save Current Runtime Layout To Preset before exiting Play Mode.",
                    MessageType.Info);
            }
        }
    }

    internal static class ForgingShapeGridDrawer
    {
        private const float CellSize = 26f;
        private const float Gap = 3f;

        public static void Draw(
            SerializedProperty widthProperty,
            SerializedProperty heightProperty,
            SerializedProperty cellsProperty,
            bool requireOneCell)
        {
            if (widthProperty == null || heightProperty == null || cellsProperty == null)
            {
                EditorGUILayout.HelpBox("Missing grid properties.", MessageType.Warning);
                return;
            }

            Normalize(widthProperty, heightProperty, cellsProperty, requireOneCell);
            EditorGUILayout.LabelField("Grid Shape", EditorStyles.boldLabel);

            int width = Mathf.Max(1, widthProperty.intValue);
            int height = Mathf.Max(1, heightProperty.intValue);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedWidth = CellSize,
                fixedHeight = CellSize,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
            };

            for (int y = 0; y < height; y++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    for (int x = 0; x < width; x++)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        bool active = Contains(cellsProperty, cell);
                        Color previousColor = GUI.backgroundColor;
                        GUI.backgroundColor = active
                            ? new Color(0.95f, 0.62f, 0.22f, 1f)
                            : new Color(0.22f, 0.18f, 0.14f, 1f);

                        if (GUILayout.Button(active ? "X" : string.Empty, buttonStyle))
                        {
                            Toggle(cellsProperty, cell, requireOneCell);
                        }

                        GUI.backgroundColor = previousColor;
                        GUILayout.Space(Gap);
                    }
                }

                GUILayout.Space(Gap);
            }

            EditorGUILayout.LabelField("Active Cells", cellsProperty.arraySize.ToString());
        }

        private static void Toggle(SerializedProperty cellsProperty, Vector2Int cell, bool requireOneCell)
        {
            int index = IndexOf(cellsProperty, cell);
            if (index >= 0)
            {
                if (requireOneCell && cellsProperty.arraySize == 1)
                {
                    return;
                }

                cellsProperty.DeleteArrayElementAtIndex(index);
                return;
            }

            int nextIndex = cellsProperty.arraySize;
            cellsProperty.InsertArrayElementAtIndex(nextIndex);
            cellsProperty.GetArrayElementAtIndex(nextIndex).vector2IntValue = cell;
        }

        private static void Normalize(
            SerializedProperty widthProperty,
            SerializedProperty heightProperty,
            SerializedProperty cellsProperty,
            bool requireOneCell)
        {
            widthProperty.intValue = Mathf.Max(1, widthProperty.intValue);
            heightProperty.intValue = Mathf.Max(1, heightProperty.intValue);

            for (int i = cellsProperty.arraySize - 1; i >= 0; i--)
            {
                Vector2Int cell = cellsProperty.GetArrayElementAtIndex(i).vector2IntValue;
                if (cell.x < 0
                    || cell.y < 0
                    || cell.x >= widthProperty.intValue
                    || cell.y >= heightProperty.intValue
                    || IndexOf(cellsProperty, cell, i + 1) >= 0)
                {
                    cellsProperty.DeleteArrayElementAtIndex(i);
                }
            }

            if (requireOneCell && cellsProperty.arraySize == 0)
            {
                cellsProperty.InsertArrayElementAtIndex(0);
                cellsProperty.GetArrayElementAtIndex(0).vector2IntValue = Vector2Int.zero;
            }
        }

        private static bool Contains(SerializedProperty cellsProperty, Vector2Int cell)
        {
            return IndexOf(cellsProperty, cell) >= 0;
        }

        private static int IndexOf(SerializedProperty cellsProperty, Vector2Int cell, int startIndex = 0)
        {
            for (int i = Mathf.Max(0, startIndex); i < cellsProperty.arraySize; i++)
            {
                if (cellsProperty.GetArrayElementAtIndex(i).vector2IntValue == cell)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
