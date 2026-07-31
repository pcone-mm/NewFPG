# FPGDemo Editor 指南

本目录保存正式 FPG 的 Editor-only 入口；`LevelAuthoring/` 与 `SkillAuthoring/` 有各自 asmdef 和局部指南，进入子目录后以其规则为准。

- 顶层 Inspector 必须复用 `SerializedObject`、Undo 和 runtime `TryValidate`，不复制校验或建立第二套 authoring 模型。
- `FpgCoverCameraProfileInspector` 只在通用策划 Inspector 上增加全量 copy/paste，并统一复用 `LevelAuthoring/FpgCoverCameraProfileAuthoring`；引用计数、独立化和孤立项检查留在 Room Editor，任何入口都不得自动删除孤立 Profile。
- `D0EnemyEntityViewEditor` 与 `FpgEnemyEntityViewEditor` 共用 hit-part follow 字段和 `FpgEnemyHitboxFollowEditorPreview`；SceneView 中的 Spine bone-follow 矩阵只用于临时 gizmo/handle 预览，不能把动画姿态写回 prefab/scene。
- `FpgWindowsReleaseBuild` 的菜单和 batch 入口必须复用 `FpgProductionSceneList`，不得另写 Boot/FormalRoom/Art Scene 清单；输出留在 `Builds/`，不写入 Assets。
- 迁移/修复工具必须显式人工触发，不得自动运行、批量手改 YAML 或把第三方插件 API 扩散到领域/运行时 asmdef；迁移落地后删除过期入口，而不是把它变成长期流程。
- 所有 Scene、Prefab、配置与 Build Settings 写入使用 Unity Editor API，保留 GUID/.meta，并在失败时停止而不是留下半更新状态。
- 验证以 Unity 编译/Console、`AssemblyBoundaryTests.cs`、`BuildSettingsTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgCoverCameraAuthoringTests.cs` 和对应子目录指南为准。
