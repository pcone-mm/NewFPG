# D0 3C 运行时预览与调参流程

## 目标

`D0ThreeCProfile` 是 Fei FormalRoom 的单一 3C 配置入口；D0 前缀仅为序列化兼容。正常调参只修改配置资产，并在 Room Editor 正式镜头预览或 FormalRoom Play Mode 中验证，不存在场景安装步骤。

## 配置入口

- 资产：`Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_ThreeC.asset`
- 编辑器预览：`FPG Demo/Room Editor` 中选择该镜头模板并应用预览
- 运行链：`FPG_PlayableCharacterCatalog -> D0CharacterDefinition/D0ThreeCProfile -> FormalRoom`
- 运行应用：正式玩家组合在初始化时把该资产注入相机、准星、护盾和射击反馈组件。

## 日常调参步骤

1. 打开 `FPG_Fei_ThreeC.asset`，进入相机、自由准星、探身/缩回、护盾或射击镜头反馈分组。
2. 在 Room Editor 选择该资产作为镜头模板，使用 16:9 Game View 预览静止正式机位。
3. 需要验证动态反馈时，从 Boot 进入 FormalRoom Play Mode；保存资产后重新进入战斗以应用完整运行时配置。
4. 修改攻击查询距离或输入缓冲时，重启当前战斗会话，使确定性运行时重新读取配置。
5. 资产上的已保存值就是下次启动的默认值，不通过生成器写入场景。

若预览缺少房间、镜头模板或 `player-main`，Room Editor 必须停止预览并报告合同错误。场景结构或绑定缺失时，在对应 RoomDefinition、Art Scene、Boot 或 FormalRoom 中显式修复。

## 字段生效时机

| 分组 | 字段 | 默认值与约束 | 生效时机 | 作用边界 |
| --- | --- | --- | --- | --- |
| 标识 | `profileId` | `fei-combatlab-2p5d`；非空且稳定 | 保存后可见 | 识别、日志和场景关联 |
| 标识 | `displayName`、`designerNotes` | `Fei CombatLab 2.5D`；说明文本可为空 | 保存后可见 | 仅识别和调参记录，不进入战斗计算 |
| 构图验收 | `fixedPlayerViewportAnchor` | `(0.5, 0.22)`；x/y 在 0～1 | 保存后用于验收 | 不移动玩家根、命中点或运行时相机姿态 |
| 构图验收 | `cameraFocusViewport` | `(0.5, 0.56)`；x/y 在 0～1 | 保存后用于验收 | 只作为 CombatLab 构图关注点 |
| 相机 | `cameraPivotLocalPosition` | `(0, 4.7, -9.09)`；有限数值 | Play Mode 即时；下次启动自动读取 | CameraPivot 相对 PlayerAnchor 的位置 |
| 相机 | `cameraPivotLocalEulerAngles` | `(-1.85, 0, 0)` 度；有限数值 | Play Mode 即时；下次启动自动读取 | CameraPivot 相对 PlayerAnchor 的旋转 |
| 相机 | `cameraLocalPosition`、`cameraLocalEulerAngles` | 位置/旋转为零；有限数值 | Play Mode 即时；下次启动自动读取 | Main Camera 相对 CameraPivot 的局部姿态 |
| 相机 | `cameraFieldOfView` | `60` 度；大于 1 且小于 179 | Play Mode 即时；下次启动自动读取 | 镜头视野；会影响屏幕准星换算 |
| 相机 | `cameraNearClipPlane`、`cameraFarClipPlane` | `0.1` / `80`；均为正，远裁剪大于近裁剪 | Play Mode 即时；下次启动自动读取 | 仅改变渲染裁剪，不改变攻击查询距离 |
| 准星 | `reticleSafeViewport` | `(0.08, 0.12, 0.84, 0.76)`；矩形在 0～1 内且宽高为正 | Play Mode 即时 | 约束虚拟准星活动区域 |
| 准星 | `reticleSensitivity` | `1`；大于 0 | Play Mode 即时 | 约束输入到准星位移的倍率，不移动玩家根 |
| 攻击查询 | `maximumAimDistance` | `50` 世界单位；大于 0 | 点击 `重启战斗并应用全部` 或重新启动 | 3C 是 authored 场景的唯一有效来源；影响空间查询上限，不是相机远裁剪面 |
| 输入 | `inputBufferTicks` | `4` Tick；范围 1～32 | 点击 `重启战斗并应用全部` 或重新启动 | 重建输入源缓冲，不改变武器冷却或伤害 |
| 探身/缩回 | `peekTransitionSeconds`、`retractTransitionSeconds` | `0.08` / `0` 秒；不小于 0 | Play Mode 即时 | `PeekRoot` 平滑探身、下一已提交 Tick 瞬时归位；进入最多门控首发 5 Tick |
| 护盾 | `barrierFadeInSeconds`、`barrierFadeOutSeconds` | `0.18` / `0.12` 秒；大于 0 | Play Mode 即时 | 实体墙同 Tick 显隐，能量轮廓按该时长淡入/淡出 |
| 护盾 | `barrierMaximumOpacity` | `0.72`；范围 0～1 | Play Mode 即时 | 护盾最高不透明度 |
| 护盾 | `barrierColor` | `(0.34, 0.88, 1, 1)`；颜色分量有限 | Play Mode 即时 | 护盾显示颜色和基础 Alpha |
| 射击反馈 | `primaryShotCameraKick` | `0.035` 相机局部单位；不小于 0 | Play Mode 即时 | 只改变主射镜头后移 |
| 射击反馈 | `shotCameraKickRecoverySeconds` | `0.11` 秒；大于 0 | Play Mode 即时 | 镜头后移回正速度；应用新值会清除当前残留后移量 |

### 3C 与兼容手感资产

`D0CombatFeelProfile.maximumAimDistance` 仅保留为旧配置兼容和正值校验字段。authored D0 场景不再要求它与 3C 数值相等，也不会在运行时覆盖 `D0ThreeCProfile.maximumAimDistance`。调参时只改 3C 资产。

本文件不定义玩家技能的模式、序列或进度；正式运行规范统一见 [FPG Formal Encounter 运行合同](../../Assets/FPGDemo/Docs/Workflow/FPG_Formal_Encounter_Runtime_Contract.zh-CN.md#fei-副射唯一规范)。

### 掩体与探身边界

Fei Prefab 自带 `1.60 x 2.15 x 0.28` 的实体墙表现，局部位置为 `(0.30, 1.075, -0.16)`。墙体复用 `M_FPG_Cover`，不带 Collider 或 Rigidbody，因此不改变敌我弹道、寻敌或命中体。护盾值大于 0 且未锁定时墙体可见；护盾耗尽或锁定时墙体立即隐藏。

攻击或瞄准请求使 `PeekRoot` 沿本地 `+X 1.35` 探身。只有 `VisualRoot` 与表现枪口代理位于该根下；gameplay 根、命中体、CharacterController、AimAnchor、CameraPivot 和权威 SocketRegistry 始终使用 authored 位置。未完成探身的首发最多延迟 5 Tick，已完全探身时不增加延迟；快速点按会保留并完成该发。普通有盾状态在最后攻击 Tick 后恢复 `Withdrawn`，破盾/锁定时则只恢复视觉位置，承伤状态继续为 `Exposed`。

## 示例

Fei 默认资产的常用起点如下：

- 镜头枢轴：`(0, 4.7, -9.09)`，枢轴 X 旋转 `-1.85` 度，FOV `60`。
- 准星安全区：`(0.08, 0.12, 0.84, 0.76)`，灵敏度 `1`。
- 最大瞄准距离：`50` 世界单位，输入缓冲 `4` Tick。
- 探身/缩回：`0.08` / `0` 秒，表现偏移 `+X 1.35`；护盾最高不透明度：`0.72`。
- 主射镜头后移：`0.035`。

建议一次只改一个字段，先观察 Room Editor 构图，再进入 FormalRoom 检查动态手感；涉及攻击查询或输入时执行一次完整战斗重启。

## 验收与交接

| 检查项 | 结果 |
| --- | --- |
| 资产 `FPG_Fei_ThreeC.asset` 可被 Inspector 编辑 | 已通过 Unity 编译确认 |
| Play Mode 修改镜头/准星/护盾/射击反馈可即时看到 | 待主管试玩/确认 |
| 修改最大瞄准距离和输入缓冲后，完整重启生效 | 待主管试玩/确认 |
| 射击、快速点按、破盾、恢复、暂停与重启的掩体手感 | 待主管试玩/确认 |
| 普通调参不依赖场景生成器 | 已由编辑器预览与运行时应用链路覆盖 |
| 场景结构、父子关系或绑定损坏时只允许显式修复权威资产 | 合同校验必须 fail-closed |

视觉构图、镜头后移幅度、准星灵敏度和掩体淡入淡出属于主观手感，最终以主管在 FormalRoom 中试玩确认的结果为准。
