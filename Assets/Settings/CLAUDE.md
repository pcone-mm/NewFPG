# Settings 使用指南

这个目录放 Unity 项目配置资源、渲染管线资源、Volume Profile 和玩法调试用 ScriptableObject 配置。

## 目录边界

- `Combat/` 放战斗相关 ScriptableObject 配置。
- `Combat/HudDebug/` 放 `CombatHudWeaponDebug.unity` 使用的 HUD 调试武器和技能指示器配置；`HUD_Debug_*` 是 `WeaponDefinition`，`IND_HUD_Debug_*` 是 `SkillIndicatorConfig`。
- `Forging/` 放炼器配置。`weapon_blueprints.json` 和 `materials.json` 是默认 catalog 输入，`Blueprints/` 与 `Materials/` 放可编辑 ScriptableObject 镜像，`Weapons/WPN_*` 放由图纸运行时绑定生成或更新的 `WeaponDefinition`。
- `Prototype/` 放原型交互配置。
- 根目录的 `PC_*`、`Mobile_*`、`DefaultVolumeProfile.asset` 和 `UniversalRenderPipelineGlobalSettings.asset` 属于 URP/渲染配置，只有明确处理渲染管线或后处理时才改。

## 工作规则

- 移动或重命名配置资产时，保留并同步 `.meta`，避免断开场景、prefab 或 ScriptableObject 引用。
- `Combat/HudDebug/` 的武器和指示器配置由 `Assets/Editor/Combat/CombatHudDebugSceneInstaller.cs` 维护；新增调试技能时优先改安装器并重新生成，不要只手改孤立 asset。
- 技能指示器资源 ID 必须和 `Assets/Art/SkillIndicators/Temporary/SO_IND_TemporaryArtIndex.asset` 保持一致。
- 炼器配置的 JSON 与 ScriptableObject 镜像通过 `Assets/Editor/ForgingConfigEditorUtility.cs` 同步；不要手改一侧后忘记导出/同步另一侧。
- 炼器图纸的 `runtime.weaponDefinitionAssetPath`、HUD 图标和 `indicatorConfigPath` 会写入 `WeaponDefinition`，改路径后同步跑炼器 Editor 测试。
- 不要把 `ProjectSettings/`、URP asset 或 package 变更作为顺手清理项一起改。

## 验证方式

- 改 `Combat/HudDebug/` 后，运行 `Assets/Tests/Editor/SkillIndicatorSystemEditorTests.cs`，再打开 `CombatHudWeaponDebug.unity` 检查技能预览和目标锁定行为。
- 改 `Forging/` 后，运行 `Assets/Tests/Editor/ForgingSystemEditorTests.cs`，再打开或安装 `lianqi.unity` 检查工作台加载、材料拖拽和武器绑定。
- 改 URP、Volume Profile 或 Renderer asset 后，打开相关场景截图确认视觉效果，并检查 Console。
