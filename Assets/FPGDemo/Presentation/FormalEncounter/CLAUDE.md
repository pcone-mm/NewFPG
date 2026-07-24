# Formal Encounter 表现指南

本目录中的 `PF_FPG_FeiEntity`、`PF_FPG_BurstbugEntity`、`PF_FPG_HudieEntity`、`PF_FPG_LuanEntity`、HUD、血条、伤害跳字与出口 prefab 是正式权威资产。

- Enemy prefab 必须使用 `FpgEnemyEntityView`；玩家 prefab 使用 `FpgPlayerEntityView`。
- Prefab 可暴露 binder、anchor、socket、hit part 和 UI port，不承载遭遇状态机或战斗决策。
- Spine 渲染依赖来自 `Presentation/Characters/*/Spine`，不得重新依赖根 `Assets/Art`。
- `FpgFormalRoomLoopInstaller` 只刷新场景组合、HUD 与出口绑定，不重新生成正式 Entity prefab。
- 修改后检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`、`FormalHudGeometryTests.cs` 与 `FormalCombatPresentationStreamTests.cs`。
