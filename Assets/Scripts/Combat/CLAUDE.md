# Combat 模块指南

这个目录负责当前实时战斗原型组件，命名空间是 `NewFPG.Combat`。

## 职责

- 生命、护盾、受击、死亡、资源池和 `CombatVitalsAuthoring` 调参组件。
- 武器定义、运行时武器实例/修饰器、玩家施法、冷却、消耗和命中结算。
- 通用攻击预警、伤害投递、命中体积求交和旧怪物攻击兼容组件。
- 技能指示器输入、预览、瞄准求解、渲染池和临时资源索引。
- 临时战斗 HUD、怪物血条/战斗跳字 HUD、玩家受击反馈、闪避表现、第一人称武器视图的战斗绑定和底部 HUD 资源表现。
- `BattleArenaZoneMap` 是战斗场景内的 3x3 区域图组件，给怪物行为树的区域移动节点提供稳定 zone id 和采样范围。

## 边界

- 房间流程、门选择、奖励预览、敌人生成节奏和相机状态切换属于 `Assets/Scripts/Level/`。
- 旧神器自动释放、目标选择和不依赖场景物体的领域规则仍属于 `Assets/Scripts/Battle/`。
- 怪物 AI、移动、技能选择、机制执行和 `monster_catalog.json` DTO 属于 `Assets/Scripts/Monsters/`；Combat 侧只提供生命、伤害、预警和兼容桥接。
- 只服务某个 prefab 的引用假设要在对应组件序列化字段或安装脚本附近保持清晰，不要写进全局规则。
- `SkillIndicators/` 子目录属于 `NewFPG.Combat.SkillIndicators`，资源引用通过 `SkillIndicatorConfig` 和 `SkillIndicatorTemporaryArtIndex` 的字符串 ID 解析；改 ID 时同步检查 `Assets/Art/SkillIndicators/Temporary/` 和 `Assets/Settings/Combat/HudDebug/`。
- `WeaponDefinition` 是运行时施法几何、输入策略、特效引用和基础数值的主要来源；`SkillIndicatorConfig` 只作为旧数据迁移来源保留。
- 战斗跳字通过 `MonsterCombatHud`、`DamageNumberView`、`HitTipCatalog` 和 `HitTipAnimationConfig` 维护；默认 catalog 运行时从 `Assets/Resources/HitTips/SO_HTC_Default.asset` 加载，源图层在 `Assets/Art/HUD/Hit_tip/`。
- 闪避表现通过 `CombatDodgePresentationController` 和 `CombatDodgePresentationConfig` 维护；配置资产在 `Assets/Settings/Combat/SO_CombatDodgePresentation_Default.asset`，速度线渲染实现仍属于 `Assets/Rendering/DodgeSpeedLines/`。
- `PrototypeWeaponCombatHud` 消费 `PrototypeFirstPersonWeaponView`、`PlayerWeaponCaster`、资源池和闪避表现；武器卡布局归 `Assets/Scripts/Prototype/` 与 `Assets/Settings/Prototype/FirstPersonWeaponHudLayout.asset`，Combat 侧不要复制一套位置数据。
- 第一人称战斗 HUD 默认底图路径是 `Assets/Art/Weapons/HUD/2d_di.png`，资源底座路径是 `Assets/Art/Weapons/HUD/2d_dou.png`；改名或迁移时同步检查 `PrototypeWeaponCombatHud` 和关联 prefab/scene。
- `BattleArenaZoneMap` 的 zone id 使用 `left_front`、`center_mid`、`right_back` 等稳定字符串；怪物侧只消费这些 id，不应在行为树里另造坐标体系。

## 已实现反馈

- 玩家受击反馈在 `PlayerHitFeedback.cs`，挂载点是带有 `CombatVitals` 的玩家对象。
- `LevelFlowDirector.ResolveReferences()` 会在玩家已有 `CombatVitals` 且缺少反馈组件时运行时补上 `PlayerHitFeedback`。

- `PlayerHitFeedback` 监听 `CombatVitals.Damaged`，触发活动游戏相机短暂震动和运行时 `PlayerHitFeedbackCanvas` 的屏幕红边闪烁。
- 若活动游戏相机带 `CinemachineBrain`，受击震动走 `CinemachineImpulseSource` 和 `CinemachineExternalImpulseListener`，避免 Cinemachine 覆盖手动相机位移；没有 Cinemachine 输出相机时才回退到本地位移震动。
- 相关测试在 `Assets/Tests/Editor/PlayerHitFeedbackEditorTests.cs`。

## 验证方式

- 修改后等待 Unity 编译并检查 Console。
- 改生命、伤害、施法、怪物攻击兼容桥接、玩家受击反馈或 HUD 时，优先跑相关 Editor 测试。
- 改 `WeaponDefinition`、运行时武器实例、施法命中体积或默认无预览释放路径时，优先跑 `Assets/Tests/Editor/WeaponRuntimeSystemEditorTests.cs`。
- 改怪物血条或跳字 HUD 时，优先跑 `Assets/Tests/Editor/MonsterCombatHudEditorTests.cs`。
- 改第一人称武器 HUD 绑定、底部 HUD 资源、武器 quad 或 layout profile 后，优先跑 `Assets/Tests/Editor/PrototypeFirstPersonWeaponViewPreviewTests.cs`，再打开 `CombatHudWeaponDebug.unity` 或 `Shulin_L0.unity` 检查实际画面。
- 改 `BattleArenaZoneMap` 区域划分、归一化规则或 Scene handle 后，运行 `Assets/Tests/Editor/BattleArenaZoneMapEditorTests.cs`，并在包含 `LevelFlowDirector` 的场景检查区域图绑定。
- 改默认战斗跳字 catalog 或动画配置时，同步检查 `Assets/Resources/CLAUDE.md`，并确认 `Resources.Load("HitTips/SO_HTC_Default")` 仍能解析。
- 改怪物 AI、技能机制或 JSON 绑定时，同步检查 `Assets/Scripts/Monsters/CLAUDE.md` 的验证路径。
- 改闪避输入、冷却显示、相机/武器位移或速度线开关后，在 `Shulin_L0.unity` 进入战斗状态手动验证，并检查 `Assets/Settings/CLAUDE.md` 与 `Assets/Rendering/CLAUDE.md`。
- 改技能指示器配置、瞄准或临时资源索引时，优先跑 `Assets/Tests/Editor/SkillIndicatorSystemEditorTests.cs`，再打开 `CombatHudWeaponDebug.unity` 做视觉检查。
- 没有自动覆盖时，在场景里验证玩家武器、鱼怪攻击预警、受击反馈和资源/血条刷新。
- 改 prefab 绑定时同步检查对应 `.meta` 和序列化引用，不要手动大范围重写 prefab YAML。
