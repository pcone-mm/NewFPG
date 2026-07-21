# Settings 使用指南

这个目录放 Unity 项目配置资源、渲染管线资源、Volume Profile 和玩法调试用 ScriptableObject 配置。

## 目录边界

- `Combat/` 放战斗相关 ScriptableObject 配置。
- `Combat/SO_CombatDodgePresentation_Default.asset` 是默认闪避表现配置，驱动战斗状态下的相机/武器位移、冷却显示和速度线时长。
- `Combat/HudDebug/` 放 `CombatHudWeaponDebug.unity` 使用的 HUD 调试武器和技能指示器配置；`HUD_Debug_*` 是 `WeaponDefinition`，`IND_HUD_Debug_*` 是 `SkillIndicatorConfig`。
- `Forging/` 放炼器配置。`weapon_blueprints.json` 和 `materials.json` 是默认 catalog 输入，`Blueprints/` 与 `Materials/` 放可编辑 ScriptableObject 镜像，`Weapons/WPN_*` 放由图纸运行时绑定生成或更新的 `WeaponDefinition`。
- `Level/` 放关卡路线和刷怪配置。`LevelRouteTable.asset` 是默认路线表，`LevelEncounterTable.asset` 是默认 encounter/波次刷怪表。
- `Monsters/` 放怪物配置。`monster_catalog.json` 是运行时默认 catalog，`MonsterCatalogAuthoring.asset` 是可编辑 ScriptableObject 镜像，`BehaviorTrees/BT_Fish.asset` 是鱼怪默认 Behavior Designer 外部行为树。
- `Prototype/` 放原型交互配置；`FirstPersonWeaponHudLayout.asset` 是 `PrototypeFirstPersonWeaponView` 的默认 HUD 武器布局 profile。
- 根目录的 `PC_*`、`Mobile_*`、`DefaultVolumeProfile.asset` 和 `UniversalRenderPipelineGlobalSettings.asset` 属于 URP/渲染配置，只有明确处理渲染管线或后处理时才改。

## 工作规则

- 移动或重命名配置资产时，保留并同步 `.meta`，避免断开场景、prefab 或 ScriptableObject 引用。
- `Combat/HudDebug/` 的武器和指示器配置直接维护；新增调试技能时同步更新相关 asset 与 `CombatHudWeaponDebug.unity` 的引用。
- 技能指示器资源 ID 必须和 `Assets/Art/SkillIndicators/Temporary/SO_IND_TemporaryArtIndex.asset` 保持一致。
- `WeaponDefinition` 已承载施法几何、输入策略、特效引用和基础战斗数值；不要把这些运行时字段只留在旧 `SkillIndicatorConfig` 里。
- 改闪避表现配置时，同步检查 `CombatDodgePresentationController` 的序列化引用和 `Shulin_L0.unity` 里的绑定；速度线参数仍要和 `Assets/Rendering/DodgeSpeedLines/` 的 Volume override 行为一致。
- 炼器配置的 JSON 与 ScriptableObject 镜像通过 `Assets/Editor/ForgingConfigEditorUtility.cs` 同步；不要手改一侧后忘记导出/同步另一侧。
- 炼器图纸的 `runtime.weaponDefinitionAssetPath`、HUD 图标和 `indicatorConfigPath` 会写入 `WeaponDefinition`，改路径后同步跑炼器 Editor 测试。
- 关卡路线房间通过 `encounterId` 查 `LevelEncounterTable`；改 id、波次或 prefab 引用时，同步检查 `LevelRouteTable.asset`、`LevelEncounterTable.asset` 和目标场景的 `LevelFlowDirector` 绑定。
- 怪物配置的 JSON 与 ScriptableObject 镜像通过 `Assets/Editor/MonsterConfigEditorUtility.cs` 同步；Fish prefab 只保留 `MonsterConfigBinding`、`monsterId` 和 catalog 引用，不要复制移动/攻击/生命调参字段到 prefab。
- `monster_catalog.json` 里的 `ai.behaviorTreePath` 要指向 `Assets/Settings/Monsters/BehaviorTrees/` 下的外部行为树；改路径后同步刷新 prefab 的 `BehaviorTree` 绑定，不要只改 JSON。
- 改 `Prototype/FirstPersonWeaponHudLayout.asset` 时同步检查 `Assets/Prefabs/Prototype/FirstPersonWeaponView.prefab`、`CombatHudWeaponDebug.unity` 和 `Shulin_L0.unity` 的 `PrototypeFirstPersonWeaponView` 引用；不要把稳定布局只留在场景覆盖里。
- 不要把 `ProjectSettings/`、URP asset 或 package 变更作为顺手清理项一起改。

## 验证方式

- 改 `Combat/HudDebug/` 后，运行 `Assets/Tests/Editor/SkillIndicatorSystemEditorTests.cs`，再打开 `CombatHudWeaponDebug.unity` 检查技能预览和目标锁定行为。
- 改 `SO_CombatDodgePresentation_Default.asset` 后，打开 `Shulin_L0.unity` 验证后退/左右闪避、冷却 HUD 和速度线表现。
- 改 `WeaponDefinition` 运行时几何、伤害、释放策略或锻造武器绑定后，运行 `Assets/Tests/Editor/WeaponRuntimeSystemEditorTests.cs`。
- 改 `Forging/` 后，打开 `lianqi.unity` 检查工作台加载、材料拖拽和武器绑定。
- 改 `Level/` 后，运行 `Assets/Tests/Editor/LevelEncounterResolverEditorTests.cs`；影响房间流程或场景绑定时同时跑 `Assets/Tests/Editor/LevelFlowDirectorEditorTests.cs`。
- 改 `Monsters/` 后，运行 `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`；改技能机制时同时跑 `MonsterSkillControllerEditorTests.cs` 和 `MonsterMechanicRunnerEditorTests.cs`。
- 改 `BehaviorTrees/` 或 `ai.behaviorTreePath` 后，同时运行 `Assets/Tests/Editor/MonsterBehaviorDesignerTasksEditorTests.cs`，确认行为树任务类型和中文元数据仍能解析。
- 改 `Prototype/FirstPersonWeaponHudLayout.asset` 后，运行 `Assets/Tests/Editor/PrototypeFirstPersonWeaponViewPreviewTests.cs`，再在目标场景检查武器卡位置、旋转和点击 hitbox。
- 改 URP、Volume Profile 或 Renderer asset 后，打开相关场景截图确认视觉效果，并检查 Console。
