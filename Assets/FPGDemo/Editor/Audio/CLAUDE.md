# Audio Editor 指南

`FpgForestAudioApprovalBinder` 是显式菜单工具，只把已批准的 Forest WAV 组写入 Fei Primary 的技能/impact presentation；它不搜索、编辑或回写 Soundminer 源库。

- 运行 binder 前确认 `Assets/FPGDemo/Audio/Forest/SFX/` 的完整变体组和 `ForestAudioRequirements.csv` 的批准状态；使用 `SerializedObject`、Undo 与 AssetDatabase，失败或 Console 出现异常时停止，不宣称资产已绑定。
- 音频的格式、hash、来源记录和候选生成由 `Tools/ForestAudio/` 及 Audio 目录资产本地文档负责；`__pycache__/`、audition 输出和工作副本不进入 Unity 资产或提交。
- 绑定后的技能仍须通过 schema V3 的 audio track/impact 校验；不要在 Editor 建立第二套事件 ID、时间轴或音频配置。

验证：Unity 编译/Console、`CombatAudioBankTests.cs`、`CombatAudioPresenterTests.cs`、`FpgFormalSkillPresentationV3AssetTests.cs`、`FpgSkillPresentationRuntimeTests.cs`；若 binder 报 managed-reference 或缺 clip 错误，先修复绑定前置条件再保存。
