# FPG Room Authoring 指南

房间系统描述空间、分类、掩体镜头和出口，不拥有 Encounter 状态机。正式运行始终复用 `FormalRoom` 中的 Host 与 Camera Rig，并按 `FpgRoomDefinition` 加载对应的 Art Scene。

## 权威资产

- 房间：`Config/Level/Rooms/Room_forest.asset`
- 掩体镜头：`Config/Level/CameraProfiles/<Room>/CAM_<room>_<cover>.asset`
- 默认镜头模板：`Config/Level/CameraProfiles/FPG_Default_CoverCamera.asset`
- Art Scene：`Presentation/Level/Rooms/<Room>/ART_<Room>.unity`
- 房间组：`Config/Level/Groups`
- 标签：`Config/Level/Tags/RoomTag_Forest.asset`
- Catalog：`Config/Level/FPG_RoomCatalog.asset`
- 出口刷新：`Config/Level/FPG_ExitRoomRefreshRule.asset`

当前稳定房间 ID 为 `room-forest`。

## Marker 合同

`FpgRoomDefinition` 可包含：

- Player Entry：玩家进入位置和朝向；
- Enemy Spawn：敌人出生位置、朝向与 role；
- Exit：清场后可激活的出口；
- Destructible：房间可破坏物插槽；
- Cover：掩体实体、耐久、玩家到达 Pose 与该掩体的镜头 Profile。

room/group/tag ID 是跨资产稳定合同，marker ID 在单个房间内稳定且唯一。复制整房时保留 marker ID 并生成新的 room ID；复制单个 marker 时生成新 marker ID。

## 掩体镜头合同

每个 Cover Slot 必须显式引用一个有效的 `FpgCoverCameraProfile`。Profile 保存相对玩家到达 Pose 的 Camera Rig Pose、Camera 子节点本地 Pose、FOV、近远裁剪面，以及只供编辑器显示的玩家视口参考点和关注点参考线。它不保存移动时长、曲线、瞄准距离、后坐或震屏参数。

- 新建掩体时，从 Room Editor 当前“镜头模板”克隆一份独立 Profile；没有模板时拒绝放置。
- 复制掩体时默认克隆 Profile，避免修改副本时影响源掩体。
- 复制整房时，每个不同的源 Profile 只克隆一次；源房内部主动共享的引用关系在新房中保持。
- 可以主动把同一个 Profile 拖给多个掩体以共享构图。详情面板会显示引用数量，“复制为独立配置”可解除共享。
- 删除掩体不会自动删除 Profile。使用孤立镜头资产检查确认无引用后，再显式清理资产。
- 缺失或无效 Profile 是房间校验错误，正式运行不会回退到角色 3C 镜头。

## 编辑工具

- `FPG Demo > Room Editor` 用于浏览、复制、分组、标签和结构校验。
- Scene Tool 用于在 Scene View 放置和调整 marker。
- 镜头预览固定使用临时 16:9 Game View，预览 Camera、角色和辅助对象均带 `DontSaveInEditor`，不会写入 Art Scene。
- 启用镜头预览时默认选中起始掩体；选择其他掩体后立即切换，并在 Profile 或玩家到达 Pose 变化时刷新。
- 上一/下一掩体用于顺序检查，过渡预览使用正式 `CoverTraversalSeconds` 与 `SmoothStep`。
- Scene View 显示 Frustum、裁剪面、角色实际视口位置、角色参考点和关注点参考线；镜头 Handle 的修改通过 Undo 写入当前 Profile。
- “从 Scene View 捕获”写入当前掩体 Profile；“恢复模板”把当前模板值复制到当前 Profile，不改变资产引用。
- Formal Encounter Preview 使用与运行时相同的 Request/Plan 入口，只保存在内存中。
- `Run in Active Formal Host` 只允许连接唯一已加载的正式 Host；缺失、重复或 digest 不一致时 fail-closed。
- 工具不得创建 CombatLab、D0 Stage、旧 Scenario 绑定或旧 Host。

## Encounter 边界

Room 只提供环境、marker 和分类。Profile/Override/EnemyPool 决定波次和敌人；`FpgRoomEncounterDirector` 在运行时校验 spawn role、容量和正式 host，再创建 `FpgEncounterSession`。

环境 Collider 是否进入战斗查询由正式 hitbox/physics 端口显式注册，不能仅凭环境 prefab 中存在 Collider 就假定已经接入。

## 制作流程

1. 建立并保存 Art Scene，确认根姿态、比例、材质、灯光和必要碰撞面。
2. 在 Room Editor 创建或复制 `FpgRoomDefinition`，设置唯一 room ID、group 和 tag。
3. 选择镜头模板，再用 Scene Tool 放置 Player Entry、Enemy Spawn、Cover 和 Exit。
4. 逐个选择 Cover，以 16:9 预览和 Scene View 辅助线调整玩家到达 Pose 与独立镜头 Profile。
5. 校验 marker ID、有限姿态、掩体 Profile、spawn role、Art Scene 与 catalog；同时检查孤立镜头资产。
6. 用 Formal Encounter Preview 检查 Profile/Override 在该房间的计划。
7. 保存后退出镜头预览，确认无临时对象或场景意外 diff，再检查 Unity 编译/Console。

## 跨房与出口

清场后，`FpgExitRoomRefreshRule` 从已校验 catalog 生成候选；选择顺序由 run context、源房间和出口稳定 ID 决定。切换前捕获玩家 run resources，释放旧 session/实体/投射物，再在同一 FormalRoom 中重建目标 Room。任何失败都进入 fault，不保留部分状态。

## 验证

- 房间与镜头结构：Room Editor、`FpgRoomDefinitionTests.cs` 与镜头 Profile/Shot EditMode 测试
- 复制与共享：`FpgRoomDuplicationContractTests.cs`、Room Authoring 资产操作测试
- Catalog/出口：`FpgExitRoomRefreshRuleTests.cs`、`FpgRoomExitRuntimeTests.cs`
- 正式 authoring：`FormalFirstAuthoringContractTests.cs`
- 默认只运行 Unity 编译/Console；批量 EditMode/PlayMode 需用户明确要求。
