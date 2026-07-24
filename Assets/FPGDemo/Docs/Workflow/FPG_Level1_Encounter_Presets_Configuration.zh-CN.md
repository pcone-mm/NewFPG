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
- 召唤目标：`FPG_Luan_SummonHudie.asset`
- 运行映射：`FPG_NormalRoom_AttackRuntimeCatalog.asset`
- Entity：`Presentation/FormalEncounter/PF_FPG_{Burstbug,Hudie,Luan}Entity.prefab`

旧 D0Slice Definition、Training、Stage、Scenario 和 prefab 不再是迁移源。

## 波次规则

- 每个 Override 只描述种子、深度、难度、预算、波次和固定布局。
- 波次引用正式 enemy ID；同屏容量必须不超过 pool、roster、projectile 和 room spawn 容量。
- 固定布局仍需通过 room marker role 预检。
- `room-forest` 是当前默认房间稳定 ID，不使用 `room-combatlab-forest`。

## Luan 召唤

- `FpgEnemyAttackDefinition -> FpgSummonActionDefinition -> FpgEnemyDefinition` 是唯一配置链。
- summon delay 必须映射到正式 telegraph/windup；延迟结束前不调用 sink、不杀死 Luan。
- sink 返回 `Queued` 后才提交 Hudie 召唤和 Luan 死亡。
- Retry/Rejected 保留 Luan；不得按敌人 ID 绕过通用事务。

## 验证

- 用 Formal Encounter Preview 检查四套 Override 的三波结果、预算、spawn role 与 digest。
- 检查三种敌人的正式 prefab、SkeletonAnimation 和所有动画键。
- 检查 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs` 和 `FpgMultiEnemyCombatTransactionTests.cs`。
