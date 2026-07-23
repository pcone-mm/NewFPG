# FPG 正式普通战斗房 Encounter 配置

本文档定义正式普通战斗房 v1 的配置与失败边界。Room 只提供空间，Encounter 只提供战斗内容；二者由运行时请求组合，互不反向持有。

## 所有权与请求

`FpgRoomRunRequest` 固定组合 `RoomDefinition + EncounterProfile + EncounterOverride + RunContext`：

- `FpgRoomDefinition`：完整环境、出口、玩家入口和带角色的 SpawnPoint；
- `FpgEncounterProfile`：预算、波次、敌池、时序、距离规则和固定容量；
- `FpgEncounterOverrideDefinition`：固定波次、强制/排除敌种和可选预算锁定；
- `FpgEncounterRunContext`：`RunSeed`、`RegionId`、`Depth`、`DifficultyMultiplierBasisPoints`、`RoomVisitOrdinal`。

Encounter 不得存入 Room 或 RoomGroup。Room Editor 的正式预览和正式试玩参数使用内存覆盖，不执行 `SetDirty`，不写回 Room、Profile、Override 或场景。

## 确定性计划

完整计划在进入房间前一次生成。计划、主题敌人、波次选择和 SpawnPoint 选择使用独立随机域，并由 `DeterministicRandomV1/StableHash` 派生。相同资产内容及相同 `RunSeed + RegionId + Depth + DifficultyMultiplierBasisPoints + RoomVisitOrdinal` 必须生成相同 digest 和计划。

默认预算公式：

```text
max(minBudget, (baseBudget + depth * depthRamp) * difficultyMultiplier)
```

波次份额使用整数 basis points，合计必须为 `10000`。内置模板为 `10000`、`5000/5000`、`3000/1500/5500`。计划记录请求预算、实际消耗、超预算、裁剪和随机决策摘要。

正式 Profile 优先使用 `weightedWaveLayouts`：每个布局以稳定 `layoutId` 和正整数 `selectionWeight` 参与独立随机域抽取，布局内的每波份额必须合计 `10000`。选中的 `WaveLayoutId`、`BudgetShareBasisPoints`、请求预算和实际消耗都会写入 Plan 与 digest。仅当加权布局数组为空时，才从旧 `waveBudgetTemplate/customWaveShares` 生成单个兼容布局；旧字段不与加权布局同时参与抽取。

固定波次 Override 的最大 `waveIndex` 会先约束可选布局的波数；没有任何布局能容纳固定波次时直接拒绝。生成模式先按资格与深度过滤敌池，再在独立随机域抽取主题敌人并预留其预算，随后按敌种权重分配剩余预算；敌种数量按 `ceil(typeBudget / spawnCost)` 计算，并继续受每波/每房上限裁剪。

## 字段单位

| 配置 | 字段 | 单位/约束 |
|---|---|---|
| RunContext | `RunSeed` | 无符号 64 位种子 |
| RunContext | `Depth`、`RoomVisitOrdinal` | 非负整数 |
| RunContext | `DifficultyMultiplierBasisPoints` | basis points；`10000 = 1.0x` |
| Enemy | `life`、`breakValue` | 战斗整数值，必须为正 |
| Enemy | `spawnCost` | 预算点，必须为正 |
| Enemy | `capWeight` | 同屏权重点，必须为正 |
| Pool | `selectionWeight` | 无单位正整数权重 |
| Pool | `minDepth/maxDepth` | 包含端点的非负深度范围 |
| Pool | `maxPerWave/maxPerRoom` | 实例数量上限 |
| Profile | `baseBudget/depthRamp/minBudget` | 预算点 |
| Profile | 波次份额 | basis points，总和 `10000` |
| Profile | `weightedWaveLayouts[].layoutId/selectionWeight` | 唯一稳定 ID / 无单位正整数相对权重 |
| Profile | `maxConcurrentCapWeight` | 同屏权重点上限 |
| Profile | `maxConcurrentEntities` | 同屏实体数量上限 |
| Profile | `spawnIntervalTicks/warningDurationTicks/waveIntervalTicks/maxSpawnWaitTicks` | 战斗 Tick |
| Profile | `spawnSafetyDistanceUnits/entrySafetyDistanceUnits/softDistanceRelaxationStepUnits` | Unity 世界单位 |
| Profile | `softDistanceRelaxationAttempts` | 确定性重试次数 |
| Profile | 所有 `*Capacity` | 固定槽位数量，必须为正 |

## SpawnPoint 角色

SpawnPoint 只声明 `Any/Melee/Ranged/Support`，不绑定敌种。敌人只能使用同角色或 `Any` 点位；再按占用、玩家距离和入口距离过滤。软距离放宽只能改变距离阈值，不得绕过角色或占用约束。超过最大等待仍无兼容空闲点位时 Fail-Closed。

## 固定容量与预热

`Preparing` 必须按完整计划及召唤静态上限预热，并在开战前验证：

- `EnemyRosterCapacity` 与 `EntityPoolCapacity`；
- `HitboxCapacity`、`ThreatCapacity`、`ProjectileCapacity`；
- Attack Pattern/Schedule 容量：覆盖预计同屏 owner 的全部攻击模式，且不允许战斗中扩容；
- `WarningCapacity` 与 `OverheadHealthBarCapacity`；
- SpawnPoint、出口、角色匹配、Entity Binder 和稳定 Geometry ID。

容量不足、Prefab/Binder 缺失、同步失败或配置非法时必须在开战前 Fail-Closed。战斗期间禁止池扩容、隐式查找以及 `Instantiate/Destroy`。同种敌人的不同实例以 `SpawnEntryId + SpawnSequence + RuntimeId` 区分。 多实例 Geometry ID 必须由稳定 `SpawnSequence + HitPartOrdinal` 派生；重复、越界或回退到 `GetInstanceID`/Prefab 固定 ID 均视为准备失败。

## 通用召唤合同

`Summon` 是通用攻击动作，不得按 Luan 或其他敌人 ID 特判。召唤条目进入同一 Spawn Queue，使用同一点位筛选、同屏限制和池容量。

每个 Summon 配置必须同时声明并校验：

- `maxSummonsPerOwner`：单个召唤者实例的总次数；
- `maxTotalSummonsPerEncounter`：整个 Encounter 的总次数；
- `maxRecursionDepth`：嵌套召唤深度；
- `cooldownTicks`：同一 owner 的召唤间隔；
- 候选权重、空引用、重复 ID 和循环引用。

召唤攻击资产还必须声明 `summonOwnerOutcome`（Inspector 中文名“召唤成功后的施法者结果”）。它是 enum，默认值为 `RemainAlive`，只在 `kind = Summon` 时生效；`DieAfterSuccessfulSummon` 要求 `maxSummonsPerOwner = 1`，并且只在请求获得 Spawn Queue 的 `Queued` 确认后，通过正式死亡流程结束施法者。`RetryNextTick`、静态上限或拒绝均不触发死亡。

任一静态上限达到后停止入队；循环或超深图在 Preparing 阶段拒绝，不得靠运行时超时兜底。

## 生命周期与清理

正式流程为：Preparing → 锁出口 → 波次/逐条预警 → 激活 → 波次清空 → 下一波 → Cleared → 解锁出口并发出 `FpgRoomClearedEvent`。预警条目可以占用点位和容量，但在激活前没有 Hitbox、Threat、攻击或伤害。

- 暂停：不推进 Tick、攻击、投射物、预警、生成队列或租约；恢复后从原 Tick 继续；
- 重开：恢复进入房间时的玩家快照，清空实体、队列、点位占用、Threat、Projectile、血条、预警和 Anchor lease，再重新 Preparing；
- 失败/Fault：停止 Tick，锁定结果，不得继续部分运行；
- 场景卸载/Dispose：清空全部运行实体与端口状态，结束池战斗锁并释放预热实例；
- 多波之间：玩家生命、护盾、弹药和 Buff 连续保留。

死亡租约只保留冻结的 `LastPose + leaseTicks`，不得保留可被对象池复用的 Transform 或 GameObject 引用。

## 默认三敌与编辑器入口

执行菜单 `FPG Demo > Formal Encounter > Install Burstbug Luan Hudie Defaults`，生成 Burstbug、Hudie、Luan 的正式 Enemy/Behavior/Attack、Luan→Hudie Summon、三种正式 Entity Prefab、3 项 Enemy Pool/Catalog 与 5 项 `FpgFormalAttackRuntimeCatalog`。Burstbug 复用既有 Fast 正式 Attack GUID，并新增 Volley 与 HeavyBreak；Behavior、Presentation 和 120/300/540 首次 Ready、660 重复间隔仅由 installer 在编辑器阶段从已导入 D0 source 迁移。Installer 可重复执行，但不得修改旧 D0 Prefab 或 CombatLab 场景；正式运行仍只走 `FpgRoomRunRequest -> FpgEncounterPlan -> FpgEncounterSession -> FpgRoomEncounterDirector`。

打开 `FPG Demo > Room Editor`，在房间资料底部展开 `Formal Encounter Preview`：选择 Profile/Override，输入 Seed、Depth、Difficulty basis points 和 Room Visit Ordinal。预览显示 digest、选中布局、每波 basis-points 份额、请求/实际预算、敌种数量、并发估算和逐 SpawnPoint 角色兼容性；该操作只生成内存 Plan。

`Run in Active Formal Host` 只在 Play Mode 中查找已加载、非持久化且唯一的 `FpgEncounterHost`，通过内存覆盖提交与预览相同的四项请求，并校验运行时 Plan digest。缺少 Host、存在多个 Host、Host 启动失败或 digest 不一致时全部 Fail-Closed，绝不回退到 CombatLab。当前正式链为 `Boot -> FormalRoom -> room-combatlab-forest`；`FormalRoom` 已配置唯一 Host/Director、对象池、正式 Catalog/端口和表现根，默认使用 `FPG_L1_01_01_Intro`。

## D0 边界

旧 `BattleSession`、`CombatLab` 和单敌替换链是冻结合同，不作为正式 Encounter 验收基线。Installer 在编辑器阶段读取 D0 source 只用于迁移字段，不表示运行时回退到 D0 Stage、D0 Encounter 或硬编码刷怪。详见 [D0_Formal_Encounter_Extension_Contract.zh-CN.md](D0_Formal_Encounter_Extension_Contract.zh-CN.md)。
