# ThirdParty 使用指南

这个目录放导入的插件、Unity samples、vendored 资源和第三方示例内容。

## 目录边界

- `JMO Assets/`、`VFX_Klaus/`、`VolumetricLights/`、`VolumetricFog2/`、`TextMesh Pro/`、`Samples/` 等优先视为外部资产或样例。
- 项目自有玩法脚本、场景安装器和配置同步工具不要放在这里。

## 工作规则

- 除非任务明确是插件集成、资源迁移或修复导入问题，否则不要编辑第三方源码、shader、sample scene 或 demo 资源。
- 移动第三方资源时必须同步 `.meta`，并在场景、prefab、material 引用验证完成前不要删除原位置。
- 从第三方资源中挑选可用特效或材质时，优先复制或包装到项目自有目录，再让 gameplay 代码依赖项目自有路径。

## 验证方式

- 改第三方资源位置后，打开引用它们的场景或 prefab 检查 missing references。
- 改第三方 shader、renderer feature 或 package 相关资源后，检查 URP 场景视觉和 Console。
