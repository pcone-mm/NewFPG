# SkillAuthoring Editor 指南

`FPG.SkillAuthoring.Editor` 是 Editor-only 技能制作层，命名空间为 `FPG.Demo.Editor.SkillAuthoring`；正式入口是 `FPG Demo/Skill Editor`。

## 职责边界

- `FpgSkillEditorWindow`、UXML/USS 与 `FpgSkillTimelineView` 管理资产选择、时间轴、校验和编辑交互。
- `FpgSkillSerializedAdapter` 通过正式配置的序列化字段读写 phase、payload 与 event，并把结果交给 `FPG.Skills` 编译/校验；不要在 Editor 复制一套运行时语义。
- Preview 类只做内存预览、几何显示和事件日志，不写回 Scene 或把预览状态当成正式 gameplay 状态。
- 上一级的 `FpgSkillTimelineDefinitionInspector` 只是打开本工具的薄入口；核心 authoring 逻辑留在本目录。

## 工作规则

- 所有资产修改使用 `SerializedObject`、`Undo`、`AssetDatabase` 与现有 adapter；保留字段名、GUID、stable ID 和 authored ordinal，不批量手改 YAML。
- copy/paste、删除和移动事件时必须同步维护 payload、presentation cue 与 warning 的引用；校验失败时不保存“部分有效”的 compiled 结果。
- 预览必须使用正式 compiled timeline、`FpgSkillExecutionRuntime` 和相同 60 tick 时钟；不得用 Editor 帧率推导另一套触发时间。
- `FpgSkillTimelineV1Migration` 是显式、可重复校验的迁移入口。已存在且通过校验的正式技能只验证不重写，迁移不得在窗口打开或 installer 运行时隐式触发。

## 验证

- 编辑、剪贴板、删除引用保护、预览与迁移合同：`Assets/FPGDemo/Tests/EditMode/FpgSkillAuthoringEditorTests.cs`。
- 序列化定义与正式资产合同：`FpgSkillDefinitionTests.cs`、`FpgPlayerSkillAssetContractTests.cs`。
- 改 asmdef、UXML/USS 或 Inspector 入口后检查 Unity 编译/Console 与 `AssemblyBoundaryTests.cs`；默认不批量运行测试。
