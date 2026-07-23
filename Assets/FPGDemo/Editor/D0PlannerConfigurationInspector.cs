using System;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    /// <summary>
    /// D0 策划资产的统一 Inspector。大部分字段沿用 Unity 原生绘制；少数策划字段会以更易用的单位显示并换算。
    /// 所有值仍通过 SerializedProperty 写入，因此既有 YAML 字段名、Undo 和资产引用保持不变。
    /// </summary>
    internal abstract class D0PlannerConfigurationInspector : UnityEditor.Editor
    {
        private const string ArraySizeLabel = "数量";
        private const string ArraySizeTooltip = "此列表中的配置项数量。新增项后请补全每一项的必填字段并执行策划配置验证。";
        private const string AddArrayElementLabel = "新增配置项";
        private const string AddArrayElementTooltip = "在列表末尾新增一项。新增后请按当前攻击类型、资源引用和验证规则补全该项。";
        private const string MoveUpLabel = "上移";
        private const string MoveDownLabel = "下移";
        private const string RemoveLabel = "删除";

        private static readonly Dictionary<Type, FieldInfo[]> SerializableFieldCache =
            new Dictionary<Type, FieldInfo[]>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FieldInfo[] fields = GetSerializableFields(target.GetType());
            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                if (!ShouldDrawField(field))
                {
                    continue;
                }

                SerializedProperty property = serializedObject.FindProperty(field.Name);
                if (property != null)
                {
                    DrawField(property, field);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Lets a dedicated asset Inspector suppress top-level fields that do
        /// not apply to the currently authored configuration.
        /// </summary>
        protected virtual bool ShouldDrawField(FieldInfo field)
        {
            return true;
        }

private static void DrawField(SerializedProperty property, FieldInfo field)
        {
            if (field.GetCustomAttribute<D0PlannerTechnicalFieldAttribute>() != null)
            {
                return;
            }

            D0PlannerSectionAttribute section = field.GetCustomAttribute<D0PlannerSectionAttribute>();
            if (section != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(section.Title, EditorStyles.boldLabel);
            }

            D0PlannerFieldAttribute plannerField = field.GetCustomAttribute<D0PlannerFieldAttribute>();
            GUIContent label = new GUIContent(
                plannerField == null
                    ? ObjectNames.NicifyVariableName(field.Name)
                    : plannerField.DisplayName,
                plannerField == null ? string.Empty : plannerField.Tooltip);
            if (TryDrawWeaponFireRate(property, field))
            {
                return;
            }

            DrawProperty(property, field.FieldType, label);
        }

        private static bool TryDrawWeaponFireRate(
            SerializedProperty property,
            FieldInfo field)
        {
            if (!(property.serializedObject.targetObject is D0WeaponDefinition))
            {
                return false;
            }

            string displayName;
            string tooltip;
            switch (field.Name)
            {
                case "primaryIntervalTicks":
                    displayName = "主射射速（发/秒）";
                    tooltip = "按住左键时每秒最多释放的主射次数。战斗按 60Hz 离散运行，输入值会换算到最近的可实现射速，提交后数值可能轻微变化。";
                    break;
                case "secondaryRecoveryTicks":
                    displayName = "副射射速（发/秒）";
                    tooltip = "成功释放副射后每秒最多可再次释放的次数。战斗按 60Hz 离散运行，输入值会换算为武器恢复时间；恢复结束前主射、副射和换弹都不能开始。";
                    break;
                default:
                    return false;
            }

            int tickRate = FPG.Demo.Core.GameplayClock.DefaultTickRate;
            int intervalTicks = Mathf.Max(1, property.intValue);
            float currentFireRate = tickRate / (float)intervalTicks;
            GUIContent fireRateLabel = new GUIContent(displayName, tooltip);

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float requestedFireRate = EditorGUILayout.DelayedFloatField(
                fireRateLabel,
                currentFireRate);
            if (EditorGUI.EndChangeCheck()
                && requestedFireRate > 0f
                && !float.IsNaN(requestedFireRate)
                && !float.IsInfinity(requestedFireRate))
            {
                double requestedIntervalTicks = tickRate / (double)requestedFireRate;
                property.intValue = requestedIntervalTicks >= int.MaxValue
                    ? int.MaxValue
                    : Mathf.Max(1, (int)Math.Round(
                        requestedIntervalTicks,
                        MidpointRounding.AwayFromZero));
            }

            EditorGUI.showMixedValue = false;
            return true;
        }


        private static void DrawProperty(SerializedProperty property, Type declaredType, GUIContent label)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                DrawArray(property, GetElementType(declaredType), label);
                return;
            }

            if (property.propertyType == SerializedPropertyType.Generic
                && IsPlannerComposite(declaredType))
            {
                DrawComposite(property, declaredType, label);
                return;
            }

            // 叶子字段仍交由 Unity 绘制，确保 Min、Range、TextArea 等原生约束不被替换。
            EditorGUILayout.PropertyField(property, label, includeChildren: false);
        }

        private static void DrawArray(SerializedProperty property, Type elementType, GUIContent label)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            SerializedProperty size = property.FindPropertyRelative("Array.size");
            if (size != null)
            {
                EditorGUILayout.PropertyField(
                    size,
                    new GUIContent(ArraySizeLabel, ArraySizeTooltip),
                    includeChildren: false);
            }

            for (int index = 0; index < property.arraySize; index++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(index);
                GUIContent elementLabel = new GUIContent($"第 {index + 1} 项", label.tooltip);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawProperty(element, elementType, elementLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        using (new EditorGUI.DisabledScope(index == 0))
                        {
                            if (GUILayout.Button(new GUIContent(MoveUpLabel, "将此项与上一项交换。遭遇时间表的同一触发时刻仍以时序编号决定先后。"), EditorStyles.miniButtonLeft))
                            {
                                property.MoveArrayElement(index, index - 1);
                            }
                        }

                        using (new EditorGUI.DisabledScope(index >= property.arraySize - 1))
                        {
                            if (GUILayout.Button(new GUIContent(MoveDownLabel, "将此项与下一项交换。"), EditorStyles.miniButtonMid))
                            {
                                property.MoveArrayElement(index, index + 1);
                            }
                        }

                        if (GUILayout.Button(new GUIContent(RemoveLabel, "删除此配置项。删除遭遇攻击后请重新执行策划配置验证。"), EditorStyles.miniButtonRight))
                        {
                            RemoveArrayElement(property, index);
                            break;
                        }
                    }
                }
            }

            if (GUILayout.Button(new GUIContent(AddArrayElementLabel, AddArrayElementTooltip)))
            {
                property.InsertArrayElementAtIndex(property.arraySize);
            }

            EditorGUI.indentLevel--;
        }

        private static void RemoveArrayElement(SerializedProperty property, int index)
        {
            int originalSize = property.arraySize;
            property.DeleteArrayElementAtIndex(index);
            if (property.arraySize == originalSize
                && index >= 0
                && index < property.arraySize)
            {
                property.DeleteArrayElementAtIndex(index);
            }
        }

        private static void DrawComposite(SerializedProperty property, Type declaredType, GUIContent label)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            FieldInfo[] fields = GetSerializableFields(declaredType);
            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                SerializedProperty child = property.FindPropertyRelative(field.Name);
                if (child != null)
                {
                    DrawField(child, field);
                }
            }

            EditorGUI.indentLevel--;
        }

        private static Type GetElementType(Type declaredType)
        {
            if (declaredType != null && declaredType.IsArray)
            {
                return declaredType.GetElementType();
            }

            if (declaredType != null
                && declaredType.IsGenericType
                && declaredType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return declaredType.GetGenericArguments()[0];
            }

            return declaredType;
        }

        private static bool IsPlannerComposite(Type declaredType)
        {
            if (declaredType == null
                || declaredType.GetCustomAttribute<SerializableAttribute>() == null
                || typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
            {
                return false;
            }

            FieldInfo[] fields = GetSerializableFields(declaredType);
            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                if (field.GetCustomAttribute<D0PlannerFieldAttribute>() != null
                    || field.GetCustomAttribute<D0PlannerSectionAttribute>() != null
                    || field.GetCustomAttribute<D0PlannerTechnicalFieldAttribute>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            if (type == null)
            {
                return Array.Empty<FieldInfo>();
            }

            if (SerializableFieldCache.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }

            List<Type> hierarchy = new List<Type>();
            for (Type current = type;
                 current != null && current != typeof(object) && current != typeof(UnityEngine.Object);
                 current = current.BaseType)
            {
                hierarchy.Add(current);
            }

            hierarchy.Reverse();
            List<FieldInfo> fields = new List<FieldInfo>();
            for (int typeIndex = 0; typeIndex < hierarchy.Count; typeIndex++)
            {
                FieldInfo[] declared = hierarchy[typeIndex].GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Array.Sort(declared, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
                for (int fieldIndex = 0; fieldIndex < declared.Length; fieldIndex++)
                {
                    FieldInfo field = declared[fieldIndex];
                    if (field.IsStatic
                        || field.IsInitOnly
                        || field.IsNotSerialized
                        || field.GetCustomAttribute<HideInInspector>() != null)
                    {
                        continue;
                    }

                    if (field.IsPublic
                        || field.GetCustomAttribute<SerializeField>() != null
                        || field.GetCustomAttribute<SerializeReference>() != null)
                    {
                        fields.Add(field);
                    }
                }
            }

            cached = fields.ToArray();
            SerializableFieldCache.Add(type, cached);
            return cached;
        }
    }


    internal static class D0ThreeCRuntimePreviewEditor
    {
        public static bool TryApply(
            D0ThreeCProfile profile,
            bool restartBattle,
            out string message)
        {
            message = string.Empty;
            if (profile == null)
            {
                message = "\u6ca1\u6709\u53ef\u5e94\u7528\u7684 D0 3C \u914d\u7f6e\u3002";
                return false;
            }

            BattleSceneContext[] contexts = UnityEngine.Object.FindObjectsByType<BattleSceneContext>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int matchingContexts = 0;
            int appliedContexts = 0;
            List<string> errors = new List<string>();
            for (int index = 0; index < contexts.Length; index++)
            {
                BattleSceneContext context = contexts[index];
                if (context == null
                    || !context.gameObject.scene.IsValid()
                    || !context.gameObject.scene.isLoaded)
                {
                    continue;
                }

                D0CombatScenarioDefinition scenario = context.ScenarioConfig == null
                    ? null
                    : context.ScenarioConfig.AuthoredScenario;
                if (scenario == null || scenario.ThreeCProfile != profile)
                {
                    continue;
                }

                matchingContexts++;
                if (restartBattle)
                {
                    if (context.SessionHost == null || !context.SessionHost.IsInitialized)
                    {
                        errors.Add($"{context.gameObject.scene.name}: \u6218\u6597\u5c1a\u672a\u521d\u59cb\u5316\uff0c\u65e0\u6cd5\u91cd\u542f\u3002");
                        continue;
                    }

                    if (!context.SessionHost.TryRestart().IsSuccess)
                    {
                        string error = context.SessionHost.LastError;
                        errors.Add($"{context.gameObject.scene.name}: {error}");
                        continue;
                    }
                }
                else if (!D0ThreeCRuntimeProfileApplier.TryApplyPresentation(
                             context,
                             profile,
                             out string applyError))
                {
                    errors.Add($"{context.gameObject.scene.name}: {applyError}");
                    continue;
                }

                appliedContexts++;
            }

            if (matchingContexts == 0)
            {
                message = "\u5f53\u524d\u6ca1\u6709\u4f7f\u7528\u6b64 D0 3C \u914d\u7f6e\u7684\u5df2\u52a0\u8f7d\u573a\u666f\u3002\u8fdb\u5165 CombatLab Play Mode \u540e\u518d\u5e94\u7528\u3002";
                return false;
            }

            if (errors.Count > 0)
            {
                message = string.Join("\n", errors);
                return false;
            }

            message = restartBattle
                ? $"\u5df2\u91cd\u542f\u5e76\u5e94\u7528 {appliedContexts} \u4e2a CombatLab \u573a\u666f\u3002"
                : $"\u5df2\u5373\u65f6\u5e94\u7528 {appliedContexts} \u4e2a CombatLab \u573a\u666f\u7684\u8868\u73b0\u53c2\u6570\u3002";
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            return true;
        }
    }

    [CustomEditor(typeof(D0CombatScenarioDefinition))]
    internal sealed class D0CombatScenarioDefinitionInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0CharacterDefinition))]
    internal sealed class D0CharacterDefinitionInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0WeaponDefinition))]
    internal sealed class D0WeaponDefinitionInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0CombatFeelProfile))]
    internal sealed class D0CombatFeelProfileInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0ThreeCProfile))]
    internal sealed class D0ThreeCProfileInspector : D0PlannerConfigurationInspector
    {
        private const string AutoPreviewSessionKey =
            "FPG.Demo.D0ThreeCProfileInspector.AutoPreview";
        private string previewMessage = string.Empty;
        private MessageType previewMessageType = MessageType.Info;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();
            bool propertiesChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("\u8fd0\u884c\u65f6\u9884\u89c8", EditorStyles.boldLabel);
            bool autoPreview = SessionState.GetBool(AutoPreviewSessionKey, true);
            bool nextAutoPreview = EditorGUILayout.ToggleLeft(
                "Play Mode \u4fee\u6539\u540e\u81ea\u52a8\u5e94\u7528\u8868\u73b0\u53c2\u6570",
                autoPreview);
            if (nextAutoPreview != autoPreview)
            {
                SessionState.SetBool(AutoPreviewSessionKey, nextAutoPreview);
            }

            EditorGUILayout.HelpBox(
                "\u76f8\u673a\u3001\u51c6\u661f\u3001\u62a4\u76fe\u548c\u955c\u5934\u57fa\u7ebf\u4f1a\u5728 Play Mode \u4e2d\u5373\u65f6\u5e94\u7528\uff1b\u653b\u51fb\u67e5\u8be2\u6700\u8fdc\u8ddd\u79bb\u548c\u8f93\u5165\u7f13\u51b2\u5c5e\u4e8e\u6218\u6597\u4f1a\u8bdd\u53c2\u6570\uff0c\u4fee\u6539\u540e\u8bf7\u70b9\u51fb\u201c\u91cd\u542f\u6218\u6597\u5e76\u5e94\u7528\u5168\u90e8\u201d\u3002\u4e0d\u9700\u8981\u91cd\u65b0\u6267\u884c\u5b89\u88c5\u5668\u3002",
                MessageType.Info);

            if (propertiesChanged && EditorApplication.isPlaying && nextAutoPreview)
            {
                ApplyPreview(restartBattle: false);
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("\u5e94\u7528\u8868\u73b0\u5230\u5f53\u524d\u8fd0\u884c"))
                {
                    ApplyPreview(restartBattle: false);
                }

                if (GUILayout.Button("\u91cd\u542f\u6218\u6597\u5e76\u5e94\u7528\u5168\u90e8"))
                {
                    ApplyPreview(restartBattle: true);
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "\u8fdb\u5165 CombatLab Play Mode \u540e\u53ef\u5728\u6b64\u8d44\u4ea7\u4e0a\u76f4\u63a5\u8c03\u53c2\u5e76\u9884\u89c8\u3002\u8fd0\u884c\u65f6\u542f\u52a8\u4e5f\u4f1a\u81ea\u52a8\u4ece 3C \u8d44\u4ea7\u5e94\u7528\u914d\u7f6e\u3002",
                    MessageType.None);
            }

            if (!string.IsNullOrEmpty(previewMessage))
            {
                EditorGUILayout.HelpBox(previewMessage, previewMessageType);
            }
        }

        private void ApplyPreview(bool restartBattle)
        {
            D0ThreeCProfile profile = target as D0ThreeCProfile;
            if (D0ThreeCRuntimePreviewEditor.TryApply(
                    profile,
                    restartBattle,
                    out string message))
            {
                previewMessageType = MessageType.Info;
            }
            else
            {
                previewMessageType = MessageType.Warning;
            }

            previewMessage = message;
            Repaint();
        }
    }

    [CustomEditor(typeof(D0EnemyDefinition))]
    internal sealed class D0EnemyDefinitionInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0EnemyBehaviorProfile))]
    internal sealed class D0EnemyBehaviorProfileInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0EnemyAttackDefinition))]
    internal sealed class D0EnemyAttackDefinitionInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0EncounterDefinition))]
    internal sealed class D0EncounterDefinitionInspector : D0PlannerConfigurationInspector
    {
    }

    [CustomEditor(typeof(D0ActorPresentationDefinition))]
    internal sealed class D0ActorPresentationDefinitionInspector : D0PlannerConfigurationInspector
    {
        protected override bool ShouldDrawField(FieldInfo field)
        {
            if (field == null
                || (field.Name != "player"
                    && field.Name != "enemy"
                    && field.Name != "enemyEffects"))
            {
                return true;
            }

            SerializedProperty actorKind = serializedObject.FindProperty("actorKind");
            if (actorKind == null || actorKind.hasMultipleDifferentValues)
            {
                return true;
            }

            switch ((D0ActorKind)actorKind.enumValueIndex)
            {
                case D0ActorKind.Player:
                    return field.Name == "player";

                case D0ActorKind.Enemy:
                    return field.Name != "player";

                default:
                    return true;
            }
        }
    }

    [CustomEditor(typeof(D0StageDefinition))]
    internal sealed class D0StageDefinitionInspector : D0PlannerConfigurationInspector
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "舞台资产只维护环境与出生点，并由 D0 安装器写入场景。角色视觉、技能 Socket 和射击特效请在角色表现资产中配置；修改森林图层或出生点后再执行安装器。"
                + "相机参数统一在 2.5D 3C 配置中编辑；敌人视觉根、投射物锚点、弱点与命中体由敌人定义引用的 Entity Prefab 完整拥有。",
                MessageType.Info);
            base.OnInspectorGUI();
        }
    }

    /// <summary>
    /// CombatPresentationProfile only owns global presentation language and
    /// budgets. Actor, weapon and attack content is edited on its owning SO.
    /// </summary>
    [CustomEditor(typeof(CombatPresentationProfile))]
    internal sealed class CombatPresentationProfileD0EntryInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawProperty("threatDefinitions", "Threat Styles");
            DrawProperty("hitDefinitions", "Hit Feedback");
            DrawProperty("formalHudResources", "正式战斗 HUD 资源");
            DrawProperty("formalDamagePopup", "正式伤害跳字");
            DrawProperty("formalReticle", "正式战斗准星");
            DrawProperty("sorting", "Global Sorting");
            DrawProperty("poolCapacities", "Shared Pool Budget");
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(
                    property,
                    new GUIContent(label),
                    includeChildren: true);
            }
        }
    }

    /// <summary>
    /// BattleScenarioConfig is the scene-to-D0 scenario selection boundary.
    /// </summary>
    [CustomEditor(typeof(BattleScenarioConfig))]
    internal sealed class BattleScenarioConfigD0EntryInspector : UnityEditor.Editor
    {
        private static readonly GUIContent AuthoredScenarioLabel = new GUIContent(
            "D0 场景配置",
            "选择后，角色、武器、手感、敌人遭遇和舞台均从该 D0 资产链读取；旧内联字段不再作为 D0 数据来源。");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty authoredScenario = serializedObject.FindProperty("authoredScenario");

            EditorGUILayout.LabelField("D0 策划配置入口（可选）", EditorStyles.boldLabel);
            if (authoredScenario != null)
            {
                EditorGUILayout.PropertyField(authoredScenario, AuthoredScenarioLabel, includeChildren: false);
            }

            if (authoredScenario != null && authoredScenario.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox(
                    "当前场景已使用 D0 策划配置。角色、怪物、关卡和手感请在引用的 D0 资产中编辑；"
                    + "工程容量、LayerMask 与旧原型兼容字段已在此面板隐藏。",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "尚未选择 D0 场景配置。旧原型兼容字段以及技术容量、LayerMask 和物理查询设置均不向策划开放；"
                    + "请由程序创建并接入 D0 场景配置资产后，再在该资产链中进行配置。",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
