# Formal Encounter 表现指南

本目录中的 `PF_FPG_FeiEntity`、`PF_FPG_BurstbugEntity`、`PF_FPG_HudieEntity`、`PF_FPG_LuanEntity`、HUD、血条、伤害跳字与出口 prefab 是正式权威资产。

- Enemy prefab 必须使用 `FpgEnemyEntityView`；玩家 prefab 使用 `FpgPlayerEntityView`。
- `PF_FPG_FeiEntity` 的 identity-pose `FacingRoot` 必须是 `PeekRoot` 直属子节点，且只直接包含 `VisualRoot` 与 `PresentationSockets`；`FpgPlayerFacingController` 位于 Entity 根。`PeekRoot` 的左右移动目标来自当前 RoomDefinition cover slot，prefab 不保存固定 `peekLocalOffset`。gameplay/aim/shot/ground/camera anchor 与权威 socket registry 必须留在 `FacingRoot` 外，避免层级继承造成隐式或双重变换；`ShotOrigin` 的朝向变化只通过显式 Spine bone-follow 在确定性 aim sampling 前刷新。
- `Covers/PF_FPG_DefaultCover.prefab` 是通用模板，`PF_FPG_Root1TreeCover.prefab` 是 `root1.asset` 使用的房间特定表现；所有正式 `PF_FPG_*Cover` 都通过 `FpgCoverEntityView` 独占完好/损毁根和阻挡 collider。耐久、当前项和移动状态属于 Run/RoomDefinition，不得存到 prefab。
- Cover blocker 优先使用 `intactRoot` 下名为 `__ShadowCasterProxy` 的 Mesh，否则使用可渲染 Mesh；每个源 Mesh 的同一对象必须有匹配的非 trigger、非 convex MeshCollider，且不得混入额外 Collider。旧 `blockingColliders` 字段仅保留序列化兼容，不是运行时真源。
- `PF_FPG_Root1TreeCover` 的 shadow proxy 引用 `Presentation/Level/Environment/Generated/SpriteShadowCasters/` 中的 mesh/material；这些引用与 `.meta` 必须成套维护，不得复制进配置资产或按 hash 文件名手工清理。
- `VFX/PF_FPG_CoverTransition.prefab` 是 `FpgCoverTransitionEffectView` 的 authored wrapper；玩家 prefab 上的 `FpgCoverTraversalPresenter` 只能移动玩家视觉并播放 transition VFX，不能提交 gameplay traversal 或拥有相机状态。
- Prefab 可暴露 binder、anchor、socket、hit part 和 UI port，不承载遭遇状态机或战斗决策。
- Spine 渲染依赖来自 `Presentation/Characters/*/Spine`，不得重新依赖根 `Assets/Art`。
- Enemy `VisualRoot` 上的 `FpgEntitySkeletonRootMotionBridge` 只抽取 Behavior 明确启用的动画和约定 root bone/track；不得用 Rigidbody 模式或移动 anchor 绕过 Entity 根运动合同。
- 场景组合、HUD、出口与 Entity prefab 都是 authored 权威资产，只能在对应 Scene/Prefab/Inspector 中显式修改和保存。
- 修改后检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`（含 facing execution order、层级与 Spine ShotOrigin 采样）、`FpgRoomDefinitionTests.cs`、`FpgFormalEnemyRootMotionAssetTests.cs`、`FormalHudGeometryTests.cs` 与 `FormalCombatPresentationStreamTests.cs`。
