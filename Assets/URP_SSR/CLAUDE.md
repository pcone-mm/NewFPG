# URP_SSR 指南

本目录是保留在项目中的旧 SSR vendor 包和样例，不是正式游戏入口。当前 `Assets/Settings/PC_Renderer.asset` 仍序列化它的 `URP Screen Space Reflection` feature，但该 feature 为禁用状态；新的候选实现位于 `Assets/ThirdParty/UniversalScreenSpaceReflection/`。

## 目录边界

- `Scripts/` 与 `Shaders/` 是旧插件实现，程序集为 `SSR`；`Demo/`、`Settings/`、`DefaultVolumeProfile.asset` 和 `Readme.asset` 都是供应商样例或模板，不是项目渲染配置真源。
- 项目级 Renderer、Pipeline Asset 与 Volume Profile 只在 `Assets/Settings/` 维护；正式水面与房间表现只在 `Assets/FPGDemo/Presentation/Level/` 维护。

## 工作规则

- 没有明确的 SSR 选型或兼容修复任务时，不编辑本目录源码、shader、sample scene、模型或烘焙数据。
- 不得同时启用本包与 `Assets/ThirdParty/UniversalScreenSpaceReflection/` 的 Renderer Feature；选型、升级或移除前先反查 `PC_Renderer.asset`、Volume Profile、正式材质和 asmdef 依赖，并保留全部 `.meta`。
- 不把 `Demo/` 或 `Settings/` 复制进正式 FPG 场景，也不使用样例 Renderer/Profile 替换 `Assets/Settings/` 中的项目资产。

## 验证

- 相关改动检查 Unity 编译/Console、`SSR` asmdef、PC Renderer Feature 与 Volume 的共同启用状态、Forward+ DepthNormals 兼容和正式目标场景视觉；供应商 demo 只能辅助诊断，不能替代正式场景验收。
