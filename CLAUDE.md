# NewFPG Agent 使用指南

## 项目大图

- `Assets/FPGDemo/` 是唯一正式游戏主线，运行入口固定为 `Boot -> FormalRoom`；代码使用 `FPG.Demo.*` 命名空间。进入该目录后先读它的局部指南。
- `Assets/InitTestScene/` 只保存 Editor/Development 用的 `BattleTest` harness；它以 additive 方式复用 `FormalRoom`，不得进入生产场景列表、Release 构建或成为第二条正式主线。
- `Assets/FPGDemo/Runtime/` 保存纯领域程序集和 Unity 适配层；`Config/`、`Presentation/`、`Scenes/`、`Editor/`、`Tests/`、`Audio/` 分别保存正式配置、表现资产、场景、编辑工具、合同测试和音频映射资源。更细边界由各子目录 `CLAUDE.md` 定义。
- `Assets/Imported/CZN/` 与 `Assets/FPGDemo/SourceArt/CZN/` 保存 CZN/Spine 输入，`External/CZN/SpineRuntime-3.8/` 是项目内 vendored Spine 运行时，`Tools/CznResourcePipeline/` 与 `.codex/skills/czn-character-spine-unity-import/` 保存可复用导入流程。
- `Tools/ForestAudio/` 是离线只读的 Soundminer 索引与 WAV 候选生成工具；它不属于 Unity 运行时入口，进入后先读该目录局部指南。
- `Prototypes/FpgBuildSandbox/` 是与 Unity 正式主线隔离的 Web 构筑沙盒，只使用测试数据验证玩法与界面；进入后先读该目录局部指南。
- `Assets/Art/`、`Assets/Materials/`、`Assets/Resources/`、`Assets/Rendering/`、`Assets/Settings/`、`Assets/ThirdParty/`、`Assets/URP_SSR/`、`Assets/ParadoxNotion/`、`Assets/VFX_Klaus/` 和 `Assets/Feel/` 是保留的源资源、渲染配置或外部资产，不是正式运行入口；复用前必须先确认 `Assets/FPGDemo/` 中存在真实引用和所有权边界。
- `Assets/Editor/` 保存项目级启动与设置工具；正式 FPG authoring 仍位于 `Assets/FPGDemo/Editor/`。修改自动启动行为前先读顶层 Editor 局部指南。

## 技术栈

- Unity `6000.3.15f1`，Universal Render Pipeline `17.3.0`，Input System `1.19.0`，Unity Test Framework `1.6.0`。
- 项目 Fixed Timestep 固定为 `1/60`，与 `FPG.Skills` 的 60Hz tick 合同一致；修改 `TimeManager.asset` 时同步检查 `GameBootstrap` 与 `FpgSkillClockConfigurationTests.cs`。
- `FPG.Core`、`FPG.Combat`、`FPG.Player`、`FPG.Enemy`、`FPG.Skills` 与 `FPG.Run` 是无 UnityEngine 依赖的领域程序集；`FPG.Unity` 负责场景、输入、物理和表现适配。
- `Packages/manifest.json` 通过本地路径引用 spine-unity `3.8`，并安装 Unity MCP 与 Unity Skills。
- More Mountains Feel `6.0` vendored 在 `Assets/Feel/`；正式接入只放在 `Assets/FPGDemo/Integrations/Feel/`，不得让 `FPG.Unity` 或纯领域程序集直接依赖插件 API。
- Web 构筑沙盒使用 TypeScript `5.7`、Vite `6`、Three.js `0.170`、Vitest `2.1` 与 Playwright `1.49`，依赖和验证均留在其独立目录内。
- Behavior Designer、嵌入式 A* 和 Unity AI Navigation 已从正式依赖中移除；没有明确任务和新的架构决定时不要重新引入。

## 写代码逻辑必读

凡涉及功能开发、向旧功能接入新功能、重构或缺陷修复，开始修改代码前必须先按本节审视设计与错误处理策略；局部指南可以补充更具体的约束，但不得弱化本节原则。

### 设计原则

- **面向业务抽象**：抽象应围绕真实业务概念、职责边界和变化方向展开；在满足当前需求的前提下保留必要扩展性，禁止脱离业务场景的过度抽象。
- **兼容现有架构**：向旧功能接入新功能前，先评估本轮修改对原有职责边界、依赖关系和扩展路径的影响；优先复用既有机制并沿现有扩展点接入，必要时调整不再适配的结构，确保新旧逻辑自然协同。
- **避免无谓兼容**：除非明确要求适配旧版系统，否则无需保留旧版兼容路径，也不要针对未提出的场景进行过度的防御性编程。
- **保持流程连贯与可读**：严禁为拆分而拆分；除非逻辑具有极高的通用复用价值，否则应保持业务流程连贯，不要将完整流程切成一串仅调用一次的微型私有方法；保证逻辑在当前方法内一眼看到底，减少跨方法频繁跳转造成的思维中断。

### 稳定性与性能

#### 错误处理与防御性编程

- **按系统属性选择策略**：根据系统层级、错误影响范围和数据重要性决定是否采用防御性编程，禁止对所有场景统一兜底。
- **底层系统优先断言**：底层或核心系统应尽可能使用断言暴露前置条件、状态约束和不变量被破坏的问题；触发断言代表系统存在明确错误，应修复根因，禁止通过兜底让错误状态继续运行。
- **业务边缘允许降级**：涉及策划配置的业务逻辑中，对非关键、枝叶性的配置采用防御性编程和表现降级；例如未配置可选特效时仅跳过播放，不得因此导致系统崩溃。
- **核心配置禁止兜底**：整个系统运行所必需的 `Setting` 等核心配置缺失属于严重错误，应通过断言或显式失败及时暴露，禁止为其缺失设计静默兜底。

## 全局规则

- 不得从正式 `FPG.Demo.*` 代码重新依赖已删除的 `NewFPG.*` 原型、旧场景、旧 Host、CombatLab 或 D0 运行入口。序列化资产仍保留的 D0 前缀类型只视为兼容合同。
- 修改 asmdef、稳定 ID、容量、tick、hash 或跨程序集合同前，先检查依赖闭包和对应局部指南；确定性与 fail-closed 行为优先于隐式回退。
- 正式业务配置以 `Assets/FPGDemo/Config/` 为真源，不建立平行配置链；字段所有权、范围和读取时机由对应 Config/Editor 局部指南、Inspector 与合同测试共同约束。
- 处理 CZN/Spine 模型、动画、特效或导入资源前，先读 `.codex/skills/czn-character-spine-unity-import/SKILL.md`；保留 Git LFS 属性、原始证据、生成报告和每个资源的 `.meta`。
- Scene、Prefab、Build Settings 和 Unity 资源引用通过 Unity Editor、Unity MCP 或现有 installer 修改，不批量手改 YAML。移动资源时让 `.meta` 始终跟随资源。
- 人工试玩、视觉、手感、难度和交互验收交给主管/用户；未执行项必须明确标为待确认，不得以静态检查或自动测试替代主观结论。
- 不维护独立 Workflow 长文：稳定规则放最近作用域的 `CLAUDE.md`，重复专家流程放 skill/script/hook，资产交付证据跟随资产目录的 README/Metadata；不得建立与代码、配置或测试平行的文档真源。
- 除非用户明确要求，不新增测试文件、不批量运行 EditMode/PlayMode。代码变更只做最小静态检查、现有 Unity 编译/Console 检查或明确指定的精确测试。
- 生成物和探索输出默认不进入上下文：`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`output/`、`tmp/`、`TestResults/`、`.workbuddy/`、根目录 `.tmp*`、`Assets/Screenshots/` 和根目录 `wp*_patch_*.xml`。
- 不要把 `ProjectSettings/`、包版本、渲染管线或第三方资产当作顺手清理项；搜索先从最可能相关的小目录开始。

## 验证原则

- 只改文档或目录指南时运行 `git diff --check`，并检查 `Assets/**/CLAUDE.md` 与相邻 `.meta` 配对。
- 修改 C# 或 Unity 资源时先确认 Unity 编译和 Console，再按局部指南选择最小合同验证；没有执行的测试不得表述为已通过。
- 修改正式入口、依赖闭包、配置真源或资源 GUID 时，优先使用现有 Build Settings、assembly boundary、authoring 与 asset contract 检查，不自行扩张测试范围。
