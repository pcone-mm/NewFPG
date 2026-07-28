# FPGDemo Editor 指南

本目录保存正式 FPG 的 Editor-only 入口；`LevelAuthoring/` 与 `SkillAuthoring/` 有各自 asmdef 和局部指南，进入子目录后以其规则为准。

- 顶层薄 Inspector 只暴露正式配置/技能入口，不复制 runtime 校验或建立第二套 authoring 模型。
- `FpgWindowsReleaseBuild` 的菜单和 batch 入口必须复用 `FpgProductionSceneList`，不得另写 Boot/FormalRoom/Art Scene 清单；输出留在 `Builds/`，不写入 Assets。
- 迁移/修复工具必须显式人工触发，不得自动运行、批量手改 YAML 或把第三方插件 API 扩散到领域/运行时 asmdef；迁移落地后删除过期入口，而不是把它变成长期流程。
- 所有 Scene、Prefab、配置与 Build Settings 写入使用 Unity Editor API，保留 GUID/.meta，并在失败时停止而不是留下半更新状态。
- 验证以 Unity 编译/Console、`AssemblyBoundaryTests.cs`、`BuildSettingsTests.cs`、`FpgRoomArtSceneContractTests.cs` 和对应子目录指南为准。
