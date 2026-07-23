# FPGDemo Unity Config 指南

本目录放 Unity 配置定义与运行时适配器，命名空间统一为 `FPG.Demo.Unity`。

## 职责边界

- `D0*` 类型是 D0 策划配置入口；策划字段使用 `D0PlannerField`，技术字段使用 `D0PlannerTechnicalField`。
- `FpgPlayableCharacterCatalog` 通过稳定 character ID 解析 `D0CharacterDefinition`、`D0ThreeCProfile` 和 visual-only Boot 预览；Boot 选择与 FormalRoom 玩家组装必须共用这条 catalog 链。
- `FpgEnemy*`、`FpgEncounter*`、`FpgFormal*`、`FpgSummonActionDefinition` 和 `FpgWaveLayoutDefinition` 定义正式遭遇配置合同。
- `FpgFormalConfigAdapters` 只负责把配置转换为运行时数据，不在适配层引入新的战斗决策。
- `roomId`、character ID、enemy ID、pool ID、profile ID、override ID 和 catalog ID 是跨资产稳定合同；修改时同步检查引用方。
- 配置转换保持 fail-closed；缺少引用、ID 冲突或校验失败时，不得静默构造部分有效的正式遭遇。

## 验证

- 配置修改至少检查对应 `TryValidate` / `TryBuildData` 路径。
- 改 playable character catalog 或默认角色选择时，检查 `Assets/FPGDemo/Tests/EditMode/FormalFirstAuthoringContractTests.cs`。
- D0 配置优先映射到 `BattleScenarioConfigTests.cs`、`BattleScenarioConfigThreatScheduleTests.cs`、`D0CombatScenarioDefinitionTests.cs`、`D0StageDefinitionTests.cs` 和 `D0ScenarioPresentationResolverTests.cs`。
- 正式遭遇场景绑定变化再检查 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
