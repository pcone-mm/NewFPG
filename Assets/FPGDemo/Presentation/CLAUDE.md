# Presentation 指南

本目录是 FPGDemo 正式表现资产边界，不承载遭遇决策或长期 gameplay 状态。

- `FormalEncounter/` 保存人工维护的正式 `PF_FPG_*Entity`、HUD、出口和反馈 prefab。
- `Characters/*/Spine` 保存正式 prefab 使用的 SkeletonData、Atlas、材质、贴图及必要渲染 prefab。
- `Characters/*/VFX` 保存项目自有特效 wrapper。正式技能配置只引用这里的 `PF_FPG_*`；wrapper 可显式依赖 `Assets/VFX_Klaus/` 源材质、网格或 prefab，但不得引用供应商 Timeline/VFX_Lab demo。
- `HUD/HitTip` 保存正式 HUD；`Level/Environment` 保存房间源资源，`Level/Rooms/*/ART_*.unity` 保存按房间 additive 加载的正式 Art Scene，进入 `Level/` 前先读局部指南。
- `Assets/FPGDemo/SourceArt/CZN` 保存源素材；项目负责人已确认 CZN/Spine 素材可进入公开仓库，但本指南不额外判断第三方法律授权。
- Entity prefab 只拥有视觉层级、anchor、socket、hit part 和 binder；战斗状态属于正式 session/director。
- 房间 Art Scene 只拥有环境、碰撞、灯光、Volume 与表现绑定，不拥有正式 Camera、AudioListener、Host、RoomInstance 或 gameplay 状态。
- 轨迹 wrapper 根节点必须带可校验的 `FpgTrajectoryVfxView`；muzzle/charge/flight/impact wrapper 的生命周期、duration 与预热容量由技能配置和 presentation world 共同校验。
- 不得直接绑定根 `Assets/Art`、旧 D0Slice 或临时场景副本。
- 修改后检查 Unity 编译/Console、`FpgEntityPrefabContractTests.cs`、`FormalFirstAuthoringContractTests.cs` 和相关正式 HUD 合同；技能 wrapper、轨迹和全局预热预算另检查 `FpgFormalSkillPresentationV3AssetTests.cs` 与 `FpgSkillPresentationRuntimeTests.cs`。
