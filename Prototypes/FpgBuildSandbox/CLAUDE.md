# FpgBuildSandbox 局部指南

## 职责边界

- 本目录是独立的 Vite + TypeScript + Three.js 构筑沙盒，所有内容均为非正式测试数据；不得把它当作 Unity 入口，也不得直接修改或复用 Unity 场景、Prefab、Spine、VFX 或 `Assets/FPGDemo/Config/`。
- `src/game/` 保存确定性局内逻辑；`build-content.json` 是灵物、神眷、灵蕴和羁绊的唯一内容入口，`types.ts` 的 schema 与受支持 effect handler 是数据合同。
- `GameController.ts` 拥有状态机和公开控制 API；`src/render/` 只消费快照做表现，不用 Three.js Mesh 决定命中；`src/ui/` 负责界面、音频和仪式交互，`src/main.ts` 只做装配、输入和固定步进驱动。
- `tests/e2e/` 覆盖真实浏览器流程、视口和 WebGL 像素变化；`window.__FPG_SANDBOX__` 是测试桥接面，不是正式产品 API。

## 局部约定

- 战斗模拟保持固定 `60Hz`，随机流程必须可由 seed 重现；不要把游戏结果绑定到渲染帧率或 Three.js 场景状态。
- JSON effect 只接受 `statAdd`、`statMultiply`、`eventDamage`、`eventCover` 和 `eventAmmo`，不得从内容数据执行任意脚本。
- 新内容统一写入 `factionTags`；`lineage` 等旧字段只用于加载迁移，不扩展成第二套内容模型。
- `localStorage` 只在稳定流程边界保存；修改持久化结构时同步维护 schema version、迁移和确定性测试。
- 最低桌面视口是 `1280 x 720`；移动端只保留兼容提示，不把移动端布局扩展为未约定的正式玩法入口。
- `node_modules/`、`dist/`、`test-results/`、`playwright-report/`、`.tmp-content/`、`.vercel/` 与 `*.tsbuildinfo` 都是本地生成物，不提交也不作为规则证据。

## 验证

```powershell
npm run typecheck
npm test
npm run build
npm run test:e2e
```

- 纯领域或内容改动先跑 typecheck 与 Vitest；输入、UI、渲染、视口或完整流程改动再跑 Playwright。没有执行的验证不得写成已通过。
