# D0 Fei 右键副攻配置

## 目标与适用范围

本配置用于 D0 CombatLab 中 Fei 的鼠标右键副攻：每次右键按下即提交一次独立副攻，成功释放时消耗 2 发共享弹匣弹药，采用比左键主射更大的范围查询，并播放 Spine 动画 `defense_play`。右键按住仍保留瞄准状态，但一次按下只触发一次副攻，不会按住连发。

当前标准左键主射在普通部位完整命中为 8 颗弹丸 × 4 伤害 = 32；右键副攻对单个普通部位的基础生命伤害为 28，因此单目标伤害略低，但可通过 3.0 Unity 世界单位的范围查询影响最多 4 个目标。本文只覆盖 Fei 的 D0 右键副攻配置与表现，不改变通用 `WeaponRuntime` 的蓄力协议，也不开放 LayerMask、查询缓冲区、输入帧采样或攻击提交顺序等技术参数。

## 配置入口与资产位置

- 战斗总入口：`Assets/FPGDemo/Config/BattleScenarioConfig.asset`
- D0 遭遇：`Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsBurstbug.asset`
- Fei 角色：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei.asset`
- 武器数值：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Weapon.asset`
- 攻击范围：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_CombatFeel.asset`
- 动画、技能 Socket 与瞬时表现：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Presentation.asset`
- 主射 / 副射表现完整字段合同：[D0 Fei 主射 / 副射表现配置](D0_Fei_Attack_Presentation_Configuration.zh-CN.md)
- 输入绑定：`Assets/InputSystem_Actions.inputactions` 的 `Battle/Secondary`；当前绑定为 `<Mouse>/rightButton`，`Battle/Aim` 继续共用右键
- 兼容回退：`Assets/FPGDemo/Config/BattleScenarioConfig.asset` 与 `Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset`

已安装的 D0 表现从 `Assets/FPGDemo/Presentation/D0Slice/Spine/D0_Fei_30048_StraightAlpha_SkeletonData.asset` 读取 Fei 动画。源动画 `defense_play` 可在 `Assets/Imported/CZN/Fei_30048/SpineSource/model/30048.json` 审计，但运行时配置不要直接改为引用 Imported/CZN 源文件。

## 引用关系

`BattleScenarioConfig → D0_CombatLab_FeiVsBurstbug → D0_Fei → D0_Fei_Weapon / D0_Fei_Presentation`

`D0_CombatLab_FeiVsBurstbug → D0_Fei_CombatFeel`

运行时由战斗配置创建领域 `WeaponDefinition` 和攻击查询设置；成功提交的副攻事件再由 D0 表现路由转交 `Actor2DPresenter` 播放动画，并由 `PlayerWeaponPresentationController` 从当前玩家表现资产读取技能 Socket、枪口、弹道和蓄力参数。视觉起点来自 Fei 派生 Prefab 的 `PrimaryMuzzle`，释放终点只来自已提交冻结轨迹，蓄力深度来自当前活动敌人锚点。已配置 authored scenario 时，以 D0 定义资产为主；兼容回退资产只服务于旧配置或无 authored scenario 的路径，修改主资产时应同步维护对应回退值，避免两条路径表现不一致。

`defense_play` 只负责角色表现。命中目标、范围查询、伤害、削韧和 2 发弹药扣除都由战斗域在攻击提交时结算，不由 Spine 动画事件、HIT 标记或动画长度触发。

## 制作步骤

1. 打开 `D0_CombatLab_FeiVsBurstbug.asset`，确认玩家引用 `D0_Fei.asset`，且其武器与表现分别引用 `D0_Fei_Weapon.asset`、`D0_Fei_Presentation.asset`。
2. 在 `D0_Fei_Weapon.asset` 的副射分组设置弹药消耗、最低蓄力、伤害倍率和最大命中数。即时右键攻击必须将 `secondaryMinimumChargeTicks` 设为 0。
3. 在 `D0_Fei_CombatFeel.asset` 将 `secondaryAreaRadius` 设为 3.0。该数值是命中查询半径，不是特效缩放。
4. 在 `D0_Fei_Presentation.asset` 将释放动画设为 `defense_play`，HIT/STOP 表现标记设为 0.033 秒和 1.0 秒；确认副射 `shot.sourceSocket = PrimaryMuzzle`、`targetDepthAnchor = ActiveEnemyGameplay`，并按主 / 副射表现字段合同核对枪口、弹道、蓄力和目标爆发参数。
5. 确认 `Battle/Secondary` 与 `Battle/Aim` 均绑定鼠标右键。不要新建并行 Action，也不要更换已有 Action/Binding ID。
6. 同步兼容回退的 `BattleScenarioConfig.asset` 和 `CombatPresentationProfile.asset`，执行 D0 安装器重建 Fei 派生 Prefab 的 Socket；确认场景中没有旧 `FeiMuzzleVisualAnchor` 或 `D0SecondaryTargetProxy`。
7. 保存资产，等待 Unity 编译与资源刷新完成，检查 Console 不存在相关错误。试玩手感、范围可读性和动画区分度交由主管按下表验收。

## 字段说明

| 配置组 | 中文名称 | 字段名 | 类型/单位 | 标准值与范围 | 生效条件与实际效果 | 常见误配 |
|---|---|---|---|---|---|---|
| 武器 | 副射弹药消耗 | `secondaryAmmoCost` | int / 发 | 2；1 到弹匣容量 | 攻击成功提交时从共享弹匣原子扣除 2 发 | 当成每目标消耗；实际一次攻击只扣一次 |
| 武器 | 副射最低蓄力 | `secondaryMinimumChargeTicks` | int / Tick | 0；大于等于 0 | 0 允许按下与释放在同一 Tick 成功提交，形成即时右键攻击 | 设为正数会恢复蓄力门槛，右键即时路径将无法按预期提交 |
| 武器 | 副射恢复时长 | `secondaryRecoveryTicks` | int / Tick | 30；大于 0 | 成功释放后锁住共享武器，结束前不能开始主射、副射或换弹 | 当成动画时长；它属于战斗状态时长 |
| 武器 | 副射生命伤害 | `secondaryDamage` | int / 生命值 | 28；大于等于 0 | 每个普通部位命中目标受到 28 基础生命伤害，低于完整左键主射的 32 | 与 8 颗主射弹丸逐颗伤害直接比较 |
| 武器 | 副射削韧伤害 | `secondaryBreakDamage` | int / 韧性值 | 20；大于等于 0 | 每个命中目标按战斗域规则削减韧性 | 误认为与生命伤害共用同一倍率 |
| 武器 | 副射弱点生命倍率 | `secondaryWeakpointDamageMultiplierBasisPoints` | int / 万分比 | 12000（1.2×）；大于等于 0 | 弱点生命伤害在 28 基础值上应用 1.2 倍 | 填 120 或 1.2；字段要求万分比 |
| 武器 | 副射弱点削韧倍率 | `secondaryWeakpointBreakMultiplierBasisPoints` | int / 万分比 | 25000（2.5×）；大于等于 0 | 弱点削韧在基础削韧值上应用倍率 | 与弱点生命倍率混用 |
| 武器 | 副射最大命中数 | `secondaryMaxImpactCount` | int / 个 | 4；大于 0 | 一次副攻最多提交 4 个目标结果 | 当成当前场景支持 4 个敌人的承诺 |
| 战斗手感 | 副射范围半径 | `secondaryAreaRadius` | float / Unity 世界单位 | 3.0；大于 0 | 先做直线命中，再以该半径执行范围查询；范围大于左键弹丸命中 | 用特效尺寸代替查询半径，导致画面与判定不一致 |
| 表现 | 副射释放动画 | `secondaryReleaseAnimation` | Spine 动画名 / string | `defense_play`；非空且必须存在 | 已提交的右键副攻单次播放该动作，播完回待机，不追加旧 `u4_attack_end` | 填入源文件名、皮肤名或未进入 D0 SkeletonData 的动画 |
| 表现 | 副射 HIT 标记时间 | `secondaryHitMarkerTime` | float / 秒 | 0.033；大于等于 0 | 用于表现时序审计与收束间隔，不触发命中或伤害 | 当成战斗域命中帧 |
| 表现 | 副射 STOP 标记时间 | `secondaryStopMarkerTime` | float / 秒 | 1.0；不早于 HIT | 与 HIT 的差值参与表现收束；不改变恢复、弹药或伤害 | 设得早于 HIT，导致表现配置校验失败 |
| 表现 | 副射发射 Socket | `secondarySkillPresentation.shot.sourceSocket` | enum | `PrimaryMuzzle` | 从 Fei 派生 Prefab 的稳定挂点发出枪口与弹道表现 | 在关卡中新建临时 Transform |
| 表现 | 蓄力目标深度来源 | `secondarySkillPresentation.targetDepthAnchor` | enum | `ActiveEnemyGameplay` | 使用当前活动敌人主体深度投影自由准星 | 当成战斗锁定目标 |
| 表现 | 副射弹道时长 / 宽度 | `shot.tracerDuration / tracerWidth` | 秒 / 世界单位 | 0.36 / 0.12；大于 0 | 控制已提交轨迹的短暂显示 | 当成投射物速度或命中宽度 |
| 表现 | 目标爆发半径换算 | `targetBurstRadiusScale / Min / Max` | float | 0.32 / 0.42 / 1.4 | 只把已提交范围换算为目标局部 VFX 尺寸 | 反向修改范围查询 |

完整枪口颜色、长度、亮度及主射对应字段见 [D0 Fei 主射 / 副射表现配置](D0_Fei_Attack_Presentation_Configuration.zh-CN.md)。

`secondaryChargeAnimation` 与 `secondaryEndAnimation` 继续保留给通用蓄力/取消配置。当前 Fei 的 0 Tick 右键成功路径不会呈现蓄力段，成功释放后也不会追加结束动画；不要为了本次攻击删除这些兼容字段。

## 示例配置与预期表现

标准样例：

- `secondaryAmmoCost = 2`
- `secondaryMinimumChargeTicks = 0`
- `secondaryDamage = 28`
- `secondaryWeakpointDamageMultiplierBasisPoints = 12000`（1.2×）
- `secondaryMaxImpactCount = 4`
- `secondaryAreaRadius = 3.0`
- `secondaryReleaseAnimation = defense_play`
- `secondaryHitMarkerTime = 0.033`
- `secondaryStopMarkerTime = 1.0`
- `secondarySkillPresentation.shot.sourceSocket = PrimaryMuzzle`
- `secondarySkillPresentation.targetDepthAnchor = ActiveEnemyGameplay`
- `secondarySkillPresentation.shot.tracerDuration = 0.36`
- `secondarySkillPresentation.shot.tracerWidth = 0.12`

弹匣至少有 2 发且武器处于可用状态时，按下一次鼠标右键会在同一 Tick 开始并提交副攻，扣除 2 发弹药，对直线命中及 3.0 半径范围内最多 4 个有效目标提交结果。普通部位每目标基础生命伤害为 28，弱点生命倍率为 1.2×；角色单次播放 `defense_play` 后回到 `b_idle`。继续按住右键只维持瞄准，不会自动重复副攻。

## 验收与交接

最小技术检查包括：三份 D0 主配置与兼容回退值一致；`Battle/Secondary` 绑定鼠标右键；`defense_play` 存在于 Fei D0 SkeletonData；Unity 编译完成且 Console 无相关错误。以下体验项未由 Agent 试玩，状态统一为“待主管试玩/确认”。

| 编号 | 测试项 | 前置条件 | 主管操作 | 通过标准 | 证据/记录栏 | 状态 | 备注/风险 |
|---|---|---|---|---|---|---|---|
| H-01 | 即时触发与弹药消耗 | CombatLab；Fei 弹匣至少 4 发；武器处于 Ready | 单击一次右键并观察弹药 | 按下即播放副攻并只提交一次；弹药恰好减少 2 发 | 试玩人、日期、录屏/问题记录 | 待主管试玩/确认 | 右键同时承担 AimHeld |
| H-02 | 按住不连发与瞄准共存 | 同 H-01 | 按住右键约 2 秒后松开 | 只在按下瞬间提交一次副攻；按住期间仍可瞄准，不发生自动连发 | 录屏/输入记录 | 待主管试玩/确认 | — |
| H-03 | 与左键伤害区分 | 对同一普通部位可稳定完整命中；记录敌人生命变化 | 分别完整命中一次左键主射和一次右键副攻 | 左键完整普通命中为 32，右键单目标基础伤害为 28；右键没有反超 | 数值记录/录屏 | 待主管试玩/确认 | 散布或漏弹会影响左键对比，须确认 8 颗均命中 |
| H-04 | 范围与多目标上限 | 准备能覆盖范围边缘和多个有效目标的验收布置 | 在目标附近使用一次右键副攻 | 3.0 世界单位内可产生比左键更宽的命中覆盖，且单次不超过 4 个目标 | 场景布置、录屏、问题记录 | 待主管试玩/确认 | 当前标准 D0 为单敌人，必要时使用主管认可的验收布置 |
| H-05 | 动画区分与回待机 | Fei 使用 D0 派生 SkeletonData | 交替执行左键主射和右键副攻 | 右键清晰播放 `defense_play`，与左键 `attack_play1/2` 可区分；动作结束回 `b_idle`，不追加 `u4_attack_end` | 录屏/问题记录 | 待主管试玩/确认 | 动作可读性属于主管判断 |
| H-06 | 弹药不足 | 弹匣仅剩 0 或 1 发 | 单击右键 | 不提交右键副攻、不扣成负数，也不播放已提交攻击的释放表现 | 录屏/弹药记录 | 待主管试玩/确认 | — |
