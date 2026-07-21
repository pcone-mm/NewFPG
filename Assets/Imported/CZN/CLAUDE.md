# Imported CZN 指南

这个目录放 CZN 导入后的 Unity 可读资源、Metadata、预览场景和 prefab。多数角色/怪物载荷目录按 `.gitignore` 保持 local-only，不能假设它们会随 Git 或聊天迁移。

## 目录边界

- `Heidemarie_30093/`、`Fei_30048/` 和 `Monsters/` 是当前已导入样本/批处理产物，包含 `SpineSource/`、`Configs/`、`Metadata/`、`Preview/` 和对应 README。
- `Editor/` 只放和导入产物直接相关的 Unity import 后处理；通用生成器仍放 `Assets/Editor/CZN/`。
- `External/CZN/` 保存原始解包副本、Spine 工程和本地 spine-unity runtime，不在此目录也不应提交。

## 工作规则

- 不要手工改 canonical Spine JSON、`*.scsp1u.bytes`、`import-manifest.json`、hash 报告或生成的 Timeline 引用；需要变更时从 `Tools/CznResourcePipeline/` 和 `Assets/Editor/CZN/` 重新生成。
- 保留 `.meta` 并让它跟随资源移动；Spine/Timeline/Prefab 引用对 GUID 很敏感。
- 新角色使用 `<SafeName>_<CharacterId>` 命名，并遵守 `.codex/skills/czn-character-spine-unity-import/references/WORKFLOW.zh-CN.md` 的目录布局。
- 不要把这些提取资源当作可分发资产；交付说明必须写清 local-only 路径、授权边界和重建步骤。

## 验证方式

- 先读每个导入根目录的 `README.md` 和 `Metadata/*report*`，再打开 `Preview/*.unity`。
- 角色技能预览用对应 `Tools/CZN/<角色>/Validate...` 菜单验证；怪物批处理用 `Tools/CZN/Monsters/Validate Selected 8 Models`。
- 如果 `Packages/manifest.json` 的 spine-unity 本地包缺失，先重建 `External/CZN/SpineRuntime-3.8`，再判断导入产物是否损坏。
