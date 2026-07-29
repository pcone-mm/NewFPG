# LevelAuthoring Editor 指南

`FPG.LevelAuthoring.Editor` 只负责正式房间编辑、Scene View marker、Art Scene 合同、正式 Encounter 预览和验证。

- `FpgRoomEditorWindow`、UXML/USS 与 `FpgRoomSceneTool` 维护房间资产和 marker。
- `FpgRoomAuthoringOperations` 只执行用户显式触发的整房复制、Art Scene root 修复和生产注册；源 RoomDefinition/Art Scene 必须先保存，RoomDefinition、独立 Art Scene、catalog 与 Build Settings 在失败时必须回滚或保留为可恢复的完整组合。
- 全局同步忽略 GUID/path 均为空的草稿房间；公开校验与 binding repair 遇到已加载且未保存的 Art Scene 时必须拒绝操作，不得替用户保存、关闭或覆盖。
- `FpgRoomArtSceneEditorUtility` 以 GUID 为真源同步存储路径；postprocessor 只更新内存并标脏 RoomDefinition，必须由用户显式保存关卡后才落盘。
- `FpgRoomArtSceneContractValidator` 校验单一 identity root、匹配的 `FpgRoomArtRoot`/RoomDefinition/主方向光和禁止出现的组件；Art Scene 不能拥有 Camera、AudioListener 或 gameplay Host。
- `FpgProductionSceneList` 固定 Boot/FormalRoom 0/1，再按 room ID 追加 Art Scene；Build preprocessor 只校验，不得同步资产或改写 EditorBuildSettings。
- `FpgEncounterPreviewWindow/Utility` 只生成内存中的正式预览；Art Scene/相机预览退出、保存或进 Play Mode 时必须解除表现绑定并清理隐藏对象。
- Boot、FormalRoom、HUD、出口与 Art Scene 都是 committed authored 资产；编辑器工具不得重建或批量覆盖它们。
- 持久化修改只能由明确的用户操作通过 `SerializedObject`、`AssetDatabase`、`Undo` 和 `EditorSceneManager` 完成；仅失败回滚可逐字节备份/恢复刚创建的资产与 `.meta` 以保留 GUID，不能把该例外用于 authoring YAML。
- 所有入口必须 fail-closed，不得重建 CombatLab、旧 Stage、旧 Host 或隐式迁移源。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgRoomDuplicationContractTests.cs`、`FpgRoomAuthoringSafetyTests.cs` 和 `BuildSettingsTests.cs` 为准。
