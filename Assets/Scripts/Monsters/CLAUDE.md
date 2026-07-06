# Monsters 模块指南

这个目录负责怪物配置、移动、AI、技能选择和机制执行，命名空间是 `NewFPG.Monsters`。

## 职责

- `MonsterCatalog`、`MonsterDefinition` 和相关 DTO 读取 `Assets/Settings/Monsters/monster_catalog.json`。
- `MonsterCatalogAuthoring` 提供 JSON 与 ScriptableObject 镜像之间的编辑入口。
- `MonsterConfigBinding` 是当前怪物 prefab 的主要运行时组件，负责套用 catalog、移动、目标选择、技能冷却、预警和机制执行。
- `MonsterVisionUtility` 与 `MonsterVisibleNavMeshSampler` 负责相机视野、遮挡检测和可到达 A* 位置采样。
- `BehaviorDesigner/` 子目录使用 `NewFPG.Monsters.BehaviorDesigner`，只放项目自定义行为树任务、ObjectDrawer 标记和战斗区域选择辅助。
- `BattleZoneSampler` 通过 `NewFPG.Combat.BattleArenaZoneMap` 采样 `left_front`、`center_mid` 等区域 id，并把候选点投影到 A* 可达路径。
- 怪物机制包括 `damage_area`、`invincible`、`invisible`、`scale_modifier` 和 `speed_modifier`。

## 边界

- 通用生命、护盾、伤害接口、攻击预警表现和 `DamagePayload` 属于 `Assets/Scripts/Combat/`。
- 怪物调参数据属于 `Assets/Settings/Monsters/`，不要复制到 `Fish.prefab` 或写死在运行时组件里。
- JSON/ScriptableObject 同步和 prefab 绑定刷新属于 `Assets/Editor/MonsterConfigEditorUtility.cs`。
- 关卡里的怪物生成节奏和房间流程属于 `Assets/Scripts/Level/`。
- 当前怪物运行时移动走 A* Pathfinding 的 `AIPath` 与 `Seeker`，行为决策走 Behavior Designer `ExternalBehaviorTree`；不要恢复旧 `UnityEngine.AI.NavMeshAgent`、旧鱼怪控制器或把移动目的地重新塞回绑定层自动推导。
- Behavior Designer 插件本体在 `Assets/Behavior Designer/`，默认按第三方资产处理；项目任务、中文显示名和技能/区域下拉都在本目录或 `Assets/Editor/MonsterSkillIdDrawer.cs` 维护。

## 配置书写约定

- 面向策划的显示名、备注、Inspector 标题和 Tooltip 尽量使用中文。
- JSON 不能写 `//` 注释；需要说明逻辑时使用正式字段 `中文备注`，并保证 ScriptableObject 往返导入导出不会丢。
- 新增开放参数时，优先补 `[InspectorName("中文名")]` 和 `[Tooltip("中文说明")]`，让策划在 Inspector 里能直接看懂。
- 运行时绑定键不要为了中文化而改名，包括 `monsterId`、`skillId`、`mechanicId`、AI `type`、Tag、LayerMask 约定、Animator 参数名、资源路径和 prefab 路径。
- 如果确实要改绑定键，必须同步代码、Prefab、Animator、关卡表、测试和已有 JSON/SO 引用。

## 工作规则

- 新增怪物或技能时，先更新 catalog DTO 和 `monster_catalog.json`，再用 Editor 工具刷新 prefab 绑定。
- `Fish.prefab` 应保留 `MonsterConfigBinding`、`monsterId`、catalog 引用和基础 Unity 组件；不要恢复旧 `FishMonsterController`，也不要把调参字段散落回 prefab。
- 新机制类型要同时更新 `MonsterMechanicDefinition`、`MonsterMechanicTypes.Parse`、执行类和对应 Editor 测试。
- 新增 AI action 时同步更新 `MonsterAiActionDefinition`、`MonsterConfigBinding.TryStartAiAction` 和 catalog；`move_to_visible_camera_band` 依赖相机可见性、line-of-sight mask、A* 可走图和可达路径检查。
- 新增 Behavior Designer 任务时同步中文 `TaskName`/`TaskDescription`、默认值、`MonsterBehaviorTaskText` 常量和 Editor 测试；区域移动节点应复用 `BattleArenaZoneMap` 的稳定 id。

## 验证方式

- 改 catalog、authoring 或 prefab JSON 绑定后，运行 `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`。
- 改技能施放、预警、伤害区域或锁定目标逻辑后，运行 `Assets/Tests/Editor/MonsterSkillControllerEditorTests.cs`。
- 改无敌、隐身、缩放或速度等机制执行后，运行 `Assets/Tests/Editor/MonsterMechanicRunnerEditorTests.cs`。
- 改可见位置采样、line-of-sight、相机距离 band 或 AI action 默认值后，运行 `Assets/Tests/Editor/MonsterVisionAndNavMeshActionEditorTests.cs`。
- 改 Behavior Designer 任务、中文元数据、区域组枚举或行为树移动入口后，运行 `Assets/Tests/Editor/MonsterBehaviorDesignerTasksEditorTests.cs`。
- 改默认外部行为树、`behaviorTreePath` 或 Fish prefab 行为树绑定后，运行 `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`，确认 `BT_Fish.asset` 仍可反序列化。
- 手动验证时执行 `NewFPG/Monsters/Validate Monster JSON`，必要时再执行 `NewFPG/Monsters/Refresh Prefab JSON Bindings`。
