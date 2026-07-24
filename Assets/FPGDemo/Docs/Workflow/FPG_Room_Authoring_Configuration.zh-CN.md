# FPG Room Authoring 指南

房间系统只描述空间、分类和出口，不拥有 Encounter 状态机。正式运行在同一个 `FormalRoom` 场景中实例化并切换 `FpgRoomDefinition`，不为每个房间加载独立 Scene。

## 权威资产

- 房间：`Config/Level/Rooms/Room_forest.asset`
- 环境：`Presentation/Level/Environment/ENV_forest.prefab`
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
- Reachable：寻路/可达性语义点。

room/group/tag/marker ID 均为跨资产稳定合同。复制房间或 marker 时必须生成新 ID；同一房间内不同 marker 类型也不得重名。

## 编辑工具

- `FPG Demo > Room Editor` 用于浏览、复制、分组、标签和结构校验。
- Scene Tool 用于在 Scene View 放置和调整 marker。
- Formal Encounter Preview 使用与运行时相同的 Request/Plan 入口，只保存在内存中。
- `Run in Active Formal Host` 只允许连接唯一已加载的正式 Host；缺失、重复或 digest 不一致时 fail-closed。
- 工具不得创建 CombatLab、D0 Stage、旧 Scenario 绑定或旧 Host。

## Encounter 边界

Room 只提供环境、marker 和分类。Profile/Override/EnemyPool 决定波次和敌人；`FpgRoomEncounterDirector` 在运行时校验 spawn role、容量和正式 host，再创建 `FpgEncounterSession`。

环境 Collider 是否进入战斗查询由正式 hitbox/physics 端口显式注册，不能仅凭环境 prefab 中存在 Collider 就假定已经接入。

## 制作流程

1. 建立环境 prefab，并确认根姿态、比例、材质和必要碰撞面。
2. 在 Room Editor 创建或复制 `FpgRoomDefinition`，设置唯一 room ID、group 和 tag。
3. 用 Scene Tool 放置 Player Entry、Enemy Spawn、Exit 及其他 marker。
4. 校验 marker ID、有限姿态、必需引用、spawn role 与 catalog。
5. 用 Formal Encounter Preview 检查 Profile/Override 在该房间的计划。
6. 保存后确认没有场景意外 diff，再检查 Unity 编译/Console。

## 跨房与出口

清场后，`FpgExitRoomRefreshRule` 从已校验 catalog 生成候选；选择顺序由 run context、源房间和出口稳定 ID 决定。切换前捕获玩家 run resources，释放旧 session/实体/投射物，再在同一 FormalRoom 中重建目标 Room。任何失败都进入 fault，不保留部分状态。

## 验证

- 房间结构：Room Editor 与 `FpgRoomDefinitionTests.cs`
- Catalog/出口：`FpgExitRoomRefreshRuleTests.cs`、`FpgRoomExitRuntimeTests.cs`
- 正式 authoring：`FormalFirstAuthoringContractTests.cs`
- 默认只运行 Unity 编译/Console；批量 EditMode/PlayMode 需用户明确要求。
