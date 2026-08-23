# Feel 供应商指南

本目录是项目内 vendored 的 More Mountains Feel `6.0`，不是 FPGDemo gameplay 或项目自有表现代码的归属地。

- `MMTools/`、`MMFeedbacks/`、`NiceVibrations/` 与 demo 目录都按供应商资产处理；保留 `readme.txt`、`license.txt`、目录结构、`.meta` 和 GUID，导入或升级时成套审查。
- 项目适配、正式 prefab 和重建工具只放在 `Assets/FPGDemo/Integrations/Feel/`；不得在本目录新增 `FPG.Demo.*` 业务代码、正式配置或场景入口。
- 正式 FPG 资产不得引用 `FeelDemos/`、`FeelDemosHDRP/` 或插件 demo prefab；只通过项目适配层使用所需运行时类型。
- 不为绕过项目生命周期、对象池或事件边界而修改供应商源码。Unity 版本兼容补丁必须显式、最小，并在插件升级时单独复核。
- 当前 Unity 6 兼容点是 `FindObjectsByType` 必须显式传 `FindObjectsSortMode.None`；升级后用 `rg -n "FindObjectsByType\\(" Assets/Feel -g "*.cs"` 复核。
- 修改或升级后先检查 Unity 编译/Console，再运行 `FpgSupplementalFeedbackTests.cs` 与 `FpgFeelEnemyHitAssetTests.cs`；同时确认正式场景和 prefab 没有引用 demo 资产。
