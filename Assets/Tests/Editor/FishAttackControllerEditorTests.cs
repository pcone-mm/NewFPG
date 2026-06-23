using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FishAttackControllerEditorTests
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

        Component attack = CreateFishAttack("Fish Attack Disabled Collider Fish");
        ConfigureAttack(attack, player.transform, 5f, 1.3f, 12f, 0);

        Invoke(attack, "RequestAttack");
        InvokePrivate(attack, "CompleteAttack");

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

        Component attack = CreateFishAttack("Fish Attack Multi Collider Fish");
        ConfigureAttack(attack, player.transform, 5f, 2f, 12f, ~0);

        Invoke(attack, "RequestAttack");
        InvokePrivate(attack, "CompleteAttack");

        Assert.AreEqual(88f, (float)GetProperty(vitals, "CurrentHealth"), 0.001f);
    }

    [Test]
    public void RequestAttackCreatesWorldSpaceWarningIndicator()
    {
        CreatePlayerVitals("Fish Attack Warning Player", out GameObject player);
        Component attack = CreateFishAttack("Fish Attack Warning Fish");
        attack.transform.localScale = Vector3.one * 0.35f;
        ConfigureAttack(attack, player.transform, 5f, 1.15f, 12f, 0);
        SetField(attack, "warningHeightOffset", 1.2f);

        Invoke(attack, "RequestAttack");

        Component warning = (Component)GetField(attack, "warningIndicator");
        Assert.IsNotNull(warning);
        Assert.IsTrue(warning.gameObject.activeSelf);
        Assert.IsNull(warning.transform.parent);
        Assert.AreEqual(attack.transform.position.x, warning.transform.position.x, 0.001f);
        Assert.AreEqual(attack.transform.position.y + 1.2f, warning.transform.position.y, 0.001f);
        Assert.AreEqual(attack.transform.position.z, warning.transform.position.z, 0.001f);
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
        Component attack = CreateFishAttack("Fish Attack Follow Warning Fish");
        attack.transform.localScale = new Vector3(0.2f, 3f, 0.5f);
        ConfigureAttack(attack, player.transform, 5f, 1.15f, 12f, 0);
        SetField(attack, "warningHeightOffset", 1.2f);

        Invoke(attack, "RequestAttack");
        Component warning = (Component)GetField(attack, "warningIndicator");

        Vector3 nextFishPosition = new Vector3(2.5f, 0f, -1.75f);
        attack.transform.position = nextFishPosition;
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
        Component attack = CreateFishAttack("Fish Attack Height Warning Fish");
        ConfigureAttack(attack, player.transform, 5f, 1.15f, 12f, 0);
        SetField(attack, "warningHeightOffset", 2.4f);

        Invoke(attack, "RequestAttack");

        Component warning = (Component)GetField(attack, "warningIndicator");
        Assert.AreEqual(attack.transform.position.y + 2.4f, warning.transform.position.y, 0.001f);
    }

    [Test]
    public void WarningIndicatorFacesAssignedCamera()
    {
        CreatePlayerVitals("Fish Attack Camera Warning Player", out GameObject player);
        Component attack = CreateFishAttack("Fish Attack Camera Warning Fish");
        ConfigureAttack(attack, player.transform, 5f, 1.15f, 12f, 0);

        Invoke(attack, "RequestAttack");
        Component warning = (Component)GetField(attack, "warningIndicator");

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
        SetField(vitals, "maxHealth", 100f);
        SetField(vitals, "startingHealth", 100f);
        SetField(vitals, "maxShield", 0f);
        SetField(vitals, "startingShield", 0f);
        Invoke(vitals, "ResetVitals");
        return vitals;
    }

    private Component CreateFishAttack(string name)
    {
        GameObject fish = CreateTrackedGameObject(name);
        fish.transform.position = new Vector3(0f, 0f, 0.4f);
        fish.AddComponent<SpriteRenderer>();
        fish.AddComponent<Rigidbody>();
        fish.AddComponent<BoxCollider>();
        fish.AddComponent(RequireType("NewFPG.Monsters.FishMonsterController, Assembly-CSharp"));
        Component attack = fish.AddComponent(RequireType("NewFPG.Combat.FishAttackController, Assembly-CSharp"));
        InvokePrivate(attack, "Awake");
        InvokePrivate(attack, "OnEnable");
        return attack;
    }

    private static void ConfigureAttack(Component attack, Transform target, float range, float radius, float damage, int maskValue)
    {
        SetField(attack, "target", target);
        SetField(attack, "autoFindPlayer", false);
        SetField(attack, "attackRange", range);
        SetField(attack, "attackPrepareTime", 0.8f);
        SetField(attack, "damageRadius", radius);
        SetField(attack, "damage", damage);
        SetField(attack, "targetMask", new LayerMask { value = maskValue });
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
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static void DestroyRuntimeWarnings()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = objects.Length - 1; i >= 0; i--)
        {
            if (objects[i] != null
                && objects[i].name == "FishAttackWarning"
                && !EditorUtility.IsPersistent(objects[i]))
            {
                UnityEngine.Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
