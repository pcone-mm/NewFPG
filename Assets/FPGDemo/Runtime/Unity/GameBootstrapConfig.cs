using System;
using UnityEngine;

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
        private string combatLabSceneName = "FormalRoom";

        [SerializeField]
        private bool loadCombatLabOnStart = true;

        [SerializeField]
        [Tooltip("When enabled, Boot waits for a shot to hit an authored room entrance before loading the room scene.")]
        private bool requireEntranceSelection = true;

        [SerializeField]
        [Tooltip("When enabled, Boot requires an authored playable-character choice before room selection.")]
        private bool requireCharacterSelection = true;

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

        public string RoomSceneName => combatLabSceneName;

        [Obsolete("Use RoomSceneName instead.")]
        public string CombatLabSceneName => RoomSceneName;

        public bool LoadRoomOnStart => loadCombatLabOnStart;

        [Obsolete("Use LoadRoomOnStart instead.")]
        public bool LoadCombatLabOnStart => LoadRoomOnStart;

        public bool RequireEntranceSelection => requireEntranceSelection;

        public bool RequireCharacterSelection => requireCharacterSelection;

        public FrameRateMode FrameRateMode => frameRateMode;

        public int LockedFramesPerSecond => lockedFramesPerSecond;

        public int VSyncCount => vSyncCount;

        public bool DevelopmentDiagnosticsEnabled => developmentDiagnosticsEnabled;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(combatLabSceneName))
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

            error = string.Empty;
            return true;
        }
    }
}
