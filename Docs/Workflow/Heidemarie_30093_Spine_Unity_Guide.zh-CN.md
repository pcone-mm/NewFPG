# 市长海德玛丽（30093）Spine / Unity 使用指南

本文说明这次处理了什么、资源放在哪里，以及如何在 Spine 3.8 和
Unity 6 中预览角色动画与特效。

> 这些资源用于本地学习和技术研究。提取资源、转换结果和本地 Spine
> Runtime 已排除在 Git 之外；正式发布或分发前请确认原游戏资源与
> Spine Runtime 的授权。

## 一分钟快速开始

### 在 Unity 中看

1. 打开 Unity 工程 `D:\Unity\NewFPG`。
2. 在 Project 窗口打开：
   `Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_SkillPreview.unity`。
3. 点击 Unity 顶部的 Play。
4. 默认循环播放 U1；用画面左上角的 `1–13` 按钮切换组合。
5. 也可以用 `←/→` 切换、`R` 重播、`Space` 暂停；数字键
   `1–9/0` 对应前 10 项。

如果只想并排看主模型和 BattleReady，不看完整技能组合，再打开
`Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_Preview.unity`。

### 在 Spine 中看

直接打开这些已经整理好的工程：

- 主战斗模型：
  `D:\Unity\NewFPG\External\CZN\Heidemarie_30093\SpineProjects\Heidemarie_30093_Main.spine`
- BattleReady 模型：
  `D:\Unity\NewFPG\External\CZN\Heidemarie_30093\SpineProjects\Heidemarie_30093_BattleReady.spine`
- U1 前景特效：
  `D:\Unity\NewFPG\External\CZN\Heidemarie_30093\SpineProjects\Heidemarie_30093_U1_FrontFX.spine`

在 Spine 中切到 `ANIMATE` 模式，展开 Tree 面板中的 `Animations`，点击动画
名称，再按空格或 Dopesheet 的播放按钮。

不要尝试直接打开 `.scsp1u.bytes`。它是游戏私有格式；Spine 应打开上面的
`.spine` 工程，或者导入转换后的 `.json`。

## 我具体做了什么

处理对象是市长海德玛丽，角色 ID 为 `30093`。

1. 从游戏 SSRC 容器中只读提取了 534 条简体中文战斗依赖。
2. 解出了 150 张 PNG、150 份 Spine Atlas，以及模型、技能、Cut-in、UG、
   UX 等 SCSP1U 骨骼数据。
3. 编写了 SCSP1U 到标准 Spine 3.8 JSON 的离线转换器。
4. 150/150 个可视骨骼转换成功，共恢复 288 个动画和 67,941 条时间轴。
5. 用 Spine 3.8.75 验证了主模型、BattleReady、U1、Path 特效和
   Transform 特效。
6. 在 Unity 中接入了 Spine 3.8 Runtime，并生成 150 个
   `SkeletonDataAsset`、150 个 `SpineAtlasAsset` 及相应材质。
7. 创建了两个角色 Prefab、一个 U1 示例特效 Prefab 和模型预览场景。
8. 验证了 `idle` 切换到 `u1_attack_play` 后 Unity 网格顶点确实变化，说明
   动画已经由 Runtime 实际求值，不只是把 JSON 当成文本导入。
9. 解析 `30093.srmd`、49 个 CFX、17 个 particle 配置和 13 个相机/节点
   SCSP1U，恢复技能图时序、前后景、SELF/TARGET/SCREEN 锚点和震屏数据。
10. 生成 13 个 `SkillSequence` 与 13 条 Unity Timeline，包括普攻、U1–U5、
    UG、UX、Fatal、入场和胜利，并创建独立技能组合预览场景。

## 目录与文件说明

| 用途 | 项目路径 |
|---|---|
| 完整技能组合场景 | `Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_SkillPreview.unity` |
| Unity 预览场景 | `Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_Preview.unity` |
| 主模型 Prefab | `Assets/Imported/CZN/Heidemarie_30093/Preview/Prefabs/Heidemarie_30093_Main.prefab` |
| BattleReady Prefab | `Assets/Imported/CZN/Heidemarie_30093/Preview/Prefabs/Heidemarie_30093_BattleReady.prefab` |
| U1 前景特效示例 | `Assets/Imported/CZN/Heidemarie_30093/Preview/Prefabs/Heidemarie_30093_U1_FrontFX.prefab` |
| 技能组合器 Prefab | `Assets/Imported/CZN/Heidemarie_30093/Preview/Prefabs/Heidemarie_30093_SkillComposer.prefab` |
| 13 个技能数据 | `Assets/Imported/CZN/Heidemarie_30093/Preview/SkillCompositions/Skills` |
| 13 条 Timeline | `Assets/Imported/CZN/Heidemarie_30093/Preview/SkillCompositions/Timelines` |
| 主模型 Spine JSON | `Assets/Imported/CZN/Heidemarie_30093/SpineSource/model/30093.json` |
| BattleReady Spine JSON | `Assets/Imported/CZN/Heidemarie_30093/SpineSource/model/30093_battle_ready.json` |
| 所有 Spine 特效 | `Assets/Imported/CZN/Heidemarie_30093/SpineSource/effect` |
| CFX/粒子等配置 | `Assets/Imported/CZN/Heidemarie_30093/Configs` |
| 转换与验证报告 | `Assets/Imported/CZN/Heidemarie_30093/Metadata` |
| 原始提取备份 | `External/CZN/Heidemarie_30093` |
| 通用转换工具 | `Tools/CznResourcePipeline` |
| 技能组合验证报告 | `Assets/Imported/CZN/Heidemarie_30093/Metadata/skill-composition-report.md` |

预览截图：

`Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_Preview_Final.png`

![市长海德玛丽 Unity 预览场景](../../Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_Preview_Final.png)

U1 组合在 `2.55s` 的验证截图：

![市长海德玛丽 U1 技能组合](../../Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_U1_SkillPreview.png)

## 在 Unity 里查看完整技能组合

### 使用技能组合预览场景

打开 `Heidemarie_30093_SkillPreview.unity` 并进入 Play。当前组合器提供：

| 编号 | 组合 |
|---:|---|
| 1–2 | 普攻一、普攻二 |
| 3–6 | U1、U2、U3、U4 |
| 7–8 | U5 连击一、U5 连击二 |
| 9–10 | UG、UX |
| 11–13 | Fatal、入场、胜利 |

场景根对象 `Heidemarie 30093 Skill Composition Preview` 上有：

- `CznSpineSkillPlayer`：按时间确定性求值角色动作、Spine 层、粒子和镜头；
- `PlayableDirector`：播放当前技能的 Timeline；
- `CznSpineSkillPreviewMenu`：负责画面按钮与快捷键。

选中根对象并打开 `Window > Sequencing > Timeline`，可以查看当前技能的
Timeline。切换技能时，`PlayableDirector.playableAsset` 会换到对应 `.playable`。

### 在自己的测试场景中使用

最省事的方式是把下面的 Prefab 拖进测试场景：

```text
Assets/Imported/CZN/Heidemarie_30093/Preview/Prefabs/Heidemarie_30093_SkillComposer.prefab
```

如果要接到自己的战斗系统，建议保留 `CznSpineSkillPlayer`，由你的技能逻辑
选择 `SkillCompositions/Skills` 中的 `CznSpineSkillSequence`，或者直接让
`PlayableDirector` 播放 `SkillCompositions/Timelines` 中的对应 Timeline。

重新解析配置并生成全部资产的菜单是：

```text
Tools > CZN > Heidemarie 30093 > Build Skill Compositions
```

### 当前精度边界

- CFX 的层顺序、动画名、时序、偏移、锚点和缩放均来自原配置；
- 14 张共享 `particle/*.sct` 不在角色依赖包中，粒子运动/颜色已恢复，贴图暂用
  生成的软粒子替代；
- 相机/节点关键帧已恢复，非 stepped 的 Spine Bezier 暂按线性插值；
- mask、自定义 shader、径向 RGB blur、speed blur、hit-stop 和 color-blend
  当前保留为诊断 marker，还不是像素级复刻；
- BattleReady UG 是独立 BRMD 图，保留在单独的 BattleReady Prefab/Spine 工程，
  没有错误绑定到主角色 Timeline。

## 在 Unity 里查看角色动画

### 方法一：使用预览场景

打开 `Heidemarie_30093_Preview.unity` 后，Hierarchy 中主要有：

```text
Heidemarie Preview Models
├─ Heidemarie_30093_Main
└─ Heidemarie_30093_BattleReady

Heidemarie Preview Camera
Directional Light
```

选中 `Heidemarie_30093_Main` 或 `Heidemarie_30093_BattleReady`，在 Inspector
的 `Skeleton Animation` 组件中修改 `Animation Name`，然后进入 Play。

默认设置：

| 对象 | 默认动画 | Loop |
|---|---|---:|
| `Heidemarie_30093_Main` | `idle` | 开启 |
| `Heidemarie_30093_BattleReady` | `b_idle` | 开启 |
| `Heidemarie_30093_U1_FrontFX` | `animation` | 开启 |

U1 特效 Prefab 没有默认放进预览场景，避免它一直盖在角色前面。需要时把
`Heidemarie_30093_U1_FrontFX.prefab` 拖进 Hierarchy 即可。

### 方法二：直接使用 Prefab

把以下 Prefab 拖入你自己的测试场景：

```text
Assets/Imported/CZN/Heidemarie_30093/Preview/Prefabs/
```

建议先在独立测试场景使用，不要立即接入现有战斗逻辑。主模型和
BattleReady 不是同一个比例：主模型面向战场角色，BattleReady 是更大的
展示/近景模型。

### 用代码切换动画

播放一次攻击，然后返回待机：

```csharp
using Spine.Unity;
using UnityEngine;

public sealed class HeidemarieAnimationExample : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation skeleton;

    public void PlayU1()
    {
        skeleton.AnimationState.SetAnimation(0, "u1_attack_ready", false);
        skeleton.AnimationState.AddAnimation(0, "u1_attack_play", false, 0f);
        skeleton.AnimationState.AddAnimation(0, "u1_attack_end", false, 0f);
        skeleton.AnimationState.AddAnimation(0, "idle", true, 0f);
    }
}
```

只切一个动画：

```csharp
skeleton.AnimationState.SetAnimation(0, "ux_attack", false);
```

### 主战斗模型的动画

主模型共有 46 个动画。常用分组如下。

| 类型 | 动画 |
|---|---|
| 待机/移动 | `idle`, `b_idle`, `move`, `stop`, `camping` |
| 普攻 | `attack_ready`, `attack_play1`, `attack_play2`, `attack_end` |
| 受击/死亡 | `hit`, `groggy`, `collapse_idle`, `death_ready`, `death` |
| U1 | `u1_attack_ready`, `u1_attack_play`, `u1_attack_end` |
| U2 | `u2_buff_ready`, `u2_buff_play` |
| U3 | `u3_attack_ready`, `u3_attack_play`, `u3_attack_end` |
| U4 | `u4_buff_ready`, `u4_buff_play` |
| 大招/特殊 | `ug_attack`, `ux_attack`, `fatal_intro`, `fatal1`, `fatal2`, `fatal3` |
| 进场/胜利 | `enter_ready`, `enter_play`, `enter_end`, `victory_ready`, `victory` |

<details>
<summary>展开全部 46 个主模型动画</summary>

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
special0_idle
stop
u1_attack_end
u1_attack_play
u1_attack_ready
u2_buff_play
u2_buff_ready
u3_attack_end
u3_attack_play
u3_attack_ready
u4_buff_play
u4_buff_ready
ug_attack
ux_attack
victory
victory_ready
```

</details>

BattleReady 模型有 4 个动画：

```text
b_idle
card_attack
card_casting
ug_attack
```

## 在 Unity 里查看任意技能特效

特效目录为：

```text
Assets/Imported/CZN/Heidemarie_30093/SpineSource/effect
```

每个已经导入的可视特效通常有这些文件：

```text
foo.json                 标准 Spine 3.8 骨骼与动画
foo.atlas.txt            Spine 图集描述
foo.png                  图集纹理
foo_SkeletonData.asset   Unity 可播放骨骼资源
foo_Atlas.asset          Unity Spine 图集资源
foo_Material.mat         Unity 材质
```

查看某个特效时，可以搜索它的 `_SkeletonData.asset`。把它拖到 Scene/Hierarchy
时，Spine Unity 通常会让你选择创建 `SkeletonAnimation`。如果当前版本没有
弹出创建选项，最简单的办法是复制
`Heidemarie_30093_U1_FrontFX.prefab`，然后在 Inspector 中把
`Skeleton Data Asset` 替换成目标特效的 `_SkeletonData.asset`。

常见命名：

| 名称片段 | 大致用途 |
|---|---|
| `_b` | 角色或目标后层 |
| `_f` | 角色或目标前层 |
| `_target` | 目标位置特效 |
| `_self` | 自身位置特效 |
| `_sword` | 武器相关层 |
| `_mask` | 遮罩层 |
| `_cutin` / `_portrait` | 插画或 Cut-in |
| `_shake` | 镜头/节点动画，可能没有贴图 |

一个完整技能通常不是单一 Spine 文件，而是多层 Spine、粒子、目标锚点、
镜头和时间配置的组合。现在这些层已经通过 `CznSpineSkillSequence` 和 Timeline
组合；本节的手工方法只用于单独研究其中某一层。

## 在 Spine 里预览动画

### 查看主模型和 BattleReady

1. 启动：
   `F:\tool\Spinepro_3.8.75学习版\Spine pro 3.8.75\Spine.exe`。
2. 选择 `File > Open Project`。
3. 打开本文开头列出的 `.spine` 文件。
4. 切到 `ANIMATE`。
5. 在 Tree 的 `Animations` 下点击动画。
6. 用空格或 Dopesheet 播放按钮播放。
7. 在 Dopesheet 中查看骨骼、Slot、Deform、Draw Order、IK 等关键帧。

项目目录中的 `images` 文件夹是从 Atlas 解包得到的单图。不要单独移动
`.spine` 文件或 `images` 文件夹，否则 Spine 会显示缺图。

### 把任意特效 JSON 导入 Spine

Unity 中的特效已经可以直接播放。若还想在 Spine Editor 中研究，需要先把
Atlas 解成单图，再导入 JSON。以下以 `foo` 代指特效文件名：

```powershell
$spine = "F:\tool\Spinepro_3.8.75学习版\Spine pro 3.8.75\Spine.com"
$source = "D:\Unity\NewFPG\Assets\Imported\CZN\Heidemarie_30093\SpineSource\effect"
$name = "heidemarie_30093_eff_u1_attack_play_f"
$work = "$env:TEMP\HeidemarieSpine\$name"

New-Item -ItemType Directory -Force "$work\atlas-pages", "$work\images" | Out-Null
Copy-Item "$source\$name.json" "$work\$name.json"
Copy-Item "$source\$name.atlas.txt" "$work\atlas-pages\$name.atlas"
Copy-Item "$source\$name.png" "$work\atlas-pages\$name.png"

& $spine -i "$work\atlas-pages" -o "$work\images" -c "$work\atlas-pages\$name.atlas"
& $spine -i "$work\$name.json" -o "$work\$name.spine" -r $name
```

完成后打开：

```text
%TEMP%\HeidemarieSpine\<特效名>\<特效名>.spine
```

如果只想看角色主模型和 BattleReady，无需执行这些命令，直接打开现成的
两个 `.spine` 工程即可。

## 文件格式怎么理解

| 文件 | 含义 | 是否直接使用 |
|---|---|---:|
| `.scsp1u.bytes` | 游戏私有骨骼运行时数据 | 仅留作原始备份 |
| `.json` | 转换后的标准 Spine 3.8 数据 | 是，Spine/Unity 的 canonical 数据 |
| `.atlas.txt` | Spine 图集区域描述 | 是 |
| `.png` | 图集纹理 | 是 |
| `_SkeletonData.asset` | spine-unity 生成的骨骼资产 | 是 |
| `_Atlas.asset` | spine-unity 生成的图集资产 | 是 |
| `.mat` | spine-unity 生成的材质 | 是 |
| `.cfx.xml` | 多层特效与播放配置 | 已由技能组合生成器读取 |
| `.particle.xml` | 粒子配置 | 已由技能组合生成器读取 |

转换后的直接 JSON 是最终基准。不要用 Spine 再导出的 JSON 覆盖它，因为
Spine Editor 可能重新整理网格权重、Deform 索引和非必要的内部边信息。

## 常见问题

### Unity 里模型是粉色的

检查本地 Runtime 是否存在：

```text
External/CZN/SpineRuntime-3.8
```

并检查 `Packages/manifest.json` 是否包含：

```json
"com.esotericsoftware.spine.spine-unity": "file:../External/CZN/SpineRuntime-3.8"
```

当前导入材质使用 `Spine/Skeleton`，已在本工程的 URP 环境验证为可用。

### Unity 中不播放动画

1. 确认对象上是 `SkeletonAnimation` 组件。
2. 确认 `Skeleton Data Asset` 已赋值。
3. 确认动画名拼写完全一致。
4. 循环动画要打开 `Loop`。
5. 进入 Play 后再观察；Scene 静态视图不会持续推进时间。

### Spine 中看不到贴图

不要移动 `.spine` 工程旁边的 `images` 目录。研究任意新特效时，先按上面的
命令解包 Atlas，再导入 JSON。

### Spine 显示 `Nonessential unchecked` 警告

这是因为游戏运行时数据没有保留 Spine 编辑器专用的网格内部边信息。它不
影响当前 Runtime 播放；不要拿 Spine 反导 JSON 替换转换器生成的直接 JSON。

### 清理工程或换电脑后 Spine Runtime 丢失

`External/CZN` 被 `.gitignore` 排除，因此本地 Runtime 和提取资源不会随 Git
提交。换电脑时需要重新复制/组装 `External/CZN/SpineRuntime-3.8`，并重新
放入提取资源。

## 验证结果

- 原始依赖哈希：全部匹配。
- Atlas/PNG：150 对，尺寸匹配。
- SCSP1U：150/150 个可视骨骼完整闭合并转换成功。
- Unity SkeletonDataAsset：150 个，加载失败 0。
- 动画总数：288。
- 时间轴总数：67,941。
- 技能/演出组合：13 个 SkillSequence、13 条 Timeline。
- 组合内容：25 个角色动作、156 个 Spine 层、58 个粒子发射器、15 个镜头/节点变换。
- 静态引用、Timeline、GUID 与动画名审计：0 问题。
- 13 组逐段采样：0 异常；U1/UG PlayMode：0 Error、0 Warning。
- Unity 控制台：0 Error，0 Warning。
- 当前活动场景为
  `Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_SkillPreview.unity`，
  Unity 停留在 Edit Mode。

更详细的格式和验证记录：

- `Tools/CznResourcePipeline/README.md`
- `Tools/CznResourcePipeline/SCSP1U_NOTES.md`
- `Assets/Imported/CZN/Heidemarie_30093/Metadata/spine-cli-validation-report.md`
- `Assets/Imported/CZN/Heidemarie_30093/Metadata/spine-unity-integration-report.md`
- `Assets/Imported/CZN/Heidemarie_30093/Metadata/skill-composition-report.md`
- `Assets/Imported/CZN/Heidemarie_30093/Metadata/skill-resource-map.json`
- `Assets/Imported/CZN/Heidemarie_30093/Metadata/heidemarie-30093-skill-effects.md`
