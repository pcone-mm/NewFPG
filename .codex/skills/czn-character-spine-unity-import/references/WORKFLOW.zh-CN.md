# CZN 角色 Spine / Unity 导入标准流程

这份流程把“从发布版客户端定位角色资源，到在 Unity 中稳定重播完整技能”的工作固化下来。它以海德玛丽 `30093` 为已验证样本，但目录、检查点和交付标准适用于其他角色。

## 1. 当前自动化边界

| 环节 | 状态 | 说明 |
|---|---|---|
| SSRC 记录提取 | 已参数化 | `extract_character.py` 可接收角色 ID、标签和输出目录，但输入必须是先审计好的记录清单。 |
| SCT/SCSP 解包 | 已自动化 | 输出 PNG、atlas、SCSP1U、JSON/XML 配置和哈希清单。 |
| SCSP1U 转 Spine 3.8 JSON | 已批处理 | 对新角色必须重新检查未知 attachment/timeline/event 类型，不能直接假设结构与 30093 完全相同。 |
| Unity Spine 资产导入 | 主要由 spine-unity 完成 | 需要核对 Runtime 版本、材质、Atlas、动画计数和 Console。 |
| 技能组合 | 半自动 | 通用播放器已存在；当前 Editor 生成器 `HeidemarieSkillComposer` 仍硬编码 30093、技能表和路径，新角色要适配或泛化生成器。 |
| 视觉等价 | 需人工验证 | mask、自定义 blend/shader、后处理、粒子共享贴图、hit-stop 等可能需要逐项恢复。 |

因此，以后可以直接说“按 CZN 角色导入流程处理某角色”，但仍必须先找到正确角色 ID并做依赖审计；不能只替换文件夹名字就宣称完成。

## 2. 输入与目录约定

每个角色使用统一标签：`<SafeName>_<CharacterId>`，例如 `Heidemarie_30093`。

```text
External/CZN/<Label>/
├─ Raw/<branch>/                 原始容器记录的只读解压副本
├─ Metadata/records.<branch>.json
├─ Reports/
└─ SpineProjects/               Spine Editor 研究工程（本地交付）

Assets/Imported/CZN/<Label>/
├─ SpineSource/
│  ├─ model/                    主模型、battle-ready
│  └─ effect/                   技能、cut-in、目标/自身层
├─ AncillarySource/
│  ├─ camera/
│  ├─ camera_path/
│  └─ node/
├─ Configs/
│  ├─ model_data/
│  ├─ model_setting/
│  └─ effect/
├─ Metadata/
└─ Preview/
   ├─ Prefabs/
   └─ SkillCompositions/{Skills,Timelines,Generated}/
```

原始提取物和第三方角色资源应保持 local-only；通用脚本、skill reference 和不含游戏载荷的模板才适合进入版本控制。

## 3. 阶段 A：角色识别与依赖审计

1. 从角色名、主模型命名、model setting 或现有索引中确认数字 ID。
2. 读取主/热更新 `manifest.ssra`，定位所需 `.ssrc` 分块；热更新同路径记录通常优先于主包。
3. 从角色核心文件展开依赖：

```text
model/<id>.*
model/<id>_battle_ready.*
model_data/<id>.srmd/.srcs/.srue
model_data/<id>_battle_ready.brmd
model_setting/<id>.setting
effect/*.cfx / *.scsp / *.atlas / *.sct
particle/*.plist / *.sct
camera*, camera_path*, node*, cutin*, portrait*
```

4. 把每条依赖写入 `complete_records.json`，至少包含逻辑路径、branch、chunk、offset、压缩/原始尺寸和 hash。
5. 单独列出：共享资源、shadow 覆盖、缺失贴图、未解析引用和可能无图集的纯 transform 骨骼。

审计输出是可重复提取的输入证据。没有它时，先完成审计，不要运行默认的 30093 清单。

## 4. 阶段 B：只读提取

在项目根目录安装依赖：

```powershell
python -m pip install -r Tools/CznResourcePipeline/requirements.txt
```

使用显式参数，避免误用海德玛丽默认值：

```powershell
py Tools/CznResourcePipeline/extract_character.py `
  --records "<absolute-records-json>" `
  --gameres-root "<absolute-gameres-root>" `
  --branch main `
  --label "<Label>" `
  --character-id "<CharacterId>" `
  --external-root "D:\Unity\NewFPG\External\CZN\<Label>" `
  --unity-root "D:\Unity\NewFPG\Assets\Imported\CZN\<Label>"
```

然后验证：

```powershell
py Tools/CznResourcePipeline/validate_import.py `
  --unity-root "D:\Unity\NewFPG\Assets\Imported\CZN\<Label>" `
  --manifest "D:\Unity\NewFPG\Assets\Imported\CZN\<Label>\Metadata\import-manifest.json"
```

必须满足：哈希匹配、SCSP1U 标记正确、配置可解析、Atlas 与 PNG 成对且尺寸一致。禁止写回游戏安装目录、启动客户端注入、绕过完整性或反作弊。

## 5. 阶段 C：SCSP1U 转换和 Spine 验证

```powershell
py Tools/CznResourcePipeline/scsp1u_to_spine.py `
  "Assets/Imported/CZN/<Label>/SpineSource" `
  --report "Assets/Imported/CZN/<Label>/Metadata/spine-json-conversion-report.json"
```

如 `AncillarySource` 也含 SCSP1U，再单独转换并生成另一份报告。对每批检查：

- 骨骼、Slot、Skin、Attachment、动画与 Timeline 计数；
- IK、Transform、Path constraint；
- mesh 权重、deform 索引、draw order、attachment timeline；
- 所有 Region/Mesh path 能在相邻 atlas 中解析；
- Parser 是否遇到当前样本库未覆盖的 EventData、EventTimeline、linked mesh、bounding box、point 或其他记录。

遇到未知结构时，转换器应报错并保留样本，不能猜布局后继续批量生成。

在 Spine 3.8 中研究时，先把 atlas 解包到 `images`，再从 canonical JSON 创建 `.spine` 工程。`.scsp1u.bytes` 不是 `.skel`。Spine Editor 保存/反导可能标准化 mesh/deform 数据，因此不能覆盖 canonical JSON。

## 6. 阶段 D：Unity 导入

1. 确认 `Packages/manifest.json` 的本地 `spine-unity` 3.8 路径实际存在。
2. 刷新 Unity 并等待编译；先解决错误，再使用新类型或生成场景。
3. 确认每组三件套能生成：

```text
foo.json + foo.atlas.txt + foo.png
→ foo_SkeletonData.asset + foo_Atlas.asset + foo_Material*.mat
```

4. 逐批核对 SkeletonDataAsset 数量、动画名/数量、Atlas 绑定和材质 shader。
5. 先生成主模型、BattleReady Prefab 和独立对比场景，验证 idle、攻击、deform/mesh 顶点确实随时间变化。

## 7. 阶段 E：技能图恢复

解析 SRMD/BRMD 命令图，递归读取 CFX 和粒子配置，并把信息映射到：

| 原配置 | Unity 数据 |
|---|---|
| 角色 phase / animation | `CznActorAnimationCue` |
| CFX Spine layer | `CznSpineLayerCue` |
| particle emitter | `CznParticleLayerCue` |
| camera/node SCSP timeline | `CznTransformCue` |
| zoom | `CznCameraZoomCue` |
| HIT/DAMAGE/STOP/post-process | `CznSkillMarkerCue` 或对应实现 |

组合时保留：相对/绝对延迟、等待结束、循环、层级顺序、前后景、锚点、偏移、缩放、旋转和目标类型。`_b/_f/_target/_self/_sword/_mask/_cutin/_shake` 只是排查提示，最终以配置和实际 Atlas/动画为准。

当前通用运行时位于：

```text
Assets/Scripts/CZN/CznSpineSkillSequence.cs
Assets/Scripts/CZN/CznSpineSkillTimeline.cs
Assets/Scripts/CZN/CznSpineSkillPlayer.cs
Assets/Scripts/CZN/CznSpineSkillPreviewMenu.cs
```

海德玛丽生成器位于 `Assets/Editor/CZN/HeidemarieSkillComposer.cs`。它可以作为算法参考，但包含角色路径、ID、技能命令表、输出名和菜单名常量。新角色必须显式适配，且生成器应幂等，不得覆盖带有未保存改动的非目标场景。

## 8. 阶段 F：验证矩阵

### 静态检查

- 所有 GUID/ScriptableObject/Timeline/Prefab 引用存在；
- 每个 cue 的动画名确实存在于 SkeletonData；
- duration 覆盖最后一个有效 cue；
- unresolved 清单与报告一致；
- 生成器重复运行不会累计对象、轨道或资产。

### 采样检查

每个技能至少采样：`0`、每个 phase/cue 边界前后、一个可见中帧和结束帧。记录活动 Spine/粒子层数、Mesh 顶点、附件数、相机/锚点变换及 Console。

### 重播检查

战斗技能预览默认采用“单次播放 → 清场 → 循环 `b_idle`”；只有源数据或用户明确要求循环时，才启用 Timeline 自动回绕。至少连续 3 轮测试：

1. 同技能刚开始立即重播；
2. 播到 attachment/alpha 已清空的末态后重播；
3. 自然播放结束后，Spine/粒子/standby 清零，相机与锚点复位，主角色进入循环 `b_idle`；
4. 结束回待机后按 `R` 重播，固定中帧的附件、类型与 mesh 顶点签名保持一致；
5. A 技能结束回待机后播放 B，再回待机；
6. 暂停/继续不会误清场，结束后只有显式重播才重新开始；
7. 拖动、重建 PlayableGraph；若明确启用循环模式，再额外验证至少 3 次自动回绕。

Spine 硬重置必须遵循：

```text
AnimationState.ClearTracks()
→ Skeleton.SetToSetupPose()
→ AnimationState.SetAnimation(...)
→ 当帧 Apply/Update
```

本项目的 `SkeletonAnimation.ClearState()` 会清网格、恢复 Setup Pose 并清轨道。只 `SetAnimation` 或只把缓存的 TrackEntry 设为 null 都不够；Attachment Timeline 可能在末帧将武器/光效设为 null，下一轮开头又没有 non-null key。

### 最终通过条件

- Unity 编译错误为 0，任务相关 Console 错误/警告为 0；
- 主模型、BattleReady 和代表性特效可在 Unity/Spine 播放；
- 全部目标技能能切换、暂停、拖动、重播，并按配置正确执行“单次结束回 `b_idle`”或显式循环；
- 连续重播时附件/网格恢复，运行时对象数量不增长；
- 报告明确区分“数据恢复”“近似替代”和“未实现”。

## 9. 每个角色的交付物

- `Assets/Imported/CZN/<Label>/README.md`，作为资产本地的使用与交付说明；
- main/BattleReady/代表性 VFX Prefab；
- 模型预览场景和技能组合预览场景；
- `SkillCompositions/Skills` 与 `Timelines`；
- import、转换、Spine CLI、Unity 集成、技能组合和 unresolved 报告；
- `External/CZN/<Label>/SpineProjects` 下可打开的 Spine 项目；
- 至少一张模型截图和一张代表技能中帧截图；
- 许可证/分发限制和 local-only 路径说明。

## 10. 下次请求方式

用户可以直接说：

```text
按项目里的 CZN 角色导入流程，把 <角色名>（ID <数字>）的战斗模型、完整技能特效、Spine 预览工程和 Unity 技能预览场景导入并验证。
```

如果不知道 ID，也可以只给角色名；第一阶段先查 ID 和依赖，不应凭名字猜。
