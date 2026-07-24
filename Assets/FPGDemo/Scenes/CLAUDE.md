# FPGDemo Scenes 指南

本目录只保存 FPGDemo 正式可运行场景。正式入口固定为 `Boot -> FormalRoom`，不要重新引入根项目旧场景或 CombatLab。

## 场景合同

- `Boot.unity` 与 `FormalRoom.unity` 分别固定为 build index 0 和 1。
- Boot 通过 `FpgPlayableCharacterCatalog` 解析角色；预览对象仅用于展示，不承载 gameplay 状态。
- FormalRoom 由 `FpgEncounterHost`、`FpgFormalEncounterHost`、`FpgRoomEncounterDirector` 和 `FpgEncounterSession` 组成唯一运行链。
- 默认房间链是 `Boot -> FormalRoom -> room-forest`。修改稳定 ID 时同步检查 `Room_forest.asset`、房间 catalog 与场景绑定。
- 场景与 Build Settings 通过 Unity Editor、Unity MCP 或正式 installer 修改，不手工批量编辑 scene YAML。

## 验证

- 修改 build 入口后检查 `Assets/FPGDemo/Tests/EditMode/BuildSettingsTests.cs`。
- 修改 Boot 选择或 FormalRoom authoring 后检查 `Assets/FPGDemo/Tests/EditMode/FormalFirstAuthoringContractTests.cs`。
- 默认只执行 Unity 编译与 Console 检查；批量 EditMode/PlayMode 测试需用户明确要求。
