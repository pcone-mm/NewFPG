# TongQianJian 浮空武器指南

这个目录保存通钱剑浮空演示的源贴图、材质、shader、动画和一键重建脚本。

## 目录边界

- `Materials/` 保留现有 `tongqianjian.mat`，不要和浮空演示的 `M_TongQianJian_*` 互相替换。
- 根目录下的 `tongqianjian.png`、`tongqianjian_floating_core.png`、`tongqianjian_floating_glow.png`、`tongqianjian_up.anim`、`tongqianjian_down.anim`、`tongqianjian_up.controller`、`TongQianJian_Floating_Example.prefab` 和 `TongQianJian_Floating_Preview.png` 是这条线的主要资产。
- `TongQianJianFloatingBody.cs` 负责运行时浮动，`TongQianJianFloatingExampleBuilder.cs` 负责通过菜单重建示例。

## 工作规则

- 变更贴图、shader、材质或 prefab 时同步 `.meta`，不要手改生成结果的 YAML 结构。
- 需要重建示例时，优先用 `NewFPG/VFX/Rebuild TongQianJian Floating Example`。
- 不要把这组演示资源挪进 `Assets/Scripts/`、`Assets/Settings/` 或 `Assets/Prefabs/` 的其他武器线。
- 若修改浮空逻辑或预览，确认示例 prefab 仍可在当前菜单下无缺失重建。

## 验证

- 改贴图、shader、材质或 builder 后，重跑重建菜单并检查 `TongQianJian_Floating_Example.prefab` 与 `TongQianJian_Floating_Preview.png`。
- 改动画或 controller 后，打开重建后的示例，确认上下浮动状态和预览仍能解析。