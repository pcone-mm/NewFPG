# D0 FormalRoom 战斗 HUD 与反馈配置

## 目标与适用范围

本配置统一管理 FormalRoom 的玩家生命、护盾、弹药 HUD，敌人头顶生命条，逐 Impact 伤害跳字和正式准星状态。数值、标签、颜色、顺序、格式、条形缓动、跳字 Sprite/布局/错位以及准星颜色/尺寸/脉冲时长由现有 `CombatPresentationProfile` 提供；战斗权威值仍只来自 `CombatantState`、武器运行时和正式反馈流。

本文不覆盖 CombatLab 的旧表现，不新增治疗、复活、敌人韧性 HUD 或敌人具体数字。LayerMask、物理查询容量、事件流容量、对象池实现和 RuntimeId 绑定属于工程配置，不作为视觉调参入口。

## 配置入口与资产位置

- 主配置资产：`Assets/FPGDemo/Config/FormalEncounter/FPG_CombatPresentationProfile.asset`
- 配置类型：`Assets/FPGDemo/Runtime/Unity/CombatPresentationProfile.cs`
- 正式字段类型：`Assets/FPGDemo/Runtime/Unity/FormalCombatPresentationConfig.cs`
- FormalRoom 场景：`Assets/FPGDemo/Scenes/FormalRoom.unity`
- 敌人头顶条 Prefab：`Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_OverheadHealthBar.prefab`
- 伤害跳字 Prefab：`Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_DamagePopup.prefab`
- 跳字美术源资源：`Assets/Art/HUD/Hit_tip`
- HUD、跳字 Prefab 与 FormalRoom 引用均为 committed authored 资产，不提供生成入口。

## 引用关系

`CombatPresentationProfile → Formal HUD / Damage Popup / Reticle 配置`

`FormalRoom authored bindings → 玩家 HUD、头顶条 Prefab、跳字 Prefab、Feedback Bridge`

`CombatantState → Vitals Snapshot Stream → Player HUD / Enemy RuntimeId Bar Binding`

`ImpactIntent → Combat Resolution → ResolvedDamageFeedbackStream → Damage Popup Pool`

数字同帧显示权威值；条形从当前视觉比例缓动到新比例。暂停只冻结条形、跳字和准星脉冲的表现推进，不冻结或反写战斗状态。事件流发生 gap 时，生命条按 RuntimeId 拉取完整快照重同步；伤害跳字丢弃缺失批次，二者都不得让战斗进入 Fault。

## 制作与验证步骤

1. 打开 `CombatPresentationProfile.asset`，在正式 HUD、正式伤害跳字和正式准星分组中调整字段。不要在 FormalRoom 场景组件上复制颜色、时长、射程或范围值。
2. 正式 HUD 必须恰好包含 `Life`、`Barrier`、`Ammo` 三项，且 `kind` 与 `order` 各自唯一。`order` 只决定三项映射到现有三个 HUD 槽位的顺序，不创建新的屏幕坐标。
3. 身体、弱点和弹体拦截分别通过 `formalDamagePopup.spriteStyles` 绑定一套底纹与 0–9 Sprite；默认映射为 `Body → normal`、`Weakpoint → critcal`、`Intercept → elemental`。映射保存在 Profile，可在 Inspector 更换，不由 View 或 Bridge 写死。显示时长继续复用 `hitDefinitions` 中对应类型的 `duration`。
4. 结构或引用变化时，在 Prefab Mode 与 FormalRoom Inspector 中只修改目标对象并显式保存；不要手工编辑场景或 Prefab YAML，也不要使用全量生成器覆盖 authored 资产。
5. 等待 Unity 编译和资源刷新完成，确认配置静态校验、Prefab 引用、实际 RectTransform 几何及正式场景绑定通过技术检查。
6. 颜色辨识、缓动手感、跳字可读性与准星反馈节奏按本文末尾表格交由主管试玩确认。

## 字段说明

| 配置组 | 中文名称 | 字段名 | 类型/单位 | 默认值与范围 | 生效条件与实际效果 | 常见误配 |
|---|---|---|---|---|---|---|
| 正式 HUD 资源 | 资源类型 | `formalHudResources[].kind` | enum | Life / Barrier / Ammo，各一项 | 绑定对应权威资源；敌人不读取该数组 | 重复类型或遗漏一项 |
| 正式 HUD 资源 | 标签 | `formalHudResources[].label` | string | LIFE / BARRIER / AMMO；非空 | 显示在 `current/max` 数字前 | 在 Presenter 中另写一套固定文本 |
| 正式 HUD 资源 | 颜色 | `formalHudResources[].color` | Color | 可见且 alpha 大于 0 | 修改对应条形填充颜色 | 使用完全透明颜色 |
| 正式 HUD 资源 | 顺序 | `formalHudResources[].order` | int | 0 / 1 / 2；互不重复 | 将资源映射到已制作的上、中、下三个槽位 | 只改数组顺序却留下重复 order |
| 正式 HUD 资源 | 数字格式 | `formalHudResources[].valueFormat` | string format | `{0}/{1}`；必须含两个占位符 | 0 为 current，1 为 max；数字同帧更新 | 删除占位符或交换含义 |
| 正式 HUD 资源 | 条形缓动时长 | `formalHudResources[].barEaseDuration` | float / 秒 | Life 0.16、Barrier 0.18、Ammo 0.12；大于 0 | 只影响 RectTransform 宽度过渡，不延迟数字 | 当成战斗资源恢复时长 |
| 伤害跳字 Sprite | 命中类型 | `formalDamagePopup.spriteStyles[].kind` | enum | Body / Weakpoint / Intercept，各一项 | 将正式命中类型映射到一套 Sprite 资源 | 重复类型或遗漏一类 |
| 伤害跳字 Sprite | 底纹 | `backgroundSprite` | Sprite | 非空；默认普通/暴击共用黑色底纹，拦截使用红色底纹 | 使用 Sliced Image 随多位数字横向扩展 | 使用不含横向 Border 的底图后期待九宫格拉伸 |
| 伤害跳字 Sprite | 数字 Sprite | `digitSprites[0..9]` | Sprite[10] | 必须按 0–9 顺序完整配置 | 按实际伤害值逐位拼接，不创建运行时字体 | 缺数字、顺序错误或把整张图当字体 |
| 伤害跳字 Sprite | 数字高度 | `digitHeight` | float / UI 单位 | 60；大于 0 | 保持每个数字 Sprite 的原始宽高比并统一显示高度 | 固定每位宽度导致 1、4 等字形变形 |
| 伤害跳字 Sprite | 数字间距 | `digitSpacing` | float / UI 单位 | -2；必须为有限值 | 控制相邻数字的水平距离，允许轻微重叠 | 负值过大导致总宽度无效 |
| 伤害跳字 Sprite | 底纹水平内边距 | `backgroundHorizontalPadding` | float / UI 单位 | 34；大于等于 0 | 底纹宽度至少为数字总宽加两侧内边距 | 当成跳字之间的间距 |
| 伤害跳字 Sprite | 底纹最小尺寸 | `backgroundMinSize` | Vector2 / UI 单位 | 133×50；两轴大于 0 | 单位数也保持美术规定的最小底纹尺寸 | 误当成固定根节点尺寸，裁切长数字 |
| 伤害跳字 | 命中点上移 | `formalDamagePopup.screenVerticalOffset` | float / 屏幕像素 | 24；大于等于 0 | 从对应命中点向上偏移 | 当成世界坐标高度 |
| 伤害跳字 | 同帧邻近距离 | `formalDamagePopup.nearbyDistance` | float / 屏幕像素 | 42；大于等于 0 | 判断同帧数字是否需要垂直错位，不合并数值 | 当成伤害聚合半径 |
| 伤害跳字 | 邻近垂直步长 | `formalDamagePopup.nearbyVerticalStep` | float / 屏幕像素 | 20；大于等于 0 | 每个邻近数字追加的垂直错位 | 设为 0 后仍期待数字分离 |
| 命中样式 | 身体/弱点/弹体样式 | `hitDefinitions[Body/Weakpoint/Intercept]` | 秒 | 使用现有三类定义 | 提供 Sprite 跳字持续时间；PNG 使用原始颜色，不再乘旧文字色；每个成功 Impact 仍独立显示 | 用 `PrimaryColor` 给已着色 PNG 再次染色 |
| 正式准星 | 空闲/可命中/阻挡颜色 | `formalReticle.idleColor / hittableColor / blockedColor` | Color | 可见且 alpha 大于 0 | 由共用 Aim Solution 选择当前目标状态 | 用射击结果反推可命中预览 |
| 正式准星 | 射击/命中颜色 | `formalReticle.shotColor / hitColor` | Color | 可见且 alpha 大于 0 | 已提交攻击和成功 Impact 分别触发 | 查询失败时仍播放已提交射击状态 |
| 正式准星 | 状态尺寸 | `idleSize / hittableSize / blockedSize` | float / UI 单位 | 18 / 20 / 22；大于 0 | 同时修改准星容器和实际十字 Graphic 主轴长度 | 只缩放父节点而不改实际线段 |
| 正式准星 | 脉冲尺寸 | `shotPulseSize / hitPulseSize` | float / UI 单位 | 28 / 30；大于 0 | 射击与命中脉冲期间使用 | 当成射线宽度或命中盒尺寸 |
| 正式准星 | 脉冲时长 | `shotPulseDuration / hitPulseDuration` | float / 秒 | 0.08 / 0.14；大于 0 | 只控制表现状态维持时间；暂停时冻结 | 当成武器射速或 hit stop |

## 示例配置与预期结果

- Life：`order = 0`、`valueFormat = {0}/{1}`、`barEaseDuration = 0.16`
- Barrier：`order = 1`、`barEaseDuration = 0.18`
- Ammo：`order = 2`、`barEaseDuration = 0.12`
- Damage Popup：Body 使用 `zi_normal`，Weakpoint 使用 `zi_critcal`，Intercept 使用 `zi_elemental`；`digitHeight = 60`、`digitSpacing = -2`、`backgroundHorizontalPadding = 34`、`backgroundMinSize = (133, 50)`、`screenVerticalOffset = 24`、`nearbyDistance = 42`、`nearbyVerticalStep = 20`
- Reticle：空闲 18、可命中 20、阻挡 22、射击脉冲 28、命中脉冲 30

玩家受伤、护盾变化或弹药变化时，数字立即显示最新 `current/max`，条形按各自配置时长过渡。敌人只显示头顶生命条，不显示韧性或具体数字。每个实际结算成功的 Impact 生成一条独立跳字；同帧邻近数字只错位，不聚合。射击与命中脉冲结束后，准星回到当前空闲、可命中或阻挡状态。

## 验收与交接

自动化只检查序列、快照、配置校验、事件数量、池溢出隔离和实际 RectTransform 几何，不判断颜色是否好看、节奏是否舒服或信息是否易读。以下体验项状态均为“待主管试玩/确认”。

| 编号 | 测试项 | 前置条件 | 主管操作 | 通过标准 | 证据/记录栏 | 状态 | 备注/风险 |
|---|---|---|---|---|---|---|---|
| H-F01 | 玩家三资源信息层级 | FormalRoom 进入 Running | 分别消耗生命、护盾与弹药 | 三项顺序、标签、颜色和 `current/max` 在目标分辨率下清楚，数字变化没有视觉误读 | 分辨率、录屏/截图、问题记录 | 待主管试玩/确认 | 数字正确性由技术测试覆盖，可读性由主管判断 |
| H-F02 | 数字即时与条形缓动 | 同 H-F01 | 制造大幅和连续的小幅资源变化 | 数字即时可信，条形过渡不拖沓、不抢注意力；暂停期间视觉冻结 | 录屏、主观记录 | 待主管试玩/确认 | 缓动时长不改变战斗值 |
| H-F03 | 多 Impact 跳字可读性 | 可稳定命中多 pellet、多目标、弱点与弹体 | 连续执行主射、穿透和副攻爆炸 | 每条命中反馈可辨认，邻近错位不会遮挡关键目标；身体/弱点/拦截风格易区分 | 录屏、问题记录 | 待主管试玩/确认 | 事件不聚合由技术测试覆盖 |
| H-F04 | 准星五类反馈 | 有目标、掩体和空白区域 | 移动准星并执行命中/未命中攻击 | 空闲、可命中、阻挡、射击、成功命中状态清楚且脉冲节奏合适 | 录屏、问题记录 | 待主管试玩/确认 | Aim/命中一致性由规则测试覆盖 |
| H-F05 | 敌人头顶生命条 | 多个普通敌人同时激活 | 依次伤害不同敌人并改变镜头 | 条形归属清楚、跟随稳定、不会被误认成韧性或数字 HUD | 录屏、问题记录 | 待主管试玩/确认 | 当前不交付 Boss 独立条 |
| H-F06 | 暂停与重开收束 | 已产生条形过渡和多个跳字 | 暂停、恢复，再执行重开 | 暂停时表现冻结；重开后旧跳字消失、条形和准星立即回到新局状态 | 录屏、问题记录 | 待主管试玩/确认 | cursor 与池复位由技术测试覆盖 |
