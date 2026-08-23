# FPGDemo / Feel 接入说明

项目使用 `Assets/Feel` 中的 More Mountains Feel v6.0，Unity 固定为
`6000.3.15f1`。

## 当前边界

- Feel 只作为附加表现层，不拥有战斗状态、伤害、命中判定、相机、音频、
  VFX、粒子或伤害跳字。
- 本目录使用 `FPG.Demo.Unity` 命名空间并留在默认 `Assembly-CSharp`；只有这里可以直接引用 `MoreMountains.*`，`FPG.Unity` 只发布中立的 `FpgSupplementalFeedbackEvent`。
- 当前唯一接入效果是敌人普通命中和弱点命中的局部渲染缩放 Spring。
- 开火、HUD 和弹体拦截补充事件仍由项目发布，但没有 Feel 消费者。
- 不使用 Flash、Vignette、Chromatic Aberration、Camera、Time、
  Haptics、Feel Audio 或 Transform Camera Shake。

## 运行时结构

- `FpgFormalCombatFeedbackBridge` 按 `TargetId` 聚合同帧命中：同一敌人每帧
  最多一次，不同敌人分别发布，弱点分类优先于普通命中。
- `FormalRoom/__FormalRoom/Presentation/FeelFeedbackRoot` 只挂
  `FpgFeelEnemyHitRouter`，通过 `FpgEnemyEntityPool.TryGet(TargetId)` 路由到
  池化敌人。
- 三个正式敌人 Prefab 都嵌套共享 Prefab：
  `Assets/FPGDemo/Integrations/Feel/Prefabs/PF_FPG_EnemyHitFeel.prefab`。
- `FpgFeelRenderScaleSpringTarget` 只保存 Spring 数值。它在 SRP 相机开始渲染
  时临时缩放 Spine `VisualRoot`，相机结束渲染后立即恢复，因此普通 Update、
  60Hz Tick、根运动和碰撞体骨骼跟随看不到缩放后的 Transform。

## 调节位置

打开共享 Prefab，选择其 `MMF_Player` 子节点，在 `MMF Player` 的
`Enemy render scale spring (Spring Float)` 中统一调节：

- `Bump Amount`: 默认 `2.0`，越大受击弹性越明显。
- `Declared Duration`: 默认 `0.06s`；到点由适配器硬停止并恢复，不等待 Spring 自然衰减。
- `Override Damping / New Damping`: 默认开启，数值 `0.82`。
- `Override Frequency / New Frequency`: 默认开启，数值 `14`。
- `Minimum Scale / Maximum Scale`: 默认 `0.985 / 1.035`，是最终安全限制。
- `FpgFeelEnemyHitFeedback/One Shot Duration`: 默认 `0.06s`。
- `FpgFeelEnemyHitFeedback/Cooldown Duration`: 默认 `0.06s`。

三个敌人共享同一 Prefab，不要分别覆盖这些参数。普通命中和弱点命中当前
故意使用同一效果。

## 重建与兼容

菜单 `FPG/Integrations/Feel/Rebuild FormalRoom Feel Setup` 会重建共享 Prefab、
挂接三个敌人、配置场景路由，并清除旧 Flash、URP Volume、全屏 MMF Player
及 HUD/准星 Scale Shaker。它不会恢复旧全屏效果。

Feel 6.0 在 Unity `6000.3.15f1` 下的 `FindObjectsByType` 调用必须显式传入
`FindObjectsSortMode.None`。重新导入或升级 Feel 后执行：

```powershell
rg -n "FindObjectsByType\(" Assets/Feel -g "*.cs"
```

`D0CombatFeelProfile` 仍是项目玩法参数，不是 More Mountains Feel 插件。

## 验证

- 修改事件分类、目标聚合或 HUD 资源事件后检查 `FpgSupplementalFeedbackTests.cs`。
- 修改共享 prefab、敌人挂接、场景路由、Spring 或清理行为后检查 `FpgFeelEnemyHitAssetTests.cs`。
- 两项验证都必须先确认 Unity 编译与 Console；没有当前 Test Runner 结果时不得宣称测试通过。
