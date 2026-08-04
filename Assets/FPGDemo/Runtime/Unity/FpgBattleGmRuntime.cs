#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Mutable BattleTest-only GM state bound to one explicit formal host.
    /// </summary>
    public sealed class FpgBattleGmRuntime : IDisposable
    {
        private readonly FpgFormalEncounterHost host;
        private int spawnPointRoundRobinCursor;
        private bool disposed;

        public FpgBattleGmRuntime(FpgFormalEncounterHost host)
        {
            this.host = host != null
                ? host
                : throw new ArgumentNullException(nameof(host));
            IsEnemyAiEnabled = true;
            ApplyCombatState();
        }

        public FpgFormalEncounterHost Host => host;
        public bool IsPlayerInvincible { get; private set; }
        public bool IsEnemyAiEnabled { get; private set; }
        public int SpawnPointRoundRobinCursor => spawnPointRoundRobinCursor;
        public bool IsDisposed => disposed;

        public bool TryExecute(string commandLine, out string result)
        {
            if (disposed)
            {
                result = "GM 运行时已关闭。";
                return false;
            }

            if (!FpgBattleGmCommandParser.TryParse(
                    commandLine,
                    out FpgBattleGmCommand command,
                    out result))
            {
                return false;
            }

            switch (command.Kind)
            {
                case FpgBattleGmCommandKind.God:
                    return TrySetPlayerInvincible(
                        ApplySwitch(IsPlayerInvincible, command.Operation),
                        out result);

                case FpgBattleGmCommandKind.Ai:
                    return TrySetEnemyAiEnabled(
                        ApplySwitch(IsEnemyAiEnabled, command.Operation),
                        out result);

                case FpgBattleGmCommandKind.Spawn:
                    return TrySpawn(
                        command.EnemyDefinitionId,
                        command.Count,
                        command.SpawnPointId,
                        out result);

                default:
                    result = "不支持的 GM 命令。";
                    return false;
            }
        }

        public bool TrySetPlayerInvincible(bool value, out string result)
        {
            if (!TryGetCombatPort(out FpgMultiEnemyCombatPort combatPort, out result))
            {
                return false;
            }

            IsPlayerInvincible = value;
            combatPort.IsPlayerInvincible = value;
            result = "玩家无敌已" + (value ? "开启。" : "关闭。");
            return true;
        }

        public bool TrySetEnemyAiEnabled(bool value, out string result)
        {
            if (!TryGetCombatPort(out FpgMultiEnemyCombatPort combatPort, out result))
            {
                return false;
            }

            IsEnemyAiEnabled = value;
            combatPort.IsEnemyAiEnabled = value;
            result = "怪物 AI 已" + (value ? "开启。" : "关闭。");
            return true;
        }

        public bool TrySpawn(
            string enemyDefinitionId,
            int count,
            string spawnPointId,
            out string result)
        {
            if (!TryGetSandboxDirector(
                    out FpgRoomEncounterDirector director,
                    out result))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(enemyDefinitionId)
                || director.EnemyCatalog == null
                || !director.EnemyCatalog.TryGet(enemyDefinitionId, out _))
            {
                result = "未找到敌人配置 ID："
                    + (enemyDefinitionId ?? string.Empty) + "。";
                return false;
            }

            if (count <= 0)
            {
                result = "召唤数量必须是正整数。";
                return false;
            }

            FpgRoomDefinition room = director.RoomDefinition;
            IReadOnlyList<FpgRoomEnemySpawnPoint> spawnPoints =
                room == null ? null : room.EnemySpawnPoints;
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                result = "当前房间没有可用的敌人出生点。";
                return false;
            }

            string explicitPoint = string.IsNullOrWhiteSpace(spawnPointId)
                ? string.Empty
                : spawnPointId.Trim();
            if (!string.IsNullOrEmpty(explicitPoint)
                && !room.TryGetEnemySpawnPoint(explicitPoint, out _))
            {
                result = "未找到敌人出生点 ID：" + explicitPoint + "。";
                return false;
            }

            int queued = 0;
            RuntimeId lastRuntimeId = RuntimeId.Invalid;
            string failure = string.Empty;
            for (int index = 0; index < count; index++)
            {
                string pointId = explicitPoint;
                int selectedPointIndex = -1;
                if (string.IsNullOrEmpty(pointId))
                {
                    int pointIndex = spawnPointRoundRobinCursor;
                    if (pointIndex < 0 || pointIndex >= spawnPoints.Count)
                    {
                        pointIndex = 0;
                    }

                    pointId = spawnPoints[pointIndex].MarkerId;
                    selectedPointIndex = pointIndex;
                }

                if (!director.TryQueueExternalSpawn(
                    enemyDefinitionId,
                    pointId,
                        out RuntimeId runtimeId,
                        out failure))
                {
                    break;
                }

                queued++;
                lastRuntimeId = runtimeId;
                if (selectedPointIndex >= 0)
                {
                    spawnPointRoundRobinCursor = selectedPointIndex + 1;
                    if (spawnPointRoundRobinCursor >= spawnPoints.Count)
                    {
                        spawnPointRoundRobinCursor = 0;
                    }
                }
            }

            if (queued == count)
            {
                result = "已加入召唤队列：" + queued + " 个 " + enemyDefinitionId
                    + (string.IsNullOrEmpty(explicitPoint)
                        ? "，使用房间出生点轮询。"
                        : "，出生点 " + explicitPoint + "。")
                    + (lastRuntimeId.IsValid
                        ? " 最后一个运行时 ID：" + lastRuntimeId + "。"
                        : string.Empty);
                return true;
            }

            result = "计划召唤 " + count + " 个 " + enemyDefinitionId
                + "，已成功加入 " + queued + " 个；正式运行时容量已耗尽："
                + TranslateRuntimeFailure(failure)
                + "。";
            return false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (host != null && host.EncounterDirector != null
                && host.EncounterDirector.CombatPort != null)
            {
                host.EncounterDirector.CombatPort.IsPlayerInvincible = false;
                host.EncounterDirector.CombatPort.IsEnemyAiEnabled = true;
            }

            IsPlayerInvincible = false;
            IsEnemyAiEnabled = true;
            disposed = true;
        }

        private void ApplyCombatState()
        {
            if (host.EncounterDirector == null
                || host.EncounterDirector.CombatPort == null)
            {
                return;
            }

            host.EncounterDirector.CombatPort.IsPlayerInvincible =
                IsPlayerInvincible;
            host.EncounterDirector.CombatPort.IsEnemyAiEnabled =
                IsEnemyAiEnabled;
        }

        private bool TryGetCombatPort(
            out FpgMultiEnemyCombatPort combatPort,
            out string error)
        {
            combatPort = null;
            if (!TryGetSandboxDirector(
                    out FpgRoomEncounterDirector director,
                    out error))
            {
                return false;
            }

            combatPort = director.CombatPort;
            if (combatPort == null)
            {
                error = "BattleTest 战斗运行时不可用。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryGetSandboxDirector(
            out FpgRoomEncounterDirector director,
            out string error)
        {
            director = host == null ? null : host.EncounterDirector;
            if (disposed || director == null || director.Session == null
                || director.Session.Runtime.Mode
                    != FpgEncounterRuntimeMode.BattleTestSandbox)
            {
                error = disposed
                    ? "GM 运行时已关闭。"
                    : "GM 命令只能在正在运行的 BattleTest 沙盒中执行。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ApplySwitch(
            bool current,
            FpgBattleGmSwitchOperation operation)
        {
            switch (operation)
            {
                case FpgBattleGmSwitchOperation.On:
                    return true;
                case FpgBattleGmSwitchOperation.Off:
                    return false;
                case FpgBattleGmSwitchOperation.Toggle:
                    return !current;
                default:
                    return current;
            }
        }

        private static string TranslateRuntimeFailure(string failure)
        {
            if (!Enum.TryParse(failure, out RejectReason reason))
            {
                return "未分类的运行时错误";
            }

            switch (reason)
            {
                case RejectReason.InvalidState:
                    return "当前运行状态不允许该操作";
                case RejectReason.InvalidDefinition:
                    return "配置无效";
                case RejectReason.WrongTick:
                    return "战斗时序无效";
                case RejectReason.DuplicateSequence:
                    return "序列重复";
                case RejectReason.ExpiredSequence:
                    return "序列已过期";
                case RejectReason.NotEnoughAmmo:
                    return "弹药不足";
                case RejectReason.NotExposed:
                    return "目标未暴露";
                case RejectReason.BarrierDepleted:
                    return "掩体耐久已耗尽";
                case RejectReason.ActionLocked:
                    return "动作已锁定";
                case RejectReason.Cooldown:
                    return "技能仍在冷却";
                case RejectReason.AlreadyTerminal:
                    return "对象已结束生命周期";
                case RejectReason.InvalidTarget:
                    return "目标无效";
                case RejectReason.DuplicateImpact:
                    return "伤害事件重复";
                case RejectReason.BudgetExceeded:
                    return "战斗预算已耗尽";
                case RejectReason.BufferCapacity:
                    return "固定容器容量已耗尽";
                case RejectReason.OwnerInterrupted:
                    return "技能持有者已被打断";
                case RejectReason.OwnerGroggy:
                    return "技能持有者处于硬直状态";
                case RejectReason.RestartRequired:
                    return "需要重新启动战斗会话";
                case RejectReason.Disposed:
                    return "运行时已关闭";
                case RejectReason.InvariantFault:
                    return "战斗运行时一致性检查失败";
                default:
                    return "未知原因";
            }
        }
    }
}
#endif
