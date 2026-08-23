# SkillAuthoring Editor 指南

`FPG.SkillAuthoring.Editor` 是 Editor-only 技能制作层，命名空间为 `FPG.Demo.Editor.SkillAuthoring`；正式入口是 `FPG Demo/Skill Editor`。

## 职责边界

- `FpgSkillEditorWindow`、UXML/USS 与 `FpgSkillTimelineView` 管理资产选择、时间轴、校验和编辑交互。
- `FpgSkillSerializedAdapter` 通过正式 V3 字段读写类型化 gameplay action、active presentation track、warning 与 event，并把结果交给 `FPG.Skills` 编译/校验；不要在 Editor 复制一套运行时语义。
- `FpgSkillEntityBindingIndex` 只从角色 Weapon slots 与敌人 `attackPatterns` 派生技能所有权、筛选顺序和预览实体，不建立第二份绑定配置。
- Preview 类使用正式 compiled timeline 与 60Hz runtime，在隔离 Scene 中预览 VFX/轨迹/相机反馈并用临时 2D AudioSource 播放音频；默认 Prefab 来自当前实体定义，手工覆盖只保存在 skill/entity 对应的 SessionState，不写回正式资产或 Scene。
- 上一级的 `FpgSkillTimelineDefinitionInspector` 只是打开本工具的薄入口；核心 authoring 逻辑留在本目录。

## 工作规则

- 所有资产修改使用 `SerializedObject`、`Undo`、`AssetDatabase` 与现有 adapter；保留字段名、GUID、stable ID 和 authored ordinal，不批量手改 YAML。
- 每个正式技能必须唯一归属一个实体定义；未绑定或被多个实体引用都是校验错误。修复所有权应修改 Character/Weapon 或 Enemy 定义中的正式引用，不得在 Editor 索引中写补丁映射。
- copy/paste、删除、转换和跨轨移动事件时必须维护稳定 ID、`authoredOrdinal`、动作绑定与节点内联表现；校验失败时不保存“部分有效”的 compiled 结果。
- attack timing mode、windup coefficient 与 different-attack interrupt marker 只能经 `FpgSkillSerializedAdapter`、Undo 和正式 sequence 字段修改；时间轴展示的 attack frame 从唯一 authored Attack 派生，不另存一套 marker 数据。CharacterAttackSpeed 序列不得混入 Projectile/Reload/Summon/SelfDestruct gameplay action。
- `allowWithdrawTick` 时间轴 marker 只能经 `FpgSkillSerializedAdapter` 与 Undo 修改；玩家攻击序列必须把它放在最后一个 Attack/Projectile event 之后且不超过 duration，不能由动画结束帧或预览状态暗改。
- `SecondaryTriggerMode` 与 minimum charge、cooldown、charge progress 是资产级 authoring 合同；当前 Fei 的 Immediate/Charge Secondary 是两个独立资产，Editor 必须保留各自声明的模式，不能在编辑其中一个时静默转换或覆盖另一个。Immediate 使用 Execute；ChargeRelease 的 area/projectile 技能使用 ChargeEnter/ChargeLoop/Release/Cancel。
- Enemy `SelfDestructOwner` 使用固定 Self target；设置依赖时只能从同 tick、较早的 Summon action 中选择，Editor 不重新引入 `summonOwnerOutcome` 或允许手写无效依赖。
- 预览必须使用正式 compiled timeline、`FpgSkillExecutionRuntime` 和相同 60 tick 时钟；不得用 Editor 帧率推导另一套触发时间。
- 正式资产只接受 `authoringSchemaVersion=3`；已删除的 V1 payload/cue 字段和迁移脚本不得作为回退写入路径。

## 验证

- 编辑、实体绑定、筛选、剪贴板、动作选择与删除引用保护：`Assets/FPGDemo/Tests/EditMode/FpgSkillAuthoringEditorTests.cs`、`FpgSkillAuthoringChoicesTests.cs`。
- 跳 tick、回退 scrub 与隔离表现预览：`FpgSkillPreviewExecutionTests.cs`。
- 序列化定义、攻击时序与正式资产合同：`FpgSkillDefinitionTests.cs`、`FpgPlayerSkillAssetContractTests.cs`、`FpgAttackTimingTests.cs`。
- 纯 V3 正式资产与 wrapper 路径：`FpgFormalSkillPresentationV3AssetTests.cs`。
- 改 asmdef、UXML/USS 或 Inspector 入口后检查 Unity 编译/Console；默认不批量运行测试。
