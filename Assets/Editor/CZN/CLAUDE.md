# CZN Editor 指南

这个目录放 CZN 导入后的 Unity 侧生成器、验证器和菜单项，命名空间是 `NewFPG.CZN.Editor`。

## 职责

- `HeidemarieSkillComposer.cs`、`FeiSkillComposer.cs` 和相关 validation utility 负责角色专用 SkillSequence、Timeline、预览场景、报告和引用检查。
- `CznMonsterBatchBuilder.cs` 负责 8 只普通怪的 SkeletonDataAsset 读取、prefab/预览场景生成和模型验证。
- `CznPlistReader.cs` 等解析辅助只服务 CZN 导入流程，不应扩散为通用项目配置解析器。

## 边界

- 先读 `.codex/skills/czn-character-spine-unity-import/SKILL.md` 和工作流，再修改角色导入、技能组合或怪物批处理逻辑。
- 现有角色 composer 是已验证样本和角色专用实现；新角色不能只替换目录名或 ID，必须显式适配 SRMD/BRMD、CFX、particle、camera/node 和技能表。
- 生成器应保持幂等：重复运行不累积重复对象、Timeline track、场景节点或报告条目。
- 生成器打开/保存场景前要保护非目标脏场景，不要覆盖用户正在编辑的无关场景。
- 运行时播放、cue 类型和预览交互逻辑留在 `Assets/Scripts/CZN/`；离线解包/转换留在 `Tools/CznResourcePipeline/`。

## 验证方式

- 角色技能组合后运行对应验证菜单，例如 `Tools/CZN/Fei 30048/Validate Skill Import`，并检查 `Metadata/*validation-report*`。
- 怪物批处理执行 `Tools/CZN/Monsters/Build Selected 8 Models` 后，再执行 `Tools/CZN/Monsters/Validate Selected 8 Models`。
- 验证至少覆盖 SkeletonDataAsset 数量、动画数、Timeline track/clip 引用、SkillSequence cue 引用、预览场景播放器绑定和 replay 行为。
- 改生成器路径常量时，同步检查 `Assets/Imported/CZN/CLAUDE.md`、`Docs/Workflow/*CZN*Guide*.md` 和 `.gitignore` 的 local-only 边界。
