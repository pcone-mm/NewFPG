# Level Presentation 指南

本目录保存正式房间环境资源与 additive Art Scene，不拥有正式 gameplay Host。

- `Covers/Prefabs/` 保存通用与房间特定的正式掩体 Entity，`Covers/VFX/` 保存掩体切换表现；`RoomExit/` 按 Prefab、Material、Shader 聚合正式房间出口表现。这些 authored 资产只承载几何、碰撞、anchor 与表现组件，耐久、选择、移动和房间流程状态属于 Run/RoomDefinition。
- `Rooms/` 下由 `FPG_RoomCatalog` 与 Build Settings 通过 GUID/path 注册的是正式 Art Scene；整房复制会按源场景名生成唯一 `_Copy` 路径，不得用文件名模式推断房间身份或所有权。`Environment/` 保存可复用源资源，不能再作为 `RoomDefinition` 的运行时 prefab 回退。
- `Environment/Generated/SpriteShadowCasters/` 保存由 Unity Editor 产出、按源 sprite GUID 命名的 scene-coupled mesh/material；当前正式 Art Scene 与 `PF_FPG_Root1TreeCover` 直接引用其 GUID。不要手改、重命名或按 hash 单独删除，重新生成或替换时必须让 asset、`.meta` 与全部消费者原子维护。
- `Environment/rootArt/root1/` 保存 root1 掩体变体使用的树/船分阶段源 sprite；源图、`.meta` 与 `Covers/Prefabs/` 下的 prefab 引用必须成套维护，不能把这些表现输入复制进 `RoomDefinition` 或运行时状态。
- `Covers/Prefabs/PF_FPG_DefaultCover.prefab` 是通用模板；`PF_FPG_Root1TreeCover.prefab`、`PF_FPG_BoatLeft.prefab` 与 `PF_FPG_BoatRight.prefab` 是 `root1.asset` 使用的房间特定变体。所有正式 `PF_FPG_*Cover` 都通过 `FpgCoverEntityView` 独占完好/损毁根和阻挡 collider。
- Cover blocker 优先使用 `intactRoot` 下名为 `__ShadowCasterProxy` 的 Mesh，否则使用可渲染 Mesh；每个源 Mesh 的同一对象必须有匹配的非 trigger、非 convex MeshCollider，且不得混入额外 Collider。旧 `blockingColliders` 字段仅保留序列化兼容，不是运行时真源。
- `PF_FPG_Root1TreeCover` 的 shadow proxy 引用 `Environment/Generated/SpriteShadowCasters/` 中的 mesh/material；这些引用与 `.meta` 必须成套维护，不得复制进配置资产或按 hash 文件名手工清理。
- `Covers/VFX/PF_FPG_CoverTransition.prefab` 是 `FpgCoverTransitionEffectView` 的 authored wrapper；玩家 Entity 上的 `FpgCoverTraversalPresenter` 只能移动玩家视觉并播放 transition VFX，不能提交 gameplay traversal 或拥有相机状态。
- `RoomExit/Prefabs/PF_FPG_RoomExit.prefab` 是正式出口表现入口，只引用同域 `RoomExit/Materials` 与 `RoomExit/Shaders`；房间解锁、切换和刷新规则仍由正式 runtime/config 拥有。
- 每个 Art Scene 只有一个 scene root，根上只有一个 `FpgRoomArtRoot`，并引用匹配的 `FpgRoomDefinition`。天空盒、环境光与可选的 `RenderSettings.sun` 直接由该 Art Scene 的 Lighting 设置拥有。
- Art Scene 可拥有环境 renderer/collider、lighting、Camera、AudioListener、Volume 与 `IFpgRoomArtPresentationBinding`，不得拥有 GameBootstrap、Encounter Host、RoomInstance 或 Art Scene loader。
- RoomDefinition 的 Scene GUID 是移动后的身份真源，Assets-relative path 用于运行时加载；移动/重命名场景必须通过 Unity Editor 并让同步工具更新路径，catalog 中 GUID/path/name 不得重复。
- 各 Art Scene 的 LightingSettings 与烘焙数据随场景成套维护；体积雾/光适配器从 Art Root 下自动发现插件组件并绑定 Formal Camera/可选 Sun，不维护 Inspector 引用数组，也不把插件对象放进 FormalRoom。
- `D0ForestParallax` 是 visual-only 的 Art presentation binding：只偏移 layer 的 authored local position，不移动 gameplay camera 或 collider；解绑、重启和复用时必须恢复捕获的 authored base position。
- Forest 水面只属于 Art Scene 表现，但当前有两条独立链路：`Shaders/FPG_ForestWater.shader`、`Environment/rootArt/root1/Materials/m_water.mat` 与 `FpgWaterPlanarReflection` 走 `IFpgRoomArtPresentationBinding`，倒影纹理只写入 MaterialPropertyBlock 并在解绑时清理；`ART_Forest 1_Copy.unity` 则直接挂载同目录的 `GenshinPlanarReflection`、`GenshinPlanarWater.shader` 与 `Water.mat`，通过 `beginCameraRendering` 渲染隐藏相机并写全局 `_ReflectionTex`，不属于上述绑定合同。
- 不得混用两条水面链路的组件、材质或渲染设置，也不得用其中一条的成功验证替代另一条。改动前先确认目标 Art Scene 的实际引用；再检查 Unity 编译/Console、反射层与 renderer index、多相机/Scene View 行为、退出清理、`FpgRoomArtSceneContractTests.cs` 与 `FpgRoomArtSceneLoaderPlayModeTests.cs`。当前没有水面专用自动化测试，PC/mobile 视觉回归仍需人工确认。
- 验证以 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgRoomExitRuntimeTests.cs`、`D0ForestParallaxTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomArtSceneLoaderPlayModeTests.cs`、`BuildSettingsTests.cs` 和 `FormalFirstAuthoringContractTests.cs` 为准。
