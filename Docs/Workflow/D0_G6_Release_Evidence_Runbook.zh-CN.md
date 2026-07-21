# D0 G6 Windows Release 证据运行手册

本手册将当前已有的 D0 自动播放、截图和性能采样路径整理为可交接的人工执行步骤。它不替代 Unity 编译、Release 实机验证或主管试玩，也不授权 Agent 自行启动 Unity、运行测试、构建 Player 或代做体验判断。

## 当前前置条件

| 项目 | 执行前必须确认 | 当前状态 |
| --- | --- | --- |
| Unity | Unity `6000.3.15f1` 可正常打开本项目 | Editor 当前未运行；待负责人启动后完成 Package Manager 解析与编译确认 |
| Spine | `Packages/manifest.json` 的 `file:../External/CZN/SpineRuntime-3.8`（相对 `Packages/` 解析）指向项目内 `External/CZN/SpineRuntime-3.8`，且包元数据为 `com.esotericsoftware.spine.spine-unity` 3.8.0 | 本地副本存在；许可归属、Unity 重新解析、编译和兼容性验证均待负责人确认 |
| D0 视觉资源 | `Assets/FPGDemo/Presentation/D0Slice/Spine/` 的来源、授权范围和可分发性 | 待负责人确认；在确认或替换为原创/获授权资产前，不得构建或发布候选 Player |
| Git 基线 | `Assets/FPGDemo` 与 D0 文档的基线归属已由负责人确认 | 待确认；不得用 reset/clean 处理现有工作区 |
| D0 场景合同 | Stage 只安装环境与具名 SpawnPoint；Context 显式绑定 SpawnPoint、`D0EnemyEntityWorld`、敌人行为控制器、准星、射击视图、玩家 Prefab Socket provider、瞄准锚点、主相机和同 Host 的表现协调器 | 候选构建前必须以本版安装器回写、Unity 编译和定向合同测试为准；历史证据不覆盖本次所有权迁移 |
| 场景 | Windows Release 只显式使用 `Assets/FPGDemo/Scenes/Boot.unity` 与 `Assets/FPGDemo/Scenes/CombatLab.unity` | 由 G6 构建执行者确认 |
| 分辨率与图形 API | Windows `1920×1080`、D3D11 Release | 执行时记录实际 Build Profile 与启动参数 |

恢复前置条件后，先打开 Unity 并等待脚本编译完成；记录 Console 中的项目错误。构建前还必须确认：Scenario 的 `playerSpawnPointId` 和 Encounter 每个 SpawnSlot 的 `spawnPointId` 均能在 Stage 解析；每个 SpawnSlot 的 `D0EnemyDefinition.EntityPrefab` 通过视觉/gameplay 分支、投射锚点、弱点与命中体校验；玩家主射/副射 Socket 和瞬时表现来自角色技能表现配置。只有在负责人授权自动化验证时，才运行对应的 EditMode/PlayMode 用例并归档 Test Runner XML。

## 仅构建 G6 场景的 Release Player

`Assets/FPGDemo/Editor/D0G6WindowsReleaseBuild.cs` 提供唯一的 G6 Windows x64 Release 构建入口。它显式将 `Boot.unity` 与 `CombatLab.unity` 传入 `BuildPipeline.BuildPlayer`，使用 `BuildTarget.StandaloneWindows64` 与 `BuildOptions.None`，不读取或修改项目全局场景列表，也不切换全局 Build Target。默认输出为 `Builds/FPGDemoD0/G6WindowsRelease/NewFPGD0.exe`。

获授权的构建执行者可在恢复依赖、确认编译通过后使用下面的 batch-mode 入口；构建日志路径须落在候选版本的证据目录中。

```text
<UnityEditor.exe> -batchmode -quit -projectPath D:\Unity\NewFPG -executeMethod FPG.Demo.Editor.D0G6WindowsReleaseBuild.BuildWindows64ReleaseFromBatch -logFile <evidence-directory>\unity-build.log
```

该入口仅完成构建，不代替后续 Player 运行、性能采样或人工试玩。Editor 当前未运行，且本次 manifest/lock 变更后的 Package Manager 解析与 Unity 编译尚未执行，因此该入口尚未在 Unity 中编译或执行。

默认输出目录是可复用的稳定位置，不是候选版本归档目录。每次构建成功后，先将 exe、同级 Data/UnityPlayer 文件、构建日志和实际 Player 启动参数复制到带候选版本标识的证据目录，再进行下一次构建；构建脚本不会自动清理或删除既有输出。

## 两次互斥的 Player 运行

证据运行与性能运行必须使用两个独立的 Windows Release Player 进程。证据路径每帧截屏并写入事件文件，会污染 GC 采样；两个驱动也都会占用 D0 的输入 override，不能在同一次运行中混用。

### A. 战斗流程与画面证据

使用同一个 Player 同时传入 `-d0-g6-evidence` 与 `-d0-g6-autoplay`。后者依赖前者已经激活；只传 autoplay 会以 `missing_evidence_bindings` 失败。

```text
<WindowsReleasePlayer>.exe -force-d3d11 -screen-width 1920 -screen-height 1080 -d0-g6-evidence -d0-g6-autoplay
```

自动播放会在真实 `BattleSessionHost` 输入 override 路径上完成 10 次交替的胜利、失败和重开，并记录初始、主射命中、弱点命中、可拦截弹幕、重型预警、Break、胜利与失败等截图点。成功日志应包含：

```text
[D0_EVIDENCE] autoplay_complete loops=10 victories=5 defeats=5 restarts=9
```

本路径输出 PNG 截图、PNG 帧序列和 `events.tsv`，不直接输出 MP4。以日志中的 `[D0_EVIDENCE] capture_ready path=...` 为准定位本次目录；归档前保留原始帧和 `events.tsv`，再使用团队认可的转码工具生成可审阅视频。若出现 `[D0_EVIDENCE] autoplay_failed`，该次运行无效，必须保留 Player.log 并记录失败原因，不得只挑选局部截图作为通过证据。

手动证据模式下，F1/F2/F3/F4/F6/F7/F8/F9 分别记录 initial、primary_hit、weakpoint_hit、interceptable_volley、heavy_warning、break、victory、defeat；F10 开始、F11 停止 PNG 帧录制。自动播放运行无需人工按键。

### B. 热路径性能与 GC 证据

性能运行不传 `-d0-g6-evidence` 或 `-d0-g6-autoplay`，并使用压力驱动保持真实固定池与射击路径持续运行：

```text
<WindowsReleasePlayer>.exe -force-d3d11 -screen-width 1920 -screen-height 1080 -d0-perf -d0-perf-stress -d0-perf-duration 60
```

完成后从 Player.log 提取 `[D0_PERF] capture_complete` 行，归档 duration、samples、p95、p99、max、`gc_counter_valid`、`gc_total_bytes` 和 `gc_peak_frame_bytes`。该行只是采样结果，不自动证明性能门通过；负责人还必须结合 Profiler/Memory capture 审核持续 GC、运行时 Instantiate/Destroy、池扩容和项目错误。

## Release 证据归档

每个候选版本建立独立、版本化的证据目录，并与候选 Git 提交或负责人确认的基线关联。至少归档以下内容：

| 类别 | 必须产物 | 通过前的检查 |
| --- | --- | --- |
| 构建身份 | 提交/基线标识、Unity 版本、Build Profile、启动参数、场景列表 | 可重现同一个 Player |
| 自动化 | Test Runner XML、Console/编译记录 | 仅记录实际执行过的结果 |
| 流程证据 | `events.tsv`、原始 PNG 截图、原始 PNG 帧、转码后视频 | 与截图标签和 autoplay 完成日志对应 |
| 性能 | Player.log、Profiler/Memory capture、`capture_complete` 摘录 | 能审计 GC、对象创建、池容量和错误 |
| 异常 | 失败日志、复现命令、环境差异 | 不删除、不用“已知问题”替代证据 |

将最终链接和结论回填到 `Docs/Workflow/D0_Validation_Evidence_Index.zh-CN.md`，以及 D0 的飞书进度与架构迁移文档。未运行的项必须保持“待执行”，不得由静态审阅替代。

## 待主管试玩与确认表

G6 的主观验收由主管组织 5 名试玩者各完成 3 次，不由 Agent 代测。填写结果时复用验证索引中的 5×3 记录，并至少覆盖下表。

| 编号 | 测试项 | 前置条件 | 主管操作 | 通过标准 | 证据/记录 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| H-G6-01 | 首次上手：准星与主射 | 正常 Release Player、默认 Fei 配置 | 瞄准并持续主射 | 操作与反馈可理解，无阻断问题 | 试玩者、日期、问题 | 待主管试玩/确认 |
| H-G6-02 | 三类 Threat 识别 | 正常遭遇流程 | 识别快攻、可拦截弹幕、重型弱点/Break，并采用预期应对 | 第二次体验后至少 4/5 通过 | 5×3 表与录像时间点 | 待主管试玩/确认 |
| H-G6-03 | 3C 循环 | 正常遭遇流程 | 完成瞄准→主射→副射 Break→缩回 | 第三次体验后至少 4/5 可独立完成 | 5×3 表与录像时间点 | 待主管试玩/确认 |
| H-G6-04 | 暂停与 F5 重开 | 正常 Release Player | 触发暂停、恢复和 F5 重开 | 状态恢复、准星重置与池清理符合场景合同 | 试玩记录与 Player.log | 待主管试玩/确认 |

## 已知边界

- 已实现项目级 `Battle/Pause.performed` 的早期控制路径：仅当 Input System 运行在 `ProcessEventsInDynamicUpdate` 时，回调才在常规 `MonoBehaviour.Update` 前提交 Pause/Resume，并立即同步 Actor2DPresenter 与 CZN 手动 Spine 视图；Host 随后跳过该控制帧的普通输入采样和模拟，避免同帧二次切换或恢复即开火。若同帧发现 F5，Host 仍优先执行重开。固定/手动 Input Update 或原始设备回退继续使用既有轮询路径，不声称输入帧零推进。
- 该路径尚无 Unity 编译、InputSystem device injection 或 Windows Player 实证；验收时必须在 Dynamic Input Update 下记录 Escape 输入帧的 Actor Skeleton `timeScale`/track time，并覆盖暂停、恢复、Esc+F5、输入 override、失焦和 F5 清理。
- 已提供 `D0G6WindowsReleaseBuild.BuildWindows64ReleaseFromBatch`，默认显式构建 Boot 与 CombatLab；它尚待当前 manifest/lock 的 Package Manager 解析、Unity 编译与一次真实构建验证，不得在此之前描述为已验证交付包。
- D0 authored scenario 的所有权合同为：Stage 只提供环境和 SpawnPoint；Scenario 用 `playerSpawnPointId` 选择玩家 gameplay 出生点；Encounter SpawnSlot 决定敌人、SpawnPoint、生成 Tick 与姿态策略；`D0EnemyDefinition.EntityPrefab` 拥有视觉根、gameplay 根、投射锚点、弱点与命中体；玩家枪口 Socket 和主射/副射瞬时表现属于角色技能表现。Host 在创建 Session 前必须检查这些引用以及 `D0EnemyEntityWorld`、行为控制器、准星、射击视图、主相机和表现协调器的同 Host 合同。场景不得保留临时枪口、副射目标代理或角色/怪物硬编码摆位；缺少绑定必须阻止初始化。本迁移必须由当前候选版本重新取得 Unity 安装、编译和定向测试证据，不能沿用旧 SceneContract 通过记录。
- D0 视觉资产来源/发布隔离审计见 [D0 资源来源与发布隔离审计](D0_Asset_Provenance_Audit.zh-CN.md)。这项门禁与 Spine Runtime 许可、Git 基线并列为构建前置条件；未确认前，G6 状态保持“待执行”。
- 本手册仅描述项目内的原创 D0 程序实现和证据路径，不涉及解包、绕过保护、提取或复用第三方游戏的代码、资源或数据；来源待确认的视觉资源不构成可发布交付物。
