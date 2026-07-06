# Art 使用指南

## 目录地图

- `Characters/XianxiaHero/` 放主角 spritesheet、拆分帧、源图和导入说明。
- `HUD/` 放 HUD 概念图、第一人称手势资源和战斗跳字素材；`HUD/Hit_tip/` 是 `HitTipAssetInstaller` 生成 `Resources/HitTips` 默认 catalog 的源图层。
- `SkillIndicators/Temporary/` 放技能指示器临时资源包，包含 `PF_IND_` prefab、`M_IND_` 材质、`T_IND_` 贴图、`MS_IND_` mesh、`S_IND_` 音频和 `SO_IND_TemporaryArtIndex.asset` 索引。
- `UI/ForgingPSDImport/` 放锻造界面 PSD 导出的分层 PNG、预览图和 `forge_ui_manifest.json`。
- `Weapons/Ani/` 放武器、相机和闪避动画测试资产；`bajiaoshan_*` 资源目前作为芭蕉扇攻击/待机动画素材管理。
- `Weapons/BajiaoshanFrames/` 放芭蕉扇风刃序列帧、sprite sheet、预览图和 `bajiaoshan_fanwind_frames_manifest.json`。
- `Weapons/HUD/` 放 HUD 使用的武器图标。
- `Scenes/` 下放树林场景、切图材质和 Unity 场景构建会引用的场景美术资源。

## 工作规则

- 移动或重命名任何美术资源时，保留并同步处理 `.meta` 文件。
- 源提示词、规格说明和导入说明放在对应资源家族旁边。
- 不要因为已经有处理后的运行时图片，就删除源图；源图对后续迭代有价值。
- 生成新美术时，草稿放到被忽略的根目录，例如 `tmp/` 或 `output/`；只有确认采用的运行时资源或参考资源才复制进 `Assets/Art/`。
- 技能指示器临时资源由 `Assets/Editor/SkillIndicatorTemporaryArtGenerator.cs` 生成；改资源 ID 时同步检查 `SkillIndicatorConfig`、`SkillIndicatorTemporaryArtIndex` 和 HUD debug 配置。
- 战斗跳字源图由 `Assets/Editor/Combat/HitTipAssetInstaller.cs` 统一设置 Sprite 导入参数并生成 `Assets/Resources/HitTips/`；替换背景、数字图层或命名时，同步重跑安装器和 HUD 测试。
- `UI/ForgingPSDImport/forge_ui_manifest.json` 是锻造 UI 分层还原的定位契约；替换图层、顺序或 bbox 时同步更新 manifest，保持 `asset_path` 指向有效资源。
- 芭蕉扇帧和动画现在是美术/特效候选资源；接入技能、伤害或输入逻辑前，不要把 `Docs/EffectInventory` 的拼装配方当作已实现玩法。

## 验证方式

- 如果任务依赖 pixels-per-unit、透明度、过滤模式或 sprite slicing，确认 Unity 导入设置。
- 改技能指示器临时资源后，重新生成索引并打开 `CombatHudWeaponDebug.unity` 检查预览资源是否能解析。
- 改 `HUD/Hit_tip/` 后，执行 `NewFPG/Combat/Install Hit Tip Assets`，再跑 `Assets/Tests/Editor/MonsterCombatHudEditorTests.cs`。
- 改锻造 PSD 导出层后，打开 `lianqi.unity` 检查炼器 UI 图层、材料热点和预览图一致。
- 改芭蕉扇序列帧、sprite sheet 或动画控制器后，打开引用它们的场景或 prefab 检查 sprite slicing、动画播放和 `.meta` 引用。
