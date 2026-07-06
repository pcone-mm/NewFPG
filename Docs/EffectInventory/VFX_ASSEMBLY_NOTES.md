# 后续特效拼装约定

这份备注用于之后在本工程里制作新特效时遵守。除非用户明确要求“从零做一套新资源”，默认都从现有特效资源包里拆组件、改参数、重新组合成新的 prefab。

## 长期协作记录

用户后续让我“拼接/组合/生成新的特效 prefab”时，默认使用这份特效清单作为参考，不从零新造视觉资源。新的特效应优先从现有特效 prefab 的子层级、粒子子节点、材质、贴图、Sprite、Mesh、Shader 和脚本组件中挑选可复用部分，复制到项目自有目录后调整参数并组装成新的 prefab。第三方资源包原始 prefab 只作为来源，不直接覆盖修改，也不默认把整个源 prefab 当作一个嵌套块直接拼进去。

## 本次临时武器攻击特效记录

新 prefab 统一放在 `Assets/Art/Effect/TemporaryWeaponAttacks/`。这些 prefab 都是项目自有根节点，内部由现有特效 prefab 的指定子层级复制组合而成，不再整包嵌套源 prefab。后续可继续拆组件、调缩放、调颜色、调发射参数。

| 武器 / 入口 | 新 prefab | 用途 | 复用来源 |
|---|---|---|---|
| 桃木剑 / FlyingSword / TargetLock | `PF_TempAttack_TaomuSword_Release.prefab` | 释放 / 落剑起手 | `FX_slash_05` + `FX_splash_sword_new_floor` |
| 桃木剑 / FlyingSword / TargetLock | `PF_TempAttack_TaomuSword_Hit.prefab` | 命中 / 剑击落点 | `FX_hit_08` + `FX_splash_sword_floor` |
| 扇子 / MoonDao 调试入口 | `PF_TempAttack_Fan_Release.prefab` | 释放 / 风刃出手 | `FX_splash_wind_air` + `CFXR4 Wind Trails` |
| 扇子 / MoonDao 调试入口 | `PF_TempAttack_Fan_Hit.prefab` | 命中 / 风压落点 | `FX_splash_wind_floor` + `FX_hit_05` |
| 护心镜 / RitualDagger 调试入口 | `PF_TempAttack_HeartMirror_Release.prefab` | 释放 / 护盾展开 | `HCFX_Shine_03` |
| 护心镜 / RitualDagger 调试入口 | `PF_TempAttack_HeartMirror_Hit.prefab` | 命中 / 护盾冲击 | `HCFX_Hit_06` + `CFXR3 Shield Leaves A (Lit)` |

本次实际复制的子层级：

- 桃木剑释放：`highlight01`、`sub`、`impact_front`、`impact_floor`、`impact_circle`、`energy_circle`、`energy_particle`、`dust_spread`、`smoke_up_center`
- 桃木剑命中：`highlight`、`impact`、`splatter`、`impact_floor`、`impact_particle`、`energy_particle_pop`、`dust_center`、`stone_pop`
- 扇子释放：`sharp_circle`、`particle_bright`、`leaf`、`smoke_side`、`smoke_spread`、`CFXR4 Wind Trails` 根粒子层
- 扇子命中：`sharp_circle`、`particle_bright`、`leaf`、`smoke_spread`、`impact`、`impact_highlight`、`dust`
- 护心镜释放：`light_long`、`twinkle`
- 护心镜命中：`mesh_circle`、`glow`、`particle`、`sharp_out`、`CFXR3 Shield Leaves A (Lit)` 根粒子层、`Orbital`

已绑定到：

- `Assets/Settings/Forging/Weapons/WPN_TaomuSword.asset`
- `Assets/Settings/Forging/Weapons/WPN_Fan.asset`
- `Assets/Settings/Forging/Weapons/WPN_HeartMirror.asset`
- `Assets/Settings/Forging/Blueprints/*.asset` 对应图纸镜像
- `Assets/Settings/Forging/weapon_blueprints.json`
- `Assets/Settings/Forging/forging_catalog.json`
- `Assets/Settings/Combat/FlyingSword.asset`
- `Assets/Settings/Combat/HudDebug/HUD_Debug_FlyingSword.asset`
- `Assets/Settings/Combat/HudDebug/HUD_Debug_TargetLock.asset`
- `Assets/Settings/Combat/HudDebug/HUD_Debug_MoonDao.asset`
- `Assets/Settings/Combat/HudDebug/HUD_Debug_RitualDagger.asset`

## 默认原则

1. 先查 `Docs/EffectInventory/effect_prefabs.md` 和 `Docs/EffectInventory/effect_prefabs.csv`，按用途、分类、路径、组件数量找候选 prefab。
2. 优先复用现有资源：粒子子节点、材质、贴图、shader、序列帧、LineRenderer、SpriteRenderer、屏幕后处理和体积雾/光系统。
3. 新特效以“组合 prefab”为主：从已有 prefab 拷贝/嵌套/拆分出组件，调整颜色、缩放、持续时间、发射速率、朝向、层级和生命周期。
4. 不随意改第三方原始 prefab。需要变体时，复制到项目自有目录后再改。
5. 新 prefab 优先放在项目自有目录，例如 `Assets/Prefabs/Effects/` 或后续约定的技能/角色特效目录。
6. 拼装完成后要能说明来源：用了哪些原始 prefab、哪些材质/贴图、改了哪些关键参数。

## 推荐查找顺序

| 需求 | 优先查找 |
|---|---|
| 弹道、飞行物、远程技能 | VFX_Klaus `Stylized Shoot & Hit` / `Stylized Shoot & Hit Vol.2` |
| 技能落点、AOE、元素爆发 | VFX_Klaus `Stylized Element Splash Vol.1-3` |
| 近战挥砍、刀光、剑气、拖尾 | VFX_Klaus `Stylized Hit & Slash`，Cartoon FX `Sword Trails`，`FX_splash_sword_new_floor` |
| 命中、小爆点、属性反馈 | Cartoon FX `Impacts` / `Fire` / `Ice` / `Electric`，VFX_Klaus `Hit` 类 |
| 充能、法阵、拾取、状态环绕 | VFX_Klaus `Hyper Casual FX`，Cartoon FX `Magic Misc` |
| 技能预警、范围圈、锁定、轨迹 | `Assets/Art/SkillIndicators` |
| 护盾、防御、格挡 | Cartoon FX `Shield Leaves`，小型 hit 反馈 |
| 风刃、芭蕉扇、扇形攻击 | `BajiaoshanFrames`，CFXR Wind Trails，VFX_Klaus wind splash |
| 闪避、冲刺、速度感 | `Assets/Rendering/DodgeSpeedLines` |
| 场景氛围、洞府、森林、神殿光束 | `VolumetricFog2` + `VolumetricLights` |

## 组合模板

| 插槽 | 作用 | 常用来源 |
|---|---|---|
| `Telegraph` | 前摇范围提示、目标锁定、落点预警 | `PF_IND_*` |
| `Cast/Muzzle` | 出手瞬间、武器尖端、施法点爆发 | `_muzzle`、flash、small hit、magic circle |
| `Projectile` | 飞行段、风刃、能量球、箭矢、激光 | `_projectile`、Bajiaoshan frame/sheet |
| `Impact` | 命中、地面爆发、受击反馈 | `_hit`、`FX_splash_*_floor/air`、CFXR hit |
| `Loop` | 持续护盾、充能、状态、区域效果 | shield、aura、magic circle、portal、poison |
| `ScreenPulse` | 屏幕速度线、闪避、重击瞬间 | `DodgeSpeedLines` |
| `Environment` | 场景雾、体积光、尘埃、氛围粒子 | `VolumetricFog2`、`VolumetricLights`、CFXR ambient |

## 操作约束

- 先在副本/新 prefab 上改，不直接覆盖资源包原件。
- 如果使用第三方特效作为基础，保留原路径记录，方便以后回溯。
- 参数调整优先从易控项开始：Transform、ParticleSystem Main/Emission/ColorOverLifetime、Renderer Material、Custom Data、Start Lifetime、Start Speed、Simulation Space、Sorting/Facing。
- 射击链尽量保持同名三件套：`_muzzle`、`_projectile`、`_hit`。
- `air` 版通常挂目标/空中点，`floor` 版通常贴地/落点/范围中心。
- 体积雾和体积光属于场景系统，不默认作为一次性 prefab 乱实例化。
- 拼装后至少抽查层级和关键组件数量，必要时在 Unity 中打开 prefab 验证。

## 后续协作默认话术

当用户要求“做一个新特效/拼一个技能特效/改一个特效”时，默认执行：

1. 明确特效目标：类型、元素、触发时机、挂点/落点、是否循环、是否需要预警。
2. 查 `effect_prefabs.csv` / `effect_prefabs.md` 选 2-5 个候选源。
3. 给出或直接执行组合方案：来源 prefab、拼装插槽、关键参数修改。
4. 生成新的项目自有 prefab。
5. 抽查并记录来源与修改点。
