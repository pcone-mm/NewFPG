# NewFPG Agent 使用指南

## 项目地图

- `Assets/Scripts/Battle/` 放可复用的战斗领域代码，命名空间是 `NewFPG.Battle`。神器、目标选择、队列、战斗状态等不依赖场景物体的规则，优先放这里。
- `Assets/Scripts/Combat/` 放当前实时战斗原型代码，命名空间是 `NewFPG.Combat`。生命/护盾、资源、武器定义、施法、敌人攻击预警和战斗 HUD 等直接驱动场景物体的逻辑放这里。
- `Assets/Scripts/Level/` 放地牢房间流程和关卡原型代码，命名空间是 `NewFPG.Level`。房间状态机、门选择、奖励预览、敌人生成、探索/战斗相机切换和关卡 HUD 放这里。
- `Assets/Scripts/Forging/` 放炼器系统代码，命名空间是 `NewFPG.Forging`。图纸、材料、五行属性、格子形状、邻接规则、结果计算、运行时武器绑定和炼器 UI 控制放这里。
- `Assets/Scripts/Monsters/` 放怪物配置、AI、移动、技能选择和机制执行代码，命名空间是 `NewFPG.Monsters`。怪物可调数据优先来自 `Assets/Settings/Monsters/monster_catalog.json`。
- `Assets/Scripts/Prototype/` 放原型场景的胶水代码，命名空间是 `NewFPG.Prototype`。这里负责 `MonoBehaviour` 编排、运行时生成 HUD、相机跟随辅助、洞穴战斗流程串联。
- `Assets/Scripts/CZN/` 与 `Assets/Editor/CZN/` 放 CZN 角色 Spine 技能运行时和角色专用生成器；通用提取/转换工具在 `Tools/CznResourcePipeline/`，复用流程入口是 `.codex/skills/czn-character-spine-unity-import/SKILL.md`。
- `Assets/Imported/CZN/` 放 CZN 导入后的 Unity 可读资源、Metadata、预览场景和 prefab；角色/怪物载荷目录按 `.gitignore` 保持本地导入产物边界，修改前先看该目录局部指南和对应交付文档。
- `Assets/FPGDemo/` 放独立的 FPG 战斗 demo harness，含 asmdef 分层、Boot/CombatLab/FormalRoom 场景、配置、表现资源和测试；修改前先看该目录局部指南，不要把它混进 `Assets/Scripts/*` 的原型模块边界。
- `Assets/Scenes/` 放可运行的 Unity 场景。`SampleScene.unity` 是基础场景；`PrototypeCaveBattleScene.unity`、`LevelScene.unity`、`CombatHudWeaponDebug.unity`、`lianqi.unity`、`Dongfu_Home.unity` 和 `Shulin_L0.unity` 是当前主要原型/验证场景。
- `Assets/Art/` 放导入后的美术资源和源素材。角色、怪物、HUD、武器、技能指示器临时资源、锻造 UI PSD 导出层和场景美术资源保持在当前各自目录中。
- `Assets/Prefabs/` 放运行时和验证场景会实例化的 prefab。角色、怪物、第一人称原型武器视图和效果 prefab 保持在对应子目录，安装器生成的 prefab 优先通过安装器更新。
- `Assets/Resources/` 只放确实需要 `Resources.Load` 的运行时资产。当前稳定入口是 `HitTips/` 默认战斗跳字 catalog 和动画配置。
- `Assets/Rendering/` 放项目自有 URP 渲染扩展，例如 `DodgeSpeedLines` 的 RendererFeature、Volume override、控制器和 shader。
- `Assets/Materials/Prototype/` 放当前原型场景使用的材质。
- `Assets/Behavior Designer/` 是导入的第三方行为树插件；项目怪物 AI 通过自定义任务和外部行为树集成它，插件本体默认按外部资产处理。
- `Assets/Settings/`、`ProjectSettings/`、`Packages/` 是 Unity 管理的配置。只有明确涉及包、渲染管线、构建或项目设置时才修改；`Packages/com.arongranberg.astar/` 是本地嵌入第三方包，默认按外部包处理。
- `Assets/ThirdParty/` 放导入的插件和 vendored 资源；优先视为外部资产，samples/demo 内容默认不纳入项目上下文，只有明确集成或迁移任务才改。
- `Docs/` 放工程说明、资源盘点和可重跑报告。大型 CSV/Markdown 清单先看同目录 README，再决定是否读取明细。

## 当前技术栈

- Unity `6000.3.15f1`。
- 已安装并配置 Universal Render Pipeline。
- 通过 `Assets/InputSystem_Actions.inputactions` 使用 Input System。
- 已安装 Unity AI Navigation；工作树中还有本地嵌入的 A* Pathfinding Project `5.4.6` 包。
- 怪物 AI 当前使用 Behavior Designer 外部行为树，并通过 A* `AIPath`/`Seeker` 执行移动。
- CZN Spine 导入依赖本地 `com.esotericsoftware.spine.spine-unity` 3.8 包，`Packages/manifest.json` 指向 `External/CZN/SpineRuntime-3.8`；该运行时和提取载荷是 local-only，换机或干净检出后需要重建。
- 通过 `com.coplaydev.unity-mcp` 接入 Unity MCP 包，并通过 `com.besty.unity-skills` 支持 Unity Editor 自动化。

## 工作规则

- Unity 自动化、脚本、场景、Prefab、材质、UI、性能等任务开始前，先确认项目包和可用 Unity 自动化入口，再按任务主题读取对应局部指南或 Unity skill 说明。
- **验收与测试统一分工（项目级规则，优先于任何目录的局部验证建议）：** 试玩、手感、视觉可读性、交互可用性、难度公平性、主管判断和需要人工体验的性能感受，一律由主管/用户验收。Agent 在开发中只记录这些项目，默认跳过执行、跳过代判，也不控制 Unity/桌面进行人工试玩；交付时必须给出可填写的验收表（测试项、前置条件、操作、通过标准、证据栏、状态），状态标为“待主管试玩/确认”。只有用户明确要求时，才执行此类试玩或人工判断测试。
- **自动化测试的默认范围：** 除非用户明确要求，禁止为一次功能开发新增测试文件、测试场景或批量运行 EditMode/PlayMode 测试。代码和资源变更只做最小必要的静态检查、已打开 Unity 的编译/Console 检查或明确指定的单项技术校验；不要为了“看起来覆盖充分”扩张测试代码或长时间操控用户电脑。任何未执行的验收项必须如实列入交接表，不能表述为已通过。
- **策划配置与配置文档（项目级交付要求）：** 制作任何包含数值、行为、流程、表现或内容组合的功能时，先判断哪些业务决策应由策划在不改代码的前提下调整，并优先接入已有的配置入口；不要为同一功能另建平行配置链。面向策划的字段须按工作流分组，提供中文显示名、默认值、单位、取值范围、前置条件和实际生效结果；工程容量、LayerMask、物理/命中盒、内部状态和其他技术实现细节仍默认隔离。新增或修改策划配置时，必须同步新增或更新配置说明文档，写明配置入口/资产位置、依赖关系、创建与安装步骤、字段完整说明、示例和验收方式；没有配置文档即不视为该配置交付完成。若一个功能明确不开放策划配置，交付时必须说明原因。
- 处理 CZN/卡厄斯梦境角色模型、动画、特效、Spine 或技能组合时，先读取 `.codex/skills/czn-character-spine-unity-import/SKILL.md` 和它指定的流程；不要把海德玛丽专用生成器当作已泛化的一键导入器。
- 中等以上设计或实现任务先套用 `Docs/Workflow/UnknownsMethodology.md`：回答涉及系统、现有参照和技术暗坑；写代码前说明验证方式；影响玩家可见行为的决策必须问用户，不要猜。
- 更新飞书 Docx 时，Windows PowerShell 必须将 `$OutputEncoding` 和 Console 输出设为 UTF-8（无 BOM），通过 `--content -` 传入 XML；写后立即重新 fetch 并扫描 `?{2,}`。默认 native 管道曾把中文不可逆替换成问号，禁止直接依赖默认编码。
- 面向策划开放 Unity Inspector 字段时，保留 C# 字段名和已有 YAML 键；通过显示层提供准确的中文名称与中文说明。说明必须基于实际生效逻辑写清所有权、单位、取值约束、条件和生效路径，不能逐词直译或把未实现能力包装成可配置功能。配置说明文档按 `Docs/Workflow/Planner_Configuration_Delivery_Guide.zh-CN.md` 交付。技术容量、LayerMask、物理/命中盒与运行时状态默认不向策划开放。
- 保留 Unity `.meta` 文件，并让它始终跟随对应资源。移动资源时，同步移动匹配的 `.meta` 文件。
- 除非用户明确要求真正重整 Unity 资源目录，否则避免大规模搬动资源。场景和 prefab 引用稳定性比目录名好看更重要。
- 生成物和探索性输出不要进入日常上下文：`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`output/`、`tmp/`、`TestResults/`、`.workbuddy/`、`Assets/Screenshots/` 和根目录 `wp*_patch_*.xml`，除非任务明确要求读取生成物、测试结果、截图或日志。
- 搜索时先在最可能相关的小目录里查，再考虑读取大型 Unity YAML 文件。
- 编辑 C# 脚本时，命名空间要和目录边界一致：神器领域规则用 `NewFPG.Battle`，实时战斗组件用 `NewFPG.Combat`，关卡流程用 `NewFPG.Level`，炼器系统用 `NewFPG.Forging`，怪物配置和 AI 用 `NewFPG.Monsters`，原型场景胶水用 `NewFPG.Prototype`，CZN Spine 运行时用 `NewFPG.CZN`，CZN Editor 生成器用 `NewFPG.CZN.Editor`。
- 不要把 Unity 项目设置、包版本、渲染管线资源、构建设置当作顺手清理项一起改。

## 验证方式

- 修改 C# 或 Unity 资源后，只做最小必要的静态检查；若 Unity 已打开，可等待编译完成并检查 Console 错误。不要为此主动展开 PlayMode、试玩、截图或批量测试。
- 场景、相机、HUD、手感和表现类需求的人工验收，使用 `Docs/Workflow/Testing_Handoff_Policy.zh-CN.md` 的交接表，留给主管/用户完成。
- 只改文档或目录指南时，检查 `git diff --check` 并人工确认新增指南内容。
