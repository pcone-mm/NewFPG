# Runtime/Unity/Level 指南

本目录负责正式房间、出口、玩家组合和 Encounter 桥接，不再承担 CombatLab 或 D0 scenario 运行职责。

- `FpgRoomDefinition/Markers/Validation` 定义房间序列化合同；Art Scene 引用必须同时持有稳定 GUID 与运行时路径。Cover slot 必须有有效 `FpgCoverEntityView` prefab、`FpgCoverCameraProfile`、正耐久、有限的独立玩家到达 pose，并满足唯一初始项与唯一到达 X。
- `FpgRoomInstance` 只解析 gameplay marker 并实例化可破坏物与 cover slot，不实例化旧环境 prefab；它按 marker ID 解析 Profile，并由玩家世界 Pose 生成不可变 Camera Shot。cover view 只按领域 snapshot 切换完好/损毁根和阻挡 collider。
- `FpgRoomArtSceneLoader` 由 FormalRoom 唯一拥有，负责 additive load/unload、active scene 切换、`LightProbes.Tetrahedralize` 和核心加载失败回滚；表现绑定或 Light Probe 更新异常只警告，不得阻断房间进入。
- 每个 Art Scene 必须只有一个 scene-root `FpgRoomArtRoot`，并通过 `IFpgRoomArtPresentationBinding` 显式绑定/解绑 Formal Camera、可选的 `RenderSettings.sun` 与瞄准视口；表现绑定不能反写 gameplay 状态，单个表现适配器异常不得阻断房间加载。
- `FpgFormalEncounterHost`、`FpgRoomEncounterDirector` 和 adapters 把房间/Profile/Override 连接到 `FpgEncounterSession`。
- `FpgRoomEncounterDirector` 每房创建并绑定一个 `FpgCoverRuntime`；`FpgCoverTraversalPresenter` 只在已配置的到达 pose 之间移动玩家视觉并播放 transition VFX，不能决定可选中性、耐久、完成 tick 或相机状态。镜头过渡由上级 `FpgFormalPlayerCameraFeedback` 独立提交。
- `FpgFormalPlayerComposer` 在 inactive staging root 完成玩家校验和组合后才激活实体。
- room ID 与 Art Scene GUID 是资产、预览和运行时共享身份，整房复制时必须生成新的两者；marker ID 只在房间内作用，可以保留，group/tag 引用默认共享。
- Editor playtest override 只用于临时正式预览，使用后必须清理，不能成为全局运行入口。
- `FpgRoomTransitionCurtain` 归 Boot 所有，跨房时先遮罩，再卸载旧 Art Scene、装载新 Art Scene、重建遭遇，最后揭幕；任一步失败都保持 fail-closed。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs`、`FpgCoverCameraProfileTests.cs`、`FpgFormalCameraPoseUtilityTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgEntityPrefabContractTests.cs`、`FpgMultiEnemyCombatTransactionTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomArtSceneLoaderPlayModeTests.cs`、`FpgRoomExitRuntimeTests.cs` 为准。
