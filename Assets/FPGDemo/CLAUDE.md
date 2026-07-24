# FPGDemo 指南

本目录是独立的 FPG 正式战斗实现，命名空间前缀为 `FPG.Demo`。不要重新依赖已删除的根项目原型代码。

## 唯一主线

`Boot -> FormalRoom -> room-forest -> FpgEncounterHost/FpgFormalEncounterHost -> FpgRoomEncounterDirector -> FpgEncounterSession`

- `Boot.unity` 与 `FormalRoom.unity` 是唯一 build 场景，固定为 index 0/1。
- `BattleSession` 若仍保留，只是无 Unity host 的纯领域兼容代码，不是正式运行入口。
- 正式资产仍引用的 `D0CharacterDefinition`、`D0ThreeCProfile`、`D0ActorPresentationDefinition`、`D0WeaponDefinition`、`D0ActorSocketRegistry`、`D0ForestParallax*` 是序列化/GUID 兼容合同，不代表第二条主线。

## 目录边界

- `Runtime/Core|Combat|Player|Enemy|Run` 保持无 UnityEngine 依赖；依赖方向为 Core -> Combat -> Player/Enemy -> Run。
- `Runtime/Unity` 负责 Boot、Formal host、输入、物理、玩家/敌人组合和表现桥。
- `Config/FormalEncounter` 与 `Config/Level` 保存正式权威配置；不从 D0Slice 或 CombatLab 迁移。
- `Presentation/FormalEncounter` 保存正式 Entity/HUD/出口 prefab；`Presentation/Characters/*/Spine` 保存运行时渲染依赖。
- `SourceArt/CZN` 与 `Assets/Imported/CZN` 保存 CZN 源输入；项目负责人已确认这些素材可进入公开仓库。
- `Editor/LevelAuthoring` 只维护房间编辑、正式预览和 FormalRoom 安装，不得重建 CombatLab。

## 工作规则

- 不得从 `FPG.Demo.*` 引用 `NewFPG.*` 或根项目旧原型。
- 保留序列化字段名、GUID 和 `.meta`；重命名或移动资产时同步更新 installer、catalog 与合同测试。
- Scene/Prefab/Build Settings 通过 Unity Editor 或 Unity MCP 修改，不手工批量编辑 YAML。
- 配置转换、容量、ID 和生命周期均 fail-closed；不得回退到旧 Host、旧 Stage 或按敌人 ID 特判。
- Luan 召唤必须遵守正式事务：延迟阶段仅遥测/蓄力，入队结果为 `Queued` 后才允许 owner 死亡；Retry/Rejected 保留 owner。

## 验证

- 默认执行 Unity 编译、Console、依赖闭包、GUID/`.meta` 与 `git diff --check`。
- Build 入口检查 `BuildSettingsTests.cs`；正式 authoring 检查 `FormalFirstAuthoringContractTests.cs`。
- 房间与出口检查 `FpgRoomDefinitionTests.cs`、`FpgExitRoomRefreshRuleTests.cs`、`FpgRoomExitRuntimeTests.cs`。
- 默认不新增测试、不批量运行 EditMode/PlayMode；只有用户明确要求时才运行。
