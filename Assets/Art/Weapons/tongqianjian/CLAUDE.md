# TongQianJian 浮空武器指南

这个目录保存通钱剑浮空演示的源贴图、材质、shader、动画和 committed 示例 Prefab。

## 目录边界

- `Materials/` 保留现有 `tongqianjian.mat`，不要和浮空演示的 `M_TongQianJian_*` 互相替换。
- 根目录下的 `tongqianjian.png`、`tongqianjian_floating_core.png`、`tongqianjian_floating_glow.png`、`tongqianjian_up.anim`、`tongqianjian_down.anim`、`tongqianjian_up.controller`、`TongQianJian_Floating_Example.prefab` 和 `TongQianJian_Floating_Preview.png` 是这条线的主要资产。
- `TongQianJianFloatingBody.cs` 负责运行时浮动；示例 Prefab、材质、贴图和 controller 都是直接维护的 authored 资产。

## 工作规则

- 变更贴图、shader、材质或 prefab 时同步 `.meta`，通过 Inspector 或 Prefab Mode 显式编辑，不要手改 YAML 结构。
- 不要把这组演示资源挪进 `Assets/Scripts/`、`Assets/Settings/` 或 `Assets/Prefabs/` 的其他武器线。
- 若修改浮空逻辑或预览，打开示例 Prefab 确认引用完整且不会改写其他武器资产。

## 验证

- 改贴图、shader、材质或 Prefab 后，检查 `TongQianJian_Floating_Example.prefab` 与 `TongQianJian_Floating_Preview.png`。
- 改动画或 controller 后，打开示例确认上下浮动状态和预览仍能解析。
