using System;
using System.Collections.Generic;

namespace FPG.Demo.Unity
{
    public enum FpgRoomEncounterValidationCode
    {
        MissingRoom = 0,
        MissingScenario = 1,
        InvalidRoom = 2,
        InvalidScenario = 3,
        MissingPlayerEntryPoint = 4,
        MissingEnemySpawnPoint = 5
    }

    public sealed class FpgRoomEncounterValidationIssue
    {
        public FpgRoomEncounterValidationIssue(
            FpgRoomValidationSeverity severity,
            FpgRoomEncounterValidationCode code,
            string message,
            string markerId = null)
        {
            Severity = severity;
            Code = code;
            Message = message ?? string.Empty;
            MarkerId = markerId ?? string.Empty;
        }

        public FpgRoomValidationSeverity Severity { get; }
        public FpgRoomEncounterValidationCode Code { get; }
        public string Message { get; }
        public string MarkerId { get; }
    }

    public sealed class FpgRoomEncounterValidationResult
    {
        private readonly FpgRoomEncounterValidationIssue[] issues;

        public FpgRoomEncounterValidationResult(
            IEnumerable<FpgRoomEncounterValidationIssue> source)
        {
            issues = source == null
                ? Array.Empty<FpgRoomEncounterValidationIssue>()
                : new List<FpgRoomEncounterValidationIssue>(source).ToArray();

            int errorCount = 0;
            int warningCount = 0;
            for (int index = 0; index < issues.Length; index++)
            {
                if (issues[index].Severity == FpgRoomValidationSeverity.Error)
                {
                    errorCount++;
                }
                else
                {
                    warningCount++;
                }
            }

            ErrorCount = errorCount;
            WarningCount = warningCount;
        }

        public IReadOnlyList<FpgRoomEncounterValidationIssue> Issues => issues;
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public bool IsValid => ErrorCount == 0;

        public FpgRoomEncounterValidationIssue FirstError
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

    /// <summary>
    /// Validates the composition boundary between a room's spatial markers
    /// and a D0 encounter. Neither asset owns or mutates the other.
    /// </summary>
    public static class FpgRoomEncounterValidator
    {
        public static FpgRoomEncounterValidationResult Validate(
            FpgRoomDefinition room,
            D0CombatScenarioDefinition scenario)
        {
            List<FpgRoomEncounterValidationIssue> issues =
                new List<FpgRoomEncounterValidationIssue>();

            if (room == null)
            {
                AddError(
                    issues,
                    FpgRoomEncounterValidationCode.MissingRoom,
                    "Room and encounter validation requires a room definition.");
            }
            else
            {
                FpgRoomValidationResult roomValidation = room.Validate();
                if (!roomValidation.IsValid)
                {
                    string detail = roomValidation.FirstError == null
                        ? "unknown room validation error"
                        : roomValidation.FirstError.Message;
                    AddError(
                        issues,
                        FpgRoomEncounterValidationCode.InvalidRoom,
                        $"Room '{room.RoomId}' is invalid: {detail}");
                }
            }

            if (scenario == null)
            {
                AddError(
                    issues,
                    FpgRoomEncounterValidationCode.MissingScenario,
                    "Room and encounter validation requires a D0 scenario definition.");
            }
            else if (!scenario.TryValidateForRoom(out string scenarioError))
            {
                AddError(
                    issues,
                    FpgRoomEncounterValidationCode.InvalidScenario,
                    $"Scenario '{scenario.ScenarioId}' is invalid: {scenarioError}");
            }

            if (room == null || scenario == null)
            {
                return new FpgRoomEncounterValidationResult(issues);
            }

            if (!room.TryGetPlayerEntryPoint(
                    scenario.PlayerSpawnPointId,
                    out _))
            {
                AddError(
                    issues,
                    FpgRoomEncounterValidationCode.MissingPlayerEntryPoint,
                    $"Room '{room.RoomId}' does not define player entry point "
                    + $"'{scenario.PlayerSpawnPointId}'.",
                    scenario.PlayerSpawnPointId);
            }

            D0EncounterDefinition encounter = scenario.Encounter;
            if (encounter == null)
            {
                return new FpgRoomEncounterValidationResult(issues);
            }

            HashSet<string> checkedSpawnIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < encounter.SpawnSlotCount; index++)
            {
                D0EncounterSpawnSlot slot = encounter.GetSpawnSlot(index);
                if (slot == null || !checkedSpawnIds.Add(slot.SpawnPointId))
                {
                    continue;
                }

                if (!room.TryGetEnemySpawnPoint(slot.SpawnPointId, out _))
                {
                    AddError(
                        issues,
                        FpgRoomEncounterValidationCode.MissingEnemySpawnPoint,
                        $"Room '{room.RoomId}' does not define enemy spawn point "
                        + $"'{slot.SpawnPointId}' required by encounter "
                        + $"'{encounter.EncounterId}'.",
                        slot.SpawnPointId);
                }
            }

            return new FpgRoomEncounterValidationResult(issues);
        }

        public static bool TryValidate(
            FpgRoomDefinition room,
            D0CombatScenarioDefinition scenario,
            out FpgRoomEncounterValidationResult result)
        {
            result = Validate(room, scenario);
            return result.IsValid;
        }

        private static void AddError(
            List<FpgRoomEncounterValidationIssue> issues,
            FpgRoomEncounterValidationCode code,
            string message,
            string markerId = null)
        {
            issues.Add(new FpgRoomEncounterValidationIssue(
                FpgRoomValidationSeverity.Error,
                code,
                message,
                markerId));
        }
    }
}
