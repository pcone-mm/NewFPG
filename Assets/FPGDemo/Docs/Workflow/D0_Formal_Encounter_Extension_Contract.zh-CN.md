# D0 与正式 Encounter 扩展合同

本文档固定正式多敌 Encounter 与旧 D0 战斗切片之间的边界。正式系统通过新增类型和宿主扩展，不改造旧单敌链来承载多敌。

## 冻结范围

以下内容保持冻结，并且不作为正式 Encounter 的功能或验收基线：

- `BattleSession` 及其 `BattleSessionHost` 公共合同；
- `CombatLab` 场景、D0 HUD、表现协调器和旧对象绑定；
- Burstbug、Luan、Hudie 的 D0 单敌/替换演示链；
- 旧 Scenario、旧快照、旧单敌诊断和旧一键试玩流程。

正式路径使用 `FpgEncounterSession`、`FpgRoomEncounterDirector`、固定容量 Roster、owner-aware Threat/Attack API 和正式场景宿主。 正式 `FpgEncounterHost`、`FpgFormalEncounterHost` 与 `FpgRoomEncounterDirector` 必须由独立正式场景根显式组合，不得挂入 `CombatLab` 或复用其旧 `BattleSession` 宿主。

## 不得破坏的 D0 公共语义

- `BattleSession.Enemy` 与 `EnemyRuntimeId` 始终表示当前唯一活动敌人；不得改成集合、任意目标或隐式“第一个敌人”。
- 旧敌人替换仍通过 `EnemyRuntimeChanged` 完成 RuntimeId、Hitbox、Projectile 和表现重绑定；不得让正式 Roster 事件进入该链。
- `BattleSession` 的 `NotStarted/Running/Paused/Completed/Faulted/Disposed` 状态转换、拒绝原因和命令提交语义保持不变。
- 旧胜负条件、单敌生命/韧性、Break/Groggy、攻击调度、Threat、Projectile 和 Trace/Snapshot 字段含义保持不变。
- 暂停时旧 Session 不推进 Tick；重开创建并重新绑定完整旧 Session；Dispose 后不得继续 Pump。
- `BattleSessionHost.SessionRestarted`、`EnemyRuntimeChanged` 及现有公开属性/事件的触发顺序和可观察结果保持兼容。
- CombatLab 的旧全局单敌血条、诊断文本、输入、相机、音频和表现路由继续只消费旧 Session。
- D0 Room 一键试玩继续使用所选旧 Scenario 和临时房间覆盖，不得被正式 Preview 按钮、Profile 或 Override 改写。
- 不修改旧资产的稳定 ID、序列化字段名、Prefab/Scene 引用或现有迁移菜单语义。

## 正式系统允许的扩展

- 可以复用 `CombatKernel`、`PlayerRuntime`、`EnemyRuntime` 和端口的内部能力，但不得扩大 `BattleSession` 的单敌公共表面。
- 正式敌人、波次、召唤、Roster、AnchorMap、对象池和头顶血条使用独立类型与容量。
- 正式 Entity Prefab 可以从旧视觉 Prefab 派生，但 Installer 必须生成独立资产，不得给旧 D0 Prefab 写入正式绑定。
- 奖励、跨房跳转和后续模式只能消费正式房间事件，不得回写或劫持旧 CombatLab 完成语义。

## 验收边界

D0 回归只验证冻结合同未退化；正式 Encounter 验收必须在正式 Request/Plan/Director/Session 路径完成。不得以 CombatLab 单敌可运行、Luan→Hudie 旧替换可运行或旧 `BattleSession` 测试通过，替代多实例、波次、召唤、容量和清场验收。
