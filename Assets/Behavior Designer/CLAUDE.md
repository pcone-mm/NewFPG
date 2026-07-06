# Behavior Designer 使用指南

这个目录是导入的 Behavior Designer 第三方插件。项目怪物 AI 会依赖它，但插件本体默认按外部资产处理。

## 目录边界

- `Runtime/`、`Editor/`、`Documentation.pdf` 等来自插件本体。
- 项目自定义怪物行为树任务放在 `Assets/Scripts/Monsters/BehaviorDesigner/`，不要放进插件目录。
- 鱼怪默认外部行为树资产在 `Assets/Settings/Monsters/BehaviorTrees/BT_Fish.asset`。

## 工作规则

- 除非任务明确是插件升级、导入修复或兼容性补丁，否则不要编辑插件源码、DLL、文档或示例。
- 新增怪物行为树节点时在 `NewFPG.Monsters.BehaviorDesigner` 下写项目代码，并保持 `TaskName`、`TaskDescription` 面向策划的中文显示。
- 更新默认行为树时优先走 `Assets/Editor/MonsterConfigEditorUtility.cs` 和 `NewFPG/Monsters/Rebuild Fish Behavior Tree`，不要手改插件内部资源。

## 验证方式

- 改自定义任务后运行 `Assets/Tests/Editor/MonsterBehaviorDesignerTasksEditorTests.cs`。
- 改默认外部行为树或 prefab 绑定后运行 `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`，确认 `Fish.prefab` 的 `BehaviorTree` 指向 `BT_Fish.asset`。
