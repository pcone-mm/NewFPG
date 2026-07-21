# D0 Fei 主射 / 副射表现配置

## 配置边界

Fei 的视觉预制体、视觉缩放、技能 Socket、枪口闪光、弹道、蓄力目标深度和副射目标爆发统一由 `Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Presentation.asset` 的 `PlayerActorPresentationDefinition` 持有。`D0_CombatLab_Stage.asset` 只负责舞台环境和后续出生点，不再持有枪口或副射目标代理。

这些字段全部属于表现层：逻辑射线起点仍来自已提交的 `AimPose`，弹道终点仍来自 `PlayerShotPresentationSnapshot` 的冻结轨迹，伤害、命中、弹药和范围查询不读取本配置。

兼容回退 `Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset` 的内联 `player` 必须同步相同值。安装器从角色配置生成 `D0_Fei_30048_StraightAlpha.prefab` 上的 `D0ActorPresentationSockets` 和 `PrimaryMuzzle` 子节点；修改 Socket 后必须重新执行 `FPG Demo/D0 2.5D/Install or Update Combat Slice`。

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
| 副射 | `shot.sourceSocket` | `PrimaryMuzzle` | 副射表现起点 | 逻辑枪口 |
| 副射 | `shot.effectColor` | `(1.00, 0.98, 0.72, 1.00)` | 副射枪口、蓄力、弹道和目标爆发主色 | 弱点命中反馈配置 |
| 副射 | `shot.muzzleDuration / Length / Width / LightIntensity` | `0.13 / 0.62 / 0.18 / 2.1` | 副射枪口闪光 | 蓄力门槛、伤害 |
| 副射 | `shot.tracerDuration / Width / EndpointLightIntensity` | `0.36 / 0.12 / 1.55` | 副射释放弹道 | 轨迹终点、命中结果 |
| 副射 | `targetDepthAnchor` | `ActiveEnemyGameplay` | 用当前活动敌人主体锚点建立准星投影深度平面 | 目标选择、物理查询 |
| 副射 | `fallbackCameraDistance` | `8` | 无活动敌人时的相机前方表现深度 | 最大射程 |
| 副射 | `chargePulseDuration` | `0.18` | 蓄力光效脉冲节奏 | 战斗蓄力 Tick |
| 副射 | `targetBurstRadiusScale / Min / Max` | `0.32 / 0.42 / 1.4` | 将已提交范围半径转换为目标局部爆发表现尺寸 | 范围查询半径 |

`CombatPresentationTiming` 中旧的主射 trail、副射 charge/release 字段暂时保留作旧资产兼容；D0 玩家射击主路径不再读取它们。弱点命中闪光读取 `CombatHitPresentationDefinition.Weakpoint.Duration`，不再借用副射蓄力节奏。

## 制作与验证

1. 在 `D0_Fei_Presentation.asset` 修改 Socket 或技能表现字段，并同步 `CombatPresentationProfile.asset` 的内联回退。
2. 执行 D0 安装器，确认派生 Fei Prefab 根节点包含 `D0ActorPresentationSockets`，且可解析 `PrimaryMuzzle`。
3. 确认 `CombatLab.unity` 不再包含 `FeiMuzzleVisualAnchor` 或 `D0SecondaryTargetProxy`。
4. 编译后检查配置校验、场景合同和射击表现控制器无错误。
5. 技术验证只证明绑定、参数消费和战斗状态不被写入；下表体验项由主管试玩。

| 编号 | 体验项 | 通过标准 | 状态 |
| --- | --- | --- | --- |
| H-AP-01 | 枪口跟随 | 主射、副射均从 Fei 模型枪口发出，动画期间无明显漂移 | 待主管试玩/确认 |
| H-AP-02 | 弹道可读性 | 主射和副射宽度、亮度、持续时间可区分且不过曝 | 待主管试玩/确认 |
| H-AP-03 | 副射目标深度 | 敌人移动或替换后，蓄力框仍位于当前活动敌人的深度平面 | 待主管试玩/确认 |
| H-AP-04 | 行为不变 | 调整表现字段不改变命中、伤害、弹药、范围或射程 | 待主管试玩/确认 |
