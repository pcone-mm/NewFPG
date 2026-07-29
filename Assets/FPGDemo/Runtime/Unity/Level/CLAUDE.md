# Runtime/Unity/Level 指南

本目录负责正式房间、出口、玩家组合和 Encounter 桥接，不再承担 CombatLab 或 D0 scenario 运行职责。

- `FpgRoomDefinition/Markers/Validation` 定义房间序列化合同；Art Scene 引用必须同时持有稳定 GUID 与运行时路径。
- `FpgRoomInstance` 只解析 gameplay marker 并实例化可破坏槽位，不实例化旧环境 prefab；catalog 和刷新规则负责确定性选房。
- `FpgRoomArtSceneLoader` 由 FormalRoom 唯一拥有，负责 additive load/unload、active scene 切换、`LightProbes.Tetrahedralize` 和失败回滚；不得接管 Boot/FormalRoom 生命周期。
- 每个 Art Scene 必须只有一个 identity `FpgRoomArtRoot`，并通过 `IFpgRoomArtPresentationBinding` 显式绑定/解绑 Formal Camera、主方向光与瞄准视口；表现绑定不能反写 gameplay 状态。
- `FpgFormalEncounterHost`、`FpgRoomEncounterDirector` 和 adapters 把房间/Profile/Override 连接到 `FpgEncounterSession`。
- `FpgFormalPlayerComposer` 在 inactive staging root 完成玩家校验和组合后才激活实体。
- room ID 与 Art Scene GUID 是资产、预览和运行时共享身份，整房复制时必须生成新的两者；marker ID 只在房间内作用，可以保留，group/tag 引用默认共享。
- Editor playtest override 只用于临时正式预览，使用后必须清理，不能成为全局运行入口。
- `FpgRoomTransitionCurtain` 归 Boot 所有，跨房时先遮罩，再卸载旧 Art Scene、装载新 Art Scene、重建遭遇，最后揭幕；任一步失败都保持 fail-closed。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomArtSceneLoaderPlayModeTests.cs`、`FpgRoomExitRuntimeTests.cs` 为准。
