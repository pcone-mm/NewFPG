# Formal Encounter 表现指南

本目录中的 `PF_FPG_FeiEntity`、`PF_FPG_BurstbugEntity`、`PF_FPG_HudieEntity`、`PF_FPG_LuanEntity`、HUD、血条、伤害跳字与出口 prefab 是正式权威资产。

- Enemy prefab 必须使用 `FpgEnemyEntityView`；玩家 prefab 使用 `FpgPlayerEntityView`。
- `Covers/PF_FPG_DefaultCover.prefab` 通过 `FpgCoverEntityView` 独占完好/损毁根和阻挡 collider；耐久、当前项和移动状态属于 Run/RoomDefinition，不得存到 prefab。
- `VFX/PF_FPG_CoverTransition.prefab` 是 `FpgCoverTransitionEffectView` 的 authored wrapper；玩家 prefab 上的 `FpgCoverTraversalPresenter` 只能移动玩家视觉并播放 transition VFX，不能提交 gameplay traversal 或拥有相机状态。
- Prefab 可暴露 binder、anchor、socket、hit part 和 UI port，不承载遭遇状态机或战斗决策。
- Spine 渲染依赖来自 `Presentation/Characters/*/Spine`，不得重新依赖根 `Assets/Art`。
- Enemy `VisualRoot` 上的 `FpgEntitySkeletonRootMotionBridge` 只抽取 Behavior 明确启用的动画和约定 root bone/track；不得用 Rigidbody 模式或移动 anchor 绕过 Entity 根运动合同。
- 场景组合、HUD、出口与 Entity prefab 都是 authored 权威资产，只能在对应 Scene/Prefab/Inspector 中显式修改和保存。
- 修改后检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`、`FpgFormalEnemyRootMotionAssetTests.cs`、`FormalHudGeometryTests.cs` 与 `FormalCombatPresentationStreamTests.cs`。
