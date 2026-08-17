# 飞光录一层构筑沙盒

独立的 Vite + TypeScript + Three.js 验证项目。所有内容均为非正式测试数据，不修改或复用 Unity 场景、Prefab、Spine、VFX 与正式配置。

## 运行

```powershell
npm install
npm run dev
```

最低桌面视口为 1280 × 720。移动端只显示兼容提示。

## 操作

- 鼠标：瞄准；左键按住连续主射；空弹后再次按左键自动换弹；右键按住蓄力、松开副射并消耗灵能
- `A` / `D`：切换掩体
- `R`：换弹
- `G`：灵气满后聚气
- `E`：场景交互或回收清房奖励
- `B`：构筑；`M`：地图；`Esc`：暂停；`F5`：同种子重开

三个掩体各自保存耐久；敌方投射物会在实际飞行路径上命中对应掩体，换位后 HUD 会显示新掩体的剩余耐久。右键副射消耗 35 点灵能，灵能会随时间恢复。

## 数据与边界

- [build-content.json](./src/game/build-content.json)：16 件灵物、6 个神眷、6 个灵蕴、统一流派标签和物品类型羁绊的唯一内容入口。旧版“祀属”和“神流派”字段会在加载时归一为 `factionTags`。
- [GameController.ts](./src/game/GameController.ts)：局内状态机和公开控制 API。
- [combat.ts](./src/game/combat.ts)：固定 60Hz 战斗模拟，不依赖 Three.js Mesh 命中。
- [GameRenderer.ts](./src/render/GameRenderer.ts)：只读快照表现与程序化建木森林位图纹理。
- `localStorage` 只在稳定流程边界保存，分析数据可从暂停或结算界面下载为 JSON。

JSON 仅接受 `statAdd`、`statMultiply`、`eventDamage`、`eventCover` 和 `eventAmmo` 处理器，不执行任意脚本。

## 验证

```powershell
npm run typecheck
npm test
npm run build
npm run test:e2e
```

Playwright 覆盖真实射击命中、换掩体、换弹、蓄力副射、完整胜利路线、真实五点一笔画、重投、商店、合灵、重铸、失败结算、同种子重开、WebGL 像素变化，以及 1440 × 900、1920 × 1080 和 390 × 844 视口。
