# FPGDemo Integrations 指南

本目录只保存项目对第三方运行时 API 的窄适配，不承载 gameplay 规则或第三方源码。

- 当前 `Volumetric/FpgVolumetricRoomArtBinding` 把 `Assets/ThirdParty/VolumetricFog2` 与 `VolumetricLights` 接到 `IFpgRoomArtPresentationBinding`。
- 该适配当前留在默认 `Assembly-CSharp`，以避免 `FPG.Unity` asmdef 和纯领域程序集直接依赖插件；没有明确架构决定时不要移动文件、添加 asmdef 或扩大插件引用面。
- 绑定只从 `FpgRoomArtPresentationContext` 接收 Formal Camera、Camera Transform 与 Art Scene 可选 Sun，并从 `FpgRoomArtRoot` 下自动发现现有插件组件，不维护手工数组或插件完整性合同。解绑只清空本适配器仍持有的 camera/sun/follow 与插件静态 camera 引用，不启停组件，也不恢复或改写其他美术状态。
- 不修改 `Assets/ThirdParty/` 插件源码来绕过生命周期问题；插件升级或 API 变化单独处理并检查所有 Art Scene 绑定。
- 验证以 Unity 编译/Console、`FpgRoomArtSceneContractTests.cs` 与 `FpgRoomArtSceneLoaderPlayModeTests.cs` 为准；当前 default-assembly 位置没有直接合同测试，若增加 asmdef 必须同时审查 scene script reference 与 `AssemblyBoundaryTests.cs`。
