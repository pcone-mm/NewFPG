# D0 Fei 瞄准指示器配置说明

## 目标与适用范围

本配置控制 Fei 在 FormalRoom 中的角色专属瞄准指示器视觉；类型名中的 D0 前缀仅为序列化兼容：

- 常态显示淡青色圆环。
- 玩家进入权威探身／战斗瞄准姿态时，圆环提亮并出现低透明光晕。
- 任一射击成功提交后，圆环短暂放大并回落。
- 攻击有效命中战斗目标或可拦截投射物后，圆环外围出现红色四段命中弧并向外淡出。

它只控制 UI 表现，不改变虚拟光标位置、输入、灵敏度、散布、攻击射线、命中盒、伤害、弹药或战斗状态。

## 配置入口与资产位置

主要配置资产：

- Fei 表现资产：`Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Presentation.asset`
- Fei 角色目录：`Assets/FPGDemo/Config/FormalEncounter/Characters/`
- 正式 HUD 配置：`Assets/FPGDemo/Config/FormalEncounter/FPG_CombatPresentationProfile.asset`
- 正式场景：`Assets/FPGDemo/Scenes/FormalRoom.unity`

在 Inspector 中打开 `FPG_Fei_Presentation.asset`，展开“玩家表现 → 玩家动画与资源 → 角色瞄准指示器”。该 committed 资产是角色专属样式的权威来源。

## 引用关系与运行时数据流

```text
FPG_PlayableCharacterCatalog
  → FPG_Fei
  → FPG_Fei_Presentation
  → player.aimIndicator
```

FormalRoom 运行时从 `FPG_PlayableCharacterCatalog` 解析当前玩家表现资产，并绑定到正式准星表现组件；场景和 Prefab 绑定均为 authored 数据，不由生成器补齐。

本文件只定义通用准星样式。技能专属状态和反馈条件不在此重复定义；正式运行规范统一见 [FPG Formal Encounter 运行合同](../../Assets/FPGDemo/Docs/Workflow/FPG_Formal_Encounter_Runtime_Contract.zh-CN.md#fei-副射唯一规范)。

状态来源固定如下：

- 瞄准层：来自 Formal 玩家表现快照的权威探身／战斗姿态，不由 Presenter 回读裸输入。
- 射击层：`BattleSessionHost.PlayerShotPresentationFeed`。只有空间事务成功提交的射击才触发；被拒绝的射击不触发。
- 命中层：`BattleSession.SelectedAttackHits`。按 `AttackId` 聚合，一发多 pellet 或同一攻击命中多个目标时只提示一次。
- 暂停：保留暂停前的瞄准层，并冻结射击／命中反馈计时。

## 制作与验证步骤

1. 选择 `FPG_Fei_Presentation.asset`，确认“表现角色类型”为“玩家”。
2. 展开“角色瞄准指示器”，按“基础圆环 / 射击反馈 / 命中反馈”调整字段。
3. 保存资产。
4. 检查 Unity 编译、Console、Formal HUD 与玩家表现合同测试。
5. 首次加入组件或改变场景绑定时，在目标 Prefab/Scene Inspector 中显式修改并保存；不运行场景生成器。
6. 仅修改已绑定资产中的颜色、尺寸或时长时，重新进入运行即可读取配置。

不要手改 `FormalRoom.unity` YAML。场景组件、排序和旧十字节点必须在 Unity Editor 中显式维护。

## 字段说明

| 配置组 | 中文名称 | 字段名 | 类型／单位 | Fei 默认值与约束 | 生效结果与常见误配 |
|---|---|---|---|---|---|
| 基础圆环 | 常态颜色 | `restingColor` | Color / RGBA | `(0.48, 0.82, 0.92, 0.56)`；Alpha 必须可见 | 非探身状态的淡青圆环；Alpha 太高会削弱瞄准层级 |
| 基础圆环 | 瞄准颜色 | `aimingColor` | Color / RGBA | `(0.76, 0.96, 1, 0.96)`；Alpha 必须可见 | `Exposed` 姿态下提亮；不改变命中判定 |
| 基础圆环 | 射击闪光颜色 | `shotColor` | Color / RGBA | `(1, 1, 1, 1)`；Alpha 必须可见 | 射击脉冲峰值颜色 |
| 基础圆环 | 命中提示颜色 | `hitColor` | Color / RGBA | `(1, 0.13, 0.10, 1)`；Fei 规范为红色 | 外围四段命中弧颜色 |
| 基础圆环 | 圆环半径 | `baseRadius` | float / UI 像素 | `15`；至少 `1` | 常态与瞄准态半径 |
| 基础圆环 | 圆环线宽 | `ringThickness` | float / UI 像素 | `2`；至少 `0.5`，且小于基础直径 | 过宽会变成实心盘，校验会拒绝 |
| 基础圆环 | 瞄准光晕强度 | `aimingGlowAlpha` | float / Alpha | `0.22`；范围 `0–1` | `0` 关闭光晕；光晕宽度和缓动由代码固定 |
| 射击反馈 | 射击峰值半径 | `shotRadius` | float / UI 像素 | `23`；必须大于 `baseRadius` | 成功提交射击时圆环扩张到该半径 |
| 射击反馈 | 射击脉冲时长 | `shotDuration` | float / 秒 | `0.16`；至少 `0.01` | 使用非缩放时间；暂停时冻结 |
| 命中反馈 | 命中提示半径 | `hitMarkerRadius` | float / UI 像素 | `27`；其内沿必须位于射击圆外沿之外 | 与线宽共同决定红弧起始位置 |
| 命中反馈 | 命中提示线宽 | `hitMarkerThickness` | float / UI 像素 | `2.6`；至少 `0.5`，且小于命中圆直径 | 过宽或与射击圆重叠时校验会拒绝 |
| 命中反馈 | 命中分段角度 | `hitMarkerArcDegrees` | float / 度 | `24`；范围 `4–60` | 四段红弧各自覆盖的角度 |
| 命中反馈 | 命中外扩距离 | `hitExpansion` | float / UI 像素 | `4`；不得小于 `0` | 红弧淡出期间继续向外移动 |
| 命中反馈 | 命中提示时长 | `hitDuration` | float / 秒 | `0.20`；至少 `0.01` | 使用非缩放时间；暂停时冻结 |

“命中提示在外围”的硬约束为：

```text
hitMarkerRadius - hitMarkerThickness / 2
>
shotRadius + ringThickness / 2
```

Fei 默认最大外缘约为 `27 + 4 + 2.6 / 2 = 32.3 px`，位于当前 `72 × 72 px` 指示器根范围内。若要显著放大未来角色的样式，需要同时评估 authored HUD 根尺寸。

## Fei 标准示例与预期表现

当前资产中的标准值就是上表默认值：

- 常态：半径 `15 px`、线宽 `2 px` 的淡青圆环。
- 瞄准／探身：颜色切换为高亮青白色，并叠加 `0.22` Alpha 光晕。
- 射击：在 `0.16 s` 内扩张到 `23 px` 后回落；连续射击会刷新本次脉冲时长。
- 命中：在 `27 px` 半径显示红色四段圆弧，并在 `0.20 s` 内向外移动 `4 px` 后淡出。
- 射击落空：只显示射击脉冲，不显示红色命中层。
- 射击与命中同帧发生：放大的基础圆环和固定在外围的红弧同时显示，互不缩放。

## 不开放的技术参数

以下内容属于稳定的视觉语言或工程绑定，不在角色资产中开放：

- 圆环网格精度、固定四段方位、脉冲和淡出的缓动曲线。
- `D0AimReticle` 的 Canvas、排序层、场景组件引用和执行顺序。
- 虚拟光标安全区、位置、输入灵敏度、攻击查询距离和命中盒。
- 射击／命中触发来源及 `AttackId` 聚合规则。
- 旧 `Horizontal` / `Vertical` 十字节点在 authored HUD 中保持禁用，不作为样式选项。

## 验收与交接

最小技术检查包括：Fei 表现资产 `TryValidate` 通过、Unity 编译无错误、正式场景合同通过。视觉、节奏和手感由主管在 FormalRoom 中确认。

| 编号 | 测试项 | 前置条件 | 主管操作 | 通过标准 | 证据／记录栏 | 状态 |
|---|---|---|---|---|---|---|
| H-01 | 常态圆环 | 进入 CombatLab，未探身 | 观察并移动虚拟光标 | 指示器是淡青圆环，无旧十字残留 | 试玩人、日期、备注 | 待主管试玩／确认 |
| H-02 | 瞄准层 | 玩家可正常探身 | 按住瞄准或进入主射探身姿态 | 圆环提亮并出现低透明光晕；暂停后样式不跳变 | 试玩人、日期、备注 | 待主管试玩／确认 |
| H-03 | 射击层 | 武器可成功释放 | 完成两次成功射击，并触发一次被拒绝射击 | 每次成功射击圆环明显放大后回落；被拒绝射击不闪 | 试玩人、日期、备注 | 待主管试玩／确认 |
| H-04 | 命中层 | 敌人或可拦截投射物可命中 | 分别制造命中与落空 | 命中时外围出现红色四段弧；落空时没有红弧 | 试玩人、日期、备注 | 待主管试玩／确认 |
