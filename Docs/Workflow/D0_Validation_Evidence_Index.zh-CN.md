# D0 验证与证据索引

> 历史证据索引：D0Slice、CombatLab 和安装器已经退役，以下记录不能作为当前 Formal 主线的发布证据。

本文件是 D0 交付物的索引模板。每次正式候选版本必须替换方括号内容，避免“已验证”没有可回放证据。

## 构建信息

| 项目 | 记录 |
| --- | --- |
| 候选版本 / Git 提交 | `尚未建立：Assets/FPGDemo 当前为未跟踪内容，须在用户确认后建立 Git 基线` |
| Unity 版本 | `6000.3.15f1` |
| 平台与分辨率 | `Windows Release / 1920×1080` |
| 场景 | `Assets/FPGDemo/Scenes/CombatLab.unity` |
| 样板 | `Fei × Burstbug` |
| 配置入口 | `Assets/FPGDemo/Config/BattleScenarioConfig.asset` |

## 自动验证矩阵

| 验收项 | 自动化位置 | 结果 | 证据 |
| --- | --- | --- | --- |
| 配置图、3C、行为、攻击资产 | `D0ProductionLineProfileTests` | `通过（本机 Editor，4 项；空间查询/observer 8 项另行通过）` | `Unity Test Runner，2026-07-15` |
| 历史场景与 Prefab/锚点/特效音频绑定 | `D0PlannerConfigurationValidator`、`SceneContractTests` | `通过（预检 + SceneContractTests 18 项；早于 2026-07-19 所有权迁移）` | `Unity Test Runner，2026-07-15` |
| Stage/Scenario/SpawnSlot/EntityPrefab/玩家技能表现所有权 | `D0StageDefinitionTests`、`D0CombatScenarioDefinitionTests`、`SceneContractTests`、`D0PlannerConfigurationValidator` | `待当前候选重新执行` | `不得复用 2026-07-15 证据` |
| 入场、巡航、攻击停驻与恢复 | `SceneContractTests.BurstbugPatrolSynchronizesVisualAndGameplayAnchorsAndStopsForAttack` | `通过` | `Unity Test Runner，2026-07-15` |
| 暂停、F5 重开、准星重置与池清理 | `SceneContractTests` | `通过（相关路径含在 18 项场景契约中）` | `Unity Test Runner，2026-07-15` |
| 确定性回放与威胁时序 | `BattleSessionThreatScheduleTests` | `通过（12 项，含回放 Digest）` | `Unity Test Runner，2026-07-15` |
| 固定池与热路径性能 | `D0FixedPoolPressurePlayModeTests` | `通过（32 投射物/32 HitTip 压力；Release GC 验证待执行）` | `Unity Test Runner，2026-07-15` |

## 本次本机自动化记录（2026-07-15）

- `D0PlannerConfigurationValidator`：通过；确认 `BattleScenarioConfig → D0CombatScenarioDefinition`、Fei 3C、Burstbug 行为、三种攻击资产、效果池与空间查询组合均有效。
- 定向 EditMode：24 项通过（生产线 4 项、空间查询/observer 8 项、配置与威胁时序 12 项）。
- PlayMode：`SceneContractTests` 18 项通过；固定池压力 1 项通过。覆盖入场、巡航、攻击停驻/恢复、快攻、可拦截弹幕、重型 Break、胜利/失败、暂停、F5、准星重置、真实物理命中和池清理。
- 以上是 Unity Editor 的自动化结果，不替代 Windows 1080p Release 的性能采样或 5 人试玩；正式候选版本须将 Test Runner XML、Profiler/Memory capture、录像、截图和 Player.log 复制到版本化证据目录。
- G6 Windows Release 证据执行与归档要求见 [D0 G6 Windows Release 证据运行手册](D0_G6_Release_Evidence_Runbook.zh-CN.md)。当前状态：`待执行`；manifest 中的相对 SpineRuntime-3.8 路径已只读确认解析到项目内 `External/CZN/SpineRuntime-3.8`，但 Unity Package Manager 重新解析、完整编译和 Git 基线确认仍待负责人执行，因此不得将上述本机 Editor 自动化结果标记为 Windows 1080p Release 验证通过。
- 已新增 `Assets/FPGDemo/Editor/D0G6WindowsReleaseBuild.cs`：它以显式的 Boot/CombatLab 场景列表生成 Windows x64 `BuildOptions.None` Player，不修改项目全局场景列表。该入口尚未经过 Unity 编译或实际构建，输出包、构建日志和 Player 证据仍为 `待执行`。
- 已新增 Dynamic Input Update 下的 Pause 回调与同帧控制帧门：`ProjectWideBattleInputAdapter` 仅在动态模式订阅 `Battle/Pause.performed`，`BattleSessionHost` 立即同步 D0 Spine 表现并跳过该帧普通采样/模拟；`BattlePresentationCoordinator` 的同步方法只写 Actor/CZN 暂停状态，不推进 feed、HUD、音频或战斗。静态路径核对已完成，Unity/Player 的严格零推进证据仍为 `待执行`。
- 2026-07-16 历史静态收口：当时以场景 `EnemyAnchor` 与显式 `D0EnemyBehaviorController` 为合同，并要求准星、射击表现与场景锚点绑定。该结构已被 2026-07-19 的 SpawnPoint/EntityPrefab 所有权迁移取代；其历史 Test Runner 记录不得用于证明新合同。
- 2026-07-16 历史测试合同同步：当时的 `SceneContractTests` 核对控制器挂在 `EnemyAnchor`；此断言已被“Stage SpawnPoint + EntityWorld + prefab-owned visual/gameplay roots”替代，不能继续作为当前合同。
- 2026-07-19 的所有权迁移记录属于已退役 D0 主线，不要求也不允许再由安装器回写；当前候选必须以 Formal authored 资产、现行 EditMode/PlayMode 合同和 Windows Release 构建重新建立证据。
- 2026-07-16 资源风险：`Assets/FPGDemo/Presentation/D0Slice/Spine/` 的来源与可分发许可尚未确认，且当前不受 `.gitignore` 排除。详见 [D0 资源来源与发布隔离审计](D0_Asset_Provenance_Audit.zh-CN.md)；在负责人确认前，不得把它作为 G6 构建/发布放行依据。

## 人体试玩记录（5 人 × 3 次）

| 试玩者 | 第 1 次 | 第 2 次：识别三类攻击 | 第 3 次：瞄准→射击→Break→缩回 | 问题与调整 |
| --- | --- | --- | --- | --- |
| P1 | `[记录]` | `[是/否]` | `[是/否]` | `[记录]` |
| P2 | `[记录]` | `[是/否]` | `[是/否]` | `[记录]` |
| P3 | `[记录]` | `[是/否]` | `[是/否]` | `[记录]` |
| P4 | `[记录]` | `[是/否]` | `[是/否]` | `[记录]` |
| P5 | `[记录]` | `[是/否]` | `[是/否]` | `[记录]` |

通过线：第二次体验后至少 4/5 能识别快攻、可拦截弹幕、重型弱点/Break，并采用预期应对；至少 4/5 能独立完成指定 3C 循环。

## 性能与运行日志

- Profiler / Memory capture: `待执行（Windows 1080p Release）`
- 1080p 实机录屏: `待执行`
- 截图（入场、巡航、三种攻击、Break、胜利、失败、重开）: `待执行`
- Player.log: `待执行`
- 已确认：战斗热路径无持续 GC、无运行时 Instantiate/Destroy、无池扩容、无项目错误：`固定池无扩容已由 PlayMode 压力测试覆盖；Release GC、运行时对象审计与正式日志仍待执行`。
