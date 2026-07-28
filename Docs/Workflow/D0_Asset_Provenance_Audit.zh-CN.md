# D0 资源来源与发布隔离审计

## 结论

本审计只读取 NewFPG 工作区，不访问、解包或提取任何商业游戏客户端。当前 D0 的程序架构可以作为原创实现继续维护；但 `Assets/FPGDemo/Presentation/D0Slice/Spine/` 的视觉资产来源和可分发许可尚未由负责人确认。因此它们不能被视为可提交、可公开演示或可发布的资源，也不能作为 G6 Windows Release 的放行依据。

本文件记录的是风险和交接门槛，不对任何文件的来源、版权归属或授权作推断性结论。

## 静态盘点（2026-07-16）

| 范围 | 只读事实 | 暂定处理 |
| --- | --- | --- |
| `Assets/FPGDemo/Presentation/D0Slice/Spine/` | 208 个文件、约 7,093,053 bytes；包含 15 PNG、15 Prefab、30 ScriptableObject asset、29 Material、15 atlas 文本及对应 meta | 来源/许可待负责人确认；在确认前隔离，不进入候选发布物 |
| `External/CZN/` | `.gitignore` 已排除；本机存在 Spine Runtime 与本地导入源 | 仅本地研究/技术验证；需确认 Spine Runtime 许可 |
| `Assets/Imported/CZN/Fei_30048/` 与 `Assets/Imported/CZN/Monsters/` | `.gitignore` 已排除；2026-07-16 的旧 D0 安装器曾把它们作为输入路径 | 仅本地输入；不得由场景或发布包直接引用 |
| `Assets/FPGDemo/Presentation/D0Slice/Spine/` | `.gitignore` 没有排除该目录；历史 D0 场景曾引用其中的派生产物 | 在来源未确认前不得建立为可发布 Git 基线；优先替换为原创灰盒/获授权资产 |
| `Assets/FPGDemo/Runtime/**` 与 `Assets/FPGDemo/Editor/**` | 原创的确定性战斗、威胁状态机、动画路由、输入和对象池代码 | 可继续作为 clean-room 实现；仍须由负责人确认 Git 基线归属 |

2026-07-16 审计时存在的旧安装器曾从被忽略的 CZN 导入目录读取角色、怪物和特效输入，再在 D0 Presentation 目录创建图集、材质、SkeletonData 和 Prefab；该安装器现已删除。历史依赖链本身不证明输出物拥有可发布授权，在书面确认前，派生产物与输入物仍按受限资源处理。

## 发布门禁

在以下四项全部有记录前，不得将 D0 视觉资源作为候选 Release、公开演示物或提交基线的一部分：

1. 负责人逐类确认资源来源、版权所有者、使用范围、版本/日期和可分发许可；Spine Runtime 许可另行确认。
2. 对每个未获批准类别选择“移除”“替换为原创占位/原创正式资产”或“取得书面授权”，并记录替换后的引用路径。
3. 负责人确认 `Assets/FPGDemo/`、D0 文档与场景变更的 Git 基线归属；不得用 `reset`、`clean` 或覆盖未跟踪内容替代确认。
4. 仅在以上门禁通过且 Unity 重新解析/编译成功后，执行 G6 构建、Player 证据采集和主管试玩。

## 原创替换的最小资源清单

若负责人决定完全移除受限视觉资源，D0 的代码合同不要求保留任何现有角色或怪物图像。最小替换集如下，均可使用原创灰盒先验证功能：

| 用途 | 最小原创资产 | 保持不变的代码合同 |
| --- | --- | --- |
| 玩家视觉 | 一个正面站位、idle/primary/secondary/hit/terminal 状态的原创 Sprite 或 Spine 骨架 | `Actor2DPresenter`、`D0ActorAnimationStateMachine`、`D0CharacterDefinition` |
| 敌人视觉 | 一个场外入场、巡航、攻击、硬直、死亡状态的原创 Sprite 或 Spine 骨架 | `D0EnemyBehaviorController`、`D0EnemyBehaviorProfile`、`D0EnemyDefinition` |
| 命中与弱点 | 原创 Body/Weakpoint 图形与现有 Collider 锚点 | Hitbox Registry 的 geometry ID 2001/2002 与空间查询合同 |
| 威胁预警/命中/Break | 原创形状、粒子或 UI 动效，按固定池容量预热 | `ThreatTelegraph2DPresenter`、`D0WeakpointPresentationController`、效果槽与音频 Cue |
| 音频 | 自制或获授权的短音效 | `CombatAudioBank` 的 Cue ID，不复制外部音频 |

替换只改 `D0ActorPresentationDefinition`、`CombatPresentationProfile` 和正式 authored 资产引用；不得改变确定性伤害、威胁时序、命中盒或行为状态机来迁就视觉资源。

## 后续记录位置

- 架构与 clean-room 迁移说明：飞书“架构迁移参考与实施基线”。
- D0 运行时场景合同与行为绑定：`D0_Production_Line_Contract.zh-CN.md`。
- Unity/Release/试玩证据：`D0_Validation_Evidence_Index.zh-CN.md` 与 `D0_G6_Release_Evidence_Runbook.zh-CN.md`。

在负责人做出来源和替换决策前，本审计状态保持为“待确认”，不得改写成“已授权”或“可发布”。
