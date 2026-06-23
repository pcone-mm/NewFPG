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
        Assert.IsNotNull(weaponObject.GetComponent(RequireType("NewFPG.Combat.PrototypeWeaponCombatHud, Assembly-CSharp")));
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
    public void CombatPresentationReparentsWeaponViewOutOfInactiveParent()
    {
        Component director = CreateDirector();
        GameObject cameraObject = new GameObject("Level Flow Active Camera", typeof(Camera));
        temporaryObjects.Add(cameraObject);
        cameraObject.tag = "MainCamera";

        GameObject inactiveCameraContainer = new GameObject("Level Flow Inactive Weapon Parent");
        temporaryObjects.Add(inactiveCameraContainer);
        inactiveCameraContainer.SetActive(false);

        GameObject weaponObject = new GameObject("Level Flow Weapon View In Inactive Parent");
        temporaryObjects.Add(weaponObject);
        weaponObject.transform.SetParent(inactiveCameraContainer.transform, false);
        Component weaponView = weaponObject.AddComponent(RequireType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp"));

        SetField(director, "weaponView", weaponView);

        Assert.IsTrue(weaponObject.activeSelf);
        Assert.IsFalse(weaponObject.activeInHierarchy);

        InvokePrivate(director, "SetCombatPresentationActive", true);

        Assert.IsTrue(weaponObject.activeSelf);
        Assert.IsTrue(weaponObject.activeInHierarchy);
        Assert.IsNotNull(weaponObject.transform.parent.GetComponent<Camera>());
    }

    [Test]
    public void EnemySpawnPositionAlwaysUsesGroundY()
    {
        Component director = CreateDirector();
        GameObject playerObject = new GameObject("Level Flow Enemy Spawn Height Player");
        temporaryObjects.Add(playerObject);
        playerObject.transform.position = new Vector3(2f, 3.5f, -1f);
        playerObject.transform.rotation = Quaternion.identity;
        SetField(director, "player", playerObject.transform);

        Vector3 fallbackPosition = (Vector3)InvokePrivate(director, "ResolveEnemySpawnPosition", 0, 1);
        Assert.AreEqual(0f, fallbackPosition.y, 0.001f);

        GameObject spawnPoint = new GameObject("Level Flow Enemy Spawn Point Height Test");
        temporaryObjects.Add(spawnPoint);
        spawnPoint.transform.position = new Vector3(-4f, 9.25f, 6f);
        SetField(director, "enemySpawnPoints", new[] { spawnPoint.transform });

        Vector3 explicitPosition = (Vector3)InvokePrivate(director, "ResolveEnemySpawnPosition", 0, 1);
        Assert.AreEqual(0f, explicitPosition.y, 0.001f);
        Assert.AreEqual(spawnPoint.transform.position.x, explicitPosition.x, 0.001f);
        Assert.AreEqual(spawnPoint.transform.position.z, explicitPosition.z, 0.001f);

        Transform enemy = (Transform)InvokePrivate(director, "SpawnEnemy", explicitPosition + Vector3.up * 5f, 0);
        temporaryObjects.Add(enemy.gameObject);
        Assert.AreEqual(0f, enemy.position.y, 0.001f);
    }

    [Test]
    public void EnsureCombatantBindsFishMovementAndAttackTargetsToPlayer()
    {
        Component director = CreateDirector();
        GameObject playerObject = new GameObject("Level Flow Fish Target Player");
        temporaryObjects.Add(playerObject);
        SetField(director, "player", playerObject.transform);

        GameObject fishObject = new GameObject("Level Flow Fish Target Enemy");
        temporaryObjects.Add(fishObject);
        fishObject.AddComponent<SpriteRenderer>();
        fishObject.AddComponent<Rigidbody>();
        fishObject.AddComponent<BoxCollider>();
        Component movement = fishObject.AddComponent(RequireType("NewFPG.Monsters.FishMonsterController, Assembly-CSharp"));
        Component attack = fishObject.AddComponent(RequireType("NewFPG.Combat.FishAttackController, Assembly-CSharp"));

        object room = Activator.CreateInstance(RequireType("NewFPG.Level.LevelRoomDefinition, Assembly-CSharp"));

        InvokePrivate(director, "EnsureCombatant", fishObject, room);

        Assert.AreSame(playerObject.transform, GetProperty(movement, "Target"));
        Assert.AreSame(playerObject.transform, GetProperty(attack, "Target"));
    }

    [Test]
    public void RuntimeDebugApiExposesChoiceDoorAndCombatControls()
    {
        Type directorType = RequireType("NewFPG.Level.LevelFlowDirector, Assembly-CSharp");
        Type combatHudType = RequireType("NewFPG.Combat.PrototypeWeaponCombatHud, Assembly-CSharp");
        Type weaponViewType = RequireType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp");

        AssertPublicMethod(directorType, "SelectChoice", typeof(int));
        AssertPublicMethod(directorType, "SelectDoor", typeof(int));
        AssertPublicMethod(directorType, "TryBeginRoomInteraction", RequireType("NewFPG.Level.LevelRoomInteractable, Assembly-CSharp"));
        AssertPublicMethod(directorType, "DebugInteractCurrentRoomObject");
        AssertPublicMethod(directorType, "GetActiveEnemyCount");
        AssertPublicMethod(directorType, "DebugKillActiveEnemies");
        AssertPublicMethod(combatHudType, "Bind", RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"), RequireType("NewFPG.Combat.CombatResourcePool, Assembly-CSharp"), RequireType("NewFPG.Combat.PlayerWeaponCaster, Assembly-CSharp"));
        AssertPublicMethod(combatHudType, "SetAimCamera", typeof(Camera));
        AssertPublicMethod(combatHudType, "SetCombatEnabled", typeof(bool));
        AssertPublicMethod(weaponViewType, "RefreshRuntimeView", typeof(Camera));
        Assert.IsNotNull(
            weaponViewType.GetEvent("WeaponAttackStarted", BindingFlags.Instance | BindingFlags.Public),
            "Weapon HUD should expose the attack event for visual release callbacks.");
        Assert.IsNotNull(
            weaponViewType.GetEvent("WeaponAttackRequested", BindingFlags.Instance | BindingFlags.Public),
            "Weapon HUD should expose the request event so resource checks can block attacks before the animation.");
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
