using NewFPG.Combat;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    [CustomEditor(typeof(WeaponDefinition))]
    public sealed class WeaponDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection("Identity", "weaponId", "displayName", "icon");
            DrawSection("Combat", "resourceCost", "damage", "cooldown", "range", "radius");
            DrawShapeSection();
            DrawSection(
                "Input And Aim",
                "inputMode",
                "tapPolicy",
                "holdPolicy",
                "invalidReleasePolicy",
                "aimSource",
                "requireSurfaceHit",
                "clampToRange",
                "placementMode",
                "surfaceMask",
                "collisionMask");
            DrawSection("Timing", "tapMaxDuration", "holdEnterDelay", "castDelay", "warningTime", "duration", "fadeOut");
            DrawSection(
                "Preview",
                "previewPrefabResourceId",
                "validMaterialResourceId",
                "invalidMaterialResourceId",
                "confirmAudioResourceId",
                "invalidAudioResourceId",
                "debugDraw");
            DrawSection("Effects", "releaseEffectPrefab", "hitEffectPrefab", "forgedRuntimeStats");
            DrawResolvedPreview();
            DrawLegacyMigrationSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSection(string title, params string[] propertyNames)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int i = 0; i < propertyNames.Length; i++)
            {
                DrawProperty(propertyNames[i]);
            }
        }

        private void DrawShapeSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cast Shape", EditorStyles.boldLabel);
            DrawProperty("shapeType");
            DrawProperty("range");
            DrawProperty("radius");

            SerializedProperty shapeType = serializedObject.FindProperty("shapeType");
            string shapeName = shapeType != null ? shapeType.enumNames[shapeType.enumValueIndex] : string.Empty;
            if (shapeName == "Line" || shapeName == "Rectangle")
            {
                DrawProperty("width");
                DrawProperty("length");
            }
            else if (shapeName == "Cone")
            {
                DrawProperty("length");
                DrawProperty("angle");
            }
            else
            {
                DrawProperty("width");
                DrawProperty("length");
                DrawProperty("angle");
            }

            DrawProperty("height");
            DrawProperty("groundOffset");
        }

        private void DrawResolvedPreview()
        {
            WeaponDefinition weapon = target as WeaponDefinition;
            WeaponRuntimeStats stats = WeaponRuntimeResolver.Resolve(weapon);
            if (stats == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resolved Cast Snapshot", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.EnumPopup("Shape", stats.ShapeType);
            EditorGUILayout.FloatField("Range", stats.Range);
            EditorGUILayout.FloatField("Radius", stats.Radius);
            EditorGUILayout.FloatField("Width", stats.Width);
            EditorGUILayout.FloatField("Length", stats.Length);
            EditorGUILayout.FloatField("Angle", stats.Angle);
            EditorGUILayout.EnumPopup("Aim Source", stats.AimSource);
            EditorGUILayout.EnumPopup("Placement", stats.PlacementMode);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawLegacyMigrationSection()
        {
            SerializedProperty legacy = serializedObject.FindProperty("indicatorConfig");
            if (legacy == null || legacy.objectReferenceValue == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Legacy Migration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(legacy);
            if (GUILayout.Button("Copy Legacy Indicator Values Into Weapon"))
            {
                serializedObject.ApplyModifiedProperties();
                WeaponDefinitionGeometryMigrationUtility.MigrateWeaponDefinition(target as WeaponDefinition);
                serializedObject.Update();
            }
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }
    }
}
