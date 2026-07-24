# Formal Encounter 配置指南

本目录中的 `FPG_*` 资产是正式遭遇的权威真源，不再由 D0 defaults installer 或 CombatLab 迁移生成。

- `Characters/` 保存正式玩家 Character、ThreeC、CombatFeel、Weapon 与 Presentation。
- `FPG_NormalRoom_*`、`Level1/FPG_L1_01_*`、Enemy/Attack/Behavior/Catalog/Pool 必须成套维护。
- `FPG_PlayableCharacterCatalog.asset` 是 Boot 与 FormalRoom 共用的角色入口。
- `FPG_CombatPresentationProfile.asset` 是正式 HUD、反馈几何和容量入口。
- `FpgFormalRoomLoopInstaller` 只维护 Boot/FormalRoom 组合、HUD、出口和 Build Settings，不把旧 D0 资产写回正式配置。
- 稳定 ID、引用、容量和动画键必须通过各自 `TryValidate`，失败时不得构造部分有效遭遇。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs` 和对应 Formal EditMode 合同为准。
