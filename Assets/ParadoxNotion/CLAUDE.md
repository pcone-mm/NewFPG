# ParadoxNotion 指南

本目录是 vendored NodeCanvas/CanvasCore 供应商源码，不保存 NewFPG 自研玩法、配置或正式场景。

- `CanvasCore/ParadoxNotion.asmdef` 提供 `ParadoxNotion`，`NodeCanvas/NodeCanvas.asmdef` 提供自动引用的 `NodeCanvas`；不要随手改程序集名、GUID、命名空间或序列化类型。
- 当前正式 `Assets/FPGDemo/` asmdef 没有显式引用这两个程序集，只有 Standalone 配置包含 `NODECANVAS` define。没有明确集成任务时，不把 NodeCanvas 当作正式运行依赖，也不在供应商目录内添加项目逻辑。
- 需要正式接入时，把窄适配放在 `Assets/FPGDemo/Integrations/`，先确认目标平台 define、程序集依赖、图资产所有权和 AOT/link 需求；领域程序集不得直接依赖供应商 API。
- 插件升级必须把 `CanvasCore/` 与 `NodeCanvas/` 视为同一供应商载荷，保留所有 `.meta` 并单独审查 deprecated 文件、序列化兼容和正式消费者，禁止顺手改第三方源码。
- `NodeCanvas/README.txt` 声明修改版 Full Serializer 使用 MIT License；升级或迁移时必须保留 `CanvasCore/Common/Runtime/Serialization/Full Serializer/License (FullSerializer).txt` 及其 `.meta`。
- 修改或升级后检查 Unity 编译/Console、`ParadoxNotion` -> `NodeCanvas` 程序集关系、目标平台 define，以及所有实际 graph/owner 消费者；供应商 sample 不能替代正式 FPG 验证。
