import { describe, expect, it } from "vitest";
import { resolveBuild } from "./buildResolver";
import { createCombat, moveCover, tickCombat } from "./combat";
import { GameController } from "./GameController";
import { attachEnchantment, canGrantItem, grantItem } from "./inventory";
import { SeededRng } from "./rng";
import { migrateSave } from "./save";
import type { EnemyState, OwnedItem, RunState } from "./types";

function beginCombat(seed = "test-seed"): GameController {
  const controller = new GameController();
  controller.startNewRun(seed);
  const opening = controller.getSnapshot().state.pendingReward?.offers[0];
  if (!opening) throw new Error("Opening reward missing");
  controller.selectReward(opening.id);
  const route = controller.getSnapshot().state.floor.nodes.find((node) => node.status === "available");
  if (!route) throw new Error("Opening route missing");
  controller.interactWithNode(route.id);
  return controller;
}

describe("deterministic run core", () => {
  it("stops fixed simulation ticks while paused", () => {
    const controller = beginCombat();
    for (let index = 0; index < 45; index += 1) controller.tick();
    const runningTick = controller.getSnapshot().state.combat?.tick ?? 0;
    expect(runningTick).toBe(45);
    controller.pause();
    for (let index = 0; index < 120; index += 1) controller.tick();
    expect(controller.getSnapshot().state.combat?.tick).toBe(runningTick);
    controller.resume();
    controller.tick();
    expect(controller.getSnapshot().state.combat?.tick).toBe(runningTick + 1);
  });

  it("reproduces floor and opening offers from the same seed", () => {
    const first = new GameController();
    const second = new GameController();
    first.startNewRun("jianmu-repeatable");
    second.startNewRun("jianmu-repeatable");
    expect(first.getSnapshot().state.floor).toEqual(second.getSnapshot().state.floor);
    expect(first.getSnapshot().state.pendingReward?.offers.map((offer) => offer.definitionId))
      .toEqual(second.getSnapshot().state.pendingReward?.offers.map((offer) => offer.definitionId));
  });

  it("isolates rerolls from all other random streams", () => {
    const first = new GameController();
    const second = new GameController();
    first.startNewRun("isolated-reroll");
    second.startNewRun("isolated-reroll");
    expect(first.rerollReward()).toBe(true);
    const firstRng = first.getSnapshot().state.rng;
    const secondRng = second.getSnapshot().state.rng;
    expect(firstRng.reroll).not.toBe(secondRng.reroll);
    expect(firstRng.map).toBe(secondRng.map);
    expect(firstRng.encounter).toBe(secondRng.encounter);
    expect(firstRng.reward).toBe(secondRng.reward);
    expect(firstRng.boss).toBe(secondRng.boss);
  });
});

describe("build rules", () => {
  it("merges three same-name same-rank items into one higher rank", () => {
    const controller = new GameController();
    controller.startNewRun("merge-test");
    const state = controller.getSnapshot().state as RunState;
    grantItem(state, "ember_repeat_bow");
    grantItem(state, "ember_repeat_bow");
    grantItem(state, "ember_repeat_bow");
    expect(state.items.filter((item) => item.definitionId === "ember_repeat_bow" && item.level === 1)).toHaveLength(0);
    expect(state.items.filter((item) => item.definitionId === "ember_repeat_bow" && item.level === 2)).toHaveLength(1);
  });

  it("counts a faction label once per unique item name regardless of copies or rank", () => {
    const items: OwnedItem[] = [
      { instanceId: "a", definitionId: "ember_repeat_bow", level: 1 },
      { instanceId: "b", definitionId: "ember_repeat_bow", level: 1 },
      { instanceId: "c", definitionId: "ember_repeat_bow", level: 3 },
      { instanceId: "d", definitionId: "ember_cinder_case", level: 1 },
    ];
    const build = resolveBuild(items, []);
    expect(build.factionCounts.ember).toBe(2);
    expect(build.activeFactionSynergies).toContain("铸火成礼·火续");
    expect(build.activeFactionSynergies).not.toContain("铸火成礼·炉心");
  });

  it("activates both 2 and 4 item synergy thresholds", () => {
    const definitionIds = ["ember_repeat_bow", "ember_cinder_case", "ember_last_spark", "ember_forge_heart"];
    const items = definitionIds.map((definitionId, index) => ({ instanceId: String(index), definitionId, level: 1 as const }));
    const build = resolveBuild(items, []);
    expect(build.factionCounts.ember).toBe(4);
    expect(build.activeFactionSynergies).toEqual(expect.arrayContaining(["铸火成礼·火续", "铸火成礼·炉心"]));
  });

  it("counts item types and unified faction tags once per unique item and applies simple bonds", () => {
    const items: OwnedItem[] = [
      { instanceId: "a", definitionId: "ember_repeat_bow", level: 1 },
      { instanceId: "b", definitionId: "ember_repeat_bow", level: 3 },
      { instanceId: "c", definitionId: "veil_hunter_pin", level: 1 },
    ];
    const build = resolveBuild(items, []);
    expect(build.itemTypeCounts.weapon).toBe(2);
    expect(build.factionCounts.solar).toBe(1);
    expect(build.activeItemTypeSynergies).toContain("武器共鸣·初成");
    expect(build.primaryDamage).toBe(24);

    const earthItems = ["veil_hidden_scale", "ward_jade_plate", "ward_root_guard", "ward_shatter_seal"]
      .map((definitionId, index) => ({ instanceId: `earth-${index}`, definitionId, level: 1 as const }));
    const earthBuild = resolveBuild(earthItems, []);
    expect(earthBuild.factionCounts.earth).toBe(4);
    expect(earthBuild.activeFactionSynergies).toEqual(expect.arrayContaining(["厚土神流·初成", "厚土神流·盛成"]));
  });

  it("requires confirmation before replacing the single enchantment slot", () => {
    const controller = new GameController();
    controller.startNewRun("enchantment-test");
    const state = controller.getSnapshot().state as RunState;
    const item = grantItem(state, "ember_repeat_bow");
    expect(attachEnchantment(state, item.instanceId, "enchant_quick")).toBe("attached");
    expect(attachEnchantment(state, item.instanceId, "enchant_spark")).toBe("confirm");
    expect(item.enchantmentId).toBe("enchant_quick");
    expect(attachEnchantment(state, item.instanceId, "enchant_spark", true)).toBe("attached");
    expect(item.enchantmentId).toBe("enchant_spark");
  });

  it("applies every test blessing to resolved combat attributes", () => {
    const base = resolveBuild([], []);
    const houyi = resolveBuild([], ["blessing_houyi"]);
    expect(houyi.primaryDamage).toBeCloseTo(base.primaryDamage * 1.18);
    expect(houyi.weakpointMultiplier).toBeCloseTo(base.weakpointMultiplier + 0.2);

    const nuwa = resolveBuild([], ["blessing_nuwa"]);
    expect(nuwa.lifeMax).toBe(base.lifeMax + 20);
    expect(nuwa.coverMax).toBe(base.coverMax + 20);

    const leigong = resolveBuild([], ["blessing_leigong"]);
    expect(leigong.secondaryDamage).toBeCloseTo(base.secondaryDamage * 1.22);
    expect(leigong.fireCooldownTicks).toBeLessThan(base.fireCooldownTicks);

    const xihe = resolveBuild([], ["blessing_xihe"]);
    expect(xihe.magazine).toBe(base.magazine + 3);
    expect(xihe.auraGain).toBeCloseTo(base.auraGain * 1.18);

    const xuanwu = resolveBuild([], ["blessing_xuanwu"]);
    expect(xuanwu.damageReduction).toBeCloseTo(base.damageReduction + 0.1);
    expect(xuanwu.coverReduction).toBeCloseTo(base.coverReduction + 0.1);

    const fengbo = resolveBuild([], ["blessing_fengbo"]);
    expect(fengbo.reloadTicks).toBeLessThan(base.reloadTicks);
    expect(fengbo.eventAmmo.leaveCover).toBe(1);
  });

  it("enforces backpack capacity but permits an incoming three-copy merge", () => {
    const controller = new GameController();
    controller.startNewRun("backpack-capacity-test");
    const state = controller.getSnapshot().state as RunState;
    state.backpackCapacity = 2;
    grantItem(state, "ember_repeat_bow");
    grantItem(state, "ember_cinder_case");
    expect(canGrantItem(state, "veil_sight_leaf")).toBe(false);
    expect(() => grantItem(state, "veil_sight_leaf")).toThrow("Backpack is full");

    state.items = [];
    grantItem(state, "ember_repeat_bow");
    grantItem(state, "ember_repeat_bow");
    expect(canGrantItem(state, "ember_repeat_bow")).toBe(true);
    grantItem(state, "ember_repeat_bow");
    expect(state.items).toEqual([expect.objectContaining({ definitionId: "ember_repeat_bow", level: 2 })]);
  });
});

describe("combat and persistence", () => {
  it("queues seven enemies for each of three normal-room waves", () => {
    const combat = createCombat("combat", new SeededRng(77), resolveBuild([], []));
    expect(combat.totalWaves).toBe(3);
    expect(combat.spawnQueue).toHaveLength(21);
    for (const delayTick of [0, 240, 480]) {
      expect(combat.spawnQueue.filter((enemy) => enemy.delayTick === delayTick)).toHaveLength(7);
    }
  });

  it("stores damage on the occupied cover and preserves it after moving", () => {
    const controller = new GameController();
    controller.startNewRun("cover-health-test");
    const state = controller.getSnapshot().state as RunState;
    const build = resolveBuild([], []);
    const rng = new SeededRng(113);
    state.mode = "combat";
    state.combat = createCombat("combat", rng, build);
    state.combat.projectiles.push({ id: "cover-hit-1", position: { x: 0, z: 3.2 }, velocity: { x: 0, z: -0.8 }, damage: 40, hostile: true, lifeTicks: 2 });
    tickCombat(state, build, rng);
    const middleCoverHealth = state.combat.coverHealth[1];
    expect(middleCoverHealth).toBeCloseTo(74);
    expect(state.resources.life).toBe(100);

    moveCover(state, build, 1);
    state.combat.projectiles.push({ id: "cover-hit-2", position: { x: 7.5, z: 3.2 }, velocity: { x: 0, z: -0.8 }, damage: 40, hostile: true, lifeTicks: 2 });
    tickCombat(state, build, rng);
    expect(state.combat.coverHealth[1]).toBe(middleCoverHealth);
    expect(state.combat.coverHealth[2]).toBeCloseTo(74);
    expect(state.combat.coverHealth[0]).toBe(100);
  });

  it("stops hostile projectiles at the physical cover before they reach the player", () => {
    const controller = new GameController();
    controller.startNewRun("projectile-cover-test");
    const state = controller.getSnapshot().state as RunState;
    const build = resolveBuild([], []);
    const rng = new SeededRng(321);
    state.mode = "combat";
    state.combat = createCombat("combat", rng, build);
    state.combat.spawnQueue = [];
    state.combat.projectiles.push({ id: "incoming", position: { x: 0, z: 3.4 }, velocity: { x: 0, z: -1 }, damage: 20, hostile: true, lifeTicks: 5 });

    tickCombat(state, build, rng);

    expect(state.combat.projectiles).toHaveLength(0);
    expect(state.combat.coverHealth[1]).toBeCloseTo(87);
    expect(state.resources.life).toBe(100);
    expect(state.combat.feedbackEvents.some((event) => event.type === "coverHit")).toBe(true);
  });

  it("spends secondary energy and only auto-reloads on a new empty-magazine attack", () => {
    const controller = beginCombat("weapon-resource-test");
    const snapshot = controller.getSnapshot();
    const combat = snapshot.state.combat as RunState["combat"];
    if (!combat) throw new Error("Combat missing");
    const energyBefore = combat.secondaryEnergy;
    controller.dispatchAction({ type: "secondaryStart" });
    for (let index = 0; index < 20; index += 1) controller.tick();
    controller.dispatchAction({ type: "secondaryRelease" });
    expect(combat.secondaryEnergy).toBeCloseTo(energyBefore - snapshot.build.secondaryEnergyCost);

    combat.ammo = 0;
    combat.reloadTicks = 0;
    controller.dispatchAction({ type: "primary" });
    expect(combat.reloadTicks).toBe(0);
    controller.dispatchAction({ type: "primary", autoReload: true });
    expect(combat.reloadTicks).toBe(snapshot.build.reloadTicks);
    expect(combat.feedbackEvents.some((event) => event.type === "reloadStart")).toBe(true);
  });

  it("emits positioned damage-number feedback for primary and secondary hits", () => {
    const controller = beginCombat("damage-number-feedback-test");
    const snapshot = controller.getSnapshot();
    const state = snapshot.state as RunState;
    const combat = state.combat;
    if (!combat) throw new Error("Combat missing");
    combat.spawnQueue = [];
    combat.enemies = [
      { id: "damage-target-a", type: "ranged", position: { x: 0, z: 10 }, hp: 200, maxHp: 200, shield: 0, attackCooldown: 999, spawnTick: 0 },
      { id: "damage-target-b", type: "melee", position: { x: 1.2, z: 10 }, hp: 200, maxHp: 200, shield: 0, attackCooldown: 999, spawnTick: 0 },
    ];
    combat.aim = { x: 0, z: 10 };

    controller.dispatchAction({ type: "primary" });
    const primaryNumber = combat.feedbackEvents.find((event) => event.type === "enemyDamage");
    expect(primaryNumber).toMatchObject({
      to: { x: 0, z: 10 },
      value: Math.round(snapshot.build.primaryDamage * snapshot.build.weakpointMultiplier),
      weakpoint: true,
    });

    combat.isCharging = true;
    combat.chargeTicks = 75;
    combat.fireCooldown = 0;
    const damageNumbersBefore = combat.feedbackEvents.filter((event) => event.type === "enemyDamage").length;
    controller.dispatchAction({ type: "secondaryRelease" });
    const secondaryNumbers = combat.feedbackEvents.filter((event) => event.type === "enemyDamage").slice(damageNumbersBefore);
    expect(secondaryNumbers).toHaveLength(2);
    expect(secondaryNumbers.map((event) => event.to)).toEqual(expect.arrayContaining([{ x: 0, z: 10 }, { x: 1.2, z: 10 }]));
    expect(secondaryNumbers.every((event) => event.value === Math.round(snapshot.build.secondaryDamage * 1.2))).toBe(true);
  });

  it("moves the temporary boss through 70% and 35% phase thresholds", () => {
    const controller = new GameController();
    controller.startNewRun("boss-phase-test");
    const state = controller.getSnapshot().state as RunState;
    const build = resolveBuild([], []);
    const rng = new SeededRng(991);
    state.mode = "combat";
    state.combat = createCombat("boss", rng, build);
    tickCombat(state, build, rng);
    const boss = state.combat.enemies.find((enemy) => enemy.type === "boss");
    if (!boss) throw new Error("Boss did not spawn");
    boss.hp = boss.maxHp * 0.69;
    tickCombat(state, build, rng);
    expect(boss.phase).toBe(2);
    expect(boss.shield).toBeGreaterThan(0);
    expect(state.combat.enemies.some((enemy) => enemy.type === "minion")).toBe(true);
    boss.hp = boss.maxHp * 0.34;
    boss.shield = 0;
    tickCombat(state, build, rng);
    expect(boss.phase).toBe(3);
    expect(state.combat.coverHealth.filter((health) => health === 0)).toHaveLength(1);
  });

  it("keeps the 24 entity and 32 projectile stress snapshot bounded", () => {
    const controller = new GameController();
    controller.startNewRun("stress-test");
    const state = controller.getSnapshot().state as RunState;
    const build = resolveBuild([], []);
    build.damageReduction = 1;
    const rng = new SeededRng(42);
    state.mode = "combat";
    state.combat = createCombat("combat", rng, build);
    state.combat.spawnQueue = [];
    const template: EnemyState = { id: "", type: "ranged", position: { x: 0, z: 12 }, hp: 100, maxHp: 100, shield: 0, attackCooldown: 999, spawnTick: 0 };
    state.combat.enemies = Array.from({ length: 24 }, (_, index) => ({ ...template, id: `stress-${index}`, position: { x: (index % 8) - 4, z: 9 + Math.floor(index / 8) } }));
    state.combat.projectiles = Array.from({ length: 32 }, (_, index) => ({ id: `p-${index}`, position: { x: 8, z: 18 }, velocity: { x: 0, z: 0 }, damage: 1, hostile: true, lifeTicks: 100 }));
    expect(() => tickCombat(state, build, rng)).not.toThrow();
    expect(state.combat.enemies.length).toBeLessThanOrEqual(24);
    expect(state.combat.projectiles.length).toBeLessThanOrEqual(32);
  });

  it("migrates a version-zero save with a stable item serial", () => {
    const controller = new GameController();
    controller.startNewRun("migration-test");
    const state = structuredClone(controller.getSnapshot().state) as RunState;
    state.schemaVersion = 0;
    state.items = [{ instanceId: "legacy", definitionId: "ward_jade_plate", level: 1 }];
    state.combat = createCombat("combat", new SeededRng(19), resolveBuild([], []));
    state.combat.playerCoverIndex = 2;
    delete (state.combat as { coverHealth?: [number, number, number] }).coverHealth;
    (state.resources as typeof state.resources & { barrier: number }).barrier = 63;
    delete (state as Partial<RunState>).nextItemSerial;
    const migrated = migrateSave(state);
    expect(migrated?.schemaVersion).toBe(4);
    expect(migrated?.backpackCapacity).toBe(12);
    expect(migrated?.nextItemSerial).toBe(1);
    expect(migrated?.combat?.coverHealth).toEqual([100, 100, 63]);
    expect(migrated?.combat?.secondaryEnergy).toBe(100);
    expect("barrier" in (migrated?.resources ?? {})).toBe(false);
  });
});
