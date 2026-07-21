# Docs 使用指南

这个目录放工程说明、资源盘点、可重跑报告和面向协作的参考材料。不要把一次性探索输出放这里；临时导出继续放 `tmp/` 或 `output/`。

## 目录边界

- `EffectInventory/` 是特效资源盘点，覆盖 `Assets/ThirdParty/`、`Assets/Art/`、`Assets/Prefabs/` 和 `Assets/Rendering/` 下的可复用 VFX、材质、shader、贴图、demo scene、sprite sheet 和包归档。
- `EffectInventory/Generate-EffectInventory.ps1` 是只读生成器；重跑会更新同目录 CSV/Markdown 报告，不修改 `Assets` 里的特效本体。
- `Workflow/` 是协作流程文档，包含未知数方法论、implementation notes 模板、CZN 角色 Spine/Unity 导入手册、D0 策划配置说明和验收交接文档。它用于指导任务执行，不是一次性任务日志。

## 工作规则

- 大型清单先读同目录 `README_CN.md` 或 `README.md`，再按需要打开 CSV/Markdown 明细。
- 更新盘点时保持生成器、README、CSV 和 Markdown 报告彼此一致。
- 不要把长日报、临时任务记录或未确认的设计猜测沉淀到这里；稳定流程优先写成可重跑脚本或局部 CLAUDE 指南。
- 更新 `Workflow/` 时写成可复用规则或模板，避免记录只服务于单次对话的临时决策。
- D0 配置说明文档必须保持工程入口、策划字段、验证方式和`待主管试玩/确认`状态一致；不要把 Agent 未执行的试玩项写成已通过。

## 验证方式

- 改 `EffectInventory` 生成器后，在项目根目录运行 `pwsh -File Docs/EffectInventory/Generate-EffectInventory.ps1`，再抽查 README 和 CSV 是否同步更新。
- 只改文档时运行 `git diff --check`。
