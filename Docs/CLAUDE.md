# Docs 指南

本目录保存工程说明和可重跑报告；一次性探索输出继续放 `tmp/` 或 `output/`。

## 目录边界

- `EffectInventory/` 是生成式特效资源盘点；实际覆盖范围以 `Generate-EffectInventory.ps1` 当前输入根为准，不以旧 README/CSV 中残留的路径为准。
- `EffectInventory/Generate-EffectInventory.ps1` 是只读生成器；重跑会更新同目录 CSV/Markdown 报告，不修改 `Assets` 资源本体。

## 工作规则

- 重跑 EffectInventory 前先对照当前 `Assets/VFX_Klaus/`、`Assets/ThirdParty/`、`Assets/Art/` 与 `Assets/Rendering/` 审计脚本输入根；生成器、README、CSV 和 Markdown 必须一起更新。
- 大型清单先读同目录 README，再按需打开明细；报告可能落后于资源迁移，不能单独证明当前引用或授权。
- 不把日报、临时任务记录或未确认设计写进这里；重复专家流程优先沉淀为脚本、hook 或 skill。

## 验证

- 改 EffectInventory 生成器后重跑并抽查 README/CSV/Markdown 与当前输入根一致；只改指南时运行 `git diff --check`。
