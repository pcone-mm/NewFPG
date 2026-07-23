# D0 Fei 右键副攻配置

## 目标与适用范围

本配置用于 FormalRoom 正式战斗中 Fei 的鼠标右键副攻：右键按下立即尝试提交一次，持续按住时在 `secondaryRecoveryTicks` 结束后自动再次尝试，松开只停止后续攻击。每次成功提交消耗 2 发共享弹匣弹药，并播放 Spine 动画 `defense_play`；弹药不足时不提交，也不自动换弹。CombatLab 只保留旧功能参考与回归用途。

当前标准左键主射在普通部位完整命中为 8 颗弹丸 × 4 伤害 = 32；右键副攻只产生范围爆炸，不产生爆心直击伤害。射线遇到目标、掩体或最大射程时确定爆心，3.0 Unity 世界单位内最多结算 4 个敌人和 4 个可拦截弹体；爆炸从爆心到范围目标不再做遮挡检查。本文只覆盖 Fei 的 D0 右键副攻配置与表现，不开放 LayerMask、查询缓冲区、输入帧采样、固定池容量或攻击事务顺序等技术参数。

## 配置入口与资产位置

- 战斗总入口：`Assets/FPGDemo/Config/BattleScenarioConfig.asset`
- FormalRoom 玩家入口：`Assets/FPGDemo/Config/FormalEncounter/FPG_PlayableCharacterCatalog.asset`
- CombatLab 旧参考：`Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsBurstbug.asset`
- Fei 角色：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei.asset`
- 武器数值：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Weapon.asset`
- 攻击范围：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_CombatFeel.asset`
- 动画、技能 Socket 与瞬时表现：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Presentation.asset`
- 主射 / 副射表现完整字段合同：[D0 Fei 主射 / 副射表现配置](D0_Fei_Attack_Presentation_Configuration.zh-CN.md)
- 输入绑定：`Assets/InputSystem_Actions.inputactions` 的 `Battle/Secondary`；当前绑定为 `<Mouse>/rightButton`，`Battle/Aim` 继续共用右键
- 兼容回退：`Assets/FPGDemo/Config/BattleScenarioConfig.asset` 与 `Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset`

已安装的 D0 表现从 `Assets/FPGDemo/Presentation/D0Slice/Spine/D0_Fei_30048_StraightAlpha_SkeletonData.asset` 读取 Fei 动画。源动画 `defense_play` 可在 `Assets/Imported/CZN/Fei_30048/SpineSource/model/30048.json` 审计，但运行时配置不要直接改为引用 Imported/CZN 源文件。

## 引用关系

`FPG_PlayableCharacterCatalog → D0_Fei / D0_Fei_3C / D0_Fei_CombatFeel`

`D0_Fei → D0_Fei_Weapon / D0_Fei_Presentation`

运行时由战斗配置创建领域 `WeaponDefinition` 和攻击查询设置；成功提交的副攻事件再由 D0 表现路由转交 `Actor2DPresenter` 播放动画，并由 `PlayerWeaponPresentationController` 从当前玩家表现资产读取技能 Socket、枪口、弹道和蓄力参数。视觉起点来自 Fei 派生 Prefab 的 `PrimaryMuzzle`，释放终点只来自已提交冻结轨迹，蓄力深度来自当前活动敌人锚点。已配置 authored scenario 时，以 D0 定义资产为主；兼容回退资产只服务于旧配置或无 authored scenario 的路径，修改主资产时应同步维护对应回退值，避免两条路径表现不一致。

`defense_play` 只负责角色表现。命中目标、范围查询、伤害、削韧和 2 发弹药扣除都由战斗域在攻击提交时结算，不由 Spine 动画事件、HIT 标记或动画长度触发。

## 制作步骤

1. 打开 `FPG_PlayableCharacterCatalog.asset`，确认 Fei 条目同时引用 `D0_Fei.asset`、`D0_Fei_3C.asset` 与 `D0_Fei_CombatFeel.asset`；角色武器与表现继续由 `D0_Fei.asset` 引用。
2. 在 `D0_Fei_Weapon.asset` 的副射分组设置 `ImmediateRepeatWhileHeld`、`AreaAtFirstSurface`、弹药消耗、伤害倍率以及敌人/弹体独立上限。即时按住连发模式下 `secondaryMinimumChargeTicks` 保留为兼容字段，不参与正式触发。
3. 在 `D0_Fei_CombatFeel.asset` 将 `secondaryAreaRadius` 设为 3.0。该数值是命中查询半径，不是特效缩放。
4. 在 `D0_Fei_Presentation.asset` 将释放动画设为 `defense_play`，HIT/STOP 表现标记设为 0.033 秒和 1.0 秒；确认副射 `shot.sourceSocket = PrimaryMuzzle`、`targetDepthAnchor = ActiveEnemyGameplay`，并按主 / 副射表现字段合同核对枪口、弹道、蓄力和目标爆发参数。
5. 确认 `Battle/Secondary` 与 `Battle/Aim` 均绑定鼠标右键。不要新建并行 Action，也不要更换已有 Action/Binding ID。
6. 同步兼容回退的 `BattleScenarioConfig.asset` 和 `CombatPresentationProfile.asset`，执行 D0 安装器重建 Fei 派生 Prefab 的 Socket；确认场景中没有旧 `FeiMuzzleVisualAnchor` 或 `D0SecondaryTargetProxy`。
7. 保存资产，等待 Unity 编译与资源刷新完成，检查 Console 不存在相关错误。试玩手感、范围可读性和动画区分度交由主管按下表验收。

## 字段说明

| 配置组 | 中文名称 | 字段名 | 类型/单位 | 标准值与范围 | 生效条件与实际效果 | 常见误配 |
|---|---|---|---|---|---|---|
| 武器 | 副射弹药消耗 | `secondaryAmmoCost` | int / 发 | 2；1 到弹匣容量 | 攻击成功提交时从共享弹匣原子扣除 2 发 | 当成每目标消耗；实际一次攻击只扣一次 |
| 武器 | 副射触发模式 | `secondaryTriggerMode` | enum | `ImmediateRepeatWhileHeld` | 按下立即尝试；成功提交后按恢复时长重复；松开不生成攻击事件 | 设为 `ChargeRelease` 后仍期待按下即攻击 |
| 武器 | 副射查询模式 | `secondaryQueryMode` | enum | `AreaAtFirstSurface` | 首个目标、掩体或最大射程确定爆心，只提交范围 Impact，不提交直击 Impact | 设为直线查询后仍期待爆炸规则 |
| 武器 | 副射最低蓄力 | `secondaryMinimumChargeTicks` | int / Tick | 0；大于等于 0 | 仅 `ChargeRelease` 使用；当前即时按住模式不读取该门槛 | 把兼容字段当成当前连发间隔 |
| 武器 | 副射恢复时长 | `secondaryRecoveryTicks` | int / Tick | 30；大于 0 | 仅成功提交后启动；按住时恢复结束便可再次提交，也是副射唯一射速来源 | 另建第二份连发间隔，造成两套射速来源 |
| 武器 | 副射生命伤害 | `secondaryDamage` | int / 生命值 | 28；大于等于 0 | 每个普通部位命中目标受到 28 基础生命伤害，低于完整左键主射的 32 | 与 8 颗主射弹丸逐颗伤害直接比较 |
| 武器 | 副射削韧伤害 | `secondaryBreakDamage` | int / 韧性值 | 20；大于等于 0 | 每个命中目标按战斗域规则削减韧性 | 误认为与生命伤害共用同一倍率 |
| 武器 | 副射弱点生命倍率 | `secondaryWeakpointDamageMultiplierBasisPoints` | int / 万分比 | 12000（1.2×）；大于等于 0 | 弱点生命伤害在 28 基础值上应用 1.2 倍 | 填 120 或 1.2；字段要求万分比 |
| 武器 | 副射弱点削韧倍率 | `secondaryWeakpointBreakMultiplierBasisPoints` | int / 万分比 | 25000（2.5×）；大于等于 0 | 弱点削韧在基础削韧值上应用倍率 | 与弱点生命倍率混用 |
| 武器 | 副射敌人命中上限 | `secondaryMaxImpactCount` | int / 个 | 4；大于 0 | 一次爆炸最多提交 4 个敌人 Impact；同一 RuntimeId 去重且弱点优先 | 把弹体也计入敌人名额 |
| 武器 | 副射弹体命中上限 | `secondaryProjectileMaxImpactCount` | int / 个 | 4；大于等于 0 | 一次爆炸最多摧毁 4 个可拦截弹体，容量独立于敌人上限 | 设为 0 后仍期待爆炸拦截弹体 |
| 战斗手感 | 副射范围半径 | `secondaryAreaRadius` | float / Unity 世界单位 | 3.0；大于 0 | 以首表面爆心执行范围查询；爆心射线受掩体截停，爆炸到范围目标不再检查遮挡 | 用特效尺寸代替查询半径，导致画面与判定不一致 |
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
- `secondaryTriggerMode = ImmediateRepeatWhileHeld`
- `secondaryQueryMode = AreaAtFirstSurface`
- `secondaryMinimumChargeTicks = 0`
- `secondaryRecoveryTicks = 30`
- `secondaryDamage = 28`
- `secondaryWeakpointDamageMultiplierBasisPoints = 12000`（1.2×）
- `secondaryMaxImpactCount = 4`
- `secondaryProjectileMaxImpactCount = 4`
- `secondaryAreaRadius = 3.0`
- `secondaryReleaseAnimation = defense_play`
- `secondaryHitMarkerTime = 0.033`
- `secondaryStopMarkerTime = 1.0`
- `secondarySkillPresentation.shot.sourceSocket = PrimaryMuzzle`
- `secondarySkillPresentation.targetDepthAnchor = ActiveEnemyGameplay`
- `secondarySkillPresentation.shot.tracerDuration = 0.36`
- `secondarySkillPresentation.shot.tracerWidth = 0.12`

弹匣至少有 2 发且武器处于可用状态时，按下一次鼠标右键会在同一 Tick 提交副攻并扣除 2 发弹药。射线只决定爆心，3.0 半径内最多分别对 4 个敌人和 4 个弹体提交范围 Impact；普通部位每目标基础生命伤害为 28，弱点生命倍率为 1.2×。继续按住右键时，每次成功提交后的第 30 Tick 才可再次提交；松开不补发，弹药不足也不自动换弹。

## 验收与交接

最小技术检查包括：三份 D0 主配置与兼容回退值一致；`Battle/Secondary` 绑定鼠标右键；`defense_play` 存在于 Fei D0 SkeletonData；Unity 编译完成且 Console 无相关错误。以下体验项未由 Agent 试玩，状态统一为“待主管试玩/确认”。

| 编号 | 测试项 | 前置条件 | 主管操作 | 通过标准 | 证据/记录栏 | 状态 | 备注/风险 |
|---|---|---|---|---|---|---|---|
| H-01 | 即时触发与弹药消耗 | FormalRoom；Fei 弹匣至少 4 发；武器处于 Ready | 单击一次右键并观察弹药 | 按下即播放并提交一次副攻；弹药恰好减少 2 发；松开不补发 | 试玩人、日期、录屏/问题记录 | 待主管试玩/确认 | 右键同时承担 AimHeld |
| H-02 | 按住射速与自由瞄准共存 | 同 H-01；准备足够弹药 | 按住右键跨过至少两个恢复周期，同时移动自由准星后松开 | 按下立即攻击，之后只按 `secondaryRecoveryTicks` 重复；准星持续可移动；松开后停止 | 录屏/输入记录 | 待主管试玩/确认 | 弹药不足时应停止提交且不自动换弹 |
| H-03 | 与左键伤害区分 | 对同一普通部位可稳定完整命中；记录敌人生命变化 | 分别完整命中一次左键主射和一次右键副攻 | 左键完整普通命中为 32，右键单目标基础伤害为 28；右键没有反超 | 数值记录/录屏 | 待主管试玩/确认 | 散布或漏弹会影响左键对比，须确认 8 颗均命中 |
| H-04 | 爆心、遮挡与独立上限 | 准备范围边缘、掩体后敌人与可拦截弹体验收布置 | 分别把准星落在目标、掩体和空处使用一次副攻 | 掩体能截停爆心射线；爆炸仍可影响半径内掩体后目标；敌人和弹体各自不超过配置上限；没有额外直击伤害 | 场景布置、录屏、数值记录 | 待主管试玩/确认 | 场景布置与结果可读性由主管判断 |
| H-05 | 动画区分与回待机 | Fei 使用 D0 派生 SkeletonData | 交替执行左键主射和右键副攻 | 右键清晰播放 `defense_play`，与左键 `attack_play1/2` 可区分；动作结束回 `b_idle`，不追加 `u4_attack_end` | 录屏/问题记录 | 待主管试玩/确认 | 动作可读性属于主管判断 |
| H-06 | 弹药不足 | 弹匣仅剩 0 或 1 发 | 单击右键 | 不提交右键副攻、不扣成负数，也不播放已提交攻击的释放表现 | 录屏/弹药记录 | 待主管试玩/确认 | — |
