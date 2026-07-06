using NewFPG.Combat;
using NewFPG.Combat.SkillIndicators;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    public static class WeaponDefinitionGeometryMigrationUtility
    {
        [MenuItem("NewFPG/Combat/Migrate Weapon Indicator Geometry To WeaponDefinitions")]
        public static void MigrateAllWeaponDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:WeaponDefinition");
            int migrated = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (MigrateWeaponDefinition(weapon))
                {
                    migrated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Migrated weapon indicator geometry into WeaponDefinition assets. Count=" + migrated);
        }

        public static bool MigrateWeaponDefinition(WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            SerializedObject serializedWeapon = new SerializedObject(weapon);
            SerializedProperty legacyProperty = serializedWeapon.FindProperty("indicatorConfig");
            SkillIndicatorConfig legacyConfig = legacyProperty != null
                ? legacyProperty.objectReferenceValue as SkillIndicatorConfig
                : null;
            if (legacyConfig == null)
            {
                return false;
            }

            WeaponRuntimeStats stats = WeaponRuntimeResolver.Resolve(weapon);
#pragma warning disable CS0618
            SkillIndicatorResolvedConfig resolved = SkillIndicatorResolvedConfig.From(legacyConfig, stats);
#pragma warning restore CS0618

            serializedWeapon.FindProperty("range").floatValue = legacyConfig.Range > 0f ? legacyConfig.Range : resolved.range;
            serializedWeapon.FindProperty("radius").floatValue = legacyConfig.Radius > 0f ? legacyConfig.Radius : resolved.radius;
            serializedWeapon.FindProperty("shapeType").enumValueIndex = (int)legacyConfig.ShapeType;
            serializedWeapon.FindProperty("width").floatValue = legacyConfig.Width > 0f ? legacyConfig.Width : resolved.width;
            serializedWeapon.FindProperty("length").floatValue = legacyConfig.Length > 0f ? legacyConfig.Length : resolved.length;
            serializedWeapon.FindProperty("angle").floatValue = resolved.angle;
            serializedWeapon.FindProperty("height").floatValue = resolved.height;
            serializedWeapon.FindProperty("groundOffset").floatValue = resolved.groundOffset;
            serializedWeapon.FindProperty("inputMode").enumValueIndex = (int)legacyConfig.InputMode;
            serializedWeapon.FindProperty("tapPolicy").enumValueIndex = (int)legacyConfig.TapPolicy;
            serializedWeapon.FindProperty("holdPolicy").enumValueIndex = (int)legacyConfig.HoldPolicy;
            serializedWeapon.FindProperty("invalidReleasePolicy").enumValueIndex = (int)legacyConfig.InvalidReleasePolicy;
            serializedWeapon.FindProperty("aimSource").enumValueIndex = (int)legacyConfig.AimSource;
            serializedWeapon.FindProperty("requireSurfaceHit").boolValue = legacyConfig.RequireSurfaceHit;
            serializedWeapon.FindProperty("clampToRange").boolValue = legacyConfig.ClampToRange;
            serializedWeapon.FindProperty("placementMode").enumValueIndex = (int)legacyConfig.PlacementMode;
            serializedWeapon.FindProperty("surfaceMask").intValue = legacyConfig.SurfaceMask.value;
            serializedWeapon.FindProperty("collisionMask").intValue = legacyConfig.CollisionMask.value;
            serializedWeapon.FindProperty("tapMaxDuration").floatValue = legacyConfig.TapMaxDuration;
            serializedWeapon.FindProperty("holdEnterDelay").floatValue = legacyConfig.HoldEnterDelay;
            serializedWeapon.FindProperty("castDelay").floatValue = legacyConfig.CastDelay;
            serializedWeapon.FindProperty("warningTime").floatValue = legacyConfig.WarningTime;
            serializedWeapon.FindProperty("duration").floatValue = legacyConfig.Duration;
            serializedWeapon.FindProperty("fadeOut").floatValue = legacyConfig.FadeOut;
            serializedWeapon.FindProperty("previewPrefabResourceId").stringValue = legacyConfig.PreviewPrefabResourceId;
            serializedWeapon.FindProperty("validMaterialResourceId").stringValue = legacyConfig.ValidMaterialResourceId;
            serializedWeapon.FindProperty("invalidMaterialResourceId").stringValue = legacyConfig.InvalidMaterialResourceId;
            serializedWeapon.FindProperty("confirmAudioResourceId").stringValue = legacyConfig.ConfirmAudioResourceId;
            serializedWeapon.FindProperty("invalidAudioResourceId").stringValue = legacyConfig.InvalidAudioResourceId;
            serializedWeapon.FindProperty("debugDraw").boolValue = legacyConfig.DebugDraw;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            return true;
        }
    }
}
