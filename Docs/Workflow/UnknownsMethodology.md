# NewFPG 未知数工作流

这份流程把《AI 方法论相关》里的“未知数方法论”落到 NewFPG 项目里。核心目标很简单：在代码、场景、资产和策划方案变贵之前，用更便宜的动作把未知数挖出来。

## 总纲

- 提示词、任务描述和口头想法是“地图”；NewFPG 的真实代码、场景绑定、资产引用、玩法口味和验证成本是“领土”。
- 最危险的未知数有两类：一类是你脑子里的隐性标准，例如“符文效果要读起来牛逼，不要只是 +5%”；另一类是你压根不知道的技术暗坑，例如状态机、序列化、prefab 引用、A* 可达性、Behavior Designer 绑定和运行时资源加载。
- 设计讨论时，用户是项目和品味的领土专家，AI 要把用户脑子里的标准挖出来。
- Vibe coding 时，AI 是代码和引擎的领土专家，AI 要先把技术暗坑、现有参照和验证方式讲清楚。
- 对看不懂代码的协作方式，质量关卡放在两端：动手前理解和跑起来后的表现；中间实现阶段用保守默认、照抄现有、决策记录圈住风险。

## 每个任务先过的三问

任何 C#、Unity 资源、场景、prefab、配置 JSON、战斗/关卡/怪物/炼器改动开始前，先回答：

1. 这件事会碰到哪几个系统？
2. 项目里有没有现成类似功能可以照抄？
3. 有哪些策划视角容易想不到的坑？

答案要具体到 NewFPG 现有边界，而不是泛泛说“战斗系统”：

| 方向 | 优先检查 |
| --- | --- |
| 纯战斗领域规则 | `Assets/Scripts/Battle/` |
| 实时战斗表现、生命、伤害、资源、武器、HUD、跳字 | `Assets/Scripts/Combat/`、`Assets/Settings/Combat/`、`CombatHudWeaponDebug.unity` |
| 房间状态、门选择、刷怪、关卡 HUD、探索/战斗切换 | `Assets/Scripts/Level/`、`Assets/Settings/Level/`、`Shulin_L0.unity` |
| 怪物配置、AI、技能、机制、行为树 | `Assets/Scripts/Monsters/`、`Assets/Settings/Monsters/monster_catalog.json`、`Assets/Settings/Monsters/BehaviorTrees/` |
| 炼器、图纸、材料、五行、运行时武器生成 | `Assets/Scripts/Forging/`、`Assets/Settings/Forging/`、`lianqi.unity` |
| 场景胶水、临时 HUD、第一人称武器视图、相机辅助 | `Assets/Scripts/Prototype/`、`Assets/Prefabs/Prototype/` |
| Editor 安装器、迁移工具、配置刷新 | `Assets/Editor/` |

## 设计讨论协议

用于符文、武器流派、怪物机制、房间奖励、炼器规则、关卡节奏等还没进入代码的设计。

1. 反向盲点扫描：先列“如果我不知道答案，方案就大概率错”的问题，例如目标玩家、和现有系统关系、期望拉动的体验、不能碰的边界。
2. 访谈一次一个问题：优先问会改变方案骨架的问题，不问可以后补的细节数值。
3. 原型先行：不要先写抽象框架，先给能触发皱眉的东西。例如 10 条最终玩家文案、4 个方向迥异的机制方案、一个假数据 UI 草图。
4. 参考物成对：正例说明“要像什么”，反例说明“不要像什么”。只给正例时，要主动追问反例。
5. 骨架决策前置：成稿前先单列最可能返工的 3 个决策，例如乘区/加区、效果走 Buff 还是 WeaponModifier、怪物机制走 catalog 还是 prefab 字段。
6. 攻击测验：方案完成后，从边界情况、系统冲突、玩家滥用、极限堆叠、可验证性五个角度审一轮。

## Vibe Coding 协议

用于已经要改项目的任务。

### 动手前

- 做盲点扫描，明确涉及系统、现有参照、技术暗坑。
- 主动解释关键技术词。比如状态机就是 `LevelFlowDirector` 这种“当前处在哪个阶段、只允许做对应动作”的流程控制；序列化就是 Unity 把 Inspector 字段写进 `.asset`、`.prefab`、`.unity` 文件。
- 先谈验证方式：做完后用户在游戏里怎么操作，看到什么算成功，哪些操作最容易暴露问题。
- 影响玩家可见行为的决策必须停下来问，不猜。比如技能是否可叠加、打断后是否退款、切图是否保留 buff、怪物失去目标后去哪里。

### 动手中

- 能照抄现有功能就照抄。优先读同目录代码、同类测试、安装器和配置资产。
- 从零发明的新机制要显式标注风险，并说明为什么不能复用现有路径。
- 没说清的纯技术决策选保守默认：少改公共接口，少动 prefab YAML，少搬资产，不引入新依赖，不改包版本。
- 记录偏离原计划的决定，尤其是“为什么选这个、不选另一个”。

### 收尾

- 跑对应测试或说明为什么不能跑。
- 对视觉/手感/场景改动，进入目标场景做截图或手动验证。
- 用非技术语言回顾：改了哪几个地方、什么时候会坏、以后要改成另一个方向该动哪里。

## 原型层级

优先选择能最便宜暴露问题的层级。

| 层级 | 用法 | 适合 |
| --- | --- | --- |
| 文案/假数据原型 | 只写最终文案、表格、HTML 草图或静态 UI | 玩法方案、HUD 信息层级、奖励/符文口味 |
| 独立验证场景 | 在 `CombatHudWeaponDebug.unity`、`lianqi.unity` 或新建临时测试场景验证最小效果 | 战斗 HUD、武器视图、炼器 UI、单个表现效果 |
| 单系统接入 | 只接 `Combat`、`Level`、`Monsters` 等一个边界 | 技能命中、怪物机制、房间流程 |
| 正式串联 | 接入目标场景、prefab、配置资产和测试 | 已确认方向的功能 |

如果用户直接要完整功能，先提醒：“这一步跳过了小原型，风险是把口味或表现错误带进正式系统。建议先做一层最小可见验证；明确要跳过也可以。”

## 必须停下来问的情况

- 会改变玩家能看到、听到、操作到的行为。
- 会改变配置数据结构、JSON/SO 字段、运行时绑定键、资源路径或 prefab 引用。
- 会改变 `LevelFlowDirector` 状态推进、门/房间选择、战斗结束时机。
- 会改变怪物 AI 的目标选择、移动方式、可见性、无敌/隐身/速度/体型等机制语义。
- 会改变武器释放策略、冷却、消耗、命中范围、是否自动选目标、是否能打多目标。
- 会引入新包、改 ProjectSettings、改渲染管线、改输入资产。

## 常用现有参照

- 关卡流程：`LevelFlowDirector`、`LevelRouteTable`、`LevelEncounterTable`、`LevelFlowDirectorEditorTests`。
- 武器释放与命中：`WeaponDefinition`、`WeaponRuntimeData`、`PlayerWeaponCaster`、`WeaponCastHitResolver`、`WeaponRuntimeSystemEditorTests`。
- 技能指示器：`Assets/Scripts/Combat/SkillIndicators/`、`SkillIndicatorSystemEditorTests`、`CombatHudWeaponDebug.unity`。
- 怪物机制：`MonsterMechanicRunner`、`MonsterConfigBinding`、`MonsterMechanicRunnerEditorTests`。
- 怪物 AI 和移动：`MonsterVisionUtility`、`MonsterVisibleNavMeshSampler`、`BattleZoneSampler`、`MonsterVisionAndNavMeshActionEditorTests`。
- HUD 和反馈：`MonsterCombatHud`、`DamageNumberView`、`PlayerHitFeedback`、`MonsterCombatHudEditorTests`、`PlayerHitFeedbackEditorTests`。
- 炼器：`ForgingCalculator`、`ForgingWeaponFactory`、`ForgingSystemEditorTests`、`ForgingWorkbenchSceneInstaller`。
- 第一人称武器视图：`PrototypeFirstPersonWeaponView`、`FirstPersonWeaponLayoutProfile`、`PrototypeFirstPersonWeaponViewPreviewTests`。

## 验证矩阵

| 改动范围 | 优先验证 |
| --- | --- |
| Level 流程、房间、门、encounter | `Assets/Tests/Editor/LevelFlowDirectorEditorTests.cs`、`LevelEncounterResolverEditorTests.cs`，再进 `Shulin_L0.unity` 走一遍 |
| 武器 runtime、施法、命中 | `Assets/Tests/Editor/WeaponRuntimeSystemEditorTests.cs` |
| 技能指示器 | `Assets/Tests/Editor/SkillIndicatorSystemEditorTests.cs`，再看 `CombatHudWeaponDebug.unity` |
| 怪物 catalog / prefab 绑定 | `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`，必要时执行 Monster 菜单校验 |
| 怪物技能/机制 | `MonsterSkillControllerEditorTests.cs`、`MonsterMechanicRunnerEditorTests.cs` |
| 怪物视野/A* 采样/行为树任务 | `MonsterVisionAndNavMeshActionEditorTests.cs`、`MonsterBehaviorDesignerTasksEditorTests.cs` |
| HUD、跳字、血条、受击反馈 | `MonsterCombatHudEditorTests.cs`、`PlayerHitFeedbackEditorTests.cs`，再做视觉验证 |
| 炼器 | `ForgingSystemEditorTests.cs`，再打开 `lianqi.unity` |
| 第一人称武器视图/布局 | `PrototypeFirstPersonWeaponViewPreviewTests.cs`，再在目标场景检查视图 |
| 只改文档 | `git diff --check` |

## 决策记录规则

较大改动或需求仍模糊时，在任务过程中维护一份 implementation notes。可以不提交临时 notes，但最终回答必须覆盖这些内容：

- 任务目标和玩家可见成功标准。
- 动了哪些系统。
- 采用了哪个现有参照。
- 哪些地方选择了保守默认。
- 哪些玩家可见决策问过用户，或明确说明用户要求跳过。
- 验证结果和剩余风险。

模板见 `Docs/Workflow/implementation-notes-template.md`。
