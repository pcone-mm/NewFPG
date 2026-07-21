# CZN Runtime 指南

这个目录放 CZN Spine 技能和模型预览的运行时代码，命名空间是 `NewFPG.CZN`。

## 职责

- `CznSpineSkillSequence` 及 cue 类型负责保存角色动作、Spine 层、粒子、锚点/相机变换、缩放和 marker 数据。
- `CznSpineSkillTimeline`、`CznSpineSkillTrack` 和 `CznSpineSkillPlayer` 负责 Timeline 采样、运行时层创建、回放求值和重播清理。
- `CznSpineSkillPreviewMenu` 与 `CznMonsterModelPreviewController` 只负责本地预览交互，不是正式战斗 UI。

## 边界

- 不要在这里硬编码某个角色 ID、导入根目录、SSRC 记录或 CFX/SRMD 解析规则；角色专用解析和生成器放在 `Assets/Editor/CZN/`。
- 不要把提取工具、转换脚本或原始载荷放进 `Assets/Scripts/CZN/`；离线流程在 `Tools/CznResourcePipeline/`，导入产物在 `Assets/Imported/CZN/`。
- Runtime 播放器必须能从生成好的 `CznSpineSkillSequence` 和 Timeline 数据求值，不依赖 Editor-only API。
- CZN 技能预览默认是单次播放并在结束后回到 `b_idle`；只有源数据或用户明确要求循环时才启用 Timeline 自动回绕。

## 验证方式

- 改重播、清理或采样逻辑时，按 `.codex/skills/czn-character-spine-unity-import/references/WORKFLOW.zh-CN.md` 的 replay 矩阵检查：立即重播、末态后重播、自然结束、跨技能播放和暂停/继续。
- Spine 硬重置必须覆盖 `ClearTracks`、setup pose、重新 `SetAnimation` 和当帧 apply/update；本项目优先使用 `SkeletonAnimation.ClearState()`。
- 改 cue 数据结构后，同步检查 `Assets/Editor/CZN/` 里的生成器和 `Assets/Imported/CZN/*/Preview/SkillCompositions/` 产物能否重新生成。
- 角色技能验证优先执行对应 `Tools/CZN/.../Validate...` 菜单项；怪物预览验证执行 `Tools/CZN/Monsters/Validate Selected 8 Models`。
