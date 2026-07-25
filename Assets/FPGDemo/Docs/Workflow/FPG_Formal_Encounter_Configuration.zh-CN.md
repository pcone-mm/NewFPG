# FPG Formal Encounter 配置指南

## 权威资产

- 玩家：`Config/FormalEncounter/Characters/FPG_Fei_*.asset`
- 玩家技能：`Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset`、`FPG_Fei_Secondary.asset`、`FPG_Fei_Reload.asset`
- 玩家 catalog：`FPG_PlayableCharacterCatalog.asset`
- 普通房基础：`FPG_NormalRoom_Profile.asset`、EnemyPool、EnemyCatalog
- 敌人：`FPG_Burstbug_*`、`FPG_Hudie_*`、`FPG_Luan_*`
- Luan 召唤：`FPG_Luan_Attack_Summon.asset` 内的本地 Summon 载荷槽
- 表现：`FPG_CombatPresentationProfile.asset`
- 正式 prefab：`Presentation/FormalEncounter/PF_FPG_*Entity.prefab`

这些 committed 资产是权威真源，不再从 D0Slice、CombatLab 或 defaults installer 迁移。

## 引用链

`FpgEncounterProfile -> FpgEnemyPoolDefinition -> FpgEnemyDefinitionCatalog -> FpgEnemyDefinition -> Behavior/Skill/EntityPrefab`

敌人攻击资产本身就是正式技能定义，逻辑事件通过本地载荷槽描述投射物、定时打击或召唤。Wave/Override 只选择池、预算、波次和时序，不拥有 Entity prefab 或 Room。

玩家链为：

`FpgPlayableCharacterCatalog -> D0CharacterDefinition/D0ThreeCProfile -> D0WeaponDefinition -> Primary/Secondary/Reload Skill -> FpgPlayerEntityView`

其中 D0 前缀仅为序列化兼容，不表示旧主线。

## 配置规则

- 所有 character/enemy/attack/pool/profile/override/catalog ID 必须唯一且稳定。
- 技能时间轴固定为 60Hz 整数 Tick；Spine 事件只用于参考，不得直接提交伤害、换弹或召唤。
- 每个逻辑事件显式引用技能本地载荷槽，不配置隐藏 Delay/Repeat；同 Tick 按 `authoredOrdinal` 顺序执行。
- 动画长度变化只产生验证提示，不会自动移动逻辑事件。
- Profile、Override、Pool、Catalog 与所有被引用的技能资产必须成套校验。
- Enemy 的动画键必须存在于绑定 SkeletonData。
- 普通波次和 `EncounterSpawnPoint` 召唤必须有兼容的 Room spawn role；`OwnerPosition` 召唤不读取或占用房间出生点。
- 缺少引用、容量不足、ID 冲突或动画缺失时 fail-closed。
- 不在 adapter、installer 或 runtime 中按敌人 ID 特判。

## 召唤策略

怪物技能资产内的 Summon 载荷槽用两条独立配置轴描述召唤，不由房间或敌人 ID 推断：

- `Occupancy Mode = AdditionalEntity`：召唤物是额外战斗单位，执行 `Max Per Owner`、`Max Per Encounter`、房间同屏数量和 Cap Weight 检测。
- `Occupancy Mode = ReplaceOwner`：召唤物替换施法者，不执行上述玩法数量检测；对应攻击必须配置 `DieAfterSuccessfulSummon`，两个未使用的 Max 字段必须为 `0`。
- `Placement Mode = EncounterSpawnPoint`：走普通房间选点、角色兼容与点位占用。
- `Placement Mode = OwnerPosition`：技能释放并提交 Spawn Queue 时，按施法者实体根节点快照世界位置与朝向；不选点、不占点，也不因房间点位不足重试。

两种策略都必须携带稳定召唤能力 ID 并进入统一 Spawn Queue。普通召唤的 per-owner 配额按 `owner + payload slot ID` 分桶，ReplaceOwner 既不占用也不消耗该玩法配额。固定 roster、队列、hitbox、稳定序列和递归深度仍由预检按召唤图推导；实体池预热则由同一预检按候选敌人类型分别给出容量，Director 不再重复推测召唤图。这些是固定内存与事务完整性边界，不是房间玩法数量限制。

## 修改流程

1. 从 `FPG Demo/Skill Editor` 打开统一技能编辑器，选择角色或怪物技能资产。
2. 在整数 Tick 时间轴中编辑动画、阶段、逻辑事件、表现事件和预警；播放、逐 Tick 与拖动预览都使用内存中的正式编译结果。
3. 修复验证面板中的阻塞错误。无效 WIP 可以保存，但正式配置预检必须失败。
4. 修改其他权威 `FPG_*` 资产，并用 Formal Encounter Preview 检查 Request/Plan/digest；预览不得写回资产。
5. 必要时运行 `FpgFormalRoomLoopInstaller` 刷新 Boot/FormalRoom、HUD、出口与 Build Settings。
6. 检查 Unity 编译/Console、EditMode 技能合同测试与对应 Formal 配置合同。

技能编辑器支持缩放、平移、吸附、多选、批量移动、复制粘贴、Undo/Redo、循环和变速。预览 Prefab 与最多四个 Body/Weakpoint 假人只用于编辑器求值，不会修改正式资产或启动 PlayMode。

运行时生命周期与事务规则见 [FPG_Formal_Encounter_Runtime_Contract.zh-CN.md](FPG_Formal_Encounter_Runtime_Contract.zh-CN.md)。
