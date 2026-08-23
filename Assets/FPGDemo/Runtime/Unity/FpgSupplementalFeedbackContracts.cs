using System;
using FPG.Demo.Core;

namespace FPG.Demo.Unity
{
    public enum FpgSupplementalFeedbackKind
    {
        None = 0,
        PrimaryFire,
        SecondaryFire,
        BodyHit,
        WeakpointHit,
        ProjectileIntercept,
        HudLifeChanged,
        HudBarrierChanged,
        HudAmmoChanged,
        StopAll
    }

    public readonly struct FpgSupplementalFeedbackEvent
    {
        private FpgSupplementalFeedbackEvent(
            FpgSupplementalFeedbackKind kind,
            float intensity,
            bool hasTarget,
            RuntimeId targetId,
            bool hasResourceValues,
            int previousValue,
            int currentValue,
            int previousMaxValue,
            int currentMaxValue)
        {
            if (!Enum.IsDefined(typeof(FpgSupplementalFeedbackKind), kind))
            {
                throw new ArgumentException(
                    "Supplemental feedback kind is invalid.",
                    nameof(kind));
            }

            Kind = kind;
            Intensity = IsFinite(intensity) ? Math.Max(0f, intensity) : 1f;
            HasTarget = hasTarget;
            TargetId = hasTarget ? targetId : RuntimeId.Invalid;
            HasResourceValues = hasResourceValues;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            PreviousMaxValue = previousMaxValue;
            CurrentMaxValue = currentMaxValue;
        }

        public FpgSupplementalFeedbackKind Kind { get; }
        public float Intensity { get; }
        public bool HasTarget { get; }
        public RuntimeId TargetId { get; }
        public bool HasResourceValues { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
        public int PreviousMaxValue { get; }
        public int CurrentMaxValue { get; }
        public int Delta => HasResourceValues ? CurrentValue - PreviousValue : 0;

        public static FpgSupplementalFeedbackEvent Create(
            FpgSupplementalFeedbackKind kind,
            float intensity = 1f)
        {
            return new FpgSupplementalFeedbackEvent(
                kind,
                intensity,
                false,
                RuntimeId.Invalid,
                false,
                0,
                0,
                0,
                0);
        }

        public static FpgSupplementalFeedbackEvent CreateTargeted(
            FpgSupplementalFeedbackKind kind,
            RuntimeId targetId,
            float intensity = 1f)
        {
            if (!targetId.IsValid)
            {
                throw new ArgumentException(
                    "Targeted feedback requires a valid RuntimeId.",
                    nameof(targetId));
            }

            return new FpgSupplementalFeedbackEvent(
                kind,
                intensity,
                true,
                targetId,
                false,
                0,
                0,
                0,
                0);
        }

        public static FpgSupplementalFeedbackEvent CreateResourceChange(
            FpgSupplementalFeedbackKind kind,
            int previousValue,
            int currentValue,
            int previousMaxValue,
            int currentMaxValue)
        {
            if (kind != FpgSupplementalFeedbackKind.HudLifeChanged
                && kind != FpgSupplementalFeedbackKind.HudBarrierChanged
                && kind != FpgSupplementalFeedbackKind.HudAmmoChanged)
            {
                throw new ArgumentException(
                    "Resource change feedback requires a HUD resource kind.",
                    nameof(kind));
            }

            return new FpgSupplementalFeedbackEvent(
                kind,
                1f,
                false,
                RuntimeId.Invalid,
                true,
                previousValue,
                currentValue,
                previousMaxValue,
                currentMaxValue);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
