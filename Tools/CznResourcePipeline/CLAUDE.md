# CZN Resource Pipeline 指南

这个目录放 CZN SSRC 记录提取、SCT/SCSP 解包、SCSP1U 转 Spine 3.8 JSON 和导入校验脚本。它是离线只读工具，不启动、不注入、不写回游戏客户端。

## 职责

- `audit_ssra_character.py` 辅助生成角色候选记录；完整依赖闭包仍需要人工审计后形成 `complete_records.json`。
- `extract_character.py`、`extract_monsters.py` 负责从已审计记录提取 Unity 可读资源和 local-only 原始副本。
- `scsp1u_to_spine.py`、`probe_scsp1u.py`、`emit_spine_animations.py` 负责私有 SCSP1U 到 canonical Spine JSON 的转换。
- `validate_import.py` 负责 hash、atlas/PNG、SCSP1U marker 和配置文件一致性校验。

## 工作规则

- 运行前先读 `.codex/skills/czn-character-spine-unity-import/SKILL.md` 和 `references/WORKFLOW.zh-CN.md`；不要直接套用 Heidemarie 的默认参数。
- 提取命令必须显式传入 `--records`、`--label`、`--character-id`、`--external-root` 和 `--unity-root`，避免把 30093 样本路径误用于新角色。
- `complete_records.json` 是可复现提取的证据；缺它时先做依赖审计，不要按名字猜资源。
- `__pycache__/` 和临时审计输出不应进入日常上下文或版本控制。
- 遇到未知 SCT/SCSP/SCSP1U 结构时让脚本失败并保留样本，不要生成占位图片或伪 Spine 数据。

## 验证方式

- 安装依赖用 `python -m pip install -r Tools/CznResourcePipeline/requirements.txt`。
- 大批量提取先跑 `--dry-run` 并审阅输出计划，再去掉 dry run。
- 每次提取后运行 `validate_import.py`，每次转换后检查 `spine-json-conversion-report.json` 和未解析结构。
- 转换后的 JSON 是 canonical 输出；不要用 Spine Editor 保存/反导结果覆盖它。
