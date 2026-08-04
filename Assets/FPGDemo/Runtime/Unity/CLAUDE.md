# Runtime/Unity 指南

`FPG.Unity` 把正式领域合同接到 Unity 场景、Input System、物理和表现生命周期。

- 唯一 host 链是 `FpgEncounterHost -> FpgFormalEncounterHost -> FpgRoomEncounterDirector -> FpgEncounterSession`。
- `GameBootstrap`、catalog 和 Boot choices 负责角色/房间选择；FormalRoom 玩家由 `FpgFormalPlayerComposer` 在运行时组合。
- `FpgFormalPlayerTickDriver` 提交确定性输入；`FpgFormalCombatPortFactory` 创建查询、投射物和 hitbox 端口。
- `FpgShootingTuningSnapshot` 是正式配置的只读投影；`FpgFormalEncounterHost` 负责 live preview、需要重建的结构预览及失败回滚，`FpgShootingTuningRuntimeRegistry` 只暴露当前 host，不得保存第二份权威配置。`FpgShootingDevelopmentPanel` 只在 Editor/Development diagnostics 下启用。
- `FpgBattleTestBootstrap` 与 `FpgBattleGmRuntime` 只编入 Editor/Development：前者 additive 复用 FormalRoom/Art Scene 并启动 `BattleTestSandbox`，后者只操作该沙盒并在 Dispose 时恢复 GM 开关。它们不得成为 Release 入口或绕过正式 host/room/config 组合。
- `FpgPlayerSkillExecutionController` 在每次攻击开始时解析并冻结 attack-speed bonus 与 timing schedule；相同攻击只在 resolved ready tick 重启，不同攻击从 authored interrupt tick 起可 inclusive 打断，并先处理该 tick 已到期事件。低层 controller 接受 `0..32` tick buffer，正式 ThreeC 资产要求 `1..32`；最新显式意图替换旧意图，过期、弹药不足或 preflight 失败不得取消仍有效的 recovery/lock。
- `UnityBattleInputSource` 与 `ProjectWideBattleInputAdapter` 把横向移动折叠成一次性 cover edge；catch-up、房间交互和清理不能重放它。`FpgFormalPlayerTickDriver` 只让 `FpgCoverRuntime` 决定 traversal，Unity presenter 只镜像已提交状态。
- 掩体内开火必须先把当前 cover、攻击方向与冻结 AimPose 原子提交为 peek gate；`FpgPlayerBarrierPresentationController` 只把 `PeekRoot` 移到 `FpgRoomInstance` 解析的左右 authored 位置，并按 ThreeC 的 retract 时长收回。位置解析、朝向或 AimPose 冻结失败时拒绝起手，不回退到 prefab 固定偏移，也不移动 gameplay anchor。
- 空弹匣的 held Primary/Secondary 最多触发一次自动换弹并只排队一个攻击槽；Secondary release 必须取消对应排队与 peek。换弹完成后排队意图重新走完整 peek gate 和最终 availability 检查，最终拒绝不得消费弹药、ID、查询、命中或表现提交。
- `FpgPlayerSkillExecutionController` 按有效玩家配置的 `AllowWithdrawTick` 判断暴露，截止 tick 为 inclusive；缺失或早于末次攻击的值由 Config 校验拒绝，运行时不得按动画或表现时长改变其语义。
- `FpgFormalPlayerCameraFeedback` 是 Formal Camera 的唯一状态所有者：基础 `FpgResolvedCameraShot` 由玩家 Pose 与掩体 Profile 解析，掩体过渡与玩家视觉共用时长和曲线，后坐/震屏只作为增量叠加；取消或故障恢复已提交 Shot，其他组件不得直接覆盖 Rig/Camera。
- `FpgSkillPresentationRegistry/World` 把 compiled handle 解析到 wrapper、音频、轨迹和相机反馈；复用 FormalRoom 的共享 VFX root，不建立第二个 gameplay 状态源。`D0CombatVfxWorld` 池满时只回收最老的非 held 实例；held 实例必须显式释放且不可驱逐，全局 active cap 达限仍拒绝获取。
- Player/Combat presentation bridge 与 `FpgSkillImpactPresentationConsumer` 只消费已提交 timeline/impact 事件；commit cache 和 correlation 只用于去重与生命周期管理，不能决定命中或伤害。
- Presentation bridge、camera feedback、HUD 和 Entity view 只消费已提交快照/事件。
- `FpgPlayerFacingController` 是准星驱动的纯表现层：执行顺序固定为 reticle -> facing -> deterministic aim sampling；普通跨半屏按 ThreeC delay/duration 平滑翻转，攻击按下强制完成当前方向。它只能旋转 authored `FacingRoot`、刷新 Spine presentation sockets，并在停用/清理时恢复 authored 朝向，不能移动 gameplay anchor 或改变命中决策。
- Enemy hit-part follow settings 必须为空或与 hit parts 平行；Spine bone-follow 必须引用存在的 bone 并允许不可见时更新骨骼。`D0EnemyHitboxBoneFollowRuntime` 从 setup pose 计算 authored offset，并在解绑/Dispose 时恢复 authored Transform；Editor SceneView 预览不是运行时权威。
- `FpgEntitySkeletonRootMotionBridge` 只为 Behavior 明确启用的动画抽取 Spine root motion；正式 motion authority 按 60Hz tick 推进并在攻击查询前同步物理 Transform，渲染帧不得改变结果。
- 根运动开始、终止、取消与对象池重置必须恢复 VisualRoot/Spine authored 状态，同时保持 gameplay/projectile anchor 相对 Entity 的合同。
- 不得恢复 `BattleSessionHost`、`BattleSceneContext`、CombatLab 绑定或 `NewFPG.*` 依赖。
- pause/restart/disable/跨房时清理订阅、输入、session、presentation registry/pool/correlation 和 runtime bundle，失败则进入 fault。
- Cover 输入与 prefab 合同检查 `UnityBattleInputSourceTests.cs`、`ProjectWideBattleInputAssetTests.cs` 和 `FpgEntityPrefabContractTests.cs`。
- 验证以 Unity 编译/Console、`GameBootstrapConfigTests.cs`、`FormalFirstAuthoringContractTests.cs`、`FpgFormalCameraPoseUtilityTests.cs`、`FpgFormalPlayerCameraFeedbackTests.cs`、`FpgAttackTimingTests.cs`、`FpgFeiAttackSpeedIntegrationTests.cs`、`FpgPlayerSkillExecutionControllerTests.cs`、`FpgPlayerBarrierPresentationControllerTests.cs`、`FpgEntityPrefabContractTests.cs`、`FpgShootingTuningSnapshotTests.cs`、`FpgSkillPresentationRuntimeTests.cs`、`D0CombatVfxWorldTests.cs`、`FpgShootingContractsTests.cs`、`FpgBattleTestPlayModeTests.cs`、`FpgFormalEnemyRootMotionAssetTests.cs` 与对应 Formal EditMode 合同为准。
