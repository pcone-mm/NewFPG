# Forging 模块指南

这个目录负责炼器系统，命名空间是 `NewFPG.Forging`。

## 职责

- 炼器图纸、材料、五行属性、格子形状、邻接规则和武器加成模型。
- `ForgingCalculator` 的放置合法性、旋转格子、属性汇总、邻接规则和技能数值结算。
- `ForgingCatalogLoader` 与 catalog JSON DTO，默认读取 `Assets/Settings/Forging/weapon_blueprints.json` 和 `Assets/Settings/Forging/materials.json`。
- `ForgingWeaponFactory` 把炼器结果绑定到 Combat `WeaponDefinition`、HUD 图标和 `SkillIndicatorConfig`。
- `ForgingWorkbenchController` 管理 `lianqi.unity` 的运行时炼器 UI、拖拽、旋转、结果面板和布局 preset。

## 边界

- 战斗施法、命中、冷却执行和技能指示器渲染属于 `Assets/Scripts/Combat/`；Forging 只生成或填充运行时武器定义。
- 图纸、材料和 UI 布局配置属于 `Assets/Settings/Forging/`，不要把可调数据硬编码进计算器。
- Editor 菜单、Inspector 网格和 JSON/ScriptableObject 同步工具属于 `Assets/Editor/` 的 `NewFPG.EditorTools`。
- 需要兼容 `weapon_blueprints.json`、`materials.json` 和旧 `forging_catalog.json` 时，优先扩展 loader/DTO，不要在 UI 控制器里分叉解析逻辑。

## 验证方式

- 改模型、catalog、计算器、旋转/邻接规则或武器绑定后，运行 `Assets/Tests/Editor/ForgingSystemEditorTests.cs`。
- 改工作台 UI 后，打开 `lianqi.unity`，必要时执行 `NewFPG/Forging/Install Lianqi Workbench`，检查 Console、材料拖拽、旋转和炼制结果。
- 改 Combat 绑定路径后，同时检查 `Assets/Settings/Forging/Weapons/WPN_*`、HUD 图标和 `Assets/Settings/Combat/HudDebug/IND_HUD_Debug_*` 是否仍可解析。
