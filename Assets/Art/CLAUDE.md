# Art 使用指南

## 目录地图

- `Characters/XianxiaHero/` 放主角 spritesheet、拆分帧、源图和导入说明。
- `Monster/` 放怪物美术源资源和导入后的模型资产；CZN/Spine 怪物源资源先看该目录局部指南，不要直接当作 D0 可发布表现 prefab。
- `HUD/` 放 HUD 概念图、第一人称手势资源和战斗跳字素材；`HUD/Hit_tip/` 是当前 `Resources/HitTips` 默认 catalog 的源图层。
- `SkillIndicators/Temporary/` 放技能指示器临时资源包，包含 `PF_IND_` prefab、`M_IND_` 材质、`T_IND_` 贴图、`MS_IND_` mesh、`S_IND_` 音频和 `SO_IND_TemporaryArtIndex.asset` 索引。
- `UI/ForgingPSDImport/` 放锻造界面 PSD 导出的分层 PNG、预览图和 `forge_ui_manifest.json`。
- `Weapons/Ani/` 放武器、相机和闪避动画测试资产；`bajiaoshan_*` 资源目前作为芭蕉扇攻击/待机动画素材管理。
- `Weapons/BajiaoshanFrames/` 放芭蕉扇风刃序列帧、sprite sheet、预览图和 `bajiaoshan_fanwind_frames_manifest.json`。
- `Weapons/HUD/` 放 HUD 使用的武器图标和第一人称战斗 HUD 底座源图；`2d_di.png` 与 `2d_dou.png` 当前被 HUD 脚本作为默认路径使用。
- `Weapons/tongqianjian/` 放通钱剑浮空演示的源贴图、材质、shader、动画和一键重建脚本；开始前先看子目录指南，不要把源素材、现有 `Materials/` 与重建后的 floating example 混放。
- `Scenes/` 下放树林场景、切图材质和 Unity 场景构建会引用的场景美术资源。

## 工作规则

- 移动或重命名任何美术资源时，保留并同步处理 `.meta` 文件。
- 源提示词、规格说明和导入说明放在对应资源家族旁边。
- 不要因为已经有处理后的运行时图片，就删除源图；源图对后续迭代有价值。
- 生成新美术时，草稿放到被忽略的根目录，例如 `tmp/` 或 `output/`；只有确认采用的运行时资源或参考资源才复制进 `Assets/Art/`。
- 技能指示器临时资源由 `Assets/Editor/SkillIndicatorTemporaryArtGenerator.cs` 生成；改资源 ID 时同步检查 `SkillIndicatorConfig`、`SkillIndicatorTemporaryArtIndex` 和 HUD debug 配置。
- 替换战斗跳字背景、数字图层或命名时，直接更新对应 Sprite 导入设置和 `Assets/Resources/HitTips/` catalog，并检查 HUD 引用。
- 改 `Weapons/HUD/2d_*.png` 或武器 HUD 图标命名时，同步检查 `PrototypeWeaponCombatHud` 和引用它们的 prefab/scene。
- `UI/ForgingPSDImport/forge_ui_manifest.json` 是锻造 UI 分层还原的定位契约；替换图层、顺序或 bbox 时同步更新 manifest，保持 `asset_path` 指向有效资源。
- 芭蕉扇帧和动画现在是美术/特效候选资源；接入技能、伤害或输入逻辑前，不要把 `Docs/EffectInventory` 的拼装配方当作已实现玩法。

## 验证方式

- 如果任务依赖 pixels-per-unit、透明度、过滤模式或 sprite slicing，确认 Unity 导入设置。
- 改技能指示器临时资源后，重新生成索引并打开 `CombatHudWeaponDebug.unity` 检查预览资源是否能解析。
- 改 `HUD/Hit_tip/` 后，检查 `Assets/Resources/HitTips/` 默认 catalog 与 Sprite 引用。
- 改锻造 PSD 导出层后，打开 `lianqi.unity` 检查炼器 UI 图层、材料热点和预览图一致。
- 改 `Weapons/HUD/` 后，打开 `CombatHudWeaponDebug.unity` 或 `Shulin_L0.unity` 检查底部 HUD、资源豆和武器图标引用。
- 改芭蕉扇序列帧、sprite sheet 或动画控制器后，打开引用它们的场景或 prefab 检查 sprite slicing、动画播放和 `.meta` 引用。
