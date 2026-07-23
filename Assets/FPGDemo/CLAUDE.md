# FPGDemo 指南

这个目录是独立的 FPG 战斗 demo harness，命名空间前缀是 `FPG.Demo`，不要和 `Assets/Scripts/Combat`、`Assets/Scripts/Battle` 或 `Assets/Scripts/Prototype` 的当前原型代码混用。

## 目录边界

- `Runtime/Core/` 是无 UnityEngine 依赖的基础类型、确定性随机、tick、坐标和 ID，asmdef 是 `FPG.Core`。
- `Runtime/Combat/` 是无 UnityEngine 依赖的伤害、投射物、目标选择、战斗队列和命中合同，asmdef 是 `FPG.Combat`。
- `Runtime/Player/` 与 `Runtime/Enemy/` 是无 UnityEngine 依赖的玩家武器、曝光状态、敌人和 threat runtime，分别依赖 Core/Combat。
- `Runtime/Run/` 编排 `BattleSession`、场景定义、空间查询端口、投射物世界端口和 replay/transcript，不能反向依赖 Unity 层。
- `Runtime/Unity/` 是 Unity 桥接层，负责 Boot、会话 host、输入快照、正式玩家 tick/port/presentation 组合、物理查询、表现池和 HUD；改这里先读该目录指南。
- `Runtime/Unity/Config/` 放 D0 与正式遭遇的 ScriptableObject 定义和运行时适配器，命名空间是 `FPG.Demo.Unity`；改这里先读该子目录指南。
- `Runtime/Unity/Level/` 是 FPGDemo 自己的房间运行时与正式遭遇桥接层，命名空间仍是 `FPG.Demo.Unity`；改这里先读该子目录指南，不要和根项目 `NewFPG.Level` 混用。
- `Config/` 放 `BattleScenarioConfig`、`BattlePresentationCatalog` 和 `GameBootstrapConfig` 的默认资产。
- `Config/FormalEncounter/` 放正式遭遇敌人、池、Profile、Override、Catalog 和 Level1 预设资产；改这里先读该子目录指南。
- `Config/Level/` 放 FPGDemo 房间、房间组和房间标签资产，`roomId`、marker ID、group/tag ID 都是运行时和 Editor 工具共享的稳定合同。
- `Presentation/` 放 demo 专用材质、投射物/预警/命中特效 prefab 和 D0 派生表现资源；改这里先读该目录指南。
- `Presentation/FormalEncounter/` 放正式遭遇实体、血条、出口和配套材质 prefab；改这里先读该子目录指南。
- `Editor/LevelAuthoring/` 放 FPGDemo 房间编辑器、Scene View 标记工具、CombatLab 房间安装器和 Formal Encounter 预览/试玩桥；改这里先读该子目录指南。
- `Scenes/Boot.unity`、`Scenes/CombatLab.unity` 和 `Scenes/FormalRoom.unity` 是当前 build 场景入口；场景职责和验证入口见 `Scenes/CLAUDE.md`。
- `Tests/EditMode/` 与 `Tests/PlayMode/` 是该 harness 的主要验收入口；测试 asmdef 保持 `autoReferenced=false`，只显式引用需要验证的本目录运行时程序集和 NUnit。

## 工作规则

- 保持 asmdef 依赖方向：Core -> Combat -> Player/Enemy -> Run -> Unity；无引擎领域层保持 `noEngineReferences: true`。
- 不要从 `FPG.Demo.*` 直接引用 `NewFPG.*` 原型模块，也不要让 `Assets/Scripts/*` 反向依赖这个 demo harness。
- Boot/CombatLab、Tag/Layer、Build Settings 和表现 prefab 的变更直接通过 Unity Editor 或 Unity MCP 落盘，避免手动大范围改 scene/prefab YAML。
- D0Slice 战斗配置、CombatLab 场景和派生表现资源优先通过 `FpgDemoD0SliceInstaller.cs`、`FpgDemoD0StageInstaller.cs` 和 `D0PlannerConfigurationValidator.cs` 维护；`Assets/FPGDemo/Presentation/` 下放 demo 派生资源，不能直接绑定 `Assets/Art/Monster` 里的原始 PMA Spine 资源。角色与怪物的人工 Entity Prefab、Generated Render Prefab 和 Installer 边界见 `Docs/Workflow/D0_Entity_Prefab_Authoring.zh-CN.md`。
- 房间/正式遭遇流程涉及 `Runtime/Unity/Config/`、`Runtime/Unity/Level/`、`Config/Level/`、`Config/FormalEncounter/`、`Presentation/FormalEncounter/`、`Editor/LevelAuthoring/` 和 `Scenes/`；先确认配置、运行时、场景绑定、表现 prefab 与预览工具各自职责，不要用场景 YAML 或临时脚本绕过 `FpgRoom*` / `FpgFormal*` 合同。
- `BattleSessionHost` 是 Unity 场景和纯运行时会话的边界；空间查询、投射物世界、输入和表现 feed 的生命周期要在这里保持 fail-closed。
- Presentation 树只放视觉和 HUD，不要挂 Collider、Rigidbody 或 gameplay state；物理查询绑定由 HitboxRegistry 和场景中的明确 hitbox 管理。
- 改配置资产时同步检查对应 runtime `TryValidate`/`TryCreateDefinition` 逻辑，不要只修 Inspector 默认值。
- D0 策划资产的 Inspector 字段必须使用 `D0PlannerField` 提供中文显示名与基于真实数据流的中文说明；新增字段先明确单位、约束、条件和生效方式。保留原字段名/YAML 键，不用重命名迁移来做本地化；工程容量、LayerMask、物理、命中盒和运行时状态用 `D0PlannerTechnicalField` 明确隔离。
- 制作或修改 D0 功能时，先判断是否需要让策划调整其数值、行为、表现或遭遇编排；需要开放时，复用 `BattleScenarioConfig → D0CombatScenarioDefinition` 及其 Profile/Definition 引用链，不建立平行入口。同步更新对应的策划配置说明文档，至少写清资产入口、引用关系、字段含义/单位/默认值/约束/生效条件、创建步骤和可验证结果；具体模板见 `Docs/Workflow/Planner_Configuration_Delivery_Guide.zh-CN.md`。
- 修改正式关卡的陆鸾召唤蝴蝶配置时，先读 `Docs/Workflow/FPG_Level1_Encounter_Presets_Configuration.zh-CN.md`；正式运行走 `FpgEnemyAttackDefinition -> FpgSummonActionDefinition -> FpgEnemyDefinition`。`D0_Luan_SummonHudie.asset` 只作为安装器迁移源，CombatLab 不再提供陆鸾到蝴蝶的替换 Scenario。

## 验证映射（仅在用户明确要求自动测试时使用）

- 项目根目录的“验收与测试统一分工”优先：默认不新增测试代码、不批量运行 EditMode/PlayMode，也不由 Agent 代做 CombatLab 试玩。所有手感、可读性、交互、战斗公平性与主管判断项应记录为待主管试玩，并按 `Docs/Workflow/Testing_Handoff_Policy.zh-CN.md` 提供交接表。
- 改 asmdef 或目录依赖后，运行 `Assets/FPGDemo/Tests/EditMode/AssemblyBoundaryTests.cs`。
- 改 build scene 顺序或 Boot、CombatLab、FormalRoom 场景入口后，运行 `BuildSettingsTests.cs` 和 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
- 改 `BattleSession`、run contracts、空间查询或投射物世界后，优先跑 `BattleSession*`、`WP4ContractTests.cs`、`UnityAttackQueryPortTests.cs` 和 `UnityProjectileWorldPortTests.cs`。
- 改玩家武器、投射物表现、HUD 或视觉池后，优先跑 `PlayerWeaponPresentationControllerTests.cs`、`ProjectilePresentation*Tests.cs` 和 PlayMode 场景合同。
- 改场景或表现安装器后，在 Unity 中执行对应 `FPG Demo/...` 菜单项，再检查 Console 和 PlayMode 场景合同。
- 改房间定义、LevelAuthoring 工具、Formal Encounter 接入或 CombatLab/FormalRoom 房间绑定后，优先跑 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`；若同时改 D0 场景/阶段配置，再补 `D0CombatScenarioDefinitionTests.cs` 与 `D0StageDefinitionTests.cs`。
