# Config/Level 指南

本目录保存正式房间、房间组、标签、catalog 与出口刷新规则。

- 默认房间是 `Rooms/Room_forest.asset`，稳定 ID 为 `room-forest`。
- `FPG_RoomCatalog.asset` 与 `FPG_ExitRoomRefreshRule.asset` 共同定义清场后的候选房间和确定性出口刷新。
- room/group/tag/marker ID 是跨资产合同；复制资产时必须生成新 ID。
- Enemy spawn role 必须与正式 Encounter Profile/Override 和 spawn role 预检一致。
- 通过 Room Editor、Scene Tool 或 Unity MCP 修改，不复制 YAML。
- 验证以 Room Editor、Unity 编译/Console、`FpgRoomDefinitionTests.cs`、`FpgExitRoomRefreshRuleTests.cs`、`FpgRoomExitRuntimeTests.cs` 为准。
