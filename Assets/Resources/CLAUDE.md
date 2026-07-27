# Resources 指南

本目录只保留必须位于 Unity `Resources/` 下的插件配置或遗留运行时资产；正式 FPG 配置和表现不在这里建立新入口。

## 目录边界

- `DOTweenSettings.asset` 是 DOTween 插件配置，不能作为普通 gameplay 配置移动或改名。
- `HitTips/` 中两个资产仍序列化为已删除的 `NewFPG.Combat` 类型；当前 `FPG.Demo` 主线没有对应 consumer 或合同测试，只把它们视为遗留资源。

## 工作规则

- 新的正式配置放入 `Assets/FPGDemo/Config/`，正式表现资源放入 `Assets/FPGDemo/Presentation/`；没有现存 `Resources.Load` 调用证据时不要继续扩张本目录。
- 移动、改名或删除资产前先反查加载路径、序列化类型和 GUID，并让 `.meta` 始终跟随资源。
- 资产能够被 Unity 导入不等于仍能被正式主线加载；不得恢复已删除的 `MonsterCombatHud` 或旧测试入口来证明可用。

## 验证

- 变更 `HitTips/` 前搜索当前 `Resources.Load` consumer，并检查资产是否出现 missing script/reference。
- 修改插件配置或遗留资源后检查 Unity 编译/Console；只改本指南时运行 `git diff --check`。
