# Art 指南

本目录保存源美术、遗留候选资源和试验内容，不是正式 `FPG.Demo` 运行入口。

## 目录地图

- `Characters/`、`HUD/`、`Weapons/`、`Effect/`、`SkillIndicators/`、`UI/` 和 `Scenes/` 保存各资源家族的源文件或旧候选。
- `Monster/` 保存怪物源资源；CZN/Spine 内容先读其局部指南，正式输入与派生分别以 `Assets/Imported/CZN/`、`Assets/FPGDemo/SourceArt/CZN/` 和 `Assets/FPGDemo/Presentation/` 为准。
- `Weapons/tongqianjian/` 有独立重建边界，进入前先读其局部指南。
- `References/` 与 `场景（试验）/` 只作参考和试验，不得作为正式场景或配置真源。

## 工作规则

- 移动或重命名美术资源时保留 `.meta`；源图、提示词、规格和导入说明保留在对应资源家族旁。
- 草稿和探索输出放入被忽略的 `tmp/` 或 `output/`；确认采用后再进入 `Assets/Art/`。
- 接入正式主线前先做 GUID 引用审计，并把正式派生放入 `Assets/FPGDemo/SourceArt/`、`Presentation/` 或 `Config/` 的对应边界。
- `Docs/EffectInventory` 的拼装配方和旧报告只是候选线索，不代表 gameplay、场景或验证入口仍然存在。

## 验证

- 资源任务按需检查 pixels-per-unit、透明度、过滤、slicing、动画播放和 `.meta`。
- 正式采用后的视觉检查必须打开当前真实引用它的 FPG prefab/scene；不要使用已删除的 CombatHud、Shulin、lianqi 或 Dongfu 场景。
