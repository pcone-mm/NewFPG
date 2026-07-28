# Runtime/Skills 指南

`FPG.Skills` 是只依赖 `FPG.Core`、`noEngineReferences=true` 的确定性技能领域层，命名空间为 `FPG.Demo.Skills`。

## 职责边界

- `FpgSkillContracts` 定义 sequence、event、target、execution result 与错误合同。
- `FpgCompiledSkillModel` 保存通过校验的只读时间轴、类型化动作索引、active/action-node 表现句柄和分离的 gameplay/presentation hash；authoring DTO、Unity Object 与表现资源不进入本程序集。
- `FpgSkillExecutionRuntime` 按 `SkillExecutionId` 和连续 `TickIndex` 执行 compiled sequence；`FpgSkillExecutionIdAllocator` 只分配确定性执行 ID。
- 技能时钟固定为 60 tick/s。领域层不读取 `Time.deltaTime`、物理帧或动画帧；Unity 驱动只负责把正式 tick 送入该合同。

## 工作规则

- event 顺序由 tick 与 authored ordinal 固定；stable ID、`GameplayHashVersion`、`PresentationHashVersion`、hash 输入或排序规则变化都属于跨资产兼容变更。
- active presentation 事件只携带 handle、track、content hash 与可选 gameplay-event 绑定；不得把 Prefab、AudioClip、Camera 或 Unity 生命周期泄漏到领域层。
- `SelfDestructOwner` 是类型化 gameplay action；若设置绑定，只能指向同一 sequence、同 tick、排序更早的 `SummonActors` 事件，绑定 ID 或顺序不合法时编译 fail-closed；Unity authoring 层另将其 target 固定为 Self。
- result buffer、event count 和 tick 范围必须在开始执行前校验；容量不足、跳 tick、重复开始、终态重入和溢出一律 fail-closed。
- Cancel 必须为尚未触发的事件产生确定的 canceled result，不能静默丢失关联关系。
- 不在这里执行伤害、召唤、投射物、动画或 VFX；类型化动作在 `Runtime/Unity/Config` 编译，实际提交由 Player/Enemy/Run/Unity 层负责。

## 验证

- 核心执行与容量合同：`Assets/FPGDemo/Tests/EditMode/FpgSkillRuntimeTests.cs`。
- tick、空间元数据与播放映射：`FpgSkillClockConfigurationTests.cs`、`FpgSkillSpatialMetadataTests.cs`、`FpgSkillAnimationPlaybackTests.cs`。
- V3 编译与 gameplay/presentation hash 合同：`FpgSkillDefinitionTests.cs`、`FpgFormalSkillPresentationV3AssetTests.cs`。
- 改 asmdef 或依赖时同时检查 `AssemblyBoundaryTests.cs`；默认只记录这些精确入口，不自行批量运行测试。
