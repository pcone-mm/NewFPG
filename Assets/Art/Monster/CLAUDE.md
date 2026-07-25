# Monster Art 指南

这个目录保存根项目遗留的怪物美术输入，不是 FPGDemo 正式运行时或表现 prefab 入口。

## 目录边界

- `Fish/`、`Materials/` 与 `wolf.png` 是当前仍保留的资源；不要因为正式主线迁移而顺手移动或删除。
- 陆鸾、蝴蝶等 CZN 正式源输入位于 `Assets/FPGDemo/SourceArt/CZN/`，运行时 Spine 依赖与 Entity prefab 位于 `Assets/FPGDemo/Presentation/`；本目录不再承载它们。

## 工作规则

- 移动或重命名怪物美术资产时同步处理 `.meta`，优先保持现有引用稳定。
- 将保留资源接入 FPGDemo 前，先建立 `SourceArt`、`Presentation` 与 `Config` 的明确派生关系；不要让根 Art 资源承担命中、投射物、召唤或伤害逻辑。

## 验证方式

- 改 `Fish/`、`Materials/` 或 `wolf.png` 后，先检查实际引用和 Unity 导入设置；没有正式引用时不要把存在本身当成主线验证入口。
- 只改指南时运行 `git diff --check`。
