# Prefabs 使用指南

这个目录保存运行时和验证场景会实例化的 prefab。保留 `.meta`，并优先用对应安装器或 Unity Editor API 更新序列化引用。

## 目录边界

- `Characters/` 放玩家和角色行走 prefab、控制器及相关动画资源。
- `Monster/` 放怪物 prefab 和它们自己的动画/控制脚本资源；怪物数值和技能调参优先放 `Assets/Settings/Monsters/monster_catalog.json`。
- `Prototype/` 放第一人称原型武器视图和原型材质；`FirstPersonWeaponView.prefab` 应保持对 `Assets/Settings/Prototype/FirstPersonWeaponHudLayout.asset` 的 layout profile 引用。
- 可复用视觉效果 prefab 只有在实际恢复目录和引用链后再建立专用子目录；不要为临时验证重新创建旧 `Effects/` 目录。

## 工作规则

- 不要手动大范围重写 prefab YAML；能通过 Unity Editor、PrefabUtility 或安装器更新时优先走工具。
- 怪物 prefab 的 JSON 绑定由 `Assets/Editor/MonsterConfigEditorUtility.cs` 刷新；不要把 `MonsterSkillController`、`MonsterMechanicRunner`、`MonsterBrain` 或旧 Fish 控制器调参副本重新塞回 `Fish.prefab`。
- 改 `Prototype/FirstPersonWeaponView.prefab` 时优先通过 Inspector、`PrototypeFirstPersonWeaponViewEditor.cs` 或安装器维护 layout profile、HUD art 和组件绑定，不要把旧 `weapons` 列表当作唯一真源。
- prefab 绑定 Combat、Level、Forging 或 Prototype 脚本时，按脚本所在目录的 CLAUDE 指南确认命名空间和验证方式。

## 验证方式

- 改角色、怪物或原型 prefab 后，打开引用它的场景并检查 Console。
- 改怪物 prefab 绑定后，运行 `Assets/Tests/Editor/MonsterJsonConfigSourceEditorTests.cs`，确认 `Fish.prefab` 仍由 `MonsterConfigBinding` 加 `monster_catalog.json` 驱动。
- 改 `Prototype/FirstPersonWeaponView.prefab` 后，运行 `Assets/Tests/Editor/PrototypeFirstPersonWeaponViewPreviewTests.cs`，再打开 `CombatHudWeaponDebug.unity` 或 `Shulin_L0.unity` 检查第一人称武器 HUD。
