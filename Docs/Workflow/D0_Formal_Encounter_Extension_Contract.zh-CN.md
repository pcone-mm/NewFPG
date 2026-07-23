# D0 与正式 Encounter 的扩展边界

本文件是 `D0_Production_Line_Contract.zh-CN.md` 的正式 Encounter 补充，不重新解释或扩展原 D0 合同。

- `BattleSession`、`CombatLab`、`D0EncounterDefinition` 和单敌替换/孵化链继续视为 `LegacySingleEnemy`，保持原有公共语义和数据格式。
- 正式普通战斗房使用 `FpgRoomRunRequest -> FpgEncounterPlan -> FpgEncounterSession -> FpgRoomEncounterDirector`，不把多敌、多波或召唤语义塞回 D0 类型。
- D0 资产只可作为迁移或美术来源；正式运行时读取 `FpgEnemyDefinition`、`FpgEnemyPoolDefinition`、`FpgEncounterProfile`、`FpgEncounterOverrideDefinition` 和正式 Entity View。
- 编辑器 installer 可以读取已导入的 D0 prefab、攻击和状态表现字段作为迁移输入，但落盘结果必须是独立 `FPG_*` 配置；正式 Player Loop 不得保留 D0 定义依赖。
- 同种敌人的多个实例使用不同 `RuntimeId`、`SpawnEntryId` 和 `SpawnSequence`。旧 D0 的单个 active enemy accessor 不作为正式路径的查询接口。
- 正式 Threat、攻击、投射物、命中、Break、Groggy、死亡和表现锚点都按 owner/target `RuntimeId` 路由。旧 D0 的单敌 Threat schedule 不参与正式仲裁。
- 正式 Enemy View 必须在配置校验时确认 SkeletonData 与 entry/idle/death/attack 动画键存在。攻击或召唤真正启动后，表现通知按 `RuntimeId + SpawnSequence + AttackPatternId` 路由；表现播放失败只记诊断，不反向改变战斗仲裁。
- Summon 的 `telegraph + windup` 由正式攻击调度转换成通用释放延迟；表现启动后仅因 owner 死亡或 session 清理取消，到期仍通过既有 SpawnQueue、容量和召唤账本，不得由表现层直接实例化。
- 正式召唤是通用 `Summon` 动作，受每 owner、每 Encounter、递归深度和循环图校验限制；不得增加 Luan ID 特判。
- `CombatLab` 保留为冻结开发场景，不作为正式多敌、多波、出口解锁和清场事件的验收基线。
- 正式 `FpgEncounterHost`、`FpgFormalEncounterHost` 与 `FpgRoomEncounterDirector` 必须位于独立正式场景根，不得挂入 `CombatLab` 或接入其旧 `BattleSession` 宿主。
- 正式房间只发 `FpgRoomClearedEvent` 与出口选择事件。奖励、Boss、跨房路线和 Survival 继续留给后续系统。
- FormalRoom 的玩家 HUD、敌人头顶生命条、逐 Impact 跳字和正式准星统一读取正式快照/反馈流，配置入口与主管验收表见 [D0 FormalRoom 战斗 HUD 与反馈配置](D0_Formal_Combat_Feedback_Configuration.zh-CN.md)。

固定容量是正式合同的一部分。实体、Hitbox、Threat 与 Projectile 等战斗资源必须在 `Preparing` 阶段完成容量校验和预热；其同步或容量绑定失败必须 Fault/Fail-Closed。头顶生命条、伤害跳字、准星和其他纯表现池同样只允许在准备阶段预热、战斗 Tick 内不得扩容，但池满、投影失败、事件 gap 或绑定失败只记录表现诊断并丢弃/重同步，不得反向改变战斗结果或 Encounter Phase。
