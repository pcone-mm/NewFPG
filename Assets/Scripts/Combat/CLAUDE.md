# Combat 模块指南

这个目录负责当前实时战斗原型组件，命名空间是 `NewFPG.Combat`。

## 职责

- 生命、护盾、受击、死亡和资源池组件。
- 武器定义、玩家施法、冷却、消耗和命中结算。
- 敌人攻击请求、攻击预警和伤害投递。
- 临时战斗 HUD、玩家受击反馈与第一人称武器视图的衔接。

## 边界

- 房间流程、门选择、奖励预览、敌人生成节奏和相机状态切换属于 `Assets/Scripts/Level/`。
- 旧神器自动释放、目标选择和不依赖场景物体的领域规则仍属于 `Assets/Scripts/Battle/`。
- 只服务某个 prefab 的引用假设要在对应组件序列化字段或安装脚本附近保持清晰，不要写进全局规则。

## 已实现反馈

- 玩家受击反馈在 `PlayerHitFeedback.cs`，挂载点是带有 `CombatVitals` 的玩家对象。
- `LevelFlowDirector.ResolveReferences()` 会在玩家已有 `CombatVitals` 且缺少反馈组件时运行时补上 `PlayerHitFeedback`。
- `Assets/Editor/Combat/CombatFoundationInstaller.cs` 会给 Player prefab 和当前场景 Player 添加并绑定 `PlayerHitFeedback`。
- `PlayerHitFeedback` 监听 `CombatVitals.Damaged`，触发活动游戏相机短暂震动和运行时 `PlayerHitFeedbackCanvas` 的屏幕红边闪烁。
- 若活动游戏相机带 `CinemachineBrain`，受击震动走 `CinemachineImpulseSource` 和 `CinemachineExternalImpulseListener`，避免 Cinemachine 覆盖手动相机位移；没有 Cinemachine 输出相机时才回退到本地位移震动。
- 相关测试在 `Assets/Tests/Editor/PlayerHitFeedbackEditorTests.cs`。

## 验证方式

- 修改后等待 Unity 编译并检查 Console。
- 改生命、伤害、施法、敌人攻击、玩家受击反馈或 HUD 时，优先跑相关 Editor 测试。
- 没有自动覆盖时，在场景里验证玩家武器、鱼怪攻击预警、受击反馈和资源/血条刷新。
- 改 prefab 绑定时同步检查对应 `.meta` 和序列化引用，不要手动大范围重写 prefab YAML。
