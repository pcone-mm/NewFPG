# LevelAuthoring Editor 指南

`FPG.LevelAuthoring.Editor` 负责正式房间编辑、Scene View marker、Art Scene 合同、正式 Encounter 预览，以及明确隔离的 BattleTest 开发路由。

- `FpgRoomEditorWindow`、UXML/USS 与 `FpgRoomSceneTool` 维护房间资产和 marker。
- marker 的类型显隐与“显示编辑手柄”只控制 Scene View 绘制/交互，不得清空选择或改写 RoomDefinition；选中 marker 的编辑手柄默认隐藏，并且只有该类型可见且用户显式开启时才绘制。
- Cover marker 写入 `RoomDefinition.coverSlots`；Scene Tool 分别编辑掩体 pose、玩家到达 pose、左右探身位置与独立镜头 Profile。新建/复制掩体从当前模板克隆 Profile，没有模板时拒绝放置；复制时同时偏移掩体、到达点和两个探身点，并清除副本的 `isStartingCover`。不要恢复 `D0ThreeCProfile.coverLocalPosition`、prefab 内固定探身偏移或静态镜头构图路径。
- `FpgCoverCameraProfileAuthoring` 统一负责 clone、make-unique、copy/paste、引用计数和孤立资产检查；删除掩体不自动删除 Profile，必须先确认无引用再显式清理。
- `FpgRoomAuthoringOperations` 只执行用户显式触发的草稿新建、整房复制、生产移出/登记、彻底删除和 Art Scene root 修复。新建生成未登记生产的 RoomDefinition 与绑定匹配定义的 Art Scene 骨架；复制名称同时用于资产名、显示名和 Art Scene 文件名，ID 自动生成，每个不同的源镜头 Profile 只克隆一次并保留源房内共享关系。移出生产保留文件；彻底删除前必须检查生产登记与项目引用，并只删除无外部引用的 Profile。RoomDefinition、Profiles、独立 Art Scene、catalog 与 Build Settings 在失败时必须回滚或保留为可恢复的完整组合。
- Cover `scaleMultiplier` 是相对 Prefab 根缩放的正有限 XYZ 倍率，旧资产缺少该字段时按 `(1,1,1)` 迁移。Scene View Scale 工具与运行时实例必须一致地应用该倍率，玩家到达/探身 pose 和镜头配置保持独立；`0.1` 至 `10` 之外只警告。
- 房间选择按 RoomDefinition 资产 GUID 同步。Art Scene 切换失败时仍可编辑目标 RoomDefinition，但场景工具必须禁用并明确报告当前场景未切换；草稿保存状态与生产合同状态分开展示，正式玩法预览必须列出完整校验错误并阻止进入。
- 全局同步忽略 GUID/path 均为空的草稿房间；公开校验与 binding repair 遇到已加载且未保存的 Art Scene 时必须拒绝操作，不得替用户保存、关闭或覆盖。
- `FpgRoomArtSceneEditorUtility` 以 GUID 为真源同步存储路径；postprocessor 只更新内存并标脏 RoomDefinition，必须由用户显式保存关卡后才落盘。
- `FpgRoomArtSceneContractValidator` 要求场景内恰有一个顶层 `FpgRoomArtRoot` 且引用匹配的 RoomDefinition；允许额外的顶层非 gameplay 对象，但整个场景都禁止 gameplay 组件/Host。不校验灯光、Camera、AudioListener 或第三方表现插件。
- `FpgSpriteShadowCasterAuthoring`/postprocessor 只生成按 sprite GUID 归档的 `__ShadowCasterProxy` Mesh/Material 视觉资产，不生成掩体阻挡 `MeshCollider`；Cover prefab 仍必须在同一对象显式维护匹配 collider 并通过 `FpgCoverEntityView` 合同。
- `FpgProductionSceneList` 固定 Boot/FormalRoom 0/1，再按 room ID 追加 Art Scene；Build preprocessor 只校验，不得同步资产或改写 EditorBuildSettings。
- `FpgBattleTestDevelopmentSceneList` 只在开发清单 index 0 前置 `Assets/InitTestScene/BattleTest.unity`，其后完整复用生产清单；Release guard 必须拒绝包含 BattleTest 的非 Development 构建。
- Play Mode 顶栏只在用户选择 Battle Test 时设置该 start scene；Test Runner 活跃或测试 bootstrap scene 被加载时必须让测试框架拥有启动场景，不得用工具栏 override 干扰测试。
- `FpgBattleGmEditorWindow` 只连接已 ready 的 `FpgBattleTestBootstrap` 并转发 `gm.god`、`gm.ai`、`gm.spawn`；不得直接改正式资产、绕过 runtime 校验或连接生产模式 session。
- `FpgEncounterPreviewWindow/Utility` 只生成内存中的正式表现预览；该正式表现预览退出、保存或进 Play Mode 时必须解除表现绑定并清理隐藏对象。
- Editor 镜头预览只能创建 `DontSaveInEditor` 的临时相机、角色和遮体；Art Scene 是只读背景，不得调用 `FpgRoomArtRoot.TryBindPresentation`、解绑或回写任何持久化场景组件。Scene View 捕获与 Handle 只通过 Undo 修改当前 Profile。
- Boot、FormalRoom、HUD、出口与 Art Scene 都是 committed authored 资产；编辑器工具不得重建或批量覆盖它们。
- 持久化修改只能由明确的用户操作通过 `SerializedObject`、`AssetDatabase`、`Undo` 和 `EditorSceneManager` 完成；仅失败回滚可逐字节备份/恢复刚创建的资产与 `.meta` 以保留 GUID，不能把该例外用于 authoring YAML。
- 所有入口必须 fail-closed，不得重建 CombatLab、旧 Stage、旧 Host 或隐式迁移源。
- Demo 默认只验证 Unity 编译/Console 和编辑器人工操作；不要自动运行测试。用户明确要求时，仅按 `Assets/FPGDemo/Tests/CLAUDE.md` 选择一个相关冒烟测试。
