using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Reads committed player-shot presentation events and adds a short,
    /// profile-authored camera kick. It has no combat authority and never reads
    /// raw input or performs a spatial query.
    /// </summary>
    [DefaultExecutionOrder(920)]
    [DisallowMultipleComponent]
    public sealed class D0ShotCameraFeedbackController : MonoBehaviour
    {
        [SerializeField]
        private BattleSessionHost sessionHost;

        private D0ThreeCProfile threeCProfile;

        [SerializeField]
        private Camera targetCamera;

        private readonly PlayerShotPresentationCursor shotCursor = new PlayerShotPresentationCursor();
        private PlayerShotPresentationEvent[] eventBuffer;
        private IPlayerShotPresentationFeed boundFeed;
        private Vector3 baselineLocalPosition;
        private float currentKick;
        private bool hasBaseline;

        public BattleSessionHost SessionHost => sessionHost;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public Camera TargetCamera => targetCamera;
        public float CurrentKick => currentKick;

        /// <summary>
        /// The world-space presentation offset currently added to the camera.
        /// BattleSessionHost removes this offset from its aim-ray origin so a
        /// visual-only kick can never change deterministic shot queries.
        /// </summary>
        public Vector3 CurrentWorldPresentationOffset
        {
            get
            {
                if (targetCamera == null || !hasBaseline)
                {
                    return Vector3.zero;
                }

                Transform parent = targetCamera.transform.parent;
                Vector3 baselineWorldPosition = parent == null
                    ? baselineLocalPosition
                    : parent.TransformPoint(baselineLocalPosition);
                return targetCamera.transform.position - baselineWorldPosition;
            }
        }

        private void Awake()
        {
            CaptureBaseline();
        }

        private void OnDisable()
        {
            RestoreBaseline();
            currentKick = 0f;
            boundFeed = null;
            shotCursor.Reset();
        }

        private void LateUpdate()
        {
            if (!TryValidate(out _))
            {
                return;
            }

            IPlayerShotPresentationFeed nextFeed = sessionHost == null
                ? null
                : sessionHost.PlayerShotPresentationFeed;
            RefreshFeed(nextFeed);

            BattleSession session = sessionHost == null ? null : sessionHost.Session;
            if (session != null && session.State == BattleSessionState.Running)
            {
                ConsumeCommittedShots();
            }

            AdvanceKick();
        }

        public void Configure(
            BattleSessionHost nextSessionHost,
            D0ThreeCProfile nextProfile,
            Camera nextTargetCamera)
        {
            sessionHost = nextSessionHost;
            threeCProfile = nextProfile;
            targetCamera = nextTargetCamera;
            boundFeed = null;
            shotCursor.Reset();
            currentKick = 0f;
            hasBaseline = false;
            CaptureBaseline();
        }

        /// <summary>
        /// Replaces the profile reference and camera baseline used by recoil.
        /// The current kick is cleared so a new authored local position is not
        /// combined with an offset from the previous profile.
        /// </summary>
        public bool TrySetThreeCProfile(D0ThreeCProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "D0 shot camera feedback requires a D0 3C profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                return false;
            }

            if (sessionHost == null || targetCamera == null)
            {
                error = "D0 shot camera feedback requires a BattleSessionHost and target Camera.";
                return false;
            }

            if (targetCamera.gameObject != gameObject)
            {
                error = "D0 shot camera feedback must be attached to its target Camera.";
                return false;
            }

            threeCProfile = profile;
            baselineLocalPosition = profile.CameraLocalPosition;
            hasBaseline = true;
            currentKick = 0f;
            targetCamera.transform.localPosition = baselineLocalPosition;
            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (sessionHost == null || targetCamera == null)
            {
                error = "D0 shot camera feedback requires a BattleSessionHost and target Camera.";
                return false;
            }

            if (threeCProfile == null)
            {
                error = "D0 shot camera feedback requires a D0 3C profile.";
                return false;
            }

            if (targetCamera.gameObject != gameObject)
            {
                error = "D0 shot camera feedback must be attached to its target Camera.";
                return false;
            }

            if (!threeCProfile.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void RefreshFeed(IPlayerShotPresentationFeed nextFeed)
        {
            if (ReferenceEquals(boundFeed, nextFeed))
            {
                return;
            }

            boundFeed = nextFeed;
            shotCursor.Reset();
            if (boundFeed == null)
            {
                return;
            }

            if (eventBuffer == null || eventBuffer.Length < boundFeed.EventCapacity)
            {
                eventBuffer = new PlayerShotPresentationEvent[boundFeed.EventCapacity];
            }

            shotCursor.SetBaseline(boundFeed);
        }

        private void ConsumeCommittedShots()
        {
            if (boundFeed == null || eventBuffer == null)
            {
                return;
            }

            int count = shotCursor.CopyUnread(boundFeed, eventBuffer, out bool hasGap);
            if (hasGap)
            {
                shotCursor.ResolveGap(boundFeed);
                return;
            }

            for (int index = 0; index < count; index++)
            {
                PlayerShotPresentationEvent shotEvent = eventBuffer[index];
                float kick = shotEvent.Snapshot.ReleaseKind == WeaponReleaseKind.Secondary
                    ? threeCProfile.SecondaryShotCameraKick
                    : threeCProfile.PrimaryShotCameraKick;
                currentKick = Mathf.Max(currentKick, kick);
                shotCursor.Commit(shotEvent);
            }
        }

        private void AdvanceKick()
        {
            CaptureBaseline();
            float recovery = threeCProfile == null
                ? 0f
                : threeCProfile.ShotCameraKickRecoverySeconds;
            if (recovery > 0f)
            {
                float maximumKick = Mathf.Max(
                    0.001f,
                    Mathf.Max(
                        threeCProfile.PrimaryShotCameraKick,
                        threeCProfile.SecondaryShotCameraKick));
                currentKick = Mathf.MoveTowards(
                    currentKick,
                    0f,
                    maximumKick * Time.unscaledDeltaTime / recovery);
            }

            if (targetCamera != null && hasBaseline)
            {
                targetCamera.transform.localPosition = baselineLocalPosition
                    + Vector3.back * currentKick;
            }
        }

        private void CaptureBaseline()
        {
            if (targetCamera == null || hasBaseline)
            {
                return;
            }

            baselineLocalPosition = targetCamera.transform.localPosition;
            hasBaseline = true;
        }

        private void RestoreBaseline()
        {
            if (targetCamera != null && hasBaseline)
            {
                targetCamera.transform.localPosition = baselineLocalPosition;
            }
        }
    }
}
