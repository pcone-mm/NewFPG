# Formal Encounter 配置指南

本目录中的 `FPG_*` 资产是正式遭遇的权威真源，不再由 D0 defaults installer 或 CombatLab 迁移生成。

- `Characters/` 保存正式玩家 Character、ThreeC、CombatFeel、Weapon 与 Presentation；`Characters/Skills/` 中的 Primary、Immediate Secondary、Charge Secondary、Reload 是 Fei 的权威技能资产，Weapon 只保留稳定引用。
- Fei Primary、两种 Secondary 与共享 Enemy 技能表现都使用 `Presentation/Characters/*/VFX/PF_FPG_*` wrapper；正式配置不得直连 `Assets/VFX_Klaus/` 或供应商 Timeline/VFX_Lab demo。wrapper 内部的源材质、网格或 prefab GUID 依赖必须随 `.meta` 保留。
- `FPG_NormalRoom_*`、`Level1/FPG_L1_01_*`、Enemy/Attack/Behavior/Catalog/Pool 必须成套维护。
- `FPG_PlayableCharacterCatalog.asset` 是 Boot 与 FormalRoom 共用的角色入口。
- `FPG_CombatPresentationProfile.asset` 是正式 HUD、反馈几何和容量入口。
- 玩家与敌人攻击统一使用 schema V3 技能时间轴：gameplay action 节点内联 trajectory/impact/collision 表现，独立 active presentation track 承载 muzzle/charge/audio/shake；不要恢复旧 payload/cue 字段或平行 attack runtime catalog。
- Luan 的 Summon 与 `SelfDestructOwner` 是两个显式动作；当前自毁在较晚 tick 无绑定执行。只有配置了绑定时，才要求指向同 tick、排序更早的 Summon event；不使用已删除的 `summonOwnerOutcome`。
- Enemy Behavior 的根运动默认关闭，只通过 `animationRootMotionRules` 为具体 Spine 动画启用；当前正式 prefab 的 VisualRoot/bridge 与 60Hz 动画时长合同必须成套维护。
- skill、sequence、event、track、warning、socket ID、authored ordinal 与 gameplay/presentation hash 是稳定合同；缺引用或校验失败时不得生成部分可用的 compiled skill。
- Boot/FormalRoom、HUD、出口和 Build Settings 都是显式维护的 authored 资产，不得由生成器或旧 D0 数据回写。
- 稳定 ID、引用、容量和动画键必须通过各自 `TryValidate`，失败时不得构造部分有效遭遇。
- 验证以 Unity 编译/Console、`FormalFirstAuthoringContractTests.cs`、`FpgPlayerSkillAssetContractTests.cs`、`FpgSkillDefinitionTests.cs` 和对应 Formal EditMode 合同为准；纯 V3 资产与 wrapper 路径检查 `FpgFormalSkillPresentationV3AssetTests.cs`，根运动检查 `FpgFormalEnemyRootMotionAssetTests.cs`。
