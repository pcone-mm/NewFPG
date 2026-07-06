# Unity 特效资源盘点导读

生成时间：2026-06-29

工程基线：`NewFPG`，Unity `6000.3.15f1`，URP `17.3.0`。

本目录是对当前工程中特效相关资源包的结构化盘点。生成器只读资源文件，不修改 `Assets` 里的任何特效本体。

## 先看结论

| 资源区 | 规模 | 最适合做什么 | 使用判断 |
|---|---:|---|---|
| `Assets/ThirdParty/VFX_Klaus` | 355 个 prefab | 战斗粒子、弹道三段链、命中、挥砍、元素爆发、充能、状态反馈 | 这是最主要的“拼装素材库”，优先从这里找战斗效果 |
| `Assets/ThirdParty/JMO Assets/Cartoon FX Remaster` | 65 个 prefab，192 个材质，141 张贴图 | 卡通命中、元素、文字弹字、火冰电、剑气拖尾、自然/烟雾氛围 | 适合给 VFX_Klaus 的基础效果加风格和识别度 |
| `Assets/ThirdParty/VolumetricFog2` | 10 个 prefab，5 个 demo scene，URP 雾系统资源 | 洞府、森林、神殿、远景雾、雾战争、局部雾体 | 这是场景氛围系统，不是一次性战斗粒子 |
| `Assets/ThirdParty/VolumetricLights` | 6 个 prefab，3 个 demo scene，URP 体积光资源 | 光束、尘埃粒子、火把/神殿光、遮挡光柱 | 和体积雾一起用，做环境层最划算 |
| `Assets/Art/SkillIndicators` | 12 个 prefab | 技能范围圈、锥形预警、轨迹、牵引线、锁定、倒计时危险圈 | 适合在真实伤害/特效前做 telegraph |
| `Assets/Art/Effect` + `Assets/Prefabs/Effects` | 1 个 prefab | 自制剑气落地爆发 | 已经贴近本项目玩法，应优先纳入技能系统 |
| `Assets/Art/Weapons/BajiaoshanFrames` | 15 张风刃/扇风帧图 | 芭蕉扇武器动画、风刃序列帧、UI 预览 | 可和风系 splash、剑气拖尾拼成武器技能 |
| `Assets/Rendering/DodgeSpeedLines` | 1 个 URP shader + RendererFeature 脚本 | 闪避、冲刺、速度感、瞬间爆发 | 屏幕空间效果，适合短促脉冲触发 |

## 可直接查的文件

| 文件 | 用法 |
|---|---|
| `effect_prefabs.csv` | 最重要。按 `Category`、`Tags`、`Placement`、`ParticleSystems` 筛 prefab |
| `effect_prefabs.md` | 人眼浏览用的完整 prefab 表 |
| `effect_assets.csv` | 包含材质、shader、贴图、demo scene、unitypackage 的全资产表 |
| `package_summary.csv` | 每个包/子目录的数量汇总 |
| `category_summary.csv` | 按实用分类统计 prefab 数量 |
| `Generate-EffectInventory.ps1` | 以后导入新包后可重跑 |
| `VFX_ASSEMBLY_NOTES.md` | 后续制作新特效时的拼装约定：默认从现有资源拆组件、改参数、组合成新 prefab |

## 按用途找

| 用途分类 | 数量 | 主要来源 | 代表例子 |
|---|---:|---|---|
| 弹道 / 射击链 | 129 | VFX_Klaus Shoot & Hit 两代，少量 Cartoon FX | `FX_Shoot_Arrow_muzzle/projectile/hit`、`FX_Shoot_EnergyBall_*`、`FX_Shoot_Laser_*` |
| 元素落点爆发 | 95 | VFX_Klaus Element Splash Vol.1-3 | `FX_splash_fire_floor`、`FX_splash_wind_air`、`FX_splash_thunder_floor`、`FX_splash_sword_floor` |
| 近战挥砍 / 拖尾 | 49 | VFX_Klaus Hit & Slash，Cartoon FX Sword Trails，自制剑气 | `FX_hit_*`、`CFXR4 Sword Trail *`、`FX_splash_sword_new_floor` |
| 元素/状态反馈 | 40 | Hyper Casual FX，Cartoon FX | 灰尘、火、电、毒、雨、闪光、状态字 |
| 魔法/能量 | 33 | Hyper Casual FX，Cartoon FX Magic Misc | `HCFX_MagicCircle_*`、`HCFX_Charging_*`、`CFXR3 Magic Aura A` |
| 命中反馈 | 27 | Cartoon FX，VFX_Klaus | 火/冰/电/光命中、小型 hit、地面 hit |
| 环境体积系统 | 16 | VolumetricFog2，VolumetricLights | `FogSubVolume`、`FogVolume2D`、`DustParticles`、demo 环境 prefab |
| 技能指示器 | 12 | SkillIndicators | `PF_IND_GroundCircle`、`PF_IND_Cone`、`PF_IND_TetherLine`、`PF_IND_ArcTrajectory` |
| 爆炸/大爆发 | 11 | Hyper Casual FX，Cartoon FX Explosions | `HCFX_Explosion_*`、`CFXR Explosion *` |
| 护盾/防御 | 1 | Cartoon FX Nature | `CFXR3 Shield Leaves A` |

## VFX_Klaus 怎么用

这是战斗特效主库。

| 子包 | 数量 | 重点 |
|---|---:|---|
| Hyper Casual FX | 100 prefab | buff、充能、灰尘、冲刺尘、跳跃尘、能量、爆炸、火、闪光、hit、魔法阵、拾取、毒、传送门、shine、水花 |
| Stylized Shoot & Hit | 64 prefab | 第一代射击链，含完整 `FX_Shoot_##` 和拆分的 `_muzzle/_projectile/_hit` |
| Stylized Shoot & Hit Vol.2 | 63 prefab | 更具体的武器/元素射击链：arrow、axe、bomb、card、dagger、energy ball、gas、hammer、ice、kunai、laser、obsidian、poison、shuriken |
| Stylized Hit & Slash | 33 prefab | 近战 hit 和 slash，适合刀剑、爪击、鱼鳍劈砍、扇面斩击 |
| Element Splash Vol.1-3 | 90 prefab | `air` / `floor` 两种空间语义，适合技能落点爆发、AOE、boss 攻击 |
| Timeline | 5 prefab | 演示/编排用，可参考如何摆放组合 |

使用原则：

- 射击类先按同名三段链拼：`_muzzle` 出手，`_projectile` 飞行，`_hit` 命中。
- `air` 版适合挂目标、半空、角色身上；`floor` 版适合地面落点、AOE、法阵中心。
- Readme 说明这些包大量依赖 ParticleSystem Custom Data 控制 dissolve、sharpness、distortion、emission、soft particle、secondary color。改色前先看 Custom Data，不要一上来复制一堆材质。

## Cartoon FX Remaster 怎么用

这是风格增强库。

| 子类 | 数量 | 适合 |
|---|---:|---|
| Sword Trails | 9 | 火/冰/普通剑气拖尾、挥砍残影 |
| Explosions | 6 | 卡通爆炸、烟爆、烟花 |
| Fire | 5 | 火焰、火墙、太阳、火属性命中 |
| Liquids | 5 | 水花、血液、泡泡 |
| Nature | 5 | 叶子命中、护盾叶、雨、风 |
| Texts | 9 | `BOOM`、`POW`、`SLASH`、`FROZEN`、`POISONED` 这类状态/漫画字 |
| Misc / Magic / Impacts / Electric / Light / Ice / Eerie | 26 | 小命中、毒雾、闪光、灵魂、光效等 |

建议用法：

- 给 VFX_Klaus 的技能加“读秒/命中表情”：例如 `FX_Shoot_*_hit` + `CFXR _POW_`。
- 做元素辨识：火技能叠 `CFXR3 Hit Fire B`，冰技能叠 `CFXR3 Hit Ice B`，毒技能叠 `CFXR4 _POISONED_`。
- 近战技能可以把 `CFXR4 Sword Trail` 和 `FX_splash_sword_new_floor` 组合。

## 环境系统怎么用

`VolumetricFog2` 和 `VolumetricLights` 要当成场景系统处理。

- 体积雾适合洞府、森林、远景雾、雾战争、局部雾体。
- 体积光适合神殿光柱、火把光束、尘埃粒子、遮挡透光。
- 两者都要求 URP，你的工程已经是 URP，基础条件匹配。
- 不建议把它们当作普通 one-shot prefab 到处 Instantiate。先确认 URP RendererFeature、Volume、LayerMask、深度贴图等配置。

## 项目自有资源

| 资源 | 内容 | 建议 |
|---|---|---|
| `FX_splash_sword_new_floor.prefab` | 20 个 ParticleSystem 子节点，包含 crack、down_line、impact、dust、stone、energy、smoke 等层 | 很适合做重击落地、剑气落点、boss 技能收尾 |
| `PF_IND_*` 指示器 | 圆、锥、线、轨迹、锁定、倒计时、危险圈、放置 ghost | 技能前摇阶段显示，真实伤害帧触发 VFX_Klaus/Cartoon FX |
| `BajiaoshanFrames` | 12 帧风刃 + sprite sheet + preview | 和风系 `FX_splash_wind_air/floor`、CFXR Wind Trails 拼成芭蕉扇技能 |
| `DodgeSpeedLines` | URP 屏幕速度线 RendererFeature/Volume/shader | 闪避、冲刺、重击出手瞬间短促触发 |

## 推荐拼装配方

| 目标 | 材料 | 顺序 |
|---|---|---|
| 剑/扇重击 | `FX_splash_sword_new_floor` + `CFXR4 Sword Trail` + `DodgeSpeedLines` | 先调武器拖尾，再在命中点生成地面爆发，最后加 0.1-0.25 秒速度线 |
| 弹道技能 | 同名 `_muzzle` + `_projectile` + `_hit` | 出手点生成 muzzle，飞行物用脚本移动，碰撞时生成 hit |
| 地面 AOE | `PF_IND_GroundCircle` 或 `PF_IND_Cone` + `FX_splash_*_floor` | 前摇显示范围，确认帧闪一下，伤害帧生成 floor splash |
| 护盾格挡 | 小型 `Hit/Impact` + `CFXR3 Shield Leaves A` | 被击中时叠 hit 和发光脉冲 |
| 芭蕉扇风刃 | `BajiaoshanFrames` + `CFXR4 Wind Trails` + `FX_splash_wind_air/floor` | 武器本体播帧动画，风刃飞行用 air，落地/命中用 floor |
| 洞府/森林氛围 | VolumetricFog2 preset + VolumetricLights + CFXR Ambient/Wind/Rain | 先铺雾，再加体积光，再少量环境粒子，避免压过战斗特效 |

## 抽查记录

我用 Unity MCP 抽查了几个关键 prefab，和表格里的静态计数一致：

- `Assets/Art/Effect/FX_splash_sword_new_floor.prefab`：20 个对象，20 个 `ParticleSystem`。
- `Assets/ThirdParty/VFX_Klaus/Prefabs/Stylized Shoot & Hit Vol.2/FX_Shoot_EnergyBall_projectile.prefab`：5 个对象，5 个 `ParticleSystem`。
- `Assets/Art/SkillIndicators/Temporary/Prefabs/PF_IND_GroundCircle.prefab`：`MeshRenderer` + `LineRenderer`，适合技能范围圈。
- `Assets/ThirdParty/VolumetricLights/Resources/Prefabs/DustParticles.prefab`：单个 `ParticleSystem`，属于体积光尘埃配套。

## 下一步建议

1. 先把 `effect_prefabs.csv` 按 `Category` 过滤，挑每类 3-5 个最常用 prefab 做收藏清单。
2. 给技能系统定义固定 VFX 插槽：`Telegraph`、`Cast/Muzzle`、`Projectile`、`Impact`、`Loop`、`ScreenPulse`。
3. 做一张测试场景，把候选 prefab 放进去统一看比例、亮度、持续时间和 URP 后处理效果。
4. 对常用 VFX 做二次 prefab 包装，统一生命周期、自动销毁、缩放、朝向、音效和屏幕震动。

## 后续协作备注

以后如果要我帮你生成或拼装新的特效 prefab，默认会以这份盘点为参考，优先从工程现有特效中拆组件、复制变体、调整粒子/材质/贴图/序列帧/屏幕后处理参数，再组合成新的项目自有 prefab。具体规则见 `Docs/EffectInventory/VFX_ASSEMBLY_NOTES.md`。
