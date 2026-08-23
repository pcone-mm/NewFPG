# HUD 表现指南

本目录保存正式战斗 HUD 与屏幕/世界空间反馈资产，不拥有战斗判定或遭遇状态。

- `Prefabs/PF_FPG_OverheadHealthBar.prefab` 与 `Prefabs/PF_FPG_DamagePopup.prefab` 是正式血条和伤害跳字入口；实例生命周期与容量由 FormalRoom 的 presentation profile、pool 和 presenter 管理。
- `HitTip/` 保存命中提示使用的正式贴图；`T_FPG_ChargeProgressRing.asset` 保存蓄力进度环表现资源。不要把这些输入复制进角色 Entity、技能配置或场景临时副本。
- HUD prefab 可暴露 UI port、布局和本地动画，不得保存生命值、伤害结果、敌人选择或其他权威 gameplay 状态。
- Scene 与配置对 HUD prefab 的引用通过 GUID 维护；移动、重命名或替换时必须保留 `.meta` 并同步路径合同测试。
- 修改后检查 Unity 编译/Console、`FormalHudGeometryTests.cs`、`FormalCombatPresentationStreamTests.cs` 及对应 presentation profile 合同。
