# FPG Formal Encounter 运行合同

正式运行链固定为：

`Boot -> FormalRoom -> room-forest -> FpgEncounterHost/FpgFormalEncounterHost -> FpgRoomEncounterDirector -> FpgEncounterSession`

旧 CombatLab、BattleSessionHost、BattleSceneContext、D0 Stage 和替换 Scenario 不得作为回退路径。

## 技能时钟与执行

- `FPG.Skills` 的 60Hz 整数 Tick 时间轴是角色与怪物技能的唯一时序权威，项目 Fixed Timestep 固定为 `1/60`。
- Tick 0 在技能启动 Tick 执行；位于序列终点的事件先提交，再结束序列。
- 一个单位同时只有一个主动技能序列；多段动作由玩家技能控制器显式转换，编译器不自动拼接序列。
- 每个序列实例分配 Execution ID，每个成功攻击事件再分配独立 Attack/Shot ID；取消或失败事件不消耗攻击 ID。
- Spine 主动画按执行 Tick 绝对求值并做渲染插值。动画事件、累计 `deltaTime` 和编辑器预览不得形成第二套攻击时机。

玩家攻击启动前汇总整段弹药成本，容量不足则不启动动画。每个攻击 Tick 重新采样瞄准，成功提交时才逐发扣弹；硬打断取消未触发事件。再施放冷却从成功攻击提交 Tick 开始，技能后摇和再施放冷却是两套独立约束。换弹提交允许位于序列终点。

怪物技能启动前以事务方式预留整段投射物、定时打击与召唤容量；任何容量不足都不启动动画或预警。硬打断只释放未触发预留，已生成或已排队载荷继续生效；再施放冷却从原计划序列结束开始，取消不会退回冷却。

## 生命周期

1. Boot 解析角色与房间选择。
2. FormalRoom 在 inactive staging root 组合玩家、端口、Director、Session 和表现桥。
3. 所有引用、容量、ID、动画与房间 marker 校验通过后才激活实体。
4. 清场后 Director 生成出口 offer；确认选择后先捕获玩家 run resources，再释放当前 session 与房间实例。
5. 目标房间或资源恢复失败时进入 fault，不保留半初始化对象，也不回退旧链。

## Fei 副射唯一规范

本节是 Fei 副射的唯一设计与运行合同。旧 D0 副射文档、旧即时模式说明和旧表现字段表均已退役，不得作为实现或验收依据。

两种模式由两个独立权威技能资产承载：`FPG_Fei_Secondary_Immediate.asset` 只拥有 `Execute`，`FPG_Fei_Secondary_Charge.asset` 只拥有 `ChargeEnter`、`ChargeLoop`、`Release`、`Cancel`。Weapon 依赖链必须同时稳定引用两者，并按已选择模式解析对应资产；不得把五个序列重新合并到单一技能资产。

角色选择阶段必须展示 Fei 的两种副射模式，并把选择写入 `FpgPlayableCharacterSelection` 和跨场景选择快照；选择只在当前运行中生效，不写入持久化设置。只有一个可用模式的角色可自动选择，Fei 默认模式为瞬发：

- **瞬发模式**：点按执行一次，长按按弹药与副射冷却重复执行；每次使用 `Execute`，动画为 `defense_play`，初始时长 60 Tick。
- **蓄力模式**：按下后依次进入 `ChargeEnter` 与 `ChargeLoop`，满蓄后松开进入 `Release`，成功发射完成后进入 `Cancel` 作为后摇；未满松开直接进入 `Cancel`，不提交攻击也不扣弹。

两个资产内各序列的固定职责如下：

| Asset | Sequence | 动画 | 初始时长 | 职责 |
| --- | --- | --- | ---: | --- |
| `FPG_Fei_Secondary_Immediate.asset` | `Execute` | `defense_play` | 60 Tick | 瞬发模式单次射击；包含与 `Release` 相同的攻击 payload 和发射表现 |
| `FPG_Fei_Secondary_Charge.asset` | `ChargeEnter` | `u4_attack_ready` | 28 Tick | 蓄力起始，不提交攻击 |
| `FPG_Fei_Secondary_Charge.asset` | `ChargeLoop` | `u4_attack_ready` | 持续 | 动画 `loop=true`，运行时 `holdUntilCanceled=true`，持续到松开或硬中断 |
| `FPG_Fei_Secondary_Charge.asset` | `Release` | `u4_attack_play` | 52 Tick | 满蓄松开的发射段；Tick 0 提交攻击 |
| `FPG_Fei_Secondary_Charge.asset` | `Cancel` | `u4_attack_end` | 28 Tick | 未满取消或成功发射后的后摇 |

`loop` 只控制 Spine 动画取样；`holdUntilCanceled` 独立控制序列运行时保持。各阶段拥有独立 Execution ID，但由控制器维护为一个逻辑动作链。拆分不改变 gameplay 行为：`Execute` 与 `Release` 必须使用相同的固定攻击 payload，弹药、伤害、削韧、射程、弹丸、范围和命中上限均不随蓄力进度变化。

蓄力进度以技能启动 Tick 为基准，`chargeProgressTicks = minimumChargeTicks = 30`。进度为 `clamp((currentTick - chargeStartTick) / 30, 0, 1)`；满蓄前松开取消，满蓄后继续按住时保持 1。仿真快照发布 `IsSecondaryCharging`、`SecondaryChargeProgress` 和蓄力起始 Tick，表现层不得用 `deltaTime` 重建第二套进度。

蓄力期间必须同时提供两类反馈：`PF_FPG_Fei_Secondary_Charge` 以持有型实例跟随副射枪口，并由归一化进度驱动尺寸和粒子发射强度；准星显示径向进度环。释放、取消、死亡、换场或硬中断时，两者必须同 Tick 停止并清零。发射链固定使用 `PF_FPG_Fei_Secondary_Muzzle`、`PF_FPG_Fei_Secondary_Projectile` 与 `PF_FPG_Fei_Secondary_Hit`；已成功提交的弹丸及其飞行、命中特效不因后摇被抢占而回收。

`Release` 播放期间普通输入不可抢占。进入 `Cancel` 后摇后，主射和副射都可立即请求抢占，但只有对应弹药、独立再施放冷却和非装填状态检查通过时才取消后摇并开始新动作；失败请求不得消耗弹药、清除后摇或生成攻击 ID。死亡、房间切换及系统故障继续使用硬中断规则。

## 多敌人与召唤事务

- 所有攻击、伤害、死亡、召唤与 roster 变化通过正式事务提交。
- 不允许按 Luan、Hudie 或其他敌人 ID 编写特殊运行分支。
- Luan 召唤的 delay 阶段只发布开始/蓄力事实，不提前调用 summon sink，也不杀死 owner。
- Luan 使用通用 `ReplaceOwner + OwnerPosition` 策略：释放时跳过房间玩法配额和普通出生点，实体端在 owner 存活期间快照其根节点世界 Pose 并 Prepare Hudie。
- Luan 的 Summon 与 `SelfDestructOwner` 是两个独立玩法节点；当前配置在 Tick 44 召唤，并在 Tick 71 以空绑定自毁，不由召唤载荷或 Occupancy Mode 隐式携带死亡结果。
- 空绑定自毁按自身 Tick 无条件执行；若策划把自毁绑定到同 Tick 更早的 Summon，则只有 Hudie Prepare 并进入统一 Spawn Queue、sink 返回 `Queued` 后才允许 Luan 死亡。
- Retry、拒绝或静态上限只会门控实际配置了依赖的自毁节点；空绑定节点不等待召唤结果。ReplaceOwner 的固定容量或状态拒绝仍按既有契约 fail-closed，不得通过敌人 ID 特判改变配置语义。

## 清场、出口与跨房

- 清场只由正式 roster/session 的已提交状态决定。
- 出口候选来自已校验的 room catalog 与 refresh rule，顺序由稳定 ID 和 run context 决定。
- 跨房只携带角色/武器 ID、已选择的副射模式、生命、护盾、弹药和护盾锁定等已校验资源。
- runtime ID、敌人、投射物、攻击队列和瞬态表现不得跨房复用。

## 验证

默认执行 Unity 编译/Console、正式场景依赖闭包、技能资产预检和 EditMode 静态合同检查。玩家技能重点检查双模式 payload 一致、`holdUntilCanceled`、30 Tick 进度、取消不扣弹、后摇抢占和表现清理；事务修改重点检查 `FpgMultiEnemyCombatTransactionTests.cs`、`FpgFormalEnemySkillSchedulerTests.cs` 与 `FpgPlayerSkillExecutionControllerTests.cs`，跨房检查选择模式与玩家资源均被正确恢复。

PlayMode 的动作观感、命中反馈和完整试玩由人工验收；自动化只验证动画开始、攻击提交、VFX/SFX 与 Combat Trace 使用同一 Execution/Event ID 和 Tick 的可观测合同。


## 人工 PlayMode 验收

1. Fei 主射启动 Tick 立即提交攻击，连续施放按 12 Tick 冷却，`attack_play1/attack_play2` 变体稳定可复现。
2. Fei 瞬发副射点按一次、长按按冷却重复播放 `defense_play`；攻击 payload 与蓄力释放一致。
3. Fei 蓄力副射按 `ChargeEnter → ChargeLoop → Release → Cancel` 转换，准星和枪口特效在 30 Tick 同步达到满蓄；未满松开不扣弹。
4. `Release` Tick 0 提交攻击，`Cancel` 后摇可被满足弹药和独立冷却条件的主射或副射抢占；已提交弹丸和命中特效继续完成。
5. Fei 换弹只在第 84 Tick 补满弹匣，终点提交后动作结束。
6. Burstbug、Hudie、Luan 的预警、动作与逻辑事件分别在迁移后的精确 Tick 触发；Luan 保持 ReplaceOwner、OwnerPosition，并由 Tick 71 的空绑定 `SelfDestructOwner` 节点按配置结束 owner。
7. 每次成功提交的动画、VFX/SFX、Combat Trace 使用相同 Execution ID、Event ID 与 Tick；被取消或失败的事件不产生 Attack/Shot ID。
8. 观察 60 次 FixedUpdate 约等于一秒，并确认硬打断只取消尚未触发的事件。
