using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LevelFlowDirectorEditorTests
{
    private GameObject directorObject;
    private readonly System.Collections.Generic.List<GameObject> temporaryObjects = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<UnityEngine.Object> temporaryAssets = new System.Collections.Generic.List<UnityEngine.Object>();

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

        for (int i = temporaryAssets.Count - 1; i >= 0; i--)
        {
            if (temporaryAssets[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryAssets[i]);
            }
        }

        temporaryAssets.Clear();

        if (directorObject != null)
        {
            UnityEngine.Object.DestroyImmediate(directorObject);
        }
    }

    [Test]
    public void RouteTableStartRoomEntersConfiguredRoom()
    {
        Component director = CreateDirector();
        object room = CreateRoom("start_room", "fish_intro", "Blessing", "OnInteract", "StartEncounter");
        SetField(director, "routeTable", CreateRouteTable("start_room", room));
        SetField(director, "encounterTable", CreateEncounterTable("fish_intro", CreateWave("fish_intro_wave", CreateEnemyPrefab("Intro Fish"), 1, false, 80f)));

        InvokePrivate(director, "Awake");
        Invoke(director, "StartRoute");
        InvokePrivate(director, "StartRoomContent", GetProperty(director, "CurrentRoom"));

        object currentRoom = GetProperty(director, "CurrentRoom");
        Assert.AreEqual("start_room", GetField(currentRoom, "roomId"));
        Assert.AreEqual("AwaitingRoomInteraction", GetProperty(director, "State").ToString());
        Assert.AreEqual(0, (int)Invoke(director, "GetActiveEnemyCount"));
    }

    [Test]
    public void OnInteractRoomDoesNotSpawnUntilInteractableIsUsed()
    {
        Component director = CreateDirector();
        GameObject enemyPrefab = CreateEnemyPrefab("Interaction Gated Fish Pair Prefab");
        object room = CreateRoom("fish_pair_room", "fish_pair", "Combat", "OnInteract", "StartEncounter");
        SetField(director, "routeTable", CreateRouteTable("fish_pair_room", room));
        SetField(director, "encounterTable", CreateEncounterTable("fish_pair", CreateWave("fish_pair_wave", enemyPrefab, 2, false, 80f)));

        try
        {
            InvokePrivate(director, "Awake");
            Invoke(director, "StartRoute");
            InvokePrivate(director, "StartRoomContent", GetProperty(director, "CurrentRoom"));

            Assert.AreEqual("AwaitingRoomInteraction", GetProperty(director, "State").ToString());
            Assert.AreEqual(0, (int)Invoke(director, "GetActiveEnemyCount"));

            Component interactable = (Component)GetField(director, "currentRoomInteractable");
            Assert.IsNotNull(interactable);
            Assert.IsTrue((bool)Invoke(interactable, "Interact"));
            Assert.AreEqual("ResolvingRoom", GetProperty(director, "State").ToString());
            Assert.AreEqual("StartCombat", GetField(director, "pendingAction").ToString());
            Assert.AreEqual(0, (int)Invoke(director, "GetActiveEnemyCount"));

            InvokePrivate(director, "Update");

            Assert.AreEqual("InCombat", GetProperty(director, "State").ToString());
            Assert.AreEqual(2, (int)Invoke(director, "GetActiveEnemyCount"));
        }
        finally
        {
            DestroyActiveEnemies(director);
        }
    }

    [Test]
    public void ChoiceEncounterOverrideReplacesRoomEncounter()
    {
        Component director = CreateDirector();
        GameObject normalPrefab = CreateEnemyPrefab("Normal Fish Prefab");
        GameObject elitePrefab = CreateEnemyPrefab("Elite Fish Prefab");
        object room = CreateRoom("choice_room", "fish_intro", "Blessing", "OnInteract", "StartEncounter");
        AddListItem(room, "choices", CreateChoice("elite_choice", "Elite", "elite_fish", 0f, 0));
        SetField(director, "routeTable", CreateRouteTable("choice_room", room));
        SetField(
            director,
            "encounterTable",
            CreateEncounterTable(
                "fish_intro",
                CreateWave("intro_wave", normalPrefab, 1, false, 80f),
                "elite_fish",
                CreateWave("elite_wave", elitePrefab, 1, true, 150f)));

        try
        {
            InvokePrivate(director, "Awake");
            Invoke(director, "StartRoute");
            InvokePrivate(director, "StartRoomContent", GetProperty(director, "CurrentRoom"));
            Component interactable = (Component)GetField(director, "currentRoomInteractable");
            Assert.IsTrue((bool)Invoke(interactable, "Interact"));
            Assert.IsTrue((bool)Invoke(director, "SelectChoice", 0));
            InvokePrivate(director, "Update");

            object enemy = FirstActiveEnemy(director);
            Assert.IsNotNull(enemy);
            Assert.AreEqual(150f, (float)GetProperty(enemy, "Hp"), 0.001f);
        }
        finally
        {
            DestroyActiveEnemies(director);
        }
    }

    [Test]
    public void EncounterAdvancesToNextWaveAfterCurrentWaveDies()
    {
        Component director = CreateDirector();
        GameObject firstWavePrefab = CreateEnemyPrefab("First Wave Fish");
        GameObject secondWavePrefab = CreateEnemyPrefab("Second Wave Fish");
        object room = CreateRoom("wave_room", "multi_wave", "Combat", "OnEnter", "StartEncounter");
        AddListItem(room, "exits", Door("end_room"));
        object endRoom = CreateRoom("end_room", string.Empty, "Rest", "OnEnter", "CompleteRoute");
        SetField(director, "routeTable", CreateRouteTable("wave_room", room, endRoom));
        SetField(
            director,
            "encounterTable",
            CreateEncounterTable(
                "multi_wave",
                CreateWave("wave_1", firstWavePrefab, 1, false, 80f),
                CreateWave("wave_2", secondWavePrefab, 1, false, 80f)));

        try
        {
            InvokePrivate(director, "Awake");
            Invoke(director, "StartRoute");
            InvokePrivate(director, "StartRoomContent", GetProperty(director, "CurrentRoom"));
            InvokePrivate(director, "Update");

            Assert.AreEqual("InCombat", GetProperty(director, "State").ToString());
            Assert.AreEqual(0, (int)GetField(director, "currentWaveIndex"));
            Assert.AreEqual(1, (int)Invoke(director, "GetActiveEnemyCount"));
            object currentEncounter = GetField(director, "currentEncounter");
            Assert.AreEqual(2, ((IList)GetField(currentEncounter, "waves")).Count);

            Invoke(director, "DebugKillActiveEnemies");
            InvokePrivate(director, "Update");

            Assert.AreEqual("InCombat", GetProperty(director, "State").ToString());
            Assert.AreEqual(1, (int)GetField(director, "currentWaveIndex"));
            Assert.AreEqual(1, ((IList)GetField(director, "activeEnemies")).Count);
            Assert.AreEqual(1, (int)Invoke(director, "GetActiveEnemyCount"));

            Invoke(director, "DebugKillActiveEnemies");
            Assert.AreEqual("ResolvingRoom", GetProperty(director, "State").ToString());

            InvokePrivate(director, "Update");
            Assert.AreEqual("ChoosingNextRoom", GetProperty(director, "State").ToString());
        }
        finally
        {
            DestroyActiveEnemies(director);
        }
    }

    [Test]
    public void MissingEncounterLogsErrorAndDoesNotFallbackSpawn()
    {
        Component director = CreateDirector();
        object room = CreateRoom("missing_room", "missing_encounter", "Combat", "OnEnter", "StartEncounter");
        AddListItem(room, "exits", Door("end_room"));
        object endRoom = CreateRoom("end_room", string.Empty, "Rest", "OnEnter", "CompleteRoute");
        SetField(director, "routeTable", CreateRouteTable("missing_room", room, endRoom));
        SetField(director, "encounterTable", CreateEncounterTable());
        LogAssert.Expect(LogType.Error, "Level encounter table has no encounter id: missing_encounter");

        InvokePrivate(director, "Awake");
        Invoke(director, "StartRoute");
        InvokePrivate(director, "StartRoomContent", GetProperty(director, "CurrentRoom"));
        InvokePrivate(director, "Update");

        Assert.AreEqual("ResolvingRoom", GetProperty(director, "State").ToString());
        Assert.AreEqual(0, (int)Invoke(director, "GetActiveEnemyCount"));
        Assert.AreEqual("ResolveRoom", GetField(director, "pendingAction").ToString());
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
    }

    [Test]
    public void SpawnEnemySetsMonsterHomePositionToActualSpawn()
    {
        Component director = CreateDirector();
        GameObject prefab = CreateEnemyPrefab("Level Flow Home Position Fish Prefab");
        prefab.AddComponent<SpriteRenderer>();
        Component prefabBinding = prefab.AddComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));
        InvokePrivate(prefabBinding, "Awake");
        prefab.transform.position = new Vector3(-9f, 0f, -9f);

        object request = CreateSpawnRequest(prefab);
        Vector3 spawnPosition = new Vector3(1.75f, 0f, 7.95f);
        Transform spawned = (Transform)InvokePrivate(director, "SpawnEnemy", spawnPosition, 0, request);
        temporaryObjects.Add(spawned.gameObject);

        Component spawnedBinding = spawned.GetComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));
        Assert.IsNotNull(spawnedBinding);
        Vector3 homePosition = (Vector3)GetProperty(spawnedBinding, "HomePosition");
        Assert.Less(Vector3.Distance(spawnPosition, homePosition), 0.001f);
    }

    [Test]
    public void EnsureCombatantAddsLevelTrackerWithoutOwningMonsterTargets()
    {
        Component director = CreateDirector();

        GameObject fishObject = new GameObject("Level Flow Fish Target Enemy");
        temporaryObjects.Add(fishObject);
        fishObject.AddComponent<SpriteRenderer>();
        fishObject.AddComponent(RequireType("Pathfinding.Seeker, AstarPathfindingProject"));
        fishObject.AddComponent(RequireType("Pathfinding.AIPath, AstarPathfindingProject"));
        fishObject.AddComponent<BoxCollider>();
        Component binding = fishObject.AddComponent(RequireType("NewFPG.Monsters.MonsterConfigBinding, Assembly-CSharp"));

        object room = Activator.CreateInstance(RequireType("NewFPG.Level.LevelRoomDefinition, Assembly-CSharp"));

        Component combatant = (Component)InvokePrivate(director, "EnsureCombatant", fishObject, room);

        Assert.IsNotNull(combatant);
        Assert.IsNotNull(fishObject.GetComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp")));
        Assert.IsNull(GetProperty(binding, "Target"));
    }

    [Test]
    public void DirectorActivatesAssignedBattleArenaZoneMap()
    {
        Component director = CreateDirector();
        Type zoneMapType = RequireType("NewFPG.Combat.BattleArenaZoneMap, Assembly-CSharp");
        GameObject zoneMapObject = new GameObject("Level Flow Battle Arena Zone Map");
        temporaryObjects.Add(zoneMapObject);
        Component zoneMap = zoneMapObject.AddComponent(zoneMapType);

        SetField(director, "battleArenaZoneMap", zoneMap);
        InvokePrivate(director, "ActivateBattleArenaZoneMap");

        PropertyInfo currentProperty = zoneMapType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(currentProperty);
        Assert.AreSame(zoneMap, currentProperty.GetValue(null));
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
            weaponViewType.GetProperty("WeaponRig", BindingFlags.Instance | BindingFlags.Public),
            "Weapon HUD should expose the generated rig for presentation controllers.");
        Assert.IsNotNull(
            weaponViewType.GetProperty("WeaponCamera", BindingFlags.Instance | BindingFlags.Public),
            "Weapon HUD should expose the generated camera for presentation controllers.");
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

    private GameObject CreateEnemyPrefab(string name)
    {
        GameObject prefab = new GameObject(name, typeof(BoxCollider));
        prefab.AddComponent(RequireType("Pathfinding.Seeker, AstarPathfindingProject"));
        prefab.AddComponent(RequireType("Pathfinding.AIPath, AstarPathfindingProject"));
        prefab.AddComponent(RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp"));
        prefab.SetActive(false);
        temporaryObjects.Add(prefab);
        return prefab;
    }

    private UnityEngine.Object CreateRouteTable(string startRoomId, params object[] rooms)
    {
        Type tableType = RequireType("NewFPG.Level.LevelRouteTable, Assembly-CSharp");
        Type routeType = RequireType("NewFPG.Level.LevelRouteId, Assembly-CSharp");
        Type roomType = RequireType("NewFPG.Level.LevelRoomDefinition, Assembly-CSharp");
        UnityEngine.Object table = ScriptableObject.CreateInstance(tableType);
        temporaryAssets.Add(table);

        Array roomArray = Array.CreateInstance(roomType, rooms.Length);
        for (int i = 0; i < rooms.Length; i++)
        {
            roomArray.SetValue(rooms[i], i);
        }

        Invoke(table, "Configure", Enum.Parse(routeType, "UndergroundFirstFloor"), startRoomId, roomArray);
        return table;
    }

    private UnityEngine.Object CreateEncounterTable(params object[] encounterIdsAndWaves)
    {
        Type tableType = RequireType("NewFPG.Level.LevelEncounterTable, Assembly-CSharp");
        Type encounterType = RequireType("NewFPG.Level.LevelEncounterDefinition, Assembly-CSharp");
        UnityEngine.Object table = ScriptableObject.CreateInstance(tableType);
        temporaryAssets.Add(table);

        var encounterList = new System.Collections.Generic.List<object>();
        for (int i = 0; i < encounterIdsAndWaves.Length;)
        {
            Assert.IsInstanceOf<string>(encounterIdsAndWaves[i], "Encounter ids must be string entries.");
            object encounter = Activator.CreateInstance(encounterType);
            SetPublicField(encounter, "encounterId", (string)encounterIdsAndWaves[i]);
            i++;

            while (i < encounterIdsAndWaves.Length && !(encounterIdsAndWaves[i] is string))
            {
                object waveOrWaves = encounterIdsAndWaves[i];
                if (waveOrWaves is object[] waves)
                {
                    for (int w = 0; w < waves.Length; w++)
                    {
                        AddListItem(encounter, "waves", waves[w]);
                    }
                }
                else
                {
                    AddListItem(encounter, "waves", waveOrWaves);
                }

                i++;
            }

            encounterList.Add(encounter);
        }

        Array encounters = Array.CreateInstance(encounterType, encounterList.Count);
        for (int i = 0; i < encounterList.Count; i++)
        {
            encounters.SetValue(encounterList[i], i);
        }

        Invoke(table, "SetEncounters", encounters);
        return table;
    }

    private object CreateWave(string waveId, GameObject enemyPrefab, int count, bool overrideMaxHealth, float maxHealth)
    {
        object wave = Activator.CreateInstance(RequireType("NewFPG.Level.LevelEncounterWave, Assembly-CSharp"));
        SetPublicField(wave, "waveId", waveId);
        SetPublicField(
            wave,
            "selectionMode",
            Enum.Parse(RequireType("NewFPG.Level.LevelSpawnSelectionMode, Assembly-CSharp"), "PresetGroupRandom"));

        object group = Activator.CreateInstance(RequireType("NewFPG.Level.LevelSpawnGroup, Assembly-CSharp"));
        SetPublicField(group, "groupId", waveId + "_group");
        SetPublicField(group, "weight", 1f);

        object entry = Activator.CreateInstance(RequireType("NewFPG.Level.LevelSpawnEntry, Assembly-CSharp"));
        SetPublicField(entry, "monsterId", "fish");
        SetPublicField(entry, "monsterPrefab", enemyPrefab);
        SetPublicField(entry, "count", count);
        SetPublicField(entry, "weight", 1f);
        SetPublicField(entry, "overrideMaxHealth", overrideMaxHealth);
        SetPublicField(entry, "maxHealthOverride", maxHealth);

        AddListItem(group, "entries", entry);
        AddListItem(wave, "presetGroups", group);
        return wave;
    }

    private static object CreateSpawnRequest(GameObject enemyPrefab)
    {
        object entry = Activator.CreateInstance(RequireType("NewFPG.Level.LevelSpawnEntry, Assembly-CSharp"));
        SetPublicField(entry, "monsterId", "fish");
        SetPublicField(entry, "monsterPrefab", enemyPrefab);
        SetPublicField(entry, "count", 1);
        SetPublicField(entry, "weight", 1f);

        Type requestType = RequireType("NewFPG.Level.LevelSpawnRequest, Assembly-CSharp");
        return Activator.CreateInstance(requestType, entry);
    }

    private static object CreateRoom(string roomId, string encounterId, string roomType, string triggerMode, string completionMode)
    {
        object room = Activator.CreateInstance(RequireType("NewFPG.Level.LevelRoomDefinition, Assembly-CSharp"));
        SetPublicField(room, "roomId", roomId);
        SetPublicField(room, "displayName", roomId);
        SetPublicField(room, "encounterId", encounterId);
        SetPublicField(
            room,
            "roomType",
            Enum.Parse(RequireType("NewFPG.Level.LevelRoomType, Assembly-CSharp"), roomType));
        SetPublicField(
            room,
            "triggerMode",
            Enum.Parse(RequireType("NewFPG.Level.LevelRoomTriggerMode, Assembly-CSharp"), triggerMode));
        SetPublicField(
            room,
            "completionMode",
            Enum.Parse(RequireType("NewFPG.Level.LevelRoomCompletionMode, Assembly-CSharp"), completionMode));
        return room;
    }

    private static object CreateChoice(string choiceId, string displayName, string encounterIdOverride, float damageBonus, int goldDelta)
    {
        object choice = Activator.CreateInstance(RequireType("NewFPG.Level.LevelRoomChoiceDefinition, Assembly-CSharp"));
        SetPublicField(choice, "choiceId", choiceId);
        SetPublicField(choice, "displayName", displayName);
        SetPublicField(choice, "encounterIdOverride", encounterIdOverride);
        SetPublicField(choice, "damageBonus", damageBonus);
        SetPublicField(choice, "goldDelta", goldDelta);
        return choice;
    }

    private static object Door(string targetRoomId)
    {
        object door = Activator.CreateInstance(RequireType("NewFPG.Level.LevelDoorDefinition, Assembly-CSharp"));
        SetPublicField(door, "targetRoomId", targetRoomId);
        SetPublicField(door, "displayName", targetRoomId);
        return door;
    }

    private static object FirstActiveEnemy(object director)
    {
        foreach (object enemy in (IEnumerable)GetField(director, "activeEnemies"))
        {
            if (enemy != null)
            {
                return enemy;
            }
        }

        return null;
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
        MethodInfo method = FindMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.Public, args);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.NonPublic, args);
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

    private static void SetPublicField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static void AddListItem(object target, string fieldName, object item)
    {
        ((IList)GetField(target, fieldName)).Add(item);
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
