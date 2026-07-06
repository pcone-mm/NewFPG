# Level 模块指南

这个目录负责地牢房间流程和关卡原型，命名空间是 `NewFPG.Level`。

## 职责

- 路线、房间、门、奖励池、事件选择、刷怪表和流程状态的可序列化定义。
- `LevelRouteTable` 和 `LevelEncounterTable` 的路线/刷怪配置，默认资产在 `Assets/Settings/Level/`。
- `LevelEncounterResolver` 的预设组随机、随机池抽取和刷怪请求解析。
- `LevelFlowDirector` 的房间状态机、第一层先选项后战斗规则、按 encounter 生成敌人、探索/战斗相机切换和下一房间选择。
- `LevelFlowDirector` 负责激活当前 `BattleArenaZoneMap` 并用它辅助 A* 图预览范围；区域采样规则本身仍在 `Assets/Scripts/Combat/` 和 `Assets/Scripts/Monsters/`。
- 关卡 HUD、房间交互、投射物和关卡敌人生命反馈。
- 关卡设计说明放在同目录 Markdown，和实现保持同步。

## 边界

- 通用生命/护盾、武器资源、施法、鱼怪攻击等实时战斗组件属于 `Assets/Scripts/Combat/`。
- 第一人称武器视图、相机跟随等原型表现胶水仍属于 `Assets/Scripts/Prototype/`。
- 路线和 encounter 数据已经有 ScriptableObject 表资产；改字段结构时同步检查 `Assets/Settings/Level/` 资产、`LevelEncounterTableEditor` 和场景里 `LevelFlowDirector` 的绑定。
- 改战斗区域图绑定时，同时检查 `LevelFlowDirector`、`BattleArenaZoneMapEditor` 和怪物行为树区域移动节点，不要只改场景对象引用。

## 验证方式

- 修改流程后检查 Unity Console，并优先跑 `LevelFlowDirectorEditorTests` 等相关 Editor 测试。
- 改路线表、刷怪表、权重抽取或波次生成后，运行 `Assets/Tests/Editor/LevelEncounterResolverEditorTests.cs` 和 `Assets/Tests/Editor/LevelFlowDirectorEditorTests.cs`。
- 改房间进入、选项、敌人生成、相机或门选择时，在目标场景里走一遍进入房间、选择、战斗、结算、选门流程。
- 手改场景或 prefab 引用前，优先考虑 Unity Editor、安装脚本或 Unity MCP；大型 Unity YAML 只做可审查的小范围改动。
