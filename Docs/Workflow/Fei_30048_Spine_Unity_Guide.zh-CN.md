# 绯（30048）Spine / Unity 使用与导入指南

本文记录绯（内部 ID `30048`，资源前缀 `fei_30048`）的本地学习流程、现有交付物、Spine 3.8 预览方法，以及在 Unity 中重建模型和 12 组战斗演出的入口。

> 本文只描述从已安装客户端复制资源后进行的离线研究。游戏安装目录始终只读；提取物、转换结果、Spine 工程和本地 Spine Runtime 都是 local-only。不要将它们提交到 Git、公开分发或用于商业发布。使用 Spine Editor 与 spine-unity 前，还需要分别确认原游戏资源和 Esoteric Software Runtime 的有效授权。

## 一分钟开始

### 在 Spine 3.8 中看动画

用 Spine 3.8 打开以下三个已经整理好的工程：

| 内容 | 精确路径 |
|---|---|
| 主战斗模型 | `D:\Unity\NewFPG\External\CZN\Fei_30048\SpineProjects\Main\Fei_30048_Main.spine` |
| BattleReady 模型 | `D:\Unity\NewFPG\External\CZN\Fei_30048\SpineProjects\BattleReady\Fei_30048_BattleReady.spine` |
| U4 攻击代表特效 | `D:\Unity\NewFPG\External\CZN\Fei_30048\SpineProjects\U4AttackFX\Fei_30048_U4AttackFX.spine` |

本机 Spine 入口为：

```text
F:\tool\Spinepro_3.8.75学习版\Spine pro 3.8.75\Spine.exe
```

打开工程后切到 `ANIMATE`，在 Tree 的 `Animations` 下选择动画，再按空格或 Dopesheet 的播放按钮。U4 代表特效的动画名是 `animation`。

不要直接打开或把 `.scsp1u.bytes` 改名为 `.skel`；它是游戏私有运行时数据，不是标准 Spine binary。

### 在 Unity 中看模型与技能

工程目录是 `D:\Unity\NewFPG`。等待 Unity 编译完成后，先执行：

```text
Tools > CZN > Fei 30048 > Build Complete Import
```

这个菜单会幂等地生成/刷新模型 Prefab、模型预览场景、12 个 `SkillSequence`、12 条 Timeline、技能组合 Prefab 和技能预览场景。它不会修改游戏安装目录。

然后打开：

```text
Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_SkillPreview.unity
```

进入 Play 后默认选择第 3 项 U1。操作方式：

- `←` / `→`：上一个或下一个组合；
- `R`：从头硬重播当前组合；
- `Space`：暂停或继续；暂停只冻结当前技能，不清场；
- 数字键 `1–9`：选择第 1–9 项；
- 数字键 `0`：选择第 10 项 Fatal；
- 画面按钮 `1–12`：可直接选择全部组合。

技能采用单次播放：自然结束时触发 `PlayableDirector.stopped`，运行时 Spine/粒子层与 standby 全部清场，主角色切回循环的 `b_idle`。Unity 的 `PlayableDirector.state` 只有 `Playing/Paused`，停止后实测为 `Paused`；它不会自动从头循环，需要再次观看时按 `R`。

只想并排查看主模型和 BattleReady 时，打开：

```text
Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_Preview.unity
```

如果上述 `Preview` 场景还未出现，表示生成菜单尚未成功执行；先查看 Unity Console，而不要手工创建同名空场景。

## 本次恢复范围

资源来自客户端主索引 `main`。当前审计没有发现 `shadow` 中存在绯的精确覆盖或相关战斗覆盖，因此没有混用热更新记录。

| 项目 | 已核实结果 |
|---|---:|
| 严格依赖闭包 | 411 条，411/411 可定位、读取和解压 |
| 可视 Spine 三件套 | 115 组：主模型 1、BattleReady 1、特效 113 |
| Ancillary SCSP | 7 个：camera 4、camera path 1、node 2 |
| 可视 SCSP1U 转换 | 115/115 成功，失败 0 |
| Ancillary 转换 | 7/7 成功，失败 0 |
| 可视动画 | 219 个，其中主模型 48、BattleReady 3 |
| Ancillary 动画 | 7 个 |
| 可视 Timeline records | 44,392 条 |
| Ancillary Timeline records | 11 条 |
| Unity Spine 集成 | 115/115 个 `SkeletonDataAsset`、115/115 个 Atlas 均可加载；共 219 个动画 |
| Unity 交付资产 | 3 个 Prefab、2 个场景、12 个 SkillSequence、12 条 Timeline |
| CFX / primitive | 35 个 / 137 个 |
| 粒子配置 / cue / 精确贴图 | 16 个 / 53 个 / 4 张 |
| 技能规格 / cue 总数 | 12 组；actor 25、Spine 113、particle 53、transform 8、camera zoom 3、marker 48 |
| unresolved / approximation | 4 条，均保留在 SkillSequence 与报告中 |

四张由配置精确引用并已解码的粒子贴图是：

```text
Assets/Imported/CZN/Fei_30048/SpineSource/particle/blur_ticle_03.png
Assets/Imported/CZN/Fei_30048/SpineSource/particle/dust_02.png
Assets/Imported/CZN/Fei_30048/SpineSource/particle/pati_03.png
Assets/Imported/CZN/Fei_30048/SpineSource/particle/pati_03_h_long2.png
```

`model_setting/30048.setting` 在 `main` 和 `shadow` 索引中都不存在；当前已选配置没有引用它，因此这不是依赖闭包中的缺失边。

## 目录与入口

| 用途 | 项目路径 |
|---|---|
| 模型预览场景 | `Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_Preview.unity` |
| 技能组合预览场景 | `Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_SkillPreview.unity` |
| 主模型 Prefab | `Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_Main.prefab` |
| BattleReady Prefab | `Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_BattleReady.prefab` |
| 技能组合 Prefab | `Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_SkillComposer.prefab` |
| 12 个技能数据 | `Assets/Imported/CZN/Fei_30048/Preview/SkillCompositions/Skills` |
| 12 条 Timeline | `Assets/Imported/CZN/Fei_30048/Preview/SkillCompositions/Timelines` |
| 生成的预览材质 | `Assets/Imported/CZN/Fei_30048/Preview/SkillCompositions/Generated` |
| 主/BattleReady canonical JSON | `Assets/Imported/CZN/Fei_30048/SpineSource/model` |
| 113 组可视特效 | `Assets/Imported/CZN/Fei_30048/SpineSource/effect` |
| camera/path/node JSON | `Assets/Imported/CZN/Fei_30048/AncillarySource` |
| SRMD/BRMD/CFX/粒子配置 | `Assets/Imported/CZN/Fei_30048/Configs` |
| 审计、转换与生成报告 | `Assets/Imported/CZN/Fei_30048/Metadata` |
| 原始只读副本与 Spine 工程 | `External/CZN/Fei_30048` |
| 通用离线转换工具 | `Tools/CznResourcePipeline` |

四个 Editor 菜单分别是：

```text
Tools > CZN > Fei 30048 > Build Complete Import
Tools > CZN > Fei 30048 > Build Model Prefabs and Preview
Tools > CZN > Fei 30048 > Build Skill Compositions
Tools > CZN > Fei 30048 > Validate Skill Import
```

平时优先运行 `Build Complete Import`。中间两个菜单用于只重建模型预览，或只重新解析 SRMD/BRMD、CFX 和粒子配置；最后运行 `Validate Skill Import` 可重做 12 技能的静态引用、边界采样、单次播放清场和 `R` 重播审计。生成器会拒绝覆盖无关的未保存场景；遇到提示时先保存或关闭当前场景。

## 12 组技能规格

编号顺序同时也是技能预览菜单中的顺序。角色动作来自 `30048.srmd.json`，不是按文件名猜测。

| 编号 | Skill ID | 时长 | 显示名 | SRMD phase | 主模型动画 |
|---:|---|---:|---|---|---|
| 1 | `attack_play1` | 1.250s | 普通攻击一 | `attack_play1` | `attack_play1` |
| 2 | `attack_play2` | 1.250s | 普通攻击二 | `attack_play2` | `attack_play2` |
| 3 | `u1_buff` | 2.416s | U1 增益 | `u1_buff_ready` → `u1_buff_play` | 同名 ready → play |
| 4 | `u2_buff` | 2.867s | U2 增益 | `u2_buff_ready` → `u2_buff_play` | 同名 ready → play |
| 5 | `u3_buff` | 2.417s | U3 增益 | `u3_buff_ready` → `u3_buff_play` | 同名 ready → play |
| 6 | `u4_attack` | 2.9164s | U4 攻击 | `u4_attack_ready` → `u4_attack_play` → `u4_attack_end` | 同名 ready → play → end |
| 7 | `u5_buff` | 3.117s | U5 增益 | `u5_buff_ready` → `u5_buff_play` | 同名 ready → play |
| 8 | `ug_attack` | 5.333s | UG 终结技 | `ug_attack` | `ug_attack_1` |
| 9 | `ux_buff` | 16.466s | UX 特殊增益 | `ux_buff` | `idle_to_b_idle` → `ux_buff_1` |
| 10 | `fatal` | 6.000s | Fatal 连段 | `fatal_intro` → `fatal1` → `fatal2` → `fatal3` | 同名四段 |
| 11 | `enter` | 1.133s | 战斗入场 | `enter_ready` → `enter_play` → `enter_end` | 同名三段 |
| 12 | `victory` | 2.667s | 胜利动作 | `victory_ready` → `victory` | 同名两段 |

主模型还含有 `ug_attack_2` 和 `ux_buff_2`，但当前 12 组预览严格采用上述已选 SRMD command 图，不因动画存在就擅自追加 phase。

每个生成的 `CznSpineSkillSequence` 会保存：

- 角色 phase 与动画；
- 多层 Spine 特效、动画名、前后排序和延迟；
- 粒子发射器参数及贴图；
- `SELF`、`TARGET`、`CENTER`、`SCREEN` 等锚点；
- camera/node transform 与 zoom；
- HIT、STOP、颜色、后处理等可诊断 marker；
- 无法证明绑定关系的 unresolved 条目。

对应 Timeline 只有一条 `CZN Skill Composition` 轨道，由 `CznSpineSkillPlayableAsset` 确定性求值同一份 `CznSpineSkillSequence`，避免 Timeline 与直接播放形成两套不一致的数据。

最终组合共有 actor 25、Spine 113、particle 53、transform 8、camera zoom 3、marker 48 个 cue。4 条 unresolved/approximation 诊断项分别是：U4 的 1 条 bone/slot attachment 按 root 锚点近似、UG 的 2 条 `FRONT` → screen foreground 映射，以及 UX 的 1 条 CUTIN 未绑定；它们不是丢失的 Unity 引用。

## 在 Spine 中研究

### 主模型、BattleReady 与 U4 特效

现成工程的目录结构是：

```text
SpineProjects/<Project>/
├─ *.spine
├─ *.json
├─ atlas-pages/
└─ images/
```

`images` 是从 Atlas 解出的单图，`.spine` 工程通过它们显示 attachment。不要单独移动 `.spine`、`images` 或 `atlas-pages`，否则会出现缺图。

主模型共有 48 个动画：

<details>
<summary>展开全部主模型动画</summary>

```text
attack_end
attack_play1
attack_play2
attack_ready
b_idle
b_idle_to_idle
buff_play
buff_ready
camping
collapse_idle
cure_play
cure_ready
death
death_ready
debuff_play
debuff_ready
defense_play
defense_ready
enter_end
enter_play
enter_ready
fatal1
fatal2
fatal3
fatal_intro
groggy
hit
idle
idle_to_b_idle
move
stop
u1_buff_play
u1_buff_ready
u2_buff_play
u2_buff_ready
u3_buff_play
u3_buff_ready
u4_attack_end
u4_attack_play
u4_attack_ready
u5_buff_play
u5_buff_ready
ug_attack_1
ug_attack_2
ux_buff_1
ux_buff_2
victory
victory_ready
```

</details>

BattleReady 有 3 个动画：

```text
b_idle
card_attack
card_casting
```

UG 的 BattleReady 演出由 `30048_battle_ready.brmd.json` 中的 `ug_attack` 图控制，它播放 `b_idle` 并叠加 standby node 变换；不要因为主模型存在 `ug_attack_1/2` 就把它们绑定到 BattleReady。

### 把任意特效导入 Spine

Unity 使用的 `.json + .atlas.txt + .png` 是 canonical Spine 3.8 数据。若要把某个其他特效整理成独立 `.spine` 工程，先复制三件套并解开 Atlas，再导入 JSON。以下以 `fei_30048_u1_buff_play_f1` 为例：

```powershell
$spine = "F:\tool\Spinepro_3.8.75学习版\Spine pro 3.8.75\Spine.com"
$source = "D:\Unity\NewFPG\Assets\Imported\CZN\Fei_30048\SpineSource\effect"
$name = "fei_30048_u1_buff_play_f1"
$work = "$env:TEMP\FeiSpine\$name"

New-Item -ItemType Directory -Force "$work\atlas-pages", "$work\images" | Out-Null
Copy-Item "$source\$name.json" "$work\$name.json"
Copy-Item "$source\$name.atlas.txt" "$work\atlas-pages\$name.atlas"
Copy-Item "$source\$name.png" "$work\atlas-pages\$name.png"

& $spine -i "$work\atlas-pages" -o "$work\images" -c "$work\atlas-pages\$name.atlas"
& $spine -i "$work\$name.json" -o "$work\$name.spine" -r $name
```

完成后打开 `%TEMP%\FeiSpine\<特效名>\<特效名>.spine`。Spine Editor 可能标准化 mesh/deform 的内部数据，因此可以另存 `.spine` 进行研究，但不要用它反导出的 JSON 覆盖 `SpineSource` 中由转换器直接生成的 canonical JSON。

## 在 Unity 中查看与复用

### 查看单个模型

运行生成菜单后，打开 `Fei_30048_Preview.unity`。主模型默认循环 `idle`，BattleReady 默认循环 `b_idle`。也可以把以下 Prefab 拖入自己的测试场景：

```text
Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_Main.prefab
Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_BattleReady.prefab
```

选中对象，在 `SkeletonAnimation` 组件中修改 `Animation Name`。Scene 静态视图不会持续推进动画，需要进入 Play，或用专门的 Spine Inspector 预览入口。

### 查看单层特效

在以下目录搜索目标的 `_SkeletonData.asset`：

```text
Assets/Imported/CZN/Fei_30048/SpineSource/effect
```

把资源拖入 Scene 并创建 `SkeletonAnimation`，动画名通常为 `animation`。单层预览只适合检查 mesh、attachment 和混合；完整技能必须按 CFX/SRMD 的延迟、锚点、排序、相机和粒子组合。

### 在自己的场景中使用完整组合

最简单的做法是拖入：

```text
Assets/Imported/CZN/Fei_30048/Preview/Prefabs/Fei_30048_SkillComposer.prefab
```

如果接入自己的战斗系统，保留 `CznSpineSkillPlayer`，并让业务逻辑选择 `SkillCompositions/Skills` 下的 `CznSpineSkillSequence`；也可以让 `PlayableDirector` 播放 `SkillCompositions/Timelines` 下对应的 `.playable`。

场景中的关键对象/组件是：

```text
Fei 30048 Skill Composition Preview
├─ Self Anchor
├─ Target Anchor
├─ Standby Anchor
├─ Center Anchor
├─ Screen Anchor
├─ Runtime Effects
├─ Camera Shake Root
├─ CznSpineSkillPlayer
├─ PlayableDirector
└─ CznSpineSkillPreviewMenu
```

### 正确重播，避免武器或剑只出现第一次

不能只再次调用 `SetAnimation`。某些 attachment timeline 会在末帧把武器设为 `null`，而下一次播放开头未必再次写入非空 attachment。当前组合播放器只在按 `R` 或切换技能时执行硬重置：

```text
AnimationState.ClearTracks()
→ Skeleton.SetToSetupPose()
→ AnimationState.SetAnimation(...)
→ 当帧 Apply/Update
```

在本项目中，`SkeletonAnimation.ClearState()` 封装了清轨道和恢复 Setup Pose。技能菜单的 `R` 会先调用 `RestartSequence`，再重建并从 `0` 求值 PlayableGraph；每个运行时 Spine 层和粒子层也会分别 `ResetForReplay()`。如果自己写播放器，应沿用这条重播路径，不要缓存并复用已经到达末态的 `TrackEntry`。

自然播放完成走的是另一条路径：`PlayableDirector.stopped → 清除运行时 Spine/粒子层 → 隐藏 standby → 主角色 b_idle（Loop）`。`Space` 暂停不会触发这条完成清场路径；继续后仍从同一技能、同一进度播放。

## 当前精度边界

以下内容已经由配置或转换数据恢复：

- SRMD phase、前驱依赖、延迟与 `wait_until_end`；
- CFX Spine 层、动画名、前后排序、锚点、偏移、旋转和普通缩放；
- 16 个粒子配置生成的 53 个 cue，其发射、运动、颜色、生命周期和 4 张精确引用贴图；53/53 均按原 blend 配置标记为 additive，并绑定项目内的 additive particle shader；
- 4 个震屏 camera、1 个 UG camera path、2 个 UG node 的关键帧；
- BattleReady standby 的 BRMD 可见性和 node transform；
- 技能 marker 与 unresolved 诊断信息。

以下内容是近似或尚未像素级恢复：

- CZN/XCent 的 `FRONT` 层在 Unity 预览中映射到 `SCREEN` 前景层；
- UG standby 的比例和摆位根据 BRMD 坐标做学习预览近似；
- ancillary 多骨骼数据目前只采样主 `cam`、`camera` 或 `node` 骨，辅助 `node/pivot` 仍保留在 canonical JSON；
- ancillary 的非 stepped Spine Bezier 在组合预览中按线性插值；
- 负数 CFX scale 可能是哨兵值或引擎镜像语义，当前使用正数 fallback 并保留 unresolved，而不猜测；
- 原生 mask、自定义 shader、radial RGB blur、speed blur、hit-stop 和 color-blend 目前保留为 Timeline 诊断 marker，不是像素级复刻；
- 本指南覆盖视觉演出重建，不代表已恢复原游戏音频播放链。

唯一经严格依赖审计确认的未解决绑定是 UX `CUTIN`：源节点既没有 `id`，也没有 `file_name`。以下名字候选没有足够配置证据，所以没有自动绑定：

```text
cutin/fhd/30048.webp
cutin/collapse_fhd/collapse_30048.webp
effect/fei_30048_ux_illust.cfx
```

这项应继续显示为 unresolved；除非后续找到脚本或运行时证据，否则不要仅按角色 ID 猜选 Cut-in。

## 验证状态

离线转换、Unity 集成、12 技能边界采样，以及代表技能的单次完成清场、暂停/继续和 `R` 重播均已完成。以下是 Unity `6000.3.15f1` 中的最终实测，不再只是文件存在性检查。

| 检查项 | 当前状态 |
|---|---|
| SSRA → SSRC 定位、解压和原始大小 | 411/411 通过 |
| Atlas/PNG/SCSP1U 配对 | 115 组可视三件套完整 |
| 可视转换 | 115/115，通过；body end 全部有效 |
| Ancillary 转换 | 7/7，通过；body end 全部有效 |
| Unity SkeletonData / Atlas 加载 | 115/115、115/115；加载失败 0，加载动画总数 219 |
| Atlas 与材质引用 | 材质主贴图空引用 0；2 个 SkeletonData 复用内容等价的 Atlas，均确认可加载，不是缺失引用 |
| Unity 交付物 | 3 个 Prefab、2 个场景、12 个 SkillSequence、12 条 Timeline |
| 组合 cue | actor 25、Spine 113、particle 53、transform 8、camera zoom 3、marker 48、unresolved/approximation 4 |
| 粒子材质 | 53/53 为 additive，并绑定项目 additive particle shader；4 张精确贴图均已绑定 |
| 全技能边界采样 | 597 个样本，Errors 0、Warnings 0 |
| U4 单次完成 / `R` 重播 | 自然完成清场 3/3；固定 `1.016s` 的 `R` 重播签名稳定 3/3 |
| UG 单次完成 / `R` 重播 | 自然完成清场 3/3；固定 `2.4s` 的 `R` 重播签名稳定 3/3 |
| `Space` 暂停/继续 | 暂停时保持 `u4_attack`、`u4_attack_play` 且 `completed=false`；继续后仍播放同一技能 |
| Unity Console | 最终验证 Error 0、Warning 0 |
| 验证截图 | 模型、U4 `1.016s`、UG `2.4s`、完成后 `b_idle` 共 4 张，见下文 |

### U4 重播稳定性

U4 时长为 `2.9164s`，固定在 `1.016s` 采样。`runtimeObjects` 是为本次技能创建的运行时层总数；attachment 签名格式为 `哈希:attachment 数`。

| 测试 | 结果 |
|---|---|
| 首次固定采样 | runtimeObjects 18、active Spine 5、active particles 4、签名 `BC31D60E672116E3:321` |
| 按 `R` 硬重播 | 3/3 次在 `1.016s` 得到相同对象数、活动层数与 attachment 签名 |
| 自然播放完成 | 3/3 次均为 `completed=true`、actor=`b_idle`、actorLoop=`true`、runtimeObjects/Spine/particles=`0/0/0`、standby=`false` |

这证明 U4 的武器/attachment 在手动重播时完整恢复，而单次播放结束后不会把 18 个运行时对象残留在场景中。完成态显示在循环 `b_idle`，不会自动再次播放 U4。

### UG 重播、standby 与相机

UG 时长为 `5.333s`，固定在 `2.4s` 采样：

| 测试 | 结果 |
|---|---|
| 首次固定采样 | runtimeObjects 28、active Spine 9、active particles 0、签名 `42B0303EECF93DCC:130` |
| 按 `R` 硬重播 | 3/3 次在 `2.4s` 得到相同对象数、活动层数与 attachment 签名 |
| 自然播放完成 | 3/3 次均为 `completed=true`、actor=`b_idle`、actorLoop=`true`、runtimeObjects/Spine/particles=`0/0/0`、standby=`false` |

UG 的关键状态边界也已单独采样：

- standby 在 `0.499s` 为 `True`，在 `0.501s` 为 `False`；
- actor 在技能内的 `1.5s` 仍为 `ug_attack_1`；只有自然播放完成后才进入循环 `b_idle`；
- 该轮测试 Console 为 Error 0、Warning 0。

相机正交尺寸实测：

| 时间 | `Camera.orthographicSize` |
|---:|---:|
| `1.130s` | 5.200 |
| `1.930s` | 2.501 |
| `2.097s` | 1.650 |
| `2.263s` | 3.662 |

这些值证明 SRMD camera zoom 与 ancillary camera scale 已共同作用于预览相机，而不是只生成了未执行的配置记录。

### `Space` 暂停不清场

U4 播放中按 `Space` 后，`PlayableDirector` 状态为 `Paused`，当前 skill 仍为 `u4_attack`，actor 仍为 `u4_attack_play`，并且 `completed=false`。再次按 `Space` 后状态恢复为 `Playing`，skill 仍为 `u4_attack`。因此暂停不会误触发完成清场，也不会把技能重置到 `b_idle`。

## 验证截图

| 内容 | 路径 |
|---|---|
| 模型预览 | `Assets/Screenshots/CZN/Fei_30048/Fei_30048_ModelPreview.png` |
| U4 `1.016s` | `Assets/Screenshots/CZN/Fei_30048/Fei_30048_U4_1p016s.png` |
| UG `2.4s` | `Assets/Screenshots/CZN/Fei_30048/Fei_30048_UG_2p4s.png` |
| 单次完成后 `b_idle` | `Assets/Screenshots/CZN/Fei_30048/Fei_30048_Completion_b_idle.png` |

![绯模型预览](../../Assets/Screenshots/CZN/Fei_30048/Fei_30048_ModelPreview.png)

![绯 U4 1.016 秒](../../Assets/Screenshots/CZN/Fei_30048/Fei_30048_U4_1p016s.png)

![绯 UG 2.4 秒](../../Assets/Screenshots/CZN/Fei_30048/Fei_30048_UG_2p4s.png)

![绯技能完成后清场并返回 b_idle](../../Assets/Screenshots/CZN/Fei_30048/Fei_30048_Completion_b_idle.png)

## 报告索引

已存在的可重复审计证据：

```text
Assets/Imported/CZN/Fei_30048/Metadata/complete_records.json
Assets/Imported/CZN/Fei_30048/Metadata/complete_records.csv
Assets/Imported/CZN/Fei_30048/Metadata/dependency-report.json
Assets/Imported/CZN/Fei_30048/Metadata/import-manifest.json
Assets/Imported/CZN/Fei_30048/Metadata/spine-json-conversion-report.json
Assets/Imported/CZN/Fei_30048/Metadata/ancillary-spine-json-conversion-report.json
Assets/Imported/CZN/Fei_30048/Metadata/spine-cli-validation-report.md
Assets/Imported/CZN/Fei_30048/Metadata/spine-unity-integration-report.md
Assets/Imported/CZN/Fei_30048/Metadata/skill-composition-report.md
Assets/Imported/CZN/Fei_30048/Metadata/skill-resource-map.json
Assets/Imported/CZN/Fei_30048/Metadata/skill-validation-report.json
Assets/Imported/CZN/Fei_30048/Metadata/skill-validation-report.md
Assets/Imported/CZN/Fei_30048/Metadata/playmode-replay-validation-report.json
Assets/Imported/CZN/Fei_30048/Metadata/playmode-replay-validation-report.md
External/CZN/Fei_30048/Metadata/records.main.json
External/CZN/Fei_30048/Reports/core-atlas-contact-sheet.png
```

`import-manifest.json` 是提取阶段快照，其中 `playable: false` 只指原始 `.scsp1u.bytes` 不能直接被 Spine 播放；它不否定后来生成的 115 份 canonical JSON 和 Unity Spine 资产。格式转换看两份 conversion report，Spine Editor 导入看 `spine-cli-validation-report.md`，Unity 加载看 `spine-unity-integration-report.md`，组合与 597 个边界样本分别看 skill composition/resource map 与 skill validation 报告，真实 PlayMode 的单次完成清场、`R` 重播、暂停/继续、UG standby/相机和截图索引看 `playmode-replay-validation-report.md`。

## 文件格式速查

| 文件 | 含义 | 正确用途 |
|---|---|---|
| `.scsp1u.bytes` | 游戏私有骨骼运行时数据 | 原始证据，不直接给 Spine/Unity 播放 |
| `.json` | 转换后的 canonical Spine 3.8 数据 | Spine 导入、spine-unity 导入 |
| `.atlas.txt` | Unity 可识别的 Spine Atlas 文本 | 与同名 PNG 配对导入 |
| `.png` | Atlas 或粒子纹理 | Unity/Spine 图像数据 |
| `_SkeletonData.asset` | spine-unity 生成的骨骼资产 | `SkeletonAnimation` 引用 |
| `_Atlas.asset` | spine-unity 生成的图集资产 | SkeletonData 的 Atlas 引用 |
| `.cfx.xml` | 多层 Spine/粒子组合配置 | 由 `FeiSkillComposer` 解析 |
| `.particle.xml` | 粒子发射器配置 | 由 `FeiSkillComposer` 转译 |
| `.srmd.json` / `.brmd.json` | 主模型/BattleReady 命令图 | 技能 phase、时序、镜头与 standby 来源 |

## 授权与安全边界

- 仅从已安装客户端复制和离线解析；不要写回 `F:\WeGameApps\rail_apps\czn(2002460)`。
- 不启动客户端注入、不绕过完整性校验或反作弊，也不修改网络服务。
- `External/CZN/` 与 `Assets/Imported/CZN/Fei_30048/` 已按 local-only 处理；不要取消忽略后提交提取物。
- canonical JSON、解包 PNG、`.spine` 工程和生成的 Unity 资产仍然是原游戏资源的衍生物，不因格式转换而获得再分发权。
- spine-unity 3.8 的本地引用为 `External/CZN/SpineRuntime-3.8`；换电脑时需要在符合 Esoteric Software 许可的前提下重新配置。
- 本机能找到 Spine 3.8.75 可执行文件只说明工具可用，不代表已自动核验其许可证；公开演示或发布前请自行确认合法授权。
