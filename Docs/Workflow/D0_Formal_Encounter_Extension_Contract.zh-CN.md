# D0 与正式 Encounter 的扩展边界

本文件是 `D0_Production_Line_Contract.zh-CN.md` 的正式 Encounter 补充，不重新解释或扩展原 D0 合同。

- `BattleSession`、`CombatLab`、`D0EncounterDefinition` 和单敌替换/孵化链继续视为 `LegacySingleEnemy`，保持原有公共语义和数据格式。
- 正式普通战斗房使用 `FpgRoomRunRequest -> FpgEncounterPlan -> FpgEncounterSession -> FpgRoomEncounterDirector`，不把多敌、多波或召唤语义塞回 D0 类型。
- D0 资产只可作为迁移或美术来源；正式运行时读取 `FpgEnemyDefinition`、`FpgEnemyPoolDefinition`、`FpgEncounterProfile`、`FpgEncounterOverrideDefinition` 和正式 Entity View。
- 同种敌人的多个实例使用不同 `RuntimeId`、`SpawnEntryId` 和 `SpawnSequence`。旧 D0 的单个 active enemy accessor 不作为正式路径的查询接口。
- 正式 Threat、攻击、投射物、命中、Break、Groggy、死亡和表现锚点都按 owner/target `RuntimeId` 路由。旧 D0 的单敌 Threat schedule 不参与正式仲裁。
- 正式召唤是通用 `Summon` 动作，受每 owner、每 Encounter、递归深度和循环图校验限制；不得增加 Luan ID 特判。
- `CombatLab` 保留为冻结开发场景，不作为正式多敌、多波、出口解锁和清场事件的验收基线。
- 正式 `FpgEncounterHost`、`FpgFormalEncounterHost` 与 `FpgRoomEncounterDirector` 必须位于独立正式场景根，不得挂入 `CombatLab` 或接入其旧 `BattleSession` 宿主。
- 正式房间只发 `FpgRoomClearedEvent` 与出口选择事件。奖励、Boss、跨房路线和 Survival 继续留给后续系统。

固定容量是正式合同的一部分。实体、Hitbox、Threat、Projectile、预警和头顶血条必须在 `Preparing` 阶段完成容量校验和预热；战斗 Tick 内禁止 `Instantiate/Destroy`、扩容与隐式查找。同步或容量绑定失败必须 Fault/Fail-Closed，不能仅记录日志后继续。
