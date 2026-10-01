# FPGDemo Tests 指南

Demo 阶段只保留少量入口冒烟测试，不把测试套件当作每次功能交付的默认步骤。默认验证是 Unity 编译、Console 和人工试玩；禁止自行运行全量 EditMode/PlayMode。

## 保留范围

- EditMode：`BuildSettingsTests.cs`、`FormalFirstAuthoringContractTests.cs`、`FpgRoomDefinitionTests.cs`、`FpgRoomArtSceneContractTests.cs`、`FpgSkillRuntimeTests.cs`、`WeaponRuntimeTests.cs`。
- PlayMode：`FpgBattleTestPlayModeTests.cs`，只在用户明确要求验证 BattleTest 场景生命周期时运行。
- 只有用户明确要求，且改动直接命中上述合同，才运行对应的单个测试类；不要因为进入本目录或看到测试程序集就启动 Test Runner。

## 边界

- `EditMode/` 覆盖少量正式入口、房间/Art Scene 配置和核心技能/武器运行时；`PlayMode/` 仅保留 BattleTest 场景冒烟。
- 不在测试目录新增历史阶段测试、重复合同、旧入口测试、第三方 Demo 测试或 Test Runner 输出。
- 新依赖必须加入对应 test asmdef；不得为了测试让 runtime asmdef 反向引用 Editor 或 test assembly。

## 隔离与恢复

- 测试创建的资产、场景和文件夹使用唯一临时路径，并在 `finally` / `TearDown` 中删除；修改 Build Settings、active/loaded scene 或静态 override 时，即使断言失败也要恢复原状态。
- 不修改正式 authored 资产来布置 fixture；需要真实序列化/GUID 行为时，通过 Unity Editor API 创建临时资产。
- 只有当前 Test Runner 结果或持久化 XML 能证明测试通过；缺少结果时只报告“未运行”。
