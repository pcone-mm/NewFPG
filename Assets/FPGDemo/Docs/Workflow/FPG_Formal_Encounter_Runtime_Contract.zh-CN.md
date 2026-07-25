# FPG Formal Encounter 运行合同

正式运行链固定为：

`Boot -> FormalRoom -> room-forest -> FpgEncounterHost/FpgFormalEncounterHost -> FpgRoomEncounterDirector -> FpgEncounterSession`

旧 CombatLab、BattleSessionHost、BattleSceneContext、D0 Stage 和替换 Scenario 不得作为回退路径。

## 技能时钟与执行

- `FPG.Skills` 的 60Hz 整数 Tick 时间轴是角色与怪物技能的唯一时序权威，项目 Fixed Timestep 固定为 `1/60`。
- Tick 0 在技能启动 Tick 执行；位于序列终点的事件先提交，再结束序列。
- 一个单位同时只有一个主动技能序列；ChargeEnter、ChargeLoop、Release、Cancel 的转换仍属于同一主动动作。
- 每个序列实例分配 Execution ID，每个成功攻击事件再分配独立 Attack/Shot ID；取消或失败事件不消耗攻击 ID。
- Spine 主动画按执行 Tick 绝对求值并做渲染插值。动画事件、累计 `deltaTime` 和编辑器预览不得形成第二套攻击时机。

玩家攻击启动前汇总整段弹药成本，容量不足则不启动动画。每个攻击 Tick 重新采样瞄准，成功提交时才逐发扣弹；硬打断取消未触发事件。再施放冷却从计划中的最后攻击 Tick 开始，但序列结束前仍受动作锁约束。换弹提交允许位于序列终点。

怪物技能启动前以事务方式预留整段投射物、定时打击与召唤容量；任何容量不足都不启动动画或预警。硬打断只释放未触发预留，已生成或已排队载荷继续生效；再施放冷却从原计划序列结束开始，取消不会退回冷却。

## 生命周期

1. Boot 解析角色与房间选择。
2. FormalRoom 在 inactive staging root 组合玩家、端口、Director、Session 和表现桥。
3. 所有引用、容量、ID、动画与房间 marker 校验通过后才激活实体。
4. 清场后 Director 生成出口 offer；确认选择后先捕获玩家 run resources，再释放当前 session 与房间实例。
5. 目标房间或资源恢复失败时进入 fault，不保留半初始化对象，也不回退旧链。

## 多敌人与召唤事务

- 所有攻击、伤害、死亡、召唤与 roster 变化通过正式事务提交。
- 不允许按 Luan、Hudie 或其他敌人 ID 编写特殊运行分支。
- Luan 召唤的 delay 阶段只发布开始/蓄力事实，不提前调用 summon sink，也不杀死 owner。
- Luan 使用通用 `ReplaceOwner + OwnerPosition` 策略：释放时跳过房间玩法配额和普通出生点，实体端在 owner 存活期间快照其根节点世界 Pose 并 Prepare Hudie。
- 只有 Hudie Prepare 并进入统一 Spawn Queue、sink 返回 `Queued` 后，才提交召唤并允许 Luan 死亡。
- Retry 保留 owner 与 pending 命令；静态玩法配额结束只消费普通召唤命令，不提交 owner 结果。ReplaceOwner 的固定容量或状态拒绝属于预检未覆盖的系统故障：不会提交“召唤成功后死亡”，而是让战斗按既有契约 fail-closed。

## 清场、出口与跨房

- 清场只由正式 roster/session 的已提交状态决定。
- 出口候选来自已校验的 room catalog 与 refresh rule，顺序由稳定 ID 和 run context 决定。
- 跨房只携带角色/武器 ID、生命、护盾、弹药和护盾锁定等已校验资源。
- runtime ID、敌人、投射物、攻击队列和瞬态表现不得跨房复用。

## 验证

默认执行 Unity 编译/Console、正式场景依赖闭包、技能资产预检和 EditMode 静态合同检查。事务修改重点检查 `FpgMultiEnemyCombatTransactionTests.cs`、`FpgFormalEnemySkillSchedulerTests.cs` 与 `FpgPlayerSkillExecutionControllerTests.cs`，跨房检查 `FpgPlayerRunResourceStateTests.cs`、`FpgExitRoomRefreshRuleTests.cs` 与 `FpgRoomExitRuntimeTests.cs`。

PlayMode 的动作观感、命中反馈和完整试玩由人工验收；自动化只验证动画开始、攻击提交、VFX/SFX 与 Combat Trace 使用同一 Execution/Event ID 和 Tick 的可观测合同。


## 人工 PlayMode 验收

1. Fei 主射启动 Tick 立即提交攻击，连续施放按 12 Tick 冷却，`attack_play1/attack_play2` 变体稳定可复现。
2. Fei 副射进入蓄力动作，Release Tick 立即提交攻击，后续施放按 30 Tick 冷却；蓄力取消不扣弹。
3. Fei 换弹只在第 84 Tick 补满弹匣，终点提交后动作结束。
4. Burstbug、Hudie、Luan 的预警、动作与逻辑事件分别在迁移后的精确 Tick 触发；Luan 保持 ReplaceOwner、OwnerPosition 与成功后 owner outcome。
5. 每次成功提交的动画、VFX/SFX、Combat Trace 使用相同 Execution ID、Event ID 与 Tick；被取消或失败的事件不产生 Attack/Shot ID。
6. 观察 60 次 FixedUpdate 约等于一秒，并确认硬打断只取消尚未触发的事件。
