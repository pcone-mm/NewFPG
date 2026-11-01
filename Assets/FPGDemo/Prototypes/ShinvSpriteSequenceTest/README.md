# 侍女序列帧测试

正式 BattleTest 使用 `shinv-test`（侍女测试），Prefab 为 `Assets/FPGDemo/Presentation/Characters/Enemies/shinvTest/Prefabs/PF_FPG_shinvTestSpriteEntity.prefab`。老 `shinv`（侍女）仍使用 Spine。

正式动画调速：打开上述 Prefab，在 `Fpg Sprite Enemy Entity View > Animations` 中按动画设置 `Playback Speed`。Walk 默认 1，Hit 默认 2.5；倍率只改变动画采样，不改变技能预警、出弹等事件的 tick。Hit 当前约 0.733 秒（60Hz 下 44 tick）。

无需进入 Play Mode，即可在该组件底部的“动画预览”选择 Walk/Hit，播放、暂停、拖动时间或逐帧查看。预览在隔离副本中完成，不保存预览帧或姿态到 Prefab。修改正式倍率后保存 Prefab，重新召唤检查实战效果。

普通受击会从头播放 Hit；再次受击重播并重新计时。Hit 期间停止移动并禁止新攻击，已起手技能继续执行；结束后站位查询重新选点，有可用移动目标才开始新的移动阶段。破防 Groggy 仍使用原有战斗中断规则。

以下为独立资源预览场景，不读取正式 View 的 Playback Speed：

打开 `Scenes/SC_ShInvSpriteSequencePreview.unity` 并进入 Play Mode，侍女默认循环播放移动动画；点击屏幕左上角的 `Play Hit` 可播放一次受击动画，结束后返回移动。

`Art/` 保存本次测试使用的 TexturePacker 图集及 JSON 导出信息。该测试只验证 SpriteRenderer/Animator 的资源工作流，不接入正式战斗中的 Spine、Root Motion 或命中盒系统。

当前按 12 FPS 播放。图集为 2522×2968 和 2422×3552，测试导入使用 4096 Max Size 与无压缩设置，主要用于检查原始画质和帧对齐；进入正式平台前需根据目标平台重新评估纹理压缩与显存占用。
