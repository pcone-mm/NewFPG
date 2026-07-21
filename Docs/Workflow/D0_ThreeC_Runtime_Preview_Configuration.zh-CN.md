# D0 3C 运行时预览与调参流程

## 目标

`D0ThreeCProfile` 是 Fei CombatLab 的单一 3C 配置入口。正常调参只修改配置资产并在 Play Mode 预览，不需要反复执行舞台安装器。安装器只负责首次搭建或修复场景结构、父子关系和序列化绑定。

## 配置入口

- 资产：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_3C.asset`
- 菜单：`FPG Demo/D0 2.5D/Open Camera & 3C Configuration`
- 运行链：`BattleScenarioConfig -> D0CombatScenarioDefinition -> D0ThreeCProfile`
- 运行应用：`BattleSessionHost` 在初始化和重启时提交会话配置；`D0ThreeCRuntimeProfileApplier` 负责把当前资产写入镜头、准星、护盾和射击反馈表现组件。

## 日常调参步骤

1. 打开 `D0_Fei_3C.asset`，进入 `相机安装参数`、`自由准星与攻击查询`、`探身／缩回表现衔接`、`护盾显示` 或 `射击镜头后移反馈` 分组。
2. 打开 Boot/CombatLab 场景并进入 Play Mode。Inspector 底部默认开启 `Play Mode 修改后自动应用表现参数`。
3. 修改镜头、准星、护盾或射击后移字段，当前运行会自动刷新。也可以点击 `应用表现到当前运行` 手动刷新。
4. 修改 `攻击查询最远距离` 或 `输入缓冲时长` 后，点击 `重启战斗并应用全部`；该按钮会重建当前 BattleSession 的查询和输入缓存。
5. 退出 Play Mode 后，资产上的值就是下次启动的默认值。无需为了普通数值调参运行安装器。

若 Inspector 显示没有匹配的已加载场景，先进入使用同一 authored scenario 的 CombatLab Play Mode；若场景结构或绑定缺失，再由程序执行一次安装器修复结构。

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
| 探身/缩回 | `peekTransitionSeconds`、`retractTransitionSeconds` | `0.08` / `0.10` 秒；不小于 0 | Play Mode 即时 | 只约束表现过渡下限；暴露度和承伤规则仍在确定性战斗域 |
| 护盾 | `barrierFadeInSeconds`、`barrierFadeOutSeconds` | `0.18` / `0.12` 秒；大于 0 | Play Mode 即时 | 护盾线框淡入/淡出节奏 |
| 护盾 | `barrierMaximumOpacity` | `0.72`；范围 0～1 | Play Mode 即时 | 护盾最高不透明度 |
| 护盾 | `barrierColor` | `(0.34, 0.88, 1, 1)`；颜色分量有限 | Play Mode 即时 | 护盾显示颜色和基础 Alpha |
| 射击反馈 | `primaryShotCameraKick`、`secondaryShotCameraKick` | `0.035` / `0.09` 相机局部单位；不小于 0 | Play Mode 即时 | 只改变主射/副射镜头后移 |
| 射击反馈 | `shotCameraKickRecoverySeconds` | `0.11` 秒；大于 0 | Play Mode 即时 | 镜头后移回正速度；应用新值会清除当前残留后移量 |

### 3C 与兼容手感资产

`D0CombatFeelProfile.maximumAimDistance` 仅保留为旧配置兼容和正值校验字段。authored D0 场景不再要求它与 3C 数值相等，也不会在运行时覆盖 `D0ThreeCProfile.maximumAimDistance`。调参时只改 3C 资产。

## 示例

Fei 默认资产的常用起点如下：

- 镜头枢轴：`(0, 4.7, -9.09)`，枢轴 X 旋转 `-1.85` 度，FOV `60`。
- 准星安全区：`(0.08, 0.12, 0.84, 0.76)`，灵敏度 `1`。
- 最大瞄准距离：`50` 世界单位，输入缓冲 `4` Tick。
- 护盾最高不透明度：`0.72`；主射/副射镜头后移：`0.035` / `0.09`。

建议一次只改一个字段，先观察画面构图，再用 `应用表现到当前运行` 检查手感；涉及攻击查询或输入时再执行一次完整重启。

## 验收与交接

| 检查项 | 结果 |
| --- | --- |
| 资产 `D0_Fei_3C.asset` 可被 Inspector 编辑 | 待 Unity 编译后确认 |
| Play Mode 修改镜头/准星/护盾/射击反馈可即时看到 | 待主管试玩/确认 |
| 修改最大瞄准距离和输入缓冲后，完整重启生效 | 待主管试玩/确认 |
| 普通调参不依赖 `Install or Update Combat Slice` | 已由运行时应用链路覆盖 |
| 场景结构、父子关系或绑定损坏时仍可用安装器修复 | 保留现有安装/迁移路径 |

视觉构图、镜头后移幅度、准星灵敏度和护盾淡入淡出属于主观手感，最终以主管在 CombatLab 中试玩确认的结果为准。
