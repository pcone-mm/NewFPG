using System;
using System.Globalization;

namespace FPG.Demo.Run
{
    public enum FpgBattleGmCommandKind
    {
        God = 0,
        Ai = 1,
        Spawn = 2
    }

    public enum FpgBattleGmSwitchOperation
    {
        On = 0,
        Off = 1,
        Toggle = 2
    }

    public readonly struct FpgBattleGmCommand
    {
        private FpgBattleGmCommand(
            FpgBattleGmCommandKind kind,
            FpgBattleGmSwitchOperation operation,
            string enemyDefinitionId,
            int count,
            string spawnPointId)
        {
            Kind = kind;
            Operation = operation;
            EnemyDefinitionId = enemyDefinitionId ?? string.Empty;
            Count = count;
            SpawnPointId = spawnPointId ?? string.Empty;
        }

        public FpgBattleGmCommandKind Kind { get; }
        public FpgBattleGmSwitchOperation Operation { get; }
        public string EnemyDefinitionId { get; }
        public int Count { get; }
        public string SpawnPointId { get; }

        internal static FpgBattleGmCommand ForSwitch(
            FpgBattleGmCommandKind kind,
            FpgBattleGmSwitchOperation operation)
        {
            return new FpgBattleGmCommand(
                kind,
                operation,
                string.Empty,
                0,
                string.Empty);
        }

        internal static FpgBattleGmCommand ForSpawn(
            string enemyDefinitionId,
            int count,
            string spawnPointId)
        {
            return new FpgBattleGmCommand(
                FpgBattleGmCommandKind.Spawn,
                FpgBattleGmSwitchOperation.On,
                enemyDefinitionId,
                count,
                spawnPointId);
        }
    }

    /// <summary>
    /// Pure grammar for the development-only battle GM console. Catalog and
    /// room validation deliberately remain in the Unity-facing runtime.
    /// </summary>
    public static class FpgBattleGmCommandParser
    {
        public static bool TryParse(
            string input,
            out FpgBattleGmCommand command,
            out string error)
        {
            command = default(FpgBattleGmCommand);
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "命令不能为空。";
                return false;
            }

            string[] tokens = input.Trim().Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (string.Equals(tokens[0], "gm.god", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tokens[0], "gm.ai", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length != 2
                    || !TryParseSwitch(tokens[1], out FpgBattleGmSwitchOperation operation))
                {
                    error = "命令格式错误，应为：gm.god on|off|toggle 或 gm.ai on|off|toggle。";
                    return false;
                }

                command = FpgBattleGmCommand.ForSwitch(
                    string.Equals(tokens[0], "gm.god", StringComparison.OrdinalIgnoreCase)
                        ? FpgBattleGmCommandKind.God
                        : FpgBattleGmCommandKind.Ai,
                    operation);
                return true;
            }

            if (!string.Equals(tokens[0], "gm.spawn", StringComparison.OrdinalIgnoreCase)
                || tokens.Length < 2
                || tokens.Length > 4
                || string.IsNullOrWhiteSpace(tokens[1]))
            {
                error = "命令格式错误，应为：gm.spawn <敌人配置ID> [数量] [出生点ID]。";
                return false;
            }

            int count = 1;
            if (tokens.Length >= 3
                && (!int.TryParse(
                        tokens[2],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out count)
                    || count <= 0))
            {
                error = "召唤数量必须是正整数。";
                return false;
            }

            string spawnPointId = tokens.Length == 4 ? tokens[3] : string.Empty;
            if (tokens.Length == 4 && string.IsNullOrWhiteSpace(spawnPointId))
            {
                error = "指定出生点时，出生点 ID 不能为空。";
                return false;
            }

            command = FpgBattleGmCommand.ForSpawn(
                tokens[1],
                count,
                spawnPointId);
            return true;
        }

        private static bool TryParseSwitch(
            string value,
            out FpgBattleGmSwitchOperation operation)
        {
            if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
            {
                operation = FpgBattleGmSwitchOperation.On;
                return true;
            }

            if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                operation = FpgBattleGmSwitchOperation.Off;
                return true;
            }

            if (string.Equals(value, "toggle", StringComparison.OrdinalIgnoreCase))
            {
                operation = FpgBattleGmSwitchOperation.Toggle;
                return true;
            }

            operation = default(FpgBattleGmSwitchOperation);
            return false;
        }
    }
}
