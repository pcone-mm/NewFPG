# D0 技能动画位移配置

## 目标与适用范围

这套配置让 D0 敌人技能按技能决定是否采用 Spine 美术位移，并保证美术位移与程序行为位移、程序技能位移相加，而不是互相覆盖。

当前只提取 Spine 标记骨的 X/Y 平移，不提取旋转、缩放、剪切或 Spine Event。适用入口包括：

- 陆鸾孵化蝴蝶时的 `appear` 出生动画；
- 任意 `D0EnemyAttackDefinition` 攻击动画；
- 以后通过 `D0EnemyBehaviorController` 写入程序技能位移的 D0 技能。

它不把表现 Collider、Spine Event 或动画播放帧变成伤害权威；命中、投射物和伤害仍由 BattleSession 的 Tick 流程负责。

## 配置入口与资产位置

| 内容 | 配置入口 | 当前示例资产 |
|---|---|---|
| 蝴蝶出生 | `D0LuanSummonHudieDefinition.appearanceAnimationMotion` | `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_SummonHudie.asset` |
| 敌人攻击 | `D0EnemyAttackDefinition.animationMotion`、`animationMotionStartPhase` | `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/Attacks/D0_Hudie_Attack_Bullet.asset` |
| 蝴蝶 Spine 源数据 | 美术源文件 | `Assets/Art/Monster/hudie/hudie.json` |
| D0 蝴蝶表现 | 派生 SkeletonData 与 Prefab | `Assets/FPGDemo/Presentation/Hudie/Spine/`、`Assets/FPGDemo/Presentation/Hudie/Prefabs/` |

脚本编译完成后，先运行菜单 `FPG Demo/D0 2.5D/Install or Update Luan Hudie Configurations`。安装器会为出生技能写入 `appear + gameplay_motion`，为蝴蝶攻击写入 `attack + gameplay_motion + 前摇开始`，但不会替策划开启 `enabled`，因此不会在美术资产尚未满足合同时误启用。

## 引用关系与运行时所有权

```text
D0EncounterDefinition.SpawnSlot
  └─ D0EnemyDefinition.EntityPrefab
       ├─ VisualRoot（视觉局部姿态来自敌人表现定义）
       └─ GameplayRoot（identity 局部姿态）
            ├─ BodyHitbox
            ├─ WeakpointAnchor
            │    └─ WeakpointHitbox
            └─ ProjectileSpawnAnchor

D0 技能资产
  └─ D0AnimationMotionSettings
       ├─ Spine 动画名
       └─ 顶层 gameplay_motion 标记骨
            ↓ 按绝对 Battle Tick 采样
D0EnemyBehaviorController
  ├─ 程序行为位移
  ├─ 程序技能位移
  ├─ 已锁存动画位移
  └─ 当前动画位移
            ↓ 四路相加到当前 EntityPrefab
       VisualRoot + GameplayRoot
```

最终位移严格为：

```text
最终实体偏移
= 程序行为位移
+ 程序技能位移
+ 已锁存动画位移
+ 当前动画位移
```
当前美术动画位移一次只维护一个活动采样通道；同一时段若启动第二个已启用动画位移的技能，前一个通道会先按其 `persistEndOffset` 收口，再切换到后一个。当前陆鸾/蝴蝶时间表不存在这种重叠，因此本场景安全。若后续允许两个带美术位移的技能真正并行，需要在配置验证中禁止重叠，或把运行时扩展为多动画位移通道；这不影响“一个技能的程序位移 + 美术位移”同时叠加。


`D0EnemyBehaviorController` 是活动敌人视觉/gameplay 位移的唯一运行时写入者，并在战斗 Tick 内写入后调用 `Physics.SyncTransforms()`。它绑定当前 `D0EnemyEntityView`，对 EntityPrefab 的 VisualRoot 与 GameplayRoot 应用同一偏移。躯干命中体必须位于 GameplayRoot 下，弱点命中体必须位于其 WeakpointAnchor 下；Prefab 校验与场景启动校验都会拒绝错误层级。

陆鸾/蝴蝶 Encounter 为两个 SpawnSlot 预备各自的 EntityPrefab。孵化 Tick 到达后，EntityWorld 解除陆鸾 gameplay 绑定并停用其实体，再按 `InheritPreviousGameplayPose` 把陆鸾当前 gameplay 世界姿态交给蝴蝶实体；只有蝴蝶的 VisualRoot、GameplayRoot 与命中体继续参与后续 Tick。

投射物只在生成瞬间读取发射者与目标位置并生成冻结的 `ProjectilePathSnapshot`。因此：

- 蝴蝶移动会改变其 EntityPrefab 内 `ProjectileSpawnAnchor` 的世界位置，从而改变之后生成的新子弹出生位置；
- 已发射子弹继续按自己的冻结轨迹运动，不会追随蝴蝶或陆鸾；
- 不得把飞行中的子弹挂到 EntityPrefab、GameplayRoot 或 ProjectileSpawnAnchor 下。

## Spine 美术制作合同

当前 `hudie.json` 不能直接开启动画位移：它没有 `gameplay_motion`，且 `appear.root.translate` 末值约为 `(-309.96, 386.02)`；`idle.root.translate` 首值约为 `(0, -101.35)`。这正是 `appear → idle` 时可见模型回跳的来源。

美术修改必须满足以下规则：

1. 新增名为 `gameplay_motion` 的独立顶层骨，不设置父骨。
2. `gameplay_motion` 自身及所有后代都不能挂 Slot，也不能承载任何可见网格；程序会拒绝违反该规则的资源。
3. 把“整个实体确实应该飞走”的 X/Y 平移曲线放到 `gameplay_motion`。
4. `gameplay_motion` 建议在动画 0 秒明确落一帧 `(0, 0)`，末帧表示相对技能开始位置的最终实体偏移。
5. 从可见 `root` 删除同一份实体平移，避免画面骨骼移动一次、实体锚点又移动一次。
6. `root` 在 `appear / idle / attack / die` 间保持统一稳定基准。呼吸、振翅、后坐、受击抖动和死亡下坠等纯视觉动作应移到 `visual_root` 或其他可见子骨。
7. 检查 `appear` 末帧与 `idle` 首帧的可见骨姿态连续；不能只迁移 `appear.root.translate` 而保留 `idle.root.translate` 的旧基准，否则切动画仍会跳。
8. 首版不要在 `gameplay_motion` 上制作旋转、缩放或剪切；运行时不会提取它们。

`hudie_SkeletonData.asset` 的导入缩放为 `0.01`，Spine 时间线载入时已经应用。程序只再按实际 Unity 表现根的旋转/缩放转换一次，不得在曲线或代码中重复乘 `0.01`。

## 制作与启用步骤

1. 美术按上面的合同修改 `Assets/Art/Monster/hudie/hudie.json` 对应的 Spine 工程并重新导出。
2. 等待 Unity 重新导入 SkeletonData，确认 `gameplay_motion` 存在且目标动画上有 TranslateTimeline。
3. 运行 `FPG Demo/D0 2.5D/Install or Update Luan Hudie Configurations`，补齐配置中的动画名、标记骨名和开始阶段。
4. 在目标技能资产中展开“动画位移”，确认动画名与实际播放动画完全一致。
5. 开启“启用动画位移”；出生飞行通常同时开启“结束后保留位移”。
6. 若本版 SpawnPoint/EntityWorld 场景基础设施尚未安装，运行一次 `FPG Demo/D0 2.5D/Install or Update Combat Slice`；随后运行 `FPG Demo/D0 2.5D/Validate Planner Configuration`。切换 Scenario 本身不要求重装 Combat Slice。
7. 由主管在 CombatLab 试玩确认轨迹、切动画连续性和命中体对齐。

## 字段说明

| 配置组 | 中文名称 | 字段名 | 类型/单位 | 默认值与范围 | 生效条件与实际效果 | 常见误配 |
|---|---|---|---|---|---|---|
| 动画位移 | 启用动画位移 | `enabled` | bool | `false` | 开启后才采样标记骨；关闭时该技能贡献零动画位移，动画仍正常播放 | 美术未提供 marker 就开启会被配置验证拒绝 |
| 动画位移 | 动画名称 | `animationName` | string | 出生为 `appear`；攻击由安装器填写 | 必须与实际表现动画一致 | 写成另一动画会出现画面与实体轨迹不一致 |
| 动画位移 | 位移标记骨 | `motionBoneName` | string | `gameplay_motion` | 必须是无父骨、无 Slot/可见后代的顶层 marker | 指向可见 `root` 会被拒绝，防止双重移动 |
| 动画位移 | 结束后保留位移 | `persistEndOffset` | bool | `true` | 正常结束时锁存末端偏移；战斗结束/组件停用中断时锁存当前偏移。关闭则结束或中断时移除本次偏移 | 非零末帧且关闭时会按设计回到技能前位置 |
| 攻击动画位移 | 动画位移开始阶段 | `animationMotionStartPhase` | enum | `Windup` | `Windup` 从前摇开始；`Release` 从攻击释放 Tick 开始 | 与可见动画启动阶段不同会产生时序错位 |

程序技能位移不属于美术配置字段。技能逻辑通过 `TrySetProgramSkillMotionOffset` 写入独立通道；它与上述动画位移相加，不会覆盖动画位移，也不会直接写 Transform。

## 示例

### 蝴蝶出生飞行

```text
enabled = true
animationName = appear
motionBoneName = gameplay_motion
persistEndOffset = true
```

预期：蝴蝶在 `appear` 中飞到美术终点，其 EntityPrefab 的 VisualRoot、GameplayRoot、躯干命中体、弱点和投射锚点同步移动；切到 `idle` 后实体保持终点。上一陆鸾 EntityPrefab 已停用。已经飞出的子弹不受影响。

### 只有视觉后坐的攻击

```text
enabled = false
```

预期：攻击动画仍播放，但实体不读取动画位移；程序巡逻或程序技能位移照常生效。视觉后坐应制作在 `visual_root`，不要放到 `gameplay_motion`。

## 验收与交接

最小技术检查：

- 策划配置验证没有缺动画、缺 marker、marker 可见层级或非法阶段错误；
- Unity Console 无编译错误；
- EntityPrefab 与场景启动校验确认 VisualRoot/GameplayRoot 相互独立，Body/Weakpoint/ProjectileSpawnAnchor 位于正确 gameplay 分支；
- 暂停时动画位移不推进，重开战斗后四个位移通道全部重置。

待主管试玩/确认：

| 编号 | 测试项 | 前置条件 | 主管操作 | 通过标准 | 状态 |
|---|---|---|---|---|---|
| H-01 | 出生轨迹与切动画连续性 | 美术已交付 `gameplay_motion`，出生动画位移已开启 | 进入 `FeiVsLuanSummonsHudie`，观察孵化到 idle | 蝴蝶无回跳；陆鸾不随蝴蝶移动 | 待主管试玩/确认 |
| H-02 | 实体附属物跟随 | 同上 | 在蝴蝶飞行前后攻击其身体与弱点，并观察新发子弹 | 命中位置随蝴蝶；新子弹从移动后的实体位置生成 | 待主管试玩/确认 |
| H-03 | 在途子弹独立 | 让蝴蝶发弹后继续产生实体位移 | 观察已发子弹轨迹 | 已发子弹不转挂、不回拉、不跟随蝴蝶 | 待主管试玩/确认 |
| H-04 | 双通道叠加 | 某技能同时配置程序技能位移与动画位移 | 触发该技能 | 最终位移为两者之和，无互相覆盖或重复移动 | 待主管试玩/确认 |

## 当前单敌人 Runtime 限制

`LuanHudieSingleProjectile` 当前仍只有一个活动 `EnemyRuntimeId`。陆鸾与蝴蝶已经各自拥有完整 EntityPrefab，不再共享场景临时命中体；但 SpawnSlot 切换时上一实体会解除 gameplay 绑定并停用，所以它仍是单战斗身份的孵化替换，不代表两者同时拥有独立生命、命中体、目标选择、AI 和死亡。

如果产品要求孵化后陆鸾仍是可攻击、可行动的战斗实体，同时蝴蝶也是另一只完整敌人，就必须扩展多敌人 Runtime 和真正的 summon 生命周期；不能仅让两个 EntityPrefab 同时可见或复用一个 `RuntimeId` 冒充完成。
