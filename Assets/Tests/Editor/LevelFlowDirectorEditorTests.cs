using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LevelFlowDirectorEditorTests
{
    private GameObject directorObject;
    private readonly System.Collections.Generic.List<GameObject> temporaryObjects = new System.Collections.Generic.List<GameObject>();

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

        if (directorObject != null)
        {
            UnityEngine.Object.DestroyImmediate(directorObject);
        }
    }

    [Test]
    public void FirstFloorRouteStartsWithPreCombatChoiceRoom()
    {
        Component director = CreateDirector();

        InvokePrivate(director, "Awake");
        Invoke(director, "StartRoute");

        InvokePrivate(director, "StartRoomContent", GetProperty(director, "CurrentRoom"));

        Assert.AreEqual("AwaitingRoomInteraction", GetProperty(director, "State").ToString());
        object currentRoom = GetProperty(director, "CurrentRoom");
        Assert.AreEqual("b1_entry_combat", GetField(currentRoom, "roomId"));
        Assert.AreEqual("Blessing", GetField(currentRoom, "roomType").ToString());
        Assert.IsTrue((bool)GetField(currentRoom, "startsCombatAfterChoice"));
        Assert.AreEqual(0, (int)Invoke(director, "GetActiveEnemyCount"));
    }

    [Test]
    public void PreCombatInteractableOpensChoiceThenDisappearsBeforeCombat()
    {
        Component director = CreateDirector();

        try
        {
            InvokePrivate(director, "Awake");
            Invoke(director, "StartRoute");
            InvokePrivate(director, "StartRoomContent", GetProperty(director, "CurrentRoom"));

            Component interactable = (Component)GetField(director, "currentRoomInteractable");
            Assert.IsNotNull(interactable);
            Collider objectCollider = interactable.GetComponent<Collider>();
            Assert.IsNotNull(objectCollider);
            Assert.IsTrue(objectCollider.isTrigger);
            Assert.IsNotNull(interactable.GetComponent<Rigidbody>());
            Assert.Greater(interactable.transform.position.sqrMagnitude, 0.01f);
            Assert.AreEqual("AwaitingRoomInteraction", GetProperty(director, "State").ToString());
            Assert.AreEqual(0, (int)Invoke(director, "GetActiveEnemyCount"));

            Assert.IsTrue((bool)Invoke(interactable, "Interact"));
            Assert.AreEqual("AwaitingEventChoice", GetProperty(director, "State").ToString());
            Assert.AreSame(interactable, GetField(director, "currentRoomInteractable"));
            Assert.AreEqual(0, (int)Invoke(director, "GetActiveEnemyCount"));

            GameObject interactableObject = interactable.gameObject;
            Assert.IsTrue((bool)Invoke(director, "SelectChoice", 0));
            Assert.AreEqual("ResolvingRoom", GetProperty(director, "State").ToString());
            Assert.IsNull(GetField(director, "currentRoomInteractable"));
            Assert.IsTrue(interactableObject == null);
            Assert.AreEqual("StartCombat", GetField(director, "pendingAction").ToString());

            InvokePrivate(director, "Update");
            Assert.AreEqual("InCombat", GetProperty(director, "State").ToString());
            Assert.AreEqual(1, (int)Invoke(director, "GetActiveEnemyCount"));
        }
        finally
        {
            DestroyActiveEnemies(director);
        }
    }

    [Test]
    public void CombatPresentationHidesPlayerVisualsAndActivatesWeaponView()
    {
        Component director = CreateDirector();
        GameObject playerObject = new GameObject(
            "Level Flow Player Presentation Test",
            typeof(Rigidbody),
            typeof(CapsuleCollider),
            typeof(SpriteRenderer));
        temporaryObjects.Add(playerObject);
        Component playerController = playerObject.AddComponent(RequireType("NewFPG.Characters.PlayerCharacterController, Assembly-CSharp"));
        SpriteRenderer spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
        Rigidbody playerBody = playerObject.GetComponent<Rigidbody>();
        CapsuleCollider playerCollider = playerObject.GetComponent<CapsuleCollider>();
        Vector3 originalPosition = new Vector3(1f, 0.25f, -2f);
        playerObject.transform.position = originalPosition;

        GameObject weaponObject = new GameObject("Level Flow Weapon View Presentation Test");
        temporaryObjects.Add(weaponObject);
        Component weaponView = weaponObject.AddComponent(RequireType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp"));
        weaponObject.SetActive(false);

        SetField(director, "player", playerObject.transform);
        SetField(director, "weaponView", weaponView);
        InvokePrivate(director, "Awake");

        Assert.IsTrue(spriteRenderer.enabled);
        Assert.IsTrue((bool)GetProperty(playerController, "MovementEnabled"));
        Assert.IsFalse(weaponObject.activeSelf);
        Assert.IsFalse(playerBody.isKinematic);
        Assert.IsTrue(playerCollider.enabled);

        InvokePrivate(director, "SetCombatPresentationActive", true);

        Assert.IsFalse(spriteRenderer.enabled);
        Assert.IsFalse((bool)GetProperty(playerController, "MovementEnabled"));
        Assert.IsTrue(weaponObject.activeSelf);
        Assert.IsNotNull(weaponObject.GetComponent(RequireType("NewFPG.Level.LevelWeaponProjectileShooter, Assembly-CSharp")));
        Assert.IsTrue(playerBody.isKinematic);
        Assert.AreEqual(RigidbodyConstraints.FreezeAll, playerBody.constraints);
        Assert.IsFalse(playerCollider.enabled);

        playerObject.transform.position = originalPosition + Vector3.up * 4f;

        InvokePrivate(director, "SetCombatPresentationActive", false);

        Assert.IsTrue(spriteRenderer.enabled);
        Assert.IsTrue((bool)GetProperty(playerController, "MovementEnabled"));
        Assert.IsFalse(weaponObject.activeSelf);
        Assert.IsFalse(playerBody.isKinematic);
        Assert.IsTrue(playerCollider.enabled);
        Assert.Less(Vector3.Distance(playerObject.transform.position, originalPosition), 0.001f);
    }

    [Test]
    public void RuntimeDebugApiExposesChoiceDoorAndCombatControls()
    {
        Type directorType = RequireType("NewFPG.Level.LevelFlowDirector, Assembly-CSharp");
        Type shooterType = RequireType("NewFPG.Level.LevelWeaponProjectileShooter, Assembly-CSharp");
        Type weaponViewType = RequireType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp");

        AssertPublicMethod(directorType, "SelectChoice", typeof(int));
        AssertPublicMethod(directorType, "SelectDoor", typeof(int));
        AssertPublicMethod(directorType, "TryBeginRoomInteraction", RequireType("NewFPG.Level.LevelRoomInteractable, Assembly-CSharp"));
        AssertPublicMethod(directorType, "DebugInteractCurrentRoomObject");
        AssertPublicMethod(directorType, "GetActiveEnemyCount");
        AssertPublicMethod(directorType, "DebugKillActiveEnemies");
        AssertPublicMethod(shooterType, "SetAimCamera", typeof(Camera));
        AssertPublicMethod(weaponViewType, "RefreshRuntimeView", typeof(Camera));
        AssertNoPublicMethod(shooterType, "FireDebugShot");
        Assert.IsNotNull(
            weaponViewType.GetEvent("WeaponAttackStarted", BindingFlags.Instance | BindingFlags.Public),
            "Weapon HUD should expose the attack event that drives projectile firing.");
    }

    private Component CreateDirector()
    {
        Type directorType = RequireType("NewFPG.Level.LevelFlowDirector, Assembly-CSharp");
        directorObject = new GameObject("Level Flow Director Editor Test");
        Component director = directorObject.AddComponent(directorType);
        SetField(director, "autoStart", false);
        SetField(director, "roomIntroSeconds", 0f);
        SetField(director, "eventResolveSeconds", 0f);
        SetField(director, "combatEndCameraDelay", 0f);
        return director;
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

    private static void AssertNoPublicMethod(Type type, string methodName)
    {
        Assert.IsNull(
            type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public),
            type.Name + "." + methodName + " should not be public.");
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

    private static void DestroyActiveEnemies(object director)
    {
        if (director == null)
        {
            return;
        }

        foreach (object enemy in (IEnumerable)GetField(director, "activeEnemies"))
        {
            if (enemy is Component component && component != null)
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }
    }
}
