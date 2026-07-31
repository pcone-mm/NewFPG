# LevelAuthoring Editor 指南

`FPG.LevelAuthoring.Editor` 只负责正式房间编辑、Scene View marker、Art Scene 合同、正式 Encounter 预览和验证。

- `FpgRoomEditorWindow`、UXML/USS 与 `FpgRoomSceneTool` 维护房间资产和 marker。
- Cover marker 写入 `RoomDefinition.coverSlots`；Scene Tool 分别编辑掩体 pose、玩家到达 pose 与独立镜头 Profile。新建/复制掩体从当前模板克隆 Profile，没有模板时拒绝放置；复制时同时偏移两种 pose 并清除副本的 `isStartingCover`。不要恢复 `D0ThreeCProfile.coverLocalPosition` 或静态镜头构图路径。
- `FpgCoverCameraProfileAuthoring` 统一负责 clone、make-unique、copy/paste、引用计数和孤立资产检查；删除掩体不自动删除 Profile，必须先确认无引用再显式清理。
- `FpgRoomAuthoringOperations` 只执行用户显式触发的整房复制、Art Scene root 修复和生产注册；源 RoomDefinition/Art Scene 必须先保存，每个不同的源镜头 Profile 只克隆一次并保留源房内共享关系。RoomDefinition、镜头 Profiles、独立 Art Scene、catalog 与 Build Settings 在失败时必须回滚或保留为可恢复的完整组合。
- 全局同步忽略 GUID/path 均为空的草稿房间；公开校验与 binding repair 遇到已加载且未保存的 Art Scene 时必须拒绝操作，不得替用户保存、关闭或覆盖。
- `FpgRoomArtSceneEditorUtility` 以 GUID 为真源同步存储路径；postprocessor 只更新内存并标脏 RoomDefinition，必须由用户显式保存关卡后才落盘。
- `FpgRoomArtSceneContractValidator` 只校验单一 scene root、匹配的 `FpgRoomArtRoot`/RoomDefinition 和禁止出现的 gameplay 组件；不校验灯光、Camera、AudioListener 或第三方表现插件。Art Scene 不能拥有 gameplay Host。
- `FpgProductionSceneList` 固定 Boot/FormalRoom 0/1，再按 room ID 追加 Art Scene；Build preprocessor 只校验，不得同步资产或改写 EditorBuildSettings。
- `FpgEncounterPreviewWindow/Utility` 只生成内存中的正式表现预览；该正式表现预览退出、保存或进 Play Mode 时必须解除表现绑定并清理隐藏对象。
- Editor 镜头预览只能创建 `DontSaveInEditor` 的临时相机、角色和遮体；Art Scene 是只读背景，不得调用 `FpgRoomArtRoot.TryBindPresentation`、解绑或回写任何持久化场景组件。Scene View 捕获与 Handle 只通过 Undo 修改当前 Profile。
- Boot、FormalRoom、HUD、出口与 Art Scene 都是 committed authored 资产；编辑器工具不得重建或批量覆盖它们。
- 持久化修改只能由明确的用户操作通过 `SerializedObject`、`AssetDatabase`、`Undo` 和 `EditorSceneManager` 完成；仅失败回滚可逐字节备份/恢复刚创建的资产与 `.meta` 以保留 GUID，不能把该例外用于 authoring YAML。
- 所有入口必须 fail-closed，不得重建 CombatLab、旧 Stage、旧 Host 或隐式迁移源。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs`、`FpgCoverCameraAuthoringTests.cs`、`FpgCoverCameraProfileTests.cs`、`FpgFormalCameraPoseUtilityTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgRoomDuplicationContractTests.cs`、`FpgRoomAuthoringSafetyTests.cs` 和 `BuildSettingsTests.cs` 为准。
