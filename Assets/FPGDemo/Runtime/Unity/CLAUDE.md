# FPGDemo Runtime/Unity 指南

本目录是 `FPG.Unity` 程序集的 Unity 桥接层，命名空间保持 `FPG.Demo.Unity`。纯战斗规则仍由 Core/Combat/Player/Enemy/Run 程序集负责；这里把场景、Input System、物理查询和表现生命周期接到这些合同上。

## 职责边界

- `GameBootstrap`、`GameBootstrapConfig`、`FpgBootCharacterChoice` 和 `FpgBootRoomEntrance` 负责 Boot 选择、帧率配置和场景进入；角色必须通过 `Config/FpgPlayableCharacterCatalog.cs` 解析。
- `BattleSessionHost` 与 `BattleSceneContext` 是 CombatLab harness 的会话边界；FormalRoom 使用 `FpgEncounterHost`、`Level/FpgFormalEncounterHost` 和正式玩家服务链，不要混入旧 host/context。
- `UnityBattleInputSource` 保存有界的 gameplay edge 队列、独立的 pause/restart latch 和量化 aim pose；`ProjectWideBattleInputAdapter` 只负责从项目 Input System 动作映射输入。
- `FpgFormalPlayerTickDriver` 提交确定性 tick input 和 action event；`FpgFormalCombatPortFactory` 创建查询、投射物、hitbox 与 tick 同步端口；presentation bridge、camera feedback 和 HUD 只消费运行时快照/事件。
- `Config/` 与 `Level/` 有更具体的配置和房间/玩家组装规则，进入子目录前继续读取对应指南。

## 工作规则

- 保持 `FPG.Unity` 只依赖本 harness 的领域程序集、Spine、Input System 和 UGUI；不要引用 `NewFPG.*` 原型模块，也不要让领域程序集反向依赖 Unity 层。
- 不要把输入 edge 改成单帧覆盖布尔值：同一渲染帧可能跨多个 battle tick，gameplay edge、控制 latch 和 aim pose 有不同消费语义；保持 planner-authored tick 容量、预分配存储和无分配消费路径。
- FormalRoom 的玩家绑定是运行时状态。先由 composer 完整校验并配置 factory/driver/director/bridge，再激活实体和表现；场景资产中保持这些引用未配置。
- pause、restart、disable 或重新组装时清理事件订阅、输入状态和 runtime bundle；presentation 组件不得拥有战斗决策或长期 gameplay state。

## 验证

- 输入快照、edge 队列或 aim pose 变化看 `Assets/FPGDemo/Tests/EditMode/UnityBattleInputSourceTests.cs`。
- Boot 配置、角色 catalog 或 FormalRoom authoring 变化看 `GameBootstrapConfigTests.cs` 与 `FormalFirstAuthoringContractTests.cs`。
- 会话、玩家服务绑定或生命周期变化看 `PlayerEntitySceneServiceBindingTests.cs` 与 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
