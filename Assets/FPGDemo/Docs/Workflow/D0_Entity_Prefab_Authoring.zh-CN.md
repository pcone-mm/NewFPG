# D0 Entity Prefab 制作与调整规范

本文适用于 D0 的玩家角色和敌人，包括 Fei、Burstbug、Luan、Hudie。两类实体遵守同一套资产边界：身份和技能选择实体，Entity Prefab 保存可编辑结构，Generated Render Prefab 只提供自动生成的渲染依赖。

## 唯一资产链路

```text
D0CharacterDefinition / D0EnemyDefinition
以及武器、攻击、召唤技能 ScriptableObject
                    │ 选择
                    ▼
唯一可人工编辑的 Entity Prefab
├─ GameplayRoot
├─ VisualRoot
│  └─ Generated Render Prefab（嵌套，只读）
├─ Hitboxes / Weakpoint
├─ D0ActorSocketRegistry
└─ 本地表现与玩家/敌人专属组件
```

- `D0CharacterDefinition` 和 `D0EnemyDefinition` 管身份、数值、行为引用及 `EntityPrefab`。Prefab 不保存角色 ID，避免 Definition 与 Prefab 出现两个身份真源。
- 武器、敌人攻击和召唤技能管本技能的动画、VFX、音频、表现时序和 Socket ID。技能不得保存场景 Transform。
- `D0ActorPresentationDefinition` 只管 Actor 状态表现，例如待机、受击、Break、死亡、胜利和失败。
- Entity Prefab 是姿态、碰撞体、弱点、锚点和 Socket 的唯一人工编辑入口。运行时不得再用 Definition 中的视觉姿态覆盖它。
- Generated Render Prefab 只保留 `SkeletonAnimation`、Renderer、材质、Atlas 和 SkeletonData 等生成内容。场景、Definition 和技能资产不得直接引用它，只允许 Entity Prefab 将其作为嵌套渲染依赖。

## Entity Prefab 结构

公共基类 `D0ActorEntityView` 统一暴露 `GameplayAnchor`、`VisualRoot`、`Actor2DPresenter`、`SkeletonAnimation` 和 `D0ActorSocketRegistry`。

- 玩家使用 `D0PlayerEntityView`，在完整 Prefab 内维护 CharacterController、移动控制、Bounds、AimAnchor、GroundAnchor、CameraPivot、BodyHitbox 和 Barrier。SessionHost、Main Camera、房间边界等场景服务必须在会话初始化时注入，不得写回 Prefab。
- 敌人使用 `D0EnemyEntityView`，在完整 Prefab 内维护 gameplay 锚点、Body/Weakpoint Hitbox 和弱点引用。敌人由 `D0EnemyEntityWorld` 动态实例化、注册和注销。
- 玩家完整 Prefab 实例驻留场景；敌人场景中只放空的 EntityWorld。两者的 authored local pose 都以 Entity Prefab 为准，重开时恢复该姿态。

## Socket Registry

`D0ActorSocketRegistry` 使用稳定字符串 ID。当前公共 ID 包括：

- `weapon.primary.muzzle`：玩家主射枪口。
- `weapon.secondary.muzzle`：玩家副射枪口。
- `attack.default.origin`：敌人攻击或召唤表现的默认起点。

每项 Registry 绑定包含 `socketId`、Prefab 内的 Transform、跟随模式和可选 Spine `boneName`。约束如下：

- ID 必须非空、无首尾空白，并在同一 Entity Prefab 内唯一。
- Transform 必须是 Registry 所在实体的子节点，且一个 Transform 只能绑定一个 ID。
- `AuthoredTransform` 直接使用 Prefab 中编辑的局部姿态，适合没有可靠骨骼的挂点。
- `SpineBone` 必须填写可靠的 `boneName`，由 `D0SpineSocketFollower` 在 Spine `UpdateComplete` 后更新 Transform。
- 技能只保存稳定 ID。Definition、场景和 Generated Prefab 都不保存枪口或攻击起点的 Transform 副本。

## 调整 Fei 射击特效挂点

Fei 的人工维护入口是 `Assets/FPGDemo/Presentation/Actors/Fei/PF_D0_FeiEntity.prefab`。不要修改嵌套的 Generated Prefab，也不要在 CombatLab 场景里移动一份视觉副本。

1. 在 Prefab Mode 打开 `PF_D0_FeiEntity.prefab`。
2. 在 `SocketRegistry` 下选择 `PrimaryMuzzle` 或 `SecondaryMuzzle` Transform。
3. 调整目标 Transform 的 Local Position、Local Rotation；需要时调整 Local Scale，但通常应保持 `(1, 1, 1)`。
4. 在 `D0ActorSocketRegistry` 中确认它仍分别绑定 `weapon.primary.muzzle` 或 `weapon.secondary.muzzle`，且跟随模式符合需求。
5. 保存 Entity Prefab。无需重生成 Spine Prefab，也无需改 Stage、Room、CombatLab 场景或重新烘焙角色表现。
6. 进入 CombatLab 验证主射、副射 VFX 从各自位置出现，同时逻辑射线、命中与伤害保持不变。

Fei 当前没有可确认的 muzzle 骨骼，因此主、副射使用两个独立的普通 Transform；初始位置均为 `(0.72, 0.42, -0.06)`。后续只有在骨骼名称和动画表现经过验证后，才将对应项改为 `SpineBone`。

敌人的攻击挂点采用同一流程：编辑对应完整 Entity Prefab 的 Socket Transform，再确认 `D0EnemyAttackDefinition` 或召唤技能引用该稳定 ID。

## Stage、Room 与会话边界

- Stage 和 Room 只描述环境、阻挡、玩家入口和敌人出生点，不拥有角色、怪物、技能表现、枪口或 Actor 视觉位置。
- Encounter 的 SpawnSlot 只决定何时生成或替换哪个 EnemyDefinition、使用哪个出生点以及姿态继承策略。
- `BattleSceneContext` 只连接玩家 Entity、EnemyEntityWorld、VFX World 和场景服务；它不再是角色结构或视觉姿态的真源。
- HitboxRegistry 的场景静态绑定只保留环境阻挡。玩家和敌人的 hitbox 随 EntityView 动态注册、注销。

## Installer 与生成器

- 生成器只更新 Generated Render Prefab 及其渲染依赖。
- Installer 可以创建缺失的初始 Entity Prefab，但已存在的人工 Entity Prefab 只能校验，禁止覆盖 Transform、Collider、Socket、组件引用或 GUID。
- Installer 不得把场景或 Definition 重新绑定到 Generated Prefab，也不得创建角色专属场景视觉树。
- 连续执行两次生成器或 Installer 后，人工 Entity Prefab 的 Transform、Collider、Socket、组件引用和 GUID 必须保持不变。

## 提交前检查

- 四个角色 Definition 都只通过 `EntityPrefab` 获得实体结构。
- 场景和策划资产没有直接引用任何 Generated 角色 Prefab。
- 每个技能引用的 Socket ID 都能在所属 Entity Prefab 中解析。
- 敌人生成后使用 Prefab authored pose；重开后玩家和敌人的局部姿态恢复。
- 会话启动后的射击和攻击热路径不执行角色 VFX 的 Instantiate/Destroy。
- Stage/Room 只保留环境与出生点职责。
