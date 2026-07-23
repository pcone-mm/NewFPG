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

## 清场与出口刷新合同

正式清场判定继续由 `FpgEncounterRuntime` 独占：所有波次已发放、生成队列为空且存活 Roster 为零后，房间才进入 `Cleared`。出口不得参与清场条件，也不得提前出现在准备或战斗阶段。

`GameBootstrapConfig` 显式引用一个 `FpgExitRoomRefreshRule`，规则再显式引用 `FpgRoomCatalog`。目录只包含 Player Build 中允许进入的完整房间，并校验空引用、无效 Room、重复 `RoomId`、缺玩家入口或缺出口。目的地所有权不得移入 `FpgRoomDefinition`、`FpgRoomExitSlot`、Encounter Profile 或旧 D0 Scenario。

清场事件到达后，规则按 `RunSeed + RoomVisitOrdinal + SourceRoomId` 为全部出口一次性生成 `FpgExitOffer`：

- 当前房间与其他目录候选等概率；同一上下文和稳定 ID 输入得到相同 offer。
- 多出口先无重复遍历候选池，候选耗尽后才开始下一轮。
- 决定层只保存源房 ID、出口 ID、目标房 ID 和访问序号；Unity 层再解析目标 Room 资产与显示名。
- offer 绑定发生在出口从 `Hidden` 进入 `Available` 时，标签显示 `前往：{Room.DisplayName}`；命中时不得重新抽取。

`FpgRoomExitRuntime` 的唯一合法访问生命周期为 `Hidden -> Available -> Consumed`。第一次出口选择后必须同步消费全部出口，并清空出口注册表，使多弹丸、同帧多命中、连续输入和重复通知都不能产生第二次选择。

## 正式攻击出口合同

出口是正式武器查询中的交互对象，不是 Combatant 或伤害目标：

- 出口 Collider 在 `Available` 期间以 `EnvironmentBlocker` 注册到 `HitboxRegistry`，使用保留 Geometry ID 区间 `95000-95999`。
- `FpgExitAttackRegistry` 独立维护 Geometry ID 到 `FpgExitOffer` 的映射；不得扩展正式伤害目标枚举，也不得接入旧 `IDamageable`。
- 主攻击和副攻击均须经过正常 `PrepareFrame -> Query -> CommitPreparedRelease`。只有正式提交后的查询命中才能选择出口；出口无生命值且不累计伤害。
- 命中出口的攻击正常消耗弹药，跨房资源在提交之后捕获，因此下一房看到的是已经扣除出口攻击消耗的弹药。
- 清场后使用独立 `ProcessRoomInteractionTick`，只推进玩家武器、装填、姿态、护盾恢复、攻击查询和表现；不得继续推进敌人、波次、Threat、敌方攻击或 Projectile World。
- 出口揭示时清除旧副攻击输入边沿；主攻击必须先观察到松开后才能重新武装。清场时持续按住主攻击不得自动进入下一房，空弹匣仍可在等待阶段装填后再攻击。

正式选择通知使用携带 Geometry ID、Tick 和完整 offer 的 `FpgExitSelectionEvent`。旧字符串 `ExitSelected` 只保留为兼容通知，新的跨房编排不得依赖它解析目标房间。

## 跨房资源与重建合同

`FpgPlayerRunResourceState` 是房间边界上的可移植资源快照，只保存：角色稳定 ID、武器稳定 ID、生命、护盾、弹药、护盾恢复剩余 Tick 和恢复比例。重新组合的角色与武器必须兼容这些稳定 ID 和容量。

武器冷却、装填、蓄力、已准备释放、输入序号、RuntimeId 和 Exposure 都是房内瞬态，不跨房继承。新房创建全新玩家 Runtime，并在 `Session.Start` 前通过 `FpgEncounterStartRequest` 导入资源；武器与 Exposure 从 `Ready/Exposed` 开始。房内 Restart 仍以该房入口时已经导入的状态为基线，不回到整局最初资源。

`FpgRunFlowController` 的状态为 `Running / AwaitingExit / Transitioning / Faulted`，由 Bootstrap 持有并只订阅正式 `RoomCleared` 与 `ExitOfferSelected`。选中出口后的顺序固定为：

1. 关闭全部出口，捕获已提交攻击后的玩家资源和当前角色选择。
2. `StopAndClear` 旧房，完整清除环境、玩家、敌人、出口注册、Hitbox 和订阅。
3. 等待一次 `WaitForEndOfFrame`，再在同一个已加载的通用 `FormalRoom` 中设置目标 Room 并重新组合同一角色。
4. 使用递增后的上下文和资源快照准备、启动并激活表现；`Depth` 与 `RoomVisitOrdinal` 各加一，RunSeed、Region、难度倍率、Encounter Profile 和 Override 保持不变。
5. 全部成功后才更新 `SelectedRoom` 与活动 Host，并重新绑定下一房事件。

任何 offer、资源、组合、容量或启动错误都必须 Fail-Closed：再次清理保留场景，记录 `LastError`，进入 `Faulted` 并恢复 Boot 房间选择。不得重新开放旧出口，不得回退旧 CombatLab，也不得保留半初始化环境、攻击碰撞或事件订阅。

## 验收边界

D0 回归只验证冻结合同未退化；正式 Encounter 验收必须在正式 Request/Plan/Director/Session 路径完成。不得以 CombatLab 单敌可运行、Luan→Hudie 旧替换可运行或旧 `BattleSession` 测试通过，替代多实例、波次、召唤、容量、清场出口和跨房重建验收。

跨房验收至少覆盖：清场前出口完全隐藏；标签与实际目标一致；持续主攻击不会越过清场边界误触发；主/副攻击和装填后攻击均可选择；同帧重复命中只切一次；资源与恢复剩余时间继承而动作瞬态重置；自循环或连续切换 5-10 次后只存在当前房对象和注册；下一房启动失败时回到 Boot 且不存在旧出口或残留 Hitbox。
