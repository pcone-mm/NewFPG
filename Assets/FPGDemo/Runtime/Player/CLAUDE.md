# Runtime/Player 指南

`FPG.Player` 依赖 `FPG.Core`、`FPG.Combat` 与 `FPG.Skills`，并保持 `noEngineReferences=true`；命名空间为 `FPG.Demo.Player`。

## 职责边界

- 本程序集拥有 `PlayerInputFrame`、Exposure、Weapon 状态、弹药与 prepare/commit 事务。
- Unity Input、physics、animation、VFX 和 HUD 属于 `FPG.Unity`；这里不读取帧时间或表现状态。

## 工作规则

- ammo、稳定 ID、recovery 和发射状态只在显式 commit 后推进；prepare 失败、取消或放弃不得产生部分消费。
- Secondary 开始前先验证弹药与当前状态；被拒绝的输入不能启动技能或表现时间轴。
- 修改 weapon 状态机时保持 tick 顺序、拒绝原因与回滚行为确定，不在 Unity 适配层复制另一套资源规则。

## 验证

- 武器 prepare/commit、弹药、恢复和回滚检查 `WeaponRuntimeTests.cs`。
- 玩家技能启动/拒绝桥接检查 `FpgPlayerSkillExecutionControllerTests.cs`；程序集边界检查 `AssemblyBoundaryTests.cs`。
