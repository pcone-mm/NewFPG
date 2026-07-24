# 市长海德玛丽（30093）战斗技能资源组合

本报告只读分析以下已提取资源，没有启动客户端，也没有修改 Unity
场景或脚本：

- `Configs/model_data/30093.srcs.json`：技能命令组；
- `Configs/model_data/30093.srmd.json`：主模型动作命令图；
- `Configs/model_data/30093_battle_ready.brmd.json`：战斗准备模型命令图；
- `Configs/effect/*.cfx.xml`：复合特效层；
- `Configs/effect/*.particle.xml`：粒子发射器；
- `SpineSource/effect`、`AncillarySource` 与 `External/.../Raw/main`：实际资源。

全部逐节点、逐 CFX 层、逐粒子发射器字段见
[`heidemarie-30093-skill-effects.json`](heidemarie-30093-skill-effects.json)。
13 个 camera/camera_path/node 资源的完整骨骼关键帧与曲线见
[`heidemarie-30093-ancillary-motions.json`](heidemarie-30093-ancillary-motions.json)。

## 组合链

```text
SRC command_set
  -> SRMD command graph（动画、EFFECT、镜头、伤害、命中停顿）
    -> CFX primitive[]（前后景复合层）
      -> Spine JSON + atlas + PNG
      -> particle XML + shared SCT texture
```

时间单位约定：SRMD 与 CFX 的时间字段按毫秒记录；粒子 XML 中报告为
`*_s` 的字段按秒记录。JSON 中的 `start_ms_graph` 是沿显式
`from_guid` 父链累加 `delay` 得到的配置图时间，不是运行时抓帧结果。
例如 U1 五段伤害原始 delay 为 `233, 33, 33, 33, 200`，图时间还原为
`233, 266, 299, 332, 532 ms`。

## 技能总览

| 技能 | 命令链 | 顶层 CFX | 关键镜头/节点 |
|---|---|---|---|
| 普攻 | `attack_ready -> attack_play1/2 -> attack_end` | 两个自身 CFX、两个目标 CFX | 66 ms 震屏与速度模糊，80 ms 命中，120 ms 三档 hit-stop |
| U1 | `attack_ready -> u1_attack_ready -> play -> end` | ready 1 个、play 自身/目标各 1 个 | 166 ms 震屏，1000 ms 全局停顿，1066 ms 径向 RGB 模糊 |
| U2 | `u2_buff_ready -> u2_buff_play` | ready/play 各 1 个 | play 的 200 ms HIT，未引用独立相机资源 |
| U3 | `attack_ready -> u3_attack_ready -> play -> end` | ready 1 个；play 目标、闪光、自身 ready、自身 play 共 4 个 | 0 ms 震屏，566 ms HIT，966 ms 模糊与 hit-stop |
| U4 | `u4_buff_ready -> u4_buff_play` | ready/play 各 1 个 | play 在 0 ms 触发 HIT；粒子层含 0/1000 ms 两个起点 |
| UG | `ug_attack` | 背景、前景、屏幕、impact、中心主体共 5 个 | camera-path 0 ms，目标节点 2700 ms，camera-shake 2766 ms，普通震屏 4700 ms |
| UX | `ux_attack` | `ux_attack_01/02_1/02_2/03` 共 4 个 | 三个 shake 资源、目标 node、六段 cam_move、四段 cam_zoom，5266 ms HIT |
| U5 | 配置中存在的额外交替攻击组 | 与普攻共用基础 Spine 层，但显式选择 `u5*` 动画 | 结构与两段普攻接近；不在用户要求的 U1-U4 中，但资源链完整 |

## 普攻

`attack_play1` 和 `attack_play2` 的角色动画均为 666.7 ms。

- 自身特效：`heidemarie_30093_eff_attack_play1/2`，`SELF + ATTACH`，
  偏移 `0,0`、缩放 1、旋转 0。每个 CFX 含前景 Spine 层 `z=+5`
  和背景 Spine 层 `z=-5`。
- 目标特效：`heidemarie_30093_eff_attack_play1_target` 或
  `...play2_target`，`TARGET + POSITION`，随机横向偏移 `-50~50`。
  每个 CFX 有 7 层：目标后景、剑、多个目标前景和 3 个粒子层。
- play1 在 66 ms 使用 `heidemarie_30093_eff_attack_play2_shake`；
  play2 反过来使用 `...play1_shake`。这是配置中的明确交叉引用，不是
  报告拼写错误。
- 66 ms 开始速度模糊：方向 125°、force 2、采样 10、持续 50 ms；
  80 ms HIT；120 ms 同时设置 STRONG/WEAK/FINISH 三档
  `STOP_SUBJECTS`（100/60/150 ms）。

## U1

- ready：`heidemarie_30093_eff_u1_attack_ready`，3 层。前后景 ready
  Spine 各一层，另有 `u1_attack_play_b:ready` 后景层。
- play 自身：`heidemarie_30093_eff_u1_attack_play`，0 ms，
  `SELF + POSITION`。CFX 含前景/后景 Spine 和 4 次同粒子资源实例：
  delay `67/100/166/33 ms`，位置分别为 `(60,45)`、`(60,120)`、
  `(30,90)`、`(-90,180)`，缩放均为 `-1`，旋转为
  `-10/0/-2/+5°`；后两层使用负 z。
- play 目标：`heidemarie_30093_eff_u1_attack_play_target`，166 ms，
  `TARGET + POSITION`，偏移 `0,10`、缩放 0.9。CFX 有 5 个 Spine
  视觉层和 1 个粒子层；剑层播放 `u1_attack_play_target`，时长约
  3233.3 ms。
- 伤害图时间：233/266/299/332 ms 各 10%，532 ms 为 60%；
  200 ms HIT；166 ms 引用
  `camera/heidemarie_30093_eff_u1_attack_play_shake`；1000 ms 强
  `STOP_ALL` 33 ms；1066 ms 对 TARGET 的 `target` 骨骼做 100 ms
  径向 RGB 模糊。

## U2

- ready：`heidemarie_30093_eff_u2_buff_ready`，3 个 Spine 层：
  `u2_buff_f` 前景、`u2_buff_b` 后景、`u2_buff_sword` 中性 z。
- play：`heidemarie_30093_eff_u2_buff_play`，`SELF + POSITION`，4 层。
  前/后景与剑层从 0 ms 开始；`heidemarie_30093_u2_buff_par_1`
  粒子在 100 ms、位置 `(50,250)`、缩放 1.2 启动。
- 角色 play 动画 1400 ms；HIT 在 200 ms，`play_action=false`。

## U3

- ready 目标：`heidemarie_30093_eff_u3_attack_ready_target`，
  `TARGET + ATTACH`、`zorder=1`，两把剑的 ready Spine 层。
- play 从 0 ms 同时启动：
  - `heidemarie_30093_eff_u3_attack_target`：TARGET，19 层；
  - `heidemarie_30093_eff_u3_attack_ready`：SELF，2 层；
  - `heidemarie_30093_eff_u3_attack_target_flash`：TARGET，单层，
    顶层 `zorder=100001`；
  - `heidemarie_30093_u3_attack_shake` 相机震屏。
- 目标 19 层包括 15 个 Spine 层和 4 个粒子层；局部 z 从
  `-4` 到 `300`，石块、剑、风、闪光与前后景分离。配置明确让
  `heidemarie_30093_eff_u3_attack_target_wind_b` 播放
  `u5_attack_play2`，这是一个可疑但确定的跨动作复用。
- 自身 play CFX 在 500 ms 启动：前景、后景和一个 130 ms 延迟粒子。
- 566 ms HIT；600/900 ms 两段伤害 30%/70%；966 ms 对 TARGET/root
  做 46.7 ms 径向模糊，并对 subjects 做 66.7 ms STRONG stop。

## U4

- ready：`heidemarie_30093_eff_u4_buff_ready`，4 个 Spine 层，
  局部 z 为 `100, 50, -50, -100`，把自身与剑的前后景完全拆开。
- play：`heidemarie_30093_eff_u4_buff_play`，`SELF + POSITION`，6 层。
  4 个 Spine 层从 0 ms 开始；两个粒子位于 `(0,150)`、`z=200`，
  `pati02` 从 0 ms 开始，`pati01` 从 1000 ms 开始。
- 角色动画 1466.7 ms；CFX 的自身前后景 Spine 动画约 2000 ms；
  HIT 配置在 0 ms。

## UG

UG 主命令的角色骨骼动画只有 333.3 ms，但五个屏幕/复合 CFX 自行
覆盖约 2.8-5.5 秒的演出：

| CFX | 顶层位置 | 顶层 zorder | 层数 | 主要内容 |
|---|---|---:|---:|---|
| `heidemarie_30093_ug_attack_bg` | CENTER | -5000 | 2 | 约 4.67 秒背景 Spine |
| `heidemarie_30093_ug_attack_01` | FRONT | 0 | 20 | 9 个 portrait1、剑 intro、光效，z=0..18 |
| `heidemarie_30093_ug_attack_screen` | SCREEN | 300 | 2 | 5.5 秒屏幕 Spine + 4600 ms 粒子 |
| `heidemarie_30093_ug_attack_impact` | CENTER | 200 | 1 | 约 4.1 秒 impact Spine |
| `heidemarie_30093_ug_attack_02` | CENTER | 0 | 19 | portrait2、剑、玻璃、光环，约 4.93 秒 |

所有五个顶层 EFFECT 都从 0 ms 开始、`screen_effect_shaking_flag=true`。

- 0 ms：`heidemarie_30093_ug_battle_ready_camera_path`；
- 2700 ms：在 TARGET/CENTER 播放
  `heidemarie_30093_ug_monster_node`；
- 2766 ms：`heidemarie_30093_ug_camera_shake` camera-path；
- 2800 ms HIT；伤害图时间 2866/3032/3198/3298/3931 ms，权重
  1/1/1/2/4；
- 4666 ms：zoom 1.5，并在 333.3 ms 内回到 1.0；
- 4700 ms：`cm_shake_vb_dn`。

`30093_battle_ready.brmd` 还有独立 UG 过渡：0 ms 播放 standby node，
150 ms 启动 `heidemarie_30093_ug_battle_ready_eff`（单个 Spine 层，
约 666.7 ms，SELF、ATTACH、zorder 1000），并删除待机循环的四个持久
effect id。

## UX

UX 从 0 ms 同时挂出四个顶层 CFX：

| CFX | 顶层位置/缩放 | 层数 | 局部 z 范围 | 估算 Spine 时长 |
|---|---|---:|---|---:|
| `heidemarie_30093_ux_attack_01` | SELF/POSITION，0.8 | 22 | 前景 900..907；后景 -991..-1005 | 约 4033.3 ms |
| `heidemarie_30093_ux_attack_02_1` | CENTER/POSITION，1.0 | 16 | 前景 899..905；后景 -9992..-10000 | 约 2066.7 ms |
| `heidemarie_30093_ux_attack_02_2` | SCREEN/ATTACH，0.5 | 2 | 999..1000 | 约 1233.3 ms |
| `heidemarie_30093_ux_attack_03` | SCREEN/POSITION，0.8 | 2 | 999..1000 | 粒子 lifetime 444 ms + 600 ms Spine |

- 0 ms：目标 node `heidemarie_30093_ux_attack_03_target_node`；shake
  `ux_attack_cm1` 与 `cm3`；`cm2` 在 33 ms。
- cam_move 包括 CENTER `(0,0)`、SELF `(-20,-140)`、
  `(0,-120)`、`(35,-120)`，以及 433 ms 的 `(40,-160)` 与
  1500 ms 平滑段。
- cam_zoom 从 1.0/1.2 进入 1.36；背景与目标还使用多组
  `bg_blend`/`color_blend`；HIT 在 5266 ms。
- UX 的大量负 z 并非负时间，而是明确的深后景排序层。

## 镜头、震屏与 node 曲线

13 个 `AncillarySource` SCSP1U 都使用同一套已验证的骨骼时间线格式，
没有 slot 或 attachment，但可以完整读取 rotate/translate/scale/shear。
因此这里不再只是“知道文件名”，而是已经恢复其关键帧、Bezier 曲线、
目标骨骼与持续时间。

| 资源 | 时长 | 关键字段范围 |
|---|---:|---|
| `cm_shake_vb_dn` | 366.7 ms | `cam` 7 帧，X `-13..15`，Y=0 |
| `eff_attack_play1_shake` | 800 ms | `cam` 10 帧，X `-31.08..20.11`，Y `-20.11..16.45` |
| `eff_attack_play2_shake` | 800 ms | `cam` 10 帧，范围同 play1；通过负 scale pivot 镜像 |
| `eff_u1_attack_play_shake` | 1300 ms | `cam` 24 帧，X `-71.20..62.05`，Y `-59.52..52.19`；另有 node 初始偏移 `(-9.89,24.15)` |
| `u3_attack_shake` | 1800 ms | `cam` 21 帧，从 533.3 ms 起；Y `-75.31..108.08` |
| `ux_attack_cm1` | 3900 ms | `camera` 14 个位移帧，Y `-40..30`；root 旋转 `-0.06°` |
| `ux_attack_cm2` | 2066.7 ms | `camera` 27 帧，Y `-30..20` |
| `ux_attack_cm3` | 666.7 ms | `camera` 21 帧，Y `-30..40` |
| `ug_battle_ready_camera_path` | 866.7 ms | node 位移到 `(351.08,-255.51)`、scale 到 1.76、rotation 到 `-0.7°` |
| `ug_camera_shake` | 1933.3 ms | `animation/animation2` 各 36 帧，X `-32.47..29.61`，Y `-100..40` |
| `ug_battle_ready_node` | 866.7 ms | node 的 rotate/translate/scale/shear，与 battle-ready camera path 对应 |
| `ug_monster_node` | 1966.7 ms | node X `6.59..356.99`、Y `-348.56..-70.64`、scale `0.274..1`；pivot 另有位移/缩放 |
| `ux_attack_03_target_node` | 2066.7 ms | node rotation `-22.61..-11.98°`、位置约 `(204.14,-304.93..-259.80)`；pivot 位移到 `(3093.05,1392.45)`、scale 到 3.873 |

完整帧数组保留标准 Spine 3.8 `curve/c2/c3/c4` 插值字段，可直接用于
Unity 中重建相机载体或目标节点动画；表中范围只是便于快速阅读的摘要。

## 混合模式与挂点

- 128 个被引用 Spine 资源的 slot 混合统计：additive 4060、normal
  2969、multiply 203、screen 7。每个具体资源的计数在 JSON
  `spine_resources.*.slot_blend_modes`。
- 15 个粒子配置共 47 个 emitter：46 个使用
  `(GL_SRC_ALPHA, GL_ONE)` 加法混合；只有
  `heidemarie_30093_eff_attack_play1_target_pati3` 使用
  `(GL_ONE, GL_ONE_MINUS_SRC_ALPHA)` 预乘 Alpha。
- 顶层 EFFECT 的 `bone_type`、`type`、`slot`、偏移、缩放、旋转均已
  保留。当前所选 CFX 的 `attach` 字段全部为空；因此没有实样可以
  证明“CFX 内部命名挂点”的精确引擎语义。顶层 `ATTACH` 仍是确定字段，
  但多数顶层 `slot` 为空，表现为挂到主体/目标根上下文而非命名 slot。

## 当前缺失的粒子纹理

所有 CFX 中的 Spine 与 particle 配置文件都已找到；缺的是粒子配置
引用的 14 个共享 SCT 纹理，它们不在本角色导入或对应 External 子集中：

```text
particle/chain_01.sct
particle/cross_01.sct
particle/cross_02.sct
particle/cross_06.sct
particle/glass_04.sct
particle/glass_05.sct
particle/pati_01.sct
particle/pati_03.sct
particle/stone_06.sct
particle/ticle_01.sct
particle/ticle_02.sct
particle/tri_pati_01.sct
particle/ui_gacha_scene_star2.sct
particle/zodiac_star3.sct
```

这意味着 Spine 复合层已经可以按 JSON/atlas/PNG 组织，但粒子层在 Unity
中重建前还要从公共 `particle/` 资源包补提取并解码这些 SCT。

## 确定项、推断项与未解析项

确定项：

- 技能命令组、命令图父链、每个 EFFECT/镜头/node 的原始字段；
- 30 个被引用 CFX 及其全部 primitive 层；
- 128 个 Spine 资源、15 个 particle 配置全部存在；
- 13 个 camera/camera_path/node SCSP1U 全部结构闭合，并已解出关键帧；
- CFX 的资源名、动画名、delay、lifetime、x/y/z、缩放、旋转、loop、
  shader/attach 字段；
- 粒子 emitter 的时长、寿命、发射率、位置、速度、重力、尺寸、纹理与
  OpenGL 混合常量。

推断项：

- 报告中的“前景/后景”仅按 CFX 局部 z 正负命名；
- `start_ms_graph` 是沿配置父链累加 delay 的还原时间；
- CFX 未写 `ani` 时，优先选择资源中的 `animation` 动画，这是当前资源
  命名的一致规律，但不是客户端源码证明。

未解析项：

- camera/node 曲线的数值已经恢复，但这些坐标最终如何与客户端战斗相机
  的 formation、屏幕分辨率和单位换算组合，仍需在 Unity 预览中校准；
- 顶层 EFFECT 的 `duration=-1` 表示自然/引擎管理寿命，报告不强行猜成
  固定值；
- U1 的若干 CFX 粒子实例写有 `scale=-1`；原值已保留，但它在客户端中
  表示镜像还是特殊哨兵值，仍需运行时对照；
- 粒子的 `positionType`、`emitterType`、`burningType` 数字枚举均已保留，
  但没有在缺少客户端枚举定义时擅自命名；
- 相同 shake basename 可同时存在于 `camera/` 与 `effect/` 命名空间。
  SHAKE 节点按 camera 优先处理，但 JSON 保留所有匹配路径；
- 14 个共享粒子 SCT 纹理尚未提取。
