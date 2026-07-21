# FPGDemo Formal Encounter 配置指南

本目录放正式遭遇的敌人、池、Profile、Override、Catalog 和 Level1 预设资产；优先由 installer 创建或刷新。

## 资产边界

- 根目录的 `FPG_*` 资产是普通房间的基础定义；`Level1/` 放固定三波遭遇的预设组合。
- 不要把 Encounter 配置反向塞回 RoomDefinition，也不要为正式遭遇就地改写旧 D0 资产。
- 默认资产由 `FpgFormalEncounterDefaultsInstaller` 和 `FpgFormalRoomLoopInstaller` 维护；重复流程应继续沉淀在 installer，而不是手工复制资产。
- Profile、Override、EnemyPool 和 Catalog 引用必须成套；稳定 ID 与 `Runtime/Unity/Config/` 中的校验合同保持一致。

## 验证

- 配置调整后先用 Room Editor 的 `Formal Encounter Preview` 检查解析结果。
- 房间绑定或场景引用变化时，再运行 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
