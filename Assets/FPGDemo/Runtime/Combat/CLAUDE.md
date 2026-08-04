# Runtime/Combat 指南

`FPG.Combat` 只依赖 `FPG.Core` 且 `noEngineReferences=true`，命名空间为 `FPG.Demo.Combat`。

## 职责边界

- 本程序集拥有 combatant、damage/impact ledger、target selection、projectile lifecycle/budget 与 combat trace 等确定性机制。
- Unity physics、表现对象、encounter 调度以及 Player/Enemy 决策属于上层端口或程序集，不进入这里。

## 工作规则

- 固定容量、预算和 `SkillExecutionId`/`GameplayEventId` correlation 在提交前完整预检；失败时不得部分入队、部分扣除或留下半终态。
- 候选排序必须与输入枚举顺序无关；trace digest、稳定 ID 和 projectile terminal commit 是兼容合同。
- `ImpactSpatialContext` 可把带有效 `GeometryId` 的 `EnvironmentBlocker + HitPart.Body` 作为合法终态接触，但它不是 combatant damage target；GeometryId 到 CoverId 的解析与掩体耐久提交属于 Run/Unity Level 上层端口。
- 新机制通过端口接入空间查询和世界状态，不读取 `UnityEngine`、Scene、MonoBehaviour 或 ScriptableObject。

## 验证

- target ordering/area 容量检查 `TargetSelectorTests.cs`；伤害与 impact 检查 `CombatDamageTests.cs`。
- projectile 预算和终态检查 `ProjectileThreatTests.cs`；程序集边界检查 `AssemblyBoundaryTests.cs`。
- 环境阻挡、GeometryId 与掩体提交的原子性检查 `FpgMultiEnemyCombatTransactionTests.cs`。
