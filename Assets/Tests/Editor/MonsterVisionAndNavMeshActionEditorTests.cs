using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class MonsterVisionAndNavMeshActionEditorTests
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
    }

    [Test]
    public void VisionUtilityRequiresPointInsideCameraViewport()
    {
        Camera camera = CreateCamera();

        Assert.IsTrue(IsPointVisible(camera, new Vector3(0f, 0f, 4f), ~0));
        Assert.IsFalse(IsPointVisible(camera, new Vector3(8f, 0f, 4f), ~0));
    }

    [Test]
    public void VisionUtilityRejectsObstructedCameraLine()
    {
        Camera camera = CreateCamera();
        GameObject blocker = CreateTrackedGameObject("Monster Vision Blocker");
        blocker.transform.position = new Vector3(0f, 0f, 2f);
        blocker.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.5f);
        Physics.SyncTransforms();

        Assert.IsFalse(IsPointVisible(camera, new Vector3(0f, 0f, 4f), ~0));
    }

    [Test]
    public void VisionUtilityRejectsGrazingObstructionWithProbeRadius()
    {
        Camera camera = CreateCamera(new Vector3(0f, 1f, -4f));
        GameObject blocker = CreateTrackedGameObject("Monster Vision Grazing Blocker");
        blocker.transform.position = new Vector3(0f, 0f, 0f);
        blocker.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.5f);
        Physics.SyncTransforms();

        Assert.IsFalse(IsPointVisible(camera, new Vector3(0f, 1.05f, 4f), ~0));
    }

    [Test]
    public void TransformVisibilityRequiresBodySampleLineOfSight()
    {
        Camera camera = CreateCamera(new Vector3(0f, 1f, -4f));
        GameObject target = CreateTrackedGameObject("Monster Vision Target");
        target.transform.position = new Vector3(0f, 0f, 4f);
        target.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 0.5f);

        GameObject blocker = CreateTrackedGameObject("Monster Vision Body Blocker");
        blocker.transform.position = new Vector3(0f, 0f, 2f);
        blocker.AddComponent<BoxCollider>().size = new Vector3(2f, 1.4f, 0.5f);
        Physics.SyncTransforms();

        Assert.IsFalse(IsTransformVisible(camera, target.transform, 1f, ~0, null));
    }

    [Test]
    public void VisionUtilityUsesConfiguredObstructionMaskOnly()
    {
        int weaponLayer = LayerMask.NameToLayer("FirstPersonWeapon");
        int blockingLayer = LayerMask.NameToLayer("AstarBlocking");
        Assert.GreaterOrEqual(weaponLayer, 0, "FirstPersonWeapon layer should exist.");
        Assert.GreaterOrEqual(blockingLayer, 0, "AstarBlocking layer should exist.");

        Camera camera = CreateCamera(new Vector3(0f, 1f, -4f));
        GameObject target = CreateTrackedGameObject("Monster Vision Mask Target");
        target.transform.position = new Vector3(0f, 0f, 4f);
        target.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 0.5f);

        GameObject weaponCollider = CreateTrackedGameObject("Monster Vision Weapon Collider");
        weaponCollider.layer = weaponLayer;
        weaponCollider.transform.position = new Vector3(0f, 1f, -1f);
        weaponCollider.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.5f);
        Physics.SyncTransforms();

        int blockingMask = 1 << blockingLayer;
        Assert.IsTrue(
            IsTransformVisible(camera, target.transform, 1f, blockingMask, null),
            "FirstPersonWeapon should not block monster line of sight when the mask only includes AstarBlocking.");

        GameObject blocker = CreateTrackedGameObject("Monster Vision Astar Blocking Collider");
        blocker.layer = blockingLayer;
        blocker.transform.position = new Vector3(0f, 1f, 1f);
        blocker.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.5f);
        Physics.SyncTransforms();

        Assert.IsFalse(
            IsTransformVisible(camera, target.transform, 1f, blockingMask, null),
            "AstarBlocking should block monster line of sight.");
    }

    [Test]
    public void CameraDistanceBandsNormalizeAndCheckHorizontalDistance()
    {
        Type bandType = RequireType("NewFPG.Monsters.MonsterCameraDistanceBandDefinition, Assembly-CSharp");
        object band = Activator.CreateInstance(bandType, "mid", 4f, 6f);

        Assert.IsTrue((bool)Invoke(band, "ContainsHorizontalDistance", Vector3.zero, new Vector3(0f, 10f, 5f)));
        Assert.IsFalse((bool)Invoke(band, "ContainsHorizontalDistance", Vector3.zero, new Vector3(0f, 0f, 7f)));
    }

    [Test]
    public void BattleZoneSamplerDefaultsToRandomReachableMode()
    {
        Type samplerType = RequireType("NewFPG.Monsters.BattleZoneSampler, Assembly-CSharp");
        MethodInfo method = samplerType.GetMethod("NormalizeSampleMode", BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method);

        Assert.AreEqual("random_reachable", method.Invoke(null, new object[] { string.Empty }));
        Assert.AreEqual("custom_mode", method.Invoke(null, new object[] { "custom_mode" }));
    }

    [Test]
    public void BattleZoneGroupsExpandRowsColumnsAndDirectZoneIds()
    {
        Type movementType = RequireType("NewFPG.Monsters.MonsterMovementDefinition, Assembly-CSharp");
        object movement = Activator.CreateInstance(movementType);
        Invoke(movement, "Normalize");

        List<string> results = new List<string>();
        Assert.IsTrue(TryExpandBattleZoneGroups(movement, new[] { "near", "mid" }, results));
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
            results);

        Assert.IsTrue(TryExpandBattleZoneGroups(movement, new[] { "far" }, results));
        CollectionAssert.AreEqual(new[] { "left_back", "center_back", "right_back" }, results);

        Assert.IsTrue(TryExpandBattleZoneGroups(movement, new[] { "left_front" }, results));
        CollectionAssert.AreEqual(new[] { "left_front" }, results);

        Assert.IsTrue(TryExpandBattleZoneGroups(movement, new[] { "left", "center" }, results));
        CollectionAssert.AreEqual(
            new[]
            {
                "left_front",
                "left_mid",
                "left_back",
                "center_front",
                "center_mid",
                "center_back",
            },
            results);
    }

    [Test]
    public void SkillDefinitionKeepsReleaseConditionsOnTheSkill()
    {
        Type skillType = RequireType("NewFPG.Monsters.MonsterSkillDefinition, Assembly-CSharp");
        object skill = Activator.CreateInstance(skillType);

        Assert.AreEqual(2.2f, (float)GetField(skill, "castRange"), 0.001f);
        Assert.AreEqual(true, GetField(skill, "requireLineOfSight"));
        Assert.AreEqual(2048, GetField(skill, "lineOfSightMask"));
        Assert.AreEqual(1f, (float)GetField(skill, "lineOfSightHeightOffset"), 0.001f);
    }

    [Test]
    public void RefreshTargetByTagWithZeroRadiusFindsFarPlayer()
    {
        GameObject monster = CreateMonsterBindingObject("Monster Unlimited Target Search Fish");
        Component binding = monster.GetComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));
        SetProperty(binding, "DetectionRadius", 0f);

        GameObject player = CreateTrackedGameObject("Monster Unlimited Target Search Player");
        player.tag = "Player";
        player.transform.position = new Vector3(50f, 0f, 0f);

        bool found = (bool)Invoke(binding, "RefreshTargetByTag", "Player", 0f, true);

        Assert.IsTrue(found, "detectionRadius=0 should mean no distance limit for combat target search.");
        Assert.AreSame(player.transform, GetProperty(binding, "Target"));
    }

    [Test]
    public void MonsterMoveStateReportsStuckAndTimedOutCommands()
    {
        GameObject monster = CreateMonsterBindingObject("Monster Stuck Move State Fish");
        Component binding = monster.GetComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));
        SetField(binding, "hasActiveMoveCommand", true);
        SetField(binding, "currentMoveStartedAt", Time.time - 9f);
        SetField(binding, "currentMoveLastProgressAt", Time.time - 3f);
        SetField(binding, "currentMoveLastRemainingDistance", 5f);
        SetField(binding, "currentMoveDestination", new Vector3(5f, 0f, 0f));
        Behaviour aiPath = (Behaviour)monster.GetComponent(RequireType("Pathfinding.AIPath, AstarPathfindingProject"));
        aiPath.enabled = false;

        Assert.IsTrue((bool)Invoke(binding, "IsCurrentMoveStuck", 2.5f, 0.1f));
        Assert.IsTrue((bool)Invoke(binding, "HasCurrentMoveTimedOut", 8f));
    }

    private static bool TryExpandBattleZoneGroups(object movement, IReadOnlyList<string> zoneGroupsOrIds, List<string> results)
    {
        MethodInfo method = movement.GetType().GetMethod(
            "TryExpandBattleZoneGroups",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(IReadOnlyList<string>), typeof(List<string>) },
            null);
        Assert.IsNotNull(method);
        return (bool)method.Invoke(movement, new object[] { zoneGroupsOrIds, results });
    }

    private bool IsPointVisible(Camera camera, Vector3 point, int mask)
    {
        Type utility = RequireType("NewFPG.Monsters.MonsterVisionUtility, Assembly-CSharp");
        MethodInfo method = utility.GetMethod(
            "IsPointVisible",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Camera), typeof(Vector3), typeof(LayerMask), typeof(Transform), typeof(Transform) },
            null);
        Assert.IsNotNull(method);
        return (bool)method.Invoke(null, new object[] { camera, point, (LayerMask)mask, null, null });
    }

    private bool IsTransformVisible(Camera camera, Transform target, float heightOffset, int mask, Transform ignoredObserverRoot)
    {
        Type utility = RequireType("NewFPG.Monsters.MonsterVisionUtility, Assembly-CSharp");
        MethodInfo method = utility.GetMethod(
            "IsTransformVisible",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Camera), typeof(Transform), typeof(float), typeof(LayerMask), typeof(Transform) },
            null);
        Assert.IsNotNull(method);
        return (bool)method.Invoke(null, new object[] { camera, target, heightOffset, (LayerMask)mask, ignoredObserverRoot });
    }

    private Camera CreateCamera()
    {
        return CreateCamera(Vector3.zero);
    }

    private Camera CreateCamera(Vector3 position)
    {
        GameObject cameraObject = CreateTrackedGameObject("Monster Vision Test Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = position;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 30f;
        return camera;
    }

    private GameObject CreateTrackedGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        temporaryObjects.Add(gameObject);
        return gameObject;
    }

    private GameObject CreateMonsterBindingObject(string name)
    {
        GameObject monster = CreateTrackedGameObject(name);
        monster.SetActive(false);
        monster.AddComponent<SpriteRenderer>();
        monster.AddComponent<BoxCollider>();
        monster.AddComponent(RequireType("Pathfinding.Seeker, AstarPathfindingProject"));
        monster.AddComponent(RequireType("Pathfinding.AIPath, AstarPathfindingProject"));
        monster.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        Component binding = monster.AddComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));
        SetField(binding, "applyOnAwake", false);
        monster.SetActive(true);
        return monster;
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.Public, args);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static MethodInfo FindMethod(Type type, string methodName, BindingFlags flags, object[] args)
    {
        MethodInfo[] methods = type.GetMethods(flags);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            bool matches = true;
            for (int p = 0; p < parameters.Length; p++)
            {
                if (args[p] != null && !parameters[p].ParameterType.IsAssignableFrom(args[p].GetType()))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return method;
            }
        }

        return null;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        return property.GetValue(target);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        property.SetValue(target, value, null);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }
}
