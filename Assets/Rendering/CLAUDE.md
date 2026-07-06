# Rendering 使用指南

这个目录放项目自有渲染扩展和 URP 后处理代码。当前稳定模块是 `DodgeSpeedLines/`，命名空间是 `NewFPG.Rendering`。

## 目录边界

- `DodgeSpeedLines.shader` 绘制屏幕空间速度线。
- `DodgeSpeedLinesRendererFeature.cs` 是 URP `ScriptableRendererFeature`，从 Volume stack 读取参数。
- `DodgeSpeedLinesVolume.cs` 定义 `NewFPG/Dodge Speed Lines` Volume override。
- `DodgeSpeedLinesController.cs` 挂在场景 Global Volume 附近，通过动画事件或脚本开关效果。
- `Assets/Scripts/Combat/CombatDodgePresentationController.cs` 可以在闪避时驱动同一个 Volume override 或创建运行时 Global Volume；渲染目录只维护效果实现，不读取战斗输入。
- 对应 Inspector 在 `Assets/Editor/DodgeSpeedLinesControllerEditor.cs`，只放编辑器辅助，不放运行时逻辑。

## 工作规则

- 不要把速度线做成角色材质或逐对象特效；它是屏幕空间后处理，开关应走 Volume override。
- 修改 RendererFeature 时确认目标 URP 版本和 RenderGraph/兼容模式路径，避免只改其中一条渲染路径。
- shader、RendererFeature、Volume 参数要保持字段名和 shader property 一致。
- 场景接入时先确认 Renderer Data 已添加对应 RendererFeature，再确认 Global Volume profile 里有 `Dodge Speed Lines` override。
- 闪避表现调参优先改 `Assets/Settings/Combat/SO_CombatDodgePresentation_Default.asset`；这里不要写战斗冷却、输入或相机状态规则。

## 验证方式

- 改渲染代码后打开 Unity，等待编译并检查 Console。
- 在使用该效果的场景里用 `DodgeSpeedLinesController` 播放开关，确认 Game 视图有速度线且 Scene View 不被意外影响。
- 闪避触发路径还要在 `Shulin_L0.unity` 里通过 `CombatDodgePresentationController` 验证。
- 改 URP RendererFeature 或 Volume 默认值后，检查相关 URP Renderer Data 和 Volume Profile 的序列化引用。
