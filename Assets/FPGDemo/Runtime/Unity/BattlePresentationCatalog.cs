using System;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Selects the visual-only transform that owns a warning's world-space
    /// position. This must never be used as a combat targeting or damage
    /// input.
    /// </summary>
    public enum WarningAnchorKind
    {
        PlayerGround = 0,
        EnemyWeakpoint
    }

    [Serializable]
    public sealed class WarningPresentationCatalogEntry
    {
        [SerializeField, Min(1)]
        private int presentationKey = 1;

        [SerializeField]
        private WarningView viewPrefab;

        [SerializeField, Min(1)]
        private int prewarmCapacity = 1;

        [SerializeField]
        private Color tint = Color.white;

        [SerializeField]
        private WarningAnchorKind anchorKind = WarningAnchorKind.PlayerGround;

        public int PresentationKey => presentationKey;
        public WarningView ViewPrefab => viewPrefab;
        public int PrewarmCapacity => prewarmCapacity;
        public Color Tint => tint;
        public WarningAnchorKind AnchorKind => anchorKind;
    }

    [Serializable]
    public sealed class ImpactPresentationCatalogEntry
    {
        [SerializeField]
        private ImpactView viewPrefab;

        [SerializeField, Min(1)]
        private int prewarmCapacity = 1;

        public ImpactView ViewPrefab => viewPrefab;
        public int PrewarmCapacity => prewarmCapacity;
    }

    [CreateAssetMenu(fileName = "BattlePresentationCatalog", menuName = "FPG Demo/Battle Presentation Catalog")]
    public sealed class BattlePresentationCatalog : ScriptableObject
    {
        [SerializeField]
        private WarningPresentationCatalogEntry[] warningEntries =
            Array.Empty<WarningPresentationCatalogEntry>();

        [SerializeField]
        private ImpactPresentationCatalogEntry impactEntry;

        public int WarningEntryCount => warningEntries == null ? 0 : warningEntries.Length;

        public WarningPresentationCatalogEntry GetWarningEntry(int index)
        {
            if (index < 0 || index >= WarningEntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return warningEntries[index];
        }

        public bool TryGetWarningEntry(
            int presentationKey,
            out WarningPresentationCatalogEntry entry)
        {
            WarningPresentationCatalogEntry[] entries = warningEntries
                ?? Array.Empty<WarningPresentationCatalogEntry>();
            for (int index = 0; index < entries.Length; index++)
            {
                WarningPresentationCatalogEntry candidate = entries[index];
                if (candidate != null && candidate.PresentationKey == presentationKey)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public bool UsesWarningAnchorKind(WarningAnchorKind anchorKind)
        {
            WarningPresentationCatalogEntry[] entries = warningEntries
                ?? Array.Empty<WarningPresentationCatalogEntry>();
            for (int index = 0; index < entries.Length; index++)
            {
                WarningPresentationCatalogEntry entry = entries[index];
                if (entry != null && entry.AnchorKind == anchorKind)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryValidateWarningCoverage(ScenarioDefinition definition, out string error)
        {
            if (definition == null)
            {
                error = "ScenarioDefinition is required to validate warning presentation coverage.";
                return false;
            }

            WarningPresentationCatalogEntry[] entries = warningEntries
                ?? Array.Empty<WarningPresentationCatalogEntry>();
            for (int index = 0; index < entries.Length; index++)
            {
                WarningPresentationCatalogEntry entry = entries[index];
                if (entry == null)
                {
                    error = $"Warning presentation entry {index} is missing.";
                    return false;
                }

                if (entry.PresentationKey <= 0 || entry.PrewarmCapacity <= 0)
                {
                    error = $"Warning presentation entry {index} needs a positive key and prewarm capacity.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(WarningAnchorKind), entry.AnchorKind))
                {
                    error = $"Warning presentation entry {index} has an invalid warning anchor kind.";
                    return false;
                }

                if (!WarningView.TryValidatePrefab(entry.ViewPrefab, out string prefabError))
                {
                    error = $"Warning presentation entry {index} is invalid: {prefabError}";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    WarningPresentationCatalogEntry previous = entries[previousIndex];
                    if (previous != null && previous.PresentationKey == entry.PresentationKey)
                    {
                        error = $"Warning presentation key {entry.PresentationKey} is duplicated.";
                        return false;
                    }
                }
            }

            for (int scheduleIndex = 0; scheduleIndex < definition.ThreatScheduleCount; scheduleIndex++)
            {
                ThreatPayloadDefinition payload = definition.GetThreatScheduleEntry(scheduleIndex).Payload;
                if (!TryGetWarningEntry(payload.PresentationKey, out WarningPresentationCatalogEntry entry))
                {
                    error = $"Warning presentation key {payload.PresentationKey} is not covered by the catalog.";
                    return false;
                }

                if (payload.PresentationKind
                        == FpgThreatPresentationKind.HeavyWeakpoint
                    && entry.AnchorKind != WarningAnchorKind.EnemyWeakpoint)
                {
                    error =
                        "HeavyWeakpoint threats require an EnemyWeakpoint warning anchor.";
                    return false;
                }

                if (entry.PrewarmCapacity < definition.ThreatCapacity)
                {
                    error = $"Warning presentation key {payload.PresentationKey} must prewarm at least the scenario threat capacity ({definition.ThreatCapacity}).";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryGetImpactEntry(out ImpactPresentationCatalogEntry entry, out string error)
        {
            entry = impactEntry;
            if (entry == null)
            {
                error = "Impact presentation entry is required.";
                return false;
            }

            if (entry.PrewarmCapacity <= 0)
            {
                entry = null;
                error = "Impact presentation entry needs a positive prewarm capacity.";
                return false;
            }

            if (!ImpactView.TryValidatePrefab(entry.ViewPrefab, out string prefabError))
            {
                entry = null;
                error = $"Impact presentation entry is invalid: {prefabError}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidatePresentationCoverage(ScenarioDefinition definition, out string error)
        {
            if (!TryValidateWarningCoverage(definition, out error))
            {
                return false;
            }

            if (!TryGetImpactEntry(out ImpactPresentationCatalogEntry ignoredEntry, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
