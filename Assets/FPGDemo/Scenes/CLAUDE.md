# FPGDemo Scenes 指南

本目录只保存 FPGDemo 正式入口与 Host 场景。正式入口固定为 `Boot -> FormalRoom`，房间 Art Scene 位于 `Presentation/Level/Rooms/` 并由 FormalRoom additive 加载；不要重新引入根项目旧场景或 CombatLab。

## 场景合同

- `Boot.unity` 与 `FormalRoom.unity` 分别固定为 build index 0 和 1；`FpgProductionSceneList` 按 room ID 排序后追加 catalog 中全部 Art Scene。
- Boot 通过 `FpgPlayableCharacterCatalog` 解析角色；预览对象仅用于展示，不承载 gameplay 状态。
- FormalRoom 由 `FpgEncounterHost`、`FpgFormalEncounterHost`、`FpgRoomEncounterDirector` 和 `FpgEncounterSession` 组成唯一运行链。
- 默认房间链是 `Boot -> FormalRoom -> room-forest`。修改稳定 ID 时同步检查 `Room_forest.asset`、房间 catalog 与场景绑定。
- Boot 持有跨房切换遮罩；FormalRoom 持有唯一正式 Camera、Host 与 Art Scene loader，不持有房间环境根、主方向光或 `FpgRoomArtRoot`。
- 场景与 Build Settings 只能通过 Unity Editor 或 Unity MCP 显式修改；构建流程只校验生产场景列表，不自动重写资产或 scene YAML。

## 验证

- 修改入口、catalog 或 Art Scene 后检查 `Assets/FPGDemo/Tests/EditMode/BuildSettingsTests.cs` 与 `FpgRoomArtSceneContractTests.cs`。
- 修改 Boot 选择或 FormalRoom authoring 后检查 `Assets/FPGDemo/Tests/EditMode/FormalFirstAuthoringContractTests.cs`。
- 默认只执行 Unity 编译与 Console 检查；批量 EditMode/PlayMode 测试需用户明确要求。
