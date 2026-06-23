using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ArtifactCycleSystemEditorTests
{
    private static object capturedRelease;

    [Test]
    public void QueueStateInitializesThreeVisibleArtifactsAndBacklog()
    {
        object queue = BuildQueue();
        IList equippedArtifacts = GetList(queue, "equippedArtifacts");
        IList activeArtifacts = GetList(queue, "activeArtifacts");
        IList drawPile = GetList(queue, "drawPile");

        Assert.AreEqual(7, equippedArtifacts.Count);
        Assert.AreEqual(3, activeArtifacts.Count);
        Assert.AreEqual(4, drawPile.Count);
        Assert.AreSame(equippedArtifacts[0], activeArtifacts[0]);
        Assert.AreSame(equippedArtifacts[1], activeArtifacts[1]);
        Assert.AreSame(equippedArtifacts[2], activeArtifacts[2]);
        Assert.AreSame(equippedArtifacts[3], drawPile[0]);
    }

    [Test]
    public void ReleasedVisibleArtifactCyclesToBacklogAndDrawsNextArtifactIntoSameSlot()
    {
        object queue = BuildQueue();
        IList equippedArtifacts = GetList(queue, "equippedArtifacts");
        IList activeArtifacts = GetList(queue, "activeArtifacts");
        IList drawPile = GetList(queue, "drawPile");
        object used = activeArtifacts[0];

        MethodInfo tryCycle = queue.GetType().GetMethod("TryCycleAfterRelease", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(tryCycle, "ArtifactQueueState.TryCycleAfterRelease should exist.");
        object[] args = { used, null, -1 };
        bool cycled = (bool)tryCycle.Invoke(queue, args);
        object drawn = args[1];
        int slotIndex = (int)args[2];

        Assert.IsTrue(cycled);
        Assert.AreEqual(0, slotIndex);
        Assert.AreSame(equippedArtifacts[3], drawn);
        Assert.AreSame(drawn, activeArtifacts[0]);
        Assert.AreSame(equippedArtifacts[1], activeArtifacts[1]);
        Assert.AreSame(equippedArtifacts[2], activeArtifacts[2]);
        Assert.AreSame(used, drawPile[drawPile.Count - 1]);
    }

    [Test]
    public void AutoReleaseOnlyAttemptsVisibleArtifactsThenCyclesAfterSuccessfulRelease()
    {
        object queue = BuildQueue();
        IList activeArtifacts = GetList(queue, "activeArtifacts");
        for (int i = 0; i < 3; i++)
        {
            SetField(activeArtifacts[i], "cooldownRemaining", 0f);
            SetField(activeArtifacts[i], "isReady", true);
        }

        Type systemType = BattleType("ArtifactAutoReleaseSystem");
        object system = Activator.CreateInstance(systemType);
        capturedRelease = null;
        SubscribeCapture(system, "Released");

        Type contextType = BattleType("BattleSessionContext");
        Type enemyType = BattleType("EnemyCombatState");
        object context = Activator.CreateInstance(contextType);
        SetField(context, "isBattleRunning", true);
        SetField(context, "totalAutoEnabled", true);
        SetField(context, "playerPosition", Vector3.zero);
        SetField(context, "playerHp", 100f);
        SetField(context, "playerMaxHp", 100f);
        SetField(context, "artifactQueue", queue);

        IList enemies = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(enemyType));
        object enemy = Activator.CreateInstance(enemyType);
        SetField(enemy, "enemyId", "target");
        SetField(enemy, "displayName", "Target");
        SetField(enemy, "hp", 100f);
        SetField(enemy, "maxHp", 100f);
        SetField(enemy, "position", Vector3.forward);
        SetField(enemy, "isTargetable", true);
        enemies.Add(enemy);
        SetField(context, "enemies", enemies);

        Invoke(system, "Tick", context, 0f);

        IList equippedArtifacts = GetList(queue, "equippedArtifacts");
        IList drawPile = GetList(queue, "drawPile");
        Assert.IsNotNull(capturedRelease);
        Assert.AreSame(equippedArtifacts[0], GetField(capturedRelease, "artifact"));
        Assert.AreSame(equippedArtifacts[3], GetField(capturedRelease, "drawnArtifact"));
        Assert.AreEqual(0, GetField(capturedRelease, "cycledSlotIndex"));
        Assert.AreEqual(3, activeArtifacts.Count);
        Assert.AreSame(equippedArtifacts[3], activeArtifacts[0]);
        Assert.AreSame(equippedArtifacts[0], drawPile[drawPile.Count - 1]);
    }

    private static object BuildQueue()
    {
        object catalog = InvokeStatic(BattleType("ArtifactCatalog"), "CreateDefault");
        object loadout = Activator.CreateInstance(BattleType("PrepLoadoutState"));
        SetProperty(loadout, "Capacity", 10);
        Invoke(
            loadout,
            "SetArtifacts",
            new object[]
            {
                new[]
                {
                    "zhanfeng_short_blade",
                    "shuangshui_needle",
                    "baoyan_talisman",
                    "jingshui_amulet",
                    "fumu_bell",
                    "duanjin_ring",
                    "qingmu_heal_orb",
                },
            });

        return Invoke(loadout, "ToQueueState", catalog);
    }

    private static Type BattleType(string typeName)
    {
        Type type = Type.GetType("NewFPG.Battle." + typeName + ", Assembly-CSharp", true);
        Assert.IsNotNull(type, typeName + " should resolve.");
        return type;
    }

    private static IList GetList(object target, string fieldName)
    {
        IList list = GetField(target, fieldName) as IList;
        Assert.IsNotNull(list, target.GetType().Name + "." + fieldName + " should be a list.");
        return list;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        property.SetValue(target, value);
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object InvokeStatic(Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, type.Name + "." + methodName + " should exist.");
        return method.Invoke(null, args);
    }

    private static void SubscribeCapture(object publisher, string eventName)
    {
        EventInfo eventInfo = publisher.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(eventInfo, publisher.GetType().Name + "." + eventName + " should exist.");
        eventInfo.AddEventHandler(publisher, CreateCaptureDelegate(eventInfo.EventHandlerType));
    }

    private static Delegate CreateCaptureDelegate(Type eventHandlerType)
    {
        MethodInfo invoke = eventHandlerType.GetMethod("Invoke");
        ParameterInfo[] parameters = invoke.GetParameters();
        ParameterExpression[] parameterExpressions = new ParameterExpression[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            parameterExpressions[i] = Expression.Parameter(parameters[i].ParameterType, parameters[i].Name);
        }

        FieldInfo capturedField = typeof(ArtifactCycleSystemEditorTests).GetField(
            nameof(capturedRelease),
            BindingFlags.Static | BindingFlags.NonPublic);
        Expression capturedValue = parameters.Length > 0
            ? Expression.Convert(parameterExpressions[0], typeof(object))
            : Expression.Constant(null, typeof(object));
        BinaryExpression assign = Expression.Assign(Expression.Field(null, capturedField), capturedValue);
        return Expression.Lambda(eventHandlerType, assign, parameterExpressions).Compile();
    }
}
