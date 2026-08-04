using System;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public interface IFpgPlayerFacingActionSource
    {
        bool CanUpdateFacingFromReticle { get; }
    }

    public enum FpgPlayerFacingDirection
    {
        Right = 0,
        Left
    }

    internal struct FpgPlayerFacingTransitionState
    {
        private FpgPlayerFacingDirection targetDirection;
        private FpgPlayerFacingDirection pendingDirection;
        private float delayElapsed;
        private bool waitingForDelay;

        public float Phase { get; private set; }
        public FpgPlayerFacingDirection TargetDirection => targetDirection;
        public bool IsWaitingForDelay => waitingForDelay;
        public float DelayElapsed => delayElapsed;
        public bool IsTransitioning => !Mathf.Approximately(
            Phase,
            ResolvePhase(targetDirection));

        public void Reset()
        {
            Phase = 0f;
            targetDirection = FpgPlayerFacingDirection.Right;
            pendingDirection = FpgPlayerFacingDirection.Right;
            delayElapsed = 0f;
            waitingForDelay = false;
        }

        public bool Force(FpgPlayerFacingDirection direction)
        {
            float previous = Phase;
            targetDirection = direction;
            pendingDirection = direction;
            delayElapsed = 0f;
            waitingForDelay = false;
            Phase = ResolvePhase(direction);
            return !Mathf.Approximately(previous, Phase);
        }

        public void Hold()
        {
            pendingDirection = targetDirection;
            delayElapsed = 0f;
            waitingForDelay = false;
        }

        public bool Advance(
            FpgPlayerFacingDirection desiredDirection,
            float deltaTime,
            float delaySeconds,
            float durationSeconds)
        {
            float previous = Phase;
            float remainingTime = Mathf.Max(0f, deltaTime);

            if (IsTransitioning)
            {
                if (desiredDirection != targetDirection)
                {
                    targetDirection = desiredDirection;
                    pendingDirection = desiredDirection;
                    delayElapsed = 0f;
                    waitingForDelay = false;
                }

                AdvanceTransition(remainingTime, durationSeconds);
                return !Mathf.Approximately(previous, Phase);
            }

            FpgPlayerFacingDirection settledDirection =
                Phase >= 0.5f
                    ? FpgPlayerFacingDirection.Left
                    : FpgPlayerFacingDirection.Right;
            targetDirection = settledDirection;
            if (desiredDirection == settledDirection)
            {
                pendingDirection = settledDirection;
                delayElapsed = 0f;
                waitingForDelay = false;
                return false;
            }

            if (!waitingForDelay || pendingDirection != desiredDirection)
            {
                pendingDirection = desiredDirection;
                delayElapsed = 0f;
                waitingForDelay = true;
            }

            float clampedDelay = Mathf.Max(0f, delaySeconds);
            float delayRemaining = Mathf.Max(
                0f,
                clampedDelay - delayElapsed);
            if (remainingTime < delayRemaining)
            {
                delayElapsed += remainingTime;
                return false;
            }

            remainingTime -= delayRemaining;
            delayElapsed = 0f;
            waitingForDelay = false;
            targetDirection = desiredDirection;
            AdvanceTransition(remainingTime, durationSeconds);
            return !Mathf.Approximately(previous, Phase);
        }

        private void AdvanceTransition(float deltaTime, float durationSeconds)
        {
            float targetPhase = ResolvePhase(targetDirection);
            if (durationSeconds <= 0f)
            {
                Phase = targetPhase;
                return;
            }

            Phase = Mathf.MoveTowards(
                Phase,
                targetPhase,
                Mathf.Max(0f, deltaTime) / durationSeconds);
        }

        private static float ResolvePhase(FpgPlayerFacingDirection direction)
        {
            return direction == FpgPlayerFacingDirection.Left ? 1f : 0f;
        }
    }

    /// <summary>
    /// Presentation-only player facing driven by the formal virtual reticle.
    /// It runs before aim sampling so the Spine-followed ShotOrigin is current
    /// when the deterministic input frame is built.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class FpgPlayerFacingController : MonoBehaviour
    {
        [SerializeField]
        private Transform facingRoot;

        private FpgPlayerFacingTransitionState transitionState;
        private FpgRoomEncounterDirector encounterDirector;
        private ICombatAimViewportSource aimViewportSource;
        private IFpgPlayerFacingActionSource actionSource;
        private FpgPlayerEntityView playerEntity;
        private D0ThreeCProfile threeCProfile;
        private Quaternion authoredLocalRotation = Quaternion.identity;
        private float flipDelaySeconds = 0.05f;
        private float flipDurationSeconds = 0.08f;
        private bool prepared;
        private bool presentationActive;

        public Transform FacingRoot => facingRoot;
        public FpgPlayerFacingDirection TargetDirection =>
            transitionState.TargetDirection;
        public float FacingPhase => transitionState.Phase;
        public bool IsWaitingForDelay => transitionState.IsWaitingForDelay;
        public bool IsTransitioning => transitionState.IsTransitioning;
        public float FlipDelaySeconds => flipDelaySeconds;
        public float FlipDurationSeconds => flipDurationSeconds;
        public bool IsPrepared => prepared;
        public bool IsPresentationActive => presentationActive;
        public int SocketRefreshFaultCount { get; private set; }

        public bool TryPrepare(
            FpgRoomEncounterDirector nextEncounterDirector,
            ICombatAimViewportSource nextAimViewportSource,
            IFpgPlayerFacingActionSource nextActionSource,
            FpgPlayerEntityView nextPlayerEntity,
            D0ThreeCProfile nextThreeCProfile,
            out string error)
        {
            if (prepared)
            {
                error =
                    "Player facing supports one preparation per entity lifetime.";
                return false;
            }

            if (!TryValidate(out error)
                || nextEncounterDirector == null
                || nextAimViewportSource == null
                || nextActionSource == null
                || nextPlayerEntity == null
                || nextThreeCProfile == null
                || !nextThreeCProfile.TryValidate(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Player facing requires director, viewport, entity and ThreeC bindings."
                    : error;
                return false;
            }

            encounterDirector = nextEncounterDirector;
            aimViewportSource = nextAimViewportSource;
            actionSource = nextActionSource;
            playerEntity = nextPlayerEntity;
            threeCProfile = nextThreeCProfile;
            authoredLocalRotation = facingRoot.localRotation;
            flipDelaySeconds = nextThreeCProfile.FacingFlipDelaySeconds;
            flipDurationSeconds =
                nextThreeCProfile.FacingFlipDurationSeconds;
            transitionState.Reset();
            SocketRefreshFaultCount = 0;
            prepared = true;
            presentationActive = false;
            ApplyFacingRotation(refreshSockets: true);
            error = string.Empty;
            return true;
        }

        public bool TryApplyShootingPreview(
            in FpgShootingTuningSnapshot tuning,
            out string error)
        {
            error = string.Empty;
            if (!prepared || !tuning.TryValidate(out error)
                || !ReferenceEquals(threeCProfile, tuning.ThreeCProfile))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Player facing preview does not match the prepared ThreeC profile."
                    : error;
                return false;
            }

            flipDelaySeconds = tuning.FacingFlipDelaySeconds;
            flipDurationSeconds = tuning.FacingFlipDurationSeconds;
            error = string.Empty;
            return true;
        }

        public void SetPresentationActive(bool value)
        {
            if (!prepared)
            {
                presentationActive = false;
                return;
            }

            presentationActive = value;
            if (!value)
            {
                ResetToAuthoredFacing();
            }
        }

        public void ResetToAuthoredFacing()
        {
            transitionState.Reset();
            ApplyFacingRotation(refreshSockets: true);
        }

        public bool TryForceDirection(
            FpgPlayerFacingDirection direction,
            out string error)
        {
            if (!prepared || !presentationActive
                || !Enum.IsDefined(typeof(FpgPlayerFacingDirection), direction))
            {
                error = "Player facing cannot force an invalid or inactive direction.";
                return false;
            }

            transitionState.Force(direction);
            ApplyFacingRotation(refreshSockets: true);
            error = string.Empty;
            return true;
        }

        internal FpgPlayerFacingTransitionState CaptureTransitionState()
        {
            return transitionState;
        }

        internal void RestoreTransitionState(
            in FpgPlayerFacingTransitionState state)
        {
            transitionState = state;
            ApplyFacingRotation(refreshSockets: true);
        }

        public void Clear()
        {
            presentationActive = false;
            if (prepared)
            {
                ResetToAuthoredFacing();
            }

            encounterDirector = null;
            aimViewportSource = null;
            actionSource = null;
            playerEntity = null;
            threeCProfile = null;
            SocketRefreshFaultCount = 0;
            prepared = false;
        }

        public bool TryValidate(out string error)
        {
            if (facingRoot == null)
            {
                error = "Player facing requires an authored FacingRoot.";
                return false;
            }

            if (!IsFinite(facingRoot.localPosition)
                || !IsFinite(facingRoot.localRotation)
                || !IsFinite(facingRoot.localScale))
            {
                error = "Player FacingRoot authored pose must be finite.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void Update()
        {
            if (!presentationActive || !prepared || !Application.isFocused
                || encounterDirector == null || encounterDirector.IsPaused
                || !IsFacingPhase(encounterDirector.Phase)
                || actionSource == null)
            {
                return;
            }

            if (!actionSource.CanUpdateFacingFromReticle)
            {
                transitionState.Hold();
                return;
            }

            if (aimViewportSource == null
                || !aimViewportSource.TryGetViewport(out Vector2 viewport)
                || !IsFinite(viewport))
            {
                return;
            }

            FpgPlayerFacingDirection desiredDirection =
                ResolveDirection(viewport.x);
            bool changed = transitionState.Advance(
                desiredDirection,
                Time.unscaledDeltaTime,
                flipDelaySeconds,
                flipDurationSeconds);
            if (changed)
            {
                ApplyFacingRotation(refreshSockets: true);
            }
        }

        private void ApplyFacingRotation(bool refreshSockets)
        {
            if (facingRoot == null)
            {
                return;
            }

            float easedPhase = EvaluateEasedPhase(transitionState.Phase);
            facingRoot.localRotation = authoredLocalRotation
                * Quaternion.AngleAxis(180f * easedPhase, Vector3.up);
            if (refreshSockets && playerEntity != null
                && !playerEntity.TryRefreshSpineSocketFollowers(out _))
            {
                SocketRefreshFaultCount++;
            }
        }

        private void OnDisable()
        {
            presentationActive = false;
            if (prepared)
            {
                ResetToAuthoredFacing();
            }

        }

        internal static FpgPlayerFacingDirection ResolveDirection(
            float viewportX)
        {
            return viewportX < 0.5f
                ? FpgPlayerFacingDirection.Left
                : FpgPlayerFacingDirection.Right;
        }

        internal static float EvaluateEasedPhase(float phase)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase));
        }

        private static bool IsFacingPhase(FpgEncounterPhase phase)
        {
            return phase == FpgEncounterPhase.Warning
                || phase == FpgEncounterPhase.Spawning
                || phase == FpgEncounterPhase.Combat
                || phase == FpgEncounterPhase.WaveDelay;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y)
                && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
