# ThirdParty 指南

本目录保存仍在项目内的插件和 vendored 资源；第三方 sample/demo 默认不纳入项目上下文。

## 目录边界

- 当前顶层只有 `TextMesh Pro/`、`VolumetricFog2/` 和 `VolumetricLights/`。
- Klaus 特效包已经迁到 `Assets/VFX_Klaus/`，按其局部指南处理；不要再使用旧 `Assets/ThirdParty/VFX_Klaus/` 路径。
- 项目自有玩法脚本、正式场景安装器和配置同步工具不要放在这里。

## 工作规则

- 除非任务明确是插件集成、资源迁移或导入修复，不编辑第三方源码、shader、sample scene 或 demo 资源。
- 移动第三方资源时必须同步 `.meta`，并在所有正式 wrapper、prefab、material 和 scene 引用验证完成前保留原位置。
- 正式主线依赖第三方资源时优先建立项目自有 wrapper；保留直接依赖时必须显式记录并做 GUID 反查。

## 验证

- 修改位置或依赖后检查实际消费者的 missing references、URP 视觉和 Console；不要用已删除的 sample 路径作为验证入口。
