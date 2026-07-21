using System;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Stable v1 authoring defaults. These values are also used by editor
    /// previews when a profile has not selected a custom wave layout.
    /// </summary>
    public static class FpgFormalEncounterDefaults
    {
        public const int DifficultyBasisPoints = 10000;
        public const int DefaultBaseBudget = 6;
        public const int DefaultDepthRamp = 2;
        public const int DefaultMinimumBudget = 6;

        public static readonly int[] FullWaveShares = { 10000 };
        public static readonly int[] HalfHalfWaveShares = { 5000, 5000 };
        public static readonly int[] ThirtyFifteenFiftyFiveWaveShares = { 3000, 1500, 5500 };

        public const string BurstbugEnemyId = "burstbug";
        public const string HudieEnemyId = "hudie";
        public const string LuanEnemyId = "luan";

        public static int[] CopyShares(FpgWaveBudgetTemplate template)
        {
            switch (template)
            {
                case FpgWaveBudgetTemplate.Full:
                    return (int[])FullWaveShares.Clone();
                case FpgWaveBudgetTemplate.HalfHalf:
                    return (int[])HalfHalfWaveShares.Clone();
                case FpgWaveBudgetTemplate.ThirtyFifteenFiftyFive:
                    return (int[])ThirtyFifteenFiftyFiveWaveShares.Clone();
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
