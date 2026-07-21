# 陆鸾召唤蝴蝶配置指南

本文说明 D0 复合遭遇中“陆鸾召唤蝴蝶”的唯一配置链路。运行时不使用专用场景桥或隐藏视觉后端；Scenario、召唤技能、Encounter SpawnSlot 和 `D0EnemyEntityWorld` 共同完成切换。

## 配置入口

```text
D0CombatScenarioDefinition
├─ Encounter：陆鸾 / 蝴蝶 SpawnSlot 与攻击日程
└─ LuanSummonHudie：D0LuanSummonHudieDefinition
                         ├─ 蝴蝶 EnemyDefinition
                         ├─ Tick 与召唤/出现表现
                         └─ Socket / VFX / Audio / Motion
```

CombatLab 入口资产为 `Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsLuanSummonsHudie.asset`。它直接引用 `Assets/FPGDemo/Config/D0Slice/Definitions/Luan/D0_Luan_SummonHudie.asset`，不经过额外的 presentation bridge 资产。

## 当前时序

D0 固定以 60 Tick/秒换算召唤时序：

- Tick `240`：`summonDelaySeconds = 4` 到期，陆鸾开始召唤。播放召唤动画、召唤 VFX、召唤音频，并按配置采样召唤动画位移。
- Tick `284`：总延迟 `4 + 0.7333` 秒换算后的出现 Tick。Encounter 的第二个 SpawnSlot 在此 Tick 生效。
- Tick `284` 的替换由 `D0EnemyEntityWorld` 完成：陆鸾实体注销，实例化蝴蝶自己的 Entity Prefab，并按 SpawnSlot 的姿态策略继承陆鸾的 gameplay 世界姿态。
- 蝴蝶的视觉、Socket、弱点和 hitbox 始终来自蝴蝶 Entity Prefab；不能继续沿用陆鸾或场景中的临时视觉节点。

不要在场景里用延时显隐两个角色来模拟切换。Tick、实体身份和姿态继承都必须来自会话和 Encounter 数据。

## 召唤技能的职责

`D0LuanSummonHudieDefinition` 持有且只持有召唤这段流程需要的数据：

- 召唤目标 `HudieEnemy`。
- 召唤延迟、出现延迟，以及由它们换算出的 Tick。
- 陆鸾召唤动画和蝴蝶出现动画。
- 召唤/出现 VFX 的稳定 key、Prefab、预热容量和持续时间。
- 召唤/出现使用的音频 cue。
- 陆鸾召唤 Socket ID 与蝴蝶出现 Socket ID。
- 召唤/出现动画位移的启用状态、动画名、motion bone 和末帧偏移策略。

VFX 由 `D0CombatVfxWorld` 在会话开始前扫描并预热，战斗热路径只取还池对象。Socket ID 必须分别能在陆鸾和蝴蝶的 Entity Prefab 中解析。

## Actor、Attack 与召唤状态归属

- `D0LuanSummonHudieDefinition`：只负责陆鸾召唤和蝴蝶出现这一段状态转换表现。
- `D0EnemyAttackDefinition`：负责蝴蝶每个具体攻击的攻击动画、攻击 VFX、音频、攻击 Socket 和表现时序。
- `D0ActorPresentationDefinition`：负责陆鸾或蝴蝶的待机、受击、Break、死亡等 Actor 状态表现。
- `D0EnemyDefinition`：负责敌人身份、数值、行为、Actor 状态表现引用和完整 Entity Prefab 引用。
- Encounter SpawnSlot：只负责替换 Tick、出生点和姿态策略，不保存召唤动画、VFX、音频或 Socket。

同一字段不得在召唤技能、Actor 表现和攻击定义之间复制。需要调整蝴蝶普通攻击时，修改具体攻击定义；需要调整死亡或 Break 时，修改蝴蝶 Actor 表现；只有调整召唤/出现过程时才修改召唤技能。

## 常用调整

调整召唤开始时间：修改 `summonDelaySeconds`，并同步确认 Encounter 中蝴蝶 SpawnSlot 的 `spawnTick` 等于新的出现 Tick。

调整蝴蝶出现时间：修改 `appearanceDelaySeconds`。出现 Tick 是召唤延迟与出现延迟之和换算到 60 Tick/秒后的结果；SpawnSlot 必须使用相同 Tick。

调整召唤或出现位置：

1. 在陆鸾或蝴蝶完整 Entity Prefab 中移动对应 Socket Transform。
2. 确认 Registry 中的稳定 ID 不变。
3. 确认召唤技能的 `summonSocketId` / `appearanceSocketId` 能解析。
4. 不要移动场景 EnemyAnchor，也不要编辑 Generated Render Prefab。

调整动画位移：只在资源存在可靠 motion bone 时启用对应 Motion 设置，并保证 Motion 的动画名与召唤/出现动画一致。该位移是表现采样，不替代 EntityWorld 的 gameplay 世界姿态继承。

## 验证清单

- Scenario 直接引用一个有效的 `D0LuanSummonHudieDefinition`。
- 陆鸾是第一个 SpawnSlot，蝴蝶是第二个 SpawnSlot；当前出现 Tick 为 `284`，姿态策略为继承陆鸾 gameplay 世界姿态。
- Tick `240` 触发召唤表现，Tick `284` 完成实体替换和蝴蝶出现表现。
- 陆鸾召唤 Socket 与蝴蝶出现 Socket 都能从各自 Entity Prefab 解析。
- 替换后活动 hitbox、弱点、视觉和 Socket 全部来自蝴蝶 Entity Prefab。
- 普通攻击表现来自具体攻击定义；待机、受击、Break、死亡来自 Actor 状态表现。
- 场景中不存在陆鸾/蝴蝶专用桥、隐藏 Presenter、Showcase 视觉树或 Generated 角色实例。
- 会话启动后，召唤和攻击 VFX 不执行 Instantiate/Destroy。
