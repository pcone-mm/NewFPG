# Scripts 使用指南

## 目录边界

- `Battle/` 是纯战斗领域逻辑。尽量不要依赖场景层级和具体场景物体。
- `Combat/` 是实时战斗原型组件。它可以引用 `MonoBehaviour`、prefab、动画、碰撞和 HUD，但不要承载关卡房间状态机。
- `Level/` 是地牢房间流程和关卡原型。它负责房间数据、门选择、敌人生成、战斗/探索切换和关卡 HUD。
- `Prototype/` 是面向场景的原型代码。它可以创建 UI、引用相机、移动场景对象，并把 `Battle` 代码接入当前可玩的原型。

## 约定

- `Battle/` 下的文件使用 `NewFPG.Battle`。
- `Combat/` 下的文件使用 `NewFPG.Combat`。
- `Level/` 下的文件使用 `NewFPG.Level`。
- `Prototype/` 下的文件使用 `NewFPG.Prototype`。
- 只有多个系统共享的可序列化领域类型才放进 `BattleContracts.cs`。如果某个类型只服务于一个系统，尽量放在对应系统附近。
- 关卡房间、门、奖励池、流程状态等关卡结构类型放在 `Level/` 附近；通用生命、伤害、资源和武器施法组件放在 `Combat/` 附近。
- 在稳定 prefab 或 UI 资源工作流形成前，运行时 UI 生成逻辑先留在原型代码里。
- 除非项目已经需要编译隔离或测试边界，否则不要新增 assembly definition。

## 验证方式

- 修改脚本后，等待 Unity 编译完成并检查 Console 错误。
- 如果暂时无法使用 Unity，至少检查命名空间、类名，并搜索被重命名符号的引用再结束。
