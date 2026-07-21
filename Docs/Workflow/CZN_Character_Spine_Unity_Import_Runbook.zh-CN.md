# CZN 角色 Spine / Unity 导入复用手册

这套流程已经固化为项目技能：

```text
.codex/skills/czn-character-spine-unity-import/SKILL.md
```

以后在这个项目里只要提出“按 CZN 角色导入流程处理某角色”，Codex 就应读取该技能，并按完整的依赖审计、只读提取、SCSP1U 转换、Spine/Unity 导入、技能组合、重播验证和文档交付流程执行。

详细规范：

- [完整标准流程](../../.codex/skills/czn-character-spine-unity-import/references/WORKFLOW.zh-CN.md)
- [角色交付文档模板](../../.codex/skills/czn-character-spine-unity-import/references/CHARACTER-HANDOFF-TEMPLATE.zh-CN.md)
- [海德玛丽 30093 已验证示例](Heidemarie_30093_Spine_Unity_Guide.zh-CN.md)
- [提取与转换工具说明](../../Tools/CznResourcePipeline/README.md)
- [SCSP1U 已知格式和边界](../../Tools/CznResourcePipeline/SCSP1U_NOTES.md)

## 当前复用程度

- 通用提取器、SCSP1U Parser/转换器、Unity 技能运行时和 Timeline 播放器已保存到项目。
- 海德玛丽的 13 个技能组合和验证产物已保存为本地示例。
- 新角色的依赖记录清单仍需从 SSRA/SSRC 索引审计生成。
- 当前 `HeidemarieSkillComposer` 仍是 30093 专用生成器；新角色需要根据其 SRMD/BRMD 命令图适配或继续泛化，不能只改目录名。
- 游戏提取物、Spine 本地工程和 Runtime 位于忽略目录，不会随 Git 或聊天自动迁移到另一台电脑。

## 推荐的下次请求

```text
按项目里的 CZN 角色导入流程，把「角色名」（如果知道就附 ID）的战斗模型、完整技能特效、Spine 预览工程和 Unity 技能预览场景导入，并做三轮重播/循环验证。
```

如果只给角色名，流程会先查角色 ID；如果游戏版本或安装路径变了，需要同时提供新的 `gameres` 位置。
