using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Scene-owned composition root for one Room + Scenario playtest. The
    /// referenced assets remain independent and are never modified at runtime.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class FpgRoomCombatLabBinding : MonoBehaviour
    {
        [D0PlannerSection("房间试玩组合")]
        [D0PlannerField("房间定义", "CombatLab 默认加载的房间；房间编辑器试玩只使用跨域重载的内存覆盖，不修改场景或资产。")]
        [SerializeField]
        private FpgRoomDefinition roomDefinition;

        [D0PlannerField("D0 遭遇配置", "与房间独立维护；组合校验只检查玩家入口和敌人 SpawnSlot 的语义 ID。")]
        [SerializeField]
        private D0CombatScenarioDefinition scenarioDefinition;

        [D0PlannerField("房间实例", "显式实例化环境、可破坏物并提供房间内玩法标记姿态的场景桥。")]
        [SerializeField]
        private FpgRoomInstance roomInstance;

        [D0PlannerField("旧环境回退根节点", "房间成功初始化后临时隐藏；房间不可用时继续显示旧 D0 Stage，便于兼容和回滚。")]
        [SerializeField]
        private GameObject legacyEnvironmentRoot;

        private readonly List<GameObject> ownedSpawnObjects = new List<GameObject>();
        private D0SpawnPoint[] spawnPoints = Array.Empty<D0SpawnPoint>();
        private bool legacyEnvironmentWasActive;
        private bool legacyEnvironmentStateCaptured;

        public FpgRoomDefinition RoomDefinition =>
            FpgRoomPlaytestOverrides.RoomDefinition != null
                ? FpgRoomPlaytestOverrides.RoomDefinition
                : roomDefinition;
        public FpgRoomDefinition ConfiguredRoomDefinition => roomDefinition;
        public D0CombatScenarioDefinition ConfiguredScenarioDefinition => scenarioDefinition;
        public D0CombatScenarioDefinition ScenarioDefinition =>
            FpgRoomPlaytestOverrides.ScenarioDefinition != null
                ? FpgRoomPlaytestOverrides.ScenarioDefinition
                : scenarioDefinition;
        public FpgRoomInstance RoomInstance => roomInstance;
        public GameObject LegacyEnvironmentRoot => legacyEnvironmentRoot;
        public IReadOnlyList<D0SpawnPoint> SpawnPoints => spawnPoints;
        public bool IsInitialized => roomInstance != null
            && roomInstance.IsInitialized
            && spawnPoints.Length > 0;

        private void OnDestroy()
        {
            ClearRuntimeRoom();
            FpgRoomPlaytestOverrides.ClearIf(roomDefinition, scenarioDefinition);
        }

        public bool TryValidate(out string error)
        {
            FpgRoomDefinition activeRoom = RoomDefinition;
            D0CombatScenarioDefinition activeScenario = ScenarioDefinition;
            if (activeRoom == null || activeScenario == null || roomInstance == null)
            {
                error = "CombatLab room binding requires room, scenario and room instance references.";
                return false;
            }

            if (!FpgRoomEncounterValidator.TryValidate(
                    activeRoom,
                    activeScenario,
                    out FpgRoomEncounterValidationResult validation))
            {
                error = validation.FirstError == null
                    ? "CombatLab room and encounter composition is invalid."
                    : validation.FirstError.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(
            FpgRoomDefinition configuredRoom,
            D0CombatScenarioDefinition configuredScenario,
            FpgRoomInstance configuredInstance,
            GameObject configuredLegacyEnvironment)
        {
            roomDefinition = configuredRoom;
            scenarioDefinition = configuredScenario;
            roomInstance = configuredInstance;
            legacyEnvironmentRoot = configuredLegacyEnvironment;
        }


        public bool TryInitializeRoom(
            Transform actorsRoot,
            CombatAimReticle aimReticle,
            out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            if (actorsRoot == null)
            {
                error = "CombatLab room binding requires the scene ActorsRoot.";
                return false;
            }

            FpgRoomDefinition activeRoom = RoomDefinition;
            if (IsInitialized && roomInstance.RoomDefinition == activeRoom)
            {
                return TryValidateRuntimeSpawnPoints(actorsRoot, out error);
            }

            ClearRuntimeRoom();
            if (legacyEnvironmentRoot != null)
            {
                legacyEnvironmentWasActive = legacyEnvironmentRoot.activeSelf;
                legacyEnvironmentStateCaptured = true;
            }
            if (!roomInstance.TryInitialize(activeRoom, out error))
            {
                RestoreLegacyEnvironment();
                return false;
            }

            try
            {
                GameObject spawnRoot = new GameObject("RoomSpawnPoints (Runtime)");
                if (!Application.isPlaying)
                {
                    spawnRoot.hideFlags |= HideFlags.DontSaveInEditor;
                }
                spawnRoot.transform.SetParent(actorsRoot, false);
                ownedSpawnObjects.Add(spawnRoot);

                int count = activeRoom.PlayerEntryPoints.Count
                    + activeRoom.EnemySpawnPoints.Count;
                spawnPoints = new D0SpawnPoint[count];
                int nextIndex = 0;
                for (int index = 0; index < activeRoom.PlayerEntryPoints.Count; index++)
                {
                    FpgRoomPlayerEntryPoint marker =
                        activeRoom.PlayerEntryPoints[index];
                    spawnPoints[nextIndex++] = CreateSpawnPoint(
                        spawnRoot.transform,
                        marker);
                }

                for (int index = 0; index < activeRoom.EnemySpawnPoints.Count; index++)
                {
                    FpgRoomEnemySpawnPoint marker =
                        activeRoom.EnemySpawnPoints[index];
                    spawnPoints[nextIndex++] = CreateSpawnPoint(
                        spawnRoot.transform,
                        marker);
                }

                BindEnvironmentPresentation(aimReticle);
                if (!TryValidateRuntimeSpawnPoints(actorsRoot, out error))
                {
                    ClearRuntimeRoom();
                    return false;
                }

                if (legacyEnvironmentRoot != null)
                {
                    legacyEnvironmentRoot.SetActive(false);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ClearRuntimeRoom();
                error = $"Unable to initialize room '{activeRoom.RoomId}': {exception.Message}";
                return false;
            }
        }

        public bool TryGetSpawnPoint(string spawnPointId, out D0SpawnPoint spawnPoint)
        {
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                D0SpawnPoint candidate = spawnPoints[index];
                if (candidate != null
                    && string.Equals(
                        candidate.SpawnPointId,
                        spawnPointId,
                        StringComparison.Ordinal))
                {
                    spawnPoint = candidate;
                    return true;
                }
            }

            spawnPoint = null;
            return false;
        }

        public bool TryValidateRuntimeSpawnPoints(
            Transform actorsRoot,
            out string error)
        {
            if (!IsInitialized)
            {
                error = "Room runtime spawn points have not been initialized.";
                return false;
            }

            FpgRoomDefinition activeRoom = RoomDefinition;
            if (activeRoom == null)
            {
                error = "Room runtime spawn points require an active room definition.";
                return false;
            }

            int expectedCount = activeRoom.PlayerEntryPoints.Count
                + activeRoom.EnemySpawnPoints.Count;
            if (spawnPoints.Length != expectedCount)
            {
                error = "Room runtime spawn-point count does not match the room definition.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                D0SpawnPoint point = spawnPoints[index];
                if (point == null)
                {
                    error = $"Room runtime spawn point {index} is missing.";
                    return false;
                }

                if (!point.TryValidate(out error))
                {
                    return false;
                }

                if (actorsRoot != null
                    && (point.transform == actorsRoot
                        || !point.transform.IsChildOf(actorsRoot)))
                {
                    error = $"Room spawn point '{point.SpawnPointId}' must be below ActorsRoot.";
                    return false;
                }

                if (!ids.Add(point.SpawnPointId))
                {
                    error = $"Room spawn point ID '{point.SpawnPointId}' must be unique.";
                    return false;
                }

                if (!roomInstance.TryGetMarkerPose(
                        point.SpawnPointId,
                        out Pose expectedPose)
                    || Vector3.Distance(
                        point.transform.position,
                        expectedPose.position) > 0.001f
                    || Quaternion.Angle(
                        point.transform.rotation,
                        expectedPose.rotation) > 0.01f)
                {
                    error = $"Room spawn point '{point.SpawnPointId}' does not match its authored room pose.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void ClearRuntimeRoom()
        {
            spawnPoints = Array.Empty<D0SpawnPoint>();
            for (int index = ownedSpawnObjects.Count - 1; index >= 0; index--)
            {
                GameObject instance = ownedSpawnObjects[index];
                if (instance == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }
            }

            ownedSpawnObjects.Clear();
            if (roomInstance != null)
            {
                roomInstance.Clear();
            }

            RestoreLegacyEnvironment();
        }

        private D0SpawnPoint CreateSpawnPoint(
            Transform parent,
            FpgRoomMarker marker)
        {
            GameObject pointObject = new GameObject(marker.MarkerId);
            if (!Application.isPlaying)
            {
                pointObject.hideFlags |= HideFlags.DontSaveInEditor;
            }
            pointObject.transform.SetParent(parent, false);
            Pose worldPose = new Pose(
                roomInstance.transform.TransformPoint(marker.LocalPosition),
                roomInstance.transform.rotation * marker.LocalRotation);
            pointObject.transform.SetPositionAndRotation(
                worldPose.position,
                worldPose.rotation);
            pointObject.transform.localScale = Vector3.one;
            D0SpawnPoint point = pointObject.AddComponent<D0SpawnPoint>();
            point.Configure(marker.MarkerId);
            return point;
        }

        private void BindEnvironmentPresentation(CombatAimReticle aimReticle)
        {
            GameObject environment = roomInstance.EnvironmentInstance;
            if (environment == null)
            {
                return;
            }

            D0ForestParallax parallax =
                environment.GetComponentInChildren<D0ForestParallax>(true);
            if (parallax == null)
            {
                return;
            }

            D0ForestParallaxLayer[] layers =
                parallax.GetComponentsInChildren<D0ForestParallaxLayer>(true);
            parallax.Configure(aimReticle, layers);
        }

        private void RestoreLegacyEnvironment()
        {
            if (!legacyEnvironmentStateCaptured)
            {
                return;
            }

            if (legacyEnvironmentRoot != null)
            {
                legacyEnvironmentRoot.SetActive(legacyEnvironmentWasActive);
            }

            legacyEnvironmentStateCaptured = false;
            legacyEnvironmentWasActive = false;
        }
    }
}