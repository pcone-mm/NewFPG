# FPGDemo LevelAuthoring Editor 指南

这个目录是 FPGDemo 房间编辑、Scene View 标记、CombatLab 房间安装和 Formal Encounter 预览/试玩工具层。asmdef 是 `FPG.LevelAuthoring.Editor`，命名空间是 `FPG.Demo.Editor.LevelAuthoring`。

## 目录边界

- `FpgRoomEditorWindow`、`FpgRoomEditor.uxml`、`FpgRoomEditor.uss` 是房间资产浏览、编辑和校验面板。
- `FpgRoomSceneTool` 负责 Scene View marker 操作；所有 marker 字段名、中文名和颜色走 `FpgRoomAuthoringSchema`。
- `FpgRoomCombatLabInstaller`、`FpgRoomStageMigrationTool` 和 `FpgRoomPlaytestController` 负责把房间资产安全接入 `Scenes/CombatLab.unity`。
- `FpgEncounterPreviewWindow` 与 `FpgEncounterPreviewUtility` 只生成内存里的正式遭遇预览；`FpgFormalEncounterDefaultsInstaller` 负责创建默认正式遭遇资产。
- `FpgFormalRoomLoopInstaller` 维护 playable character catalog、Boot 选择、FormalRoom 玩家/遭遇服务链和 Build Settings；它是整环安装入口，不要用临时场景脚本复制这套绑定。

## 工作规则

- Editor 工具必须通过 `SerializedObject`、`AssetDatabase`、`Undo`、`EditorSceneManager` 或现有 schema 改资产/场景，不要批量手写 Unity YAML。
- 安装器和迁移工具保持可重复运行；缺少输入时给出明确错误，不要静默回退到旧 Stage 或隐藏桥接对象。
- Formal loop 安装器必须保留调用前的 Scene setup；FormalRoom 不预放玩家实体，Boot 预览保持 visual-only，build index 维持 Boot/CombatLab/FormalRoom 为 0/1/2。
- Formal Encounter 预览默认只读：预览按钮不得 mark dirty 房间、profile、override 或场景；只有名字明确为 install/migrate/playtest 的入口才允许写入。
- 新增可见字段时保留 C# 字段名/YAML 键，中文展示名和说明基于真实数据流写在 `D0PlannerField` 或 `FpgRoomAuthoringSchema` 映射里。

## 验证

- 改房间编辑器、场景安装器、试玩桥或 Formal Encounter 预览后，优先看 `Assets/FPGDemo/Tests/PlayMode/SceneContractTests.cs`。
- 改 Formal loop installer、Boot 角色选择或 build scene 合同时，再看 `Assets/FPGDemo/Tests/EditMode/FormalFirstAuthoringContractTests.cs` 和 `BuildSettingsTests.cs`。
- 改 D0 scenario/stage 兼容逻辑时，再看 `Assets/FPGDemo/Tests/EditMode/D0CombatScenarioDefinitionTests.cs` 和 `Assets/FPGDemo/Tests/EditMode/D0StageDefinitionTests.cs`。
