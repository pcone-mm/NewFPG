# FPGDemo Scenes 指南

本目录只放 FPGDemo harness 的可运行场景，不要和根项目 `Assets/Scenes/` 的原型场景混用。

## 场景合同

- `Boot.unity` 是 build index 0，负责进入 FPGDemo 流程。
- `CombatLab.unity` 是 build index 1，承载 D0/CombatLab 验证入口。
- `FormalRoom.unity` 是正式遭遇场景；正式 host、director、pool、catalog、runtime catalog 和 port 在场景中保持唯一实例。
- 场景入口或 Build Settings 变更通过 Unity Editor、Unity MCP 或对应 installer 落盘，不要手改大段 scene YAML。
- `FormalRoom.unity` 的默认链路是 `Boot -> FormalRoom -> room-combatlab-forest`；改稳定 ID 时同步检查配置资产与场景绑定。

## 验证

- 改 Build Settings 或场景入口后，运行 `Assets/FPGDemo/Tests/EditMode/BuildSettingsTests.cs`。
- 改场景组件、唯一实例或房间绑定后，运行 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
