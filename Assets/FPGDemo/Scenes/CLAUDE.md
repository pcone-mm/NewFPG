# FPGDemo Scenes 指南

本目录只放 FPGDemo harness 的可运行场景，不要和根项目 `Assets/Scenes/` 的原型场景混用。

## 场景合同

- `Boot.unity`、`CombatLab.unity`、`FormalRoom.unity` 分别保持 build index 0、1、2；CombatLab 是 Editor/D0 harness，正式运行入口走 Boot 到 FormalRoom。
- Boot 的角色选择必须由 `FpgPlayableCharacterCatalog` 解析，预览只含视觉对象，不预放 `D0PlayerEntityView` gameplay 实体。
- `FormalRoom.unity` 的正式 host、player composer、director、port factory、tick driver、presentation bridge、pool、catalog 和 runtime catalog 保持单一服务链；玩家实体由运行时 selection 组装，不写入场景资产。
- 场景入口或 Build Settings 变更通过 Unity Editor、Unity MCP 或对应 installer 落盘，不要手改大段 scene YAML。
- `FormalRoom.unity` 的默认链路是 `Boot -> FormalRoom -> room-combatlab-forest`；改稳定 ID 时同步检查配置资产与场景绑定。

## 验证

- 改 Build Settings 或场景入口后，运行 `Assets/FPGDemo/Tests/EditMode/BuildSettingsTests.cs`。
- 改 Boot 选择、FormalRoom 玩家组装或 authoring 合同时，运行 `Assets/FPGDemo/Tests/EditMode/FormalFirstAuthoringContractTests.cs`。
- 改场景组件、唯一实例或房间绑定后，运行 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
