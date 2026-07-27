# Runtime/Core 指南

`FPG.Core` 是零依赖、`noEngineReferences=true` 的基础领域程序集，命名空间为 `FPG.Demo.Core`。

## 职责边界

- `TickIndex/TickDuration` 与 `GameplayClock` 定义 60Hz 时间、债务、暂停和 prepare/commit/rollback 时钟合同。
- `SessionIds` 拥有 runtime/attack/shot/projectile/impact 等单调 ID；只有成功 commit 才推进需要事务保护的 ID。
- `StableHash`、`DeterministicRandomV1` 与 `SpatialContract` 定义跨程序集重放格式；Unity Vector、Transform、Physics 与浮点空间推断不进入这里。
- `SpatialVectorKey` 是毫米级量化位置，方向与距离按 `SpatialContract` 常量解释；当前 `SpatialContract.Version=3`。

## 工作规则

- 修改 tick、ID 初值/顺序、hash、随机版本、量化比例、容量或 `SpatialContract.Version` 都是跨层兼容变更，必须同步检查所有 transcript、adapter 与 golden digest。
- 溢出、无效 ID、错误 tick、重复 commit/rollback 和债务上限必须显式拒绝或诊断，不使用隐式修正。
- Core 不引用其他 FPG 程序集、UnityEngine、ScriptableObject 或场景对象。

## 验证

- tick、clock、ID、hash 与随机 golden vector：`Assets/FPGDemo/Tests/EditMode/CoreDeterminismTests.cs`。
- 空间版本或量化变化同时检查 `BattleSessionSpatialQueryTests.cs`、`FpgSkillSpatialMetadataTests.cs` 与 `WP4ContractTests.cs`。
- 改 asmdef 或依赖时检查 `AssemblyBoundaryTests.cs`；默认只记录这些精确入口，不自行批量运行测试。
