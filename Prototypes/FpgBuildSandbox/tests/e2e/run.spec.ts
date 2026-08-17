import { expect, type Page, test } from "@playwright/test";

async function startRun(page: Page, seed: string): Promise<void> {
  await page.goto("/?e2e=1");
  await expect(page.getByTestId("title-screen")).toBeVisible();
  await page.locator("#seed-input").fill(seed);
  await page.getByRole("button", { name: "开始新局" }).click();
  await expect(page.getByTestId("reward-screen")).toBeVisible();
  await expect(page.locator("[data-testid='offer-blessing'] svg.lucide-crown").first()).toBeVisible();
  const blessingOffer = page.locator("[data-testid='offer-blessing']").first();
  await expect(blessingOffer.locator(".generated-description .mechanic-term").first()).toBeVisible();
  await expect(blessingOffer.locator(".offer-effects")).toHaveCount(0);
  await blessingOffer.click();
  await expect(page.getByTestId("map-screen")).toBeVisible();
}

async function selectAvailableRoute(page: Page, preferredId?: string): Promise<void> {
  const preferred = preferredId ? page.getByTestId(`map-node-${preferredId}`) : undefined;
  if (preferred && await preferred.isEnabled()) await preferred.click();
  else await page.locator(".map-node.available").first().click();
}

async function chooseCurrentReward(page: Page, expectedAfter: "map" | "combat"): Promise<void> {
  await expect(page.getByTestId("reward-screen")).toBeVisible();
  await page.locator(".offer-card:not([disabled])").first().click();
  const targetScreen = page.getByTestId("enchantment-target-screen");
  if (await targetScreen.isVisible()) await targetScreen.locator("[data-action^='attach:']").first().click();
  await expect(page.getByTestId(expectedAfter === "map" ? "map-screen" : "combat-hud")).toBeVisible();
}

async function clearRoomAndClaim(page: Page): Promise<void> {
  await page.evaluate(() => window.__FPG_SANDBOX__.clearCombat());
  await expect(page.getByRole("button", { name: /回收房间遗物/ })).toBeVisible();
  await page.getByRole("button", { name: /回收房间遗物/ }).click();
}

async function completeRitual(page: Page): Promise<void> {
  const canvas = page.getByTestId("ritual-canvas");
  const box = await canvas.boundingBox();
  if (!box) throw new Error("Ritual canvas has no bounds");
  const points = [
    [0.5, 0.14],
    [0.18, 0.78],
    [0.85, 0.34],
    [0.15, 0.34],
    [0.82, 0.78],
  ] as const;
  await page.mouse.move(box.x + box.width * points[0][0], box.y + box.height * points[0][1]);
  await page.mouse.down();
  for (const [x, y] of points.slice(1)) await page.mouse.move(box.x + box.width * x, box.y + box.height * y, { steps: 14 });
  await page.mouse.up();
  await expect(page.getByTestId("reward-screen")).toBeVisible();
}

async function readWebGlHash(page: Page): Promise<{ hash: number; nonZero: number }> {
  return page.evaluate(async () => {
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    const canvas = document.querySelector<HTMLCanvasElement>("canvas.game-canvas");
    if (!canvas) throw new Error("WebGL canvas missing");
    const gl = canvas.getContext("webgl2") ?? canvas.getContext("webgl");
    if (!gl) throw new Error("WebGL context missing");
    const pixels = new Uint8Array(canvas.width * canvas.height * 4);
    gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    let hash = 2166136261;
    let nonZero = 0;
    for (let index = 0; index < pixels.length; index += 401) {
      const value = pixels[index] ?? 0;
      if (value > 0) nonZero += 1;
      hash = Math.imul(hash ^ value, 16777619) >>> 0;
    }
    return { hash, nonZero };
  });
}

test("plays opening, ritual, shop, elite and boss through a complete floor", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await startRun(page, "e2e-complete-floor");
  await expect(page.locator(".map-node .concept-label svg").first()).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/map-1440x900.png" });
  await selectAvailableRoute(page, "n1a");
  await expect(page.getByTestId("combat-hud")).toBeVisible();
  await expect(page.getByText(/掩体 2/)).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/combat-1440x900.png" });

  await page.evaluate(() => window.__FPG_SANDBOX__.fillAura());
  await expect(page.getByTestId("experience-bar")).toHaveClass(/ready/);
  await expect(page.getByTestId("experience-bar")).toContainText("G 聚气");
  await page.getByRole("button", { name: /经验已满/ }).click();
  await expect(page.getByTestId("ritual-screen")).toBeVisible();
  await completeRitual(page);
  await page.getByRole("button", { name: /重投/ }).click();
  await expect(page.locator(".offer-card .concept-seal svg").first()).toBeVisible();
  const itemOffer = page.getByTestId("offer-item").first();
  await expect(itemOffer).toBeVisible();
  await expect(itemOffer.locator(".rarity-chip")).toBeVisible();
  await expect(itemOffer.locator(".item-stars .filled")).toHaveCount(1);
  await expect(itemOffer.locator(".item-description .mechanic-term")).toHaveCount(2);
  await expect(itemOffer.locator(".offer-effects")).toHaveCount(0);
  await expect(page.locator(".offer-card.kind-item .item-description").filter({ hasText: "每个掩体的" }).first()).toContainText("上限");
  const factionLabel = itemOffer.locator(".tag-columns .faction-mark").first();
  await factionLabel.hover();
  const factionTooltip = factionLabel.locator(".faction-tooltip");
  await expect(factionTooltip).toBeVisible();
  await expect(factionTooltip).toContainText("2件");
  await expect(factionTooltip.locator(".tooltip-effect-row")).toHaveCount(2);
  expect((await factionTooltip.boundingBox())?.width ?? 0).toBeGreaterThan(300);
  await page.screenshot({ path: "test-results/screenshots/reward-faction-tooltip-1440x900.png" });
  const itemTypeLabel = itemOffer.locator(".tag-columns .tag-mark").first();
  await itemTypeLabel.hover();
  await expect(itemTypeLabel.locator(".item-type-tooltip")).toBeVisible();
  await expect(itemTypeLabel.locator(".item-type-tooltip")).toContainText("物品类型");
  await expect(itemTypeLabel.locator(".item-type-tooltip small")).not.toBeEmpty();
  const mechanic = itemOffer.locator(".item-description .mechanic-term").first();
  await mechanic.hover();
  await expect(mechanic.locator(".mechanic-tooltip")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/reward-1440x900.png" });
  const itemCountBeforeDrop = await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.items.length);
  await itemOffer.dragTo(page.getByTestId("reward-backpack"));
  await expect(page.getByTestId("combat-hud")).toBeVisible();
  expect(await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.items.length)).toBe(itemCountBeforeDrop + 1);

  await page.getByRole("button", { name: "查看构筑" }).click();
  await expect(page.getByTestId("build-screen")).toBeVisible();
  await expect(page.getByTestId("character-panel")).toBeVisible();
  await expect(page.locator(".character-stat")).toHaveCount(14);
  await expect(page.locator(".special-stat")).toHaveCount(6);
  await expect(page.getByTestId("backpack-grid").first().locator(".backpack-slot")).toHaveCount(12);
  await expect(page.getByTestId("backpack-grid").first().locator(".backpack-slot.occupied .item-stars .filled")).toHaveCount(1);
  await expect(page.getByTestId("backpack-grid").first().locator(".backpack-slot.occupied .slot-rarity")).toBeVisible();
  await expect(page.getByTestId("build-screen")).not.toContainText("空蕴位");
  await expect(page.locator(".faction-synergy-group > h3 svg.lucide-crown")).toBeVisible();
  await expect(page.locator(".tag-synergy-group")).toHaveCount(2);
  await expect(page.locator(".tag-synergy-group").first().locator(".tag-synergy-row")).toHaveCount(4);
  await expect(page.locator(".faction-synergy-group")).toContainText("流派羁绊");
  await page.locator(".faction-synergy-row .faction-mark").first().hover();
  await expect(page.locator(".faction-synergy-row .faction-mark").first().locator(".faction-tooltip")).toBeVisible();
  await expect(page.locator(".faction-synergy-row .faction-mark").first().locator(".faction-tooltip")).toContainText("2件");
  await expect(page.locator(".backpack-slot.occupied .slot-tags .tag-mark").first()).toBeVisible();
  await expect(page.locator(".blessing-list .generated-description .mechanic-term").first()).toBeVisible();
  const lifeStat = page.locator(".character-stat").filter({ hasText: "生命上限" });
  await lifeStat.hover();
  await expect(lifeStat.locator(".mechanic-tooltip")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/build-1440x900.png" });
  const clippedBuildText = await page.locator(".character-stat > span:not(.mechanic-tooltip), .character-stat > b, .synergy-row > p").evaluateAll((elements) => elements.some((element) => element.scrollWidth > element.clientWidth + 2));
  expect(clippedBuildText).toBe(false);
  await page.getByRole("button", { name: "关闭" }).click();
  await expect(page.getByTestId("combat-hud")).toBeVisible();

  const firstFrame = await readWebGlHash(page);
  await page.waitForTimeout(240);
  const secondFrame = await readWebGlHash(page);
  expect(firstFrame.nonZero).toBeGreaterThan(100);
  expect(secondFrame.hash).not.toBe(firstFrame.hash);

  await clearRoomAndClaim(page);
  await chooseCurrentReward(page, "map");
  await selectAvailableRoute(page, "n2a");
  await clearRoomAndClaim(page);
  await chooseCurrentReward(page, "map");

  await selectAvailableRoute(page, "shop");
  await expect(page.getByTestId("shop-screen")).toBeVisible();
  await page.getByText("重投符", { exact: true }).click();
  await page.getByRole("button", { name: /继续前行/ }).click();
  await selectAvailableRoute(page, "elite");
  await clearRoomAndClaim(page);
  await chooseCurrentReward(page, "map");
  await selectAvailableRoute(page, "boss");
  await clearRoomAndClaim(page);
  await expect(page.getByTestId("result-screen")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/result-1440x900.png" });

  const seedBeforeRestart = await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.seed);
  await page.getByRole("button", { name: /同种子重开/ }).click();
  const seedAfterRestart = await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.seed);
  expect(seedAfterRestart).toBe(seedBeforeRestart);
  await expect(page.getByTestId("reward-screen")).toBeVisible();
});

test("assembles an enchantment inside the limited backpack", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await startRun(page, "e2e-backpack-enchantment");
  await selectAvailableRoute(page, "n1a");

  await page.evaluate(() => window.__FPG_SANDBOX__.fillAura());
  await page.keyboard.press("g");
  await expect(page.getByTestId("ritual-screen")).toBeVisible();
  await completeRitual(page);
  await page.getByTestId("offer-item").first().click();
  await expect(page.getByTestId("combat-hud")).toBeVisible();

  await page.evaluate(() => window.__FPG_SANDBOX__.fillAura());
  await page.keyboard.press("g");
  await completeRitual(page);
  await page.getByTestId("offer-enchantment").first().click();
  await expect(page.getByTestId("enchantment-target-screen")).toBeVisible();
  await expect(page.getByTestId("enchantment-target-screen")).not.toContainText("蕴位");
  await expect(page.locator(".pending-enchantment .generated-description .mechanic-term").first()).toBeVisible();
  await expect(page.locator(".pending-enchantment .offer-effects")).toHaveCount(0);
  await expect(page.getByTestId("backpack-grid").locator(".backpack-slot")).toHaveCount(12);
  const target = page.locator("[data-action^='attach:']").first();
  await expect(target).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/enchantment-backpack-1440x900.png" });
  await target.click();
  await expect(page.getByTestId("combat-hud")).toBeVisible();
  expect(await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.items.some((item) => Boolean(item.enchantmentId)))).toBe(true);
});

test("keeps the character panel usable at the minimum desktop viewport", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await startRun(page, "e2e-minimum-desktop");
  await selectAvailableRoute(page, "n1a");
  await page.getByRole("button", { name: "查看构筑" }).click();
  await expect(page.getByTestId("character-panel")).toBeVisible();
  await expect(page.getByTestId("backpack-grid")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/build-1280x720.png" });
  const boundsAreSafe = await page.evaluate(() => {
    const screen = document.querySelector<HTMLElement>("[data-testid='build-screen']");
    const experience = document.querySelector<HTMLElement>("[data-testid='experience-bar']");
    if (!screen || !experience) return false;
    const screenBounds = screen.getBoundingClientRect();
    const experienceBounds = experience.getBoundingClientRect();
    return screenBounds.left >= 0 && screenBounds.right <= innerWidth && screenBounds.bottom <= experienceBounds.top + 1;
  });
  expect(boundsAreSafe).toBe(true);
});

test("supports real shooting, cover movement, reloading and charged fire", async ({ page }) => {
  const runtimeErrors: string[] = [];
  page.on("pageerror", (error) => runtimeErrors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") runtimeErrors.push(message.text());
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await startRun(page, "e2e-real-combat-input");
  await selectAvailableRoute(page, "n1a");
  await expect(page.getByTestId("combat-hud")).toBeVisible();
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.enemies.length ?? 0)).toBeGreaterThan(0);
  const framing = await page.evaluate(() => {
    const combat = window.__FPG_SANDBOX__.getSnapshot().state.combat;
    const enemy = combat?.enemies.find((candidate) => candidate.hp > 0);
    if (!combat || !enemy) throw new Error("Combat framing targets unavailable");
    return {
      player: window.__FPG_SANDBOX__.worldToScreen(combat.playerPosition.x, combat.playerPosition.z, 1),
      enemy: window.__FPG_SANDBOX__.worldToScreen(enemy.position.x, enemy.position.z, 1.15),
    };
  });
  expect(framing.player.y).toBeGreaterThan(540);
  expect(framing.player.y).toBeLessThan(760);
  expect(framing.enemy.y).toBeGreaterThan(400);
  expect(framing.enemy.y).toBeLessThan(650);

  const incomingProjectile = await page.evaluate(() => {
    const snapshot = window.__FPG_SANDBOX__.getSnapshot();
    const combat = snapshot.state.combat;
    if (!combat) throw new Error("Combat missing for cover collision test");
    combat.projectiles = [];
    for (const enemy of combat.enemies) enemy.attackCooldown = 999;
    const coverIndex = combat.playerCoverIndex;
    const coverX = [-7.5, 0, 7.5][coverIndex] ?? 0;
    combat.projectiles.push({ id: "e2e-incoming-cover", position: { x: coverX, z: 4 }, velocity: { x: 0, z: -0.04 }, damage: 20, hostile: true, lifeTicks: 100 });
    return { coverIndex, coverHealth: combat.coverHealth[coverIndex] ?? 0, life: snapshot.state.resources.life };
  });
  await page.waitForTimeout(180);
  await page.screenshot({ path: "test-results/screenshots/incoming-cover-projectile-1440x900.png" });
  await expect.poll(() => page.evaluate((coverIndex) => window.__FPG_SANDBOX__.getSnapshot().state.combat?.coverHealth[coverIndex] ?? 0, incomingProjectile.coverIndex)).toBeLessThan(incomingProjectile.coverHealth);
  expect(await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.resources.life)).toBe(incomingProjectile.life);

  const target = await page.evaluate(() => {
    const combat = window.__FPG_SANDBOX__.getSnapshot().state.combat;
    const enemy = combat?.enemies.find((candidate) => candidate.hp > 0);
    if (!combat || !enemy) throw new Error("No live enemy available for input test");
    return {
      id: enemy.id,
      hp: enemy.hp,
      ammo: combat.ammo,
      screen: window.__FPG_SANDBOX__.worldToScreen(enemy.position.x, enemy.position.z, enemy.type === "boss" ? 1.9 : 1.15),
    };
  });
  const primaryEventsBefore = await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.feedbackEvents.filter((event) => event.type === "primary").length ?? 0);
  await page.mouse.move(target.screen.x, target.screen.y);
  await page.mouse.down({ button: "left" });
  await page.waitForTimeout(230);
  await page.screenshot({ path: "test-results/screenshots/continuous-fire-1440x900.png" });
  await page.waitForTimeout(290);
  await page.mouse.up({ button: "left" });
  await expect.poll(() => page.evaluate((id) => window.__FPG_SANDBOX__.getSnapshot().state.combat?.enemies.find((enemy) => enemy.id === id)?.hp ?? 0, target.id)).toBeLessThan(target.hp);
  const afterShot = await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat!);
  expect(afterShot.ammo).toBeLessThanOrEqual(target.ammo - 3);
  expect(afterShot.feedbackEvents.filter((event) => event.type === "primary")).toHaveLength(primaryEventsBefore + (target.ammo - afterShot.ammo));
  expect(afterShot.feedbackEvents.some((event) => event.type === "primary" && event.hit)).toBe(true);

  await page.evaluate(() => {
    const combat = window.__FPG_SANDBOX__.getSnapshot().state.combat;
    if (!combat) throw new Error("Combat missing for auto-reload test");
    combat.ammo = 0;
    combat.reloadTicks = 0;
    combat.fireCooldown = 0;
  });
  await page.mouse.click(target.screen.x, target.screen.y);
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.reloadTicks ?? 0)).toBeGreaterThan(0);
  await expect(page.getByTestId("weapon-state")).toHaveAttribute("data-weapon-state", "reload");
  await expect(page.getByTestId("reload-crosshair")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/auto-reload-crosshair-1440x900.png" });
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.reloadTicks ?? -1), { timeout: 3_000 }).toBe(0);
  expect(await page.evaluate(() => {
    const snapshot = window.__FPG_SANDBOX__.getSnapshot();
    return snapshot.state.combat?.ammo === snapshot.build.magazine;
  })).toBe(true);

  const playerScreenBeforeMove = await page.evaluate(() => {
    const position = window.__FPG_SANDBOX__.getSnapshot().state.combat?.playerPosition;
    if (!position) throw new Error("Player position unavailable");
    return window.__FPG_SANDBOX__.worldToScreen(position.x, position.z, 1);
  });
  await page.keyboard.press("d");
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.playerCoverIndex)).toBe(0);
  const playerAfterMove = await page.evaluate(() => {
    const position = window.__FPG_SANDBOX__.getSnapshot().state.combat?.playerPosition;
    if (!position) throw new Error("Player position unavailable");
    return { position, screen: window.__FPG_SANDBOX__.worldToScreen(position.x, position.z, 1) };
  });
  expect(playerAfterMove.position.x).toBe(-7.5);
  expect(playerAfterMove.screen.x).toBeGreaterThan(playerScreenBeforeMove.x);
  await expect(page.getByText(/掩体 1/)).toBeVisible();
  const selectedCoverHealth = await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.coverHealth[0] ?? 0);
  await expect(page.locator("#hud-cover")).toContainText(`${Math.ceil(selectedCoverHealth)} /`);

  const chargedTarget = await page.evaluate(() => {
    const enemy = window.__FPG_SANDBOX__.getSnapshot().state.combat?.enemies.find((candidate) => candidate.hp > 0);
    if (!enemy) throw new Error("No live enemy available for charged-fire test");
    return window.__FPG_SANDBOX__.worldToScreen(enemy.position.x, enemy.position.z, enemy.type === "boss" ? 1.9 : 1.15);
  });
  const energyBefore = await page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.secondaryEnergy ?? 0);
  await page.mouse.move(chargedTarget.x, chargedTarget.y);
  await page.mouse.down({ button: "right" });
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.chargeTicks ?? 0)).toBeGreaterThan(10);
  await expect(page.getByTestId("weapon-state")).toHaveAttribute("data-weapon-state", "charge");
  await page.screenshot({ path: "test-results/screenshots/real-combat-feedback-1440x900.png" });
  await page.mouse.up({ button: "right" });
  await page.waitForTimeout(50);
  await page.screenshot({ path: "test-results/screenshots/secondary-release-1440x900.png" });
  await expect.poll(() => page.evaluate(() => window.__FPG_SANDBOX__.getSnapshot().state.combat?.feedbackEvents.some((event) => event.type === "secondary") ?? false)).toBe(true);
  const energyResult = await page.evaluate(() => {
    const snapshot = window.__FPG_SANDBOX__.getSnapshot();
    const event = snapshot.state.analytics.filter((entry) => entry.type === "secondaryReleased").at(-1);
    return {
      energy: snapshot.state.combat?.secondaryEnergy ?? 0,
      cost: snapshot.build.secondaryEnergyCost,
      recordedCost: Number(event?.data.energySpent ?? 0),
    };
  });
  expect(energyResult.recordedCost).toBe(energyResult.cost);
  expect(energyResult.energy).toBeLessThan(energyBefore);
  const displayedEnergy = Number((await page.locator("#hud-energy").textContent())?.split("/")[0]?.trim() ?? energyBefore);
  expect(displayedEnergy).toBeLessThan(100);
  expect(runtimeErrors).toEqual([]);
});

for (const stationType of ["merge", "recast"] as const) {
  test(`uses the ${stationType} function station`, async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await startRun(page, `e2e-${stationType}`);
    await selectAvailableRoute(page, "n1a");
    await clearRoomAndClaim(page);
    await chooseCurrentReward(page, "map");
    await selectAvailableRoute(page, "n2a");
    await clearRoomAndClaim(page);
    await chooseCurrentReward(page, "map");
    const stationId = await page.evaluate((type) => {
      const state = window.__FPG_SANDBOX__.getSnapshot().state;
      return state.floor.nodes.find((node) => node.type === type && node.status === "available")?.id;
    }, stationType);
    expect(stationId).toBeTruthy();
    await selectAvailableRoute(page, stationId);
    await expect(page.getByTestId("function-screen")).toBeVisible();
    await page.locator(`[data-action^='${stationType}:']`).first().click();
    await expect(page.getByTestId("map-screen")).toBeVisible();
  });
}

test("shows defeat recovery and desktop/mobile visual states", async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto("/");
  await expect(page.getByTestId("title-screen")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/title-1920x1080.png" });
  const titleCanvas = await readWebGlHash(page);
  expect(titleCanvas.nonZero).toBeGreaterThan(100);

  await startRun(page, "e2e-defeat");
  await selectAvailableRoute(page, "n1a");
  await page.evaluate(() => window.__FPG_SANDBOX__.defeatCombat());
  await expect(page.getByTestId("result-screen")).toBeVisible();
  await expect(page.getByText("灵光暂熄")).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.getByText("请使用桌面浏览器")).toBeVisible();
  await page.screenshot({ path: "test-results/screenshots/mobile-390x844.png" });
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth || document.documentElement.scrollHeight > window.innerHeight);
  expect(overflow).toBe(false);
});
