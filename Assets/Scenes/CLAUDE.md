# Scenes 使用指南

## 场景角色

- `SampleScene.unity` 是当前已跟踪的基础场景。
- `PrototypeCaveBattleScene.unity` 是原型洞穴战斗场景。
- `LevelScene.unity` 是当前森林切图关卡原型场景，常由 `Assets/Editor/ForestCutoutLevelSceneBuilder.cs` 更新。
- `CombatHudWeaponDebug.unity` 是战斗 HUD、武器图标和技能指示器验证场景，常由 `Assets/Editor/Combat/CombatHudDebugSceneInstaller.cs` 生成。
- `lianqi.unity` 是炼器工作台验证场景，常由 `Assets/Editor/ForgingWorkbenchSceneInstaller.cs` 绑定 `ForgingWorkbenchController`、配置路径和布局 preset。
- `Dongfu_Home.unity` 是洞府主场景原型，包含炼器房、外出战斗入口、洞府材质和 `SceneInteractablePlaceholder` 交互占位。
- `Shulin_L0.unity` 和 `ShulinDemoScene.unity` 是树林场景验证入口；改树林材质或切图资源后优先检查它们。

## 工作规则

- 除非改动是机械且容易检查的，否则避免手改大型 Unity YAML。
- 修改场景对象优先使用 Unity Editor 或 Unity MCP。
- 场景特定脚本放在 `Assets/Scripts/Prototype/`；可复用战斗逻辑才放到 `Battle`。
- 只有在明确要改变构建场景列表时，才更新 `ProjectSettings/EditorBuildSettings.asset`。

## 验证方式

- 修改场景后，打开目标场景，检查 Console 错误；如果改动是视觉相关的，再截图验证。
- 改安装器生成的场景时，优先重新运行对应 Editor 菜单再审查差异，避免手动维护一大段场景 YAML。
