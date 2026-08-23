# FPGDemo Audio 指南

本目录是正式音频资源与映射配置边界，不承载 Soundminer 源库或战斗决策。

- `ForestAudioRequirements.csv` 是事件、来源 hash、命名、审批状态和最终资源的状态真源；`ForestAudioCompletionPlan.md` 只记录当前审批队列。未批准候选留在 Unity 项目外，只有批准后的 WAV 才能进入 `Forest/SFX/`。
- 资源命名遵循 `SFX_<role>_<action>_<variant>`、`AMB_<area>_<layer>`、`MUS_<area>_<state>`；每个 WAV 与 `.meta` 必须成对保留。Soundminer 搜索和非破坏编辑通过 `Tools/ForestAudio/` 离线完成，不修改源文件或源数据库。
- `ForestCombatAudioBank.asset` 维护稳定 `CombatAudioCue`、SFX/UI bus、空间参数、并发/冷却和变体；`ForestAudioProfile.asset` 维护房间音乐、stinger、ambience bed、空间点声与淡入淡出。映射阶段的空 clip 是允许的中间状态；宣称播放就绪或交付前必须显式通过 `TryValidatePlayback`，因为当前 presenter/director 的 `TryPrepare` 只做 mapping 校验，实际 cue/state 缺 clip 时会拒绝播放，不得用占位音频掩盖缺口。
- `FPG_AudioMixer.mixer` 是正式音频 bus 真源；`FormalRoom/AudioRoot` 通过 Runtime/Unity 的 coordinator/presenter 消费已提交事件。音频只能影响表现，不能改变 combat trace、tick、hash 或结果。

验证优先使用 Unity 编译/Console、`CombatAudioCueRoutingTests.cs`、`CombatAudioBankTests.cs`、`CombatAudioPresenterTests.cs`、`FpgFormalSkillPresentationV3AssetTests.cs` 和 `FpgSkillPresentationRuntimeTests.cs`。详细 Soundminer 输入、导出格式和审批步骤留在本目录资产文档与离线脚本，不复制进更高层指南。
