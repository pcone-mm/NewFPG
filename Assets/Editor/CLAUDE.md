# Editor 工具指南

这个目录放 Unity Editor 专用安装器、导入器、修复器和调试菜单，命名空间是 `NewFPG.EditorTools`。

## 职责

- `NewFPG/...` 菜单项、场景/Prefab 安装器和运行时探针。
- 美术/材质/Blender 导入后的 Unity 侧修复工具。
- 只在编辑器内运行的资源生成、动画安装、标签和序列化引用绑定。
- 关卡路线表、刷怪表和默认关卡绑定的 Inspector/安装辅助，当前集中在 `LevelEncounterTableEditor.cs` 和 `LevelFlowSceneInstaller.cs`。
- 战斗跳字 Sprite 导入参数、默认动画配置和默认 catalog 安装，当前集中在 `Combat/HitTipAssetInstaller.cs`。
- 炼器配置同步、炼器工作台场景安装和炼器 Inspector 辅助，当前集中在 `ForgingConfigEditorUtility.cs`、`ForgingConfigEditors.cs` 和 `ForgingWorkbenchSceneInstaller.cs`。
- 怪物 catalog JSON、ScriptableObject authoring 和 prefab JSON 绑定刷新，当前集中在 `MonsterConfigEditorUtility.cs`。
- Behavior Designer 默认鱼怪行为树重建、`BehaviorTree` 组件绑定和怪物技能/区域下拉，当前集中在 `MonsterConfigEditorUtility.cs` 与 `MonsterSkillIdDrawer.cs`。
- `WeaponDefinition` 自定义 Inspector 和旧 `SkillIndicatorConfig` 几何迁移工具，当前集中在 `Combat/WeaponDefinitionEditor.cs` 和 `Combat/WeaponDefinitionGeometryMigrationUtility.cs`。
- `BattleArenaZoneMap` 的 Inspector、Scene handle 和创建菜单集中在 `BattleArenaZoneMapEditor.cs`。
- 项目自有渲染扩展的 Inspector 辅助，例如 `DodgeSpeedLinesControllerEditor.cs`。
- Unity MCP / Unity-Skills 启动与本地自动化入口，当前集中在 `UnityMcpAutoStart.cs`。

## 边界

- 运行时玩法逻辑放 `Assets/Scripts/` 对应模块，不要藏在 Editor 菜单里。
- 安装器可以写 prefab、场景和 `ProjectSettings/TagManager.asset`，但应保持幂等：重复执行不能重复添加组件、状态或 Tag。
- 改 prefab/场景绑定时优先通过 `SerializedObject`、`PrefabUtility`、`AssetDatabase`、`Undo` 和 `EditorSceneManager`，避免手动大范围改 Unity YAML。

## 验证方式

- 修改安装器后，在 Unity 里执行对应菜单项，并检查 Console。
- 影响 `LevelFlowDirector` 或 Combat 基础安装时，优先跑相关 Editor 测试，再手动检查 Player、Fish、FirstPersonWeaponView prefab 和当前打开场景绑定。
- 改关卡路线表、刷怪表或默认场景绑定工具时，优先跑 `Assets/Tests/Editor/LevelEncounterResolverEditorTests.cs` 和 `Assets/Tests/Editor/LevelFlowDirectorEditorTests.cs`，再检查 `Assets/Settings/Level/` 里的表资产。
- 改怪物配置同步或 prefab JSON 绑定工具时，优先跑 `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`，再执行 `NewFPG/Monsters/Validate Monster JSON` 或 `NewFPG/Monsters/Refresh Prefab JSON Bindings` 检查结果。
- 改 Behavior Designer 任务抽屉、默认行为树重建或 Fish 行为树绑定时，同时跑 `Assets/Tests/Editor/MonsterBehaviorDesignerTasksEditorTests.cs` 和 `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`。
- 改 `BattleArenaZoneMapEditor.cs` 后，运行 `Assets/Tests/Editor/BattleArenaZoneMapEditorTests.cs`，并检查 `GameObject/NewFPG/Combat/Battle Arena Zone Map` 菜单能绑定到 `LevelFlowDirector`。
- 改 `WeaponDefinition` Inspector、施法几何迁移或 HUD debug 武器生成逻辑时，优先跑 `Assets/Tests/Editor/WeaponRuntimeSystemEditorTests.cs` 和 `Assets/Tests/Editor/SkillIndicatorSystemEditorTests.cs`。
- 改战斗跳字安装器时，执行 `NewFPG/Combat/Install Hit Tip Assets`，再跑 `Assets/Tests/Editor/MonsterCombatHudEditorTests.cs`，确认 `Assets/Resources/HitTips/` 可被运行时加载。
- 改炼器配置同步或工作台安装器时，优先跑 `Assets/Tests/Editor/ForgingSystemEditorTests.cs`，再执行 `NewFPG/Forging/Install Lianqi Workbench` 检查场景绑定。
- 改渲染扩展 Inspector 时，同步检查对应运行时组件指南，例如 `Assets/Rendering/CLAUDE.md`。
- 改 Unity MCP 自动启动或本地 HTTP 传输配置时，优先跑 `Assets/Tests/Editor/UnityMcpAutoStartTests.cs`，再检查 Editor Console 或 `/health` 可达性。
