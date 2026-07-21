# Monster Art 指南

这个目录放怪物美术源资源和导入后的模型资产。它是美术输入边界，不是战斗运行时或可发布表现 prefab 的默认入口。

## 目录边界

- `Fish/` 与 `Materials/` 保留现有怪物资源和材质，不要因新增 CZN 资源顺手重排。
- `luan/` 与 `hudie/` 放陆鸾、蝴蝶的 Spine 源贴图、atlas、json 和 Unity 生成的 atlas/material/SkeletonData 资产。
- D0 CombatLab 使用的派生 straight-alpha 资源、展示 prefab 和配置资产放在 `Assets/FPGDemo/Presentation/` 与 `Assets/FPGDemo/Config/D0Slice/`，不要直接从这里拖原始 PMA 资源进场景或配置。

## 工作规则

- 移动或重命名怪物美术资产时同步处理 `.meta`，优先保持现有引用稳定。
- CZN/Spine 怪物资源重导时，先确认来源目录、PMA/straight-alpha 转换目标和目标 prefab 位置，不要让源资源承担战斗命中、投射物或伤害逻辑。
- `luan`/`hudie` 的召唤与出现动画、视觉时序由 `Docs/Workflow/Luan_Summons_Hudie_Configuration.zh-CN.md` 和 `D0LuanSummonHudieDefinition` 约束；普通攻击归具体攻击定义，待机、受击、Break、死亡归 Actor 状态表现。资源没有可靠发射事件时，不要用 Spine event 替代 D0 投射物链路。

## 验证方式

- 改 Spine 源资源后，确认 Unity 重新导入出的 atlas、material 和 SkeletonData 引用完整。
- 改 `luan/` 或 `hudie/` 后，同步检查各自完整 Entity Prefab 与嵌套 Generated Render Prefab 的 Spine、材质、Atlas 和 SkeletonData 引用；制作边界见 `Assets/FPGDemo/Docs/Workflow/D0_Entity_Prefab_Authoring.zh-CN.md`。源资源和 Generated Prefab 不得保存 gameplay、hitbox 或 Socket 配置。
- 只改指南时运行 `git diff --check`。