using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Authoring projection for one weighted wave layout. Layout weights choose
    /// between layouts; shares split the selected layout's room budget.
    /// </summary>
    [Serializable]
    public sealed class FpgWaveLayoutDefinition
    {
        [D0PlannerField("Layout ID", "Stable identity included in the generated plan digest.")]
        [SerializeField]
        private string layoutId = "layout";

        [D0PlannerField("Selection Weight", "Relative weight used only when choosing a wave layout.")]
        [SerializeField, Min(1)]
        private int selectionWeight = 1;

        [D0PlannerField("Wave Shares", "Positive basis-point shares; one layout must total 10000.")]
        [SerializeField]
        private FpgWaveBudgetShareDefinition[] waveShares =
        {
            new FpgWaveBudgetShareDefinition(FpgEncounterRunContext.BasisPointsOne)
        };

        public FpgWaveLayoutDefinition()
        {
        }

        public FpgWaveLayoutDefinition(
            string layoutId,
            int selectionWeight,
            FpgWaveBudgetShareDefinition[] waveShares)
        {
            this.layoutId = layoutId;
            this.selectionWeight = selectionWeight;
            this.waveShares = waveShares == null
                ? Array.Empty<FpgWaveBudgetShareDefinition>()
                : (FpgWaveBudgetShareDefinition[])waveShares.Clone();
        }

        public string LayoutId => layoutId;
        public int SelectionWeight => selectionWeight;
        public IReadOnlyList<FpgWaveBudgetShareDefinition> WaveShares =>
            waveShares ?? Array.Empty<FpgWaveBudgetShareDefinition>();

        public bool TryBuildData(out FpgWaveLayoutData data, out string error)
        {
            data = null;
            if (!TryValidate(out error))
            {
                return false;
            }

            FpgWaveBudgetShareDefinition[] definitions =
                waveShares ?? Array.Empty<FpgWaveBudgetShareDefinition>();
            FpgWaveBudgetShare[] shares = new FpgWaveBudgetShare[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                shares[index] = new FpgWaveBudgetShare(definitions[index].BasisPoints);
            }

            try
            {
                data = new FpgWaveLayoutData(layoutId, selectionWeight, shares);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                data = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                error = "Wave layout requires a stable ID.";
                return false;
            }

            if (selectionWeight <= 0)
            {
                error = $"Wave layout '{layoutId}' requires a positive selection weight.";
                return false;
            }

            FpgWaveBudgetShareDefinition[] definitions =
                waveShares ?? Array.Empty<FpgWaveBudgetShareDefinition>();
            long total = 0L;
            for (int index = 0; index < definitions.Length; index++)
            {
                if (definitions[index].BasisPoints <= 0)
                {
                    error = $"Wave layout '{layoutId}' has a non-positive share.";
                    return false;
                }

                total += definitions[index].BasisPoints;
            }

            if (definitions.Length == 0
                || total != FpgEncounterRunContext.BasisPointsOne)
            {
                error = $"Wave layout '{layoutId}' shares must total "
                    + FpgEncounterRunContext.BasisPointsOne + ".";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static FpgWaveLayoutDefinition[] CreateHadesDefaults()
        {
            return new[]
            {
                new FpgWaveLayoutDefinition(
                    "single-100",
                    1,
                    new[]
                    {
                        new FpgWaveBudgetShareDefinition(10000)
                    }),
                new FpgWaveLayoutDefinition(
                    "double-50-50",
                    1,
                    new[]
                    {
                        new FpgWaveBudgetShareDefinition(5000),
                        new FpgWaveBudgetShareDefinition(5000)
                    }),
                new FpgWaveLayoutDefinition(
                    "triple-30-15-55",
                    1,
                    new[]
                    {
                        new FpgWaveBudgetShareDefinition(3000),
                        new FpgWaveBudgetShareDefinition(1500),
                        new FpgWaveBudgetShareDefinition(5500)
                    })
            };
        }
    }
}
