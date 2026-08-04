# Level Presentation 指南

本目录保存正式房间环境资源与 additive Art Scene，不拥有正式 gameplay Host。

- `Rooms/` 下由 `FPG_RoomCatalog` 与 Build Settings 通过 GUID/path 注册的是正式 Art Scene；整房复制会按源场景名生成唯一 `_Copy` 路径，不得用文件名模式推断房间身份或所有权。`Environment/` 保存可复用源资源，不能再作为 `RoomDefinition` 的运行时 prefab 回退。
- `Environment/Generated/SpriteShadowCasters/` 保存由 Unity Editor 产出、按源 sprite GUID 命名的 scene-coupled mesh/material；当前正式 Art Scene 与 `PF_FPG_Root1TreeCover` 直接引用其 GUID。不要手改、重命名或按 hash 单独删除，重新生成或替换时必须让 asset、`.meta` 与全部消费者原子维护。
- 每个 Art Scene 只有一个 scene root，根上只有一个 `FpgRoomArtRoot`，并引用匹配的 `FpgRoomDefinition`。天空盒、环境光与可选的 `RenderSettings.sun` 直接由该 Art Scene 的 Lighting 设置拥有。
- Art Scene 可拥有环境 renderer/collider、lighting、Camera、AudioListener、Volume 与 `IFpgRoomArtPresentationBinding`，不得拥有 GameBootstrap、Encounter Host、RoomInstance 或 Art Scene loader。
- RoomDefinition 的 Scene GUID 是移动后的身份真源，Assets-relative path 用于运行时加载；移动/重命名场景必须通过 Unity Editor 并让同步工具更新路径，catalog 中 GUID/path/name 不得重复。
- 各 Art Scene 的 LightingSettings 与烘焙数据随场景成套维护；体积雾/光适配器从 Art Root 下自动发现插件组件并绑定 Formal Camera/可选 Sun，不维护 Inspector 引用数组，也不把插件对象放进 FormalRoom。
- `D0ForestParallax` 是 visual-only 的 Art presentation binding：只偏移 layer 的 authored local position，不移动 gameplay camera 或 collider；解绑、重启和复用时必须恢复捕获的 authored base position。
- 验证以 Unity 编译/Console、`D0ForestParallaxTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomArtSceneLoaderPlayModeTests.cs`、`BuildSettingsTests.cs` 和 `FormalFirstAuthoringContractTests.cs` 为准。
