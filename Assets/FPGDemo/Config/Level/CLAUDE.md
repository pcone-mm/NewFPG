# FPGDemo Config/Level 指南

这个目录保存 FPGDemo 房间系统的策划资产，只服务 `Assets/FPGDemo/` harness，不归根项目 `NewFPG.Level` 管。

## 目录边界

- `Rooms/` 放 `FpgRoomDefinition`，例如 CombatLab 默认房间和后续正式房间。
- `Groups/` 放 `FpgRoomGroupDefinition`，用于房间池、筛选和正式遭遇选择。
- `Tags/` 放 `FpgRoomTagDefinition`，用于房间语义标签和迁移标记。

## 工作规则

- `roomId`、group/tag ID、player entry、enemy spawn、exit、reachable 和 destructible marker ID 都是运行时合同；复制资产时必须改成新 ID。
- 房间资产优先通过 `Editor/LevelAuthoring` 的 Room Editor、Scene Tool 或迁移/安装器创建和修改，不要复制 YAML 后手工修局部字段。
- Enemy spawn 的 role 会影响 Formal Encounter 生成；改 marker 时同步检查 scenario 兼容校验，不要只看 Inspector 是否能保存。
- 新增资产时保留 `.meta`，并让 room/group/tag 引用走本目录内的稳定资产，不要直接绑定外部原型模块对象。

## 验证

- 改房间资产后，先用 Room Editor 校验面板检查 ID、marker 和 scenario 兼容性。
- 需要自动验证时，优先看 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`；涉及 D0 stage/scenario 时再看 `D0CombatScenarioDefinitionTests.cs` 和 `D0StageDefinitionTests.cs`。