using System;
using FPG.Demo.Core;

namespace FPG.Demo.Player
{
    public enum InputEdgeType
    {
        SecondaryPressed = 0,
        SecondaryReleased,
        ReloadPressed
    }

    public readonly struct InputEdgeCommand
    {
        public InputEdgeCommand(InputSequence sequence, InputEdgeType type)
        {
            Sequence = sequence;
            Type = type;
        }

        public InputSequence Sequence { get; }
        public InputEdgeType Type { get; }
    }

    public readonly struct PlayerInputFrame
    {
        public PlayerInputFrame(
            TickIndex tick,
            bool aimHeld,
            bool primaryHeld,
            InputEdgeCommand[] edgeCommands,
            int edgeCommandCount,
            bool cancelSecondary = false)
        {
            if (edgeCommands == null)
            {
                if (edgeCommandCount != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(edgeCommandCount));
                }
            }
            else if (edgeCommandCount < 0 || edgeCommandCount > edgeCommands.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeCommandCount));
            }

            Tick = tick;
            AimHeld = aimHeld;
            PrimaryHeld = primaryHeld;
            EdgeCommands = edgeCommands;
            EdgeCommandCount = edgeCommandCount;
            CancelSecondary = cancelSecondary;
        }

        public TickIndex Tick { get; }
        public bool AimHeld { get; }
        public bool PrimaryHeld { get; }
        public InputEdgeCommand[] EdgeCommands { get; }
        public int EdgeCommandCount { get; }

        /// <summary>
        /// Set only when the Unity-facing input owner intentionally clears
        /// gameplay controls (for example on pause, restart or focus loss).
        /// It lets a pending secondary charge cancel safely instead of staying
        /// exposed after its physical release edge has been discarded.
        /// </summary>
        public bool CancelSecondary { get; }

        public bool HasSecondaryInput
        {
            get
            {
                for (int index = 0; index < EdgeCommandCount; index++)
                {
                    InputEdgeType type = EdgeCommands[index].Type;
                    if (type == InputEdgeType.SecondaryPressed
                        || type == InputEdgeType.SecondaryReleased)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasReloadInput
        {
            get
            {
                for (int index = 0; index < EdgeCommandCount; index++)
                {
                    if (EdgeCommands[index].Type == InputEdgeType.ReloadPressed)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static PlayerInputFrame Empty(
            TickIndex tick,
            bool aimHeld = false,
            bool primaryHeld = false,
            bool cancelSecondary = false)
        {
            return new PlayerInputFrame(
                tick,
                aimHeld,
                primaryHeld,
                null,
                0,
                cancelSecondary);
        }
    }
}
