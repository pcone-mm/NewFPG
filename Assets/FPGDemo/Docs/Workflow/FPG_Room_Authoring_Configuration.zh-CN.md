# FPGDemo 可视化房间关卡配置说明

## 目标与 v1 边界

本工具采用“完整手工房间 + 预埋玩法插槽”的制作方式。美术制作一个完整环境 Prefab，策划在 `FpgRoomDefinition` 中配置出口、玩家入口、敌人出生、可破坏物和可到达点；运行时组合两类资产，不做地形块拼接。

v1 只面向普通战斗房，提供创建、复制、分组、标签、五类标记可视化摆放、结构校验和正式 Encounter 预览。旧 D0 CombatLab 遭遇选择器与“一键试玩”不再由 Room Editor 暴露；正式 `FormalRoom` 运行时已经支持清场后按外部目录刷新出口并原地重建下一房，但这些能力都不改变房间编辑器的空间数据职责。以下内容仍不属于本工具：

- 为单个出口手工配置固定目标、编辑房间拓扑或路线图；
- 抽房权重、奖励、房间类型和按深度过滤等高级刷新规则；
- A*、NavMesh 或物理意义上的真实可达性判断；
- 多种专用房间规则和实时多人协同编辑；
- 可破坏物的生命、掉落、破坏动画与行为制作。

## 配置入口与资产位置

打开 Unity 菜单 `FPG Demo > Room Editor`。窗口负责浏览房间、筛选、编辑资料、选择标记、查看结构校验结果和生成正式 Encounter 预览；SceneView 是实际摆点画布。

| 内容 | 类型 | 标准位置 | 负责人 |
| --- | --- | --- | --- |
| 房间定义 | `FpgRoomDefinition` | `Assets/FPGDemo/Config/Level/Rooms/` | 关卡策划 |
| 可进入房间目录 | `FpgRoomCatalog` | `Assets/FPGDemo/Config/Level/` | 关卡策划/系统策划 |
| 出口房间刷新规则 | `FpgExitRoomRefreshRule` | `Assets/FPGDemo/Config/Level/` | 系统策划 |
| 主分组 | `FpgRoomGroupDefinition` | `Assets/FPGDemo/Config/Level/Groups/` | 关卡策划/内容负责人 |
| 标签 | `FpgRoomTagDefinition` | `Assets/FPGDemo/Config/Level/Tags/` | 关卡策划/内容负责人 |
| 环境 Prefab | `GameObject` Prefab | `Assets/FPGDemo/Presentation/Level/Environment/` | 关卡美术 |
| 可破坏物 Prefab | `GameObject` Prefab | 对应玩法/表现资源目录 | 该 Prefab 的功能负责人 |
| 编辑器代码与界面 | Editor-only | `Assets/FPGDemo/Editor/LevelAuthoring/` | 程序 |
| 房间运行时数据与桥接 | Runtime | `Assets/FPGDemo/Runtime/Unity/Level/` | 程序 |

若团队已经按地图主题细分子目录，可以在上述标准目录下继续分层；不要把房间定义放进环境 Prefab 目录，也不要把环境模型复制进房间定义目录。

## 引用关系与职责边界

```text
FpgRoomGroupDefinition <--- FpgRoomDefinition ---> Environment Prefab
FpgRoomTagDefinition   <---        |
                                  +--> 五类房间标记
                                           |
                                           +--> 可破坏物 Prefab（仅可破坏物标记）

旧 CombatLab 序列化绑定（Room Editor 不暴露）：
FpgRoomDefinition + D0CombatScenarioDefinition
                    |
                    +--> FpgRoomEncounterValidator 校验 Spawn ID
                    +--> FpgRoomInstance 组合进 CombatLab

GameBootstrapConfig --> FpgExitRoomRefreshRule --> FpgRoomCatalog
                                                   |
                                                   +--> 可进入的 FpgRoomDefinition
```

| 资产/系统 | 拥有的内容 | 明确不拥有 |
| --- | --- | --- |
| 环境 Prefab | 美术环境、可选的地面/墙体/遮挡、灯光及供 SceneView 点击放置使用的碰撞面；v1 不会自动把 Prefab Collider 注册进 `HitboxRegistry` | 出口目标、玩家/敌人选择、遭遇时间、房间分组和标记 ID |
| `FpgRoomDefinition` | 环境引用、分类和五类玩法标记的局部姿态 | 环境模型副本、敌人定义、刷怪 Tick、攻击编排、可破坏物数值 |
| `FpgRoomCatalog` | Player Build 中允许被出口抽取的有效房间显式列表 | 权重、出口位置、波次、奖励和某次访问的随机结果 |
| `FpgExitRoomRefreshRule` | 清场时如何从目录为全部出口生成并冻结目的地 | 房间空间、出口 Prefab、玩家资源和切房生命周期 |
| `GameBootstrapConfig` / `FpgRunFlowController` | 引用刷新规则并编排当前 Run 的清场、选出口、清理和重建 | 单个 Room 的出口目标字段和旧 CombatLab 完成语义 |
| `D0CombatScenarioDefinition` / Encounter | 玩家与遭遇组合、`playerSpawnPointId`、敌人 SpawnSlot 和姿态策略 | 房间坐标、环境 Prefab、房间分组与标签 |
| 可破坏物 Prefab | 生命、掉落、动画、行为及自身缩放 | 在每个房间中的位置和朝向 |
| `FpgRoomInstance` | 将所选房间实例化为环境、可破坏物和可查询姿态 | 随机选房、出口跳转和纯战斗规则 |

Room 与 Encounter 始终是两份独立资产。一个房间可以与多个遭遇组合；一个遭遇也可复用到标记 ID 合同一致的多个房间。不要为了某个遭遇复制房间坐标到 Encounter，也不要在 Room 中绑定具体敌人。

当前 v1 的 CombatLab 攻击/投射物 `EnvironmentBlocker` 仍由场景中的 `HitboxRegistry` 静态绑定提供；`FpgRoomInstance` 只负责实例化环境、可破坏物和标记姿态，不自动注册环境 Prefab 的 Collider。后续要让任意房间完全自带战斗碰撞，需要另行定义环境阻挡组件、稳定 Geometry ID 和容量合同。

## 正式出口目录与刷新规则

正式跨房流程不从 `FpgRoomDefinition` 或 `FpgRoomExitSlot` 读取目的地。房间只声明“出口在哪里”，目的地由 `GameBootstrapConfig` 引用的 `FpgExitRoomRefreshRule` 在清场时生成：

- `FpgRoomCatalog` 必须显式引用所有可进入房间。每项都必须是完整有效的 Room，具有玩家入口和至少一个出口，且 `RoomId` 不得重复；空占位房不得加入目录。
- 当前房间也属于候选池，与其他候选房等概率参与抽取。因此目录只有一个有效房间时，正式流程会合法地自循环，用于验证连续清场与重建。
- 随机上下文由 `RunSeed + RoomVisitOrdinal + SourceRoomId` 构成；同一上下文和同一组稳定 ID 必须得到相同结果。访问序号变化后重新抽取。
- 多出口先在一个候选池周期内无重复分配；出口数量超过候选房数量后才开始下一轮。规则层只处理稳定 ID，生成 `FpgExitOffer` 时才解析 Room 资产与 `DisplayName`。
- 每个 offer 只在房间进入 `Cleared` 时生成并绑定一次。攻击出口时只消费已显示的 offer，不允许重新抽取。

房间策划新增正式可进入房时，先确认房间结构合法，并通过正式 Encounter 预览或对应安装器确认 SpawnPoint 兼容，再把 Room 资产加入目录。不要在 `markerId`、显示名、备注或环境 Prefab 中编码目标房间；未来的权重、房间类型和奖励筛选应扩展刷新规则，而不是扩展出口插槽的空间合同。

`FPG Demo > Formal Encounter > Refresh Exit Room Flow Assets` 只会确保森林样例房存在于目录，不会删除策划已加入的其他合法房间。该菜单会重建 `PF_FPG_RoomExit.prefab` 并刷新 `M_FPG_RoomExit.mat`；这两个资产属于 Installer 生成输出，样式调整应同步修改 Installer 或后续独立样式资产，不要只手改生成结果。

## 正式出口运行时

`FpgRoomExitRuntime` 使用 `Hidden -> Available -> Consumed` 生命周期：

1. 准备和战斗阶段为 `Hidden`，出口渲染、标签和攻击碰撞均不可用。
2. 正式清场条件满足后，Director 为每个出口绑定 offer，显示 `前往：{Room.DisplayName}`，并进入 `Available`。
3. 出口碰撞体以 `EnvironmentBlocker` 注册到正式攻击查询，使用保留的 Geometry ID `95000-95999`；独立出口注册表负责从 Geometry ID 找回 offer，不把出口接入敌人伤害枚举或旧 `IDamageable`。
4. 主攻击或副攻击只要正式提交且查询命中可用出口，即把全部出口同步置为 `Consumed`。出口没有生命值；同帧多命中、连续射击或重复事件最多选择一次。
5. 清场边界会清空旧副攻击边沿，并要求主攻击先观察到松开后才重新武装。持续按住主攻击不能在出口出现时自动切房；装填、武器恢复、姿态和护盾恢复仍可在等待出口期间推进。

命中出口的攻击先按正常武器规则提交并消耗弹药，再捕获跨房资源。标签显示的 Room、`FpgExitSelectionEvent.Offer` 与最终进入的 Room 必须是同一个冻结 offer。

## 创建房间

1. 先创建或选择一个主分组；需要组合筛选时再创建标签。
2. 由美术提交独立环境 Prefab，确认根节点姿态为零、预览比例正确；如需在 SceneView 贴面摆点，可提供碰撞面。碰撞面在 v1 仅用于编辑器放置，不代表已经接入 CombatLab 攻击阻挡。
3. 打开 `FPG Demo > Room Editor`，点击“创建房间”，选择标准房间目录。
4. 填写显示名、备注，绑定环境 Prefab、一个主分组及零到多个标签。
5. 依次选择五类工具，在环境碰撞面上点击放置。无碰撞面时，工具会投影到房间 XZ 平面；放置后应重新检查高度。当前 CombatLab 的战斗阻挡仍来自场景静态绑定。
6. 为标记填写当前房间内唯一、可复用的语义 ID，例如 `player-main`、`enemy-melee-01`、`exit-north-01`。
7. 使用位置/旋转 Handle 和网格吸附调整姿态；通过类型显示开关排除无关标记。
8. 修复全部错误。缺少出口或可到达点可以保留为警告，但应在提交说明中写明原因。
9. 保存资产，关闭后重新打开房间，确认环境、列表、标记姿态和筛选结果保持一致。

预览环境和可破坏物是编辑器临时对象，不写入当前工作场景。不要尝试在 Hierarchy 中把预览对象保存为关卡内容；策划修改标记只应弄脏当前 `FpgRoomDefinition`。

## 复制房间

从房间列表选择源房间并执行“复制房间”。复制规则如下：

- 新房间必须生成新的全局房间 ID；
- 标记 ID 保持不变，便于同一个遭遇跨房复用；
- 环境、主分组、标签、备注和标记姿态作为起点一并复制；
- 复制后先修改显示名，再按需要替换环境和调整标记；
- 不要手工把新房间 ID 改回源房间 ID。

如果复制后改变了玩家入口或敌人出生 ID，必须同步选择对应遭遇做组合校验；仅移动同 ID 标记不需要修改 Encounter。

## 房间字段

| 配置组 | 中文名称 | 字段/类型 | 约束与实际效果 | 常见误配 |
| --- | --- | --- | --- | --- |
| 基础资料 | 房间 ID | `roomId` / string | 全项目唯一；创建和复制时生成，日常只读；未来抽房系统的稳定键 | 手工复用旧 ID，导致全局重复 |
| 基础资料 | 显示名 | `displayName` / string | 供列表、搜索和协作识别，不作为运行时键 | 用显示名代替 ID 做外部引用 |
| 基础资料 | 策划备注 | `designerNotes` / multiline string | 记录设计意图、未完成项和使用限制；运行时不读取 | 在备注中保存必须由运行时读取的规则 |
| 环境 | 环境 Prefab | `environmentPrefab` / Prefab | 初始化房间时实例化；美术独立维护 | 直接引用工作场景中的对象或把玩法标记烘进 Prefab |
| 分类 | 主分组 | `mainGroup` / `FpgRoomGroupDefinition` | 每个房间必须且只能引用一个；未来用于第一层分类选取 | 同时把多个主题当主分组，或缺少主分组 |
| 分类 | 标签 | `tags` / `FpgRoomTagDefinition[]` | 零到多个组合筛选维度；重复引用无意义并会被校验 | 用标签替代唯一主分组，或同一标签重复添加 |
| 标记 | 出口插槽 | `exitSlots` / list | 只声明出口位置和朝向，不保存目标房间 | 在 ID 或备注中硬编码目标房间并依赖它跳转 |
| 标记 | 玩家入口 | `playerEntryPoints` / list | 提供玩家 gameplay 入场姿态，由 Scenario 的 ID 解析 | 只摆视觉位置，未与 `playerSpawnPointId` 对齐 |
| 标记 | 敌人出生点 | `enemySpawnPoints` / list | 提供敌人初始及替换姿态，由 SpawnSlot 的 ID 解析 | 在点上绑定具体敌人或刷怪 Tick |
| 标记 | 可破坏物槽位 | `destructibleSlots` / list | 初始化房间时按局部姿态实例化所引用 Prefab | 在房间中覆盖生命、掉落或缩放 |
| 标记 | 可到达点 | `reachablePoints` / list | 策划声明的空间候选点；v1 仅查询数据 | 认为编辑器已证明物理上可达或自行创建连线 |

## 标记公共字段与专用字段

五类标记都保存以下公共字段：

| 中文名称 | 字段/类型 | 约束与实际效果 | 常见误配 |
| --- | --- | --- | --- |
| 标记 ID | `markerId` / string | 只需在当前房间内唯一；使用小写语义名和连字符；复制房间时保留 | 使用数组序号、空格或无意义 GUID，导致遭遇难以复用 |
| 中文显示名 | `displayName` / string | 只用于列表和 SceneView 标签，不参与运行时解析 | 修改显示名后误以为 Encounter ID 已同步 |
| 局部位置 | `localPosition` / Vector3，Unity 世界单位 | 相对房间实例根节点；必须是有限数值 | 把环境 Prefab 内部某层级的局部坐标直接抄入 |
| 局部旋转 | `localEulerAngles` / Vector3，度 | 相对房间实例根节点；必须是有限数值；不保存缩放 | 用可破坏物的缩放需求修改房间标记 |

专用字段：

| 标记类型 | 中文名称 | 字段/枚举 | 说明 |
| --- | --- | --- | --- |
| 敌人出生点 | 出生角色分类 | `role`: `Any` / `Melee` / `Ranged` / `Support` | 表示该位置适合的角色类别，不绑定敌人资产；v1 组合校验主要按 ID，分类供筛选和后续规则使用 |
| 可破坏物槽位 | 可破坏物 Prefab | `prefab` / GameObject | 必填；Prefab 完全拥有生命、掉落、破坏动画、行为和缩放 |
| 可到达点 | 适用对象 | `audience`: `Player` / `Enemy` / `PlayerAndEnemy` | 至少选择玩家或敌人；不保存边，不触发 A*/NavMesh 校验 |

推荐 ID 模式：

| 类型 | 示例 |
| --- | --- |
| 出口 | `exit-north-01`、`exit-left-01` |
| 玩家入口 | `player-main`、`player-return-south` |
| 敌人出生 | `enemy-main`、`enemy-melee-01`、`enemy-ranged-high-01` |
| 可破坏物 | `destruct-crate-01`、`destruct-pillar-02` |
| 可到达点 | `reachable-player-center`、`reachable-enemy-flank-01` |

## 主分组与标签

主分组代表内容生产和未来抽取的第一层稳定分类，例如“普通战斗房”“森林普通战斗房”。一个房间必须引用一个主分组，分组资产只保存自身资料，不反向保存房间列表；房间列表由编辑器扫描房间资产得到。

标签代表可组合条件，例如“狭长”“远程友好”“含掩体”“双出口”。一个房间可引用多个标签，也可以不引用标签。标签不表达权重、深度或奖励，不应把临时任务状态做成长期标签。

分组和标签资产都包含稳定 ID、显示名和策划说明。稳定 ID 一旦被内容使用，不应只为改中文显示名而修改。

## SceneView 编辑规则

- 五类工具用不同颜色和图标显示；先用显示开关缩小范围，再操作密集区域。
- 单击标记选择，使用移动/旋转 Handle 编辑；房间不支持标记缩放。
- 点击碰撞面放置时确认法线与期望朝向；投影到 XZ 平面后重点检查 Y 高度。
- 复制标记后立即修改 ID，避免同房间重复；复制整个房间时则保留标记 ID。
- 删除、复制、移动和旋转均应通过工具完成，以保留 Undo/Redo 和资产脏标记。
- “聚焦”只改变 SceneView 镜头，不改变标记数据。
- 环境 Prefab 更新后等待预览自动刷新；若预览处于未保存编辑状态，先完成或撤销当前标记操作。

## 校验规则与严重级别

错误会在 Room Editor 中标红，并使正式 Encounter 预览或 Host 启动按各自预检规则 Fail-Closed；警告允许继续编辑，但必须由内容负责人判断是否符合当前房间意图。

| 严重级别 | 检查项 | 处理方式 |
| --- | --- | --- |
| 错误 | 房间 ID 或显示名缺失、全局房间 ID 重复 | 重新生成/填写唯一资料；不要通过改显示名规避 ID 冲突 |
| 错误 | 环境 Prefab 缺失 | 绑定美术交付的独立 Prefab |
| 错误 | 主分组缺失或分组资产无效 | 绑定一个有效主分组并补齐其稳定 ID、显示名 |
| 错误 | 标签引用为空、标签无效或重复 | 删除空项/重复项，修复标签资产资料 |
| 错误 | 标记为空、ID 缺失或当前房间内重复 | 填写语义 ID；同房复制标记后必须改 ID |
| 错误 | 标记位置或旋转包含 NaN/Infinity | 用 SceneView Handle 重新摆放，不手改 YAML |
| 错误 | 没有玩家入口或没有敌人出生点 | 普通战斗房至少各配置一个 |
| 错误 | 可破坏物槽位未引用 Prefab | 绑定有效 Prefab，或删除尚未完成的槽位 |
| 错误 | 可到达点适用对象为空 | 至少选择 `Player` 或 `Enemy` |
| 警告 | 没有出口插槽 | 可继续编辑；但该房不得加入正式 `FpgRoomCatalog`，否则目录校验失败 |
| 警告 | 没有可到达点 | 可继续编辑；表示当前房间尚无策划声明的可达候选点 |

旧 CombatLab 的房间与 D0 遭遇组合校验仍由 `FpgRoomEncounterValidator` 执行，并检查：

- `D0CombatScenarioDefinition.playerSpawnPointId` 能否解析到当前房间的玩家入口；
- 每个 `D0EncounterSpawnSlot.spawnPointId` 能否解析到当前房间的敌人出生点；
- `AtSpawnPoint` 使用该点作为初始或替换姿态；`InheritPreviousGameplayPose` 在替换时继承上一活动实体姿态，但初始实体仍必须能解析出生点。

Room Editor 不再显示这组 D0 组合校验结果；它们只在 CombatLab 安装器、场景绑定和专项验证中使用。不要通过临时改 Scenario ID 让错误消失后再忘记恢复。

## 从 D0StageDefinition 迁移

`D0StageDefinition` 只作为一次性迁移来源和兼容回退，不再是新关卡的作者入口。

1. 选择旧 `D0StageDefinition` 资产。
2. 执行菜单 `FPG Demo > Room Migration > Migrate Selected D0 Stage`。首版迁移器使用 `Assets/FPGDemo/Config/Level/` 和 `Assets/FPGDemo/Presentation/Level/Environment/` 下的标准子目录，不覆盖已有同名目标。
3. 执行迁移：森林环境图层转换为独立环境 Prefab；`player-main` 转为玩家入口，`enemy-main` 转为敌人出生点，并保留局部位置与朝向。
4. 打开生成的房间，补齐显示名、标签、出口、可破坏物和可到达点。
5. 如仍维护旧 CombatLab，执行 `FPG Demo > Room Migration > Migrate CombatLab And Install Default Room`；安装器会用原 D0 Scenario 校验玩家与每个 SpawnSlot 的 ID。
6. 需要专项验证旧链路时，直接打开已安装的 CombatLab 场景进入 Play Mode；Room Editor 不再提供 D0 遭遇选择器或旧“一键试玩”入口。
7. 保留旧 Stage 资产，不删除、不改名；它继续用于兼容回退和迁移结果对照，但不再新增内容。

迁移只转换旧 Stage 已拥有的数据，不会推测出口、可破坏物、可到达点、分组或标签。生成后出现相关警告是预期的待补配置，不代表迁移失败。

## 旧 CombatLab 绑定

`D0CombatScenarioDefinition` 仍由 `BattleScenarioConfig`、`FpgRoomCombatLabBinding` 和 `BattleSceneContext` 消费，因此不能删除 D0 遭遇资产或运行时校验器。但这条链只服务旧 CombatLab 场景，不是当前正式房间作者入口：

1. Room Editor 不显示 D0 `遭遇配置` 对象框，也不显示旧“一键试玩”按钮。
2. 默认绑定由 `FPG Demo > Room Migration > Migrate CombatLab And Install Default Room` 安装并校验；当前 CombatLab 的固定表现只接受 `BattleScenarioConfig.AuthoredScenario`。
3. 需要验证旧链路时，保存 RoomDefinition、主分组/标签、D0 遭遇和环境 Prefab，再直接打开 CombatLab 场景进入 Play Mode。
4. 正常验证不应产生 `CombatLab.unity`、房间定义或遭遇资产 diff。若退出后这些资产变脏，先不要提交，记录复现步骤并交给程序检查。

## Git 协作边界

| 修改者 | 正常应产生的主要 diff | 不应产生的 diff |
| --- | --- | --- |
| 关卡美术 | 环境 Prefab 及其依赖的模型、材质、贴图 | `FpgRoomDefinition`、Encounter、`CombatLab.unity` |
| 关卡策划 | `FpgRoomDefinition`；必要时分组/标签资产 | 环境 Prefab 内部美术层级、`CombatLab.unity` |
| 可破坏物负责人 | 可破坏物 Prefab 及行为/表现依赖 | 每个房间中复制的生命、掉落和缩放覆盖 |
| 程序 | Runtime、Editor 工具、迁移器和场景桥接 | 为单个房间硬编码坐标或 ID |

协作约定：

- 美术和策划分别提交，避免把环境 Prefab 与房间标记调整混在一个提交中。
- 主分组/标签属于共享词表，改稳定 ID 前先通知内容团队；改显示名通常不需要迁移房间。
- 同一时刻尽量只由一人修改同一个 RoomDefinition；v1 不提供实时合并。
- 发生 YAML 冲突时不要盲目接受整文件任一侧。先在编辑器中对照标记 ID，再由资产负责人重做冲突部分。
- 打开房间、刷新 Prefab、切换筛选和生成正式 Encounter 预览不应产生内容 diff。

## 示例房间

```text
roomId: room-forest-combat-001（创建后只读）
displayName: 森林普通战斗房 01
mainGroup: 普通战斗房
tags: [森林, 中距离, 双出口]
environmentPrefab: ENV_Forest_Combat_01

playerEntryPoints:
  - player-main
enemySpawnPoints:
  - enemy-melee-01 (Melee)
  - enemy-ranged-01 (Ranged)
exitSlots:
  - exit-north-01
  - exit-south-01
destructibleSlots:
  - destruct-crate-01 -> PRP_DestructibleCrate
reachablePoints:
  - reachable-player-center (Player)
  - reachable-enemy-flank-01 (Enemy)
```

若 Scenario 使用 `player-main`、`enemy-melee-01` 和 `enemy-ranged-01`，它可以与该房间组合；复制房间后保留这三个标记 ID，同一 Scenario 也可以直接复用。

## v1 验收与交接

最小技术检查：Unity 编译和 Console 无相关错误；房间结构校验与正式 Encounter 预览结果正确；旧 CombatLab 绑定由安装器或专项验证校验；保存重开后数据一致；打开编辑器和生成预览不产生内容 diff。

### 手工功能检查

- [ ] 创建房间后自动获得全局唯一房间 ID，复制房间生成新房间 ID 并保留标记 ID。
- [ ] 搜索、主分组、标签和校验状态筛选能定位预期房间。
- [ ] 五类标记均可放置、选择、移动、旋转、复制、删除、聚焦和切换显示。
- [ ] 有/无环境碰撞面时的点击放置分别命中表面和房间 XZ 平面。
- [ ] 网格吸附、Undo/Redo、保存重开后的结果一致。
- [ ] 环境 Prefab 改动后预览自动刷新，且不改 RoomDefinition。
- [ ] 策划移动标记只产生当前 RoomDefinition diff。
- [ ] 结构错误正确标红，缺出口/可到达点只显示警告；无效正式 Encounter 请求 Fail-Closed。
- [ ] 正式预览使用当前房间与所选 Profile/Override，运行后不写回 Room、Encounter 或场景资产。
- [ ] 旧 Stage 迁移后的环境、`player-main` 和 `enemy-main` 姿态与迁移源一致。
- [ ] 清场前出口完全隐藏，最后一波和生成队列都清空后才显示出口与目的地标签。
- [ ] 标签目的地与实际进入房间一致；清场时持续按住主攻击不会误触发，松开后主/副攻击均可选择。
- [ ] 连续切房 5-10 次后只保留当前环境、玩家、出口注册和事件订阅。

### 待主管验收表

| 编号 | 测试项 | 前置条件 | 主管操作 | 通过标准 | 证据/记录栏 | 状态 | 备注/风险 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| H-01 | 美术工作流可用性 | 准备一个环境 Prefab 和一个已绑定房间 | 修改环境 Prefab，返回 Room Editor 检查并提交 diff | 预览刷新明确，操作不要求修改 RoomDefinition，diff 边界符合职责 | 操作人、日期、问题记录 | 待主管试玩/确认 | 重点检查 Prefab Mode 往返体验 |
| H-02 | 策划摆点效率 | 打开包含完整环境的普通战斗房 | 完成五类标记的新增、调整、复制、删除和筛选 | 标记易识别、定位准确、常用操作无需修改 YAML | 操作人、日期、录屏/问题记录 | 待主管试玩/确认 | 密集标记区域重点观察遮挡 |
| H-03 | 校验信息可理解性 | 准备缺环境、重复 ID、缺出口等错误/警告样例 | 查看校验结果并按提示定位修复 | 错误与警告边界清晰，点击结果能找到需修改内容 | 操作人、日期、问题记录 | 待主管试玩/确认 | 文案不应承诺 v1 未实现能力 |
| H-04 | 正式 Encounter 预览闭环 | 准备有效 Room、Profile 和可选 Override | 生成预览；需要运行时验证时在 FormalRoom Play Mode 中启动 Active Formal Host | 预览与 Host 使用相同计划摘要，且 Room、Encounter、场景资产无额外 diff | 操作人、日期、录屏、Git 状态 | 待主管试玩/确认 | 战斗手感不属于房间工具技术验收 |
| H-05 | Git 协作边界 | 美术与策划分别做一次独立修改 | 对比两次提交的文件范围 | 美术主要改环境 Prefab，策划主要改 RoomDefinition，无 CombatLab diff | 提交哈希、审阅记录 | 待主管试玩/确认 | v1 是异步 Git 协作，不是实时多人编辑 |

## 后续扩展时不得破坏的合同

- 房间仍是完整手工环境，不把 v1 标记变成运行时地形拼装块。
- Room 与 Encounter 保持独立，空间解析继续留在 `FPG.Unity`，不把 Unity 坐标或 Prefab 传入 `FPG.Run`。
- 房间 ID 和标记 ID 继续作为稳定合同；新增抽房规则不应通过改写已有 ID 实现。
- 可到达点在接入导航校验前仍是策划声明数据；不得把“已配置”描述成“已证明可达”。
- 基础出口连接已经由 `FpgExitRoomRefreshRule` 与 `FpgRoomCatalog` 拥有；后续权重、奖励、房间类型和按深度过滤也应扩展该规则，不塞入 v1 编辑器的临时备注或隐藏字段。

## 正式 Encounter 边界

`FpgRoomDefinition` 只拥有完整环境引用、出口、玩家入口和敌人 `SpawnPoint` 等空间信息。具体敌种、波次、预算、难度、召唤和运行状态均不属于 Room，也不得由 RoomGroup 或标签反向持有。

正式房间在进入前由 `FpgRoomRunRequest` 组合以下四项：

- `RoomDefinition`：提供环境、出口和 SpawnPoint；
- `EncounterProfile`：提供预算、波次、池、时序、距离和固定容量；
- `EncounterOverride`：提供可选的固定波次、强制/排除敌种或预算锁定；
- `RunContext`：提供 Seed、Region、Depth、难度 basis points 和访问序号。

`FPG Demo > Room Editor` 的 `Formal Encounter Preview` Foldout 只在内存中保存 Profile、Override、Seed、Depth、难度和访问序号，并调用与运行时相同的 Request/Plan 生成入口。正式预览和正式试玩请求都必须通过内存覆盖传参，不得把 Encounter 引用或试玩参数写回 Room、Profile、Override、场景或其他资产。旧 D0 `遭遇配置` 选择器和“一键试玩”按钮不再暴露；底层 D0 CombatLab 资产与运行时绑定继续保留。

加权波形布局由 `FpgEncounterProfile.weightedWaveLayouts` 独占：Room/RoomGroup 不保存布局权重或波次份额。生成后的 `FpgEncounterPlan` 明确记录 `WaveLayoutId`，每个 Wave 明确记录 `BudgetShareBasisPoints`、请求预算和实际消耗，房间编辑器只读展示这些结果。

`Run in Active Formal Host` 仅在 Play Mode 且场景内恰有一个已加载、非持久化 `FpgEncounterHost` 时启动；缺少或存在多个正式 Host、Host 绑定/容量失败、或预览与运行时 digest 不一致时立即 Fail-Closed，绝不调用旧控制器或回退 CombatLab。正式 Run 从 Boot 加载并保留通用 `FormalRoom`，跨房时不加载独立房间 Scene，而是在同一 Host 内清理并重建目标 Room。

## 保留 FormalRoom 的跨房合同

`FpgRunFlowController` 只允许 `Running -> AwaitingExit -> Transitioning` 的单向房间访问流程；任何刷新、资源捕获、组合或启动错误都进入 `Faulted`。切房顺序固定如下：

1. 第一次正式攻击命中出口后关闭全部出口和输入，捕获攻击已扣除弹药后的 `FpgPlayerRunResourceState`。
2. 调用 `StopAndClear` 清除旧环境、玩家、敌人、出口 Hitbox/注册表和事件订阅，再等待一次 `WaitForEndOfFrame`，避免 Unity 延迟销毁对象与新房重叠。
3. 在同一个已加载的 `FormalRoom` 中设置 offer 指向的 Room，重新组合相同角色，并通过 `FpgEncounterStartRequest` 显式传入新 `RunContext` 和可选玩家资源。
4. 资源必须在 `Session.Start` 前导入。生命、护盾、弹药、护盾恢复剩余 Tick 和恢复比例跨房保留；角色/武器稳定 ID 必须与重新组合结果兼容。武器冷却、装填、蓄力、输入序号和 Exposure 等瞬态从新房入口重置为 `Ready/Exposed`。
5. 新上下文的 `Depth` 与 `RoomVisitOrdinal` 各加一，`RunSeed`、Region、难度倍率、Encounter Profile 和 Override 保持不变。全部启动成功后才原子更新 `SelectedRoom` 和活动 Host。

若任一步失败，Bootstrap 再次 `StopAndClear`，保留 `LastError`，清空活动房间引用并恢复 Boot 的房间选择交互。旧出口不得重新开放，也不得留下半初始化环境或 Hitbox。
