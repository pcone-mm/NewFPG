# FPGDemo Runtime/Unity/Level 指南

这个目录是 FPGDemo 的房间运行时、CombatLab 房间绑定和 Formal Encounter 桥接层，asmdef 是 `FPG.Unity`，命名空间保持 `FPG.Demo.Unity`。它不是根项目 `Assets/Scripts/Level/` 的 `NewFPG.Level` 原型模块。

## 目录边界

- `FpgRoomDefinition`、`FpgRoomGroupDefinition`、`FpgRoomTagDefinition`、`FpgRoomMarkers` 和 `FpgRoomValidation` 定义房间资产的序列化合同与校验。
- `FpgRoomInstance`、`FpgRoomExitRuntime`、`FpgRoomCombatLabBinding` 负责把 `Config/Level/` 房间资产解析到 CombatLab 场景对象。
- `FpgRoomEncounterValidator`、`FpgRoomEncounterDirector`、`FpgFormalEncounterHost` 和 `FpgFormalEncounter*` adapter/override 负责把房间、D0 scenario 和正式遭遇计划接到 `BattleSession`。

## 工作规则

- `roomId`、marker ID、group/tag ID 是资产、场景绑定、预览和试玩 override 共享的稳定合同；复制资产时必须生成新 ID，不要通过 YAML 复制保留旧 ID。
- 新增序列化字段时同步 `TryValidate`/`Validate`、中文 `D0PlannerField` 说明和 Editor schema 映射；校验失败要 fail-closed。
- `FpgRoomPlaytestOverrides` 与 `FpgFormalEncounterPlaytestOverrides` 只用于 Editor 试玩桥，使用后必须清理，不能成为运行时全局状态入口。
- 这里可以引用 `FPG.Core`、`FPG.Combat`、`FPG.Player`、`FPG.Enemy`、`FPG.Run` 和 Unity 层依赖；不要反向引用 `NewFPG.*` 原型模块。

## 验证

- 房间 runtime、CombatLab 绑定或 Formal Encounter 桥接变化，优先看 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
- 改 scenario/stage 与房间兼容关系时，再看 `Assets/FPGDemo/Tests/EditMode/D0CombatScenarioDefinitionTests.cs` 和 `Assets/FPGDemo/Tests/EditMode/D0StageDefinitionTests.cs`。