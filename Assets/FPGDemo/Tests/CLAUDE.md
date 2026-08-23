# FPGDemo Tests 指南

本目录保存正式 FPG 的合同测试，不保存运行时代码、手工试玩记录或 Test Runner 输出。

## 边界

- `EditMode/` 属于 `FPG.EditMode.Tests` / `FPG.Demo.Tests.EditMode`，覆盖纯领域、配置、AssetDatabase 与 Editor authoring 合同；需要 Unity 帧循环、物理或 additive scene 生命周期的检查才放入 `PlayMode/`。
- 测试优先复用上级与模块局部指南列出的精确 fixture；默认不把窄改动扩成全量 EditMode/PlayMode 运行。
- 新依赖必须加入对应 test asmdef；不得为了测试让 runtime asmdef 反向引用 Editor 或 test assembly。

## 隔离与恢复

- 测试创建的资产、场景和文件夹使用唯一临时路径，并在 `finally` / `TearDown` 中删除；修改 catalog、`EditorBuildSettings`、active/loaded scene 或静态 override 时，即使断言失败也要恢复原状态。
- 不修改正式 authored 资产来布置 fixture；需要真实序列化/GUID 行为时，通过 Unity Editor API 创建临时资产并保留 `.meta` 语义。
- 只有当前 Test Runner 结果或持久化 XML 能证明测试通过；缺少结果时只报告“未运行”，不从编译成功推断测试成功。

## 验证路由

- 房间复制、Art Scene 与构建列表：`FpgRoomDuplicationContractTests.cs`、`FpgRoomAuthoringSafetyTests.cs`、`FpgRoomArtSceneContractTests.cs`、`BuildSettingsTests.cs`。
- 独立掩体的领域、房间、prefab、探身表现与输入边界：`FpgMultiEnemyCombatTransactionTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgEntityPrefabContractTests.cs`、`FpgPlayerBarrierPresentationControllerTests.cs`、`FpgPlayerSkillExecutionControllerTests.cs`、`UnityBattleInputSourceTests.cs`、`ProjectWideBattleInputAssetTests.cs`。
- 掩体镜头配置、authoring、Shot 解析与过渡状态：`FpgCoverCameraProfileTests.cs`、`FpgCoverCameraAuthoringTests.cs`、`FpgFormalCameraPoseUtilityTests.cs`、`FpgFormalPlayerCameraFeedbackTests.cs`。
- 技能实体绑定、编辑、正式玩家资产与 inclusive 暴露边界：`FpgSkillAuthoringEditorTests.cs`、`FpgSkillDefinitionTests.cs`、`FpgPlayerSkillAssetContractTests.cs`、`FpgPlayerSkillExecutionControllerTests.cs`。
- 攻速解析、resolved schedule/hash、武器快照与 Fei 连射/打断集成：`FpgAttackTimingTests.cs`、`FpgAttackTimingHashAndWeaponSnapshotTests.cs`、`FpgFeiAttackSpeedIntegrationTests.cs`。
- 技能表现 registry、VFX 池与清理生命周期：`FpgSkillPresentationRuntimeTests.cs`、`D0CombatVfxWorldTests.cs`。
- 音频 cue 路由、Bank 的 bus/空间/variation 校验、固定 voice pool 与技能音频变体：`CombatAudioCueRoutingTests.cs`、`CombatAudioBankTests.cs`、`CombatAudioPresenterTests.cs`、`FpgFormalSkillPresentationV3AssetTests.cs`、`FpgSkillPresentationRuntimeTests.cs`。
- supplemental feedback 与 More Mountains Feel 的事件、聚合、prefab、场景路由和清理：`FpgSupplementalFeedbackTests.cs`、`FpgFeelEnemyHitAssetTests.cs`。
- Sprite Shadow Caster 的 sprite mesh/material 生成、复用与 importer 刷新：`FpgSpriteShadowCasterAuthoringTests.cs`。
- 射击配置投影、玩家朝向、瞄准/掩体几何和分层准星：`FpgShootingTuningSnapshotTests.cs`、`FpgEntityPrefabContractTests.cs`、`FpgShootingContractsTests.cs`、`FpgLayeredAimIndicatorTests.cs`、`CombatAimViewportMathTests.cs`。
- BattleTest 的纯 GM 语法、沙盒 external spawn、Editor 窗口与场景生命周期：`FpgBattleGmCommandParserTests.cs`、`FpgBattleTestSandboxRuntimeTests.cs`、`FpgBattleGmEditorWindowTests.cs`、`FpgBattleTestPlayModeTests.cs`。PlayMode fixture 必须卸载 BattleTest/FormalRoom/Art Scene 并恢复原 active scene。
- `BuildSettingsTests.cs` 同时保证生产清单排除 BattleTest；Development 构建临时改写 global/active scene list 的测试或工具必须在 `finally` 恢复。
- 场景加载/回滚仅在需要帧生命周期时使用 `FpgRoomArtSceneLoaderPlayModeTests.cs`；其余合同优先留在 EditMode。
