# FPGDemo 指南

本目录是独立的 FPG 正式战斗实现，命名空间前缀为 `FPG.Demo`。不要重新依赖已删除的根项目原型代码。

## 唯一主线

`Boot -> FormalRoom -> room-forest -> FpgEncounterHost/FpgFormalEncounterHost -> FpgRoomEncounterDirector -> FpgEncounterSession`

- `Boot.unity` 与 `FormalRoom.unity` 是固定为 index 0/1 的入口/Host 场景；`FpgProductionSceneList` 还会按 room ID 追加 catalog 中的 Art Scene，它们只由 FormalRoom additive 加载，不是第二套入口。
- `BattleSession` 若仍保留，只是无 Unity host 的纯领域兼容代码，不是正式运行入口。
- 正式资产仍引用的 `D0CharacterDefinition`、`D0ThreeCProfile`、`D0ActorPresentationDefinition`、`D0WeaponDefinition`、`D0ActorSocketRegistry`、`D0ForestParallax*` 是序列化/GUID 兼容合同，不代表第二条主线。

## 目录边界

- `Runtime/Core|Combat|Player|Enemy|Skills|Run` 保持无 UnityEngine 依赖；进入这些目录前先读对应局部指南。
- `Runtime/Unity` 负责 Boot、Formal host、输入、物理、玩家/敌人组合和表现桥。
- `Config/FormalEncounter` 与 `Config/Level` 保存正式权威配置；`Config/FormalEncounter/Characters/Skills` 保存 Fei 的正式技能资产，不从 D0Slice 或 CombatLab 迁移。
- `Presentation/FormalEncounter` 保存正式 Entity/HUD/出口 prefab；`Presentation/Level` 保存房间 Art Scene 与环境资源；`Presentation/Characters/*/Spine` 保存运行时渲染依赖，`Presentation/Characters/*/VFX` 保存项目自有特效 wrapper。
- 正式技能配置只引用 `Presentation/Characters/*/VFX/PF_FPG_*` wrapper；wrapper 可保留对 `Assets/VFX_Klaus/` 源材质、网格或 prefab 的显式 GUID 依赖，但不得引用供应商 `Timeline/` 或 `VFX_Lab/` demo。
- `SourceArt/CZN` 与 `Assets/Imported/CZN` 保存 CZN 源输入；项目负责人已确认这些素材可进入公开仓库。
- `Editor/` 保存发布构建和薄 Inspector；`Editor/LevelAuthoring` 维护房间编辑、Art Scene 合同、正式预览与验证，进入后先读对应局部指南。
- `Editor/SkillAuthoring` 保存纯 V3 技能时间轴、校验和隔离预览工具；常规配置修改从 `FPG Demo/Skill Editor` 进入，并先看该目录局部指南。
- `Integrations/` 隔离项目对第三方运行时 API 的直接适配；当前体积雾/体积光绑定不得扩散到纯领域程序集或 `FPG.Unity` asmdef。

## 工作规则

- 不得从 `FPG.Demo.*` 引用 `NewFPG.*` 或根项目旧原型。
- 保留序列化字段名、GUID 和 `.meta`；重命名或移动资产时同步更新 catalog 与合同测试。
- Scene/Prefab/Build Settings 通过 Unity Editor 或 Unity MCP 修改，不手工批量编辑 YAML。
- 配置转换、容量、ID 和生命周期均 fail-closed；不得回退到旧 Host、旧 Stage 或按敌人 ID 特判。
- 技能 tick、stable ID、authored ordinal、gameplay/presentation hash 和 execution ID 是跨 Runtime/Unity/Editor 的合同；不要在适配层另建第二套时间轴或表现语义。

## 验证

- 默认执行 Unity 编译、Console、依赖闭包、GUID/`.meta` 与 `git diff --check`。
- Build 入口与 catalog Art Scene 顺序检查 `BuildSettingsTests.cs`；正式 authoring 检查 `FormalFirstAuthoringContractTests.cs`。
- 房间与出口检查 `FpgRoomDefinitionTests.cs`、`FpgExitRoomRefreshRuleTests.cs`、`FpgRoomExitRuntimeTests.cs`。
- Art Scene 合同与加载回滚检查 `FpgRoomArtSceneContractTests.cs`、`FpgRoomArtSceneLoaderPlayModeTests.cs`。
- 默认不新增测试、不批量运行 EditMode/PlayMode；只有用户明确要求时才运行。
