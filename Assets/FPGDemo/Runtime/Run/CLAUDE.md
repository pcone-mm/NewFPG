# Runtime/Run 指南

`FPG.Run` 是无 UnityEngine 依赖的纯领域编排层。

- `FpgEncounterSession`、`FpgEnemyRoster`、`FpgMultiEnemyCombatPort` 和召唤合同组成正式主线。
- `BattleSession` 若保留，仅用于纯领域兼容和历史测试，不得再拥有 Unity host、场景入口或正式验收地位。
- tick、stable ID、sequence、digest 和固定容量必须可重放；溢出、重复与非法顺序均 fail-closed。
- 表现 feed 只发布已提交事实；`FixedFpgSkillImpactPresentationStream` 按 source/execution/gameplay-event correlation 发布精确 contact 与一次 group completion，不根据表现播放结果反写战斗状态。
- stream 使用固定容量和单调 sequence；消费者发现 cursor gap 时必须清理陈旧 correlation 绑定并从保留窗口继续，不能猜测丢失事件。
- 跨房只携带已校验的 `FpgPlayerRunResourceState`，不携带敌人、投射物或瞬态武器状态。
- 正式事务修改检查 `FpgMultiEnemyCombatTransactionTests.cs`；跨房资源检查 `FpgPlayerRunResourceStateTests.cs`。
- impact contact、group completion、终止与 gap 合同检查 `FpgSkillImpactPresentationStreamTests.cs`。
