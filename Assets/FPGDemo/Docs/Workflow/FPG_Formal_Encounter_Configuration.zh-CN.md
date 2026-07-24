# FPG Formal Encounter 配置指南

## 权威资产

- 玩家：`Config/FormalEncounter/Characters/FPG_Fei_*.asset`
- 玩家 catalog：`FPG_PlayableCharacterCatalog.asset`
- 普通房基础：`FPG_NormalRoom_Profile.asset`、EnemyPool、EnemyCatalog、AttackRuntimeCatalog
- 敌人：`FPG_Burstbug_*`、`FPG_Hudie_*`、`FPG_Luan_*`
- Luan 召唤：`FPG_Luan_Attack_Summon.asset` 与 `FPG_Luan_SummonHudie.asset`
- 表现：`FPG_CombatPresentationProfile.asset`
- 正式 prefab：`Presentation/FormalEncounter/PF_FPG_*Entity.prefab`

这些 committed 资产是权威真源，不再从 D0Slice、CombatLab 或 defaults installer 迁移。

## 引用链

`FpgEncounterProfile -> FpgEnemyPoolDefinition -> FpgEnemyDefinitionCatalog -> FpgEnemyDefinition -> Behavior/Attack/EntityPrefab`

AttackRuntimeCatalog 把稳定 attack ID 映射到正式运行时实现；Wave/Override 只选择池、预算、波次和时序，不拥有 Entity prefab 或 Room。

玩家链为：

`FpgPlayableCharacterCatalog -> D0CharacterDefinition/D0ThreeCProfile -> FpgPlayerEntityView`

其中 D0 前缀仅为序列化兼容，不表示旧主线。

## 配置规则

- 所有 character/enemy/attack/pool/profile/override/catalog ID 必须唯一且稳定。
- Profile、Override、Pool、Catalog 和 Runtime Catalog 必须成套校验。
- Enemy 的动画键必须存在于绑定 SkeletonData。
- Room spawn role 必须能满足波次中的敌人角色和同屏上限。
- 缺少引用、容量不足、ID 冲突或动画缺失时 fail-closed。
- 不在 adapter、installer 或 runtime 中按敌人 ID 特判。

## 修改流程

1. 修改权威 `FPG_*` 资产。
2. 用 Formal Encounter Preview 检查 Request/Plan/digest；预览不得写回资产。
3. 必要时运行 `FpgFormalRoomLoopInstaller` 刷新 Boot/FormalRoom、HUD、出口与 Build Settings。
4. 检查 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs` 与对应 Formal 配置合同。

运行时生命周期与事务规则见 [FPG_Formal_Encounter_Runtime_Contract.zh-CN.md](FPG_Formal_Encounter_Runtime_Contract.zh-CN.md)。
