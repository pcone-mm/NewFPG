# FPG Formal Encounter 运行合同

正式运行链固定为：

`Boot -> FormalRoom -> room-forest -> FpgEncounterHost/FpgFormalEncounterHost -> FpgRoomEncounterDirector -> FpgEncounterSession`

旧 CombatLab、BattleSessionHost、BattleSceneContext、D0 Stage 和替换 Scenario 不得作为回退路径。

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
- 只有 sink 返回 `Queued` 后，才提交召唤并允许 Luan 死亡。
- Retry 或 Rejected 必须保留 Luan 与 pending 命令，不能发布伪造的死亡或召唤成功事件。

## 清场、出口与跨房

- 清场只由正式 roster/session 的已提交状态决定。
- 出口候选来自已校验的 room catalog 与 refresh rule，顺序由稳定 ID 和 run context 决定。
- 跨房只携带角色/武器 ID、生命、护盾、弹药和护盾锁定等已校验资源。
- runtime ID、敌人、投射物、攻击队列和瞬态表现不得跨房复用。

## 验证

默认执行 Unity 编译/Console、正式场景依赖闭包和静态合同检查。事务修改重点检查 `FpgMultiEnemyCombatTransactionTests.cs`，跨房检查 `FpgPlayerRunResourceStateTests.cs`、`FpgExitRoomRefreshRuleTests.cs` 与 `FpgRoomExitRuntimeTests.cs`。
