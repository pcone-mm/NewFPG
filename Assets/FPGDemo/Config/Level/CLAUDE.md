# Config/Level 指南

本目录保存正式房间、房间组、标签、catalog、掩体镜头配置与出口刷新规则。

- 默认房间是 `Rooms/Room_forest.asset`，稳定 ID 为 `room-forest`。
- 每个 `FpgRoomDefinition` 通过稳定 GUID 与 `Assets/` 相对路径一对一引用自己的 Art Scene；路径由 Editor 同步，运行时不回退到旧 `environmentPrefab`。
- `CameraProfiles/FPG_Default_CoverCamera.asset` 是新建掩体的模板；正式 Profile 位于 `CameraProfiles/<Room>/CAM_<room>_<cover>.asset`，保存相对玩家到达 Pose 的 Rig/Camera Pose、镜头参数与编辑器构图参考，不保存移动时长、后坐或震屏。
- `FPG_RoomCatalog.asset` 与 `FPG_ExitRoomRefreshRule.asset` 共同定义清场后的候选房间和确定性出口刷新；catalog 中的 Art Scene GUID、路径和 scene name 必须各自唯一。
- `coverSlots` 是房间的独立掩体真源：每项保存 `FpgCoverEntityView` prefab、正耐久、掩体 pose、玩家到达 pose、左右探身位置和有效的 `FpgCoverCameraProfile`；左右探身位置必须有限，且彼此及与到达点都不重合。每房至少一个且恰有一个初始掩体，玩家到达点的 X 不能重合。缺失或无效 Profile 必须 fail-closed，不回退到 `D0ThreeCProfile`。
- room ID 与 Art Scene GUID 是跨资产身份；整房复制时必须生成新的两者。marker ID 只在单个房间内生效，可以随房间保留；group/tag 引用默认共享，除非设计意图明确改变。
- 复制正式房间只使用 Room Editor 的 `Duplicate Room` / `FpgRoomAuthoringOperations`：修复 `FpgRoomArtRoot` 绑定、为每个不同的源镜头 Profile 克隆一次并保留房内显式共享拓扑，同时把 catalog/Build Settings 注册视为同一事务；不要手工复制资产后单独改 Build Settings。
- Enemy spawn role 必须与正式 Encounter Profile/Override 和 spawn role 预检一致。
- 通过 Room Editor 的 SceneAsset 字段、Scene Tool 或 Unity MCP 修改，不复制 YAML，也不只改 GUID/path 其中一侧。
- 验证以 Room Editor、Unity 编译/Console、`FpgRoomDefinitionTests.cs`、`FpgCoverCameraProfileTests.cs`、`FpgCoverCameraAuthoringTests.cs`、`FpgFormalCameraPoseUtilityTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgRoomDuplicationContractTests.cs`、`FpgRoomAuthoringSafetyTests.cs`、`BuildSettingsTests.cs`、`FpgExitRoomRefreshRuleTests.cs` 和 `FpgRoomExitRuntimeTests.cs` 为准。
