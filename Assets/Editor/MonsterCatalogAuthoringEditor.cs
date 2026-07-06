using NewFPG.Monsters;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    [CustomEditor(typeof(MonsterCatalogAuthoring))]
    internal sealed class MonsterCatalogAuthoringEditor : UnityEditor.Editor
    {
        private SerializedProperty catalog;

        private void OnEnable()
        {
            catalog = serializedObject.FindProperty("catalog");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            MonsterCatalogAuthoring authoring = (MonsterCatalogAuthoring)target;
            int monsterCount = authoring.Catalog != null && authoring.Catalog.monsters != null
                ? authoring.Catalog.monsters.Count
                : 0;

            MonsterCatalogInspectorGui.DrawInspectorTitle(
                "怪物配置编辑入口",
                "ScriptableObject 镜像；运行时读取导出的 monster_catalog.json。",
                MonsterCatalogInspectorGui.CatalogColor);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("怪物数量", monsterCount.ToString(), EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(6f);

            EditorGUILayout.PropertyField(catalog, true);
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(MonsterCatalog))]
    internal sealed class MonsterCatalogDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property)
        {
            SerializedProperty version = property.FindPropertyRelative("version");
            return string.IsNullOrWhiteSpace(version.stringValue)
                ? "怪物配置表"
                : "怪物配置表 - " + version.stringValue;
        }

        protected override Color AccentColor => MonsterCatalogInspectorGui.CatalogColor;
        protected override string[] FieldNames => new[] { "version", "source", "designerNote", "monsters" };
    }

    [CustomPropertyDrawer(typeof(MonsterDefinition))]
    internal sealed class MonsterDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property)
        {
            string displayName = property.FindPropertyRelative("displayName").stringValue;
            string monsterId = property.FindPropertyRelative("monsterId").stringValue;

            if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(monsterId))
            {
                return displayName + " - " + monsterId;
            }

            return string.IsNullOrWhiteSpace(monsterId) ? "怪物" : "怪物 - " + monsterId;
        }

        protected override Color AccentColor => MonsterCatalogInspectorGui.MonsterColor;

        protected override string[] FieldNames => new[]
        {
            "monsterId",
            "displayName",
            "designerNote",
            "prefabPath",
            "movement",
            "vitals",
            "attack",
            "ai",
            "skills",
            "presentation",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterMovementDefinition))]
    internal sealed class MonsterMovementDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property) => "移动";
        protected override Color AccentColor => MonsterCatalogInspectorGui.MovementColor;

        protected override string[] FieldNames => new[]
        {
            "designerNote",
            "movementEnabled",
            "moveSpeed",
            "acceleration",
            "deceleration",
            "autoFindTargetByTag",
            "targetTag",
            "detectionRadius",
            "stoppingDistance",
            "navMeshAgentRadius",
            "navMeshAgentHeight",
            "navMeshAgentAngularSpeed",
            "navMeshAgentBaseOffset",
            "navMeshAreaMask",
            "navMeshSampleDistance",
            "visibilitySampleHeight",
            "visiblePositionLineOfSightMask",
            "visiblePositionOccupancyMask",
            "visiblePositionSampleAttempts",
            "visiblePositionOccupancyRadius",
            "nearZoneGroup",
            "midZoneGroup",
            "farZoneGroup",
            "leftZoneGroup",
            "centerZoneGroup",
            "rightZoneGroup",
            "targetRefreshInterval",
            "patrolWhenNoTarget",
            "patrolRadius",
            "patrolPointTolerance",
            "patrolPauseDuration",
            "flipSpriteWithHorizontalMovement",
            "spriteFacesRightByDefault",
            "autoConfigureCollider",
            "colliderWidthScale",
            "colliderHeightScale",
            "colliderDepth",
            "moveXParameter",
            "moveZParameter",
            "speedParameter",
            "isMovingParameter",
            "movementStateParameter",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterVitalsDefinition))]
    internal sealed class MonsterVitalsDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property) => "生命";
        protected override Color AccentColor => MonsterCatalogInspectorGui.VitalsColor;

        protected override string[] FieldNames => new[]
        {
            "designerNote",
            "maxHealth",
            "startingHealth",
            "maxShield",
            "startingShield",
            "destroyOnDeath",
            "deathDelay",
            "hitTriggerParameter",
            "hitTint",
            "hitTintSeconds",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterAttackDefinition))]
    internal sealed class MonsterAttackDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property) => "旧攻击兼容";
        protected override Color AccentColor => MonsterCatalogInspectorGui.AttackColor;

        protected override string[] FieldNames => new[]
        {
            "designerNote",
            "autoFindPlayer",
            "playerTag",
            "attackRange",
            "requestInterval",
            "attackPrepareTime",
            "damage",
            "damageRadius",
            "warningHeightOffset",
            "targetMask",
            "attackTriggerParameter",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterAiDefinition))]
    internal sealed class MonsterAiDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property) => "行为树";
        protected override Color AccentColor => MonsterCatalogInspectorGui.AiColor;

        protected override string[] FieldNames => new[]
        {
            "designerNote",
            "enabled",
            "behaviorTreePath",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterSkillDefinition))]
    internal sealed class MonsterSkillDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property)
        {
            string displayName = property.FindPropertyRelative("displayName").stringValue;
            string skillId = property.FindPropertyRelative("skillId").stringValue;

            if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(skillId))
            {
                return displayName + " - " + skillId;
            }

            return string.IsNullOrWhiteSpace(skillId) ? "技能" : "技能 - " + skillId;
        }

        protected override Color AccentColor => MonsterCatalogInspectorGui.SkillColor;

        protected override string[] FieldNames => new[]
        {
            "skillId",
            "displayName",
            "designerNote",
            "cooldown",
            "windup",
            "activeDuration",
            "recovery",
            "castRange",
            "requireLineOfSight",
            "lineOfSightMask",
            "lineOfSightHeightOffset",
            "stopMovementDuringCast",
            "animationTriggerParameter",
            "showWarning",
            "warningHeightOffset",
            "mechanics",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterMechanicDefinition))]
    internal sealed class MonsterMechanicDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property)
        {
            string mechanicId = property.FindPropertyRelative("mechanicId").stringValue;
            string type = property.FindPropertyRelative("type").stringValue;

            if (!string.IsNullOrWhiteSpace(mechanicId) && !string.IsNullOrWhiteSpace(type))
            {
                return mechanicId + " - " + type;
            }

            return string.IsNullOrWhiteSpace(type) ? "机制" : "机制 - " + type;
        }

        protected override Color AccentColor => MonsterCatalogInspectorGui.MechanicColor;

        protected override string[] FieldNames => new[]
        {
            "mechanicId",
            "type",
            "designerNote",
            "delay",
            "duration",
            "value",
            "radius",
            "heightOffset",
            "targetMask",
            "affectSelf",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterPresentationDefinition))]
    internal sealed class MonsterPresentationDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property) => "表现";
        protected override Color AccentColor => MonsterCatalogInspectorGui.PresentationColor;

        protected override string[] FieldNames => new[]
        {
            "designerNote",
            "attackTriggerParameter",
            "hitTriggerParameter",
            "warningHeightOffset",
        };
    }

    [CustomPropertyDrawer(typeof(MonsterBattleZoneGroupDefinition))]
    internal sealed class MonsterBattleZoneGroupDefinitionDrawer : MonsterCatalogSectionDrawer
    {
        protected override string Title(SerializedProperty property)
        {
            string groupId = property.FindPropertyRelative("groupId").stringValue;
            return string.IsNullOrWhiteSpace(groupId) ? "战斗区域组" : "战斗区域组 - " + groupId;
        }

        protected override Color AccentColor => MonsterCatalogInspectorGui.AiColor;

        protected override string[] FieldNames => new[]
        {
            "designerNote",
            "groupId",
            "zoneIds",
        };
    }

    internal abstract class MonsterCatalogSectionDrawer : PropertyDrawer
    {
        protected abstract string Title(SerializedProperty property);
        protected abstract Color AccentColor { get; }
        protected abstract string[] FieldNames { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            MonsterCatalogInspectorGui.DrawSection(position, property, Title(property), AccentColor, FieldNames);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return MonsterCatalogInspectorGui.GetSectionHeight(property, FieldNames);
        }
    }

    internal static class MonsterCatalogInspectorGui
    {
        public static readonly Color CatalogColor = ColorFor(0.25f, 0.48f, 0.72f);
        public static readonly Color MonsterColor = ColorFor(0.27f, 0.64f, 0.58f);
        public static readonly Color MovementColor = ColorFor(0.30f, 0.58f, 0.28f);
        public static readonly Color VitalsColor = ColorFor(0.78f, 0.32f, 0.30f);
        public static readonly Color AttackColor = ColorFor(0.82f, 0.52f, 0.24f);
        public static readonly Color AiColor = ColorFor(0.48f, 0.42f, 0.78f);
        public static readonly Color SkillColor = ColorFor(0.74f, 0.44f, 0.76f);
        public static readonly Color MechanicColor = ColorFor(0.76f, 0.38f, 0.48f);
        public static readonly Color PresentationColor = ColorFor(0.42f, 0.56f, 0.70f);

        private const float Padding = 6f;
        private const float Spacing = 3f;
        private const float HeaderExtraHeight = 6f;

        public static void DrawInspectorTitle(string title, string subtitle, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
            DrawPanel(rect, color, 0.28f);

            Rect accent = new Rect(rect.x, rect.y, 4f, rect.height);
            EditorGUI.DrawRect(accent, Opaque(color));

            Rect titleRect = new Rect(rect.x + 12f, rect.y + 7f, rect.width - 18f, EditorGUIUtility.singleLineHeight);
            GUI.Label(titleRect, title, EditorStyles.boldLabel);

            Rect subtitleRect = new Rect(titleRect.x, titleRect.yMax + 4f, titleRect.width, EditorGUIUtility.singleLineHeight);
            GUI.Label(subtitleRect, subtitle, EditorStyles.miniLabel);
        }

        public static void DrawSection(Rect position, SerializedProperty property, string title, Color color, string[] fieldNames)
        {
            Rect fullRect = new Rect(position.x, position.y, position.width, GetSectionHeight(property, fieldNames));
            DrawPanel(fullRect, color, 0.14f);

            Rect headerRect = new Rect(
                fullRect.x + Padding,
                fullRect.y + Padding,
                fullRect.width - Padding * 2f,
                EditorGUIUtility.singleLineHeight + HeaderExtraHeight);
            DrawPanel(headerRect, color, 0.32f);

            Rect foldoutRect = new Rect(
                headerRect.x + 4f,
                headerRect.y + 3f,
                headerRect.width - 8f,
                EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true, EditorStyles.foldout);

            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            float y = headerRect.yMax + Spacing;
            for (int i = 0; i < fieldNames.Length; i++)
            {
                SerializedProperty child = property.FindPropertyRelative(fieldNames[i]);
                if (child == null)
                {
                    continue;
                }

                float height = EditorGUI.GetPropertyHeight(child, true);
                Rect childRect = new Rect(
                    fullRect.x + Padding,
                    y,
                    fullRect.width - Padding * 2f,
                    height);
                EditorGUI.PropertyField(childRect, child, true);
                y += height + Spacing;
            }

            EditorGUI.indentLevel--;
        }

        public static float GetSectionHeight(SerializedProperty property, string[] fieldNames)
        {
            float height = Padding + EditorGUIUtility.singleLineHeight + HeaderExtraHeight + Padding;
            if (!property.isExpanded)
            {
                return height;
            }

            for (int i = 0; i < fieldNames.Length; i++)
            {
                SerializedProperty child = property.FindPropertyRelative(fieldNames[i]);
                if (child != null)
                {
                    height += EditorGUI.GetPropertyHeight(child, true) + Spacing;
                }
            }

            return height + Padding;
        }

        private static void DrawPanel(Rect rect, Color color, float alpha)
        {
            Color background = color;
            background.a = EditorGUIUtility.isProSkin ? alpha : Mathf.Min(0.36f, alpha + 0.08f);
            EditorGUI.DrawRect(rect, background);

            Rect border = new Rect(rect.x, rect.y, rect.width, 1f);
            EditorGUI.DrawRect(border, BorderColor(color));
        }

        private static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
        }

        private static Color BorderColor(Color color)
        {
            color.a = EditorGUIUtility.isProSkin ? 0.62f : 0.45f;
            return color;
        }

        private static Color ColorFor(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }
    }
}
