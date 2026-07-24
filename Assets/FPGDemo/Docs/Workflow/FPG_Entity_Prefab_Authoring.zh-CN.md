# FPG Entity Prefab 制作合同

本合同适用于正式玩家与敌人实体。权威入口是：

- `Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_FeiEntity.prefab`
- `Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_BurstbugEntity.prefab`
- `Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_HudieEntity.prefab`
- `Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_LuanEntity.prefab`

## 所有权

- 正式 Entity prefab 是人工维护的完整绑定层，不再继承或变体化旧 D0 Entity prefab。
- Spine/SkeletonData、Atlas、材质和贴图位于 `Presentation/Characters/*/Spine`，只承担渲染。
- `SourceArt/CZN` 与 `Assets/Imported/CZN` 是源输入，不承载 hitbox、socket 或 gameplay 配置。
- Character/Enemy Definition 只引用完整正式 Entity prefab，不直接引用临时场景对象或旧 Generated Entity。

## 玩家合同

`PF_FPG_FeiEntity` 根节点必须包含 `FpgPlayerEntityView`、`CharacterController`、`FpgPlayerBounds` 和正式玩家表现组件。它必须显式提供：

- Gameplay/Visual 根；
- Aim、Ground、CameraPivot；
- Body hitbox；
- `D0ActorSocketRegistry` 中稳定的主射、副射和默认攻击 socket；
- `FpgPlayerBarrierPresentationController`。

D0 前缀的 Presentation/Socket 类型是序列化兼容合同，不是旧运行主线。

## 敌人合同

正式敌人根节点使用 `FpgEnemyEntityView`，并显式提供：

- Gameplay、Projectile、Weakpoint、OverheadHealthBar anchor；
- 与 `HitPart` 数组平行的 Collider 列表；
- SkeletonAnimation；
- 由 `FpgEnemyDefinition`、Behavior 与 Attack 定义校验的动画键。

池与 Director 拥有 runtime ID、spawn sequence、激活和回收；Prefab 不保存长期战斗状态。

## 修改流程

1. 在 Prefab Mode 修改正式 Entity prefab。
2. 只在 `Characters/*/Spine` 替换渲染依赖，保持 Entity GUID 和稳定路径。
3. 更新对应正式 Character/Enemy/Behavior/Attack 资产。
4. 通过 Unity 保存，让 AssetDatabase 维护 Prefab 与 `.meta`。
5. 检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs` 和 `FormalFirstAuthoringContractTests.cs`。
