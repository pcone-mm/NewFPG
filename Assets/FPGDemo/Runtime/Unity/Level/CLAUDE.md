# Runtime/Unity/Level 指南

本目录负责正式房间、出口、玩家组合和 Encounter 桥接，不再承担 CombatLab 或 D0 scenario 运行职责。

- `FpgRoomDefinition/Markers/Validation` 定义房间序列化合同。
- `FpgRoomInstance`、`FpgRoomExitRuntime`、catalog 和刷新规则负责加载环境、出口与确定性选房。
- `FpgFormalEncounterHost`、`FpgRoomEncounterDirector` 和 adapters 把房间/Profile/Override 连接到 `FpgEncounterSession`。
- `FpgFormalPlayerComposer` 在 inactive staging root 完成玩家校验和组合后才激活实体。
- room/group/tag/marker ID 是资产、预览和运行时共享合同；复制时必须生成新 ID。
- Editor playtest override 只用于临时正式预览，使用后必须清理，不能成为全局运行入口。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgRoomExitRuntimeTests.cs` 为准。
