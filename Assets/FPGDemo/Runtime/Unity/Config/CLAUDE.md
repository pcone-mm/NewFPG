# Unity Config 指南

本目录保存正式 Unity 配置定义与运行时适配器。

- `FpgEnemy*`、`FpgEncounter*`、`FpgFormal*`、`FpgSummonActionDefinition` 和 `FpgPlayableCharacterCatalog` 是正式配置合同。
- 正式资产仍引用的 D0 前缀类型是序列化/GUID 兼容合同；不得据此建立第二套运行入口。
- `FpgFormalConfigAdapters` 只转换数据，不引入新战斗决策或按敌人 ID 特判。
- character/enemy/pool/profile/override/catalog ID 是跨资产稳定合同。
- 缺少引用、ID 冲突、容量不足或动画/Prefab 校验失败时必须 fail-closed。
- 修改后检查对应 `TryValidate/TryBuildData`、Unity 编译/Console 和现存的精确 Formal EditMode 合同。
