# CombatTuning Editor 指南

本目录保存 `FPG Demo/Shooting Tuning` 工作台，命名空间为 `FPG.Demo.Editor`；它编辑正式射击配置，不建立第二套 authoring 模型。

- `FpgShootingTuningWindow` 必须从 `FpgPlayableCharacterCatalog` 解析 Character、ThreeC、CombatFeel、Weapon 与当前 Secondary Skill；角色攻速和技能 timing 只显示 resolved/compiled 摘要，不能在窗口写 Character 或 Skill 字段，修改转交对应 Inspector 或 `Editor/SkillAuthoring`。
- 预览统一使用 `FpgShootingTuningSnapshot` 与当前 `IFpgShootingTuningPreviewHost`。输入、准星和表现类修改走 live preview；攻击查询、散布、范围或弹匣等结构值走 rebuild，并在失败时恢复最后有效快照。
- 朝向翻转 delay/duration 属于 ThreeC 可写字段，live preview 必须驱动现有 `FpgPlayerFacingController`，停止预览或失败回滚时恢复捕获快照。
- 写回只覆盖窗口声明的 ThreeC、CombatFeel 与 Weapon 字段，使用 `SerializedObject` 和单一 Undo group；写后必须重新捕获并逐字段核对，任一步失败整组回滚，不得部分保存或改写 Skill 资产。
- Catalog GUID 与角色 ID 只可保存在 `SessionState`；临时快照和 Play Mode 诊断不得成为持久配置真源。
- 验证检查 Unity 编译/Console、`FpgShootingTuningSnapshotTests.cs`、`FpgShootingContractsTests.cs`、`FpgLayeredAimIndicatorTests.cs` 与相关正式技能资产合同；默认不批量运行测试。
