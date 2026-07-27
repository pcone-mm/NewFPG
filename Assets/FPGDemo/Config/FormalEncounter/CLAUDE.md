# Formal Encounter 配置指南

本目录中的 `FPG_*` 资产是正式遭遇的权威真源，不再由 D0 defaults installer 或 CombatLab 迁移生成。

- `Characters/` 保存正式玩家 Character、ThreeC、CombatFeel、Weapon 与 Presentation；`Characters/Skills/` 中的 Primary、Secondary、Reload 是 Fei 的权威技能资产，Weapon 只保留稳定引用。
- Fei Primary/Secondary 与共享 Enemy 技能表现都使用 `Presentation/Characters/*/VFX/PF_FPG_*` wrapper；正式配置不得直连 `Assets/VFX_Klaus/` 或供应商 Timeline/VFX_Lab demo。wrapper 内部的源材质、网格或 prefab GUID 依赖必须随 `.meta` 保留。
- `FPG_NormalRoom_*`、`Level1/FPG_L1_01_*`、Enemy/Attack/Behavior/Catalog/Pool 必须成套维护。
- `FPG_PlayableCharacterCatalog.asset` 是 Boot 与 FormalRoom 共用的角色入口。
- `FPG_CombatPresentationProfile.asset` 是正式 HUD、反馈几何和容量入口。
- 玩家与敌人攻击统一使用 schema V3 技能时间轴：gameplay action 节点内联 trajectory/impact/collision 表现，独立 active presentation track 承载 muzzle/charge/audio/shake；不要恢复旧 payload/cue 字段或平行 attack runtime catalog。
- skill、sequence、event、track、warning、socket ID、authored ordinal 与 gameplay/presentation hash 是稳定合同；缺引用或校验失败时不得生成部分可用的 compiled skill。
- `FpgFormalRoomLoopInstaller` 只维护 Boot/FormalRoom 组合、HUD、出口和 Build Settings，不把旧 D0 资产写回正式配置。
- 稳定 ID、引用、容量和动画键必须通过各自 `TryValidate`，失败时不得构造部分有效遭遇。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs`、`FpgPlayerSkillAssetContractTests.cs`、`FpgSkillDefinitionTests.cs` 和对应 Formal EditMode 合同为准；纯 V3 资产与 wrapper 路径另检查 `FpgFormalSkillPresentationV3AssetTests.cs`。
