import { expect, test, type Page } from "@playwright/test";

async function battle(page: Page): Promise<void> {
  await page.goto("/?e2e=horde");
  await page.getByRole("button", { name: "开始新局" }).click();
  await page.getByTestId("offer-blessing").first().click();
  await page.getByTestId("map-node-n1a").click();
  await expect(page.getByTestId("combat-hud")).toBeVisible();
}

test("aims at flying insects and absorbs real multikill experience; overlays freeze everything", async ({ page }) => {
  const errors: string[] = []; page.on("pageerror", (error) => errors.push(error.message));
  await page.setViewportSize({ width: 1440, height: 900 }); await battle(page);
  // Known geometry fixture isolates airborne aiming from director movement/random rolls.
  const target = await page.evaluate(() => {
    const c = window.__FPG_SANDBOX__.getSnapshot().state.combat!;
    c.horde.mode = "legacy"; c.spawnQueue = []; c.enemies = [
      { id: "air-fixture", type: "flyer", layer: "air", position: { x: 0, y: 4, z: 12 }, hp: 12, maxHp: 12, shield: 0, attackCooldown: 9999, spawnTick: 0, behavior: "windup", windupTicks: 9999 },
      ...Array.from({ length: 8 }, (_, i) => ({ id: `cluster-${i}`, type: "melee" as const, layer: "ground" as const, position: { x: (i % 4) * 0.9 - 1.35, y: 0, z: 10 + Math.floor(i / 4) }, hp: 18, maxHp: 18, shield: 0, attackCooldown: 9999, spawnTick: 0, behavior: "windup" as const, windupTicks: 9999 })),
    ]; return window.__FPG_SANDBOX__.worldToScreen(0, 12, 5);
  });
  await page.mouse.click(target.x, target.y);
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat!.feedbackEvents.some((e) => e.type === "enemyDamage" && e.targetId === "air-fixture"))).toBe(true);
  await page.screenshot({ path: "test-results/screenshots/horde-air-hit-1440x900.png" });
  await page.waitForTimeout(280);
  const center = await page.evaluate(() => window.__FPG_SANDBOX__.worldToScreen(0, 10, 1));
  await page.mouse.move(center.x, center.y); await page.mouse.down({ button: "right" });
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat!.chargeTicks)).toBeGreaterThanOrEqual(75);
  await page.mouse.up({ button: "right" });
  await page.screenshot({ path: "test-results/screenshots/horde-multikill-burst-1440x900.png" });
  const before = await page.evaluate(() => {
    const s = window.__FPG_SANDBOX__.getSnapshot().state;
    return { aura: s.resources.aura, orbs: s.combat!.experienceOrbs.length, count: s.analytics.filter((e) => e.type === "secondaryReleased").at(-1)?.data.targets };
  });
  expect(before.count).toBeGreaterThanOrEqual(4); expect(before.orbs).toBeGreaterThan(0);
  await page.keyboard.press("Escape");
  const paused = await page.evaluate(() => JSON.stringify(window.__FPG_SANDBOX__.getSnapshot().state.combat));
  await page.waitForTimeout(350);
  expect(await page.evaluate(() => JSON.stringify(window.__FPG_SANDBOX__.getSnapshot().state.combat))).toBe(paused);
  await page.keyboard.press("Escape"); await page.waitForTimeout(540);
  await page.keyboard.press("d");
  await page.screenshot({ path: "test-results/screenshots/horde-experience-trails-1440x900.png" });
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.resources.aura)).toBeGreaterThan(before.aura);
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat!.rewardReady)).toBe(true);
  expect(errors).toEqual([]);
});

test("renders the capacity scene at desktop sizes, keeps resources bounded and preserves mobile messaging", async ({ page }, testInfo) => {
  test.setTimeout(60000);
  await page.setViewportSize({ width: 1440, height: 900 }); await battle(page);
  await page.evaluate(() => {
    const s = window.__FPG_SANDBOX__.getSnapshot(), c = s.state.combat!;
    Object.assign(s.build, { damageReduction: 1 });
    c.tick = 3600; c.horde.nextSpawnTick = 99999;
    c.enemies = Array.from({ length: 48 }, (_, i) => ({ id: `stress-${i}`, type: i < 14 ? "flyer" : i < 18 ? "ranged" : i < 20 ? "summoner" : "melee", layer: i < 14 ? "air" : "ground", position: { x: (i % 8) * 2.4 - 8.4, y: i < 14 ? 3.5 + i % 3 : 0, z: 8 + Math.floor(i / 8) * 1.65 }, hp: 100, maxHp: 100, shield: 0, attackCooldown: 99999, spawnTick: 0 }));
    c.projectiles = Array.from({ length: 128 }, (_, i) => ({ id: `stress-p-${i}`, position: { x: (i % 16) * 1.1 - 8, y: 2 + i % 3, z: 6 + Math.floor(i / 16) }, velocity: { x: 0, y: 0, z: 0.0001 }, damage: 1, hostile: true, lifeTicks: 99999 }));
    c.experienceOrbs = Array.from({ length: 128 }, (_, i) => ({ id: `stress-xp-${i}`, position: { x: i % 16 - 8, y: 2, z: 12 }, origin: { x: i % 16 - 8, y: 2, z: 12 }, burst: { x: i % 16 - 8, y: 3 + i % 3, z: 8 + Math.floor(i / 16) }, value: 1, age: 25, duration: 99999 }));
  });
  await page.waitForTimeout(5500);
  const performance = await page.evaluate(() => ({ ...window.__FPG_SANDBOX__.diagnostics(), userAgent: navigator.userAgent, cores: navigator.hardwareConcurrency }));
  await testInfo.attach("capacity-performance.json", { body: JSON.stringify(performance, null, 2), contentType: "application/json" });
  console.log("HORDE_PERFORMANCE", JSON.stringify(performance));
  expect(performance.calls).toBeLessThan(120); expect(performance.geometries).toBeLessThan(80);
  for (const [width, height] of [[1280, 720], [1440, 900], [1920, 1080]]) {
    await page.setViewportSize({ width, height }); await page.waitForTimeout(100);
    const framing = await page.evaluate(() => ({
      player: window.__FPG_SANDBOX__.worldToScreen(0, 1.1, 1),
      ground: window.__FPG_SANDBOX__.worldToScreen(0, 10, 1),
      air: window.__FPG_SANDBOX__.worldToScreen(0, 12, 5),
    }));
    expect(framing.player.y / height!).toBeGreaterThan(0.73); expect(framing.player.y / height!).toBeLessThan(0.86);
    expect(framing.ground.y / height!).toBeGreaterThan(0.5); expect(framing.ground.y / height!).toBeLessThan(0.7);
    expect(framing.air.y / height!).toBeGreaterThan(0.25); expect(framing.air.y / height!).toBeLessThan(0.5);
    await page.screenshot({ path: `test-results/screenshots/horde-capacity-${width}x${height}.png` });
  }
  const memoryBefore = await page.evaluate(() => window.__FPG_SANDBOX__.diagnostics());
  await page.evaluate(() => window.__FPG_SANDBOX__.controller.restartSameSeed());
  await page.getByTestId("offer-blessing").first().click(); await page.getByTestId("map-node-n1a").click();
  await page.waitForTimeout(250);
  const memoryAfter = await page.evaluate(() => window.__FPG_SANDBOX__.diagnostics());
  expect(memoryAfter.geometries).toBeLessThanOrEqual(memoryBefore.geometries);
  expect(memoryAfter.textures).toBeLessThanOrEqual(memoryBefore.textures);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator(".mobile-guard")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/horde-mobile-390x844.png" });
});

test("a full backpack can decline an upgrade and continue combat", async ({ page }) => {
  await battle(page);
  await page.evaluate(() => {
    const api = window.__FPG_SANDBOX__, s = api.getSnapshot().state;
    Object.assign(s, { backpackCapacity: 0 }); s.resources.aura = 230;
    api.controller.dispatchAction({ type: "gather" }); api.controller.completeRitual();
  });
  await expect(page.getByTestId("reward-screen")).toBeVisible();
  await expect(page.locator(".offer-card").first()).toBeDisabled();
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "放弃本次奖励" }).click();
  await expect(page.getByTestId("combat-hud")).toBeVisible();
  await expect(page.getByTestId("experience-bar")).toContainText("×1");
  expect(await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.resources.aura)).toBe(130);
});
