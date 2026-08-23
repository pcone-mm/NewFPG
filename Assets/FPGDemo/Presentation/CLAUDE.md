# Presentation 指南

本目录是 FPGDemo 正式表现资产边界，不承载遭遇决策或长期 gameplay 状态。

- `Characters/Players/<Name>` 与 `Characters/Enemies/<Name>` 按角色聚合正式 Entity、Spine 和角色自有 VFX；敌人共享 VFX 位于 `Characters/Enemies/Shared`。进入该目录前先读局部指南。
- `HUD/` 保存正式血条、伤害跳字、命中提示和蓄力 UI；`Boot/Materials` 只保存 Boot 场景专用材质。
- `Level/Covers` 与 `Level/RoomExit` 保存正式掩体、过渡 VFX 和房间出口表现；`Level/Environment` 保存房间源资源，`Level/Rooms/*/ART_*.unity` 保存按房间 additive 加载的正式 Art Scene，进入 `Level/` 前先读局部指南。
- `Assets/FPGDemo/SourceArt/CZN` 保存源素材；项目负责人已确认 CZN/Spine 素材可进入公开仓库，但本指南不额外判断第三方法律授权。
- Entity prefab 只拥有视觉层级、anchor、socket、hit part 和 binder；战斗状态属于正式 session/director，具体合同以 `Characters/CLAUDE.md` 为准。
- 房间 Art Scene 只拥有环境与表现内容；Camera、AudioListener 与表现绑定的局部合同以 `Level/CLAUDE.md` 为准，不得拥有 Host、RoomInstance 或 gameplay 状态。
- 轨迹 wrapper 根节点必须带可校验的 `FpgTrajectoryVfxView`；muzzle/charge/flight/impact wrapper 的生命周期、duration 与预热容量由技能配置和 presentation world 共同校验。
- 不得直接绑定根 `Assets/Art`、旧 D0Slice 或临时场景副本。
- 修改后检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`、`FormalFirstAuthoringContractTests.cs` 和相关正式 HUD 合同；技能 wrapper、轨迹和全局预热预算另检查 `FpgFormalSkillPresentationV3AssetTests.cs` 与 `FpgSkillPresentationRuntimeTests.cs`。
