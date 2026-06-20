using Unity.Cinemachine;
using UnityEngine;

namespace NewFPG.CameraRig
{
    public enum GameplayCameraMode
    {
        Explore,
        Battle
    }

    public sealed class CinemachineCameraModeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform exploreReferenceCamera;
        [SerializeField] private Transform battleReferenceCamera;
        [SerializeField] private Transform exploreFollowTarget;
        [SerializeField] private CinemachineCamera exploreCamera;
        [SerializeField] private CinemachineCamera battleCamera;
        [SerializeField] private Transform[] battleOnlyObjects;

        [Header("Priorities")]
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 0;
        [SerializeField] private GameplayCameraMode startingMode = GameplayCameraMode.Explore;

        private float exploreLockedX;
        private float exploreLockedY;
        private float exploreZOffset;
        private GameplayCameraMode currentMode;

        public GameplayCameraMode CurrentMode => currentMode;
        public CinemachineCamera ExploreCamera => exploreCamera;
        public CinemachineCamera BattleCamera => battleCamera;

        private void Awake()
        {
            CacheExploreAnchor();
            SnapExploreTarget();
            ApplyMode(startingMode);
        }

        private void LateUpdate()
        {
            UpdateExploreTarget();
        }

        public void SwitchToExplore()
        {
            ApplyMode(GameplayCameraMode.Explore);
        }

        public void SwitchToBattle()
        {
            ApplyMode(GameplayCameraMode.Battle);
        }

        public void SetMode(GameplayCameraMode mode)
        {
            ApplyMode(mode);
        }

        public void SetPlayer(Transform newPlayer)
        {
            player = newPlayer;
            CacheExploreAnchor();
            SnapExploreTarget();
        }

        public void SnapExploreTarget()
        {
            UpdateExploreTarget(true);
        }

        private void CacheExploreAnchor()
        {
            Transform reference = exploreReferenceCamera != null ? exploreReferenceCamera : exploreFollowTarget;
            if (reference == null)
            {
                return;
            }

            Vector3 referencePosition = reference.position;
            exploreLockedX = referencePosition.x;
            exploreLockedY = referencePosition.y;
            exploreZOffset = player != null ? referencePosition.z - player.position.z : 0f;
        }

        private void UpdateExploreTarget(bool force = false)
        {
            if (exploreFollowTarget == null || player == null)
            {
                return;
            }

            Vector3 desiredPosition = new Vector3(exploreLockedX, exploreLockedY, player.position.z + exploreZOffset);
            if (!force && exploreFollowTarget.position == desiredPosition)
            {
                return;
            }

            exploreFollowTarget.position = desiredPosition;
        }

        private void ApplyMode(GameplayCameraMode mode)
        {
            currentMode = mode;

            if (exploreCamera != null)
            {
                exploreCamera.Priority = mode == GameplayCameraMode.Explore ? activePriority : inactivePriority;
            }

            if (battleCamera != null)
            {
                battleCamera.Priority = mode == GameplayCameraMode.Battle ? activePriority : inactivePriority;
            }

            SetBattleOnlyObjectsActive(mode == GameplayCameraMode.Battle);
        }

        private void SetBattleOnlyObjectsActive(bool active)
        {
            if (battleOnlyObjects != null && battleOnlyObjects.Length > 0)
            {
                for (int i = 0; i < battleOnlyObjects.Length; i++)
                {
                    if (battleOnlyObjects[i] != null)
                    {
                        battleOnlyObjects[i].gameObject.SetActive(active);
                    }
                }

                return;
            }

            if (battleReferenceCamera == null)
            {
                return;
            }

            for (int i = 0; i < battleReferenceCamera.childCount; i++)
            {
                battleReferenceCamera.GetChild(i).gameObject.SetActive(active);
            }
        }
    }
}
