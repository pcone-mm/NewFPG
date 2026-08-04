# 顶层 Editor 指南

本目录只保存项目级、跨模块的 Editor 启动与设置工具；正式房间、技能和构建 authoring 归 `Assets/FPGDemo/Editor/`。

- `CodexCharacterSortingSetup` 用于已经落地的 `Character` Sorting Layer 与 Fei renderer 状态，但仍会在 Editor session 中尝试保存 `ProjectSettings/TagManager.asset` 和正式 prefab。把它视为待确认生命周期的迁移残留，不是长期真源；新的修复必须改为用户显式触发的 `MenuItem`/installer，禁止在 assembly reload 时持久化项目设置或正式资产。
- `UnitySkillsProjectAutoStart` 在 Editor 启动后启用 Unity Skills server，并每个 session 向 GitHub 检查一次版本；网络失败必须保持非阻塞，包版本仍以 `Packages/manifest.json` 的固定引用为准。
- `SpineSettings.asset` 是 spine-unity 的 Editor 配置，不承载正式角色、动画或战斗配置。
- 顶层脚本留在 Editor-only 默认程序集，不得向 runtime 或纯领域程序集扩散 `UnityEditor`、联网或项目资产写入依赖。
- 修改后检查 Unity 编译与 Console；涉及排序初始化时同时核对 `Character` layer ID 和 Fei renderer，涉及 Unity Skills 时核对 server 启动与一次性更新检查。当前没有该目录的直接合同测试。
