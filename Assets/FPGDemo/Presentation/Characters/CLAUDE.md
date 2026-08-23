# Characters 表现指南

本目录是玩家与敌人长期表现资产的权威边界。角色资产先按阵营分为 `Players/` 与 `Enemies/`，再按稳定角色名聚合；敌人共用资源位于 `Enemies/Shared/`。

- 每个角色目录使用 `Prefabs/`、`Spine/` 与可选 `VFX/` 分工：`Prefabs/PF_FPG_*Entity.prefab` 是正式运行时实体，`Spine/` 保存 SkeletonData、Atlas、材质、贴图及必要渲染 prefab，`VFX/` 保存该角色自有表现 wrapper。
- `Players/Fei/Prefabs/PF_FPG_FeiEntity.prefab` 使用 `FpgPlayerEntityView`；`Enemies/Burstbug|Hudie|Luan/Prefabs/PF_FPG_*Entity.prefab` 使用 `FpgEnemyEntityView`。不要恢复无正式消费者的 `PF_D0_*Entity` 平行入口。
- Entity prefab 只拥有视觉层级、anchor、socket、hit part、binder 与局部表现组件；遭遇状态机、战斗决策和长期 gameplay 状态属于正式 session/director。
- `PF_FPG_FeiEntity` 的 identity-pose `FacingRoot` 必须是 `PeekRoot` 直属子节点，且只直接包含 `VisualRoot` 与 `PresentationSockets`；`FpgPlayerFacingController` 位于 Entity 根。`PeekRoot` 的左右移动目标来自当前 RoomDefinition cover slot，prefab 不保存固定 `peekLocalOffset`。
- gameplay/aim/shot/ground/camera anchor 与权威 socket registry 必须留在 `FacingRoot` 外，避免层级继承造成隐式或双重变换；`ShotOrigin` 的朝向变化只通过显式 Spine bone-follow 在确定性 aim sampling 前刷新。
- 玩家 `FpgCoverTraversalPresenter` 只能移动玩家视觉并播放 `Presentation/Level/Covers/VFX/PF_FPG_CoverTransition.prefab`，不能提交 gameplay traversal 或拥有相机状态。
- Enemy `VisualRoot` 上的 `FpgEntitySkeletonRootMotionBridge` 只抽取 Behavior 明确启用的动画和约定 root bone/track；不得用 Rigidbody 模式或移动 anchor 绕过 Entity 根运动合同。
- 正式技能配置只引用 `Players/*/VFX/PF_FPG_*` 或 `Enemies/Shared/VFX/PF_FPG_*` wrapper；wrapper 可显式依赖 `Assets/VFX_Klaus/` 源材质、网格或 prefab，但不得引用供应商 Timeline/VFX_Lab demo。
- Spine 渲染依赖不得重新引用根 `Assets/Art`；Prefab、渲染输入与各自 `.meta` 必须成套维护。
- 修改后检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`、`FpgFormalEnemyRootMotionAssetTests.cs`、`FpgSkillAuthoringChoicesTests.cs`、`FpgFormalSkillPresentationV3AssetTests.cs` 与相关玩家技能资产合同。
