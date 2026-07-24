# LevelAuthoring Editor 指南

`FPG.LevelAuthoring.Editor` 只负责正式房间编辑、Scene View marker、正式 Encounter 预览和 FormalRoom 安装。

- `FpgRoomEditorWindow`、UXML/USS 与 `FpgRoomSceneTool` 维护房间资产和 marker。
- `FpgEncounterPreviewWindow/Utility` 只生成内存中的正式预览，不写回 Profile、Override、Room 或 Scene。
- `FpgFormalRoomLoopInstaller` 维护 Boot、FormalRoom、玩家组合、HUD、出口与 Build Settings。
- 工具通过 `SerializedObject`、`AssetDatabase`、`Undo` 和 `EditorSceneManager` 写入；不批量手改 YAML。
- 所有入口必须可重复执行且 fail-closed，不得重建 CombatLab、旧 Stage、旧 Host 或隐式迁移源。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs` 和 `BuildSettingsTests.cs` 为准。
