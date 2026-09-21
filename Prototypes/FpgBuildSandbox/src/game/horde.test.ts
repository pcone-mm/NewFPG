import { describe, expect, it } from "vitest";
import { GameController } from "./GameController";
import { resolveBuild } from "./buildResolver";
import { AIM_RAY_ORIGIN, createCombat, damageEnemy, dropExperience, firePrimary, moveCover, releaseSecondary, tickCombat } from "./combat";
import { SeededRng } from "./rng";
import { migrateSave } from "./save";
import type { EnemyState, RunState } from "./types";

function fixture(seed = 17, room: "combat" | "elite" | "boss" = "combat") {
  const controller = new GameController(); controller.startNewRun(String(seed));
  const state = controller.getSnapshot().state as RunState, build = resolveBuild([], []), rng = new SeededRng(seed);
  state.mode = "combat"; state.pendingReward = undefined; state.combat = createCombat(room, rng, build);
  return { controller, state, c: state.combat, build, rng };
}
function enemy(id: string, x = 0, y = 0, z = 10, hp = 100): EnemyState {
  return { id, type: y > 0 ? "flyer" : "melee", position: { x, y, z }, hp, maxHp: hp, shield: 0, attackCooldown: 9999, spawnTick: 0, layer: y > 0 ? "air" : "ground" };
}

describe("horde director and damage", () => {
  it("reproduces all three stages and cancels deferred arrivals at 90 seconds", () => {
    const a = fixture(), b = fixture(); a.build.damageReduction = b.build.damageReduction = 1;
    for (let tick = 0; tick < 5500; tick++) {
      tickCombat(a.state, a.build, a.rng); tickCombat(b.state, b.build, b.rng);
      expect(a.c.enemies.length).toBeLessThanOrEqual(48);
      expect(a.c.enemies.length).toBeLessThanOrEqual(tick < 1799 ? 24 : tick < 3599 ? 36 : 48);
      expect(a.c.enemies.filter((e) => e.type === "ranged").length).toBeLessThanOrEqual(4);
      expect(a.c.enemies.filter((e) => e.type === "summoner").length).toBeLessThanOrEqual(2);
      expect(a.c.enemies.filter((e) => e.behavior === "windup" && (e.type === "melee" || e.type === "minion")).length).toBeLessThanOrEqual(2);
      expect(a.c.enemies.filter((e) => e.behavior === "windup" && e.type !== "melee" && e.type !== "minion").length).toBeLessThanOrEqual(2);
    }
    expect(a.c).toEqual(b.c); expect(a.c.horde.stopped).toBe(true); expect(a.c.horde.pending).toBe(0);
    const serial = a.c.nextEntitySerial;
    a.c.enemies = [];
    tickCombat(a.state, a.build, a.rng);
    expect(a.c.nextEntitySerial).toBe(serial); expect(a.c.rewardReady).toBe(true);
  });
  it("hits ground and air separately, in near-to-far penetration order", () => {
    const f = fixture(); f.c.enemies = [enemy("far", 0, 0, 15), enemy("air", 0, 5, 8), enemy("near", 0, 0, 8), enemy("extra", 0, 0, 18)];
    f.c.aim = { x: 0, y: 1, z: 8 }; firePrimary(f.state, f.build, f.rng);
    const events = f.c.feedbackEvents.filter((e) => e.type === "enemyDamage");
    expect(events.map((e) => e.targetId)).toEqual(["near", "far"]);
    expect(f.c.enemies[1]!.hp).toBe(100); expect(f.c.enemies[3]!.hp).toBe(100);
    // The downward ray exits the near weakpoint and hits the far target's body.
    expect(100 - f.c.enemies[0]!.hp).toBeCloseTo(12 * 0.7);
    f.c.fireCooldown = 0; f.c.aim = { x: 0, y: 6, z: 8 }; firePrimary(f.state, f.build, f.rng);
    expect(f.c.enemies[1]!.hp).toBe(82);
  });
  it("keeps camera cursor rays aligned with close ground targets", () => {
    const f = fixture();
    f.c.enemies = [enemy("close", 0, 0, 8)];
    f.c.aimOrigin = { ...AIM_RAY_ORIGIN };
    // Camera ray through (0, 1, 8), continued to the shared z=12 aim plane.
    f.c.aim = { x: 0, y: -0.384615, z: 12 };
    firePrimary(f.state, f.build, f.rng);
    expect(f.c.enemies[0]!.hp).toBeLessThan(100);
  });
  it("uses a sphere for charged explosions, rather than an infinite vertical cylinder", () => {
    const f = fixture(); f.c.enemies = [enemy("near", 0, 0), enemy("near-air", 1, 3), enemy("high", 0, 7), enemy("far", 6, 0)];
    f.c.aim = { x: 0, y: 1, z: 10 }; f.c.isCharging = true; f.c.chargeTicks = 75;
    releaseSecondary(f.state, f.build, f.rng);
    expect(f.c.enemies.map((e) => e.hp < 100)).toEqual([true, true, false, false]);
    expect(f.c.secondaryEnergy).toBe(65);
  });
  it("settles death once and prevents recursive kill explosions", () => {
    const f = fixture(); f.build.killExplosionDamage = 6;
    const a = enemy("a", 0, 0, 10, 1), b = enemy("b", 1.8, 0, 10, 6), c = enemy("c", 3.6, 0, 10, 6);
    f.c.enemies = [a, b, c]; const money = f.state.resources.currency;
    damageEnemy(f.state, f.build, a, 10, f.rng); damageEnemy(f.state, f.build, a, 10, f.rng);
    expect(a.hp).toBe(0); expect(b.hp).toBe(0); expect(c.hp).toBe(6);
    expect(f.c.kills).toBe(2); expect(f.state.resources.currency - money).toBe(2);
    expect(f.c.experienceOrbs.reduce((sum, orb) => sum + orb.value, 0)).toBe(2);
  });
  it("settles cover-break retaliation through the same loot path", () => {
    const f = fixture(); f.build.eventDamage.coverBreak = 100;
    f.c.enemies = [enemy("victim")]; f.c.coverHealth[1] = 1;
    f.c.projectiles = [{ id: "impact", position: { x: 0, y: 1, z: 4 }, velocity: { x: 0, y: 0, z: -2 }, damage: 20, hostile: true, lifeTicks: 2 }];
    tickCombat(f.state, f.build, f.rng);
    expect(f.c.kills).toBe(1); expect(f.c.experienceOrbs.length).toBe(1);
  });
  it("intercepts descending projectiles but lets shots above the cover pass", () => {
    const f = fixture(); f.c.projectiles = [
      { id: "high", position: { x: 0, y: 5, z: 4 }, velocity: { x: 0, y: 0, z: -2 }, damage: 10, hostile: true, lifeTicks: 9 },
      { id: "descending", position: { x: 0, y: 3, z: 4 }, velocity: { x: 0, y: -2, z: -2 }, damage: 10, hostile: true, lifeTicks: 9 },
    ]; tickCombat(f.state, f.build, f.rng);
    expect(f.c.projectiles.map((p) => p.id)).toEqual(["high"]); expect(f.c.coverHealth[1]).toBeCloseTo(93.5);
  });
  it("caps boss support at 96 and the shared entity count at 48", () => {
    const f = fixture(7, "boss"); f.build.damageReduction = 1;
    for (let t = 0; t < 5400; t++) {
      if (t % 180 === 0) f.c.enemies = f.c.enemies.filter((e) => e.type === "boss");
      tickCombat(f.state, f.build, f.rng);
      expect(f.c.enemies.length).toBeLessThanOrEqual(48);
    }
    expect(f.c.horde.supportSpawned).toBe(96);
  });
  it("can clear a default room with normal weapon resources and without invulnerability", () => {
    const f = fixture(29);
    for (let t = 0; t < 9000 && !f.c.rewardReady && !f.state.completed; t++) {
      const targets = f.c.enemies.filter((e) => e.hp > 0).sort((a, b) => a.position.z - b.position.z);
      const target = targets[0];
      if (target) {
        f.c.aim = { ...target.position, y: (target.position.y ?? 0) + (target.type === "boss" ? 1.75 : 1) };
        if (!f.c.isCharging && !f.c.reloadTicks && !f.c.fireCooldown && f.c.secondaryEnergy >= 35 && targets.length >= 14) f.c.isCharging = true;
        if (f.c.isCharging && f.c.chargeTicks >= 75) releaseSecondary(f.state, f.build, f.rng);
        if (!f.c.isCharging) {
          if (f.c.ammo <= 0 && !f.c.reloadTicks) f.c.reloadTicks = f.build.reloadTicks;
          else firePrimary(f.state, f.build, f.rng);
        }
        if (f.c.coverHealth[f.c.playerCoverIndex]! < 15) {
          const best = f.c.coverHealth.indexOf(Math.max(...f.c.coverHealth));
          if (best !== f.c.playerCoverIndex) moveCover(f.state, f.build, best > f.c.playerCoverIndex ? 1 : -1);
        }
      }
      tickCombat(f.state, f.build, f.rng);
    }
    expect(f.state.completed).toBe(false); expect(f.c.rewardReady).toBe(true); expect(f.c.tick).toBeGreaterThanOrEqual(5400);
    expect(f.c.kills).toBeGreaterThan(100); expect(f.state.resources.aura).toBeGreaterThanOrEqual(100);
    console.log("HORDE_BASELINE", JSON.stringify({ seconds: f.c.tick / 60, kills: f.c.kills, aura: f.state.resources.aura, life: f.state.resources.life }));
  });
});

describe("experience, rewards and persistence", () => {
  it("fixes auraGain at drop time and credits only on arrival", () => {
    const f = fixture(); f.build.auraGain = 1.5; const e = enemy("xp", 0, 4, 10, 1); f.c.enemies = [e];
    damageEnemy(f.state, f.build, e, 10, f.rng); f.build.auraGain = 4;
    expect(f.state.resources.aura).toBe(0); expect(f.c.experienceOrbs.reduce((s, o) => s + o.value, 0)).toBe(1.5);
    for (let i = 0; i < 30; i++) tickCombat(f.state, f.build, f.rng);
    expect(f.state.resources.aura).toBe(0);
    for (let i = 0; i < 60; i++) tickCombat(f.state, f.build, f.rng);
    expect(f.state.resources.aura).toBeCloseTo(1.5); expect(f.c.experienceOrbs.length).toBe(0);
  });
  it("conserves split and merged values at the 128 orb limit and follows cover switches", () => {
    const f = fixture(); for (let i = 0; i < 200; i++) dropExperience(f.c, { x: 0, y: 4, z: 14 }, 3);
    expect(f.c.experienceOrbs.length).toBe(128); expect(f.c.experienceOrbs.reduce((s, o) => s + o.value, 0)).toBe(600);
    for (let i = 0; i < 35; i++) tickCombat(f.state, f.build, f.rng);
    moveCover(f.state, f.build, 1); for (let i = 0; i < 20; i++) tickCombat(f.state, f.build, f.rng);
    expect(f.c.experienceOrbs.every((o) => o.position.x > 0)).toBe(true);
    for (let i = 0; i < 50; i++) tickCombat(f.state, f.build, f.rng);
    expect(f.state.resources.aura).toBeCloseTo(600);
  });
  it("freezes orbs in every overlay and drains them before enabling a cleared room reward", () => {
    const f = fixture(); dropExperience(f.c, { x: 0, y: 1, z: 10 }, 3);
    f.controller.pause(); const before = structuredClone(f.c);
    for (let i = 0; i < 90; i++) f.controller.tick(); expect(f.c).toEqual(before);
    f.controller.resume(); f.c.tick = 5400; f.c.enemies = [];
    f.controller.tick(); expect(f.c.cleared).toBe(true); expect(f.c.rewardReady).toBe(false);
    for (let i = 0; i < 90; i++) f.controller.tick(); expect(f.c.rewardReady).toBe(true); expect(f.state.resources.aura).toBe(3);
  });
  it("retains overflow across repeated rituals and allows a full backpack to skip", () => {
    const f = fixture(); f.state.resources.aura = 250; f.state.backpackCapacity = 0;
    for (let i = 0; i < 2; i++) {
      f.controller.dispatchAction({ type: "gather" }); f.controller.completeRitual();
      expect(f.state.pendingReward?.offers.length).toBe(5); expect(f.controller.skipReward()).toBe(true); expect(f.state.mode).toBe("combat");
    }
    expect(f.state.resources.aura).toBe(50); f.controller.dispatchAction({ type: "gather" }); expect(f.state.mode).toBe("combat");
  });
  it("experience packs never overwrite overflow, and ordinary consumables are capped per room", () => {
    const f = fixture(); f.state.resources.aura = 250; f.state.mode = "map";
    f.state.floor.nodes.find((n) => n.id === "xp")!.status = "available";
    f.controller.interactWithNode("xp"); expect(f.state.resources.aura).toBe(250);
    f.rng.next = () => 0;
    for (let i = 0; i < 20; i++) damageEnemy(f.state, f.build, enemy(`drop-${i}`, 0, 0, 10, 1), 1, f.rng);
    expect(f.c.consumableDrops).toBe(2); expect(f.state.resources.consumables).toBe(2);
  });
  it("restores in-flight orbs without replaying feedback and uses legacy queues only for old saves", () => {
    const f = fixture(); dropExperience(f.c, { x: 2, y: 3, z: 10 }, 45); for (let i = 0; i < 30; i++) tickCombat(f.state, f.build, f.rng);
    const loaded = migrateSave(structuredClone(f.state))!;
    expect(loaded.combat!.experienceOrbs).toEqual(f.c.experienceOrbs); expect(loaded.combat!.feedbackEvents).toEqual([]);
    const rng = new SeededRng(f.rng.state);
    for (let i = 0; i < 60; i++) { tickCombat(loaded, f.build, rng); tickCombat(f.state, f.build, f.rng); }
    expect(loaded.resources.aura).toBe(f.state.resources.aura);
    const legacy = structuredClone(f.state); legacy.schemaVersion = 4; delete (legacy.combat as Partial<typeof f.c>).horde;
    legacy.combat!.spawnQueue = [{ ...enemy("legacy-queued"), delayTick: 500 }];
    const migrated = migrateSave(legacy)!; expect(migrated.schemaVersion).toBe(5); expect(migrated.combat!.horde.mode).toBe("legacy"); expect(migrated.combat!.spawnQueue).toHaveLength(1);
  });
});
