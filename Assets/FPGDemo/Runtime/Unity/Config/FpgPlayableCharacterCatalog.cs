using System;
using System.Collections.Generic;
using FPG.Demo.Player;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public readonly struct FpgPlayableCharacterSelection
    {
        private static readonly SecondaryTriggerMode[]
            LegacySupportedSecondaryModes =
            {
                SecondaryTriggerMode.ImmediateRepeatWhileHeld
            };

        public FpgPlayableCharacterSelection(
            D0CharacterDefinition characterDefinition,
            D0ThreeCProfile threeCProfile,
            D0CombatFeelProfile combatFeelProfile,
            GameObject selectionPreviewPrefab)
            : this(
                characterDefinition,
                threeCProfile,
                combatFeelProfile,
                selectionPreviewPrefab,
                LegacySupportedSecondaryModes,
                SecondaryTriggerMode.ImmediateRepeatWhileHeld)
        {
        }

        public FpgPlayableCharacterSelection(
            D0CharacterDefinition characterDefinition,
            D0ThreeCProfile threeCProfile,
            D0CombatFeelProfile combatFeelProfile,
            GameObject selectionPreviewPrefab,
            IReadOnlyList<SecondaryTriggerMode> supportedSecondaryModes,
            SecondaryTriggerMode selectedSecondaryTriggerMode)
        {
            CharacterDefinition = characterDefinition;
            ThreeCProfile = threeCProfile;
            CombatFeelProfile = combatFeelProfile;
            SelectionPreviewPrefab = selectionPreviewPrefab;
            SupportedSecondaryModes = supportedSecondaryModes;
            SelectedSecondaryTriggerMode = selectedSecondaryTriggerMode;
        }

        public D0CharacterDefinition CharacterDefinition { get; }
        public D0ThreeCProfile ThreeCProfile { get; }
        public D0CombatFeelProfile CombatFeelProfile { get; }
        public GameObject SelectionPreviewPrefab { get; }
        public IReadOnlyList<SecondaryTriggerMode> SupportedSecondaryModes
        {
            get;
        }
        public SecondaryTriggerMode SelectedSecondaryTriggerMode { get; }
        public string CharacterId => CharacterDefinition == null
            ? string.Empty
            : CharacterDefinition.CharacterId;
        public bool IsValid => TryValidate(out _);

        public bool SupportsSecondaryMode(SecondaryTriggerMode mode)
        {
            IReadOnlyList<SecondaryTriggerMode> supportedModes =
                SupportedSecondaryModes;
            if (supportedModes == null)
            {
                return false;
            }

            for (int index = 0; index < supportedModes.Count; index++)
            {
                if (supportedModes[index] == mode)
                {
                    return true;
                }
            }

            return false;
        }

        public FpgPlayableCharacterSelection WithSecondaryMode(
            SecondaryTriggerMode mode)
        {
            return new FpgPlayableCharacterSelection(
                CharacterDefinition,
                ThreeCProfile,
                CombatFeelProfile,
                SelectionPreviewPrefab,
                SupportedSecondaryModes,
                mode);
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (CharacterDefinition == null
                || !CharacterDefinition.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Playable character selection requires a valid character definition.";
                }

                return false;
            }

            if (ThreeCProfile == null || !ThreeCProfile.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Playable character selection requires a valid 3C profile.";
                }

                return false;
            }

            if (CombatFeelProfile == null)
            {
                error = "Playable character selection requires a combat-feel profile.";
                return false;
            }

            if (!CombatFeelProfile.TryValidate(out error))
            {
                return false;
            }

            IReadOnlyList<SecondaryTriggerMode> supportedModes =
                SupportedSecondaryModes;
            if (supportedModes == null || supportedModes.Count == 0)
            {
                error = "Playable character selection requires at least one supported secondary mode.";
                return false;
            }

            for (int index = 0; index < supportedModes.Count; index++)
            {
                SecondaryTriggerMode mode = supportedModes[index];
                if (!Enum.IsDefined(typeof(SecondaryTriggerMode), mode))
                {
                    error =
                        $"Playable character selection contains invalid secondary mode '{mode}'.";
                    return false;
                }

                for (int previousIndex = 0;
                    previousIndex < index;
                    previousIndex++)
                {
                    if (supportedModes[previousIndex] == mode)
                    {
                        error =
                            $"Playable character selection contains duplicate secondary mode '{mode}'.";
                        return false;
                    }
                }
            }

            if (!Enum.IsDefined(
                    typeof(SecondaryTriggerMode),
                    SelectedSecondaryTriggerMode)
                || !SupportsSecondaryMode(SelectedSecondaryTriggerMode))
            {
                error =
                    $"Secondary mode '{SelectedSecondaryTriggerMode}' is not supported by playable character '{CharacterId}'.";
                return false;
            }

            if (!CharacterDefinition.Weapon.TryCreate(
                    SelectedSecondaryTriggerMode,
                    out _,
                    out error))
            {
                error =
                    $"Playable character '{CharacterId}' cannot use secondary mode '{SelectedSecondaryTriggerMode}': {error}";
                return false;
            }

            if (SelectionPreviewPrefab == null)
            {
                error = "Playable character selection requires a visual-only preview prefab.";
                return false;
            }

            if (SelectionPreviewPrefab.GetComponentInChildren<D0ActorEntityView>(true) != null)
            {
                error =
                    "Playable character selection preview must be visual-only and cannot contain an actor Entity.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class FpgPlayableCharacterCatalogEntry
    {
        private static readonly SecondaryTriggerMode[]
            LegacySupportedSecondaryModes =
            {
                SecondaryTriggerMode.ImmediateRepeatWhileHeld
            };

        [SerializeField]
        private D0CharacterDefinition character;

        [SerializeField]
        private D0ThreeCProfile threeCProfile;

        [SerializeField]
        private D0CombatFeelProfile combatFeelProfile;

        [SerializeField]
        [Tooltip("Visual-only prefab used by Boot. It must not contain a D0 actor Entity.")]
        private GameObject selectionPreviewPrefab;

        [SerializeField]
        private SecondaryTriggerMode[] supportedSecondaryModes =
            Array.Empty<SecondaryTriggerMode>();

        [SerializeField]
        private SecondaryTriggerMode defaultSecondaryMode =
            SecondaryTriggerMode.ImmediateRepeatWhileHeld;

        public D0CharacterDefinition Character => character;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public D0CombatFeelProfile CombatFeelProfile => combatFeelProfile;
        public GameObject SelectionPreviewPrefab => selectionPreviewPrefab;
        public IReadOnlyList<SecondaryTriggerMode> SupportedSecondaryModes =>
            supportedSecondaryModes == null
                || supportedSecondaryModes.Length == 0
                ? LegacySupportedSecondaryModes
                : supportedSecondaryModes;
        public SecondaryTriggerMode DefaultSecondaryMode =>
            supportedSecondaryModes == null
                || supportedSecondaryModes.Length == 0
                ? SecondaryTriggerMode.ImmediateRepeatWhileHeld
                : defaultSecondaryMode;

        public bool TryCreateSelection(
            out FpgPlayableCharacterSelection selection,
            out string error)
        {
            selection = new FpgPlayableCharacterSelection(
                character,
                threeCProfile,
                combatFeelProfile,
                selectionPreviewPrefab,
                SupportedSecondaryModes,
                DefaultSecondaryMode);
            if (!selection.TryValidate(out error))
            {
                return false;
            }

            IReadOnlyList<SecondaryTriggerMode> supportedModes =
                SupportedSecondaryModes;
            for (int index = 0; index < supportedModes.Count; index++)
            {
                FpgPlayableCharacterSelection candidate =
                    selection.WithSecondaryMode(supportedModes[index]);
                if (!candidate.TryValidate(out error))
                {
                    error =
                        $"Supported secondary mode '{supportedModes[index]}' is invalid: {error}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "FpgPlayableCharacterCatalog",
        menuName = "FPG Demo/Config/Playable Character Catalog")]
    public sealed class FpgPlayableCharacterCatalog : ScriptableObject
    {
        [SerializeField]
        private D0CharacterDefinition defaultCharacter;

        [SerializeField]
        private FpgPlayableCharacterCatalogEntry[] entries =
            Array.Empty<FpgPlayableCharacterCatalogEntry>();

        public FpgPlayableCharacterSelection DefaultSelection
        {
            get
            {
                return TryResolveDefault(
                    out FpgPlayableCharacterSelection selection,
                    out _)
                    ? selection
                    : default(FpgPlayableCharacterSelection);
            }
        }

        public D0CharacterDefinition DefaultCharacter => defaultCharacter;
        public IReadOnlyList<FpgPlayableCharacterCatalogEntry> Entries =>
            entries ?? Array.Empty<FpgPlayableCharacterCatalogEntry>();
        public int Count => entries == null ? 0 : entries.Length;

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            FpgPlayableCharacterCatalogEntry[] configuredEntries =
                entries ?? Array.Empty<FpgPlayableCharacterCatalogEntry>();
            if (configuredEntries.Length == 0)
            {
                error = "Playable character catalog requires at least one entry.";
                return false;
            }

            if (defaultCharacter == null)
            {
                error = "Playable character catalog requires a default character.";
                return false;
            }

            HashSet<string> characterIds =
                new HashSet<string>(StringComparer.Ordinal);
            bool containsDefault = false;
            for (int index = 0; index < configuredEntries.Length; index++)
            {
                FpgPlayableCharacterCatalogEntry entry = configuredEntries[index];
                if (entry == null
                    || !entry.TryCreateSelection(
                        out FpgPlayableCharacterSelection selection,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Playable character catalog entry {index} is missing.";
                    }
                    else
                    {
                        error = $"Playable character catalog entry {index} is invalid: {error}";
                    }

                    return false;
                }

                if (!characterIds.Add(selection.CharacterId))
                {
                    error =
                        $"Playable character catalog contains duplicate character ID '{selection.CharacterId}'.";
                    return false;
                }

                containsDefault |= ReferenceEquals(
                    selection.CharacterDefinition,
                    defaultCharacter);
            }

            if (!containsDefault)
            {
                error = "Playable character catalog default character is not present in its entries.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryResolveDefault(
            out FpgPlayableCharacterSelection selection,
            out string error)
        {
            return TryResolve(defaultCharacter, out selection, out error);
        }

        public bool TryResolve(
            D0CharacterDefinition character,
            out FpgPlayableCharacterSelection selection,
            out string error)
        {
            selection = default;
            if (!TryValidate(out error))
            {
                return false;
            }

            if (character == null)
            {
                error = "Playable character lookup requires a character definition.";
                return false;
            }

            FpgPlayableCharacterCatalogEntry[] configuredEntries = entries;
            for (int index = 0; index < configuredEntries.Length; index++)
            {
                FpgPlayableCharacterCatalogEntry entry = configuredEntries[index];
                if (ReferenceEquals(entry.Character, character))
                {
                    return entry.TryCreateSelection(out selection, out error);
                }
            }

            error =
                $"Character '{character.CharacterId}' is not present in the playable character catalog.";
            return false;
        }

        public bool TryResolve(
            string characterId,
            out FpgPlayableCharacterSelection selection,
            out string error)
        {
            selection = default;
            if (!TryValidate(out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(characterId))
            {
                error = "Playable character lookup requires a stable character ID.";
                return false;
            }

            FpgPlayableCharacterCatalogEntry[] configuredEntries = entries;
            for (int index = 0; index < configuredEntries.Length; index++)
            {
                FpgPlayableCharacterCatalogEntry entry = configuredEntries[index];
                if (string.Equals(
                        entry.Character.CharacterId,
                        characterId,
                        StringComparison.Ordinal))
                {
                    return entry.TryCreateSelection(out selection, out error);
                }
            }

            error =
                $"Character ID '{characterId}' is not present in the playable character catalog.";
            return false;
        }
    }
}
