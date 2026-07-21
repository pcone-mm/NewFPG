# D0 Fei 换弹表现配置

## 目标与范围

本配置控制 Fei 在 D0 CombatLab 中收到已提交换弹事件后的 Spine 表现。换弹不检查剩余护盾值：Fei 在暴露或掩体内都能发起换弹；发起时战斗姿态会先自动切到 `Withdrawn`（回到掩体内），再进入 `Reloading`。表现字段本身不改变弹匣容量、换弹耗时或弹药结算。

## 配置入口与引用关系

- 主入口：`Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_Presentation.asset`
- 兼容回退：`Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset`
- 引用链：`BattleScenarioConfig → D0CharacterDefinition → D0ActorPresentationDefinition → Actor2DPresenter`
- 运行时事件：`ReloadStarted` 开始动作，`ReloadCompleted` 返回待机；玩家 `DamageApplied` 通过 `DamageChannel` 区分护盾与生命受击，生命伤害取消换弹时还会记录 `AttackCanceled (Reloading → Ready)`。

## 字段说明

| 中文名称 | 字段名 | 类型 | 默认值 | 生效条件与结果 | 常见误配 |
|---|---|---|---|---|---|
| 换弹动作动画名称 | `reloadPlayAnimation` | Spine 动画名 / string | `u1_buff_play` | 收到当前玩家的 `ReloadStarted` 时立即单次播放；只改变表现 | 填入不属于 Fei 主骨骼的动画名会导致角色表现校验失败 |
| 换弹等待动画名称 | `reloadReadyAnimation` | Spine 动画名 / string | `u1_buff_ready` | 换弹动作播完但战斗仍处于 `Reloading` 时循环；收到 `ReloadCompleted` 后返回 `idleAnimation` | 当成换弹完成回调；该字段不会改变实际换弹时长 |

两个字段必须非空，并且必须存在于所绑定 Fei `SkeletonDataAsset`。换弹期间的护盾伤害不会替换当前换弹动画，护盾被打破的那一下也不会触发 Fei 的踉跄动作；只有 `DamageChannel.Life` 的直接扣血会取消实际换弹（保留当前弹量并清除换弹计时），同时播放受击并回待机。取消由战斗逻辑执行，Spine 表现只跟随已提交事件。

## 制作与安装步骤

1. 在 Project 窗口打开 `D0_Fei_Presentation.asset`。
2. 在玩家表现的“换弹动画”分组填写两个 Spine 动画名。
3. 确认该资产仍绑定 Fei 的 D0 派生视觉 Prefab。
4. 若更换了 Prefab 或重新生成 D0 表现资源，执行 `FPG Demo/D0 2.5D/Install or Update Combat Slice`；只修改动画名时无需改战斗配置。

## 示例与预期表现

标准 Fei 配置为 `u1_buff_play → u1_buff_ready`。无论 Fei 当前是否位于掩体内、护盾是否已经耗尽，按下换弹键都会先自动缩回，再播放一次 `u1_buff_play`；若动作先于 84 Tick 的换弹流程结束，则以 `u1_buff_ready` 承接；收到换弹完成事件后回到 `b_idle`。换弹尚未完成时，即使玩家按住瞄准或射击，战斗姿态仍保持在掩体内。

## 验收与交接

| 测试项 | 前置条件 | 操作 | 通过标准 | 证据 | 状态 |
|---|---|---|---|---|---|
| 掩体内换弹 | CombatLab、Fei 弹匣未满且位于掩体内 | 按一次换弹键并等待完成 | 立即播放 `u1_buff_play`，必要时由 `u1_buff_ready` 承接，完成后回 `b_idle`；换弹期间保持缩回姿态 | 录屏/问题记录 | 待主管试玩/确认 |
| 暴露时换弹 | Fei 弹匣未满且当前处于暴露状态 | 保持瞄准并按一次换弹键 | Fei 同 Tick 自动缩回掩体并进入 `Reloading`，随后按正常换弹流程播放 | 录屏/问题记录 | 待主管试玩/确认 |
| 无护盾换弹 | 护盾值为 0 / 正在锁定恢复 | 按一次换弹键 | 仍会自动缩回并进入 `Reloading`；此时来袭伤害直接走生命通道，并按生命受击规则取消实际换弹 | 录屏/问题记录 | 待主管试玩/确认 |
| 换弹中护盾受击 | Fei 正在换弹且敌人攻击命中护盾 | 分别承受普通护盾伤害与护盾打破伤害 | 当前换弹动作不被受击/踉跄动画替换 | 录屏/问题记录 | 待主管试玩/确认 |
| 换弹中生命受击 | Fei 正在换弹且攻击直接扣除生命 | 承受一次 `DamageChannel.Life` 伤害 | 立即取消实际换弹且不补满弹匣，同时播放 Fei 受击动作并回待机 | 录屏/问题记录 | 待主管试玩/确认 |
| 连续重试 | 至少可完成两次换弹 | 连续进行两轮射击与换弹 | 每轮 `u1_buff_play` 都从头播放，不残留上一轮姿势 | 录屏/问题记录 | 待主管试玩/确认 |

静态技术检查包括：两个动画名均存在于 Fei 主骨骼、`D0ActorPresentationDefinition` 与兼容 Profile 配置一致、Unity 编译和 Console 无相关错误。视觉节奏与动作可读性由主管按上表试玩确认。
