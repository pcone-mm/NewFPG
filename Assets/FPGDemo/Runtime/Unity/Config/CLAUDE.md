# Unity Config 指南

本目录保存正式 Unity 配置定义与运行时适配器。

- `FpgEnemy*`、`FpgEncounter*`、`FpgFormal*`、统一技能定义和 `FpgPlayableCharacterCatalog` 是正式配置合同。
- `D0WeaponDefinition` 同时保存 ID 不同的 Immediate 与 Charge Secondary 稳定引用；`SecondaryTriggerMode` 只选择匹配模式的技能投影到 runtime，但两份资产始终都要独立校验，不能恢复单一可变 `secondarySkill`。
- `D0CharacterDefinition` 拥有 base attack speed、bonus ratio 与 cap；`D0ThreeCProfile` 只保留角色控制、输入缓冲、朝向翻转与掩体过渡时序等角色合同。准星输入按设备分离：鼠标使用 sensitivity 与固定 authored reference resolution，手柄使用 viewport speed、径向 deadzone、response exponent 和 unscaled delta time；不要合回旧单一 `reticleSensitivity`，也不要按输出分辨率或渲染帧数改变手感。静态镜头 Pose、FOV 和裁剪面归 `Config/Level/CameraProfiles`，不得在适配器中提供隐式兜底。
- `FpgSkillGameplayActionDefinitions.cs` 与 `FpgSkillPresentationDefinitions.cs` 分别拥有类型化 gameplay action、action-node impact/trajectory/collision 表现和 active presentation track；正式资产只接受 schema V3。
- 每个 skill sequence 自己拥有 attack timing mode、windup attack-speed coefficient 与 different-attack interrupt tick；CharacterAttackSpeed 模式必须恰好有一个 Attack 且没有其他 gameplay action，攻击帧取该 authored Attack tick。不要把这些字段迁到 Weapon、ThreeC 或调参快照。
- 玩家技能中含 Attack/Projectile event 的每个序列都必须声明 `allowWithdrawTick`，且该 tick 严格晚于最后一次攻击、不超过序列 duration；无攻击序列才可保留 `-1`。这是 gameplay 暴露与 hash 合同，不从动画结束时间推导。
- Enemy `SelfDestructOwner` 只能 target Self、不能带 socket/offset；若设置绑定，只能指向同 tick 较早的 Summon event。不要恢复 `summonOwnerOutcome` 隐式字段。
- 正式资产仍引用的 D0 前缀类型是序列化/GUID 兼容合同；不得据此建立第二套运行入口。
- `FpgFormalConfigAdapters` 只转换数据，不引入新战斗决策或按敌人 ID 特判。
- character/enemy/pool/profile/override/catalog ID，以及 skill stable ID、authored ordinal、gameplay/presentation/timing hash 是跨资产稳定合同。
- trajectory prefab 根节点必须有可校验的 `FpgTrajectoryVfxView`；正式配置只引用 `Presentation/Characters/Players/*/VFX/PF_FPG_*` 或 `Presentation/Characters/Enemies/Shared/VFX/PF_FPG_*` wrapper，不直连供应商 demo。
- 缺少引用、ID 冲突、容量不足或动画/Prefab 校验失败时必须 fail-closed。
- Behavior 的 `animationRootMotionRules` 是逐动画显式 allowlist；缺少规则即禁用，重复动画名或无效 Spine bridge 配置必须 fail-closed。
- 修改后检查对应 `TryValidate/TryBuildData`、Unity 编译/Console、`CombatAimViewportMathTests.cs`、`FpgShootingTuningSnapshotTests.cs`、`FpgSkillDefinitionTests.cs`、`FpgPlayerSkillAssetContractTests.cs`、`FpgAttackTimingTests.cs`、`FpgAttackTimingHashAndWeaponSnapshotTests.cs`、`FpgPlayerSkillExecutionControllerTests.cs`、`FpgFormalSkillPresentationV3AssetTests.cs`、`FpgFormalEnemyRootMotionAssetTests.cs` 与现存的精确 Formal EditMode 合同。
