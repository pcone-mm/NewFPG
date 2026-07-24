# 绯（30048）PlayMode 单次播放与 R 重播验证

- Unity：`6000.3.15f1`
- 场景：`Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_SkillPreview.unity`
- Console：Error `0`，Warning `0`
- 方法：真实 Play Mode；技能只播放一次，自然完成时触发 `PlayableDirector.stopped`，随后清除运行时 Spine/粒子层和 standby，角色进入循环 `b_idle`。Unity 的 `PlayableDirector.state` 只有 `Playing/Paused`，停止后实测为 `Paused`；按 `R` 执行手动硬重播。

## 固定中帧与 R 重播

| 技能 | 时长 | 采样时刻 | runtimeObjects | active Spine | active particles | attachment 签名 | R 重播 |
|---|---:|---:|---:|---:|---:|---|---:|
| `u4_attack` | 2.9164s | 1.016s | 18 | 5 | 4 | `BC31D60E672116E3:321` | 3/3 一致 |
| `ug_attack` | 5.333s | 2.400s | 28 | 9 | 0 | `42B0303EECF93DCC:130` | 3/3 一致 |

每次 `R` 重播后，固定采样的 slot/attachment/type/mesh-world-vertex 组合哈希均一致。U4 的相同 attachment 集合在 3/3 次重播中恢复，覆盖了“首次有武器、之后重播消失”的回归场景。

## 自然完成清场

| 技能 | 完成验证 | completed | actor | actorLoop | runtimeObjects | active Spine | active particles | standby |
|---|---:|---|---|---|---:|---:|---:|---|
| `u4_attack` | 3/3 | `true` | `b_idle` | `true` | 0 | 0 | 0 | `false` |
| `ug_attack` | 3/3 | `true` | `b_idle` | `true` | 0 | 0 | 0 | `false` |

两项技能每次自然完成后都没有遗留运行时对象；角色停留在自身循环的 `b_idle`，不会自动再次播放技能。

## 暂停/继续

- 暂停：director state=`Paused`、skill=`u4_attack`、actor=`u4_attack_play`、completed=`false`；
- 继续：director state=`Playing`，skill 仍为 `u4_attack`；
- 结论：`Space` 暂停只冻结当前技能，不触发完成清场，也不切换到 `b_idle`。

## UG 附加检查

- BattleReady standby：0.499s 可见，0.501s 已关闭，符合 `STANDBY_ON → ACTION 0.5s → STANDBY_OFF`。
- 角色技能内状态：1.5s 仍为 `ug_attack_1`；自然完成后才进入循环 `b_idle`。
- 相机路径 scale 已并入正交 zoom；orthographic size 样本为 1.130s=`5.200`、1.930s=`2.501`、2.097s=`1.650`、2.263s=`3.662`。

## 截图

- `Assets/Screenshots/CZN/Fei_30048/Fei_30048_ModelPreview.png`
- `Assets/Screenshots/CZN/Fei_30048/Fei_30048_U4_1p016s.png`
- `Assets/Screenshots/CZN/Fei_30048/Fei_30048_UG_2p4s.png`
- `Assets/Screenshots/CZN/Fei_30048/Fei_30048_Completion_b_idle.png`
