# InitTestScene 指南

本目录只保存开发与自动化测试入口，不保存正式游戏场景。当前唯一场景 `BattleTest.unity` 仅在 `UNITY_EDITOR` 或 `DEVELOPMENT_BUILD` 下有效。

- `BattleTest.unity` 只拥有 fallback Camera/Light 与一个 `FpgBattleTestBootstrap`；Bootstrap additive 加载正式 `FormalRoom`、组合默认玩家、加载 catalog Art Scene，并以空 encounter plan 启动 `BattleTestSandbox`。不要在此复制 Host、房间配置、玩家 prefab 或战斗状态机。
- 开发场景清单由 `FpgBattleTestDevelopmentSceneList` 定义为 `BattleTest + FpgProductionSceneList`；生产清单与 Release 构建必须排除本场景。
- `FpgWindowsDevelopmentBuild` 可为构建临时替换 global/active Build Settings，但必须在 `finally` 恢复；不要手工把开发清单保存成项目的生产 Build Settings。
- GM 控制只通过 ready 的 `FpgBattleTestBootstrap.GmRuntime` 进入；场景本身不保存无敌、AI 开关、动态敌人或射击调参快照，退出时必须释放并恢复默认状态。
- Scene 与引用只通过 Unity Editor、Unity MCP 或现有工具修改，保留 `BattleTest.unity.meta`；不要手改 YAML 或把测试框架的 bootstrap scene 当作正式内容。
- 验证检查 `Assets/FPGDemo/Tests/EditMode/BuildSettingsTests.cs`、`FpgBattleTestSandboxRuntimeTests.cs`、`FpgBattleGmEditorWindowTests.cs` 与 `Assets/FPGDemo/Tests/PlayMode/FpgBattleTestPlayModeTests.cs`；只有实际 Test Runner/XML 结果能证明通过。
