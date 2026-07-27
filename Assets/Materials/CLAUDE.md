# Materials 指南

本目录是遗留原型材质库，不是正式 FPG 表现入口。

## 目录边界

- `Prototype/` 保存旧原型和 ProBuilder 材质。
- `Dongfu/` 保存已删除洞府场景留下的洞壁、地面、木材、炉火和传送门材质。

## 工作规则

- 不以已删除的 `Dongfu_Home.unity` 或旧 Prototype 场景作为验证入口。
- 正式采用前先做 GUID 反向引用审计；需要项目自有派生材质时放入 `Assets/FPGDemo/Presentation/Materials/` 或对应正式表现子目录。
- 移动或派生材质时保留源材质与 `.meta`，并检查 shader、texture、render queue 和 URP surface 设置；不要顺手改全局渲染管线。

## 验证

- 视觉改动只在当前真实引用该材质的 prefab/scene 中检查；没有正式引用时明确标为候选素材，不宣称已接入。
