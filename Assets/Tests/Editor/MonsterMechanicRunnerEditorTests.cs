using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MonsterMechanicRunnerEditorTests
{
    private GameObject monster;

    [TearDown]
    public void TearDown()
    {
        if (monster != null)
        {
            UnityEngine.Object.DestroyImmediate(monster);
        }
    }

    [Test]
    public void InvincibleMechanicBlocksCombatVitalsDamage()
    {
        Component runtime;
        Component vitals;
        Component runner = CreateMonster(out runtime, out vitals, out _);
        object mechanic = CreateMechanic("invincible", 0f, 1f, 0f);

        Invoke(runner, "ExecuteNow", mechanic, null, null);
        Invoke(vitals, "ReceiveDamage", CreateDamagePayload(25f));

        Assert.AreEqual(100f, (float)GetProperty(vitals, "CurrentHealth"), 0.001f);
        Assert.IsTrue((bool)GetProperty(runtime, "IsInvincible"));
    }

    [Test]
    public void InvisibleMechanicUpdatesStateAndRendererVisibility()
    {
        Component runtime;
        Component runner = CreateMonster(out runtime, out _, out SpriteRenderer renderer);
        object mechanic = CreateMechanic("invisible", 0f, 1f, 0f);

        Invoke(runner, "ExecuteNow", mechanic, null, null);

        Assert.IsTrue((bool)GetProperty(runtime, "IsInvisible"));
        Assert.IsFalse(renderer.enabled);
    }

    [Test]
    public void DamageAreaMechanicSkipsUntargetableDamageable()
    {
        Component runner = CreateMonster(out _, out _, out _);
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject stateHolder = new GameObject("Untargetable State Holder");
        try
        {
            target.name = "Untargetable Damage Area Target";
            target.transform.position = monster.transform.position + Vector3.forward * 0.25f;
            Component vitals = target.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
            ApplyVitalsSettings(vitals, 100f);

            Component state = stateHolder.AddComponent(RequireType("NewFPG.Monsters.MonsterState, Assembly-CSharp"));
            SetField(state, "invisibleStacks", 1);
            SetField(vitals, "monsterState", state);

            object mechanic = CreateMechanic("damage_area", 25f, 0f, 0f);
            Physics.SyncTransforms();

            Invoke(runner, "ExecuteNow", mechanic, target.transform, vitals);

            Assert.IsFalse((bool)GetProperty(vitals, "IsTargetable"));
            Assert.AreEqual(100f, (float)GetProperty(vitals, "CurrentHealth"), 0.001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(stateHolder);
        }
    }

    [Test]
    public void ScaleAndSpeedMechanicsUpdateReusableState()
    {
        Component runtime;
        Component runner = CreateMonster(out runtime, out _, out _);

        Invoke(runner, "ExecuteNow", CreateMechanic("scale_modifier", 2f, 1f, 0f), null, null);
        Invoke(runner, "ExecuteNow", CreateMechanic("speed_modifier", 1.5f, 1f, 0f), null, null);

        Assert.AreEqual(2f, (float)GetProperty(runtime, "ScaleMultiplier"), 0.001f);
        Assert.AreEqual(1.5f, (float)GetProperty(runtime, "SpeedMultiplier"), 0.001f);
        Assert.AreEqual(Vector3.one * 2f, monster.transform.localScale);
    }

    private Component CreateMonster(out Component runtime, out Component vitals, out SpriteRenderer renderer)
    {
        monster = new GameObject("Monster Mechanic Runner Test");
        renderer = monster.AddComponent<SpriteRenderer>();
        monster.AddComponent(RequireType("Pathfinding.Seeker, AstarPathfindingProject"));
        monster.AddComponent(RequireType("Pathfinding.AIPath, AstarPathfindingProject"));
        vitals = monster.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        runtime = monster.AddComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));

        InvokePrivate(runtime, "Awake");
        InvokePrivate(vitals, "Awake");
        ApplyVitalsSettings(vitals, 100f);
        return runtime;
    }

    private static object CreateMechanic(string type, float value, float duration, float delay)
    {
        object mechanic = Activator.CreateInstance(RequireType("NewFPG.Monsters.MonsterMechanicDefinition, Assembly-CSharp"));
        SetField(mechanic, "type", type);
        SetField(mechanic, "value", value);
        SetField(mechanic, "duration", duration);
        SetField(mechanic, "delay", delay);
        SetField(mechanic, "radius", 1f);
        return mechanic;
    }

    private static object CreateDamagePayload(float amount)
    {
        Type damagePayloadType = RequireType("NewFPG.Combat.DamagePayload, Assembly-CSharp");
        return Activator.CreateInstance(damagePayloadType, amount, null, Vector3.zero);
    }

    private static void ApplyVitalsSettings(Component vitals, float health)
    {
        object settings = Activator.CreateInstance(RequireType("NewFPG.Combat.CombatVitalsSettings, Assembly-CSharp"));
        SetField(settings, "maxHealth", health);
        SetField(settings, "startingHealth", health);
        SetField(settings, "destroyOnDeath", false);
        Invoke(vitals, "ApplySettings", settings, true);
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        return property.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }
}
