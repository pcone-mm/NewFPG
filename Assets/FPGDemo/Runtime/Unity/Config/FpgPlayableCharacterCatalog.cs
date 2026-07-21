using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public readonly struct FpgPlayableCharacterSelection
    {
        public FpgPlayableCharacterSelection(
            D0CharacterDefinition characterDefinition,
            D0ThreeCProfile threeCProfile,
            GameObject selectionPreviewPrefab)
        {
            CharacterDefinition = characterDefinition;
            ThreeCProfile = threeCProfile;
            SelectionPreviewPrefab = selectionPreviewPrefab;
        }

        public D0CharacterDefinition CharacterDefinition { get; }
        public D0ThreeCProfile ThreeCProfile { get; }
        public GameObject SelectionPreviewPrefab { get; }
        public string CharacterId => CharacterDefinition == null
            ? string.Empty
            : CharacterDefinition.CharacterId;
        public bool IsValid => TryValidate(out _);

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
        [SerializeField]
        private D0CharacterDefinition character;

        [SerializeField]
        private D0ThreeCProfile threeCProfile;

        [SerializeField]
        [Tooltip("Visual-only prefab used by Boot. It must not contain a D0 actor Entity.")]
        private GameObject selectionPreviewPrefab;

        public D0CharacterDefinition Character => character;
        public D0ThreeCProfile ThreeCProfile => threeCProfile;
        public GameObject SelectionPreviewPrefab => selectionPreviewPrefab;

        public bool TryCreateSelection(
            out FpgPlayableCharacterSelection selection,
            out string error)
        {
            selection = new FpgPlayableCharacterSelection(
                character,
                threeCProfile,
                selectionPreviewPrefab);
            return selection.TryValidate(out error);
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
