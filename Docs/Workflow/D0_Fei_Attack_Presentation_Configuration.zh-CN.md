# D0 Fei 主射表现配置

## 配置边界

Fei 的视觉预制体、视觉缩放、技能 Socket、主射枪口闪光和弹道统一由 `Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Presentation.asset` 的 `PlayerActorPresentationDefinition` 持有。正式环境与出生点分别由 Art Scene 和 RoomDefinition 持有。

本文件不定义玩家副射；相关正式运行规范统一见 [FPG Formal Encounter 运行合同](../../Assets/FPGDemo/Docs/Workflow/FPG_Formal_Encounter_Runtime_Contract.zh-CN.md#fei-副射唯一规范)。

这些字段全部属于表现层：逻辑射线起点仍来自已提交的 `AimPose`，弹道终点仍来自 `PlayerShotPresentationSnapshot` 的冻结轨迹，伤害、命中、弹药和范围查询不读取本配置。

正式 Fei Entity Prefab 直接拥有 `D0ActorPresentationSockets` 和 `PrimaryMuzzle` 子节点。修改 Socket 时在 Prefab Mode 显式编辑并保存，同时保持 `FPG_Fei_Presentation.asset` 的引用一致；不通过生成器重建。

## 字段表

| 配置组 | 字段 | 标准值 | 实际作用 | 不影响 |
| --- | --- | --- | --- | --- |
| 角色视觉 | `visualScale` | `2.15` | Fei 派生视觉实例的统一缩放 | 出生点、碰撞、伤害 |
| Socket | `socketPoses[PrimaryMuzzle].localPosition` | `(0.72, 0.42, -0.06)` | 角色预制体内枪口表现挂点 | `AimPose.Origin` |
| Socket | `localEulerAngles` | `(0, 0, 0)` | 枪口闪光朝向 | 射线方向 |
| 主射 | `sourceSocket` | `PrimaryMuzzle` | 主射表现起点 | 逻辑枪口 |
| 主射 | `effectColor` | `(0.42, 0.90, 1.00, 0.96)` | 主射枪口主色 | 命中类型颜色 |
| 主射 | `muzzleDuration / Length / Width / LightIntensity` | `0.07 / 0.34 / 0.12 / 1.25` | 主射枪口闪光 | 攻速、伤害 |
| 主射 | `tracerDuration / Width / EndpointLightIntensity` | `0.13 / 0.052 / 0.8` | 主射弹道持续时间、宽度和命中端亮度 | 轨迹终点、命中结果 |
`CombatPresentationTiming` 中旧的主射 trail 字段只作旧资产兼容，Formal 玩家射击主路径不再读取。弱点命中闪光读取 `CombatHitPresentationDefinition.Weakpoint.Duration`。

## 制作与验证

1. 在 `FPG_Fei_Presentation.asset` 修改技能表现字段，并在正式 Fei Entity Prefab 中显式维护 Socket。
2. 确认正式 Fei Entity Prefab 根节点包含 `D0ActorPresentationSockets`，且可解析 `PrimaryMuzzle`。
3. 确认 `FormalRoom.unity` 不包含旧 `FeiMuzzleVisualAnchor`。
4. 编译后检查配置校验、场景合同和射击表现控制器无错误。
5. 技术验证只证明绑定、参数消费和战斗状态不被写入；下表体验项由主管试玩。

| 编号 | 体验项 | 通过标准 | 状态 |
| --- | --- | --- | --- |
| H-AP-01 | 枪口跟随 | 主射从 Fei 模型枪口发出，动画期间无明显漂移 | 待主管试玩/确认 |
| H-AP-02 | 弹道可读性 | 主射宽度、亮度和持续时间清晰且不过曝 | 待主管试玩/确认 |
| H-AP-04 | 行为不变 | 调整表现字段不改变命中、伤害、弹药、范围或射程 | 待主管试玩/确认 |
