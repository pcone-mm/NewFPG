using NewFPG.Level;
using UnityEditor;
using UnityEngine;

namespace NewFPG.EditorTools
{
    [CustomEditor(typeof(LevelRouteTable))]
    internal sealed class LevelRouteTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "路线表只配置房间流程：起始房间、触发方式、房间选择和出口门。刷怪内容通过房间 encounterId 到刷怪表中查找。",
                MessageType.Info);
            LevelInspectorGui.DrawLayoutProperty(serializedObject.FindProperty("routeId"));
            LevelInspectorGui.DrawLayoutProperty(serializedObject.FindProperty("startRoomId"));
            LevelInspectorGui.DrawLayoutProperty(serializedObject.FindProperty("routeNote"));
            LevelInspectorGui.DrawLayoutProperty(serializedObject.FindProperty("rooms"));
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(LevelEncounterTable))]
    internal sealed class LevelEncounterTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "刷怪表只配置 encounter 的波次和每波选什么怪；何时触发由路线表房间的触发方式和完成方式决定。",
                MessageType.Info);
            LevelInspectorGui.DrawLayoutProperty(serializedObject.FindProperty("tableNote"));
            LevelInspectorGui.DrawLayoutProperty(serializedObject.FindProperty("encounters"));
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(LevelEncounterDefinition))]
    internal sealed class LevelEncounterDefinitionDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float HelpBoxHeight = 38f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty encounterId = property.FindPropertyRelative("encounterId");
            SerializedProperty waves = property.FindPropertyRelative("waves");

            Rect foldoutRect = LevelInspectorGui.LineRect(position, position.y);
            string title = string.IsNullOrWhiteSpace(encounterId.stringValue)
                ? "刷怪配置"
                : "刷怪配置 - " + encounterId.stringValue + "（" + waves.arraySize + " 波）";
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = foldoutRect.yMax + Spacing;
                y = LevelInspectorGui.DrawHelpBox(position, y, HelpBoxHeight, "备注：房间通过 Encounter ID 引用这里；波次会按列表顺序执行。");
                y = LevelInspectorGui.DrawProperty(position, y, encounterId);
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("encounterNote"));
                LevelInspectorGui.DrawProperty(position, y, waves);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight
                + Spacing
                + HelpBoxHeight
                + Spacing
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("encounterId"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("encounterNote"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("waves"));
        }
    }

    [CustomPropertyDrawer(typeof(LevelEncounterWave))]
    internal sealed class LevelEncounterWaveDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float HelpBoxHeight = 38f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty waveId = property.FindPropertyRelative("waveId");
            SerializedProperty selectionMode = property.FindPropertyRelative("selectionMode");

            Rect foldoutRect = LevelInspectorGui.LineRect(position, position.y);
            string title = string.IsNullOrWhiteSpace(waveId.stringValue)
                ? "波次"
                : "波次 - " + waveId.stringValue + " / " + LevelInspectorGui.EnumDisplayName(selectionMode);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = foldoutRect.yMax + Spacing;
                y = LevelInspectorGui.DrawHelpBox(position, y, HelpBoxHeight, "备注：本波只决定刷什么怪；触发时机由路线房间决定。当前波清空后才会进入下一波。");
                y = LevelInspectorGui.DrawProperty(position, y, waveId);
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("waveNote"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("delayAfterPreviousWave"));
                y = LevelInspectorGui.DrawProperty(position, y, selectionMode);

                if (selectionMode.enumValueIndex == (int)LevelSpawnSelectionMode.RandomPool)
                {
                    LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("randomPool"));
                }
                else
                {
                    LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("presetGroups"));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            SerializedProperty selectionMode = property.FindPropertyRelative("selectionMode");
            SerializedProperty selectedConfig = selectionMode.enumValueIndex == (int)LevelSpawnSelectionMode.RandomPool
                ? property.FindPropertyRelative("randomPool")
                : property.FindPropertyRelative("presetGroups");

            return EditorGUIUtility.singleLineHeight
                + Spacing
                + HelpBoxHeight
                + Spacing
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("waveId"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("waveNote"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("delayAfterPreviousWave"))
                + LevelInspectorGui.PropertyHeight(selectionMode)
                + LevelInspectorGui.PropertyHeight(selectedConfig);
        }
    }

    [CustomPropertyDrawer(typeof(LevelRoomDefinition))]
    internal sealed class LevelRoomDefinitionDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float HelpBoxHeight = 38f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty roomId = property.FindPropertyRelative("roomId");
            SerializedProperty displayName = property.FindPropertyRelative("displayName");
            Rect foldoutRect = LevelInspectorGui.LineRect(position, position.y);
            string title = string.IsNullOrWhiteSpace(roomId.stringValue)
                ? "房间"
                : "房间 - " + roomId.stringValue + (string.IsNullOrWhiteSpace(displayName.stringValue) ? string.Empty : " / " + displayName.stringValue);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = foldoutRect.yMax + Spacing;
                y = LevelInspectorGui.DrawHelpBox(position, y, HelpBoxHeight, "备注：触发方式控制是否等待交互；完成方式控制交互或选择后是结算、开战还是结束路线。");
                y = LevelInspectorGui.DrawProperty(position, y, roomId);
                y = LevelInspectorGui.DrawProperty(position, y, displayName);
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("roomType"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("rewardPool"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("triggerMode"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("completionMode"));

                SerializedProperty completionMode = property.FindPropertyRelative("completionMode");
                if (completionMode.enumValueIndex == (int)LevelRoomCompletionMode.StartEncounter)
                {
                    y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("encounterId"));
                }

                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("rewardPreview"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("roomNote"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("choices"));
                LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("exits"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            float height = EditorGUIUtility.singleLineHeight + Spacing + HelpBoxHeight + Spacing;
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("roomId"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("displayName"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("roomType"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("rewardPool"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("triggerMode"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("completionMode"));

            SerializedProperty completionMode = property.FindPropertyRelative("completionMode");
            if (completionMode.enumValueIndex == (int)LevelRoomCompletionMode.StartEncounter)
            {
                height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("encounterId"));
            }

            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("rewardPreview"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("roomNote"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("choices"));
            height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("exits"));
            return height;
        }
    }

    [CustomPropertyDrawer(typeof(LevelRoomChoiceDefinition))]
    internal sealed class LevelRoomChoiceDefinitionDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float HelpBoxHeight = 34f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty choiceId = property.FindPropertyRelative("choiceId");
            SerializedProperty displayName = property.FindPropertyRelative("displayName");
            Rect foldoutRect = LevelInspectorGui.LineRect(position, position.y);
            string title = string.IsNullOrWhiteSpace(choiceId.stringValue)
                ? "选择项"
                : "选择项 - " + choiceId.stringValue + (string.IsNullOrWhiteSpace(displayName.stringValue) ? string.Empty : " / " + displayName.stringValue);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = foldoutRect.yMax + Spacing;
                y = LevelInspectorGui.DrawHelpBox(position, y, HelpBoxHeight, "备注：选择项只修改奖励数值和可选 Encounter 覆盖；不会直接生成怪。");
                y = LevelInspectorGui.DrawProperty(position, y, choiceId);
                y = LevelInspectorGui.DrawProperty(position, y, displayName);
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("description"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("encounterIdOverride"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("damageBonus"));
                LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("goldDelta"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight
                + Spacing
                + HelpBoxHeight
                + Spacing
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("choiceId"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("displayName"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("description"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("encounterIdOverride"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("damageBonus"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("goldDelta"));
        }
    }

    [CustomPropertyDrawer(typeof(LevelDoorDefinition))]
    internal sealed class LevelDoorDefinitionDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float HelpBoxHeight = 34f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty targetRoomId = property.FindPropertyRelative("targetRoomId");
            SerializedProperty displayName = property.FindPropertyRelative("displayName");
            Rect foldoutRect = LevelInspectorGui.LineRect(position, position.y);
            string title = string.IsNullOrWhiteSpace(targetRoomId.stringValue)
                ? "出口门"
                : "出口门 - " + (string.IsNullOrWhiteSpace(displayName.stringValue) ? targetRoomId.stringValue : displayName.stringValue) + " -> " + targetRoomId.stringValue;
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = foldoutRect.yMax + Spacing;
                y = LevelInspectorGui.DrawHelpBox(position, y, HelpBoxHeight, "备注：门只负责跳转目标房间和展示预告；目标房间的真实逻辑看目标房间配置。");
                y = LevelInspectorGui.DrawProperty(position, y, targetRoomId);
                y = LevelInspectorGui.DrawProperty(position, y, displayName);
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("roomType"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("rewardPool"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("rewardPreview"));
                y = LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("canReroll"));
                LevelInspectorGui.DrawProperty(position, y, property.FindPropertyRelative("isRiskDoor"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight
                + Spacing
                + HelpBoxHeight
                + Spacing
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("targetRoomId"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("displayName"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("roomType"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("rewardPool"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("rewardPreview"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("canReroll"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("isRiskDoor"));
        }
    }

    [CustomPropertyDrawer(typeof(LevelSpawnEntry))]
    internal sealed class LevelSpawnEntryDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty monsterId = property.FindPropertyRelative("monsterId");
            SerializedProperty monsterPrefab = property.FindPropertyRelative("monsterPrefab");
            SerializedProperty count = property.FindPropertyRelative("count");
            SerializedProperty weight = property.FindPropertyRelative("weight");
            SerializedProperty overrideMaxHealth = property.FindPropertyRelative("overrideMaxHealth");
            SerializedProperty maxHealthOverride = property.FindPropertyRelative("maxHealthOverride");

            Rect foldoutRect = LevelInspectorGui.LineRect(position, position.y);
            string title = string.IsNullOrWhiteSpace(monsterId.stringValue)
                ? "怪物条目"
                : "怪物条目 - " + monsterId.stringValue;
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                bool isRandomPoolCandidate = IsRandomPoolCandidate(property);
                EditorGUI.indentLevel++;
                float y = foldoutRect.yMax + Spacing;
                y = LevelInspectorGui.DrawProperty(position, y, monsterId);
                y = LevelInspectorGui.DrawProperty(position, y, monsterPrefab);
                y = LevelInspectorGui.DrawProperty(position, y, isRandomPoolCandidate ? weight : count);
                y = LevelInspectorGui.DrawProperty(position, y, overrideMaxHealth);
                if (overrideMaxHealth.boolValue)
                {
                    LevelInspectorGui.DrawProperty(position, y, maxHealthOverride);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            SerializedProperty countOrWeight = IsRandomPoolCandidate(property)
                ? property.FindPropertyRelative("weight")
                : property.FindPropertyRelative("count");
            SerializedProperty overrideMaxHealth = property.FindPropertyRelative("overrideMaxHealth");

            float height = EditorGUIUtility.singleLineHeight
                + Spacing
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("monsterId"))
                + LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("monsterPrefab"))
                + LevelInspectorGui.PropertyHeight(countOrWeight)
                + LevelInspectorGui.PropertyHeight(overrideMaxHealth);

            if (overrideMaxHealth.boolValue)
            {
                height += LevelInspectorGui.PropertyHeight(property.FindPropertyRelative("maxHealthOverride"));
            }

            return height;
        }

        private static bool IsRandomPoolCandidate(SerializedProperty property)
        {
            return property.propertyPath.Contains("randomPool.candidates");
        }
    }

    internal static class LevelInspectorGui
    {
        private const float Spacing = 2f;

        public static Rect LineRect(Rect position, float y)
        {
            return new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        }

        public static float DrawHelpBox(Rect position, float y, float height, string message)
        {
            EditorGUI.HelpBox(new Rect(position.x, y, position.width, height), message, MessageType.None);
            return y + height + Spacing;
        }

        public static void DrawLayoutProperty(SerializedProperty property)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            DrawProperty(rect, property);
        }

        public static float DrawProperty(Rect position, float y, SerializedProperty property)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            DrawProperty(new Rect(position.x, y, position.width, height), property);
            return y + height + Spacing;
        }

        public static float PropertyHeight(SerializedProperty property)
        {
            return EditorGUI.GetPropertyHeight(property, true) + Spacing;
        }

        public static string EnumDisplayName(SerializedProperty property)
        {
            switch (property.name)
            {
                case "routeId":
                    return ValueAt(new[] { "地下第一层" }, property.enumValueIndex);
                case "roomType":
                    return ValueAt(new[] { "战斗", "祝福", "事件", "精英战斗", "商店", "休整", "首领" }, property.enumValueIndex);
                case "rewardPool":
                    return ValueAt(new[] { "无", "主要发现", "次要发现", "特殊门", "清房附加" }, property.enumValueIndex);
                case "triggerMode":
                    return ValueAt(new[] { "进房触发", "交互触发" }, property.enumValueIndex);
                case "completionMode":
                    return ValueAt(new[] { "结算房间", "启动刷怪", "完成路线" }, property.enumValueIndex);
                case "selectionMode":
                    return ValueAt(new[] { "预设组随机", "随机池抽取" }, property.enumValueIndex);
                default:
                    return property.enumDisplayNames[property.enumValueIndex];
            }
        }

        private static void DrawProperty(Rect rect, SerializedProperty property)
        {
            if (TryDrawEnum(rect, property))
            {
                return;
            }

            EditorGUI.PropertyField(rect, property, Label(property.name), true);
        }

        private static bool TryDrawEnum(Rect rect, SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                return false;
            }

            string[] labels;
            int[] values;
            switch (property.name)
            {
                case "routeId":
                    labels = new[] { "地下第一层" };
                    values = new[] { 0 };
                    break;
                case "roomType":
                    labels = new[] { "战斗", "祝福", "事件", "精英战斗", "商店", "休整", "首领" };
                    values = new[] { 0, 1, 2, 3, 4, 5, 6 };
                    break;
                case "rewardPool":
                    labels = new[] { "无", "主要发现", "次要发现", "特殊门", "清房附加" };
                    values = new[] { 0, 1, 2, 3, 4 };
                    break;
                case "triggerMode":
                    labels = new[] { "进房触发", "交互触发" };
                    values = new[] { 0, 1 };
                    break;
                case "completionMode":
                    labels = new[] { "结算房间", "启动刷怪", "完成路线" };
                    values = new[] { 0, 1, 2 };
                    break;
                case "selectionMode":
                    labels = new[] { "预设组随机", "随机池抽取" };
                    values = new[] { 0, 1 };
                    break;
                default:
                    return false;
            }

            property.enumValueIndex = EditorGUI.IntPopup(rect, Label(property.name), property.enumValueIndex, ToContents(labels), values);
            return true;
        }

        private static GUIContent Label(string propertyName)
        {
            switch (propertyName)
            {
                case "routeId": return new GUIContent("路线标识", "Director 用它检查绑定的路线表是否匹配。");
                case "startRoomId": return new GUIContent("起始房间 ID", "进入路线时首先进入的房间 id。");
                case "routeNote": return new GUIContent("路线备注", "只用于策划说明，不参与运行时逻辑。");
                case "rooms": return new GUIContent("房间列表", "本路线包含的全部房间。");
                case "tableNote": return new GUIContent("刷怪表备注", "只用于策划说明，不参与运行时逻辑。");
                case "encounters": return new GUIContent("Encounter 列表", "可被路线房间引用的刷怪配置。");
                case "encounterId": return new GUIContent("Encounter ID", "路线表的 encounterId 必须和这里一致。");
                case "encounterNote": return new GUIContent("Encounter 备注", "只用于说明该 encounter 的用途。");
                case "waves": return new GUIContent("波次列表", "按顺序执行；当前波清空后才进入下一波。");
                case "waveId": return new GUIContent("波次 ID", "用于配置辨识和错误日志。");
                case "waveNote": return new GUIContent("波次备注", "只用于说明该波次的用途。");
                case "delayAfterPreviousWave": return new GUIContent("波次延迟（秒）", "上一波清空后，到本波开始前的等待时间。");
                case "selectionMode": return new GUIContent("选怪方式", "预设组随机或随机池抽取。");
                case "presetGroups": return new GUIContent("预设组列表", "预设组随机模式使用。");
                case "randomPool": return new GUIContent("随机池配置", "随机池抽取模式使用。");
                case "groupId": return new GUIContent("预设组 ID", "只用于配置辨识。");
                case "weight": return new GUIContent("权重", "0 表示不会被抽中。");
                case "entries": return new GUIContent("怪物条目", "该预设组被选中后生成的怪物。");
                case "minCount": return new GUIContent("最小数量", "随机池模式下，本波最少生成数量。");
                case "maxCount": return new GUIContent("最大数量", "随机池模式下，本波最多生成数量。");
                case "candidates": return new GUIContent("候选怪物", "随机池模式下按权重抽取的候选。");
                case "monsterId": return new GUIContent("怪物 ID", "用于日志和配置辨识。");
                case "monsterPrefab": return new GUIContent("怪物 Prefab", "实际实例化的怪物 prefab。");
                case "count": return new GUIContent("生成数量", "预设组模式下该条目一次生成多少只。");
                case "overrideMaxHealth": return new GUIContent("覆盖生命值", "勾选后会覆盖 prefab/怪物配置自带生命值。");
                case "maxHealthOverride": return new GUIContent("生命值覆盖", "覆盖生命值时使用的最大生命值。");
                case "roomId": return new GUIContent("房间 ID", "起始房间和门跳转会用这个 id 查找房间。");
                case "displayName": return new GUIContent("显示名称", "会显示在 HUD 或门按钮上。");
                case "roomType": return new GUIContent("房间类型", "用于表现和门预告；是否开战由完成方式决定。");
                case "rewardPool": return new GUIContent("奖励池", "用于展示和后续奖励系统接入。");
                case "triggerMode": return new GUIContent("触发方式", "进房自动触发或等待玩家交互。");
                case "completionMode": return new GUIContent("完成方式", "触发/选择后执行结算、开战或结束路线。");
                case "rewardPreview": return new GUIContent("奖励预告", "显示在房间摘要或门选项中。");
                case "roomNote": return new GUIContent("房间备注", "显示在房间摘要中，也用于说明策划意图。");
                case "choices": return new GUIContent("选择项", "房间触发后给玩家选择的项目。");
                case "exits": return new GUIContent("出口门", "房间结算后可去的下一房间。");
                case "targetRoomId": return new GUIContent("目标房间 ID", "必须能在同一张路线表的房间列表中找到。");
                case "canReroll": return new GUIContent("可重随机", "当前只作为后续换门/重随机逻辑的配置标记。");
                case "isRiskDoor": return new GUIContent("风险门", "会在门按钮中追加风险标记。");
                case "choiceId": return new GUIContent("选项 ID", "只用于配置辨识。");
                case "description": return new GUIContent("说明", "会和显示名称一起显示在选择按钮上。");
                case "encounterIdOverride": return new GUIContent("覆盖 Encounter ID", "选择该项后可覆盖房间默认 encounterId。留空则不覆盖。");
                case "damageBonus": return new GUIContent("伤害加成", "小数表示，例如 0.2 表示 +20%。");
                case "goldDelta": return new GUIContent("金币变化", "选择后增加或减少的金币数量。");
                default: return new GUIContent(ObjectNames.NicifyVariableName(propertyName));
            }
        }

        private static GUIContent[] ToContents(string[] labels)
        {
            GUIContent[] contents = new GUIContent[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                contents[i] = new GUIContent(labels[i]);
            }

            return contents;
        }

        private static string ValueAt(string[] values, int index)
        {
            return index >= 0 && index < values.Length ? values[index] : string.Empty;
        }
    }
}
