using System;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPG.Demo.Unity
{
    public readonly struct UnityInputSnapshot
    {
        public UnityInputSnapshot(
            bool aimHeld,
            bool primaryHeld,
            bool secondaryPressed,
            bool secondaryReleased,
            bool reloadPressed,
            bool pausePressed,
            bool restartPressed,
            bool secondaryHeld = false,
            FpgCoverMoveDirection coverMoveDirection = FpgCoverMoveDirection.None)
        {
            AimHeld = aimHeld;
            PrimaryHeld = primaryHeld;
            SecondaryPressed = secondaryPressed;
            SecondaryReleased = secondaryReleased;
            ReloadPressed = reloadPressed;
            PausePressed = pausePressed;
            RestartPressed = restartPressed;
            SecondaryHeld = secondaryHeld;
            CoverMoveDirection = coverMoveDirection;
        }

        public bool AimHeld { get; }
        public bool PrimaryHeld { get; }
        public bool SecondaryPressed { get; }
        public bool SecondaryReleased { get; }
        public bool ReloadPressed { get; }
        public bool PausePressed { get; }
        public bool RestartPressed { get; }
        public bool SecondaryHeld { get; }
        public FpgCoverMoveDirection CoverMoveDirection { get; }
    }

    public sealed class UnityBattleInputSource : IPlayerInputSource, IBattleTickInputSource
    {
        // A single rendered frame can enqueue more than one gameplay edge while a
        // simulation tick is still pending. Keep a bounded backlog rather than
        // overwriting those edges on the next Capture call. The capacity covers
        // thirty-two full BattleTickInput payloads without allocating in either
        // the capture or tick paths.
        private const int GameplayEdgeQueueCapacity = BattleTickInput.MaxEdgeCommandCount * 32;

        private readonly InputEdgeCommand[] edgeBuffer =
            new InputEdgeCommand[BattleTickInput.MaxEdgeCommandCount];
        private readonly InputEdgeCommand[] gameplayEdgeQueue =
            new InputEdgeCommand[GameplayEdgeQueueCapacity];

        private bool aimHeld;
        private bool primaryHeld;
        private bool secondaryHeld;
        private bool pausePressed;
        private bool restartPressed;
        private bool cancelSecondaryOnNextFrame;
        private bool hasCaptured;
        private int gameplayEdgeHead;
        private int gameplayEdgeCount;
        private int gameplayEdgeQueueCapacity = GameplayEdgeQueueCapacity;
        private long nextInputSequence = 1L;
        private SpatialVectorKey aimOrigin;
        private SpatialVectorKey aimForward;
        private SpatialVectorKey aimRight;
        private SpatialVectorKey aimUp;
        private long aimPoseVersion;
        private bool hasAimPose;
        private FpgCoverMoveDirection pendingCoverMoveDirection;

        public int ConfiguredInputBufferTicks => gameplayEdgeQueueCapacity
            / BattleTickInput.MaxEdgeCommandCount;
        public bool PrimaryHeld => primaryHeld;
        public bool SecondaryHeld => secondaryHeld;

        public bool HasQueuedGameplayEdge(InputEdgeType type)
        {
            if (!Enum.IsDefined(typeof(InputEdgeType), type))
            {
                return false;
            }

            for (int offset = 0; offset < gameplayEdgeCount; offset++)
            {
                int index = gameplayEdgeHead + offset;
                if (index >= gameplayEdgeQueueCapacity)
                {
                    index -= gameplayEdgeQueueCapacity;
                }

                if (gameplayEdgeQueue[index].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryEnqueueSyntheticEdge(InputEdgeType type)
        {
            if (!Enum.IsDefined(typeof(InputEdgeType), type))
            {
                return false;
            }

            for (int offset = 0; offset < gameplayEdgeCount; offset++)
            {
                int index = gameplayEdgeHead + offset;
                if (index >= gameplayEdgeQueueCapacity)
                {
                    index -= gameplayEdgeQueueCapacity;
                }

                if (gameplayEdgeQueue[index].Type == type)
                {
                    return true;
                }
            }

            if (gameplayEdgeCount == gameplayEdgeQueueCapacity)
            {
                return false;
            }

            int tail = gameplayEdgeHead + gameplayEdgeCount;
            if (tail >= gameplayEdgeQueueCapacity)
            {
                tail -= gameplayEdgeQueueCapacity;
            }

            gameplayEdgeQueue[tail] = NextEdge(type);
            gameplayEdgeCount++;
            return true;
        }

        /// <summary>
        /// Limits the preallocated edge backlog to a planner-authored number of
        /// simulation ticks. Storage remains fixed at the worst-case D0 budget,
        /// so changing this never allocates on the gameplay path.
        /// </summary>
        public void ConfigureInputBufferTicks(int ticks)
        {
            if (ticks < 1 || ticks > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks));
            }

            gameplayEdgeQueueCapacity = ticks * BattleTickInput.MaxEdgeCommandCount;
            gameplayEdgeHead = 0;
            gameplayEdgeCount = 0;
        }

        public void CaptureFromDevices()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            // RMB is the sole secondary contract. Aim remains a presentation
            // alias for the same control, so the fallback path must match the
            // project-wide InputAction map exactly.
            bool secondaryPressed =
                mouse != null && mouse.rightButton.wasPressedThisFrame;
            bool secondaryReleased =
                mouse != null && mouse.rightButton.wasReleasedThisFrame;
            bool moveLeft = keyboard != null
                && (keyboard.aKey.wasPressedThisFrame
                    || keyboard.leftArrowKey.wasPressedThisFrame);
            bool moveRight = keyboard != null
                && (keyboard.dKey.wasPressedThisFrame
                    || keyboard.rightArrowKey.wasPressedThisFrame);
            FpgCoverMoveDirection coverMoveDirection = moveLeft == moveRight
                ? FpgCoverMoveDirection.None
                : moveLeft
                    ? FpgCoverMoveDirection.Left
                    : FpgCoverMoveDirection.Right;
            Capture(new UnityInputSnapshot(
                mouse != null && mouse.rightButton.isPressed,
                mouse != null && mouse.leftButton.isPressed,
                secondaryPressed,
                secondaryReleased,
                keyboard != null && keyboard.rKey.wasPressedThisFrame,
                keyboard != null && keyboard.escapeKey.wasPressedThisFrame,
                keyboard != null && keyboard.f5Key.wasPressedThisFrame,
                mouse != null && mouse.rightButton.isPressed,
                coverMoveDirection));
        }

        public void Capture(UnityInputSnapshot snapshot)
        {
            aimHeld = snapshot.AimHeld;
            primaryHeld = snapshot.PrimaryHeld;
            secondaryHeld = snapshot.SecondaryHeld;

            EnqueueGameplayEdge(
                snapshot.SecondaryPressed,
                InputEdgeType.SecondaryPressed);
            EnqueueGameplayEdge(
                snapshot.SecondaryReleased,
                InputEdgeType.SecondaryReleased);
            EnqueueGameplayEdge(
                snapshot.ReloadPressed,
                InputEdgeType.ReloadPressed);
            pausePressed |= snapshot.PausePressed;
            restartPressed |= snapshot.RestartPressed;
            if (pendingCoverMoveDirection == FpgCoverMoveDirection.None
                && snapshot.CoverMoveDirection != FpgCoverMoveDirection.None)
            {
                pendingCoverMoveDirection = snapshot.CoverMoveDirection;
            }
            hasCaptured = true;
        }

        public PlayerInputFrame GetFrame(TickIndex tick)
        {
            if (!hasCaptured)
            {
                bool cancelOnUncapturedFrame = cancelSecondaryOnNextFrame;
                cancelSecondaryOnNextFrame = false;
                return PlayerInputFrame.Empty(
                    tick,
                    cancelSecondary: cancelOnUncapturedFrame);
            }

            int edgeCount = Math.Min(gameplayEdgeCount, BattleTickInput.MaxEdgeCommandCount);
            for (int index = 0; index < edgeCount; index++)
            {
                edgeBuffer[index] = gameplayEdgeQueue[gameplayEdgeHead];
                gameplayEdgeHead = NextGameplayEdgeIndex(gameplayEdgeHead);
            }
            gameplayEdgeCount -= edgeCount;
            bool cancelSecondary = cancelSecondaryOnNextFrame;
            cancelSecondaryOnNextFrame = false;

            return new PlayerInputFrame(
                tick,
                aimHeld,
                primaryHeld,
                edgeCount == 0 ? null : edgeBuffer,
                edgeCount,
                cancelSecondary,
                secondaryHeld);
        }

        public BattleTickInput GetTickInput(TickIndex tick)
        {
            if (!hasAimPose)
            {
                return default(BattleTickInput);
            }

            AimPoseSnapshot pose = new AimPoseSnapshot(
                tick,
                aimOrigin,
                aimForward,
                aimRight,
                aimUp,
                aimPoseVersion);
            FpgCoverMoveDirection coverMoveDirection =
                pendingCoverMoveDirection;
            pendingCoverMoveDirection = FpgCoverMoveDirection.None;
            return new BattleTickInput(
                GetFrame(tick),
                pose,
                coverMoveDirection);
        }

        public void SetAimPose(AimPoseSnapshot pose)
        {
            if (!pose.IsValid)
            {
                throw new System.ArgumentException("Aim pose must be valid.", nameof(pose));
            }

            aimOrigin = pose.Origin;
            aimForward = pose.Forward;
            aimRight = pose.Right;
            aimUp = pose.Up;
            aimPoseVersion = pose.PoseVersion;
            hasAimPose = true;
        }

        public void CaptureAimPose(Transform aimAnchor)
        {
            if (aimAnchor == null)
            {
                throw new ArgumentNullException(nameof(aimAnchor));
            }

            CaptureAimPose(aimAnchor.position, aimAnchor.forward, aimAnchor.up);
        }

        /// <summary>
        /// Captures an explicit combat ray while preserving a separately authored
        /// origin. This lets a third-person view converge on the screen-center
        /// target without changing the battle ray's chest-height origin.
        /// </summary>
        public void CaptureAimPose(Vector3 origin, Vector3 forward, Vector3 referenceUp)
        {
            if (!IsFinite(origin) || !IsFinite(forward) || !IsFinite(referenceUp))
            {
                throw new ArgumentOutOfRangeException(nameof(forward));
            }

            if (forward.sqrMagnitude <= 0.000001f)
            {
                throw new ArgumentException("Aim forward must be non-zero.", nameof(forward));
            }

            Vector3 normalizedForward = forward.normalized;
            Vector3 normalizedUp = referenceUp.sqrMagnitude <= 0.000001f
                ? Vector3.up
                : referenceUp.normalized;
            Vector3 rightVector = Vector3.Cross(normalizedUp, normalizedForward);
            if (rightVector.sqrMagnitude <= 0.000001f)
            {
                Vector3 fallbackUp = Mathf.Abs(normalizedForward.y) < 0.99f
                    ? Vector3.up
                    : Vector3.forward;
                rightVector = Vector3.Cross(fallbackUp, normalizedForward);
            }

            rightVector.Normalize();
            Vector3 upVector = Vector3.Cross(normalizedForward, rightVector).normalized;

            SpatialVectorKey quantizedOrigin = Quantize(
                origin,
                SpatialContract.PositionUnitsPerMeter);
            SpatialVectorKey quantizedForward = Quantize(
                normalizedForward,
                SpatialContract.DirectionUnits);
            SpatialVectorKey quantizedRight = Quantize(
                rightVector,
                SpatialContract.DirectionUnits);
            SpatialVectorKey quantizedUp = Quantize(
                upVector,
                SpatialContract.DirectionUnits);
            bool changed = !hasAimPose
                || quantizedOrigin != aimOrigin
                || quantizedForward != aimForward
                || quantizedRight != aimRight
                || quantizedUp != aimUp;
            aimOrigin = quantizedOrigin;
            aimForward = quantizedForward;
            aimRight = quantizedRight;
            aimUp = quantizedUp;
            if (changed)
            {
                aimPoseVersion = aimPoseVersion == long.MaxValue ? 1L : aimPoseVersion + 1L;
            }
            hasAimPose = true;
        }

        public bool ConsumePausePressed()
        {
            bool value = pausePressed;
            pausePressed = false;
            return value;
        }

        public bool ConsumeRestartPressed()
        {
            bool value = restartPressed;
            restartPressed = false;
            return value;
        }

        // Session lifecycle code calls this when focus is lost or when it enters
        // a pause/restart transition. Control latches intentionally remain owned
        // by their independent consumers so a pending pause/restart is not lost.
        public void ClearGameplayInput()
        {
            aimHeld = false;
            primaryHeld = false;
            secondaryHeld = false;
            gameplayEdgeHead = 0;
            gameplayEdgeCount = 0;
            pendingCoverMoveDirection = FpgCoverMoveDirection.None;
            cancelSecondaryOnNextFrame = true;
            hasCaptured = false;
        }

        public void BeginRoomInteraction()
        {
            BeginRoomInteraction(cancelSecondary: false);
        }

        public void BeginRoomInteraction(bool cancelSecondary)
        {
            gameplayEdgeHead = 0;
            gameplayEdgeCount = 0;
            pendingCoverMoveDirection = FpgCoverMoveDirection.None;
            cancelSecondaryOnNextFrame = cancelSecondary || secondaryHeld;
        }

        private void EnqueueGameplayEdge(bool requested, InputEdgeType type)
        {
            if (!requested || gameplayEdgeCount == gameplayEdgeQueueCapacity)
            {
                return;
            }

            int tail = gameplayEdgeHead + gameplayEdgeCount;
            if (tail >= gameplayEdgeQueueCapacity)
            {
                tail -= gameplayEdgeQueueCapacity;
            }

            gameplayEdgeQueue[tail] = NextEdge(type);
            gameplayEdgeCount++;
        }

        private int NextGameplayEdgeIndex(int index)
        {
            index++;
            return index == gameplayEdgeQueueCapacity ? 0 : index;
        }

        private InputEdgeCommand NextEdge(InputEdgeType type)
        {
            return new InputEdgeCommand(new InputSequence(nextInputSequence++), type);
        }

        private static SpatialVectorKey Quantize(Vector3 value, int units)
        {
            return new SpatialVectorKey(
                Quantize(value.x, units),
                Quantize(value.y, units),
                Quantize(value.z, units));
        }

        private static int Quantize(float value, int units)
        {
            double scaled = value * (double)units;
            if (double.IsNaN(scaled)
                || double.IsInfinity(scaled)
                || scaled > int.MaxValue
                || scaled < int.MinValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return checked((int)Math.Round(scaled, MidpointRounding.AwayFromZero));
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
