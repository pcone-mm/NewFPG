using System.Collections.Generic;
using UnityEngine;
using NewFPG.CameraRig;
using NewFPG.Characters;
using NewFPG.Combat;
using NewFPG.Monsters;
using NewFPG.Prototype;
using Pathfinding;
using UnityEngine.Serialization;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Level
{
    public sealed class LevelFlowDirector : MonoBehaviour
    {
        [Header("路线配置")]
        [Tooltip("当前 Director 运行的路线标识，用于和绑定的 LevelRouteTable 做一致性检查。")]
        [SerializeField] private LevelRouteId routeId = LevelRouteId.UndergroundFirstFloor;

        [Tooltip("路线表：配置起始房间、房间触发方式、选择项和出口门。")]
        [SerializeField] private LevelRouteTable routeTable;

        [Tooltip("是否在 Start 时自动进入路线。测试或手动调试时可以关闭。")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("刷怪表：房间通过 encounterId 到这里查找波次和怪物配置。")]
        [SerializeField] private LevelEncounterTable encounterTable;

        [Header("场景引用")]
        [Tooltip("玩家 Transform。为空时会在场景中查找 PlayerCharacterController。")]
        [SerializeField] private Transform player;

        [Tooltip("怪物刷点。为空时会按玩家前方的环形 fallback 位置生成。")]
        [SerializeField] private Transform[] enemySpawnPoints;

        [Tooltip("房间锚点。进入第 N 个房间时会把玩家移动到对应锚点。")]
        [SerializeField] private Transform[] roomAnchors;

        [Tooltip("相机模式控制器。进战斗切 Battle，清房/探索切 Explore。")]
        [SerializeField] private CinemachineCameraModeController cameraModeController;

        [Tooltip("第一人称武器表现。战斗时会挂到当前可用相机下。")]
        [SerializeField] private PrototypeFirstPersonWeaponView weaponView;

        [Tooltip("武器战斗 HUD。为空时会尝试从 weaponView 上获取或添加。")]
        [SerializeField] private PrototypeWeaponCombatHud weaponCombatHud;

        [Tooltip("怪物战斗 HUD。为空时会在 Director 子物体中查找或创建。")]
        [SerializeField] private MonsterCombatHud monsterCombatHud;

        [Tooltip("怪物行为树节点“移动到战斗区域”使用的可选战斗区域图。")]
        [SerializeField] private BattleArenaZoneMap battleArenaZoneMap;

        [Tooltip("Optional A* root used for monster movement. Created at runtime when missing.")]
        [SerializeField] private AstarPath astarPath;

        [FormerlySerializedAs("autoBuildNavMeshSurface")]
        [SerializeField] private bool autoBuildAstarGraph = true;

        [Header("A* Runtime Recast")]
        [SerializeField, Min(0.05f)] private float astarCellSize = 0.25f;
        [SerializeField, Min(0.05f)] private float astarCharacterRadius = 0.35f;
        [SerializeField, Min(0.1f)] private float astarCharacterHeight = 1.2f;
        [SerializeField, Min(0f)] private float astarWalkableClimb = 0.35f;
        [SerializeField, Range(0f, 90f)] private float astarMaxSlope = 45f;
        [SerializeField] private Vector3 astarGraphBoundsOffset = new Vector3(0f, 2f, 0f);
        [SerializeField] private Vector3 astarGraphBoundsSize = new Vector3(24f, 8f, 24f);
        [Tooltip("Only these layers are scanned by A* Recast. Nothing means A* scans nothing.")]
        [InspectorName("Astar Graph Layer Mask")]
        [SerializeField] private LayerMask astarGraphScanLayerMask = 0;

        [Tooltip("关卡流程 HUD。为空时会在 Director 子物体中查找或创建。")]
        [SerializeField] private LevelFlowHud hud;

        [Tooltip("房间交互物 prefab。为空时会创建一个临时球体交互物。")]
        [SerializeField] private LevelRoomInteractable roomInteractablePrefab;

        [Header("时序")]
        [Tooltip("进入房间后，到开始执行房间内容前的等待时间。")]
        [SerializeField, Min(0f)] private float roomIntroSeconds = 0.75f;

        [Tooltip("战斗结束后，到房间结算/开门前的等待时间。")]
        [SerializeField, Min(0f)] private float combatEndCameraDelay = 0.8f;

        [Tooltip("选择项或交互触发后，到执行完成方式前的等待时间。")]
        [SerializeField, Min(0f)] private float eventResolveSeconds = 0.45f;

        [Tooltip("没有显式刷点时，fallback 环形刷怪位置的半径。")]
        [SerializeField, Min(0f)] private float enemySpawnRadius = 1.6f;

        [Header("房间交互")]
        [Tooltip("自动生成交互物时，放在玩家前方的距离。")]
        [SerializeField, Min(0.5f)] private float interactableForwardOffset = 2.2f;

        [Tooltip("自动生成交互物时，距离地面的高度。")]
        [SerializeField, Min(0.5f)] private float interactableHeight = 0.8f;

        [Header("战斗表现")]
        [Tooltip("进入战斗时是否隐藏玩家视觉渲染器。")]
        [SerializeField] private bool hidePlayerVisualsDuringCombat = true;

        [Tooltip("进入战斗时是否禁用玩家移动控制。")]
        [SerializeField] private bool disablePlayerMovementDuringCombat = true;

        [Tooltip("进入战斗时是否冻结玩家物理，退出战斗后恢复。")]
        [SerializeField] private bool freezePlayerPhysicsDuringCombat = true;

        private readonly Dictionary<string, LevelRoomDefinition> roomsById = new Dictionary<string, LevelRoomDefinition>();
        private readonly List<LevelCombatant> activeEnemies = new List<LevelCombatant>();
        private readonly System.Random encounterRandom = new System.Random();
        private LevelRoomDefinition currentRoom;
        private LevelRoomInteractable currentRoomInteractable;
        private PlayerCharacterController playerController;
        private Rigidbody playerBody;
        private Collider[] playerColliders;
        private Renderer[] playerVisualRenderers;
        private CombatVitals playerVitals;
        private CombatResourcePool playerResourcePool;
        private PlayerWeaponCaster playerWeaponCaster;
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
        private LevelEncounterDefinition currentEncounter;
        private int currentWaveIndex = -1;
        private string pendingEncounterIdOverride;
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
            weaponCombatHud = weaponView != null ? weaponView.GetComponent<PrototypeWeaponCombatHud>() : null;
            monsterCombatHud = GetComponentInChildren<MonsterCombatHud>();
            astarPath = FindFirstObjectByType<AstarPath>(FindObjectsInactive.Include);
            hud = GetComponentInChildren<LevelFlowHud>();
            ResolveDefaultRouteTable();
            ResolveDefaultEncounterTable();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureRuntimeAstarGraph();
            BuildRouteIndex();
            ConfigureWeaponHud();
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
            PruneDeadEnemiesAndAdvanceIfCleared();

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
                case PendingFlowAction.StartNextWave:
                    StartCurrentWave(room);
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
            if (monsterCombatHud != null)
            {
                monsterCombatHud.Clear();
            }

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
            currentEncounter = null;
            currentWaveIndex = -1;
            pendingEncounterIdOverride = null;
            ClearRoomInteractable();
            ClearActiveEnemies();
            SetCombatPresentationActive(false);
            BuildRouteIndex();
            if (routeTable == null)
            {
                CompleteRoute("Missing LevelRouteTable.");
                return;
            }

            if (string.IsNullOrWhiteSpace(routeTable.StartRoomId))
            {
                CompleteRoute("LevelRouteTable has no start room.");
                return;
            }

            EnterRoom(routeTable.StartRoomId);
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
                    IDamageable damageable = activeEnemies[i].GetComponent<IDamageable>();
                    if (IsDamageableAlive(damageable))
                    {
                        Vector3 hitPoint = damageable.AimTransform != null
                            ? damageable.AimTransform.position
                            : activeEnemies[i].transform.position;
                        damageable.ReceiveDamage(new DamagePayload(FatalDamageFor(activeEnemies[i], damageable), gameObject, hitPoint));
                    }
                }
            }

            PruneDeadEnemiesAndAdvanceIfCleared();
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

            BeginTriggeredRoom(currentRoom);
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

            pendingEncounterIdOverride = null;
            if (ShouldWaitForRoomInteraction(room))
            {
                BeginRoomInteraction(room);
                return;
            }

            BeginTriggeredRoom(room);
        }

        private void BeginTriggeredRoom(LevelRoomDefinition room)
        {
            if (room == null || room != currentRoom)
            {
                return;
            }

            if (HasRoomChoices(room))
            {
                BeginEventRoom(room);
                return;
            }

            ClearRoomInteractable();
            ExecuteRoomCompletion(room, null);
        }

        private static bool ShouldWaitForRoomInteraction(LevelRoomDefinition room)
        {
            return room != null && room.triggerMode == LevelRoomTriggerMode.OnInteract;
        }

        private static bool HasRoomChoices(LevelRoomDefinition room)
        {
            return room != null && room.choices != null && room.choices.Count > 0;
        }

        private void BeginCombatRoom(LevelRoomDefinition room)
        {
            if (room == null || room != currentRoom)
            {
                return;
            }

            state = LevelFlowState.InCombat;
            hud.SetStatus(
                "战斗：" + room.displayName,
                "点击屏幕上的武器发射子弹。\n击杀全部怪物后进入清房结算。");

            if (cameraModeController != null)
            {
                cameraModeController.SwitchToBattle();
            }

            SetCombatPresentationActive(true);
            ActivateBattleArenaZoneMap();
            EnsureRuntimeAstarGraph();
            SpawnEncounter(room);
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
                ExecuteRoomCompletion(room, null);
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
            }

            if (choice.goldDelta != 0)
            {
                gold += choice.goldDelta;
            }

            string encounterOverride = string.IsNullOrWhiteSpace(choice.encounterIdOverride)
                ? null
                : choice.encounterIdOverride;
            hud.SetStatus(
                "获得：" + choice.displayName,
                "伤害加成 +" + Mathf.RoundToInt(damageBonus * 100f) + "%\n金币 " + gold);
            ExecuteRoomCompletion(currentRoom, encounterOverride);
        }

        private void ExecuteRoomCompletion(LevelRoomDefinition room, string encounterOverride)
        {
            if (room == null || room != currentRoom)
            {
                return;
            }

            ClearRoomInteractable();
            pendingEncounterIdOverride = encounterOverride;
            state = LevelFlowState.ResolvingRoom;

            switch (room.completionMode)
            {
                case LevelRoomCompletionMode.StartEncounter:
                    hud.SetStatus(
                        "Triggered: " + room.displayName,
                        "Combat is starting.");
                    Schedule(PendingFlowAction.StartCombat, room, eventResolveSeconds);
                    break;
                case LevelRoomCompletionMode.CompleteRoute:
                    CompleteRoute("Route complete.");
                    break;
                default:
                    Schedule(PendingFlowAction.ResolveRoom, room, eventResolveSeconds);
                    break;
            }
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
            currentEncounter = null;
            currentWaveIndex = -1;
            pendingEncounterIdOverride = null;
            pendingAction = PendingFlowAction.None;
            pendingRoom = null;
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
            currentEncounter = null;
            currentWaveIndex = -1;

            string encounterId = !string.IsNullOrWhiteSpace(pendingEncounterIdOverride)
                ? pendingEncounterIdOverride
                : room != null ? room.encounterId : null;
            pendingEncounterIdOverride = null;

            if (room == null)
            {
                FailEncounterConfiguration("Cannot start encounter for a null room.");
                return;
            }

            if (encounterTable == null)
            {
                FailEncounterConfiguration("Missing LevelEncounterTable.");
                return;
            }

            if (string.IsNullOrWhiteSpace(encounterId))
            {
                FailEncounterConfiguration("Room has no encounter id: " + room.roomId);
                return;
            }

            currentEncounter = encounterTable.FindEncounter(encounterId);
            if (currentEncounter == null)
            {
                FailEncounterConfiguration("Level encounter table has no encounter id: " + encounterId);
                return;
            }

            if (currentEncounter.waves == null || currentEncounter.waves.Count == 0)
            {
                FailEncounterConfiguration("Level encounter has no waves: " + encounterId);
                return;
            }

            currentWaveIndex = 0;
            StartCurrentWave(room);
        }

        private void StartCurrentWave(LevelRoomDefinition room)
        {
            if (room == null || room != currentRoom || state != LevelFlowState.InCombat)
            {
                return;
            }

            if (currentEncounter == null
                || currentEncounter.waves == null
                || currentWaveIndex < 0
                || currentWaveIndex >= currentEncounter.waves.Count)
            {
                FailEncounterConfiguration("Invalid encounter wave state.");
                return;
            }

            LevelEncounterWave wave = currentEncounter.waves[currentWaveIndex];
            LevelSpawnRequest[] requests = LevelEncounterResolver.Resolve(wave, encounterRandom);
            if (requests == null || requests.Length == 0)
            {
                FailEncounterConfiguration("Level encounter wave resolved no spawn requests: " + currentEncounter.encounterId + "/" + wave.waveId);
                return;
            }

            for (int i = 0; i < requests.Length; i++)
            {
                Vector3 spawnPosition = ResolveEnemySpawnPosition(i, requests.Length);
                Transform enemyTransform = SpawnEnemy(spawnPosition, i, requests[i]);
                if (enemyTransform == null)
                {
                    FailEncounterConfiguration("Level spawn request has no monster prefab: " + requests[i].MonsterId);
                    return;
                }

                LevelCombatant combatant = EnsureCombatant(enemyTransform.gameObject, room, requests[i]);
                TrackActiveEnemy(combatant);
            }

            hud.SetStatus(
                "Combat: " + room.displayName,
                "Wave " + (currentWaveIndex + 1) + " / " + currentEncounter.waves.Count);
        }

        private void FailEncounterConfiguration(string message)
        {
            Debug.LogError(message, this);
            ClearActiveEnemies();
            currentEncounter = null;
            currentWaveIndex = -1;
            SetCombatPresentationActive(false);
            state = LevelFlowState.ResolvingRoom;
            Schedule(PendingFlowAction.ResolveRoom, currentRoom, eventResolveSeconds);
        }

        private void TrackActiveEnemy(LevelCombatant combatant)
        {
            if (combatant == null)
            {
                return;
            }

            combatant.Died -= OnEnemyDied;
            combatant.Died += OnEnemyDied;
            activeEnemies.Add(combatant);
        }

        private Transform SpawnEnemy(Vector3 position, int index, LevelSpawnRequest request)
        {
            position = FlattenEnemySpawnPosition(position);
            if (request.MonsterPrefab != null)
            {
                GameObject spawned = Instantiate(request.MonsterPrefab, position, request.MonsterPrefab.transform.rotation);
                spawned.name = request.MonsterPrefab.name + "_RoomEnemy_" + index;
                MonsterConfigBinding binding = spawned.GetComponent<MonsterConfigBinding>();
                if (binding != null)
                {
                    binding.SetHomePositionToCurrent();
                }

                spawned.SetActive(true);
                return spawned.transform;
            }

            return null;
        }

        private LevelCombatant EnsureCombatant(GameObject enemyObject, LevelRoomDefinition room, LevelSpawnRequest request)
        {
            return EnsureCombatant(enemyObject, room, request.HasMaxHealthOverride, request.MaxHealthOverride);
        }

        private LevelCombatant EnsureCombatant(GameObject enemyObject, LevelRoomDefinition room)
        {
            return EnsureCombatant(enemyObject, room, false, 1f);
        }

        private LevelCombatant EnsureCombatant(GameObject enemyObject, LevelRoomDefinition room, bool overrideMaxHealth, float maxHealth)
        {
            CombatVitals vitals = enemyObject.GetComponent<CombatVitals>();
            if (vitals == null)
            {
                vitals = enemyObject.AddComponent<CombatVitals>();
            }

            LevelCombatant combatant = enemyObject.GetComponent<LevelCombatant>();
            if (combatant == null)
            {
                combatant = enemyObject.AddComponent<LevelCombatant>();
            }

            if (overrideMaxHealth)
            {
                combatant.ResetHp(maxHealth);
            }
            else
            {
                vitals.ResetVitals();
            }

            TrackMonsterHud(combatant, room);

            return combatant;
        }

        private void OnEnemyDied(LevelCombatant enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= OnEnemyDied;
                if (monsterCombatHud != null)
                {
                    monsterCombatHud.Untrack(enemy);
                }
            }

            activeEnemies.Remove(enemy);
            if (activeEnemies.Count > 0 || state != LevelFlowState.InCombat)
            {
                return;
            }

            AdvanceEncounterOrComplete();
        }

        private void PruneDeadEnemiesAndAdvanceIfCleared()
        {
            if (activeEnemies.Count == 0)
            {
                return;
            }

            bool removed = false;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                LevelCombatant enemy = activeEnemies[i];
                if (enemy != null && !enemy.IsDead)
                {
                    continue;
                }

                if (enemy != null)
                {
                    enemy.Died -= OnEnemyDied;
                    if (monsterCombatHud != null)
                    {
                        monsterCombatHud.Untrack(enemy);
                    }
                }

                activeEnemies.RemoveAt(i);
                removed = true;
            }

            if (removed && activeEnemies.Count == 0 && state == LevelFlowState.InCombat)
            {
                AdvanceEncounterOrComplete();
            }
        }

        private void AdvanceEncounterOrComplete()
        {
            if (currentEncounter != null
                && currentEncounter.waves != null
                && currentWaveIndex + 1 < currentEncounter.waves.Count)
            {
                currentWaveIndex++;
                LevelEncounterWave nextWave = currentEncounter.waves[currentWaveIndex];
                float delay = nextWave != null ? nextWave.delayAfterPreviousWave : 0f;
                hud.SetStatus(
                    "Next wave",
                    "Wave " + (currentWaveIndex + 1) + " / " + currentEncounter.waves.Count);
                if (delay <= 0f)
                {
                    StartCurrentWave(currentRoom);
                }
                else
                {
                    Schedule(PendingFlowAction.StartNextWave, currentRoom, delay);
                }

                return;
            }

            BeginCombatComplete();
        }

        private void BeginCombatComplete()
        {
            state = LevelFlowState.ResolvingRoom;
            currentEncounter = null;
            currentWaveIndex = -1;
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
                    return FlattenEnemySpawnPosition(spawn.position);
                }
            }

            Vector3 center = player != null ? player.position + player.forward * 4f : transform.position + Vector3.forward * 4f;
            float angle = count <= 1 ? 0f : (360f / count) * index;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.right * enemySpawnRadius;
            offset.y = 0f;
            return FlattenEnemySpawnPosition(center + offset);
        }

        private static Vector3 FlattenEnemySpawnPosition(Vector3 position)
        {
            position.y = 0f;
            return position;
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
            ResolveDefaultRouteTable();
            ResolveDefaultEncounterTable();

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

            if (player != null)
            {
                if (playerVitals == null)
                {
                    playerVitals = player.GetComponent<CombatVitals>();
                }

                if (playerResourcePool == null)
                {
                    playerResourcePool = player.GetComponent<CombatResourcePool>();
                }

                if (playerWeaponCaster == null)
                {
                    playerWeaponCaster = player.GetComponent<PlayerWeaponCaster>();
                }

                if (playerVitals != null && player.GetComponent<PlayerHitFeedback>() == null)
                {
                    player.gameObject.AddComponent<PlayerHitFeedback>();
                }

                PlayerHitFeedback hitFeedback = player.GetComponent<PlayerHitFeedback>();
                if (hitFeedback != null)
                {
                    hitFeedback.SetTargetCamera(Camera.main);
                }
            }

            if (cameraModeController == null)
            {
                cameraModeController = FindFirstObjectByType<CinemachineCameraModeController>();
            }

            if (weaponView == null)
            {
                weaponView = FindFirstObjectByType<PrototypeFirstPersonWeaponView>(FindObjectsInactive.Include);
            }

            if (weaponCombatHud == null && weaponView != null)
            {
                weaponCombatHud = weaponView.GetComponent<PrototypeWeaponCombatHud>();
                if (weaponCombatHud == null)
                {
                    weaponCombatHud = weaponView.gameObject.AddComponent<PrototypeWeaponCombatHud>();
                }
            }

            if (astarPath == null)
            {
                astarPath = AstarPath.active != null
                    ? AstarPath.active
                    : FindFirstObjectByType<AstarPath>(FindObjectsInactive.Include);
            }

            ResolveBattleArenaZoneMap();

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

            hud.Initialize();
            EnsureMonsterCombatHud();
            BindWeaponCombatHud();
            CachePlayerVisuals();
            CachePlayerPhysics();
        }

        private BattleArenaZoneMap ResolveBattleArenaZoneMap()
        {
            if (battleArenaZoneMap != null)
            {
                return battleArenaZoneMap;
            }

            battleArenaZoneMap = GetComponentInChildren<BattleArenaZoneMap>(true);
            if (battleArenaZoneMap == null)
            {
                battleArenaZoneMap = FindFirstObjectByType<BattleArenaZoneMap>(FindObjectsInactive.Include);
            }

            return battleArenaZoneMap;
        }

        private void ActivateBattleArenaZoneMap()
        {
            BattleArenaZoneMap.SetCurrent(ResolveBattleArenaZoneMap());
        }

        public Bounds GetAstarGraphPreviewBounds()
        {
            return ResolveAstarGraphBounds();
        }

        public void SetAstarGraphPreviewBounds(Bounds worldBounds)
        {
            Vector3 sanitizedSize = new Vector3(
                Mathf.Max(1f, Mathf.Abs(worldBounds.size.x)),
                Mathf.Max(1f, Mathf.Abs(worldBounds.size.y)),
                Mathf.Max(1f, Mathf.Abs(worldBounds.size.z)));
            astarGraphBoundsSize = sanitizedSize;
            astarGraphBoundsOffset = worldBounds.center - ResolveAstarGraphAnchor();
        }

        public bool FitAstarGraphBoundsToBattleArena()
        {
            BattleArenaZoneMap zoneMap = ResolveBattleArenaZoneMap();
            if (zoneMap == null)
            {
                return false;
            }

            Vector2 arenaSize = zoneMap.ArenaSize;
            astarGraphBoundsSize = new Vector3(
                Mathf.Max(1f, arenaSize.x + astarCharacterRadius * 4f),
                Mathf.Max(1f, Mathf.Abs(astarGraphBoundsSize.y)),
                Mathf.Max(1f, arenaSize.y + astarCharacterRadius * 4f));
            astarGraphBoundsOffset = new Vector3(0f, astarGraphBoundsOffset.y, 0f);
            return true;
        }

        public void ScanAstarGraphNow()
        {
            EnsureRuntimeAstarGraph();
        }

        private void EnsureRuntimeAstarGraph()
        {
            if (!autoBuildAstarGraph)
            {
                return;
            }

            if (astarPath == null)
            {
                astarPath = AstarPath.active != null
                    ? AstarPath.active
                    : FindFirstObjectByType<AstarPath>(FindObjectsInactive.Include);
            }

            if (astarPath == null)
            {
                GameObject astarObject = new GameObject("RuntimeAstarPath");
                astarObject.transform.SetParent(transform, false);
                astarPath = astarObject.AddComponent<AstarPath>();
            }

            RecastGraph graph = astarPath.data.recastGraph;
            if (graph == null)
            {
                astarPath.data.FindGraphTypes();
                graph = astarPath.data.AddGraph<RecastGraph>();
            }

            ConfigureRuntimeRecastGraph(graph);
            astarPath.Scan(graph);
        }

        private void ConfigureRuntimeRecastGraph(RecastGraph graph)
        {
            if (graph == null)
            {
                return;
            }

            Bounds bounds = ResolveAstarGraphBounds();
            graph.cellSize = Mathf.Max(0.05f, astarCellSize);
            graph.characterRadius = Mathf.Max(0.05f, astarCharacterRadius);
            graph.walkableHeight = Mathf.Max(0.1f, astarCharacterHeight);
            graph.walkableClimb = Mathf.Min(Mathf.Max(0f, astarWalkableClimb), graph.walkableHeight);
            graph.maxSlope = Mathf.Clamp(astarMaxSlope, 0f, 90f);
            graph.forcedBoundsCenter = bounds.center;
            graph.forcedBoundsSize = bounds.size;
            graph.collectionSettings.collectionMode = RecastGraph.CollectionSettings.FilterMode.Layers;
            graph.collectionSettings.layerMask = astarGraphScanLayerMask;
            graph.collectionSettings.rasterizeColliders = true;
            graph.collectionSettings.rasterizeMeshes = true;
            graph.collectionSettings.rasterizeTerrain = true;
        }

        private Bounds ResolveAstarGraphBounds()
        {
            Vector3 sanitizedSize = new Vector3(
                Mathf.Max(1f, Mathf.Abs(astarGraphBoundsSize.x)),
                Mathf.Max(1f, Mathf.Abs(astarGraphBoundsSize.y)),
                Mathf.Max(1f, Mathf.Abs(astarGraphBoundsSize.z)));
            return new Bounds(ResolveAstarGraphAnchor() + astarGraphBoundsOffset, sanitizedSize);
        }

        private Vector3 ResolveAstarGraphAnchor()
        {
            BattleArenaZoneMap zoneMap = ResolveBattleArenaZoneMap();
            if (zoneMap != null)
            {
                return zoneMap.transform.TransformPoint(zoneMap.CenterOffset);
            }

            return player != null ? player.position : transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            if (!autoBuildAstarGraph)
            {
                return;
            }

            Bounds bounds = ResolveAstarGraphBounds();
            Color previousColor = Gizmos.color;

            Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.85f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.DrawSphere(bounds.center, 0.12f);

            Vector3 bottomCenter = bounds.center - Vector3.up * bounds.extents.y;
            Vector3 bottomSize = new Vector3(bounds.size.x, 0f, bounds.size.z);
            Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.35f);
            Gizmos.DrawWireCube(bottomCenter, bottomSize);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(0.1f, 0.65f, 1f, 0.95f);
            UnityEditor.Handles.Label(
                bounds.center + Vector3.up * (bounds.extents.y + 0.3f),
                $"A* Recast Bounds\n{bounds.size.x:0.#} x {bounds.size.y:0.#} x {bounds.size.z:0.#}");
#endif

            Gizmos.color = previousColor;
        }

        private void ResolveDefaultRouteTable()
        {
            if (routeTable != null)
            {
                return;
            }

#if UNITY_EDITOR
            routeTable = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelRouteTable>(LevelRouteTable.DefaultAssetPath);
#endif
        }

        private void ResolveDefaultEncounterTable()
        {
            if (encounterTable != null)
            {
                return;
            }

#if UNITY_EDITOR
            encounterTable = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelEncounterTable>(LevelEncounterTable.DefaultAssetPath);
#endif
        }

        private void BuildRouteIndex()
        {
            roomsById.Clear();
            ResolveDefaultRouteTable();
            if (routeTable == null)
            {
                Debug.LogError("Missing LevelRouteTable.", this);
                return;
            }

            if (routeTable.RouteId != routeId)
            {
                Debug.LogWarning("LevelRouteTable route id does not match director route id: " + routeTable.RouteId + " / " + routeId, this);
            }

            IReadOnlyList<LevelRoomDefinition> rooms = routeTable.Rooms;
            if (rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                LevelRoomDefinition room = rooms[i];
                if (room == null || string.IsNullOrWhiteSpace(room.roomId))
                {
                    continue;
                }

                roomsById[room.roomId] = room;
            }
        }

        private static bool IsDamageableAlive(IDamageable damageable)
        {
            if (damageable == null)
            {
                return false;
            }

            if (damageable is Object unityObject && unityObject == null)
            {
                return false;
            }

            return damageable.IsAlive;
        }

        private static float FatalDamageFor(LevelCombatant enemy, IDamageable damageable)
        {
            if (damageable is CombatVitals vitals && vitals != null)
            {
                return Mathf.Max(1f, vitals.CurrentHealth + vitals.CurrentShield);
            }

            return enemy != null ? Mathf.Max(1f, enemy.Hp) : 1f;
        }

        private void ConfigureWeaponHud()
        {
            SetWeaponPresentationActive(false);
            SetCombatPresentationActive(false);
        }

        private void SetCombatPresentationActive(bool active)
        {
            if (playerWeaponCaster != null)
            {
                playerWeaponCaster.SetCombatEnabled(active);
            }

            if (active)
            {
                SetWeaponPresentationActive(true);
                SetPlayerHiddenForCombat(true);
                return;
            }

            BattleArenaZoneMap.ClearCurrent(battleArenaZoneMap);
            SetWeaponPresentationActive(false);
            SetPlayerHiddenForCombat(false);
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
                Camera combatCamera = ResolveCombatCamera();
                if (monsterCombatHud != null)
                {
                    monsterCombatHud.SetTargetCamera(combatCamera);
                }

                AttachWeaponViewToActiveCamera(combatCamera);
                BindWeaponCombatHud();
                weaponView.RefreshRuntimeView(combatCamera);
                if (weaponCombatHud != null)
                {
                    weaponCombatHud.SetAimCamera(combatCamera);
                    weaponCombatHud.SetCombatEnabled(true);
                }
                return;
            }

            BindWeaponCombatHud();
            if (weaponCombatHud != null)
            {
                weaponCombatHud.SetCombatEnabled(false);
            }
        }

        private Camera ResolveCombatCamera()
        {
            Camera camera = Camera.main;
            if (IsUsableGameplayCamera(camera))
            {
                return camera;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (IsUsableGameplayCamera(cameras[i]))
                {
                    return cameras[i];
                }
            }

            return camera;
        }

        private bool IsUsableGameplayCamera(Camera camera)
        {
            return camera != null
                && camera.gameObject.scene == gameObject.scene
                && camera.cameraType == CameraType.Game
                && camera.isActiveAndEnabled
                && (weaponView == null || camera != weaponView.GetComponentInChildren<Camera>(true));
        }

        private void AttachWeaponViewToActiveCamera(Camera camera)
        {
            if (weaponView == null || camera == null)
            {
                return;
            }

            Transform viewTransform = weaponView.transform;
            if (viewTransform.parent == camera.transform && weaponView.gameObject.activeInHierarchy)
            {
                return;
            }

            viewTransform.SetParent(camera.transform, false);
            viewTransform.localPosition = Vector3.zero;
            viewTransform.localRotation = Quaternion.identity;
            viewTransform.localScale = Vector3.one;
        }

        private void BindWeaponCombatHud()
        {
            if (weaponView == null)
            {
                weaponView = FindFirstObjectByType<PrototypeFirstPersonWeaponView>(FindObjectsInactive.Include);
            }

            if (weaponCombatHud == null && weaponView != null)
            {
                weaponCombatHud = weaponView.GetComponent<PrototypeWeaponCombatHud>();
                if (weaponCombatHud == null)
                {
                    weaponCombatHud = weaponView.gameObject.AddComponent<PrototypeWeaponCombatHud>();
                }
            }

            if (weaponCombatHud != null)
            {
                weaponCombatHud.Bind(playerVitals, playerResourcePool, playerWeaponCaster);
            }
        }

        private void TrackMonsterHud(LevelCombatant combatant, LevelRoomDefinition room)
        {
            MonsterCombatHud combatHud = EnsureMonsterCombatHud();
            if (combatHud == null || combatant == null || room == null)
            {
                return;
            }

            combatHud.SetTargetCamera(ResolveCombatCamera());
            combatHud.Track(combatant, room.roomType == LevelRoomType.Boss, room.displayName);
        }

        private MonsterCombatHud EnsureMonsterCombatHud()
        {
            if (monsterCombatHud != null)
            {
                return monsterCombatHud;
            }

            monsterCombatHud = GetComponentInChildren<MonsterCombatHud>(true);
            if (monsterCombatHud != null)
            {
                return monsterCombatHud;
            }

            GameObject hudObject = new GameObject("MonsterCombatHud");
            hudObject.transform.SetParent(transform, false);
            monsterCombatHud = hudObject.AddComponent<MonsterCombatHud>();
            return monsterCombatHud;
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
            if (monsterCombatHud != null)
            {
                monsterCombatHud.Clear();
            }

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
                    if (monsterCombatHud != null)
                    {
                        monsterCombatHud.Untrack(activeEnemies[i]);
                    }
                }
            }
        }

        private void Schedule(PendingFlowAction action, LevelRoomDefinition room, float delay)
        {
            pendingAction = action;
            pendingRoom = room;
            pendingActionAt = Time.unscaledTime + Mathf.Max(0f, delay);
        }

#if false
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

#endif
        private enum PendingFlowAction
        {
            None,
            StartRoomContent,
            StartCombat,
            StartNextWave,
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
