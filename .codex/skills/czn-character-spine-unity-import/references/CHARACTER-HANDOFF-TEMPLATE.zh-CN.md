# <角色名>（<CharacterId>）Spine / Unity 使用指南

> 本文记录可复现的本地学习流程。说明原游戏资源与 Spine Runtime 的授权和分发边界。

## 一分钟开始

- Unity 技能预览场景：`Assets/Imported/CZN/<Label>/Preview/<Label>_SkillPreview.unity`
- Unity 模型预览场景：`Assets/Imported/CZN/<Label>/Preview/<Label>_Preview.unity`
- 主模型 Spine 工程：`External/CZN/<Label>/SpineProjects/<Label>_Main.spine`
- BattleReady Spine 工程：`External/CZN/<Label>/SpineProjects/<Label>_BattleReady.spine`
- 操作：Play、技能切换键、重播键、暂停键。

## 本次恢复范围

| 项目 | 数量/状态 |
|---|---|
| 审计记录 | `<count>` |
| SpineSource / AncillarySource | `<count>` |
| 转换成功 | `<passed>/<total>` |
| 动画 / Timeline records | `<count>` / `<count>` |
| SkeletonDataAsset / Atlas | `<count>` / `<count>` |
| SkillSequence / Timeline | `<count>` / `<count>` |
| Spine / 粒子 / Transform cues | `<count>` / `<count>` / `<count>` |

## 目录和入口

列出 Prefab、场景、SkillSequence、Timeline、SpineSource、Configs、Metadata、External 原始副本和 Editor 重建菜单。

## 动画与技能表

列出主模型、BattleReady 动画，以及每个技能的角色 phase、Spine/粒子层、时长、目标锚点和关键事件。

## 在 Spine 中预览

说明打开哪个 `.spine`、如何导入任意 canonical JSON、Atlas 解包路径和 `images` 目录要求。强调不要直接打开/改名 SCSP1U，也不要用 Spine 反导覆盖 canonical JSON。

## 在 Unity 中预览和复用

说明如何打开场景、拖入 Prefab、使用 `CznSpineSkillSequence`/Timeline，以及如何执行角色专用的幂等生成菜单。

## 当前精度边界

- 缺失的共享粒子贴图：`<list>`
- mask / custom shader / blend：`<status>`
- camera / node / bezier：`<status>`
- post-process / hit-stop：`<status>`
- unresolved：`<count and report path>`

## 验证结果

- 提取 hash、Atlas/PNG、SCSP1U 结构；
- Unity 资产加载与动画计数；
- 每技能边界采样；
- 代表技能 PlayMode 截图/数值；
- 同技能重播、末态重播、Timeline 自动回绕各 3 次；
- 运行时层/对象数不增长；
- Unity Console 错误/警告计数。

## 报告索引

列出 import manifest、转换、Spine CLI、Unity integration、skill composition、resource map 和 unresolved 报告的准确路径。
