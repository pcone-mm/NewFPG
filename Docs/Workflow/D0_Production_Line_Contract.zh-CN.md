# D0 Fei × Burstbug 生产线合同

> 历史合同：D0Slice、CombatLab 和对应安装器已经退役。当前正式主线以 `Config/FormalEncounter`、RoomDefinition、Art Scene、Boot 与 FormalRoom 的 committed authored 资产为准；不得恢复本文件中的旧生成流程。

## 目的与边界

本合同将 D0 定义为首条可复制的角色、怪物与策划配置生产线。Fei（30048）与 Burstbug（1001003）仍是标准战斗基准；Hudie 独立出场及 Luan → Hudie 替换场景用于验证同一 SpawnSlot/EntityPrefab 合同的复用。D0 不授权第二玩家角色、玩家自由移动、追踪、转向、导航、多敌同时在场、多波次、构筑或地图路线。

唯一运行时入口为：

```text
BattleScenarioConfig
  → D0CombatScenarioDefinition
    → D0StageDefinition（环境 + SpawnPoint）
    → D0CharacterDefinition + playerSpawnPointId
    → D0EncounterDefinition（SpawnSlot）→ D0EnemyDefinition（EntityPrefab）
    → D0ThreeCProfile / D0EnemyBehaviorProfile / D0EnemyAttackDefinition
```

不得为 D0 新建绕开该图谱的平行 JSON、场景常量或临时 Inspector 字段。玩家主射 Socket 与瞬时表现字段见 [D0 Fei 主射表现配置](D0_Fei_Attack_Presentation_Configuration.zh-CN.md)；本合同不重复定义副射，正式规范统一见 [FPG Formal Encounter 运行合同](../../Assets/FPGDemo/Docs/Workflow/FPG_Formal_Encounter_Runtime_Contract.zh-CN.md#fei-副射唯一规范)。

## 资产交付合同

| 项目 | 角色（Fei） | 怪物（Burstbug） | 必须验证 |
| --- | --- | --- | --- |
| 来源与授权 | 源文件、来源、授权范围、版本号 | 同左 | 来源可追溯；外部原始资源不进入发布包 |
| 命名 | `D0_Fei_*`；稳定角色 ID `fei-30048` | `D0_Burstbug_*`；稳定怪物 ID `burstbug` | 文件、Prefab、配置 ID 不重名 |
| 比例与朝向 | 以 CombatLab 固定镜头为基准 | 同左 | 正面朝镜头；缩放不承担命中范围调整 |
| Spine | idle、主射、换弹 play/ready、正式技能动作、受击、胜败；事件名记录 | enter、idle、受击、groggy、death、fast、volley、heavy；事件名记录 | 动画名与正式角色表现和技能合同一致 |
| 锚点 | 派生角色 Prefab 的固定 `PeekRoot`、技能 Socket 与 `CoverRoot` | `EntityPrefab` 内的视觉根、gameplay 根、投射锚点与弱点锚点 | 仅 `VisualRoot` 与表现枪口代理随 `PeekRoot` 移动；权威 Socket、命中体、控制器、AimAnchor 和 CameraPivot 保持静止 |
| 命中 | 不以视觉网格替代碰撞 | `EntityPrefab` 内 Body 2001、Weakpoint 2002 保持独立 | 命中体跟随 gameplay 根，且不由 Stage、Encounter 或 VFX 创建 |
| 特效/音频槽 | 主射、通用技能、命中、护盾、受击 | 入场、快攻、弹幕、重型、Break、死亡 | 每个招式写明 VFX 槽和音频 Cue 槽 |
| Prefab | D0 派生 Spine Prefab，根节点带 `D0ActorPresentationSockets` | `D0EnemyDefinition.EntityPrefab` 与固定池 FX Prefab | EntityPrefab 自包含视觉/gameplay 两分支；技能 source socket 可解析 |
| 性能 | 不在战斗热路径 Instantiate/Destroy | 同左 | 池化容量、GC、峰值实例数记录 |
| 证据 | 1080p 截图、录屏、Player.log | 同左 | 证据索引可定位到版本与配置 ID |

来源、许可与发布隔离的当前静态盘点见 [D0 资源来源与发布隔离审计](D0_Asset_Provenance_Audit.zh-CN.md)。在负责人确认前，D0 Spine 派生产物与其本地输入都不是可发布资源。

## 舞台、生成与实体所有权

| 配置层 | 唯一职责 | 明确不拥有 |
| --- | --- | --- |
| `D0StageDefinition` | 环境图层，以及相对 `ActorsRoot` 的具名 `SpawnPoint` 位置与朝向 | 玩家/敌人选择、视觉偏移、Prefab、生成 Tick、技能 Socket、特效、命中体 |
| `D0CombatScenarioDefinition` | 组合本场玩家、遭遇和舞台；以 `playerSpawnPointId` 选择玩家 gameplay 出生点 | 玩家视觉摆位和技能表现参数 |
| `D0EncounterDefinition` | 用 `SpawnSlot` 编排敌人定义、`spawnPointId`、`spawnTick` 和姿态策略；另行编排攻击时间表 | 出生点坐标、敌人视觉层级、枪口、弱点和命中体 |
| `D0EnemyDefinition` | 绑定敌人数值、行为、表现和完整 `EntityPrefab` | 关卡环境与本场生成时机 |
| `D0EnemyDefinition.EntityPrefab` | 拥有独立的视觉根、identity gameplay 根、投射物生成锚点、弱点锚点及 Body/Weakpoint 命中体 | 生命、Break、AI 和 `RuntimeId` 权威；这些仍在战斗域 |
| 玩家角色表现/技能 | 拥有角色视觉局部姿态、技能 Socket 和技能引用 | Stage 临时枪口或技能目标代理 |

`D0EnemyEntityWorld` 在会话准备时按 Encounter 的 SpawnSlot 实例化各敌人的 EntityPrefab，只激活当前槽位并把 gameplay 绑定交给当前 `EnemyRuntime`。`AtSpawnPoint` 将实体放到所选 SpawnPoint；`InheritPreviousGameplayPose` 在替换/孵化时继承上一活动实体的 gameplay 世界姿态。Stage 始终只提供可复用的空间点，不知道谁会在何时使用它。

## CZN 导入子流程

CZN 资源导入不是本合同的替代品，而是“源文件与 Spine 交付”的子流程。执行顺序为：

1. 按 [CZN 角色 Spine / Unity 导入手册](CZN_Character_Spine_Unity_Import_Runbook.zh-CN.md) 或 [CZN 怪物导入说明](CZN_Monsters_8_Unity_Import_Guide.zh-CN.md) 完成只读提取、转换、依赖审计和回读。
2. 从导入产物创建 D0 自有的直 alpha / Prefab / 材质 / 特效池，不让 CombatLab 直接依赖外部 canonical 资源。
3. 补齐本合同的动画、锚点、槽位、性能和证据清单。
4. 将通过验收的资产绑定到 `D0ActorPresentationDefinition`；正式玩家 Entity Prefab 直接 authored Socket provider，再交由角色定义引用。

## 角色 3C 与统一相机配置流程

`D0ThreeCProfile` 是 Fei 的唯一 3C 配置资产，也是已安装 D0 CombatLab 的唯一相机配置来源。相机机位、镜头和手感参数不能再分别写在 D0 舞台资产、场景或 D0 脚本常量中。它必须同时定义：

- 固定的画面下方角色构图与镜头焦点；角色根不参与自由移动。
- `CameraPivot` 相对玩家的局部位置与旋转，以及 Main Camera 相对 Pivot 的局部位置与旋转。
- Main Camera 的 FOV、近裁剪距离和远裁剪距离。
- 准星安全区、灵敏度、最大瞄准距离和输入缓冲 Tick。
- 探身/缩回的表现节奏；进入耗时 `0.08s`，缩回为下一已提交 Tick 瞬时归位，且不能驱动 gameplay 根位移。
- 护盾淡入、淡出、颜色与不透明度表现。
- 射击镜头反馈幅度及回正时间。

当前调参入口是在 Project 中打开 `Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_ThreeC.asset`。Room Editor 的正式镜头预览与 FormalRoom Play Mode 都读取该资产；不要在 Art Scene 或场景 Camera 上保留第二份相机数值。完整字段与生效时机见 [D0 3C 运行时预览与调参流程](D0_ThreeC_Runtime_Preview_Configuration.zh-CN.md)。

运行时规则：准星和输入缓冲由 3C Profile 应用；攻击查询最大距离由 3C Profile 覆盖。Fei 攻击或瞄准时，只把 `PeekRoot` 沿本地 `+X` 移动 `1.35`；`GameplayRoot`、命中体、CharacterController、AimAnchor、CameraPivot 与权威 `SocketRegistry` 不移动。尚未完成 `0.08s` 探身时，正式输入门控按原始 InputSequence 暂存首发，最多等待 5 个 60Hz Tick；快速点按也会完成该发，并于开火后的下一 Tick 归位。表现枪口代理只改变 VFX 和射击轨迹的显示起点，攻击查询、投射物路径、碰撞与命中位置仍使用权威锚点。

`CoverRoot` 是无 Collider/Rigidbody 的纯表现掩体。护盾有效且未锁定时墙体留在原位，角色从右侧探出；护盾耗尽或锁定时墙体同 Tick 隐藏，轮廓按 3C 淡出。普通有盾状态在最后实际攻击 Tick 后恢复 `Withdrawn`；破盾/锁定是已确认例外：松开后视觉仍瞬时归位，但确定性承伤状态继续保持 `Exposed`。暂停冻结当前过渡，终局、解绑、禁用与重启必须立即隐藏墙并恢复 authored 姿态。

## Burstbug 行为与招式配置流程

`D0EnemyBehaviorProfile` 定义：场外入场偏移、左右巡航锚点、入场/巡航速度、攻击停驻、后摇后恢复和死亡退场。行为控制器对当前 EntityPrefab 拥有的视觉根与 gameplay 根应用同一确定性偏移，保证视觉、Body 和 Weakpoint 同步。

`BattleSessionHost` 在每个战斗 Tick 的空间查询之前调用 `D0EnemyBehaviorController`。因此当前敌人 EntityPrefab 的视觉根和 gameplay 根在同一确定性 Tick 上更新；运行中的 `Update` 不再移动命中体，避免渲染帧率改变射击命中结果。

`BattleScenarioConfig.UsesAuthoredScenario` 是 D0 运行时组合的唯一开关。它为真时，`BattleSceneContext` 必须显式绑定 Stage SpawnPoint、`D0EnemyEntityWorld` 与已启用的 `D0EnemyBehaviorController`；当前活动敌人的视觉/gameplay 根必须来自 Encounter 所引用 `D0EnemyDefinition.EntityPrefab`。D0 专用预检还要求 `CombatAimReticle`、`PlayerWeaponPresentationController`、射击视图根、角色 Prefab Socket provider、主相机、瞄准锚点和 `BattlePresentationCoordinator` 均存在，并与同一 Host、场景父子关系和表现所有权合同一致。技能 source socket 必须由当前玩家表现资产声明并在 provider 中可解析。`BattleSessionHost.TryInitialize` 会在创建 Session 前执行这些预检；不得再用场景临时节点、运行时猜测或静默 `null` 回退补齐缺失所有权。

运行中若外部脚本禁用或销毁该行为控制器，Host 必须记录错误、停止 Tick 并走既有 `OnDisable → Shutdown` 清理路径；不得继续以空观察器推进静止敌人的战斗。非 authored/legacy 场景保持原有空观察器路径。

旧 D0 舞台安装器已经删除，不得恢复或用生成器修复场景。正式环境由 Art Scene authored，出生点和遭遇由 RoomDefinition/Encounter 资产 authored，Boot 与 FormalRoom 直接维护技术绑定；缺少必要绑定时必须由合同校验拒绝运行，并在对应权威资产中显式修复。

Burstbug 固定状态机：

```text
场外入场 → 左右循环横移 → 发现 Telegraph/Windup/Release/Recovery → 原地停驻
  → 后摇完成 → 按原方向继续横移 → 胜利时延迟死亡退场
```

不实现目标追踪、转向、导航、随机闪避或瞄准公平性推断。

每个 `D0EnemyAttackDefinition` 必须配置：

1. 稳定攻击 ID、显示名、攻击语言与动画槽。
2. 预警槽、预警 Tick、前摇 Tick、后摇 Tick、恢复规则。
3. 攻击载荷、伤害/Break 结果、可拦截与耐久信息。
4. 特效槽与音频 Cue 槽。
5. 可选的 `animationMotion`：按技能决定是否采用 Spine 标记骨位移；关闭时不影响动画播放。
6. `animationMotionStartPhase`：从前摇或释放 Tick 开始采样。动画位移与程序行为/技能位移相加，详细合同见 `D0_Skill_Animation_Motion_Configuration.zh-CN.md`。

标准样板中，`Burstbug Interceptable Volley` 耐久为 `4`，等于 Fei 一发基础主射伤害；“看见预警 → 瞄准 → 一发主射拦截”因此是可重复验证的明确应对。

`D0EncounterAttackScheduleEntry` 只保存顺序、触发 Tick 和攻击资产引用。禁止在遭遇表复制动画、预警、载荷或 VFX/音频参数。

`threatSchedule` 是旧兼容字段；D0 标准战斗必须保持它为空，并且只使用 `attackSchedule`。配置验证器会拒绝双重时序来源、重复攻击定义 ID、动画槽与角色表现不一致、或攻击语言与载荷／特效／音频合同不一致的资产。

## 标准战斗验收流程

CombatLab 必须按以下顺序可重复运行：

1. Fei 固定在画面下方，Burstbug 从场外进入初始巡航点。
2. Burstbug 完成至少一次左/右巡航、停攻、后摇恢复的完整循环。
3. 依次出现快攻、可拦截弹幕、重型弱点/Break 三种攻击语言；随后可重复此循环。
4. 玩家可完成瞄准、射击、技能响应、快速点按、探身、瞬时缩回和 Break；破盾后视觉归位但保持 `Exposed`，战斗可走向胜利、失败和 F5 重开。

战斗时长不设硬门槛。验收以流程完整性、攻击可读性、3C 手感和生产链可复现性为准。

## 交付检查表

- [ ] 角色/怪物源文件、授权与版本记录完整。
- [ ] Spine 动画、事件、锚点、命中盒、特效和音频槽均通过配置验证。
- [ ] `BattleScenarioConfig → D0CombatScenarioDefinition` 可以构建战斗定义。
- [ ] Stage 只含环境与 SpawnPoint；Scenario 的 `playerSpawnPointId` 和 Encounter 的每个 SpawnSlot 都能解析到该 Stage。
- [ ] 每个 `D0EnemyDefinition.EntityPrefab` 自包含视觉根、gameplay 根、投射锚点、弱点和 Body/Weakpoint 命中体。
- [ ] 玩家技能 Socket 与瞬时表现只从角色技能表现配置解析，场景中不存在临时枪口或目标代理。
- [ ] 3C、行为、招式和遭遇均是可追溯 ScriptableObject，不存在场景硬编码副本。
- [ ] EditMode / PlayMode 验证已运行；无项目错误。
- [ ] Windows 1080p Release 证据包含无持续 GC、无运行时 Instantiate/Destroy、无池扩容的报告。
- [ ] 5 人 × 3 次试玩记录、截图/录屏、Player.log 已写入证据索引。
