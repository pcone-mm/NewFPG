# FPGDemo 关卡 1 刷怪预设配置

## 目标与适用范围

本配置用已导入的 Burstbug、蝴蝶（Hudie）和陆鸾（Luan）组成四套关卡 1 固定三波遭遇，供 `Formal Encounter` 预览和正式关卡运行请求复用。它定义正式敌池、攻击运行时映射、波次、预算、时序和同屏上限，不修改 Room，不接回旧 D0 Stage 或 CombatLab 宿主。

通用规则、确定性计划、预热和失败边界见 [FPG_Formal_Encounter_Configuration.zh-CN.md](FPG_Formal_Encounter_Configuration.zh-CN.md)。

## 配置入口与资产位置

执行菜单 `FPG Demo > Formal Encounter > Install Burstbug Luan Hudie Defaults`。安装器可重复执行，并创建或刷新下列资产：

| 用途 | 资产位置 |
|---|---|
| 关卡 1 专用敌池 | `Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_EnemyPool.asset` |
| 四套预设共用 Profile | `Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_Profile.asset` |
| 入门预设 | `Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_01_Intro.asset` |
| 混合预设 | `Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_02_Mixed.asset` |
| 远程压制预设 | `Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_03_RangedPressure.asset` |
| 挑战预设 | `Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_04_Challenge.asset` |

## 引用关系

```text
Burstbug / Hudie / Luan EnemyDefinition + formal Entity View
  -> FPG_L1_01_EnemyPool
  -> FPG_L1_01_Profile
  + 其中一个 FPG_L1_01_* Override
  -> FpgRoomRunRequest
  + RoomDefinition / RunContext
  -> FpgEncounterPlan -> FpgEncounterSession -> FpgRoomEncounterDirector
```

Profile 和 Override 必须成对选择。四个 Override 都只适配这套固定三波 Profile；不要改用默认 `FPG_NormalRoom_Profile`，否则其加权布局可能产生额外波次。Encounter 不反向写入 Room，正式运行时由请求或 Host 组合两者；不得改成 D0 Stage，也不得按敌人 ID 硬编码刷怪。

## 四套预设

表中数量只统计计划内敌人。Luan 的召唤行为可额外召唤 1 只 Hudie；召唤请求成功进入统一 Spawn Queue 后，Luan 才通过正式死亡流程结束。召唤物仍受同一同屏上限、点位和对象池约束；容量或点位不足时请求按 Tick 重试，Luan 在成功前保持存活。

| 预设 | 锁定预算 | 第 1 波 | 第 2 波 | 第 3 波 | 体验定位 |
|---|---:|---|---|---|---|
| 01 入门 | 12 | Burstbug x2 | Burstbug x1 + Hudie x1 | Luan x1 | 先认识 Burstbug，再引入远程敌人与召唤者 |
| 02 混合 | 18 | Burstbug x2 + Hudie x1 | Burstbug x1 + Hudie x2 | Luan x1 + Burstbug x1 | 逐步提高近远程混合密度，末波处理召唤压力 |
| 03 远程压制 | 18 | Burstbug x1 + Hudie x2 | Hudie x3 | Luan x1 + Hudie x1 | 以远程密度为主，保留 Burstbug 作为开场近战干扰 |
| 04 挑战 | 24 | Burstbug x3 + Hudie x1 | Burstbug x2 + Hudie x2 | Luan x1 + Hudie x2 | 三类敌人完整组合，用于关卡 1 高压或支线版本 |

每一波的敌人费用分别相等；共享的 `3334 / 3333 / 3333` 份额把整数余数留给前两波累计，可让 12、18、24 三种锁定预算都精确三等分，不产生误导性的预算超支或裁剪诊断。

## Prefab、攻击与表现映射

安装器只在编辑器阶段读取下列已导入资产，并把所需字段复制到正式 `FPG_*` 资产。正式运行时不读取这些 D0 定义：

| 迁移来源 | 用途 |
|---|---|
| `Assets/FPGDemo/Presentation/D0Slice/Spine/PF_D0_BurstbugEntity.prefab` | 生成 `PF_FPG_BurstbugEntity.prefab`，保留 Spine、Body/Weakpoint、Gameplay/Projectile/Weakpoint 锚点 |
| `Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab` | 生成 `PF_FPG_HudieEntity.prefab`，保留 Spine 与命中锚点 |
| `Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab` | 生成 `PF_FPG_LuanEntity.prefab`，保留 Spine 与命中锚点 |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Presentation.asset` | 复制 `normal_enter / normal_idle / normal_death` 状态动画 |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Behavior.asset` | 显式映射为 Patrol、5/1.4 速度与 `stopDuringAttack = true` |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Training.asset` | 仅在编辑器推导三招首次 Ready 与重复间隔，不作为运行时 Stage |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Fast.asset` | Fast：24/12/30 Tick、28 伤害、Projectile 301、36/51 Tick、`normal_skill1` |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Volley.asset` | Volley：48/18/30 Tick、3 x 12 伤害、可拦截 HP 4、Projectile 302、120/135 Tick、`normal_skill2` |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_HeavyBreak.asset` | Heavy：90/45/30 Tick、TimedImpact 120 伤害、Presentation 3、`normal_skill2` |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Presentation.asset` | 复制 `appear / idle / die` 状态动画 |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Presentation.asset` | 复制 `idle / idle / die&broken` 状态动画 |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/Attacks/D0_Hudie_Attack_Bullet.asset` | 复制单弹、28 伤害、401 定义、36/51 Tick 与 `attack` 动画 |
| `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_SummonHudie.asset` | 复制 240/284 Tick 召唤时序与陆鸾 `die&broken`；正式 Attack 另配置成功召唤后死亡，蝴蝶 `appear` 来自上方 Hudie presentation |

`FpgEnemyEntityView` 在正式敌人配置校验时检查 SkeletonData 以及 entry、idle、death、attack 动画是否真实存在；实体绑定时播放 entry。Burstbug 三招相对该实例激活 Tick 在 120/300/540 首次 Ready，并各以 660 Tick 重复；非召唤 Threat 按 `RuntimeId + SpawnSequence + AttackPatternId` 在 telegraph 结束时播放攻击动画，Groggy 或死亡会取消尚未播放的动作，弱点击破若发生在 windup 内还会中断已开始的攻击 one-shot 并回到 Idle。召唤表现仍在动作开始时播放，`telegraphTicks + windupTicks` 到期后提交既有 SpawnQueue；Luan 在第 240 Tick 启动 `die&broken`，44 Tick 后释放召唤，只有 Spawn Queue 返回 `Queued` 才发布死亡生命值、清理攻击/威胁并发出 `EnemyDied`。该路由不包含 Burstbug、Hudie 或 Luan ID 特判。

正式 Attack 资产会保存模型 Spine 动画和 `warningSlot`；当前 Formal runtime 实际消费攻击动画与 Attack Runtime Catalog 的 Presentation Key，`warningSlot` 尚无正式消费者。Burstbug D0 Skill1/Skill2 独立 VFX prefab 与源音频字段也尚未迁入正式池/端口，不能作为正式 VFX 或音频已完成的依据。实体死亡后仍按既有池合同立即释放，因此 `deathAnimation` 已接入并参与校验，但不会停留展示完整死亡动画。

Burstbug 的 Patrol、5/1.4 速度与 `stopDuringAttack` 已进入正式 Behavior 资产，但当前 Formal Director 尚未消费行为移动字段；它们目前是配置/校验元数据，不代表正式移动控制器已经接入。


## 关键字段说明

| 配置组 | 中文名称 | 字段名 | 类型/单位 | 本配置值 | 生效条件与实际效果 | 常见误配 |
|---|---|---|---|---|---|---|
| Enemy Pool | 最小深度 | `entries[].minDepth` | int / 层 | 三种敌人均为 0 | 关卡 1 在 depth 0 也能生成 Luan | 复用默认 Pool 会因 Luan 的最小深度为 1 而静默裁剪固定项 |
| Burstbug Attack | 三招节奏 | `firstReadyOffsetTicks / cooldownTicks` | int / Tick | 120/300/540；均为 660 | 相对每个 Burstbug 激活 Tick 独立排程 | Groggy 会取消未释放 Threat；恢复后多个逾期 pattern 仍可能靠得较近，需试玩判断 |
| Luan Attack | 召唤成功后的施法者结果 | `summonOwnerOutcome` | enum | `DieAfterSuccessfulSummon` | 仅在召唤条目成功入队后，通过正式死亡事务结束 Luan；重试或失败不死亡 | 非 Summon 攻击配置该值会校验失败；不要从动画名推断死亡 |
| Luan Summon | 单施法者召唤上限 | `maxSummonsPerOwner` | int / 只 | 1 | 与成功后死亡策略配套，单个 Luan 最多成功召唤一次 | `DieAfterSuccessfulSummon` 下不等于 1 会校验失败 |
| Enemy Pool | 每房 Luan 上限 | `entries[].maxPerRoom` | int / 只 | 1 | 四套预设最多放入一个 Luan | 低于固定数量时计划会裁剪；提高后要重新计算召唤上界 |
| Profile | 加权波次布局 | `weightedWaveLayouts` | basis points | 单一三波；3334/3333/3333 | 强制四套预设都只生成三波，并正确分配整数余数 | 混用默认 1/2/3 波布局会让固定配置出现额外空波 |
| Profile | 同屏实体上限 | `maxConcurrentEntities` | int / 只 | 2 | 第 3 个计划敌人等待空位后作为增援出现 | 大于 Room 点位容量会在开战前预检失败 |
| Profile | 同屏权重上限 | `maxConcurrentCapWeight` | int / 权重点 | 3 | 允许 Luan（2）与一只普通敌人（1）同屏 | 小于 3 会让 Luan 与普通敌不能同时准备 |
| Profile | 生成间隔 | `spawnIntervalTicks` | int / Tick | 18 | 同一波连续准备敌人的最小间隔 | 只改变入队节奏，不绕过同屏上限 |
| Profile | 生成预警 | `warningDurationTicks` | int / Tick | 30 | 每只敌人激活前占点并显示预警 | 预警中的敌人也占用实体与点位容量 |
| Profile | 波间隔 | `waveIntervalTicks` | int / Tick | 60 | 当前波全部清空后等待再开始下一波 | 它不是从最后一只敌人生成时开始计时 |
| Override | 固定波次模式 | `mode` | enum | `FixedWaves` | 只按 `fixedSpawns` 生成敌人 | 固定敌人仍受 Pool 深度和每波/每房上限过滤 |
| Override | 锁定预算 | `lockBudget` / `lockedBudget` | bool + int / 预算点 | true；12/18/18/24 | 让计划预算与固定敌人费用一致 | 不锁定时深度和难度只改变诊断，不改变固定数量 |
| Override | 固定敌人 | `fixedSpawns` | 波次、敌人、数量 | 见上表 | `waveIndex` 从 0 开始，对应表中的第 1/2/3 波 | 引用不在专用 Pool 内的敌人会被裁剪 |

## 制作和预览步骤

1. 执行安装菜单，确认 Project 窗口出现 `FormalEncounter/Level1` 目录。
2. 打开 `FPG Demo > Room Editor`，展开 `Formal Encounter Preview`。
3. Profile 选择 `FPG_L1_01_Profile`，Override 选择四套预设之一。
4. 关卡 1 基准预览使用 `Depth = 0`、`Difficulty = 10000`；Seed 可以变化，但固定敌人和波次不应变化。
5. 检查预览为三波，且每波敌种和数量与上表完全一致；不应出现 `fixed enemy unavailable`、`clipped` 或额外空波。
6. 正式接入时，让关卡运行请求或唯一的 `FpgEncounterHost` 引用同一对 Profile/Override；不要把 Encounter 引用写进 RoomDefinition。

## 当前关卡前置条件

命名上的关卡 1 资产 `Assets/FPGDemo/Config/Level/Rooms/L1_01.asset` 仍是空 Room：没有环境、主分组、玩家入口、敌人出生点或出口。它是占位资产，不是当前运行时的 Room。

当前实际 Formal 关卡链路是 `Boot -> FormalRoom -> room-combatlab-forest`。`FPG Demo > Formal Encounter > Install Boot Formal Room Loop` 已为该 Room 配置一个出口、4 个 `Any` 敌人出生点，并在 `FormalRoom.unity` 安装唯一 Host、Director、实体池、Catalog、攻击运行时 Catalog 和表现端口。四套预设在 depth 0 已通过完整 Plan/Preflight；场景默认绑定 `FPG_L1_01_01_Intro`。

切换到其他预设时，在 Unity Inspector 中选中 `FormalRoom` 场景的 `FpgEncounterHost`，只替换 `encounterOverride` 为 `FPG_L1_01_02_Mixed`、`03_RangedPressure` 或 `04_Challenge`，保存场景后再 Play。不要手改 Room YAML，也不要把 Override 写进 RoomDefinition。

如果后续要把空壳 `L1_01.asset` 改成独立正式 Room，仍需补齐环境/主分组、玩家入口、至少两个兼容出生点和一个出口，并在独立 Formal 场景完成同样的 Host/Director/Pool/Catalog/端口配置。

## 验收与交接

最小技术检查：

- NormalRoom/L1 的 Enemy Pool 与 Enemy Catalog 均为 3 项、Attack Runtime Catalog 为 5 项，三个 EnemyDefinition `TryValidate` 成功，四个 Override `TryBuildData` 成功；
- depth 0 生成的四份 Plan 均恰好三波，敌种和数量与表格一致且无裁剪；
- NormalRoom 与四个 L1 Override 均正式引用 Burstbug/Hudie/Luan，Burstbug 不再使用旧占位攻击/动画，运行时没有 D0 Stage、D0 Encounter 或敌人 ID 特判；
- Burstbug/Hudie/Luan prefab 均不包含旧 `Actor2DPresenter`，SkeletonData 和全部正式动画键通过校验，攻击开始事件能按运行时身份命中已绑定实体；
- 当前 FormalRoom 已执行 Preflight，确认出口、点位角色、同时容量、Catalog 和 Luan 召唤闭包全部通过；
- 重复执行安装菜单，资产 GUID 和引用关系保持稳定。

需要主管试玩判断：Burstbug Fast/Volley/Heavy 的预警与动作是否清晰、Volley 可拦截弹和 Heavy 弱点压力是否合适、Groggy 后逾期 pattern 是否显得过密、入门预设是否足够教学、Hudie 密度是否过高、Luan 240 Tick 的首次召唤时机是否清晰，以及挑战预设的增援时长是否适合作为关卡 1 高压版本。这些属于手感和难度判断，不能由静态校验替代。Burstbug 独立攻击 VFX/音频、死亡停留和正式移动控制仍需独立合同后再验收。
