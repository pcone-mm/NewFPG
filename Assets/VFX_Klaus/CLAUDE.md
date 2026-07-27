# VFX_Klaus 指南

本目录是 Klaus VFX 供应商源包和选型库，不是 FPG 正式表现入口。

## 目录边界

- `Prefabs/`、`Materials/`、`Meshes/`、`Shaders/` 和 `Textures/` 保存供应商源依赖；`Timeline/` 与 `VFX_Lab/` 只是预览、样例和选型内容。
- `Scripts/` 是供应商 demo 辅助脚本，不承载 `FPG.Demo` gameplay。
- 项目自有 wrapper 放在 `Assets/FPGDemo/Presentation/Characters/*/VFX/`。正式配置只引用 wrapper；wrapper 可保留对本目录 Prefab、Material、Mesh、Shader 或 Texture 的显式 GUID 依赖。

## 工作规则

- 除非任务明确要求供应商集成或兼容修复，不修改源 prefab、shader、脚本、Timeline 或 demo scene。
- 移动、升级或替换供应商依赖前，反查所有正式 wrapper 并保留 `.meta`；正式配置不得直连本目录，任何正式资产都不得引用 `Timeline/` 或 `VFX_Lab/`。
- 仅打开旧 demo scene 可能触发 Unity YAML 升级、光照重烘焙或 `LightingData.asset` 漂移；没有明确视觉改动和验证证据时，不提交这些自动变更。

## 验证

- 修改供应商依赖后，检查对应 `Assets/FPGDemo/Presentation/` wrapper 的 missing references、材质/shader、粒子朝向与 Console，并确认 `Assets/FPGDemo/Config/FormalEncounter/` 没有供应商 GUID 直连。
- 只维护本指南时运行 `git diff --check` 并检查 `CLAUDE.md.meta` 配对。
