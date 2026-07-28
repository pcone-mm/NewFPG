# Config/Level 指南

本目录保存正式房间、房间组、标签、catalog 与出口刷新规则。

- 默认房间是 `Rooms/Room_forest.asset`，稳定 ID 为 `room-forest`。
- 每个 `FpgRoomDefinition` 通过稳定 GUID 与 `Assets/` 相对路径一对一引用自己的 Art Scene；路径由 Editor 同步，运行时不回退到旧 `environmentPrefab`。
- `FPG_RoomCatalog.asset` 与 `FPG_ExitRoomRefreshRule.asset` 共同定义清场后的候选房间和确定性出口刷新；catalog 中的 Art Scene GUID、路径和 scene name 必须各自唯一。
- room/group/tag/marker ID 是跨资产合同；复制资产时必须生成新 ID。
- Enemy spawn role 必须与正式 Encounter Profile/Override 和 spawn role 预检一致。
- 通过 Room Editor 的 SceneAsset 字段、Scene Tool 或 Unity MCP 修改，不复制 YAML，也不只改 GUID/path 其中一侧。
- 验证以 Room Editor、Unity 编译/Console、`FpgRoomDefinitionTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgExitRoomRefreshRuleTests.cs`、`FpgRoomExitRuntimeTests.cs` 为准。
