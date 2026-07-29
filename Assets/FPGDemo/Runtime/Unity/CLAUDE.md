# Runtime/Unity 指南

`FPG.Unity` 把正式领域合同接到 Unity 场景、Input System、物理和表现生命周期。

- 唯一 host 链是 `FpgEncounterHost -> FpgFormalEncounterHost -> FpgRoomEncounterDirector -> FpgEncounterSession`。
- `GameBootstrap`、catalog 和 Boot choices 负责角色/房间选择；FormalRoom 玩家由 `FpgFormalPlayerComposer` 在运行时组合。
- `FpgFormalPlayerTickDriver` 提交确定性输入；`FpgFormalCombatPortFactory` 创建查询、投射物和 hitbox 端口。
- `FpgSkillPresentationRegistry/World` 把 compiled handle 解析到 wrapper、音频、轨迹和相机反馈；复用 FormalRoom 的共享 VFX root，不建立第二个 gameplay 状态源。
- Player/Combat presentation bridge 与 `FpgSkillImpactPresentationConsumer` 只消费已提交 timeline/impact 事件；commit cache 和 correlation 只用于去重与生命周期管理，不能决定命中或伤害。
- Presentation bridge、camera feedback、HUD 和 Entity view 只消费已提交快照/事件。
- Enemy hit-part follow settings 必须为空或与 hit parts 平行；Spine bone-follow 必须引用存在的 bone 并允许不可见时更新骨骼。`D0EnemyHitboxBoneFollowRuntime` 从 setup pose 计算 authored offset，并在解绑/Dispose 时恢复 authored Transform；Editor SceneView 预览不是运行时权威。
- `FpgEntitySkeletonRootMotionBridge` 只为 Behavior 明确启用的动画抽取 Spine root motion；正式 motion authority 按 60Hz tick 推进并在攻击查询前同步物理 Transform，渲染帧不得改变结果。
- 根运动开始、终止、取消与对象池重置必须恢复 VisualRoot/Spine authored 状态，同时保持 gameplay/projectile anchor 相对 Entity 的合同。
- 不得恢复 `BattleSessionHost`、`BattleSceneContext`、CombatLab 绑定或 `NewFPG.*` 依赖。
- pause/restart/disable/跨房时清理订阅、输入、session、presentation registry/pool/correlation 和 runtime bundle，失败则进入 fault。
- 验证以 Unity 编译/Console、`GameBootstrapConfigTests.cs`、`FormalFirstAuthoringContractTests.cs`、`FpgSkillPresentationRuntimeTests.cs`、`FpgFormalEnemyRootMotionAssetTests.cs` 与对应 Formal EditMode 合同为准。
