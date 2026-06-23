# Editor 工具指南

这个目录放 Unity Editor 专用安装器、导入器、修复器和调试菜单，命名空间是 `NewFPG.EditorTools`。

## 职责

- `NewFPG/...` 菜单项、场景/Prefab 安装器和运行时探针。
- 美术/材质/Blender 导入后的 Unity 侧修复工具。
- 只在编辑器内运行的资源生成、动画安装、标签和序列化引用绑定。

## 边界

- 运行时玩法逻辑放 `Assets/Scripts/` 对应模块，不要藏在 Editor 菜单里。
- 安装器可以写 prefab、场景和 `ProjectSettings/TagManager.asset`，但应保持幂等：重复执行不能重复添加组件、状态或 Tag。
- 改 prefab/场景绑定时优先通过 `SerializedObject`、`PrefabUtility`、`AssetDatabase`、`Undo` 和 `EditorSceneManager`，避免手动大范围改 Unity YAML。

## 验证方式

- 修改安装器后，在 Unity 里执行对应菜单项，并检查 Console。
- 影响 `LevelFlowDirector` 或 Combat 基础安装时，优先跑相关 Editor 测试，再手动检查 Player、Fish、FirstPersonWeaponView prefab 和当前打开场景绑定。
