# Config/Level 指南

本目录保存正式房间、房间组、标签、catalog 与出口刷新规则。

- 默认房间是 `Rooms/Room_forest.asset`，稳定 ID 为 `room-forest`。
- 每个 `FpgRoomDefinition` 通过稳定 GUID 与 `Assets/` 相对路径一对一引用自己的 Art Scene；路径由 Editor 同步，运行时不回退到旧 `environmentPrefab`。
- `FPG_RoomCatalog.asset` 与 `FPG_ExitRoomRefreshRule.asset` 共同定义清场后的候选房间和确定性出口刷新；catalog 中的 Art Scene GUID、路径和 scene name 必须各自唯一。
- room ID 与 Art Scene GUID 是跨资产身份；整房复制时必须生成新的两者。marker ID 只在单个房间内生效，可以随房间保留；group/tag 引用默认共享，除非设计意图明确改变。
- 复制正式房间只使用 Room Editor 的 `Duplicate Room` / `FpgRoomAuthoringOperations`：修复 `FpgRoomArtRoot` 绑定，并把 catalog/Build Settings 注册视为同一事务；不要手工复制资产后单独改 Build Settings。
- Enemy spawn role 必须与正式 Encounter Profile/Override 和 spawn role 预检一致。
- 通过 Room Editor 的 SceneAsset 字段、Scene Tool 或 Unity MCP 修改，不复制 YAML，也不只改 GUID/path 其中一侧。
- 验证以 Room Editor、Unity 编译/Console、`FpgRoomDefinitionTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomDuplicationContractTests.cs`、`FpgRoomAuthoringSafetyTests.cs`、`BuildSettingsTests.cs`、`FpgExitRoomRefreshRuleTests.cs` 和 `FpgRoomExitRuntimeTests.cs` 为准。
