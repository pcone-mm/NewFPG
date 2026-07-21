using NewFPG.Prototype;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(PrototypeFirstPersonWeaponView))]
public sealed class PrototypeFirstPersonWeaponViewEditor : Editor
{
    private SerializedProperty interactionConfigProperty;
    private SerializedProperty layoutProfileProperty;
    private SerializedProperty legacyWeaponsProperty;
    private ReorderableList weaponsList;
    private FirstPersonWeaponLayoutProfile cachedLayoutProfile;
    private SerializedObject layoutSerializedObject;
    private SerializedProperty layoutWeaponsProperty;
    private Editor interactionConfigEditor;
    private bool showInteractionConfig = true;

    private void OnEnable()
    {
        interactionConfigProperty = serializedObject.FindProperty("interactionConfig");
        layoutProfileProperty = serializedObject.FindProperty("layoutProfile");
        legacyWeaponsProperty = serializedObject.FindProperty("weapons");
        RebuildWeaponsList();
    }

    private void OnDisable()
    {
        if (interactionConfigEditor != null)
        {
            DestroyImmediate(interactionConfigEditor);
        }
    }

    public override void OnInspectorGUI()
    {
        SyncTargetsFromScene();
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject, "m_Script", "interactionConfig", "layoutProfile", "weapons");
        EditorGUILayout.PropertyField(interactionConfigProperty);
        EditorGUILayout.PropertyField(layoutProfileProperty);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild Preview"))
            {
                RebuildTargets();
            }

            if (GUILayout.Button("Frame Scene Handles"))
            {
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        bool viewChanged = EditorGUI.EndChangeCheck();
        if (viewChanged)
        {
            serializedObject.ApplyModifiedProperties();
            RebuildWeaponsList();
            RebuildTargets();
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }

        DrawLayoutProfileInspector();
        DrawInteractionConfigInspector();
    }

    private void OnSceneGUI()
    {
        PrototypeFirstPersonWeaponView view = (PrototypeFirstPersonWeaponView)target;
        view.SyncWeaponPosesFromScene();
        Transform root = view.transform;

        SerializedObject layoutObject = CreateLayoutSerializedObject(view);
        SerializedProperty layoutWeapons = layoutObject.FindProperty("weapons");
        layoutObject.Update();

        bool changed = false;
        for (int i = 0; i < layoutWeapons.arraySize; i++)
        {
            SerializedProperty weapon = layoutWeapons.GetArrayElementAtIndex(i);
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
                Undo.RecordObject(layoutObject.targetObject, "Edit First Person Weapon Pose");
                localPosition.vector3Value = root.InverseTransformPoint(newWorldPosition);
                localEulerAngles.vector3Value = (Quaternion.Inverse(root.rotation) * newWorldRotation).eulerAngles;
                width.floatValue = Mathf.Max(0.01f, newWidth);
                changed = true;
            }
        }

        if (changed)
        {
            layoutObject.ApplyModifiedProperties();
            view.RebuildWeapons();
            EditorUtility.SetDirty(layoutObject.targetObject);
            if (layoutObject.targetObject == view)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }

            SceneView.RepaintAll();
        }
        else
        {
            layoutObject.ApplyModifiedProperties();
        }
    }

    private void DrawWeaponElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty weapon = weaponsList.serializedProperty.GetArrayElementAtIndex(index);
        SerializedProperty name = weapon.FindPropertyRelative("name");
        SerializedProperty localPosition = weapon.FindPropertyRelative("localPosition");
        SerializedProperty localEulerAngles = weapon.FindPropertyRelative("localEulerAngles");
        SerializedProperty width = weapon.FindPropertyRelative("width");
        SerializedProperty sortingOrder = weapon.FindPropertyRelative("sortingOrder");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float y = rect.y + 2f;
        Rect line = new Rect(rect.x, y, rect.width, lineHeight);

        EditorGUI.PropertyField(line, name);
        y += lineHeight + 2f;

        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sortingOrder);
        y += lineHeight + 2f;

        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), localPosition);
        y += lineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), localEulerAngles);
        y += lineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), width);
    }

    private void DrawLayoutProfileInspector()
    {
        FirstPersonWeaponLayoutProfile profile = layoutProfileProperty.objectReferenceValue as FirstPersonWeaponLayoutProfile;
        if (profile == null)
        {
            EditorGUILayout.HelpBox("Assign a layout profile to make HUD weapon positions reusable and editable by artists.", MessageType.Info);
            if (GUILayout.Button("Create/Assign Default Layout"))
            {
                AssignDefaultLayoutProfile();
            }

            return;
        }

        EnsureLayoutList(profile);
        layoutSerializedObject.Update();
        EditorGUI.BeginChangeCheck();
        weaponsList.DoLayoutList();
        if (EditorGUI.EndChangeCheck())
        {
            layoutSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            RebuildTargets();
            SceneView.RepaintAll();
        }
        else
        {
            layoutSerializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawInteractionConfigInspector()
    {
        Object config = interactionConfigProperty.objectReferenceValue;
        if (config == null)
        {
            EditorGUILayout.HelpBox("Assign an interaction config asset to enable Play Mode hover and attack tuning.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4f);
        showInteractionConfig = EditorGUILayout.Foldout(showInteractionConfig, "Interaction Config", true);
        if (!showInteractionConfig)
        {
            return;
        }

        EditorGUI.indentLevel++;
        CreateCachedEditor(config, null, ref interactionConfigEditor);
        interactionConfigEditor.OnInspectorGUI();
        EditorGUI.indentLevel--;
    }

    private void AddWeapon(ReorderableList list)
    {
        SerializedProperty property = list.serializedProperty;
        property.serializedObject.Update();
        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);

        SerializedProperty weapon = property.GetArrayElementAtIndex(index);
        weapon.FindPropertyRelative("name").stringValue = "Weapon " + (index + 1);
        weapon.FindPropertyRelative("localPosition").vector3Value = new Vector3(0f, -0.35f, 1.35f);
        weapon.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        weapon.FindPropertyRelative("width").floatValue = 0.75f;
        weapon.FindPropertyRelative("sortingOrder").intValue = index;

        property.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(property.serializedObject.targetObject);
        RebuildTargets();
    }

    private void RemoveWeapon(ReorderableList list)
    {
        list.serializedProperty.serializedObject.Update();
        ReorderableList.defaultBehaviours.DoRemoveButton(list);
        list.serializedProperty.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(list.serializedProperty.serializedObject.targetObject);
        RebuildTargets();
    }

    private void RebuildWeaponsList()
    {
        FirstPersonWeaponLayoutProfile profile = layoutProfileProperty != null
            ? layoutProfileProperty.objectReferenceValue as FirstPersonWeaponLayoutProfile
            : null;

        if (profile != null)
        {
            EnsureLayoutList(profile);
            return;
        }

        cachedLayoutProfile = null;
        layoutSerializedObject = null;
        layoutWeaponsProperty = null;
        weaponsList = CreateWeaponsList(legacyWeaponsProperty);
    }

    private void EnsureLayoutList(FirstPersonWeaponLayoutProfile profile)
    {
        if (profile == cachedLayoutProfile && layoutSerializedObject != null && weaponsList != null)
        {
            return;
        }

        cachedLayoutProfile = profile;
        layoutSerializedObject = new SerializedObject(profile);
        layoutWeaponsProperty = layoutSerializedObject.FindProperty("weapons");
        weaponsList = CreateWeaponsList(layoutWeaponsProperty);
    }

    private ReorderableList CreateWeaponsList(SerializedProperty weaponsProperty)
    {
        return new ReorderableList(weaponsProperty.serializedObject, weaponsProperty, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "HUD Weapon Layout"),
            elementHeightCallback = _ => (EditorGUIUtility.singleLineHeight + 2f) * 5f + 6f,
            drawElementCallback = DrawWeaponElement,
            onAddCallback = AddWeapon,
            onRemoveCallback = RemoveWeapon,
        };
    }

    private SerializedObject CreateLayoutSerializedObject(PrototypeFirstPersonWeaponView view)
    {
        if (view.LayoutProfile != null)
        {
            return new SerializedObject(view.LayoutProfile);
        }

        return new SerializedObject(view);
    }

    private void AssignDefaultLayoutProfile()
    {
        FirstPersonWeaponLayoutProfile profile = AssetDatabase.LoadAssetAtPath<FirstPersonWeaponLayoutProfile>(
            FirstPersonWeaponLayoutProfile.DefaultAssetPath);
        if (profile == null)
        {
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Settings/Prototype");
            profile = CreateInstance<FirstPersonWeaponLayoutProfile>();
            profile.ResetToDefaultLayout();
            AssetDatabase.CreateAsset(profile, FirstPersonWeaponLayoutProfile.DefaultAssetPath);
            AssetDatabase.SaveAssets();
        }

        serializedObject.Update();
        layoutProfileProperty.objectReferenceValue = profile;
        serializedObject.ApplyModifiedProperties();
        RebuildWeaponsList();
        RebuildTargets();
        EditorGUIUtility.PingObject(profile);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
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

    private void SyncTargetsFromScene()
    {
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is PrototypeFirstPersonWeaponView view)
            {
                view.SyncWeaponPosesFromScene();
            }
        }
    }
}
