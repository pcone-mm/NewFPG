using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FPG.Demo.Unity
{
    public enum FrameRateMode
    {
        Unlocked,
        Locked
    }

    [CreateAssetMenu(fileName = "GameBootstrapConfig", menuName = "FPG Demo/Game Bootstrap Config")]
    public sealed class GameBootstrapConfig : ScriptableObject
    {
        [Header("Room Flow")]
        [SerializeField]
        [FormerlySerializedAs("combatLabSceneName")]
        private string roomSceneName = "FormalRoom";

        [SerializeField]
        [FormerlySerializedAs("loadCombatLabOnStart")]
        private bool loadRoomOnStart = true;

        [SerializeField]
        [Tooltip("When enabled, Boot waits for a shot to hit an authored room entrance before loading the room scene.")]
        private bool requireEntranceSelection = true;

        [SerializeField]
        [Tooltip("When enabled, Boot requires an authored playable-character choice before room selection.")]
        private bool requireCharacterSelection = true;

        [SerializeField]
        [D0PlannerField(
            "出口房间刷新规则",
            "房间清空后为每个出口决定并冻结目的地。规则引用可进入的房间目录；当前房间也参与等概率抽取。")]
        private FpgExitRoomRefreshRule exitRoomRefreshRule;

        [Header("Runtime")]
        [SerializeField]
        private FrameRateMode frameRateMode = FrameRateMode.Locked;

        [SerializeField]
        [Min(1)]
        private int lockedFramesPerSecond = 60;

        [SerializeField]
        [Range(0, 4)]
        private int vSyncCount;

        [SerializeField]
        private bool developmentDiagnosticsEnabled = true;

        public string RoomSceneName => roomSceneName;

        public bool LoadRoomOnStart => loadRoomOnStart;

        public bool RequireEntranceSelection => requireEntranceSelection;

        public bool RequireCharacterSelection => requireCharacterSelection;

        public FpgExitRoomRefreshRule ExitRoomRefreshRule =>
            exitRoomRefreshRule;

        public FrameRateMode FrameRateMode => frameRateMode;

        public int LockedFramesPerSecond => lockedFramesPerSecond;

        public int VSyncCount => vSyncCount;

        public bool DevelopmentDiagnosticsEnabled => developmentDiagnosticsEnabled;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(roomSceneName))
            {
                error = "Room scene name is empty.";
                return false;
            }

            if (frameRateMode == FrameRateMode.Locked && lockedFramesPerSecond <= 0)
            {
                error = "Locked frames per second must be greater than zero.";
                return false;
            }

            if (vSyncCount < 0 || vSyncCount > 4)
            {
                error = "VSync count must be between 0 and 4.";
                return false;
            }

            if (exitRoomRefreshRule == null)
            {
                error = "Exit room refresh rule is required.";
                return false;
            }

            if (!exitRoomRefreshRule.TryValidate(out error))
            {
                error = "Exit room refresh rule is invalid: " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
