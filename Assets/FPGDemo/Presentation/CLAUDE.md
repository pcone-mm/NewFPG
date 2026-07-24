# Presentation 指南

本目录是 FPGDemo 正式表现资产边界，不承载遭遇决策或长期 gameplay 状态。

- `FormalEncounter/` 保存人工维护的正式 `PF_FPG_*Entity`、HUD、出口和反馈 prefab。
- `Characters/*/Spine` 保存正式 prefab 使用的 SkeletonData、Atlas、材质、贴图及必要渲染 prefab。
- `HUD/HitTip` 与 `Level/Environment` 保存正式 HUD 和房间环境表现。
- `SourceArt/CZN` 保存源素材；项目负责人已确认 CZN/Spine 素材可进入公开仓库，但本指南不额外判断第三方法律授权。
- Entity prefab 只拥有视觉层级、anchor、socket、hit part 和 binder；战斗状态属于正式 session/director。
- 不得直接绑定根 `Assets/Art`、旧 D0Slice 或临时场景副本。
- 修改后检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`、`FormalFirstAuthoringContractTests.cs` 和相关正式 HUD 合同。
