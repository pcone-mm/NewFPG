# Runtime/Enemy 指南

`FPG.Enemy` 依赖 `FPG.Core`、`FPG.Combat` 与 `FPG.Skills`，并保持 `noEngineReferences=true`；命名空间为 `FPG.Demo.Enemy`。

## 职责边界

- `EnemyRuntime` 拥有敌人 Active/Groggy/Dead 控制状态、break 恢复和固定容量 threat slot。
- `ThreatRuntime` 拥有 Scheduled -> Telegraph -> Windup -> ReleaseCommitted -> Recovery -> Completed/Canceled 状态机，以及投射物预算的预留、激活与释放。
- `ThreatPayloadDefinition` 只表达 swept projectile 或 timed impact；`FpgThreatPresentationKind` 必须与可拦截性或 heavy weakpoint 语义匹配。
- Unity warning/VFX、配置转换、技能调度与 encounter roster 属于 `FPG.Unity` 或 `FPG.Run`，不进入本程序集。

## 工作规则

- definition/runtime/attack ID、tick、payload count、presentation key 与 stable hash 是重放合同；不得按敌人名字或 prefab 路径特判。
- projectile budget 必须整组原子预留；release 前取消归还 reservation，release 后不能回滚为未发射。
- owner groggy/dead、容量不足、非法状态或错误 tick 一律 fail-closed，不留下部分 threat 或预算泄漏。

## 验证

- threat 预算、取消、release 与终态：`Assets/FPGDemo/Tests/EditMode/ProjectileThreatTests.cs`。
- schedule、snapshot 与 replay：`BattleSessionThreatScheduleTests.cs`。
- presentation language 与正式 scheduler：`FpgThreatPresentationContractTests.cs`、`FpgFormalEnemySkillSchedulerTests.cs`。
- 改 asmdef 或依赖时检查 `AssemblyBoundaryTests.cs`；默认只记录这些精确入口，不自行批量运行测试。
