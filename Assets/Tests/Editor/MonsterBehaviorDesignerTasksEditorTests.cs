using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

public sealed class MonsterBehaviorDesignerTasksEditorTests
{
    [TestCase("MonsterHasTarget", "有目标")]
    [TestCase("MonsterTargetValid", "目标有效")]
    [TestCase("MonsterSkillUsable", "技能可用")]
    [TestCase("MonsterTargetInSkillRange", "目标在技能范围内")]
    [TestCase("MonsterTargetLineOfSight", "目标视线可见")]
    [TestCase("MonsterHasArrived", "已到达")]
    [TestCase("MonsterIsCasting", "正在施法")]
    [TestCase("MonsterFindTargetByTag", "按 Tag 查找目标")]
    [TestCase("MonsterChaseTarget", "追踪目标")]
    [TestCase("MonsterMoveToVisibleCameraBand", "旧兼容：移动到区域组")]
    [TestCase("MonsterMoveToBattleZoneGroup", "移动到战斗区域组")]
    [TestCase("MonsterMoveToBattleZone", "移动到战斗区域")]
    [TestCase("MonsterUseSkill", "释放怪物技能")]
    [TestCase("MonsterPatrol", "巡逻")]
    [TestCase("MonsterStopMovement", "停止移动")]
    public void MonsterBehaviorDesignerTaskMetadataIsChinese(string typeName, string chineseName)
    {
        Type type = RequireTaskType(typeName);

        AssertAttributeContains(type, "TaskCategoryAttribute", "NewFPG/怪物AI");
        AssertAttributeContains(type, "TaskNameAttribute", chineseName);
        AssertAttributeHasChinese(type, "TaskDescriptionAttribute");
    }

    [Test]
    public void KeyMonsterBehaviorDesignerTaskDefaultsAreDesignerFriendly()
    {
        Type skillUsableType = RequireTaskType("MonsterSkillUsable");
        object skillUsable = Activator.CreateInstance(skillUsableType);
        Assert.AreEqual("melee_bite", GetSharedValue(GetField(skillUsable, "skillId")));

        Type zoneGroupType = RequireTaskType("MonsterMoveToBattleZoneGroup");
        object zoneGroup = Activator.CreateInstance(zoneGroupType);
        Assert.AreEqual("Front, Middle", GetField(zoneGroup, "rows").ToString());
        Assert.AreEqual("All", GetField(zoneGroup, "columns").ToString());

        Type useSkillType = RequireTaskType("MonsterUseSkill");
        object useSkill = Activator.CreateInstance(useSkillType);
        Assert.AreEqual(true, GetSharedValue(GetField(useSkill, "checkReleaseConditions")));

        Type chaseTargetType = RequireTaskType("MonsterChaseTarget");
        object chaseTarget = Activator.CreateInstance(chaseTargetType);
        Assert.AreEqual(false, GetSharedValue(GetField(chaseTarget, "waitUntilArrived")));
        Assert.AreEqual(2.5f, (float)GetSharedValue(GetField(chaseTarget, "stuckSeconds")), 0.001f);
        Assert.AreEqual(0.1f, (float)GetSharedValue(GetField(chaseTarget, "minProgressDistance")), 0.001f);
        Assert.AreEqual(8f, (float)GetSharedValue(GetField(chaseTarget, "moveTimeoutSeconds")), 0.001f);

        object moveZone = Activator.CreateInstance(RequireTaskType("MonsterMoveToBattleZoneGroup"));
        Assert.AreEqual(2.5f, (float)GetSharedValue(GetField(moveZone, "stuckSeconds")), 0.001f);
    }

    [Test]
    public void ConditionsReturnFailureWhenBindingIsMissing()
    {
        object hasTarget = Activator.CreateInstance(RequireTaskType("MonsterHasTarget"));
        object skillUsable = Activator.CreateInstance(RequireTaskType("MonsterSkillUsable"));

        AssertTaskStatus("Failure", Invoke(hasTarget, "OnUpdate"));
        AssertTaskStatus("Failure", Invoke(skillUsable, "OnUpdate"));
    }

    [Test]
    public void ActionsReturnFailureWhenBindingOrTargetIsMissing()
    {
        object findTarget = Activator.CreateInstance(RequireTaskType("MonsterFindTargetByTag"));
        object chaseTarget = Activator.CreateInstance(RequireTaskType("MonsterChaseTarget"));
        object useSkill = Activator.CreateInstance(RequireTaskType("MonsterUseSkill"));

        AssertTaskStatus("Failure", Invoke(findTarget, "OnUpdate"));
        AssertTaskStatus("Failure", Invoke(chaseTarget, "OnUpdate"));
        Invoke(useSkill, "OnStart");
        AssertTaskStatus("Failure", Invoke(useSkill, "OnUpdate"));
    }

    [Test]
    public void BattleZoneEnumSelectionBuildsStableZoneIds()
    {
        Type rowsType = RequireTaskType("MonsterMoveToBattleZoneGroup").Assembly.GetType(
            "NewFPG.Monsters.BehaviorDesigner.MonsterBattleZoneRows",
            true);
        Type columnsType = RequireTaskType("MonsterMoveToBattleZoneGroup").Assembly.GetType(
            "NewFPG.Monsters.BehaviorDesigner.MonsterBattleZoneColumns",
            true);
        Type utilityType = RequireTaskType("MonsterMoveToBattleZoneGroup").Assembly.GetType(
            "NewFPG.Monsters.BehaviorDesigner.MonsterBattleZoneSelectionUtility",
            true);
        Type listType = typeof(System.Collections.Generic.List<string>);

        object approachRows = Enum.Parse(rowsType, "Front, Middle");
        object allColumns = Enum.Parse(columnsType, "All");
        object results = Activator.CreateInstance(listType);
        MethodInfo build = utilityType.GetMethod(
            "TryBuildZoneIds",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { rowsType, columnsType, listType },
            null);
        Assert.IsNotNull(build);
        Assert.AreEqual(true, build.Invoke(null, new[] { approachRows, allColumns, results }));
        CollectionAssert.AreEqual(
            new[]
            {
                "left_front",
                "center_front",
                "right_front",
                "left_mid",
                "center_mid",
                "right_mid",
            },
            (System.Collections.IEnumerable)results);

        object retreatRows = Enum.Parse(rowsType, "Back");
        results = Activator.CreateInstance(listType);
        Assert.AreEqual(true, build.Invoke(null, new[] { retreatRows, allColumns, results }));
        CollectionAssert.AreEqual(
            new[] { "left_back", "center_back", "right_back" },
            (System.Collections.IEnumerable)results);
    }

    [Test]
    public void MonsterConfigBindingOnlyConsumesBehaviorTreeMovementCommands()
    {
        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Assets/Scripts/Monsters/MonsterConfigBinding.cs");
        string source = File.ReadAllText(path);
        int methodStart = source.IndexOf("private bool TryResolveDestination", StringComparison.Ordinal);
        Assert.GreaterOrEqual(methodStart, 0);

        int methodEnd = source.IndexOf("private bool TryPickPatrolDestination", methodStart, StringComparison.Ordinal);
        Assert.Greater(methodEnd, methodStart);

        string methodBody = source.Substring(methodStart, methodEnd - methodStart);
        StringAssert.Contains("行为树节点是唯一能下发移动目的地的入口", methodBody);
        StringAssert.Contains("if (hasManualDestination)", methodBody);
        Assert.False(methodBody.Contains("Transform currentTarget"), "不能在绑定层自动从 Target 派生追踪目的地。");
        Assert.False(methodBody.Contains("patrolWhenNoTarget"), "不能在绑定层自动从无目标巡逻派生移动目的地。");
    }

    [Test]
    public void MonsterConfigBindingExposesHomeAndStuckMovementAtoms()
    {
        Type bindingType = RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp");

        AssertPublicMethod(bindingType, "SetHomePositionToCurrent");
        AssertPublicMethod(bindingType, "SetHomePosition", typeof(UnityEngine.Vector3));
        AssertPublicMethod(bindingType, "IsCurrentMoveStuck", typeof(float), typeof(float));
        AssertPublicMethod(bindingType, "HasCurrentMoveTimedOut", typeof(float));
        Assert.IsNotNull(bindingType.GetProperty("HomePosition", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(bindingType.GetProperty("HasActiveMoveCommand", BindingFlags.Instance | BindingFlags.Public));
    }

    private static Type RequireTaskType(string typeName)
    {
        return RequireType("NewFPG.Monsters.BehaviorDesigner." + typeName + ", Assembly-CSharp");
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static void AssertPublicMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        Assert.IsNotNull(
            type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null),
            type.Name + "." + methodName + " should be public.");
    }

    private static void AssertAttributeContains(Type type, string attributeName, string expected)
    {
        object[] attributes = type.GetCustomAttributes(false);
        for (int i = 0; i < attributes.Length; i++)
        {
            object attribute = attributes[i];
            if (attribute.GetType().Name != attributeName)
            {
                continue;
            }

            if (AttributeContains(attribute, expected))
            {
                return;
            }
        }

        Assert.Fail(type.Name + " should have " + attributeName + " containing " + expected + ".");
    }

    private static void AssertAttributeHasChinese(Type type, string attributeName)
    {
        object[] attributes = type.GetCustomAttributes(false);
        for (int i = 0; i < attributes.Length; i++)
        {
            object attribute = attributes[i];
            if (attribute.GetType().Name == attributeName && AttributeHasChinese(attribute))
            {
                return;
            }
        }

        Assert.Fail(type.Name + " should have " + attributeName + " with Chinese text.");
    }

    private static bool AttributeContains(object attribute, string expected)
    {
        Type attributeType = attribute.GetType();
        FieldInfo[] fields = attributeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].GetValue(attribute) is string text && text.Contains(expected))
            {
                return true;
            }
        }

        PropertyInfo[] properties = attributeType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < properties.Length; i++)
        {
            if (properties[i].GetIndexParameters().Length == 0
                && properties[i].GetValue(attribute, null) is string text
                && text.Contains(expected))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AttributeHasChinese(object attribute)
    {
        Type attributeType = attribute.GetType();
        FieldInfo[] fields = attributeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].GetValue(attribute) is string text && ContainsChinese(text))
            {
                return true;
            }
        }

        PropertyInfo[] properties = attributeType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < properties.Length; i++)
        {
            if (properties[i].GetIndexParameters().Length == 0
                && properties[i].GetValue(attribute, null) is string text
                && ContainsChinese(text))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsChinese(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] >= '\u4e00' && text[i] <= '\u9fff')
            {
                return true;
            }
        }

        return false;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static object GetSharedValue(object sharedVariable)
    {
        PropertyInfo valueProperty = sharedVariable.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(valueProperty, sharedVariable.GetType().Name + ".Value should exist.");
        return valueProperty.GetValue(sharedVariable, null);
    }

    private static object Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, null);
    }

    private static void AssertTaskStatus(string expectedName, object status)
    {
        Assert.IsNotNull(status);
        Assert.AreEqual(expectedName, status.ToString());
    }
}
