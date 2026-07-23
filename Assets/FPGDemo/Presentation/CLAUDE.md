# FPGDemo Presentation 指南

本目录是 FPGDemo 的表现资源边界。它可以保存 Entity Prefab 的视觉结构、Socket、弱点/命中体和池化表现组件，但不承载遭遇状态机、伤害决策或场景服务所有权。

## 目录边界

- `D0Slice/Spine/` 是 D0 的派生 Spine/特效资源，`D0Slice/Audio/` 是 D0 音频与 Cue 资源；`FpgDemoD0SliceInstaller` 和对应配置/测试维护它们。
- `Actors/Fei/`、`Luan/Prefabs/`、`Hudie/Prefabs/` 保存可人工维护的完整 Entity Prefab；嵌套的 `D0Slice/Spine`、`Luan/Spine`、`Hudie/Spine` Generated Render Prefab 只提供渲染依赖。
- `LuanHudie/` 放召唤/出现等独立表现 prefab；`FormalEncounter/` 放正式遭遇绑定层 prefab；`Level/Environment/` 放房间环境表现。
- Entity Prefab 是姿态、Socket、碰撞体和弱点结构的人工入口。不要把 Generated Render Prefab 直接拖进场景或配置，也不要在场景里复制一份角色视觉树。

## 工作规则

- 通过 `FpgDemoD0SliceInstaller`、`FpgFormalEncounterDefaultsInstaller` 或 `FpgFormalRoomLoopInstaller` 的既有入口创建/刷新资源；重复执行应保持人工 Entity Prefab 的 Transform、Collider、Socket、组件引用和 GUID 不变。
- 原始 PMA/Spine 输入来自本地导入或美术源目录，运行时使用本目录的派生资源；不要让源资源承担 gameplay、hitbox 或 Socket 配置。
- `D0Slice/Spine/` 的来源与可分发许可仍待负责人确认。在确认或替换为原创/获授权资产前，不得把该目录作为提交基线、公开演示物或 G6 Release 放行依据。
- 改表现结构时保持 `D0CharacterDefinition`/`D0EnemyDefinition` 到唯一 Entity Prefab 的引用链；不要用临时场景对象绕过 Entity Prefab 合同。

## 验证

- 改 Entity Prefab 或生成引用时看 `D0EntitySceneAssetContractTests.cs`、`D0GeneratedActorPrefabReferenceContractTests.cs` 和 `D0SliceInstallerEntityOwnershipTests.cs`。
- 改 Fei Socket/锚点时看 `BattlePresentationAnchorTests.cs`；改 Burstbug D0 Spine FX 时看 `D0BurstbugCznFxAssetContractTests.cs`。
- 只改本指南时运行 `git diff --check`；资源来源和发布资格以 `Docs/Workflow/D0_Asset_Provenance_Audit.zh-CN.md` 为准。
