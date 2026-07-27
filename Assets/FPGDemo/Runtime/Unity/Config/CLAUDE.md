# Unity Config 指南

本目录保存正式 Unity 配置定义与运行时适配器。

- `FpgEnemy*`、`FpgEncounter*`、`FpgFormal*`、统一技能定义和 `FpgPlayableCharacterCatalog` 是正式配置合同。
- `FpgSkillGameplayActionDefinitions` 与 `FpgSkillPresentationDefinitions` 分别拥有类型化 gameplay action、action-node impact/trajectory/collision 表现和 active presentation track；正式资产只接受 schema V3。
- 正式资产仍引用的 D0 前缀类型是序列化/GUID 兼容合同；不得据此建立第二套运行入口。
- `FpgFormalConfigAdapters` 只转换数据，不引入新战斗决策或按敌人 ID 特判。
- character/enemy/pool/profile/override/catalog ID，以及 skill stable ID、authored ordinal、gameplay/presentation hash 是跨资产稳定合同。
- trajectory prefab 根节点必须有可校验的 `FpgTrajectoryVfxView`；正式配置只引用 `Presentation/Characters/*/VFX/PF_FPG_*` wrapper，不直连供应商 demo。
- 缺少引用、ID 冲突、容量不足或动画/Prefab 校验失败时必须 fail-closed。
- 修改后检查对应 `TryValidate/TryBuildData`、Unity 编译/Console、`FpgSkillDefinitionTests.cs`、`FpgFormalSkillPresentationV3AssetTests.cs` 与现存的精确 Formal EditMode 合同。
