# Scenes 使用指南

## 场景角色

- `SampleScene.unity` 是当前已跟踪的基础场景。
- `PrototypeCaveBattleScene.unity` 是原型洞穴战斗场景。

## 工作规则

- 除非改动是机械且容易检查的，否则避免手改大型 Unity YAML。
- 修改场景对象优先使用 Unity Editor 或 Unity MCP。
- 场景特定脚本放在 `Assets/Scripts/Prototype/`；可复用战斗逻辑才放到 `Battle`。
- 只有在明确要改变构建场景列表时，才更新 `ProjectSettings/EditorBuildSettings.asset`。

## 验证方式

- 修改场景后，打开目标场景，检查 Console 错误；如果改动是视觉相关的，再截图验证。
