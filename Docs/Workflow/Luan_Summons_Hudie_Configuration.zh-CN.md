# CombatLab：陆鸾与蝴蝶独立配置、孵化关系与测试

## 配置原则

陆鸾（`Luan`）和蝴蝶（`Hudie`）是两只独立的怪物，不是同一只怪物的两个形态：

- 蝴蝶可以单独作为 `FeiVsHudie` 的敌人进入 CombatLab。
- 陆鸾可以单独作为一只怪物配置；它额外拥有“孵化蝴蝶”技能。
- `D0_Luan_SummonHudie.asset` 只保存对蝴蝶敌人定义的引用与孵化规则，不能内嵌、复制或拥有蝴蝶的攻击、行为、表现预制体或 Spine 资源。
- 当前 D0 的 Encounter 可用两个 SpawnSlot 编排“陆鸾 → 蝴蝶”替换，但战斗域仍只有一个活动 `EnemyRuntime`；它只支持一次孵化一只蝴蝶，不支持多只同场或蝴蝶死亡后的重召。陆鸾不应被配置为“变身成蝴蝶”。

`Assets/Art/Monster/luan/` 与 `Assets/Art/Monster/hudie/` 始终是美术源资源，不能移动、删除或直接作为运行时 PMA 资源绑定。

## 资产归属

| 归属 | 资产位置 | 职责 |
|---|---|---|
| 陆鸾 | `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Enemy.asset` | 陆鸾自身的敌人定义。 |
| 陆鸾 | `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Behavior.asset` | 陆鸾自身的行为配置。 |
| 陆鸾 | `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_Presentation.asset` | 陆鸾自身的表现配置。 |
| 陆鸾 | `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_SummonHudie.asset` | 陆鸾的孵化技能；只引用 `D0_Hudie_Enemy.asset` 与孵化规则。 |
| 陆鸾 | `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_LuanSummonsHudie_Encounter.asset` | 陆鸾孵化蝴蝶的组合遭遇配置。 |
| 蝴蝶 | `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Enemy.asset` | 蝴蝶自身的敌人定义。 |
| 蝴蝶 | `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Behavior.asset` | 蝴蝶自身的行为配置。 |
| 蝴蝶 | `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Presentation.asset` | 蝴蝶自身的表现配置。 |
| 蝴蝶 | `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/D0_Hudie_Encounter.asset` | 蝴蝶独立出场时使用的遭遇配置。 |
| 蝴蝶 | `Assets/FPGDemo/Config/D0Slice/Definitions/Hudie/Attacks/D0_Hudie_Attack_Bullet.asset` | 蝴蝶单发投射物攻击。 |
| 陆鸾表现 | `Assets/FPGDemo/Presentation/Luan/Spine/`、`Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab` | 陆鸾的派生 Spine 资源；EntityPrefab 自包含视觉根、gameplay 根、投射锚点、弱点与命中体。 |
| 蝴蝶表现 | `Assets/FPGDemo/Presentation/Hudie/Spine/`、`Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab` | 蝴蝶的派生 Spine 资源；EntityPrefab 自包含视觉根、gameplay 根、投射锚点、弱点与命中体。 |
| 测试 Scenario | `Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsLuanSummonsHudie.asset` | 陆鸾孵化蝴蝶的测试入口。 |
| 测试 Scenario | `Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsHudie.asset` | 蝴蝶独立出现的测试入口。 |

若保留 `D0_LuanSummonsHudie_Presentation.asset`，它仅是组合场景的表现桥接资产：可引用陆鸾技能和两个已拆分的表现配置，但不能成为保存蝴蝶基础配置的第二个位置。

## 表现资产的 Inspector 显示

`D0_*_Presentation.asset` 共用同一个序列化类型，因此文件内部会保留玩家、敌人与敌人专属特效三组数据；这保证既有 YAML 键和旧资产兼容，并不表示每组都会在运行时使用。

- `actorKind = Player` 时，只显示并使用“玩家表现”。
- `actorKind = Enemy` 时，只显示并使用“敌人表现”和“敌人专属特效”；陆鸾、蝴蝶与 Burstbug 都属于这一种。
- 被隐藏的另一组仍会保留在资产中，不会被 Inspector 删除，也不会参与当前类型的运行时读取。只有明确要把资产改造成另一类角色时，才应切换 `actorKind` 并配置新显示的一组。

```text
D0_Luan_Enemy
  ├─ D0_Luan_Behavior / D0_Luan_Presentation
  ├─ PF_D0_LuanEntity（visual/gameplay/投射锚点/弱点/命中体）
  └─ D0_Luan_SummonHudie
       └─ D0_Hudie_Enemy
            ├─ D0_Hudie_Behavior / D0_Hudie_Presentation
            ├─ PF_D0_HudieEntity（visual/gameplay/投射锚点/弱点/命中体）
            └─ D0_Hudie_Attack_Bullet

D0_CombatLab_FeiVsLuanSummonsHudie
  ├─ playerSpawnPointId: player-main
  ├─ D0_CombatLab_Stage（环境 + player-main / enemy-main）
  └─ D0_LuanSummonsHudie_Encounter
       ├─ SpawnSlot 1: Luan / enemy-main / Tick 0 / AtSpawnPoint
       └─ SpawnSlot 2: Hudie / enemy-main / Tick 284 / InheritPreviousGameplayPose

D0_CombatLab_FeiVsHudie
  ├─ playerSpawnPointId: player-main
  ├─ D0_CombatLab_Stage（环境 + player-main / enemy-main）
  └─ D0_Hudie_Encounter
       └─ SpawnSlot 1: Hudie / enemy-main / Tick 0 / AtSpawnPoint
```

## `D0_Luan_SummonHudie.asset` 的边界

该资产只负责陆鸾“调用哪一种蝴蝶”和“何时调用”。Encounter 把这条技能规则编译为后续 SpawnSlot：当前标准值为 `definitionId = 2`、`spawnTick = 284`、`posePolicy = InheritPreviousGameplayPose`。EntityWorld 到 Tick 后切换活动 EntityPrefab，战斗域则仍沿用单 EnemyRuntime 的替换生命周期。

| 字段 | 默认值 | 说明 |
|---|---:|---|
| `hudieEnemy` | `D0_Hudie_Enemy` | 唯一的蝴蝶配置引用。 |
| `summonDelaySeconds` | `4.0` | 陆鸾开始孵化前的等待时间。 |
| `appearanceDelaySeconds` | `0.7333` | 孵化表现开始后，蝴蝶出现的可调可见切点。 |
| `appearanceAnimationMotion.enabled` | `false` | 是否把蝴蝶出生动画中的 `gameplay_motion` 平移应用到实体；美术 marker 未交付前必须关闭。 |
| `appearanceAnimationMotion.animationName` | `appear` | 被采样的出生动画名，必须与实际播放动画一致。 |
| `appearanceAnimationMotion.motionBoneName` | `gameplay_motion` | 无父骨、无 Slot/可见后代的顶层位移 marker。 |
| `appearanceAnimationMotion.persistEndOffset` | `true` | 出生结束后保留最终实体位移；中断时保留当前位移。 |

当前不要把“最大同时数量”或“蝴蝶死亡后重召”视为可配置功能：单 `EnemyRuntime` 没有对应的运行时消费者。多只蝴蝶与重召都待多敌人 Runtime 支持后再设计和开放。

不得把以下内容放回此资产：蝴蝶 EntityPrefab、SkeletonData、完整动画映射、命中体、投射物起点、攻击排期、伤害数值，或另一份蝴蝶敌人定义。EntityPrefab 只由 `D0_Hudie_Enemy.asset` 引用；敌人、SpawnPoint、生成 Tick 与姿态策略只在 Encounter 的 SpawnSlot 中组合。

其中 `appearanceAnimationMotion.animationName` 是技能位移采样合同，不是蝴蝶表现资源的第二份归属；安装器从既有蝴蝶动画映射填入该值。完整制作合同与叠加规则见 `Docs/Workflow/D0_Skill_Animation_Motion_Configuration.zh-CN.md`。

## 配置/安装顺序

安装器维护派生资源、EntityPrefab 与配置资产，但不会偷偷切换测试 Scenario。Combat Slice 安装器只需在首次搭建或修复环境、SpawnPoint 和场景基础设施时运行；切换 Scenario 不再重建角色摆位或敌人预制体。

1. 让 Unity 完成脚本编译，运行菜单 `FPG Demo/D0 2.5D/Install or Update Luan Hudie Configurations`，创建或更新陆鸾、蝴蝶的拆分配置、EntityPrefab 和 Encounter SpawnSlot。
2. 若 CombatLab 尚未安装本版 SpawnPoint/EntityWorld 基础设施，运行一次 `FPG Demo/D0 2.5D/Install or Update Combat Slice`；普通 Scenario 切换不重复这一步。
3. 选中 `Assets/FPGDemo/Config/BattleScenarioConfig.asset`，显式设置 `authoredScenario`：
   - 测试陆鸾孵化蝴蝶：`D0_CombatLab_FeiVsLuanSummonsHudie.asset`；
   - 测试蝴蝶独立出场：`D0_CombatLab_FeiVsHudie.asset`。
4. 运行菜单 `FPG Demo/D0 2.5D/Validate Planner Configuration`；验证应确认 Scenario 的 `playerSpawnPointId`、所有 SpawnSlot 的 SpawnPoint、敌人 EntityPrefab 与 prefab-owned 命中体合同。若报错，先修复引用，不要手改 `.asset`、`.prefab` 或 `.unity` YAML。
5. 打开 `Assets/FPGDemo/Scenes/Boot.unity`，再进入 Play Mode，由主管/测试人员执行试玩。

`authoredScenario` 是运行时组合选择，不是场景烘焙开关。保存后可直接重启会话读取新组合；Stage 不持有当前敌人的视觉位置或 Prefab。

## 两个测试入口应验证什么

| Scenario | 应验证的配置 | 不应依赖的配置 |
|---|---|---|
| `FeiVsLuanSummonsHudie` | 陆鸾自身配置、`D0_Luan_SummonHudie` 对蝴蝶的引用、孵化延迟、一次孵化一只的当前限制与组合表现桥接。 | 不要把蝴蝶的攻击或表现复制进陆鸾目录。 |
| `FeiVsHudie` | 蝴蝶自己的敌人、行为、表现、遭遇与 `D0_Hudie_Attack_Bullet`。 | 不要选择陆鸾技能或组合表现桥接资产。 |

蝴蝶的 `attack` 只是表现：真实单发投射物仍由 D0 攻击配置在 `ReleaseCommitted` 时创建。攻击、命中、护盾/生命结算不能由 Spine Event、动画帧或展示预制体上的 Collider/Rigidbody 驱动。

## 当前 D0 的重要限制

当前 D0 战斗域只有一个活动 `EnemyRuntime`。EntityWorld 会按 Encounter SpawnSlot 预备陆鸾与蝴蝶各自的 EntityPrefab，但任一时刻只激活并 gameplay-bind 当前槽位；切到蝴蝶时，上一槽位解除命中绑定并停用。

- 陆鸾与蝴蝶不再共享场景里的临时敌人锚点、命中体或投射端口；每个 `D0EnemyDefinition.EntityPrefab` 都拥有自己的视觉/gameplay 根、投射锚点、弱点和命中体。
- `InheritPreviousGameplayPose` 只传递替换瞬间的 gameplay 世界姿态，不转移 Prefab 所有权，也不让两套命中体同时生效。
- 组合场景可以验证 SpawnSlot、孵化时序、实体替换和蝴蝶攻击，但不能证明陆鸾与蝴蝶同时拥有独立生命、目标选择、AI、死亡或并行攻击。
- 真正支持两只怪同场仍需扩展多敌人 Runtime、目标选择、独立生命/Break、攻击并行、死亡清理及 UI/日志归属；不能仅靠把两个 EntityPrefab 同时显示出来完成。

## 试玩交接清单

- `FeiVsHudie`：蝴蝶无需陆鸾即可由初始 SpawnSlot 出现，使用自己的 EnemyDefinition 与 EntityPrefab。
- `FeiVsLuanSummonsHudie`：陆鸾与蝴蝶分别来自 SpawnSlot 1/2；后者在 Tick 284 继承上一 gameplay 姿态并成为唯一活动实体。
- 两条路径：Stage 只提供 `player-main` / `enemy-main`；切换 Scenario 不依赖重建场景角色摆位。
- 两条路径：不出现 Burstbug 专属动画/特效叠加；投射物从当前活动 EntityPrefab 的投射锚点生成并正确结算。
- 组合路径：明确按“单活动 EnemyRuntime 替换”验收，不将其误报为真正的双怪同场战斗。

所有 Play Mode 结果都应由主管/测试人员记录；本文只定义配置与验收边界，不替代实际试玩结论。
