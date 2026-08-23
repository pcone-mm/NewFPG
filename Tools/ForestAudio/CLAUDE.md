# Forest Audio Tools 指南

本目录是离线、只读的音频索引与 WAV 候选生成工具，不是 Unity runtime 或资产导入入口。

- `index_soundminer.py`、`inspect_gungeon_soundbank.py` 只读取外部库/索引并输出可审计元数据；`forest_audio_wav.py`、`prepare_auditions.py` 在工作目录生成带 hash 的候选、manifest、CSV 和试听列表。源 WAV 与 Soundminer 数据库不得被改写。
- 运行前显式指定工作根、索引和 preset；输出放在项目外。Unity 只消费经人工批准、符合 48 kHz/24-bit 和声道约定的导出，不直接读取这些脚本的中间产物。
- `__pycache__/`、临时修复源和 audition 输出是生成物，应保持在日常上下文和提交之外；需要复现时保留 source hash、处理参数和报告。

详细参数以脚本 `--help`、`Assets/FPGDemo/Audio/SoundminerWorkflow.md` 和 `Assets/FPGDemo/Audio/ForestAudioCompletionPlan.md` 为准；项目级 CLAUDE 只保留上述边界。
