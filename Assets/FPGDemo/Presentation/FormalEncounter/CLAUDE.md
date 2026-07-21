# FPGDemo Formal Encounter 表现指南

本目录放正式遭遇 installer 生成或维护的实体、血条、出口 prefab 和配套材质。

## 资源边界

- 当前稳定入口是 `PF_FPG_BurstbugEntity`、`PF_FPG_HudieEntity`、`PF_FPG_LuanEntity`、`PF_FPG_OverheadHealthBar`、`PF_FPG_RoomExit` 和 `Materials/`。
- 这些 prefab 是 FPGDemo 的绑定层，不是源美术目录；不要直接绑定 `Assets/Art/Monster` 中的原始 PMA Spine 资源。
- 保留 prefab 名称和 installer 使用的稳定路径；改名或移动时同步修改 installer 与场景合同。
- 表现 prefab 可以暴露 binder、socket、hitbox 和 UI port，但不承载战斗规则或遭遇状态机。

## 验证

- 资源由对应 installer 刷新后检查 Console 与生成结果。
- prefab 引用或场景绑定变化时，运行 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
