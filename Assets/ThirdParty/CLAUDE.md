# ThirdParty 指南

本目录保存仍在项目内的插件和 vendored 资源；第三方 sample/demo 默认不纳入项目上下文。

## 目录边界

- 当前顶层包含 `TextMesh Pro/`、`VolumetricFog2/`、`VolumetricLights/` 和 `UniversalScreenSpaceReflection/`。
- `UniversalScreenSpaceReflection/` 是从 `mseonKim/URP-ScreenSpaceReflection` commit `57023e2` vendored 的 `1.1.0` 包；`Runtime/` 与根 asmdef 是插件实现，`Sample/` 和 `Documentation~/` 只作供应商参考，来源与项目接入点以其 `SOURCE.md` 为准。
- Klaus 特效包已经迁到 `Assets/VFX_Klaus/`，按其局部指南处理；不要再使用旧 `Assets/ThirdParty/VFX_Klaus/` 路径。
- 项目自有玩法脚本、正式场景安装器和配置同步工具不要放在这里。

## 工作规则

- 除非任务明确是插件集成、资源迁移或导入修复，不编辑第三方源码、shader、sample scene 或 demo 资源。
- 升级 `UniversalScreenSpaceReflection/` 时保留 `LICENSE.md`、`SOURCE.md` 和全部 `.meta`，按上游 commit 整包核对；项目集成只落在 `Assets/Settings/` 与正式项目材质，不修改 sample 形成第二套配置真源。
- 移动第三方资源时必须同步 `.meta`，并在所有正式 wrapper、prefab、material 和 scene 引用验证完成前保留原位置。
- 正式主线依赖第三方资源时优先建立项目自有 wrapper；保留直接依赖时必须显式记录并做 GUID 反查。

## 验证

- 修改位置或依赖后检查实际消费者的 missing references、URP 视觉和 Console；SSR 还需确认 `PC_Renderer.asset` 中只启用选定的一套 feature、对应 Volume override 生效，并验证 Forward+ DepthNormals 合同。不要用供应商 sample scene 作为正式验证入口。
