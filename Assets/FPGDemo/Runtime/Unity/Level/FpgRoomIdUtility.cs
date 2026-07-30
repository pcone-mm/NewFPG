using System;
using System.Collections.Generic;
using System.Text;

namespace FPG.Demo.Unity
{
    public static class FpgRoomIdUtility
    {
        public static string GenerateRoomId(
            string preferredNameOrId,
            IEnumerable<string> existingRoomIds)
        {
            return GenerateUniqueId(preferredNameOrId, existingRoomIds, "room");
        }

        public static string GenerateMarkerId(
            FpgRoomMarkerKind kind,
            string semanticSuffix,
            IEnumerable<string> existingMarkerIds)
        {
            string prefix;
            switch (kind)
            {
                case FpgRoomMarkerKind.Exit:
                    prefix = "exit";
                    break;
                case FpgRoomMarkerKind.PlayerEntry:
                    prefix = "player";
                    break;
                case FpgRoomMarkerKind.EnemySpawn:
                    prefix = "enemy";
                    break;
                case FpgRoomMarkerKind.Destructible:
                    prefix = "destructible";
                    break;
                case FpgRoomMarkerKind.Cover:
                    prefix = "cover";
                    break;
                default:
                    prefix = "marker";
                    break;
            }

            string suffix = NormalizeSemanticId(semanticSuffix);
            string preferred = string.IsNullOrEmpty(suffix)
                ? prefix + "-main"
                : prefix + "-" + suffix;
            return GenerateUniqueId(preferred, existingMarkerIds, prefix + "-main");
        }

        public static string GenerateUniqueId(
            string preferredId,
            IEnumerable<string> existingIds,
            string fallbackId = "item")
        {
            string normalizedFallback = NormalizeSemanticId(fallbackId);
            if (string.IsNullOrEmpty(normalizedFallback))
            {
                normalizedFallback = "item";
            }

            string candidate = NormalizeSemanticId(preferredId);
            if (string.IsNullOrEmpty(candidate))
            {
                candidate = normalizedFallback;
            }

            HashSet<string> occupied = new HashSet<string>(StringComparer.Ordinal);
            if (existingIds != null)
            {
                foreach (string existingId in existingIds)
                {
                    if (!string.IsNullOrWhiteSpace(existingId))
                    {
                        occupied.Add(existingId);
                    }
                }
            }

            if (!occupied.Contains(candidate))
            {
                return candidate;
            }

            for (int suffix = 2; suffix < int.MaxValue; suffix++)
            {
                string numberedCandidate = candidate + "-" + suffix.ToString("00");
                if (!occupied.Contains(numberedCandidate))
                {
                    return numberedCandidate;
                }
            }

            throw new InvalidOperationException(
                $"No unique semantic ID could be generated for '{candidate}'.");
        }

        public static string NormalizeSemanticId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool pendingSeparator = false;
            string trimmed = value.Trim();
            for (int index = 0; index < trimmed.Length; index++)
            {
                char character = char.ToLowerInvariant(trimmed[index]);
                bool isAsciiLetter = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                if (isAsciiLetter || isDigit)
                {
                    if (pendingSeparator && builder.Length > 0)
                    {
                        builder.Append('-');
                    }

                    builder.Append(character);
                    pendingSeparator = false;
                }
                else
                {
                    pendingSeparator = builder.Length > 0;
                }
            }

            return builder.ToString();
        }
    }
}
