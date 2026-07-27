# NewFPG Agent 使用指南

## 项目大图

- `Assets/FPGDemo/` 是唯一正式游戏主线，运行入口固定为 `Boot -> FormalRoom`；代码使用 `FPG.Demo.*` 命名空间。进入该目录后先读它的局部指南。
- `Assets/FPGDemo/Runtime/` 保存纯领域程序集和 Unity 适配层；`Config/`、`Presentation/`、`Scenes/`、`Editor/`、`Tests/` 分别保存正式配置、表现资产、场景、编辑工具和合同测试。更细边界由各子目录 `CLAUDE.md` 定义。
- `Assets/Imported/CZN/` 与 `Assets/FPGDemo/SourceArt/CZN/` 保存 CZN/Spine 输入，`External/CZN/SpineRuntime-3.8/` 是项目内 vendored Spine 运行时，`Tools/CznResourcePipeline/` 与 `.codex/skills/czn-character-spine-unity-import/` 保存可复用导入流程。
- `Assets/Art/`、`Assets/Materials/`、`Assets/Resources/`、`Assets/Rendering/`、`Assets/Settings/`、`Assets/ThirdParty/` 和 `Assets/VFX_Klaus/` 是保留的源资源、渲染配置或外部资产，不是正式运行入口；复用前必须先确认 `Assets/FPGDemo/` 中存在真实引用和所有权边界。
- `Docs/Workflow/` 保存项目级协作规则；正式 FPG 配置与运行合同说明位于 `Assets/FPGDemo/Docs/Workflow/`。

## 技术栈

- Unity `6000.3.15f1`，Universal Render Pipeline `17.3.0`，Input System `1.19.0`，Unity Test Framework `1.6.0`。
- 项目 Fixed Timestep 固定为 `1/60`，与 `FPG.Skills` 的 60Hz tick 合同一致；修改 `TimeManager.asset` 时同步检查 `GameBootstrap` 与 `FpgSkillClockConfigurationTests.cs`。
- `FPG.Core`、`FPG.Combat`、`FPG.Player`、`FPG.Enemy`、`FPG.Skills` 与 `FPG.Run` 是无 UnityEngine 依赖的领域程序集；`FPG.Unity` 负责场景、输入、物理和表现适配。
- `Packages/manifest.json` 通过本地路径引用 spine-unity `3.8`，并安装 Unity MCP 与 Unity Skills。
- Behavior Designer、嵌入式 A* 和 Unity AI Navigation 已从正式依赖中移除；没有明确任务和新的架构决定时不要重新引入。

## 全局规则

- 不得从正式 `FPG.Demo.*` 代码重新依赖已删除的 `NewFPG.*` 原型、旧场景、旧 Host、CombatLab 或 D0 运行入口。序列化资产仍保留的 D0 前缀类型只视为兼容合同。
- 修改 asmdef、稳定 ID、容量、tick、hash 或跨程序集合同前，先检查依赖闭包和对应局部指南；确定性与 fail-closed 行为优先于隐式回退。
- 正式业务配置以 `Assets/FPGDemo/Config/` 为真源，不建立平行配置链。策划字段与说明按 `Docs/Workflow/Planner_Configuration_Delivery_Guide.zh-CN.md` 维护。
- 处理 CZN/Spine 模型、动画、特效或导入资源前，先读 `.codex/skills/czn-character-spine-unity-import/SKILL.md`；保留 Git LFS 属性、原始证据、生成报告和每个资源的 `.meta`。
- Scene、Prefab、Build Settings 和 Unity 资源引用通过 Unity Editor、Unity MCP 或现有 installer 修改，不批量手改 YAML。移动资源时让 `.meta` 始终跟随资源。
- 中等以上设计或实现任务使用 `Docs/Workflow/UnknownsMethodology.md` 的问题拆解框架；其中目录、场景和测试示例必须再以当前 `Assets/FPGDemo/` 局部指南为准，不得据此恢复已删除的 NewFPG 原型。
- 人工试玩、视觉、手感、难度和交互验收按 `Docs/Workflow/Testing_Handoff_Policy.zh-CN.md` 交给主管/用户；未执行项必须明确标为待确认。
- 除非用户明确要求，不新增测试文件、不批量运行 EditMode/PlayMode。代码变更只做最小静态检查、现有 Unity 编译/Console 检查或明确指定的精确测试。
- 生成物和探索输出默认不进入上下文：`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`output/`、`tmp/`、`TestResults/`、`.workbuddy/`、根目录 `.tmp*`、`Assets/Screenshots/` 和根目录 `wp*_patch_*.xml`。
- 不要把 `ProjectSettings/`、包版本、渲染管线或第三方资产当作顺手清理项；搜索先从最可能相关的小目录开始。

## 验证原则

- 只改文档或目录指南时运行 `git diff --check`，并检查 `Assets/**/CLAUDE.md` 与相邻 `.meta` 配对。
- 修改 C# 或 Unity 资源时先确认 Unity 编译和 Console，再按局部指南选择最小合同验证；没有执行的测试不得表述为已通过。
- 修改正式入口、依赖闭包、配置真源或资源 GUID 时，优先使用现有 Build Settings、assembly boundary、authoring 与 asset contract 检查，不自行扩张测试范围。
