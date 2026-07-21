# FPGDemo 关卡 1 刷怪预设配置

## 目标与适用范围

本配置用现有 Burstbug、Hudie、Luan 三种正式敌人组成四套关卡 1 固定三波遭遇，供 `Formal Encounter` 预览和后续关卡运行请求复用。它只定义敌池、波次、预算、时序和同屏上限，不修改 Room、场景或旧 D0 CombatLab 场景绑定。

通用规则、确定性计划、预热和失败边界见 [FPG_Formal_Encounter_Configuration.zh-CN.md](FPG_Formal_Encounter_Configuration.zh-CN.md)。

## 配置入口与资产位置

执行菜单 `FPG Demo > Formal Encounter > Install Burstbug Hudie Luan Defaults`。安装器可重复执行，并创建或刷新下列资产：

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
Burstbug / Hudie / Luan EnemyDefinition
  -> FPG_L1_01_EnemyPool
  -> FPG_L1_01_Profile
  + 其中一个 FPG_L1_01_* Override
  -> FpgRoomRunRequest
  + RoomDefinition / RunContext
```

Profile 和 Override 必须成对选择。四个 Override 都只适配这套固定三波 Profile；不要改用默认 `FPG_NormalRoom_Profile`，否则其加权布局可能产生额外波次。Encounter 不反向写入 Room，正式运行时由请求或 Host 组合两者。

## 四套预设

表中数量只统计计划内敌人。Luan 的召唤行为仍可额外召唤 Hudie，单个 Luan 最多召唤 2 只，召唤物受同一同屏上限、点位和对象池约束。

| 预设 | 锁定预算 | 第 1 波 | 第 2 波 | 第 3 波 | 体验定位 |
|---|---:|---|---|---|---|
| 01 入门 | 12 | Burstbug x2 | Burstbug x1 + Hudie x1 | Luan x1 | 先识别近战，再加入远程，最后单独认识召唤者 |
| 02 混合 | 18 | Burstbug x2 + Hudie x1 | Burstbug x1 + Hudie x2 | Luan x1 + Burstbug x1 | 近远程比例逐步反转，末波保留近战牵制 |
| 03 远程压制 | 18 | Burstbug x1 + Hudie x2 | Hudie x3 | Luan x1 + Hudie x1 | 强调远程目标处理和对召唤者的优先级判断 |
| 04 挑战 | 24 | Burstbug x3 + Hudie x1 | Burstbug x2 + Hudie x2 | Luan x1 + Hudie x2 | 更长的增援波次，用于关卡 1 高压或支线版本 |

每一波的敌人费用分别相等；共享的 `3334 / 3333 / 3333` 份额把整数余数留给前两波累计，可让 12、18、24 三种锁定预算都精确三等分，不产生误导性的预算超支或裁剪诊断。

## 关键字段说明

| 配置组 | 中文名称 | 字段名 | 类型/单位 | 本配置值 | 生效条件与实际效果 | 常见误配 |
|---|---|---|---|---|---|---|
| Enemy Pool | 最小深度 | `entries[].minDepth` | int / 层 | 三种敌人均为 0 | 关卡 1 在 depth 0 也能生成 Luan | 复用默认 Pool 会因 Luan 的最小深度为 1 而静默裁剪固定项 |
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

- Enemy Pool `TryValidate` 成功，Profile `TryBuildData` 成功，四个 Override `TryBuildData` 成功；
- depth 0 生成的四份 Plan 均恰好三波，敌种和数量与表格一致且无裁剪；
- 当前 FormalRoom 已执行 Preflight，确认出口、点位角色、同时容量、Catalog 和 Luan 召唤闭包全部通过；
- 重复执行安装菜单，资产 GUID 和引用关系保持稳定。

需要主管试玩判断：入门预设是否足够教学、远程压制是否过密、Luan 出现时机是否清晰、挑战预设的增援时长是否适合作为关卡 1 高压版本。这些属于手感和难度判断，不能由静态校验替代。
