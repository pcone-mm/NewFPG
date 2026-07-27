# SkillAuthoring Editor 指南

`FPG.SkillAuthoring.Editor` 是 Editor-only 技能制作层，命名空间为 `FPG.Demo.Editor.SkillAuthoring`；正式入口是 `FPG Demo/Skill Editor`。

## 职责边界

- `FpgSkillEditorWindow`、UXML/USS 与 `FpgSkillTimelineView` 管理资产选择、时间轴、校验和编辑交互。
- `FpgSkillSerializedAdapter` 通过正式 V3 字段读写类型化 gameplay action、active presentation track、warning 与 event，并把结果交给 `FPG.Skills` 编译/校验；不要在 Editor 复制一套运行时语义。
- Preview 类使用正式 compiled timeline 与 60Hz runtime，在隔离 Scene 中预览 VFX/轨迹/相机反馈并用临时 2D AudioSource 播放音频；不写回 Scene 或把预览状态当成正式 gameplay 状态。
- 上一级的 `FpgSkillTimelineDefinitionInspector` 只是打开本工具的薄入口；核心 authoring 逻辑留在本目录。

## 工作规则

- 所有资产修改使用 `SerializedObject`、`Undo`、`AssetDatabase` 与现有 adapter；保留字段名、GUID、stable ID 和 authored ordinal，不批量手改 YAML。
- copy/paste、删除、转换和跨轨移动事件时必须维护稳定 ID、`authoredOrdinal`、动作绑定与节点内联表现；校验失败时不保存“部分有效”的 compiled 结果。
- 预览必须使用正式 compiled timeline、`FpgSkillExecutionRuntime` 和相同 60 tick 时钟；不得用 Editor 帧率推导另一套触发时间。
- 正式资产只接受 `authoringSchemaVersion=3`；已删除的 V1 payload/cue 字段和迁移脚本不得作为回退写入路径。

## 验证

- 编辑、剪贴板与删除引用保护：`Assets/FPGDemo/Tests/EditMode/FpgSkillAuthoringEditorTests.cs`。
- 跳 tick、回退 scrub 与隔离表现预览：`FpgSkillPreviewExecutionTests.cs`。
- 序列化定义与正式资产合同：`FpgSkillDefinitionTests.cs`、`FpgPlayerSkillAssetContractTests.cs`。
- 纯 V3 正式资产与 wrapper 路径：`FpgFormalSkillPresentationV3AssetTests.cs`。
- 改 asmdef、UXML/USS 或 Inspector 入口后检查 Unity 编译/Console 与 `AssemblyBoundaryTests.cs`；默认不批量运行测试。
