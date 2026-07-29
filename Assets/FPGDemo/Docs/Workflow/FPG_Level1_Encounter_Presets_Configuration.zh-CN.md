# FPG Level 1 Encounter 预设指南

Level 1 使用 Burstbug、Hudie 和 Luan 组成固定三波预设。权威入口位于 `Config/FormalEncounter/Level1`：

- `FPG_L1_01_01_Intro.asset`
- `FPG_L1_01_02_Mixed.asset`
- `FPG_L1_01_03_RangedPressure.asset`
- `FPG_L1_01_04_Challenge.asset`
- `FPG_L1_01_EnemyPool.asset`
- `FPG_L1_01_Profile.asset`

## 敌人与攻击

- Burstbug：`FPG_Burstbug_Enemy/Behavior/Attack*.asset`
- Hudie：`FPG_Hudie_Enemy/Behavior/Attack.asset`
- Luan：`FPG_Luan_Enemy/Behavior/Attack_Summon.asset`
- 召唤目标：`FPG_Luan_Attack_Summon.asset` 的本地 Summon 载荷槽引用 Hudie 敌人定义
- Entity：`Presentation/FormalEncounter/PF_FPG_{Burstbug,Hudie,Luan}Entity.prefab`

旧 D0Slice Definition、Training、Stage、Scenario 和 prefab 不再是迁移源。

## 波次规则

- 每个 Override 只描述种子、深度、难度、预算、波次和固定布局。
- 波次引用正式 enemy ID；同屏容量必须不超过 pool、roster 和 projectile 容量，普通波次与使用房间点位的召唤还必须满足 room spawn 容量。
- 固定布局仍需通过 room marker role 预检。
- `room-forest` 是当前默认房间稳定 ID，不使用 `room-combatlab-forest`。

## Luan 召唤

- `FpgEnemyAttackDefinition -> 本地 Summon Payload Slot -> FpgEnemyDefinition` 是唯一配置链。
- `FPG_Luan_Attack_Summon.asset` 的召唤载荷槽配置为 `ReplaceOwner + OwnerPosition`，两个召唤数量字段为 `0`；独立 `SelfDestructOwner` 节点在 Tick 71 空绑定执行。
- 240 tick 到达攻击 Ready 后开始 `die&broken` 演出；Tick 44 提交召唤，Tick 71 才按自毁节点配置结束 Luan。
- 释放 tick 构造稳定 summon request；不检查每个 owner/整场召唤数量、房间同屏数量、Cap Weight，也不选择或占用 room spawn point。
- Unity entity port 在 Luan 仍存活时读取其实体根节点世界 Pose，并立即从预热池 Prepare Hudie，因此随后死亡不会改变 Hudie 的出生位置。
- Hudie 通过 roster 与统一 Spawn Queue 成功入队并返回 `Queued` 后提交召唤成功事件；当前空绑定的 `SelfDestructOwner` 不等待该结果，在自身 Tick 通过正式 `ForceDeath -> EnemyDied -> EncounterRuntime.MarkEnemyDead` 链路结束 Luan。
- Retry、拒绝或静态上限不会门控当前空绑定自毁；需要“入队成功后才死亡”时应在配置中显式绑定召唤事件。不得按 Luan/Hudie ID 绕过通用事务；固定池、队列、hitbox、递归和稳定 ID 容量由同一召唤图预检，其中 L1 Challenge 当前为 11 个计划实体、1 次替换、12 个 roster 生命周期槽、7 个按类型与最大同时需求预热的实体池槽，房间点位需求仍为 2。

## 验证

- 用 Formal Encounter Preview 检查四套 Override 的三波结果、预算、spawn role 与 digest。
- 检查三种敌人的正式 prefab、SkeletonAnimation 和所有动画键。
- 检查 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs` 和 `FpgMultiEnemyCombatTransactionTests.cs`。
