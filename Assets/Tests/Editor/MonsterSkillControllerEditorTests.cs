using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MonsterSkillControllerEditorTests
{
    private readonly List<GameObject> temporaryObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
        {
            if (temporaryObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
            }
        }

        temporaryObjects.Clear();
        DestroyRuntimeWarnings();
    }

    [Test]
    public void CompleteAttackDamagesLockedTargetEvenWhenTargetColliderIsDisabled()
    {
        Component vitals = CreatePlayerVitals("Fish Attack Disabled Collider Player", out GameObject player);
        Collider collider = player.AddComponent<CapsuleCollider>();
        collider.enabled = false;

        Component runtime = CreateMonsterRuntime("Fish Attack Disabled Collider Fish");
        ConfigureSkill(runtime, player.transform, 5f, 1.3f, 12f, 0);

        Invoke(runtime, "TryUseSkill", "melee_bite", player.transform);
        CompleteAttackMechanics(runtime, player.transform);

        Assert.AreEqual(88f, (float)GetProperty(vitals, "CurrentHealth"), 0.001f);
    }

    [Test]
    public void CompleteAttackOnlyDamagesEachTargetOnceWhenMultipleCollidersOverlap()
    {
        Component vitals = CreatePlayerVitals("Fish Attack Multi Collider Player", out GameObject player);
        player.AddComponent<CapsuleCollider>();
        player.AddComponent<BoxCollider>();

        Physics.SyncTransforms();
        Assert.GreaterOrEqual(Physics.OverlapSphere(player.transform.position, 2f).Length, 2);

        Component runtime = CreateMonsterRuntime("Fish Attack Multi Collider Fish");
        ConfigureSkill(runtime, player.transform, 5f, 2f, 12f, ~0);

        Invoke(runtime, "TryUseSkill", "melee_bite", player.transform);
        CompleteAttackMechanics(runtime, player.transform);

        Assert.AreEqual(88f, (float)GetProperty(vitals, "CurrentHealth"), 0.001f);
    }

    [Test]
    public void CompleteAttackDoesNotDamageOtherMonstersInArea()
    {
        Component playerVitals = CreatePlayerVitals("Fish Attack Friendly Fire Player", out GameObject player);
        player.AddComponent<CapsuleCollider>();
        Component nearbyMonsterVitals = CreateMonsterVitals(
            "Fish Attack Nearby Monster",
            new Vector3(0.25f, 0f, 0.1f),
            out _);

        Component runtime = CreateMonsterRuntime("Fish Attack Friendly Fire Fish");
        ConfigureSkill(runtime, player.transform, 5f, 2f, 12f, ~0);

        Physics.SyncTransforms();
        Invoke(runtime, "TryUseSkill", "melee_bite", player.transform);
        CompleteAttackMechanics(runtime, player.transform);

        Assert.AreEqual(88f, (float)GetProperty(playerVitals, "CurrentHealth"), 0.001f);
        Assert.AreEqual(100f, (float)GetProperty(nearbyMonsterVitals, "CurrentHealth"), 0.001f);
        Assert.AreEqual(0f, (float)GetProperty(nearbyMonsterVitals, "CurrentShield"), 0.001f);
    }

    [Test]
    public void RequestAttackCreatesWorldSpaceWarningIndicator()
    {
        CreatePlayerVitals("Fish Attack Warning Player", out GameObject player);
        Component runtime = CreateMonsterRuntime("Fish Attack Warning Fish");
        runtime.transform.localScale = Vector3.one * 0.35f;
        ConfigureSkill(runtime, player.transform, 5f, 1.15f, 12f, 0);

        Invoke(runtime, "TryUseSkill", "melee_bite", player.transform);

        Component warning = (Component)GetField(runtime, "warningIndicator");
        Assert.IsNotNull(warning);
        Assert.IsTrue(warning.gameObject.activeSelf);
        Assert.IsNull(warning.transform.parent);
        Assert.AreEqual(runtime.transform.position.x, warning.transform.position.x, 0.001f);
        Assert.AreEqual(runtime.transform.position.y + 1.2f, warning.transform.position.y, 0.001f);
        Assert.AreEqual(runtime.transform.position.z, warning.transform.position.z, 0.001f);
        Assert.AreEqual(Vector3.one, warning.transform.localScale);

        LineRenderer renderer = warning.GetComponent<LineRenderer>();
        Assert.IsNotNull(renderer);
        Assert.IsFalse(renderer.useWorldSpace);
        Assert.Greater(renderer.positionCount, 8);
        Assert.Greater(renderer.widthMultiplier, 0f);
    }

    [Test]
    public void WarningIndicatorFollowsFishWithoutInheritingFishScale()
    {
        CreatePlayerVitals("Fish Attack Follow Warning Player", out GameObject player);
        Component runtime = CreateMonsterRuntime("Fish Attack Follow Warning Fish");
        runtime.transform.localScale = new Vector3(0.2f, 3f, 0.5f);
        ConfigureSkill(runtime, player.transform, 5f, 1.15f, 12f, 0);

        Invoke(runtime, "TryUseSkill", "melee_bite", player.transform);
        Component warning = (Component)GetField(runtime, "warningIndicator");

        Vector3 nextFishPosition = new Vector3(2.5f, 0f, -1.75f);
        runtime.transform.position = nextFishPosition;
        InvokePrivate(warning, "Update");

        Assert.AreEqual(nextFishPosition.x, warning.transform.position.x, 0.001f);
        Assert.AreEqual(nextFishPosition.y + 1.2f, warning.transform.position.y, 0.001f);
        Assert.AreEqual(nextFishPosition.z, warning.transform.position.z, 0.001f);
        Assert.AreEqual(Vector3.one, warning.transform.localScale);
    }

    [Test]
    public void WarningHeightOffsetControlsWarningWorldHeight()
    {
        CreatePlayerVitals("Fish Attack Height Warning Player", out GameObject player);
        Component runtime = CreateMonsterRuntime("Fish Attack Height Warning Fish");
        ConfigureSkill(runtime, player.transform, 5f, 1.15f, 12f, 0, 2.4f);

        Invoke(runtime, "TryUseSkill", "melee_bite", player.transform);

        Component warning = (Component)GetField(runtime, "warningIndicator");
        Assert.AreEqual(runtime.transform.position.y + 2.4f, warning.transform.position.y, 0.001f);
    }

    [Test]
    public void WarningIndicatorFacesAssignedCamera()
    {
        CreatePlayerVitals("Fish Attack Camera Warning Player", out GameObject player);
        Component runtime = CreateMonsterRuntime("Fish Attack Camera Warning Fish");
        ConfigureSkill(runtime, player.transform, 5f, 1.15f, 12f, 0);

        Invoke(runtime, "TryUseSkill", "melee_bite", player.transform);
        Component warning = (Component)GetField(runtime, "warningIndicator");

        GameObject cameraObject = CreateTrackedGameObject("Fish Attack Warning Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 2f, -6f);
        camera.transform.rotation = Quaternion.LookRotation(warning.transform.position - camera.transform.position, Vector3.up);
        SetField(warning, "targetCamera", camera);

        InvokePrivate(warning, "Update");

        Vector3 expectedForward = (camera.transform.position - warning.transform.position).normalized;
        Assert.Greater(Vector3.Dot(warning.transform.forward, expectedForward), 0.999f);
    }

    private Component CreatePlayerVitals(string name, out GameObject player)
    {
        player = CreateTrackedGameObject(name);
        player.tag = "Player";
        player.transform.position = Vector3.zero;

        Component vitals = player.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        ApplyVitalsSettings(vitals, 100f, 0f);
        Invoke(vitals, "ResetVitals");
        return vitals;
    }

    private Component CreateMonsterVitals(string name, Vector3 position, out GameObject monster)
    {
        monster = CreateTrackedGameObject(name);
        monster.transform.position = position;
        monster.AddComponent<SpriteRenderer>();
        monster.AddComponent<BoxCollider>();

        Component vitals = monster.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        Component state = monster.AddComponent(RequireType("NewFPG.Monsters.MonsterState, Assembly-CSharp"));
        InvokePrivate(state, "Awake");
        InvokePrivate(vitals, "Awake");
        ApplyVitalsSettings(vitals, 100f, 0f);
        Invoke(vitals, "ResetVitals");
        return vitals;
    }

    private Component CreateMonsterRuntime(string name)
    {
        GameObject fish = CreateTrackedGameObject(name);
        fish.transform.position = new Vector3(0f, 0f, 0.4f);
        fish.AddComponent<SpriteRenderer>();
        fish.AddComponent(RequireType("Pathfinding.Seeker, AstarPathfindingProject"));
        fish.AddComponent(RequireType("Pathfinding.AIPath, AstarPathfindingProject"));
        fish.AddComponent<BoxCollider>();
        Component runtime = fish.AddComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));
        InvokePrivate(runtime, "Awake");
        return runtime;
    }

    private static void ConfigureSkill(Component skills, Transform target, float range, float radius, float damage, int maskValue, float warningHeightOffset = 1.2f)
    {
        object monsterDefinition = Activator.CreateInstance(RequireType("NewFPG.Monsters.MonsterDefinition, Assembly-CSharp"));
        object attackDefinition = GetField(monsterDefinition, "attack");
        SetField(attackDefinition, "autoFindPlayer", false);
        SetField(attackDefinition, "attackRange", range);
        SetField(attackDefinition, "attackPrepareTime", 0.8f);
        SetField(attackDefinition, "damageRadius", radius);
        SetField(attackDefinition, "damage", damage);
        SetField(attackDefinition, "targetMask", maskValue);
        SetField(attackDefinition, "warningHeightOffset", warningHeightOffset);

        object skill = Activator.CreateInstance(RequireType("NewFPG.Monsters.MonsterSkillDefinition, Assembly-CSharp"));
        SetField(skill, "skillId", "melee_bite");
        SetField(skill, "cooldown", 0f);
        SetField(skill, "windup", 0.8f);
        SetField(skill, "showWarning", true);
        SetField(skill, "warningHeightOffset", warningHeightOffset);
        object mechanic = Activator.CreateInstance(RequireType("NewFPG.Monsters.MonsterMechanicDefinition, Assembly-CSharp"));
        SetField(mechanic, "mechanicId", "bite_damage_area");
        SetField(mechanic, "type", "damage_area");
        SetField(mechanic, "value", damage);
        SetField(mechanic, "radius", radius);
        SetField(mechanic, "heightOffset", warningHeightOffset);
        SetField(mechanic, "targetMask", maskValue);
        SetField(mechanic, "affectSelf", false);

        Type listType = typeof(List<>).MakeGenericType(RequireType("NewFPG.Monsters.MonsterMechanicDefinition, Assembly-CSharp"));
        object mechanics = Activator.CreateInstance(listType);
        Invoke(mechanics, "Add", mechanic);
        SetField(skill, "mechanics", mechanics);

        Type skillListType = typeof(List<>).MakeGenericType(RequireType("NewFPG.Monsters.MonsterSkillDefinition, Assembly-CSharp"));
        object skillList = Activator.CreateInstance(skillListType);
        Invoke(skillList, "Add", skill);
        SetField(monsterDefinition, "skills", skillList);

        Invoke(skills, "ApplyDefinition", monsterDefinition);
    }

    private static void CompleteAttackMechanics(Component skills, Transform target)
    {
        object activeTarget = GetProperty(skills, "ActiveTarget");
        object skill = Invoke(skills, "GetSkill", "melee_bite");
        object mechanics = GetField(skill, "mechanics");
        object mechanic = Invoke(mechanics, "get_Item", 0);
        object lockedTarget = target.GetComponentInParent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        Invoke(skills, "ExecuteNow", mechanic, activeTarget, lockedTarget);
    }

    private GameObject CreateTrackedGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        temporaryObjects.Add(gameObject);
        return gameObject;
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

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        }

        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static void ApplyVitalsSettings(Component vitals, float health, float shield)
    {
        object settings = Activator.CreateInstance(RequireType("NewFPG.Combat.CombatVitalsSettings, Assembly-CSharp"));
        SetField(settings, "maxHealth", health);
        SetField(settings, "startingHealth", health);
        SetField(settings, "maxShield", shield);
        SetField(settings, "startingShield", shield);
        SetField(settings, "destroyOnDeath", false);
        Invoke(vitals, "ApplySettings", settings, true);
    }

    private static void DestroyRuntimeWarnings()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = objects.Length - 1; i >= 0; i--)
        {
            if (objects[i] != null
                && objects[i].name == "MonsterAttackWarning"
                && !EditorUtility.IsPersistent(objects[i]))
            {
                UnityEngine.Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
