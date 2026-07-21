# CZN 8 只普通怪 Spine / Unity 导入说明

本批次已完成 8 只怪物的模型、动作配置和 SRMD 直接引用特效闭包导入。游戏安装目录只读；提取资源保存在本机并由 `.gitignore` 排除。

## 已导入内容

| ID | 内部名 | 模型分支 | 主模型动作数 |
|---|---|---:|---:|
| `1001005` | `killer_fly` | main | 15 |
| `1001023` | `bare_beetle` | main | 8 |
| `1001016` | `honey_jar_porte` | main | 17 |
| `1001003` | `burstbug` | main | 8 |
| `1004002` | `power_taker` | main | 7 |
| `1004020` | `mini_bite` | shadow | 12 |
| `1006002` | `spawn_insect` | main | 13 |
| `1006018` | `dust_insect` | shadow | 9 |

整体结果：

- 344 条原始依赖记录；
- 8 个主模型，共 89 个主模型动作；
- 76 个 Unity `SkeletonDataAsset`，包含可转换的模型/特效骨骼；
- 主模型与可转换特效合计 181 个动画；
- 所有 76 个 `SkeletonDataAsset` 均通过 Unity 加载验证；
- 纹理、atlas、配置和提取文件哈希全部通过验证。

## 在 Unity 中查看

打开场景：

```text
Assets/Imported/CZN/Monsters/Preview/CZN_Monsters_8_Preview.unity
```

按 Play 后可使用：

- `1`～`8`：直接选怪；
- `←` / `→`：切换怪物；
- `↑` / `↓`：切换当前怪物动作；
- `Space`：从头重播当前动作。

名字中包含 `idle` 的动作会循环。攻击、受击、死亡等非 idle 动作不循环，结束后自动接回 `normal_idle`。

## Prefab

8 个可直接拖入其他场景的 Prefab 位于：

```text
Assets/Imported/CZN/Monsters/Preview/Prefabs
```

每个 Prefab 默认循环播放 `normal_idle`。也可以选中 `SkeletonAnimation` 组件，在 `Animation Name` 中选择动作。

## 单只怪物的资源目录

以 `1001005` 为例：

```text
Assets/Imported/CZN/Monsters/1001005/
├─ SpineSource/model/          主模型 PNG、atlas、Spine JSON 和 Unity 生成资产
├─ SpineSource/effect/         可转换的 Spine 特效层
├─ SpineSource/particle/       粒子纹理
├─ Configs/                    setting、SRMD、SRCS、stance、CFX、particle
├─ AncillarySource/            相机震动等辅助 SCSP1U
├─ UnsupportedSource/          已识别但当前转换器不支持的旧 SCSP
└─ Metadata/                   提取与 Spine JSON 转换报告
```

原始解包副本位于：

```text
External/CZN/Monsters/<ID>/Raw/
```

## 当前限制

`1001005 / Killer Fly` 有 6 个技能特效骨骼使用旧私有 `SCSP v3`。它们已原样保存在：

```text
Assets/Imported/CZN/Monsters/1001005/UnsupportedSource/effect/
```

这 6 个文件不会伪装成 Spine JSON，也不会被 spine-unity 扫描。其 atlas、纹理和 CFX 配置仍已导入。其余 76 个 SCSP1U 模型/特效骨骼均已转换并可由 Unity 读取。

本批次完成的是“资源恢复 + 模型/动作预览”。CFX、particle 和特效 Spine 层已经按怪物归档，但尚未像角色技能预览器那样为每只怪自动生成完整 Timeline 技能组合。

部分 setting/SRMD 会列出模型 SCSP 中并不存在的动作占位名，例如死亡或 stance 备用动作。Spine 3.8.75 对 8 个模型的回读结果与 SCSP 内部动画表逐项一致，因此上表动作数不是转换丢失。

## 重新生成 Unity 预览

资源已经存在时，在 Unity 菜单执行：

```text
Tools > CZN > Monsters > Build Selected 8 Models
```

验证菜单：

```text
Tools > CZN > Monsters > Validate Selected 8 Models
```

生成器代码：

```text
Assets/Editor/CZN/CznMonsterBatchBuilder.cs
Assets/Scripts/CZN/CznMonsterModelPreviewController.cs
```
