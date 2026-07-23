# FPGDemo Formal Encounter 配置指南

本目录放正式遭遇的敌人、池、Profile、Override、Catalog 和 Level1 预设资产；优先由 installer 创建或刷新。

## 资产边界

- 根目录的 `FPG_*` 资产是普通房间的基础定义；`Level1/` 放固定三波遭遇的预设组合。
- `FPG_NormalRoom_*` 与 `Level1/FPG_L1_01_*` 的当前活动敌人表包含 Burstbug/Hudie/Luan；Pool、Enemy Catalog、Attack Runtime Catalog 与固定波次必须成套维护。
- `FPG_PlayableCharacterCatalog.asset` 是 Boot 角色选择与 FormalRoom 运行时玩家组装的共享入口；默认项必须同时提供角色定义、ThreeC profile 和 visual-only 选择预览。
- 不要把 Encounter 配置反向塞回 RoomDefinition，也不要为正式遭遇就地改写旧 D0 资产。
- `FpgFormalEncounterDefaultsInstaller` 可在编辑器阶段读取已导入的 D0 prefab、Presentation、Behavior、Training 与攻击定义并迁移所需字段；Player Loop 仍只读取 `FpgEnemyDefinition`、正式 Attack Runtime Catalog 和正式 Entity View。
- 默认资产由 `FpgFormalEncounterDefaultsInstaller` 和 `FpgFormalRoomLoopInstaller` 维护；重复流程应继续沉淀在 installer，而不是手工复制资产。
- Profile、Override、EnemyPool 和 Catalog 引用必须成套；稳定 ID 与 `Runtime/Unity/Config/` 中的校验合同保持一致。

## 验证

- 配置调整后先用 Room Editor 的 `Formal Encounter Preview` 检查解析结果。
- Burstbug/Hudie/Luan 正式 prefab 必须绑定 SkeletonAnimation，且 behavior/attack 中的全部动画键通过 `FpgEnemyDefinition.TryValidate`。
- 扫描 NormalRoom 与四个 L1 Override，确认三种正式敌人引用齐全，且没有 D0 Stage、运行时 D0 Encounter 或按敌人 ID 硬编码的刷怪/攻击分支。
- 改 playable character catalog 或 Formal First 默认绑定时，检查 `Assets/FPGDemo/Tests/EditMode/FormalFirstAuthoringContractTests.cs`。
- 房间绑定或场景引用变化时，再运行 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
