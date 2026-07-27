# Rendering 指南

本目录保留项目自有 URP 扩展。当前只有 `DodgeSpeedLines/`，命名空间为 `NewFPG.Rendering`；正式 FPG gameplay 目前没有对应驱动器。

## 目录边界

- `DodgeSpeedLines.shader` 实现屏幕空间速度线。
- `DodgeSpeedLinesRendererFeature.cs` 是 URP `ScriptableRendererFeature`，从 Volume stack 读取参数。
- `DodgeSpeedLinesVolume.cs` 定义 `NewFPG/Dodge Speed Lines` Volume override。
- 已删除的 `DodgeSpeedLinesController`、旧 Combat 驱动器和 Editor Inspector 不属于当前模块，不得从指南恢复。

## 工作规则

- 修改 RendererFeature 时同时检查目标 URP 版本的 RenderGraph/兼容路径；shader property、RendererFeature 与 Volume 字段名保持一致。
- 当前序列化绑定位于 `Assets/Settings/PC_Renderer.asset`、`Mobile_Renderer.asset` 和 `DefaultVolumeProfile.asset`；不要把共享渲染扩展当成正式战斗入口。
- 若重新接入 gameplay，先在 `FPG.Unity` 建立明确适配边界和验证合同，不直接读取旧 `NewFPG.Combat` 状态。

## 验证

- 修改渲染代码后检查 Unity 编译/Console，以及 PC/Mobile Renderer Data 和 Default Volume Profile 是否仍能解析该 feature/override。
- 视觉启用效果必须在实际引用它的当前场景中验证；不要使用已删除的 Shulin/CombatLab 场景作为固定入口。
