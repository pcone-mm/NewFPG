# Art 使用指南

## 目录地图

- `Characters/XianxiaHero/` 放主角 spritesheet、拆分帧、源图和导入说明。
- `HUD/` 放 HUD 概念图和第一人称手势资源。
- `SkillIndicators/Temporary/` 放技能指示器临时资源包，包含 `PF_IND_` prefab、`M_IND_` 材质、`T_IND_` 贴图、`MS_IND_` mesh、`S_IND_` 音频和 `SO_IND_TemporaryArtIndex.asset` 索引。
- `UI/ForgingPSDImport/` 放锻造界面 PSD 导出的分层 PNG、预览图和 `forge_ui_manifest.json`。
- `Weapons/HUD/` 放 HUD 使用的武器图标。
- `Scenes/` 下放树林场景、切图材质和 Unity 场景构建会引用的场景美术资源。

## 工作规则

- 移动或重命名任何美术资源时，保留并同步处理 `.meta` 文件。
- 源提示词、规格说明和导入说明放在对应资源家族旁边。
- 不要因为已经有处理后的运行时图片，就删除源图；源图对后续迭代有价值。
- 生成新美术时，草稿放到被忽略的根目录，例如 `tmp/` 或 `output/`；只有确认采用的运行时资源或参考资源才复制进 `Assets/Art/`。
- 技能指示器临时资源由 `Assets/Editor/SkillIndicatorTemporaryArtGenerator.cs` 生成；改资源 ID 时同步检查 `SkillIndicatorConfig`、`SkillIndicatorTemporaryArtIndex` 和 HUD debug 配置。
- `UI/ForgingPSDImport/forge_ui_manifest.json` 是锻造 UI 分层还原的定位契约；替换图层、顺序或 bbox 时同步更新 manifest，保持 `asset_path` 指向有效资源。

## 验证方式

- 如果任务依赖 pixels-per-unit、透明度、过滤模式或 sprite slicing，确认 Unity 导入设置。
- 改技能指示器临时资源后，重新生成索引并打开 `CombatHudWeaponDebug.unity` 检查预览资源是否能解析。
- 改锻造 PSD 导出层后，打开 `lianqi.unity` 检查炼器 UI 图层、材料热点和预览图一致。
