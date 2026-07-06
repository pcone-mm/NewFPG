# Resources 使用指南

这个目录只放运行时必须通过 `Resources.Load` 查找的资产。普通美术、配置或临时输出不要顺手放进来。

## 目录边界

- `HitTips/` 放战斗跳字运行时默认资源。`SO_HTC_Default.asset` 是 `HitTipCatalog`，`SO_HTA_Default.asset` 是默认动画配置。

## 工作规则

- `HitTips/` 资产由 `Assets/Editor/Combat/HitTipAssetInstaller.cs` 从 `Assets/Art/HUD/Hit_tip/` 安装或更新。
- 改 `HitTips/` 文件名、路径或 catalog 类型时，同步检查 `MonsterCombatHud` 中的 `Resources.Load("HitTips/SO_HTC_Default")`。
- 保留并同步 `.meta` 文件，避免 runtime load 资产或 sprite 引用断开。
- 不要把 `Resources/` 当作临时缓存、截图输出或大型第三方资源仓库。

## 验证方式

- 改战斗跳字资源后，在 Unity 里执行 `NewFPG/Combat/Install Hit Tip Assets`。
- 运行 `Assets/Tests/Editor/MonsterCombatHudEditorTests.cs`，确认默认 `HitTipCatalog` 三种样式都能加载完整背景和数字图层。
