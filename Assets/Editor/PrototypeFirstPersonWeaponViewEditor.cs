using NewFPG.Prototype;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(PrototypeFirstPersonWeaponView))]
public sealed class PrototypeFirstPersonWeaponViewEditor : Editor
{
    private SerializedProperty weaponsProperty;
    private ReorderableList weaponsList;

    private void OnEnable()
    {
        weaponsProperty = serializedObject.FindProperty("weapons");
        weaponsList = new ReorderableList(serializedObject, weaponsProperty, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "HUD Weapons"),
            elementHeightCallback = _ => (EditorGUIUtility.singleLineHeight + 2f) * 5f + 6f,
            drawElementCallback = DrawWeaponElement,
            onAddCallback = AddWeapon,
            onRemoveCallback = RemoveWeapon,
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject, "m_Script", "weapons");
        weaponsList.DoLayoutList();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild"))
            {
                RebuildTargets();
            }

            if (GUILayout.Button("Frame Scene Handles"))
            {
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            RebuildTargets();
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void OnSceneGUI()
    {
        PrototypeFirstPersonWeaponView view = (PrototypeFirstPersonWeaponView)target;
        Transform root = view.transform;

        serializedObject.Update();

        bool changed = false;
        for (int i = 0; i < weaponsProperty.arraySize; i++)
        {
            SerializedProperty weapon = weaponsProperty.GetArrayElementAtIndex(i);
            SerializedProperty name = weapon.FindPropertyRelative("name");
            SerializedProperty localPosition = weapon.FindPropertyRelative("localPosition");
            SerializedProperty localEulerAngles = weapon.FindPropertyRelative("localEulerAngles");
            SerializedProperty width = weapon.FindPropertyRelative("width");

            Vector3 worldPosition = root.TransformPoint(localPosition.vector3Value);
            Quaternion worldRotation = root.rotation * Quaternion.Euler(localEulerAngles.vector3Value);
            float handleSize = HandleUtility.GetHandleSize(worldPosition);

            Handles.color = Color.Lerp(Color.yellow, Color.cyan, i % 2);
            Handles.Label(worldPosition + Vector3.up * handleSize * 0.15f, string.IsNullOrWhiteSpace(name.stringValue) ? "Weapon " + i : name.stringValue);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, worldRotation);
            Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, worldPosition);
            float newWidth = Handles.ScaleValueHandle(
                width.floatValue,
                worldPosition + (worldRotation * Vector3.right) * handleSize * 0.6f,
                worldRotation,
                handleSize * 0.15f,
                Handles.CubeHandleCap,
                0.025f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(view, "Edit First Person Weapon Pose");
                localPosition.vector3Value = root.InverseTransformPoint(newWorldPosition);
                localEulerAngles.vector3Value = (Quaternion.Inverse(root.rotation) * newWorldRotation).eulerAngles;
                width.floatValue = Mathf.Max(0.01f, newWidth);
                changed = true;
            }
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            view.RebuildWeapons();
            EditorUtility.SetDirty(view);
            SceneView.RepaintAll();
        }
    }

    private void DrawWeaponElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty weapon = weaponsProperty.GetArrayElementAtIndex(index);
        SerializedProperty name = weapon.FindPropertyRelative("name");
        SerializedProperty textureIndex = weapon.FindPropertyRelative("textureIndex");
        SerializedProperty localPosition = weapon.FindPropertyRelative("localPosition");
        SerializedProperty localEulerAngles = weapon.FindPropertyRelative("localEulerAngles");
        SerializedProperty width = weapon.FindPropertyRelative("width");
        SerializedProperty sortingOrder = weapon.FindPropertyRelative("sortingOrder");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float y = rect.y + 2f;
        Rect line = new Rect(rect.x, y, rect.width, lineHeight);

        EditorGUI.PropertyField(line, name);
        y += lineHeight + 2f;

        float halfWidth = (rect.width - 8f) * 0.5f;
        EditorGUI.PropertyField(new Rect(rect.x, y, halfWidth, lineHeight), textureIndex);
        EditorGUI.PropertyField(new Rect(rect.x + halfWidth + 8f, y, halfWidth, lineHeight), sortingOrder);
        y += lineHeight + 2f;

        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), localPosition);
        y += lineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), localEulerAngles);
        y += lineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), width);
    }

    private void AddWeapon(ReorderableList list)
    {
        int index = weaponsProperty.arraySize;
        weaponsProperty.InsertArrayElementAtIndex(index);

        SerializedProperty weapon = weaponsProperty.GetArrayElementAtIndex(index);
        weapon.FindPropertyRelative("name").stringValue = "Weapon " + (index + 1);
        weapon.FindPropertyRelative("textureIndex").intValue = 0;
        weapon.FindPropertyRelative("localPosition").vector3Value = new Vector3(0f, -0.35f, 1.35f);
        weapon.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        weapon.FindPropertyRelative("width").floatValue = 0.75f;
        weapon.FindPropertyRelative("sortingOrder").intValue = index;

        serializedObject.ApplyModifiedProperties();
        RebuildTargets();
    }

    private void RemoveWeapon(ReorderableList list)
    {
        ReorderableList.defaultBehaviours.DoRemoveButton(list);
        serializedObject.ApplyModifiedProperties();
        RebuildTargets();
    }

    private void RebuildTargets()
    {
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is PrototypeFirstPersonWeaponView view)
            {
                view.RebuildWeapons();
                EditorUtility.SetDirty(view);
            }
        }
    }
}
