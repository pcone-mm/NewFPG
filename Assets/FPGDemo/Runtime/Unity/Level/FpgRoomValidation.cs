using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgRoomValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public enum FpgRoomValidationCode
    {
        MissingRoomId = 0,
        MissingDisplayName = 1,
        MissingArtScene = 2,
        MissingMainGroup = 3,
        InvalidMainGroup = 4,
        MissingTagReference = 5,
        InvalidTag = 6,
        DuplicateTag = 7,
        MissingMarker = 8,
        MissingMarkerId = 9,
        DuplicateMarkerId = 10,
        InvalidMarkerPose = 11,
        MissingDestructiblePrefab = 12,
        MissingPlayerEntryPoint = 14,
        MissingEnemySpawnPoint = 15,
        MissingExitSlot = 16,
        DuplicateRoomId = 18,
        MissingRoomReference = 19,
        MissingMarkerDisplayName = 20,
        InvalidEnemySpawnRole = 21,
        InvalidArtSceneReference = 22,
        MissingCoverSlot = 23,
        MissingCoverPrefab = 24,
        InvalidCoverPrefab = 25,
        InvalidCoverDurability = 26,
        InvalidCoverReachablePose = 27,
        MissingStartingCover = 28,
        MultipleStartingCovers = 29,
        OverlappingCoverReachablePosition = 30
    }

    public sealed class FpgRoomValidationIssue
    {
        public FpgRoomValidationIssue(
            FpgRoomValidationSeverity severity,
            FpgRoomValidationCode code,
            string message,
            FpgRoomMarkerKind? markerKind = null,
            string markerId = null)
        {
            Severity = severity;
            Code = code;
            Message = message ?? string.Empty;
            MarkerKind = markerKind;
            MarkerId = markerId ?? string.Empty;
        }

        public FpgRoomValidationSeverity Severity { get; }
        public FpgRoomValidationCode Code { get; }
        public string Message { get; }
        public FpgRoomMarkerKind? MarkerKind { get; }
        public string MarkerId { get; }
    }

    public sealed class FpgRoomValidationResult
    {
        private readonly FpgRoomValidationIssue[] issues;

        public FpgRoomValidationResult(
            IEnumerable<FpgRoomValidationIssue> source)
        {
            List<FpgRoomValidationIssue> materialized = source == null
                ? null
                : new List<FpgRoomValidationIssue>(source);
            issues = materialized == null || materialized.Count == 0
                ? Array.Empty<FpgRoomValidationIssue>()
                : materialized.ToArray();

            int errors = 0;
            int warnings = 0;
            for (int index = 0; index < issues.Length; index++)
            {
                if (issues[index].Severity == FpgRoomValidationSeverity.Error)
                {
                    errors++;
                }
                else
                {
                    warnings++;
                }
            }

            ErrorCount = errors;
            WarningCount = warnings;
        }

        public IReadOnlyList<FpgRoomValidationIssue> Issues => issues;
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public bool IsValid => ErrorCount == 0;

        public FpgRoomValidationIssue FirstError
        {
            get
            {
                for (int index = 0; index < issues.Length; index++)
                {
                    if (issues[index].Severity == FpgRoomValidationSeverity.Error)
                    {
                        return issues[index];
                    }
                }

                return null;
            }
        }
    }

    public static class FpgRoomCollectionValidator
    {
        public static FpgRoomValidationResult Validate(
            IEnumerable<FpgRoomDefinition> rooms)
        {
            List<FpgRoomValidationIssue> issues = new List<FpgRoomValidationIssue>();
            if (rooms == null)
            {
                issues.Add(new FpgRoomValidationIssue(
                    FpgRoomValidationSeverity.Error,
                    FpgRoomValidationCode.MissingRoomReference,
                    "Room collection is missing."));
                return new FpgRoomValidationResult(issues);
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (FpgRoomDefinition room in rooms)
            {
                if (room == null)
                {
                    issues.Add(new FpgRoomValidationIssue(
                        FpgRoomValidationSeverity.Error,
                        FpgRoomValidationCode.MissingRoomReference,
                        $"Room collection entry {index} is missing."));
                    index++;
                    continue;
                }

                FpgRoomValidationResult roomResult = room.Validate();
                for (int issueIndex = 0; issueIndex < roomResult.Issues.Count; issueIndex++)
                {
                    issues.Add(roomResult.Issues[issueIndex]);
                }

                if (!string.IsNullOrWhiteSpace(room.RoomId) && !ids.Add(room.RoomId))
                {
                    issues.Add(new FpgRoomValidationIssue(
                        FpgRoomValidationSeverity.Error,
                        FpgRoomValidationCode.DuplicateRoomId,
                        $"Room ID '{room.RoomId}' must be globally unique."));
                }

                index++;
            }

            return new FpgRoomValidationResult(issues);
        }

        public static bool TryValidate(
            IEnumerable<FpgRoomDefinition> rooms,
            out FpgRoomValidationResult result)
        {
            result = Validate(rooms);
            return result.IsValid;
        }
    }


    internal static class FpgRoomValidationUtility
    {
        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
