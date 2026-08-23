# Settings 使用指南

这个目录当前只保存项目级 URP Renderer、Render Pipeline Asset、Volume Profile 与 URP Global Settings；正式玩法配置位于 `Assets/FPGDemo/Config/`。

## 目录边界

- `PC_*` 与 `Mobile_*` 必须成对维护 Renderer Data 和 Render Pipeline Asset。
- `DefaultVolumeProfile.asset`、`SampleSceneProfile.asset` 与 `UniversalRenderPipelineGlobalSettings.asset` 是全局渲染配置，不承载战斗、关卡或技能规则。
- `PC_Renderer.asset` 同时序列化了 `Assets/ThirdParty/UniversalScreenSpaceReflection/` 的 `ScreenSpaceReflection` 与旧 `Assets/URP_SSR/` 的 `URP Screen Space Reflection`，当前两项 `m_Active` 都为 `0`；`SampleSceneProfile.asset` 中启用的 SSR Volume override 不能单独让效果生效。`Mobile_Renderer.asset` 没有这次 SSR 接入，不能从 PC 状态推断移动端支持。
- 已删除的 `Combat/`、`Forging/`、`Level/`、`Monsters/`、`Prototype/` 不再是本目录入口；不要按旧指南重建平行配置链。

## 工作规则

- 只有明确涉及 URP、Renderer Feature、Volume 或平台渲染质量时才修改本目录，并保留所有 `.meta` 和 Renderer/Asset 引用。
- SSR 调整必须先选定唯一实现，再成套维护 Renderer Feature、Volume Profile 与目标材质；不要同时启用两套 SSR，也不要把供应商 sample 的 Renderer/Profile 设为项目真源。
- 不要把 `ProjectSettings/`、Quality、Graphics 或 package 变更作为顺手清理项；需要联动时先列出影响面。
- 正式 FPG 的数值、技能、角色、敌人和房间配置一律进入 `Assets/FPGDemo/Config/`。

## 验证方式

- 修改后检查 Unity 编译/Console、Graphics/Quality 中的 RP Asset 绑定，以及 PC/Mobile Renderer Data 的序列化引用；SSR 还需检查 Renderer Feature 与 Volume 的共同启用状态、Forward+ DepthNormals 兼容和实际目标材质。
- 视觉结果需由主管/用户按项目验收交接流程确认；Agent 不把未试玩状态写成通过。
