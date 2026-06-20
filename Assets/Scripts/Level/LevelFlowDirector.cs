using System.Collections.Generic;
using UnityEngine;
using NewFPG.CameraRig;
using NewFPG.Characters;
using NewFPG.Monsters;
using NewFPG.Prototype;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Level
{
    public sealed class LevelFlowDirector : MonoBehaviour
    {
        [Header("Route")]
        [SerializeField] private LevelRouteId routeId = LevelRouteId.UndergroundFirstFloor;
        [SerializeField] private bool autoStart = true;

        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform enemyPrefab;
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Transform[] roomAnchors;
        [SerializeField] private CinemachineCameraModeController cameraModeController;
        [SerializeField] private PrototypeFirstPersonWeaponView weaponView;
        [SerializeField] private LevelWeaponProjectileShooter weaponShooter;
        [SerializeField] private LevelFlowHud hud;
        [SerializeField] private LevelRoomInteractable roomInteractablePrefab;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float roomIntroSeconds = 0.75f;
        [SerializeField, Min(0f)] private float combatEndCameraDelay = 0.8f;
        [SerializeField, Min(0f)] private float eventResolveSeconds = 0.45f;
        [SerializeField, Min(0f)] private float enemySpawnRadius = 1.6f;

        [Header("Enemy Defaults")]
        [SerializeField, Min(1f)] private float normalEnemyHp = 80f;
        [SerializeField, Min(1f)] private float eliteEnemyHp = 150f;
        [SerializeField] private Sprite fishHitSprite;

        [Header("Room Interaction")]
        [SerializeField, Min(0.5f)] private float interactableForwardOffset = 2.2f;
        [SerializeField, Min(0.5f)] private float interactableHeight = 0.8f;

        [Header("Combat Presentation")]
        [SerializeField] private bool hidePlayerVisualsDuringCombat = true;
        [SerializeField] private bool disablePlayerMovementDuringCombat = true;
        [SerializeField] private bool freezePlayerPhysicsDuringCombat = true;

        private readonly Dictionary<string, LevelRoomDefinition> roomsById = new Dictionary<string, LevelRoomDefinition>();
        private readonly List<LevelCombatant> activeEnemies = new List<LevelCombatant>();
        private LevelRoomDefinition currentRoom;
        private LevelRoomInteractable currentRoomInteractable;
        private PlayerCharacterController playerController;
        private Rigidbody playerBody;
        private Collider[] playerColliders;
        private Renderer[] playerVisualRenderers;
        private bool[] playerRendererEnabledBeforeCombat;
        private bool[] playerColliderEnabledBeforeCombat;
        private bool playerMovementEnabledBeforeCombat;
        private bool playerBodyKinematicBeforeCombat;
        private RigidbodyConstraints playerBodyConstraintsBeforeCombat;
        private Vector3 playerPositionBeforeCombat;
        private Quaternion playerRotationBeforeCombat;
        private bool playerHiddenForCombat;
        private bool playerPhysicsFrozenForCombat;
        private LevelFlowState state;
        private int roomDepth;
        private float damageBonus;
        private int gold;
        private bool starting;
        private PendingFlowAction pendingAction;
        private LevelRoomDefinition pendingRoom;
        private float pendingActionAt;

        public LevelFlowState State => state;
        public bool IsInCombat => state == LevelFlowState.InCombat;
        public LevelRoomDefinition CurrentRoom => currentRoom;
        public int RoomDepth => roomDepth;
        public int Gold => gold;
        public float DamageBonus => damageBonus;

        private void Reset()
        {
            player = FindFirstObjectByType<PlayerCharacterController>()?.transform;
            cameraModeController = FindFirstObjectByType<CinemachineCameraModeController>();
            weaponView = FindFirstObjectByType<PrototypeFirstPersonWeaponView>();
            weaponShooter = weaponView != null ? weaponView.GetComponent<LevelWeaponProjectileShooter>() : null;
            hud = GetComponentInChildren<LevelFlowHud>();
        }

        private void Awake()
        {
            ResolveReferences();
            BuildUndergroundFirstFloorRoute();
            ConfigureWeaponShooter();
        }

        private void Start()
        {
            if (autoStart)
            {
                StartRoute();
            }
        }

        private void Update()
        {
            HandleDebugInput();

            if (pendingAction == PendingFlowAction.None || Time.unscaledTime < pendingActionAt)
            {
                return;
            }

            PendingFlowAction action = pendingAction;
            LevelRoomDefinition room = pendingRoom;
            pendingAction = PendingFlowAction.None;
            pendingRoom = null;

            switch (action)
            {
                case PendingFlowAction.StartRoomContent:
                    StartRoomContent(room);
                    break;
                case PendingFlowAction.StartCombat:
                    BeginCombatRoom(room);
                    break;
                case PendingFlowAction.ResolveRoom:
                    ResolveRoom();
                    break;
            }
        }

        private void OnDestroy()
        {
            SetPlayerHiddenForCombat(false);
            UnsubscribeEnemies();
            ClearRoomInteractable();
        }

        [ContextMenu("Start Underground First Floor")]
        public void StartRoute()
        {
            if (starting && state != LevelFlowState.Idle && state != LevelFlowState.Complete)
            {
                return;
            }

            starting = true;
            pendingAction = PendingFlowAction.None;
            pendingRoom = null;
            roomDepth = 0;
            damageBonus = 0f;
            gold = 0;
            ClearRoomInteractable();
            ClearActiveEnemies();
            SetCombatPresentationActive(false);
            EnterRoom("b1_entry_combat");
        }

        [ContextMenu("Debug Select First Room Choice")]
        public void DebugSelectFirstChoice()
        {
            SelectChoice(0);
        }

        [ContextMenu("Debug Interact Current Room Object")]
        public bool DebugInteractCurrentRoomObject()
        {
            return currentRoomInteractable != null && currentRoomInteractable.Interact();
        }

        [ContextMenu("Debug Kill Active Enemies")]
        public void DebugKillActiveEnemies()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                if (activeEnemies[i] != null && !activeEnemies[i].IsDead)
                {
                    activeEnemies[i].ApplyDamage(activeEnemies[i].Hp, activeEnemies[i].transform.position, gameObject);
                }
            }
        }

        private void HandleDebugInput()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugKeyPressed(DebugFlowKey.SelectChoice))
            {
                if (state == LevelFlowState.AwaitingRoomInteraction && currentRoomInteractable != null)
                {
                    currentRoomInteractable.Interact();
                }
                else
                {
                    SelectChoice(0);
                }
            }

            if (DebugKeyPressed(DebugFlowKey.KillEnemies))
            {
                DebugKillActiveEnemies();
            }

            if (DebugKeyPressed(DebugFlowKey.SelectDoor))
            {
                SelectDoor(0);
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool DebugKeyPressed(DebugFlowKey key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                switch (key)
                {
                    case DebugFlowKey.SelectChoice:
                        return keyboard.f6Key.wasPressedThisFrame;
                    case DebugFlowKey.KillEnemies:
                        return keyboard.f8Key.wasPressedThisFrame;
                    case DebugFlowKey.SelectDoor:
                        return keyboard.f9Key.wasPressedThisFrame;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            switch (key)
            {
                case DebugFlowKey.SelectChoice:
                    return Input.GetKeyDown(KeyCode.F6);
                case DebugFlowKey.KillEnemies:
                    return Input.GetKeyDown(KeyCode.F8);
                case DebugFlowKey.SelectDoor:
                    return Input.GetKeyDown(KeyCode.F9);
            }
#endif

            return false;
        }
#endif

        public int GetActiveEnemyCount()
        {
            int count = 0;
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                if (activeEnemies[i] != null && !activeEnemies[i].IsDead)
                {
                    count++;
                }
            }

            return count;
        }

        public bool SelectChoice(int choiceIndex)
        {
            if (state != LevelFlowState.AwaitingEventChoice
                || currentRoom == null
                || currentRoom.choices == null
                || choiceIndex < 0
                || choiceIndex >= currentRoom.choices.Count)
            {
                return false;
            }

            SelectEventChoice(currentRoom.choices[choiceIndex]);
            return true;
        }

        public bool TryBeginRoomInteraction(LevelRoomInteractable interactable)
        {
            if (state != LevelFlowState.AwaitingRoomInteraction
                || interactable == null
                || interactable != currentRoomInteractable
                || currentRoom == null)
            {
                return false;
            }

            BeginEventRoom(currentRoom);
            return true;
        }

        public bool SelectDoor(int doorIndex)
        {
            if (state != LevelFlowState.ChoosingNextRoom
                || currentRoom == null
                || currentRoom.exits == null
                || doorIndex < 0
                || doorIndex >= currentRoom.exits.Count)
            {
                return false;
            }

            LevelDoorDefinition door = currentRoom.exits[doorIndex];
            hud.HideChoices();
            EnterRoom(door.targetRoomId);
            return true;
        }

        public void EnterRoom(string roomId)
        {
            if (!roomsById.TryGetValue(roomId, out LevelRoomDefinition room))
            {
                CompleteRoute("找不到房间：" + roomId);
                return;
            }

            currentRoom = room;
            roomDepth++;
            state = LevelFlowState.EnteringRoom;
            SetCombatPresentationActive(false);
            ClearRoomInteractable();
            hud.HideChoices();
            hud.SetStatus(
                RouteDisplayName() + "  Room " + roomDepth,
                "进入：" + room.displayName + "\n" + RoomSummary(room));

            if (cameraModeController != null)
            {
                cameraModeController.SwitchToExplore();
            }

            MovePlayerToRoomAnchor(roomDepth - 1);
            Schedule(PendingFlowAction.StartRoomContent, room, roomIntroSeconds);
        }

        private void StartRoomContent(LevelRoomDefinition room)
        {
            if (room == null || room != currentRoom)
            {
                return;
            }

            if (room.choices != null && room.choices.Count > 0)
            {
                BeginRoomInteraction(room);
                return;
            }

            if (room.IsCombatRoom)
            {
                BeginCombatRoom(room);
                return;
            }

            BeginEventRoom(room);
        }

        private void BeginCombatRoom(LevelRoomDefinition room)
        {
            state = LevelFlowState.InCombat;
            hud.SetStatus(
                "战斗：" + room.displayName,
                "点击屏幕上的武器发射子弹。\n击杀全部怪物后进入清房结算。");

            if (cameraModeController != null)
            {
                cameraModeController.SwitchToBattle();
            }

            SpawnEncounter(room);
            SetCombatPresentationActive(true);
        }

        private void BeginRoomInteraction(LevelRoomDefinition room)
        {
            state = LevelFlowState.AwaitingRoomInteraction;
            currentRoomInteractable = SpawnRoomInteractable(room);
            hud.SetStatus(
                "发现：" + room.displayName,
                "靠近场景中的" + InteractionObjectName(room) + "后按 E，或直接点击它。\n" + RoomSummary(room));
        }

        private LevelRoomInteractable SpawnRoomInteractable(LevelRoomDefinition room)
        {
            Vector3 position = ResolveInteractablePosition();
            LevelRoomInteractable interactable;
            if (roomInteractablePrefab != null)
            {
                interactable = Instantiate(roomInteractablePrefab, position, Quaternion.identity);
            }
            else
            {
                GameObject interactableObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                interactableObject.name = InteractionObjectName(room);
                interactableObject.transform.position = position;
                interactableObject.transform.localScale = Vector3.one * 0.75f;
                Collider objectCollider = interactableObject.GetComponent<Collider>();
                if (objectCollider != null)
                {
                    objectCollider.isTrigger = true;
                }

                Renderer renderer = interactableObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                    material.color = room.roomType == LevelRoomType.StoryEvent
                        ? new Color(0.55f, 0.78f, 1f, 1f)
                        : new Color(1f, 0.78f, 0.24f, 1f);
                    renderer.sharedMaterial = material;
                }

                interactable = interactableObject.AddComponent<LevelRoomInteractable>();
            }

            interactable.name = InteractionObjectName(room);
            interactable.transform.SetParent(transform, true);
            interactable.Initialize(this, room, player, "按 E 互动");
            return interactable;
        }

        private Vector3 ResolveInteractablePosition()
        {
            Transform reference = player != null ? player : transform;
            Vector3 forward = reference.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = Vector3.forward;
            }

            return reference.position + forward.normalized * interactableForwardOffset + Vector3.up * interactableHeight;
        }

        private static string InteractionObjectName(LevelRoomDefinition room)
        {
            if (room == null)
            {
                return "LevelRoomInteractable";
            }

            switch (room.roomType)
            {
                case LevelRoomType.Blessing:
                    return "BlessingInteractable_" + room.roomId;
                case LevelRoomType.StoryEvent:
                    return "EventInteractable_" + room.roomId;
                default:
                    return "RoomInteractable_" + room.roomId;
            }
        }

        private void BeginEventRoom(LevelRoomDefinition room)
        {
            state = LevelFlowState.AwaitingEventChoice;
            hud.SetStatus("事件：" + room.displayName, RoomSummary(room));

            if (room.choices == null || room.choices.Count == 0)
            {
                ResolveRoom();
                return;
            }

            List<LevelHudChoice> choices = new List<LevelHudChoice>();
            for (int i = 0; i < room.choices.Count; i++)
            {
                LevelRoomChoiceDefinition choice = room.choices[i];
                choices.Add(new LevelHudChoice(choice.BuildLabel(), () => SelectEventChoice(choice)));
            }

            hud.ShowChoices(choices);
        }

        private void SelectEventChoice(LevelRoomChoiceDefinition choice)
        {
            hud.HideChoices();
            ClearRoomInteractable();

            if (choice.damageBonus > 0f)
            {
                damageBonus += choice.damageBonus;
                if (weaponShooter != null)
                {
                    weaponShooter.SetDamageMultiplier(1f + damageBonus);
                }
            }

            if (choice.goldDelta != 0)
            {
                gold += choice.goldDelta;
            }

            hud.SetStatus(
                "获得：" + choice.displayName,
                "伤害加成 +" + Mathf.RoundToInt(damageBonus * 100f) + "%\n金币 " + gold);
            state = LevelFlowState.ResolvingRoom;
            Schedule(currentRoom != null && currentRoom.IsCombatRoom ? PendingFlowAction.StartCombat : PendingFlowAction.ResolveRoom, currentRoom, eventResolveSeconds);
        }

        private void ResolveRoom()
        {
            if (currentRoom == null)
            {
                CompleteRoute("当前房间为空");
                return;
            }

            if (currentRoom.exits == null || currentRoom.exits.Count == 0)
            {
                CompleteRoute("地下第一层原型流程结束");
                return;
            }

            state = LevelFlowState.ChoosingNextRoom;
            SetCombatPresentationActive(false);
            hud.SetStatus(
                "选择下一个房间",
                "门会预告奖励池和风险。\nMajor/Minor/Special 的分层先在流程里保留。");

            List<LevelHudChoice> choices = new List<LevelHudChoice>();
            for (int i = 0; i < currentRoom.exits.Count; i++)
            {
                int doorIndex = i;
                LevelDoorDefinition door = currentRoom.exits[i];
                choices.Add(new LevelHudChoice(door.BuildLabel(), () => SelectDoor(doorIndex)));
            }

            hud.ShowChoices(choices);
        }

        private void CompleteRoute(string message)
        {
            state = LevelFlowState.Complete;
            SetCombatPresentationActive(false);
            if (cameraModeController != null)
            {
                cameraModeController.SwitchToExplore();
            }

            hud.HideChoices();
            hud.SetStatus("流程完成", message);
            starting = false;
        }

        private void SpawnEncounter(LevelRoomDefinition room)
        {
            ClearActiveEnemies();
            int count = Mathf.Max(1, room.enemyCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPosition = ResolveEnemySpawnPosition(i, count);
                Transform enemyTransform = SpawnEnemy(spawnPosition, i);
                LevelCombatant combatant = EnsureCombatant(enemyTransform.gameObject, room);
                combatant.Died += OnEnemyDied;
                activeEnemies.Add(combatant);
            }
        }

        private Transform SpawnEnemy(Vector3 position, int index)
        {
            if (enemyPrefab != null)
            {
                Transform spawned = Instantiate(enemyPrefab, position, enemyPrefab.rotation);
                spawned.name = enemyPrefab.name + "_RoomEnemy_" + index;
                spawned.gameObject.SetActive(true);
                return spawned;
            }

            GameObject enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObject.name = "PrototypeEnemy_" + index;
            enemyObject.transform.position = position;
            enemyObject.transform.localScale = new Vector3(1.1f, 1.4f, 1.1f);
            Rigidbody body = enemyObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            return enemyObject.transform;
        }

        private LevelCombatant EnsureCombatant(GameObject enemyObject, LevelRoomDefinition room)
        {
            LevelCombatant combatant = enemyObject.GetComponent<LevelCombatant>();
            if (combatant == null)
            {
                combatant = enemyObject.AddComponent<LevelCombatant>();
            }

            float hp = room.roomType == LevelRoomType.EliteCombat || room.roomType == LevelRoomType.Boss
                ? eliteEnemyHp
                : normalEnemyHp;
            combatant.ResetHp(hp);

            if (fishHitSprite != null)
            {
                combatant.SetHitSprite(fishHitSprite);
            }

            FishMonsterController fish = enemyObject.GetComponent<FishMonsterController>();
            if (fish != null && player != null)
            {
                fish.Target = player;
            }

            return combatant;
        }

        private void OnEnemyDied(LevelCombatant enemy)
        {
            activeEnemies.Remove(enemy);
            if (activeEnemies.Count > 0 || state != LevelFlowState.InCombat)
            {
                return;
            }

            BeginCombatComplete();
        }

        private void BeginCombatComplete()
        {
            state = LevelFlowState.ResolvingRoom;
            SetCombatPresentationActive(false);
            hud.SetStatus("战斗结束", "怪物已清除，镜头回到探索视角。");

            if (cameraModeController != null)
            {
                cameraModeController.SwitchToExplore();
            }

            Schedule(PendingFlowAction.ResolveRoom, currentRoom, combatEndCameraDelay);
        }

        private Vector3 ResolveEnemySpawnPosition(int index, int count)
        {
            if (enemySpawnPoints != null && enemySpawnPoints.Length > 0)
            {
                Transform spawn = enemySpawnPoints[index % enemySpawnPoints.Length];
                if (spawn != null)
                {
                    return spawn.position;
                }
            }

            Vector3 center = player != null ? player.position + player.forward * 4f : transform.position + Vector3.forward * 4f;
            float angle = count <= 1 ? 0f : (360f / count) * index;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.right * enemySpawnRadius;
            offset.y = 0f;
            return center + offset;
        }

        private void MovePlayerToRoomAnchor(int index)
        {
            if (player == null || roomAnchors == null || roomAnchors.Length == 0)
            {
                return;
            }

            Transform anchor = roomAnchors[Mathf.Clamp(index, 0, roomAnchors.Length - 1)];
            if (anchor == null)
            {
                return;
            }

            player.position = anchor.position;
        }

        private string RoomSummary(LevelRoomDefinition room)
        {
            string summary = room.roomType + " / " + room.rewardPool;
            if (!string.IsNullOrWhiteSpace(room.rewardPreview))
            {
                summary += "\n奖励预告：" + room.rewardPreview;
            }

            if (!string.IsNullOrWhiteSpace(room.roomNote))
            {
                summary += "\n" + room.roomNote;
            }

            return summary;
        }

        private string RouteDisplayName()
        {
            switch (routeId)
            {
                case LevelRouteId.UndergroundFirstFloor:
                    return "地下第一层";
                default:
                    return routeId.ToString();
            }
        }

        private void ResolveReferences()
        {
            if (playerController == null && player != null)
            {
                playerController = player.GetComponent<PlayerCharacterController>();
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerCharacterController>(FindObjectsInactive.Include);
            }

            if (player == null && playerController != null)
            {
                player = playerController.transform;
            }

            if (cameraModeController == null)
            {
                cameraModeController = FindFirstObjectByType<CinemachineCameraModeController>();
            }

            if (weaponView == null)
            {
                weaponView = FindFirstObjectByType<PrototypeFirstPersonWeaponView>(FindObjectsInactive.Include);
            }

            if (weaponShooter == null && weaponView != null)
            {
                weaponShooter = weaponView.GetComponent<LevelWeaponProjectileShooter>();
                if (weaponShooter == null)
                {
                    weaponShooter = weaponView.gameObject.AddComponent<LevelWeaponProjectileShooter>();
                }
            }

            if (hud == null)
            {
                hud = GetComponentInChildren<LevelFlowHud>();
                if (hud == null)
                {
                    GameObject hudObject = new GameObject("LevelFlowHud");
                    hudObject.transform.SetParent(transform, false);
                    hud = hudObject.AddComponent<LevelFlowHud>();
                }
            }

            if (enemyPrefab == null)
            {
                FishMonsterController fish = FindFirstObjectByType<FishMonsterController>();
                if (fish != null)
                {
                    enemyPrefab = fish.transform;
                    fish.gameObject.SetActive(false);
                }
            }

            if (fishHitSprite == null)
            {
                SpriteRenderer[] sprites = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] != null && sprites[i].name.ToLowerInvariant().Contains("fish_hit"))
                    {
                        fishHitSprite = sprites[i].sprite;
                        sprites[i].gameObject.SetActive(false);
                        break;
                    }
                }
            }

            hud.Initialize();
            CachePlayerVisuals();
            CachePlayerPhysics();
        }

        private void ConfigureWeaponShooter()
        {
            EnsureWeaponShooter();
            if (weaponShooter == null)
            {
                return;
            }

            weaponShooter.SetFlowDirector(this);
            weaponShooter.IsEnabledByCombat = false;
            SetWeaponPresentationActive(false);
        }

        private void SetCombatPresentationActive(bool active)
        {
            if (active)
            {
                SetWeaponPresentationActive(true);
                SetWeaponCombatEnabled(true);
                SetPlayerHiddenForCombat(true);
                return;
            }

            SetWeaponCombatEnabled(false);
            SetWeaponPresentationActive(false);
            SetPlayerHiddenForCombat(false);
        }

        private void SetWeaponCombatEnabled(bool enabled)
        {
            EnsureWeaponShooter();
            if (weaponShooter != null)
            {
                weaponShooter.IsEnabledByCombat = enabled;
            }
        }

        private void SetWeaponPresentationActive(bool active)
        {
            if (weaponView == null)
            {
                weaponView = FindFirstObjectByType<PrototypeFirstPersonWeaponView>(FindObjectsInactive.Include);
            }

            if (weaponView == null)
            {
                return;
            }

            GameObject weaponObject = weaponView.gameObject;
            if (weaponObject.activeSelf != active)
            {
                weaponObject.SetActive(active);
            }

            if (active)
            {
                Camera mainCamera = Camera.main;
                weaponView.RefreshRuntimeView(mainCamera);
                EnsureWeaponShooter();
                if (weaponShooter != null)
                {
                    weaponShooter.SetAimCamera(mainCamera);
                }
            }
        }

        private void EnsureWeaponShooter()
        {
            if (weaponView == null)
            {
                weaponView = FindFirstObjectByType<PrototypeFirstPersonWeaponView>(FindObjectsInactive.Include);
            }

            if (weaponShooter == null && weaponView != null)
            {
                weaponShooter = weaponView.GetComponent<LevelWeaponProjectileShooter>();
                if (weaponShooter == null)
                {
                    weaponShooter = weaponView.gameObject.AddComponent<LevelWeaponProjectileShooter>();
                }

                weaponShooter.SetFlowDirector(this);
            }
        }

        private void CachePlayerVisuals()
        {
            if (player == null)
            {
                playerVisualRenderers = null;
                return;
            }

            playerVisualRenderers = player.GetComponentsInChildren<Renderer>(true);
        }

        private void CachePlayerPhysics()
        {
            if (player == null)
            {
                playerBody = null;
                playerColliders = null;
                return;
            }

            if (playerBody == null)
            {
                playerBody = player.GetComponent<Rigidbody>();
            }

            if (playerColliders == null || playerColliders.Length == 0)
            {
                playerColliders = player.GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetPlayerHiddenForCombat(bool hidden)
        {
            if (!hidePlayerVisualsDuringCombat && !disablePlayerMovementDuringCombat && !freezePlayerPhysicsDuringCombat)
            {
                return;
            }

            if (playerController == null && player != null)
            {
                playerController = player.GetComponent<PlayerCharacterController>();
            }

            if (playerVisualRenderers == null || playerVisualRenderers.Length == 0)
            {
                CachePlayerVisuals();
            }

            if (playerBody == null || playerColliders == null || playerColliders.Length == 0)
            {
                CachePlayerPhysics();
            }

            if (hidden)
            {
                if (playerHiddenForCombat)
                {
                    return;
                }

                if (hidePlayerVisualsDuringCombat && playerVisualRenderers != null)
                {
                    playerRendererEnabledBeforeCombat = new bool[playerVisualRenderers.Length];
                    for (int i = 0; i < playerVisualRenderers.Length; i++)
                    {
                        Renderer playerRenderer = playerVisualRenderers[i];
                        if (playerRenderer == null)
                        {
                            continue;
                        }

                        playerRendererEnabledBeforeCombat[i] = playerRenderer.enabled;
                        playerRenderer.enabled = false;
                    }
                }

                if (disablePlayerMovementDuringCombat && playerController != null)
                {
                    playerMovementEnabledBeforeCombat = playerController.MovementEnabled;
                    playerController.SetMovementEnabled(false);
                }

                if (freezePlayerPhysicsDuringCombat)
                {
                    FreezePlayerPhysicsForCombat();
                }

                playerHiddenForCombat = true;
                return;
            }

            if (!playerHiddenForCombat)
            {
                return;
            }

            RestorePlayerPhysicsAfterCombat();

            if (hidePlayerVisualsDuringCombat
                && playerVisualRenderers != null
                && playerRendererEnabledBeforeCombat != null)
            {
                int count = Mathf.Min(playerVisualRenderers.Length, playerRendererEnabledBeforeCombat.Length);
                for (int i = 0; i < count; i++)
                {
                    Renderer playerRenderer = playerVisualRenderers[i];
                    if (playerRenderer != null)
                    {
                        playerRenderer.enabled = playerRendererEnabledBeforeCombat[i];
                    }
                }
            }

            if (disablePlayerMovementDuringCombat && playerController != null)
            {
                playerController.SetMovementEnabled(playerMovementEnabledBeforeCombat);
            }

            playerHiddenForCombat = false;
            playerRendererEnabledBeforeCombat = null;
        }

        private void FreezePlayerPhysicsForCombat()
        {
            if (playerPhysicsFrozenForCombat || player == null)
            {
                return;
            }

            playerPositionBeforeCombat = player.position;
            playerRotationBeforeCombat = player.rotation;

            if (playerBody != null)
            {
                playerBodyKinematicBeforeCombat = playerBody.isKinematic;
                playerBodyConstraintsBeforeCombat = playerBody.constraints;
                ClearPlayerBodyMotionIfDynamic();
                playerBody.isKinematic = true;
                playerBody.constraints = RigidbodyConstraints.FreezeAll;
            }

            if (playerColliders != null)
            {
                playerColliderEnabledBeforeCombat = new bool[playerColliders.Length];
                for (int i = 0; i < playerColliders.Length; i++)
                {
                    Collider playerCollider = playerColliders[i];
                    if (playerCollider == null)
                    {
                        continue;
                    }

                    playerColliderEnabledBeforeCombat[i] = playerCollider.enabled;
                    playerCollider.enabled = false;
                }
            }

            playerPhysicsFrozenForCombat = true;
        }

        private void RestorePlayerPhysicsAfterCombat()
        {
            if (!playerPhysicsFrozenForCombat)
            {
                return;
            }

            if (playerBody != null)
            {
                playerBody.position = playerPositionBeforeCombat;
                playerBody.rotation = playerRotationBeforeCombat;
            }

            if (player != null)
            {
                player.SetPositionAndRotation(playerPositionBeforeCombat, playerRotationBeforeCombat);
            }

            if (playerBody != null)
            {
                playerBody.constraints = playerBodyConstraintsBeforeCombat;
                playerBody.isKinematic = playerBodyKinematicBeforeCombat;
                ClearPlayerBodyMotionIfDynamic();
            }

            if (playerColliders != null && playerColliderEnabledBeforeCombat != null)
            {
                int count = Mathf.Min(playerColliders.Length, playerColliderEnabledBeforeCombat.Length);
                for (int i = 0; i < count; i++)
                {
                    Collider playerCollider = playerColliders[i];
                    if (playerCollider != null)
                    {
                        playerCollider.enabled = playerColliderEnabledBeforeCombat[i];
                    }
                }
            }

            playerPhysicsFrozenForCombat = false;
            playerColliderEnabledBeforeCombat = null;
        }

        private void ClearPlayerBodyMotionIfDynamic()
        {
            if (playerBody == null || playerBody.isKinematic)
            {
                return;
            }

            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }

        private void ClearActiveEnemies()
        {
            UnsubscribeEnemies();
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                if (activeEnemies[i] != null)
                {
                    Destroy(activeEnemies[i].gameObject);
                }
            }

            activeEnemies.Clear();
        }

        private void ClearRoomInteractable()
        {
            if (currentRoomInteractable == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(currentRoomInteractable.gameObject);
            }
            else
            {
                DestroyImmediate(currentRoomInteractable.gameObject);
            }

            currentRoomInteractable = null;
        }

        private void UnsubscribeEnemies()
        {
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                if (activeEnemies[i] != null)
                {
                    activeEnemies[i].Died -= OnEnemyDied;
                }
            }
        }

        private void Schedule(PendingFlowAction action, LevelRoomDefinition room, float delay)
        {
            pendingAction = action;
            pendingRoom = room;
            pendingActionAt = Time.unscaledTime + Mathf.Max(0f, delay);
        }

        private void BuildUndergroundFirstFloorRoute()
        {
            roomsById.Clear();
            AddRoom(new LevelRoomDefinition
            {
                roomId = "b1_entry_combat",
                displayName = "潮湿石门",
                roomType = LevelRoomType.Blessing,
                rewardPool = LevelRewardPool.MajorFind,
                encounterId = "fish_intro",
                rewardPreview = "初始祝福",
                roomNote = "第一间固定为战前祝福选择，选择后才生成鱼怪。",
                enemyCount = 1,
                startsCombatAfterChoice = true,
                choices =
                {
                    new LevelRoomChoiceDefinition { choiceId = "entry_blade_flame", displayName = "灵火入刃", description = "本房开始前获得 20% 子弹伤害。", damageBonus = 0.2f },
                    new LevelRoomChoiceDefinition { choiceId = "entry_gold_echo", displayName = "碎金试炼", description = "获得 20 金币，然后触发战斗。", goldDelta = 20 },
                },
                exits =
                {
                    Door("b1_blessing", "泛光符门", LevelRoomType.Blessing, LevelRewardPool.MajorFind, "三选一祝福", true, false),
                    Door("b1_story_event", "低语侧室", LevelRoomType.StoryEvent, LevelRewardPool.SpecialDoor, "NPC/事件", false, false),
                },
            });

            AddRoom(new LevelRoomDefinition
            {
                roomId = "b1_blessing",
                displayName = "潮火祝福",
                roomType = LevelRoomType.Blessing,
                rewardPool = LevelRewardPool.MajorFind,
                rewardPreview = "本局强化",
                roomNote = "先选择 Major Find 式强化，再生成怪物进入战斗。",
                enemyCount = 1,
                startsCombatAfterChoice = true,
                choices =
                {
                    new LevelRoomChoiceDefinition { choiceId = "blade_heat", displayName = "剑火入脉", description = "武器子弹伤害提高 25%。", damageBonus = 0.25f },
                    new LevelRoomChoiceDefinition { choiceId = "quick_gold", displayName = "碎金回响", description = "获得 30 金币，用于后续商店原型。", goldDelta = 30 },
                },
                exits =
                {
                    Door("b1_cross_combat", "兽影甬道", LevelRoomType.Combat, LevelRewardPool.MinorFind, "局外材料", true, false),
                    Door("b1_elite_combat", "刻痕石门", LevelRoomType.EliteCombat, LevelRewardPool.SpecialDoor, "精英奖励", false, true),
                },
            });

            AddRoom(new LevelRoomDefinition
            {
                roomId = "b1_story_event",
                displayName = "井边残影",
                roomType = LevelRoomType.StoryEvent,
                rewardPool = LevelRewardPool.SpecialDoor,
                rewardPreview = "事件/代价",
                roomNote = "先处理事件/代价选择，再生成怪物；后续可挂 NPC 对话或限时宝箱。",
                enemyCount = 1,
                startsCombatAfterChoice = true,
                choices =
                {
                    new LevelRoomChoiceDefinition { choiceId = "listen", displayName = "听完低语", description = "获得 15 金币。", goldDelta = 15 },
                    new LevelRoomChoiceDefinition { choiceId = "take_mark", displayName = "触碰刻印", description = "武器子弹伤害提高 15%。", damageBonus = 0.15f },
                },
                exits =
                {
                    Door("b1_cross_combat", "回到主路", LevelRoomType.Combat, LevelRewardPool.MajorFind, "战斗奖励", true, false),
                },
            });

            AddRoom(new LevelRoomDefinition
            {
                roomId = "b1_cross_combat",
                displayName = "交错水廊",
                roomType = LevelRoomType.Combat,
                rewardPool = LevelRewardPool.MinorFind,
                encounterId = "fish_pair",
                rewardPreview = "材料/金币",
                roomNote = "战前选择局外收益倾向，然后测试多目标和清房结算。",
                enemyCount = 2,
                startsCombatAfterChoice = true,
                choices =
                {
                    new LevelRoomChoiceDefinition { choiceId = "minor_bones", displayName = "拾取残骨", description = "获得 20 金币作为局外资源占位。", goldDelta = 20 },
                    new LevelRoomChoiceDefinition { choiceId = "minor_focus", displayName = "凝神进击", description = "本局子弹伤害提高 10%。", damageBonus = 0.1f },
                },
                exits =
                {
                    Door("b1_elite_combat", "下沉斗室", LevelRoomType.EliteCombat, LevelRewardPool.SpecialDoor, "精英/小 Boss", false, true),
                    Door("b1_rest", "浅光泉眼", LevelRoomType.Rest, LevelRewardPool.SpecialDoor, "休整", false, false),
                },
            });

            AddRoom(new LevelRoomDefinition
            {
                roomId = "b1_elite_combat",
                displayName = "下沉斗室",
                roomType = LevelRoomType.EliteCombat,
                rewardPool = LevelRewardPool.SpecialDoor,
                encounterId = "elite_fish",
                rewardPreview = "高稀有度奖励",
                roomNote = "先确认高风险奖励，再触发地下第一层的小强度尖峰。",
                enemyCount = 1,
                startsCombatAfterChoice = true,
                choices =
                {
                    new LevelRoomChoiceDefinition { choiceId = "elite_risk", displayName = "接下刻痕", description = "高风险门：伤害提高 20%，随后生成精英怪。", damageBonus = 0.2f },
                    new LevelRoomChoiceDefinition { choiceId = "elite_gold", displayName = "稳取供品", description = "获得 35 金币，随后生成精英怪。", goldDelta = 35 },
                },
                exits =
                {
                    Door("b1_rest", "泉眼出口", LevelRoomType.Rest, LevelRewardPool.SpecialDoor, "休整", false, false),
                },
            });

            AddRoom(new LevelRoomDefinition
            {
                roomId = "b1_rest",
                displayName = "浅光泉眼",
                roomType = LevelRoomType.Rest,
                rewardPool = LevelRewardPool.SpecialDoor,
                rewardPreview = "休整完成",
                roomNote = "区域之间的休整/中转节点占位。",
            });
        }

        private void AddRoom(LevelRoomDefinition room)
        {
            roomsById[room.roomId] = room;
        }

        private static LevelDoorDefinition Door(
            string targetRoomId,
            string displayName,
            LevelRoomType roomType,
            LevelRewardPool rewardPool,
            string rewardPreview,
            bool canReroll,
            bool risk)
        {
            return new LevelDoorDefinition
            {
                targetRoomId = targetRoomId,
                displayName = displayName,
                roomType = roomType,
                rewardPool = rewardPool,
                rewardPreview = rewardPreview,
                canReroll = canReroll,
                isRiskDoor = risk,
            };
        }

        private enum PendingFlowAction
        {
            None,
            StartRoomContent,
            StartCombat,
            ResolveRoom,
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private enum DebugFlowKey
        {
            SelectChoice,
            KillEnemies,
            SelectDoor,
        }
#endif
    }
}
