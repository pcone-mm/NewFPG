using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MonsterJsonConfigSourceEditorTests
{
    private const string FishPrefabPath = "Assets/Prefabs/Monster/Fish.prefab";
    private const string FishBehaviorTreePath = "Assets/Settings/Monsters/BehaviorTrees/BT_Fish.asset";

    [Test]
    public void FishPrefabKeepsJsonBindingAndUsesBehaviorTree()
    {
        InvokeStatic(
            RequireType("NewFPG.EditorTools.MonsterConfigEditorUtility, Assembly-CSharp-Editor"),
            "RefreshPrefabJsonBindings");

        string yaml = File.ReadAllText(Path.GetFullPath(FishPrefabPath));
        StringAssert.Contains("NewFPG.Monsters.MonsterConfigBinding", yaml);
        StringAssert.Contains("Pathfinding.AIPath", yaml);
        StringAssert.Contains("Pathfinding.Seeker", yaml);
        StringAssert.DoesNotContain("NavMeshAgent", yaml);
        StringAssert.DoesNotContain("Rigidbody:", yaml);
        StringAssert.DoesNotContain("NewFPG.Monsters.FishMonsterController", yaml);
        StringAssert.DoesNotContain("NewFPG.Monsters.MonsterRuntimeController", yaml);
        StringAssert.DoesNotContain("NewFPG.Monsters.MonsterBrain", yaml);
        StringAssert.DoesNotContain("NewFPG.Monsters.MonsterSkillController", yaml);
        StringAssert.DoesNotContain("NewFPG.Monsters.MonsterMechanicRunner", yaml);
        StringAssert.DoesNotContain("NewFPG.Monsters.MonsterState", yaml);
        StringAssert.DoesNotContain("NewFPG.Combat.MonsterAttackController", yaml);
        StringAssert.Contains("monsterId: fish", yaml);
        StringAssert.Contains("catalogJson:", yaml);
        StringAssert.Contains("applyOnAwake: 1", yaml);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FishPrefabPath);
        Assert.IsNotNull(prefab, "Fish prefab should load.");
        BoxCollider fishCollider = prefab.GetComponent<BoxCollider>();
        Assert.IsNotNull(fishCollider, "Fish.prefab should keep a BoxCollider for hit/skill detection.");
        Assert.IsTrue(fishCollider.isTrigger, "Fish BoxCollider should be trigger-only; A* movement uses the small AIPath radius.");

        System.Type behaviorTreeType = RequireType("BehaviorDesigner.Runtime.BehaviorTree, Assembly-CSharp");
        Component behaviorTree = prefab.GetComponent(behaviorTreeType);
        Assert.IsNotNull(behaviorTree, "Fish.prefab should have a BehaviorTree component.");

        System.Type externalTreeType = RequireType("BehaviorDesigner.Runtime.ExternalBehaviorTree, Assembly-CSharp");
        UnityEngine.Object externalTree = AssetDatabase.LoadAssetAtPath(
            FishBehaviorTreePath,
            externalTreeType);
        Assert.IsNotNull(externalTree, "BT_Fish external behavior tree should exist.");
        AssertReferencesExternalTree(behaviorTree, externalTree);

        AssertNoTuningCopy(yaml, "moveSpeed:");
        AssertNoTuningCopy(yaml, "attackRange:");
        AssertNoTuningCopy(yaml, "damageRadius:");
        AssertNoTuningCopy(yaml, "maxHealth:");
        AssertNoTuningCopy(yaml, "startingHealth:");
        AssertNoTuningCopy(yaml, "hitTint:");
        AssertNoTuningCopy(yaml, "movementBehaviour:");
        AssertNoTuningCopy(yaml, "warningIndicator:");
        AssertNoTuningCopy(yaml, "monsterRuntime:");
    }

    [Test]
    public void FishExternalBehaviorTreeContainsDefaultAiFlow()
    {
        InvokeStatic(
            RequireType("NewFPG.EditorTools.MonsterConfigEditorUtility, Assembly-CSharp-Editor"),
            "RefreshPrefabJsonBindings");

        string yaml = File.ReadAllText(Path.GetFullPath(FishBehaviorTreePath));
        StringAssert.Contains("JSONSerialization:", yaml);
        StringAssert.Contains("BehaviorDesigner.Runtime.Tasks.Repeater", yaml);
        StringAssert.Contains("BehaviorDesigner.Runtime.Tasks.Selector", yaml);
        StringAssert.Contains("BehaviorDesigner.Runtime.Tasks.Sequence", yaml);
        StringAssert.Contains("NewFPG.Monsters.BehaviorDesigner.MonsterFindTargetByTag", yaml);
        StringAssert.Contains("NewFPG.Monsters.BehaviorDesigner.MonsterMoveToBattleZoneGroup", yaml);
        StringAssert.DoesNotContain("NewFPG.Monsters.BehaviorDesigner.MonsterMoveToVisibleCameraBand", yaml);
        StringAssert.Contains("NewFPG.Monsters.BehaviorDesigner.MonsterTargetInSkillRange", yaml);
        StringAssert.Contains("NewFPG.Monsters.BehaviorDesigner.MonsterTargetLineOfSight", yaml);
        StringAssert.Contains("NewFPG.Monsters.BehaviorDesigner.MonsterUseSkill", yaml);
        StringAssert.Contains("NewFPG.Monsters.BehaviorDesigner.MonsterChaseTarget", yaml);
        StringAssert.Contains("NewFPG.Monsters.BehaviorDesigner.MonsterPatrol", yaml);
        StringAssert.Contains("StringmValue\":\"melee_bite", yaml);
        StringAssert.Contains("\"MonsterBattleZoneRowsrows\":\"Front,", yaml);
        StringAssert.Contains("Middle\"", yaml);
        StringAssert.Contains("\"MonsterBattleZoneRowsrows\":\"Back\"", yaml);
        StringAssert.Contains("\"MonsterBattleZoneColumnscolumns\":\"All\"", yaml);
        StringAssert.DoesNotContain("\"Disabled\":true", yaml);
        StringAssert.DoesNotContain("\"SharedStringzoneGroups\"", yaml);
        StringAssert.DoesNotContain("\"SharedStringdistanceBands\"", yaml);
        StringAssert.Contains("\"Name\":\"\\u6280\\u80fd\\u6216\\u8ffd\\u8e2a\"", yaml);
        StringAssert.Contains("\"AbortTypeabortType\":\"LowerPriority\"", yaml);
        StringAssert.Contains("\"SharedBoolwaitUntilArrived\"", yaml);
        StringAssert.Contains("\"BooleanmValue\":false", yaml);
        AssertEntryTaskDoesNotDuplicateRootTask(yaml);
        AssertFishBehaviorTreeUsesReadableLayout(yaml);
        AssertBehaviorDesignerCanDeserializeFishTree();
    }

    [Test]
    public void MonsterJsonDefinesFishBehaviorTreeSkillAndMechanic()
    {
        string json = File.ReadAllText(Path.GetFullPath("Assets/Settings/Monsters/monster_catalog.json"));

        StringAssert.Contains("\"ai\"", json);
        StringAssert.Contains("\"behaviorTreePath\": \"Assets/Settings/Monsters/BehaviorTrees/BT_Fish.asset\"", json);
        StringAssert.Contains("\"displayName\": \"鱼怪\"", json);
        StringAssert.Contains("\"中文备注\"", json);
        StringAssert.Contains("\"detectionRadius\": 0.0", json);
        StringAssert.Contains("不限制距离", json);
        StringAssert.Contains("\"skills\"", json);
        StringAssert.Contains("\"skillId\": \"melee_bite\"", json);
        StringAssert.Contains("\"displayName\": \"近身咬击\"", json);
        StringAssert.Contains("\"cooldown\": 2.0", json);
        StringAssert.Contains("\"castRange\"", json);
        StringAssert.Contains("\"requireLineOfSight\": true", json);
        StringAssert.Contains("\"lineOfSightMask\": 2048", json);
        StringAssert.Contains("\"lineOfSightHeightOffset\": 1.0", json);
        StringAssert.Contains("\"type\": \"damage_area\"", json);
        StringAssert.Contains("\"nearZoneGroup\"", json);
        StringAssert.Contains("\"midZoneGroup\"", json);
        StringAssert.Contains("\"farZoneGroup\"", json);
        StringAssert.Contains("\"leftZoneGroup\"", json);
        StringAssert.Contains("\"centerZoneGroup\"", json);
        StringAssert.Contains("\"rightZoneGroup\"", json);
        StringAssert.Contains("\"zoneIds\": \"left_front,center_front,right_front\"", json);
        StringAssert.Contains("\"zoneIds\": \"left_mid,center_mid,right_mid\"", json);
        StringAssert.Contains("\"zoneIds\": \"left_back,center_back,right_back\"", json);
        StringAssert.Contains("AstarBlocking", json);
        StringAssert.DoesNotContain("\"nearCameraBand\"", json);
        StringAssert.DoesNotContain("\"midCameraBand\"", json);
        StringAssert.DoesNotContain("\"farCameraBand\"", json);
        StringAssert.DoesNotContain("MonsterMoveToVisibleCameraBand", File.ReadAllText(Path.GetFullPath(FishBehaviorTreePath)));
        StringAssert.DoesNotContain("\"skillRules\"", json);
        StringAssert.DoesNotContain("\"preSkillActions\"", json);
        StringAssert.DoesNotContain("\"postSkillActions\"", json);
        StringAssert.DoesNotContain("\"move_to_visible_camera_band\"", json);
    }

    [Test]
    public void MonsterCatalogAuthoringRoundTripsJson()
    {
        string json = File.ReadAllText(Path.GetFullPath("Assets/Settings/Monsters/monster_catalog.json"));
        ScriptableObject authoringObject = ScriptableObject.CreateInstance(RequireType("NewFPG.Monsters.MonsterCatalogAuthoring, Assembly-CSharp"));
        try
        {
            Invoke(authoringObject, "ImportFromJson", json);
            string exported = (string)Invoke(authoringObject, "ExportToJson");

            StringAssert.Contains("\"monsterId\": \"fish\"", exported);
            StringAssert.Contains("\"displayName\": \"鱼怪\"", exported);
            StringAssert.Contains("\"中文备注\"", exported);
            StringAssert.Contains("\"behaviorTreePath\": \"Assets/Settings/Monsters/BehaviorTrees/BT_Fish.asset\"", exported);
            StringAssert.Contains("\"skillId\": \"melee_bite\"", exported);
            StringAssert.Contains("\"displayName\": \"近身咬击\"", exported);
            StringAssert.Contains("\"castRange\"", exported);
            StringAssert.Contains("\"requireLineOfSight\": true", exported);
            StringAssert.Contains("\"nearZoneGroup\"", exported);
            StringAssert.Contains("\"zoneIds\": \"left_front,center_front,right_front\"", exported);
            StringAssert.Contains("\"type\": \"damage_area\"", exported);
            StringAssert.DoesNotContain("\"skillRules\"", exported);
            StringAssert.DoesNotContain("\"preSkillActions\"", exported);
            StringAssert.DoesNotContain("\"postSkillActions\"", exported);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(authoringObject);
        }
    }

    [Test]
    public void MonsterCatalogRoundTripsBehaviorTreeAndSkillReleaseConditions()
    {
        const string json = @"{
  ""monsters"": [
    {
      ""monsterId"": ""zone_fish"",
      ""ai"": {
        ""behaviorTreePath"": ""Assets/Settings/Monsters/BehaviorTrees/BT_Fish.asset""
      },
      ""skills"": [
        {
          ""skillId"": ""spit"",
          ""castRange"": 6.0,
          ""requireLineOfSight"": true,
          ""lineOfSightMask"": 2048,
          ""lineOfSightHeightOffset"": 1.25,
          ""mechanics"": [ { ""type"": ""damage_area"" } ]
        }
      ]
    }
  ]
}";

        System.Type catalogType = RequireType("NewFPG.Monsters.MonsterCatalog, Assembly-CSharp");
        MethodInfo fromJson = catalogType.GetMethod("FromJson", BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(fromJson);
        object catalog = fromJson.Invoke(null, new object[] { json });
        string exported = (string)Invoke(catalog, "ToJson");

        StringAssert.Contains("\"behaviorTreePath\": \"Assets/Settings/Monsters/BehaviorTrees/BT_Fish.asset\"", exported);
        StringAssert.Contains("\"castRange\": 6.0", exported);
        StringAssert.Contains("\"requireLineOfSight\": true", exported);
        StringAssert.Contains("\"lineOfSightMask\": 2048", exported);
        StringAssert.Contains("\"lineOfSightHeightOffset\": 1.25", exported);
        StringAssert.Contains("\"nearZoneGroup\"", exported);
        StringAssert.Contains("\"zoneIds\": \"left_front,center_front,right_front\"", exported);
        StringAssert.DoesNotContain("\"move_to_battle_zone\"", exported);
        StringAssert.DoesNotContain("\"actionId\"", exported);
    }

    [Test]
    public void MonsterAiAuthoringDrawerShowsOnlyBehaviorTreeFields()
    {
        System.Type drawerType = RequireType("NewFPG.EditorTools.MonsterAiDefinitionDrawer, Assembly-CSharp-Editor");
        object drawer = System.Activator.CreateInstance(drawerType, true);
        PropertyInfo fieldNamesProperty = drawerType.GetProperty(
            "FieldNames",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fieldNamesProperty);

        string[] fieldNames = (string[])fieldNamesProperty.GetValue(drawer);
        CollectionAssert.Contains(fieldNames, "behaviorTreePath");
        CollectionAssert.DoesNotContain(fieldNames, "skillRules");
        CollectionAssert.DoesNotContain(fieldNames, "preSkillActions");
        CollectionAssert.DoesNotContain(fieldNames, "postSkillActions");
    }

    private static void AssertNoTuningCopy(string yaml, string fieldName)
    {
        Assert.IsFalse(yaml.Contains(fieldName), $"{fieldName} should live in monster_catalog.json, not Fish.prefab.");
    }

    private static void AssertReferencesExternalTree(Component behaviorTree, UnityEngine.Object externalTree)
    {
        SerializedObject serializedTree = new SerializedObject(behaviorTree);
        SerializedProperty property = serializedTree.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyType == SerializedPropertyType.ObjectReference
                && property.objectReferenceValue == externalTree)
            {
                return;
            }
        }

        Assert.Fail("BehaviorTree should reference BT_Fish.asset.");
    }

    private static void AssertEntryTaskDoesNotDuplicateRootTask(string yaml)
    {
        int entryIndex = yaml.IndexOf("\"EntryTask\"", StringComparison.Ordinal);
        int rootIndex = yaml.IndexOf("\"RootTask\"", StringComparison.Ordinal);
        Assert.GreaterOrEqual(entryIndex, 0, "BT_Fish should serialize an EntryTask.");
        Assert.Greater(rootIndex, entryIndex, "BT_Fish should serialize RootTask after EntryTask.");

        string entryBlock = yaml.Substring(entryIndex, rootIndex - entryIndex);
        StringAssert.DoesNotContain(
            "\"Children\"",
            entryBlock,
            "EntryTask must not duplicate RootTask as a child, or Behavior Designer opens with duplicate task IDs.");
    }

    private static void AssertFishBehaviorTreeUsesReadableLayout(string yaml)
    {
        float minSkillFlowX = Math.Min(
            Math.Min(RequireNodeOffsetX(yaml, "\\u8fd1\\u8eab\\u54ac\\u51fb\\u53ef\\u7528"), RequireNodeOffsetX(yaml, "\\u5c1d\\u8bd5\\u9760\\u8fd1\\u6218\\u6597\\u533a\\u57df")),
            Math.Min(RequireNodeOffsetX(yaml, "\\u786e\\u8ba4\\u65bd\\u653e\\u8ddd\\u79bb"), RequireNodeOffsetX(yaml, "\\u786e\\u8ba4\\u89c6\\u7ebf")));
        float maxSkillFlowX = Math.Max(
            Math.Max(RequireNodeOffsetX(yaml, "\\u91ca\\u653e\\u8fd1\\u8eab\\u54ac\\u51fb"), RequireNodeOffsetX(yaml, "\\u5c1d\\u8bd5\\u62c9\\u8fdc")),
            RequireNodeOffsetX(yaml, "\\u8ffd\\u8e2a\\u76ee\\u6807"));

        Assert.Greater(
            maxSkillFlowX - minSkillFlowX,
            500f,
            "近身咬击流程和追踪分支应横向展开，不能再堆成同一列。");
        StringAssert.DoesNotContain("\"Offset\":\"(-560,760)\"", yaml);
        StringAssert.DoesNotContain("\"Offset\":\"(-560,880)\"", yaml);
        StringAssert.DoesNotContain("\"Offset\":\"(-560,1120)\"", yaml);
        StringAssert.DoesNotContain("\"Offset\":\"(-560,1360)\"", yaml);
    }

    private static float RequireNodeOffsetX(string yaml, string escapedNodeName)
    {
        string nameMarker = "\"Name\":\"" + escapedNodeName + "\"";
        int nameIndex = yaml.IndexOf(nameMarker, StringComparison.Ordinal);
        Assert.GreaterOrEqual(nameIndex, 0, "BT_Fish should contain node " + escapedNodeName + ".");

        int offsetIndex = yaml.LastIndexOf("\"Offset\":\"(", nameIndex, StringComparison.Ordinal);
        Assert.GreaterOrEqual(offsetIndex, 0, "Node " + escapedNodeName + " should serialize an offset.");
        int valueStart = offsetIndex + "\"Offset\":\"(".Length;
        int commaIndex = yaml.IndexOf(",", valueStart, StringComparison.Ordinal);
        Assert.Greater(commaIndex, valueStart, "Node " + escapedNodeName + " should serialize a Vector2 offset.");

        string xText = yaml.Substring(valueStart, commaIndex - valueStart);
        Assert.IsTrue(
            float.TryParse(xText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x),
            "Node " + escapedNodeName + " offset x should parse.");
        return x;
    }

    private static void AssertBehaviorDesignerCanDeserializeFishTree()
    {
        System.Type externalTreeType = RequireType("BehaviorDesigner.Runtime.ExternalBehaviorTree, Assembly-CSharp");
        UnityEngine.Object externalTree = AssetDatabase.LoadAssetAtPath(FishBehaviorTreePath, externalTreeType);
        Assert.IsNotNull(externalTree, "BT_Fish external behavior tree should exist.");

        MethodInfo getBehaviorSource = externalTreeType.GetMethod(
            "GetBehaviorSource",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(getBehaviorSource, "ExternalBehavior.GetBehaviorSource should exist.");
        object behaviorSource = getBehaviorSource.Invoke(externalTree, null);
        Assert.IsNotNull(behaviorSource, "BT_Fish should have a BehaviorSource.");

        System.Type behaviorSourceType = behaviorSource.GetType();
        MethodInfo checkForSerialization = behaviorSourceType.GetMethod(
            "CheckForSerialization",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(bool), behaviorSourceType, typeof(bool) },
            null);
        Assert.IsNotNull(checkForSerialization, "BehaviorSource.CheckForSerialization should exist.");

        bool result = (bool)checkForSerialization.Invoke(
            behaviorSource,
            new[] { (object)true, behaviorSource, false });
        Assert.IsTrue(result, "Behavior Designer should deserialize BT_Fish without duplicate task IDs.");
    }

    private static System.Type RequireType(string assemblyQualifiedName)
    {
        System.Type type = System.Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object InvokeStatic(System.Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, type.Name + "." + methodName + " should exist.");
        return method.Invoke(null, args);
    }
}
