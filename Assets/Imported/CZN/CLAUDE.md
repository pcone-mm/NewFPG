# Imported CZN 指南

这个目录放 CZN 导入后的 Unity 可读资源、Metadata、预览场景和 prefab。当前仓库明确跟踪这些载荷并由 Git LFS 管理二进制资源；不要再把它们当作 `.gitignore` 下的 local-only 目录。

## 目录边界

- `Heidemarie_30093/`、`Fei_30048/` 和 `Monsters/` 是已跟踪的导入输入与审计产物，包含 `SpineSource/`、`Configs/`、`Metadata/`、`Preview/` 和对应 README。
- `Editor/` 只保存导入纹理后处理，不承载角色专用生成器或正式 gameplay authoring。
- `External/CZN/` 保存审计过的原始副本与 vendored spine-unity 3.8；`Assets/FPGDemo/SourceArt/CZN/` 和 `Presentation/Characters/*/Spine` 才是正式 FPG 的源输入与运行时渲染边界。

## 工作规则

- 不要手工改 canonical Spine JSON、`*.scsp1u.bytes`、`import-manifest.json`、hash 报告或生成的 Timeline 引用；需要变更时从 `Tools/CznResourcePipeline/` 和项目 CZN skill 重新生成。
- 保留 `.meta` 并让它跟随资源移动；Spine/Timeline/Prefab 引用对 GUID 很敏感。
- 新角色使用 `<SafeName>_<CharacterId>` 命名，并遵守 `.codex/skills/czn-character-spine-unity-import/references/WORKFLOW.zh-CN.md` 的目录布局。
- Git 跟踪决定不等于第三方发布许可结论；不要擅自改写 LFS/跟踪策略，也不要在缺少负责人结论时扩大公开分发范围。

## 验证方式

- 先读每个导入根目录的 `README.md` 和 `Metadata/*report*`，再打开 `Preview/*.unity`。
- 重新提取或转换后运行 `Tools/CznResourcePipeline/validate_import.py`，检查 conversion/import 报告，再确认 Unity 导入、Spine Atlas/Material/SkeletonData 和 Preview 引用。
- 如果 `Packages/manifest.json` 的 spine-unity 路径不能解析，先检查已跟踪的 `External/CZN/SpineRuntime-3.8` 和 Git LFS 状态，再判断导入产物是否损坏。
