# Level Presentation 指南

本目录保存正式房间环境资源与 additive Art Scene，不拥有正式 gameplay Host。

- `Rooms/<Room>/ART_<Room>.unity` 是 catalog/build list 中的正式 Art Scene；`Environment/` 保存可复用源资源，不能再作为 `RoomDefinition` 的运行时 prefab 回退。
- 每个 Art Scene 只有一个 identity scene root，根上只有一个 `FpgRoomArtRoot`；它必须引用匹配的 `FpgRoomDefinition` 和同场景主方向光，且 `RenderSettings.sun` 指向同一 Light。
- Art Scene 可拥有环境 renderer/collider、lighting、Volume 与 `IFpgRoomArtPresentationBinding`，不得拥有 Camera、AudioListener、GameBootstrap、Encounter Host、RoomInstance 或 Art Scene loader。
- RoomDefinition 的 Scene GUID 是移动后的身份真源，Assets-relative path 用于运行时加载；移动/重命名场景必须通过 Unity Editor 并让同步工具更新路径，catalog 中 GUID/path/name 不得重复。
- 各 Art Scene 的 LightingSettings 与烘焙数据随场景成套维护；体积雾/光通过 `Assets/FPGDemo/Integrations/` 的项目适配绑定 Formal Camera/主光，不把插件对象放进 FormalRoom。
- `D0ForestParallax` 是 visual-only 的 Art presentation binding：只偏移 layer 的 authored local position，不移动 gameplay camera 或 collider；解绑、重启和复用时必须恢复捕获的 authored base position。
- 验证以 Unity 编译/Console、`D0ForestParallaxTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomArtSceneLoaderPlayModeTests.cs`、`BuildSettingsTests.cs` 和 `FormalFirstAuthoringContractTests.cs` 为准。
