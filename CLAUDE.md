# NewFPG Agent 使用指南

## 项目地图

- `Assets/Scripts/Battle/` 放可复用的战斗领域代码，命名空间是 `NewFPG.Battle`。神器、目标选择、队列、战斗状态等不依赖场景物体的规则，优先放这里。
- `Assets/Scripts/Combat/` 放当前实时战斗原型代码，命名空间是 `NewFPG.Combat`。生命/护盾、资源、武器定义、施法、敌人攻击预警和战斗 HUD 等直接驱动场景物体的逻辑放这里。
- `Assets/Scripts/Level/` 放地牢房间流程和关卡原型代码，命名空间是 `NewFPG.Level`。房间状态机、门选择、奖励预览、敌人生成、探索/战斗相机切换和关卡 HUD 放这里。
- `Assets/Scripts/Forging/` 放炼器系统代码，命名空间是 `NewFPG.Forging`。图纸、材料、五行属性、格子形状、邻接规则、结果计算、运行时武器绑定和炼器 UI 控制放这里。
- `Assets/Scripts/Monsters/` 放怪物配置、AI、移动、技能选择和机制执行代码，命名空间是 `NewFPG.Monsters`。怪物可调数据优先来自 `Assets/Settings/Monsters/monster_catalog.json`。
- `Assets/Scripts/Prototype/` 放原型场景的胶水代码，命名空间是 `NewFPG.Prototype`。这里负责 `MonoBehaviour` 编排、运行时生成 HUD、相机跟随辅助、洞穴战斗流程串联。
- `Assets/Scenes/` 放可运行的 Unity 场景。`SampleScene.unity` 是基础场景；`PrototypeCaveBattleScene.unity`、`LevelScene.unity`、`CombatHudWeaponDebug.unity`、`lianqi.unity`、`Dongfu_Home.unity` 和 `Shulin_L0.unity` 是当前主要原型/验证场景。
- `Assets/Art/` 放导入后的美术资源和源素材。角色、HUD、武器、技能指示器临时资源、锻造 UI PSD 导出层和场景美术资源保持在当前各自目录中。
- `Assets/Prefabs/` 放运行时和验证场景会实例化的 prefab。角色、怪物、第一人称原型武器视图和效果 prefab 保持在对应子目录，安装器生成的 prefab 优先通过安装器更新。
- `Assets/Resources/` 只放确实需要 `Resources.Load` 的运行时资产。当前稳定入口是 `HitTips/` 默认战斗跳字 catalog 和动画配置。
- `Assets/Rendering/` 放项目自有 URP 渲染扩展，例如 `DodgeSpeedLines` 的 RendererFeature、Volume override、控制器和 shader。
- `Assets/Materials/Prototype/` 放当前原型场景使用的材质。
- `Assets/Behavior Designer/` 是导入的第三方行为树插件；项目怪物 AI 通过自定义任务和外部行为树集成它，插件本体默认按外部资产处理。
- `Assets/Screenshots/` 放视觉验证截图。把它当作参考证据，不要当作运行时依赖。
- `Assets/Settings/`、`ProjectSettings/`、`Packages/` 是 Unity 管理的配置。只有明确涉及包、渲染管线、构建或项目设置时才修改；`Packages/com.arongranberg.astar/` 是本地嵌入第三方包，默认按外部包处理。
- `Assets/ThirdParty/` 放导入的插件、samples 和 vendored 资源；优先视为外部资产，只有明确集成或迁移任务才改。
- `Docs/` 放工程说明、资源盘点和可重跑报告。大型 CSV/Markdown 清单先看同目录 README，再决定是否读取明细。

## 当前技术栈

- Unity `6000.3.15f1`。
- 已安装并配置 Universal Render Pipeline。
- 通过 `Assets/InputSystem_Actions.inputactions` 使用 Input System。
- 已安装 Unity AI Navigation；工作树中还有本地嵌入的 A* Pathfinding Project `5.4.6` 包。
- 怪物 AI 当前使用 Behavior Designer 外部行为树，并通过 A* `AIPath`/`Seeker` 执行移动。
- 通过 `com.coplaydev.unity-mcp` 接入 Unity MCP 包，并通过 `com.besty.unity-skills` 支持 Unity Editor 自动化。

## 工作规则

- Unity 自动化、脚本、场景、Prefab、材质、UI、性能等任务开始前，先确认项目包和可用 Unity 自动化入口，再按任务主题读取对应局部指南或 Unity skill 说明。
- 保留 Unity `.meta` 文件，并让它始终跟随对应资源。移动资源时，同步移动匹配的 `.meta` 文件。
- 除非用户明确要求真正重整 Unity 资源目录，否则避免大规模搬动资源。场景和 prefab 引用稳定性比目录名好看更重要。
- 生成物和探索性输出不要进入日常上下文：`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`output/`、`tmp/`，除非任务明确要求读取生成物或日志。
- 搜索时先在最可能相关的小目录里查，再考虑读取大型 Unity YAML 文件。
- 编辑 C# 脚本时，命名空间要和目录边界一致：神器领域规则用 `NewFPG.Battle`，实时战斗组件用 `NewFPG.Combat`，关卡流程用 `NewFPG.Level`，炼器系统用 `NewFPG.Forging`，怪物配置和 AI 用 `NewFPG.Monsters`，原型场景胶水用 `NewFPG.Prototype`。
- 不要把 Unity 项目设置、包版本、渲染管线资源、构建设置当作顺手清理项一起改。

## 验证方式

- 修改 C# 或 Unity 资源后，打开 Unity 或使用 Unity MCP，等待编译完成，再检查 Console 错误。
- 修改场景、相机或 HUD 后，截图并和预期视觉状态对比。
- 只改文档或目录指南时，检查 `git diff --check` 并人工确认新增指南内容。
