import { SeededRng } from "./rng";
import config from "./combat-config.json";
import { distance3, enemyCenter, enemyRadius, height, raySphere, segmentBox } from "./geometry";
import type { AnalyticsEvent, CombatFeedbackEvent, CombatState, EnemyState, ResolvedCombatBuild, RunState, Vec2 } from "./types";

const COVER_X = [-7.5, 0, 7.5] as const;
const chest = (combat: CombatState): Vec2 => ({ ...combat.playerPosition, y: 1.35 });
// The fixed perspective camera is the player's aiming reference. Using the
// same origin for hit queries keeps the cursor, rendered beam and damage ray
// on exactly one line even though the character is below the camera.
export const AIM_RAY_ORIGIN: Vec2 = { x: 0, y: 10, z: -18 };
const entityId = (combat: CombatState, kind: string): string => `${kind}-${combat.nextEntitySerial++}`;

export function pushCombatFeedback(combat: CombatState, event: Omit<CombatFeedbackEvent, "id" | "tick">): void {
  const serial = combat.nextFeedbackSerial++;
  combat.feedbackEvents.push({ ...event, id: `feedback-${serial}`, serial, tick: combat.tick });
  if (combat.feedbackEvents.length > config.limits.feedback) combat.feedbackEvents.splice(0, combat.feedbackEvents.length - config.limits.feedback);
}

function makeEnemy(combat: CombatState, type: EnemyState["type"], rng: SeededRng): EnemyState {
  const air = type === "flyer";
  const lane = rng.int(3);
  const entrance = rng.int(3);
  const x = entrance === 0 ? -11 + rng.next() * 2 : entrance === 1 ? 9 + rng.next() * 2 : -10 + rng.next() * 20;
  const z = entrance === 2 ? 17 + rng.next() * 5 : 8 + rng.next() * 12;
  const hp = config.health[type];
  return { id: entityId(combat, "enemy"), type, position: { x, y: air ? 3 + rng.next() * 3 : 0, z }, hp, maxHp: hp, shield: 0,
    attackCooldown: 75 + rng.int(90), spawnTick: combat.tick, layer: air ? "air" : "ground", behavior: "approach", targetCover: lane, windupTicks: 0 };
}

function spawnSupport(combat: CombatState, rng: SeededRng): boolean {
  const living = combat.enemies.filter((e) => e.hp > 0);
  if (living.length >= config.limits.enemies || (combat.roomType !== "combat" && combat.horde.supportSpawned >= config.limits.support)) return false;
  const roll = rng.next();
  let type: EnemyState["type"] = roll < config.airRatio ? "flyer" : "melee";
  if (roll >= 1 - config.specialistRatio) {
    if (roll > 0.96 && living.filter((e) => e.type === "summoner").length < config.limits.summoners) type = "summoner";
    else if (living.filter((e) => e.type === "ranged").length < config.limits.ranged) type = "ranged";
  }
  combat.enemies.push(makeEnemy(combat, type, rng));
  combat.horde.supportSpawned += 1;
  return true;
}

export function createCombat(roomType: CombatState["roomType"], rng: SeededRng, build: ResolvedCombatBuild): CombatState {
  const combat: CombatState = {
    tick: 0, roomType, roomStartedTick: 0, wave: 1, totalWaves: roomType === "elite" ? 1 : 3,
    spawnQueue: [], enemies: [], projectiles: [], nextFeedbackSerial: 0, feedbackEvents: [],
    playerCoverIndex: 1, coverHealth: [build.coverMax, build.coverMax, build.coverMax],
    playerPosition: { x: 0, y: 0, z: 1.1 }, aim: { x: 0, y: 1, z: 11 }, ammo: build.magazine,
    reloadTicks: 0, fireCooldown: 0, secondaryEnergy: build.secondaryEnergyMax, chargeTicks: 0, isCharging: false,
    cleared: false, rewardReady: false, damageDealt: 0, damageTaken: 0, bossPhaseTicks: [0, 0, 0], spiritWellAvailable: true,
    horde: { mode: "horde", endTick: config.durationTicks, nextSpawnTick: config.spawnIntervalTicks, pending: 0, supportSpawned: 0, stopped: false },
    experienceOrbs: [], nextEntitySerial: 0, consumableDrops: 0, kills: 0, combo: 0, lastKillTick: -999, lastCollectTick: -999,
  };
  if (roomType !== "combat") {
    const main = makeEnemy(combat, roomType === "boss" ? "boss" : "elite", rng);
    main.position = { x: 0, y: 0, z: 13 }; main.phase = 1;
    combat.enemies.push(main);
  }
  for (let i = 0; i < (roomType === "combat" ? config.openingCount : 8); i++) spawnSupport(combat, rng);
  return combat;
}

function addEvent(state: RunState, type: string, data: AnalyticsEvent["data"]): void {
  state.analytics.push({ tick: state.combat?.tick ?? 0, type, data });
}

/** Value is fixed at death. Orb animation never consumes gameplay random streams. */
export function dropExperience(combat: CombatState, position: Vec2, value: number): void {
  if (value <= 0) return;
  const count = Math.min(3, Math.max(1, Math.ceil(value)));
  for (let i = 0; i < count; i++) {
    const part = i === count - 1 ? value - (value / count) * i : value / count;
    if (combat.experienceOrbs.length >= config.limits.orbs) {
      combat.experienceOrbs[combat.experienceOrbs.length - 1]!.value += part;
      continue;
    }
    const serial = combat.nextEntitySerial;
    const angle = serial * 2.399963;
    combat.experienceOrbs.push({ id: entityId(combat, "orb"), position: { ...position }, origin: { ...position },
      burst: { x: position.x + Math.cos(angle) * 1.5, y: height(position) + 0.7 + (serial % 3) * 0.22, z: position.z + Math.sin(angle) },
      value: part, age: 0, duration: 60 + serial % 19 });
  }
}

function collectExperience(state: RunState): void {
  const combat = state.combat!;
  const destination = chest(combat);
  for (const orb of combat.experienceOrbs) {
    orb.age++;
    if (orb.age >= orb.duration) {
      state.resources.aura += orb.value;
      combat.lastCollectTick = combat.tick;
      pushCombatFeedback(combat, { type: "experienceCollected", to: destination, value: orb.value });
      addEvent(state, "experienceCollected", { value: orb.value, total: state.resources.aura });
      continue;
    }
    const flying = orb.age > 24;
    const t = flying ? Math.pow((orb.age - 24) / (orb.duration - 24), 2) : Math.min(1, orb.age / 12);
    const from = flying ? orb.burst : orb.origin, to = flying ? destination : orb.burst;
    orb.position = { x: from.x + (to.x - from.x) * t, z: from.z + (to.z - from.z) * t,
      y: height(from) + (height(to) - height(from)) * t + (flying ? Math.sin(t * Math.PI) * 1.7 : 0) };
  }
  combat.experienceOrbs = combat.experienceOrbs.filter((orb) => orb.age < orb.duration);
}

/** The only enemy death/loot entry point, including retaliation and explosions. */
export function damageEnemy(state: RunState, build: ResolvedCombatBuild, target: EnemyState, rawDamage: number, rng: SeededRng, weakpoint = false, allowExplosion = true): void {
  if (target.hp <= 0 || target.deathSettled || rawDamage <= 0) return;
  const combat = state.combat!;
  const actual = Math.min(target.hp + target.shield, rawDamage);
  const absorbed = Math.min(target.shield, rawDamage);
  target.shield -= absorbed; target.hp = Math.max(0, target.hp - (rawDamage - absorbed));
  target.lastHitTick = combat.tick; combat.damageDealt += actual;
  const point = enemyCenter(target);
  pushCombatFeedback(combat, { type: "enemyDamage", to: point, targetId: target.id, enemyType: target.type, value: Math.round(actual), weakpoint, worldY: height(point) + 1 });
  if (target.hp > 0) return;
  target.deathSettled = true; combat.kills++;
  combat.combo = combat.tick - combat.lastKillTick <= 120 ? combat.combo + 1 : 1;
  combat.lastKillTick = combat.tick;
  const special = target.type === "ranged" || target.type === "summoner";
  const aura = (target.type === "boss" ? 0 : target.type === "elite" ? config.drops.eliteAura : special ? config.drops.specialistAura : config.drops.fodderAura) * build.auraGain;
  dropExperience(combat, point, aura);
  state.resources.currency += target.type === "boss" ? config.drops.bossMoney : target.type === "elite" ? config.drops.eliteMoney : special ? config.drops.specialistMoney : config.drops.fodderMoney;
  if (target.type !== "boss" && target.type !== "elite" && combat.consumableDrops < config.drops.maxConsumables && rng.next() < config.drops.consumableChance) {
    combat.consumableDrops++; state.resources.rerolls++; state.resources.consumables++;
    addEvent(state, "consumableDrop", { enemyType: target.type });
  }
  pushCombatFeedback(combat, { type: "enemyDeath", to: point, targetId: target.id, enemyType: target.type });
  addEvent(state, "enemyDefeated", { enemyType: target.type, aura });
  if (allowExplosion && build.killExplosionDamage > 0) {
    pushCombatFeedback(combat, { type: "explosion", to: point, value: config.killExplosionRadius });
    for (const other of combat.enemies) if (other.hp > 0 && distance3(enemyCenter(other), point) <= config.killExplosionRadius) damageEnemy(state, build, other, build.killExplosionDamage, rng, false, false);
  }
}

export function firePrimary(state: RunState, build: ResolvedCombatBuild, rng: SeededRng): boolean {
  const combat = state.combat;
  if (!combat || combat.cleared || combat.reloadTicks > 0 || combat.fireCooldown > 0 || combat.ammo <= 0) return false;
  combat.ammo--; combat.fireCooldown = build.fireCooldownTicks;
  const feedbackOrigin = chest(combat), aim = { ...combat.aim, y: height(combat.aim, 1) };
  const origin = combat.aimOrigin ?? feedbackOrigin;
  const length = Math.max(0.001, distance3(origin, aim));
  const direction = { x: (aim.x - origin.x) / length, y: (height(aim) - height(origin)) / length, z: (aim.z - origin.z) / length };
  const hits = combat.enemies.filter((e) => e.hp > 0).map((target) => ({ target, t: raySphere(origin, direction, enemyCenter(target), enemyRadius(target)) }))
    .filter((hit): hit is { target: EnemyState; t: number } => hit.t !== undefined).sort((a, b) => a.t - b.t).slice(0, 1 + build.primaryPierce);
  let total = 0, anyWeakpoint = false;
  for (let i = 0; i < hits.length; i++) {
    const { target } = hits[i]!;
    const weakpoint = raySphere(origin, direction, enemyCenter(target), enemyRadius(target) * 0.48) !== undefined;
    const damage = (build.primaryDamage * (weakpoint ? build.weakpointMultiplier : 1) + (weakpoint ? build.eventDamage.weakpoint ?? 0 : 0) + (combat.ammo === 0 ? build.eventDamage.lastShot ?? 0 : 0)) * (i === 0 ? 1 : config.pierceMultiplier);
    damageEnemy(state, build, target, damage, rng, weakpoint); total += damage; anyWeakpoint ||= weakpoint;
  }
  const end = hits.length ? enemyCenter(hits[hits.length - 1]!.target) : aim;
  pushCombatFeedback(combat, { type: "primary", from: feedbackOrigin, to: end, hit: hits.length > 0, weakpoint: anyWeakpoint, value: Math.round(total) });
  addEvent(state, hits.length ? "shotHit" : "shotMiss", { targets: hits.length, weakpoint: anyWeakpoint, damage: Math.round(total) });
  return true;
}

export function releaseSecondary(state: RunState, build: ResolvedCombatBuild, rng: SeededRng): boolean {
  const combat = state.combat;
  if (!combat || !combat.isCharging) return false;
  combat.isCharging = false;
  const charge = Math.min(1, combat.chargeTicks / 75); combat.chargeTicks = 0;
  if (combat.secondaryEnergy < build.secondaryEnergyCost) return false;
  combat.secondaryEnergy -= build.secondaryEnergyCost; combat.fireCooldown = Math.max(combat.fireCooldown, 18);
  const center = { ...combat.aim, y: height(combat.aim, 1) }, radius = build.secondaryRadius + charge * config.secondaryChargeRadius;
  const targets = combat.enemies.filter((e) => e.hp > 0 && distance3(enemyCenter(e), center) <= radius);
  const damage = build.secondaryDamage * (0.45 + charge * 0.75) + (build.eventDamage.charge ?? 0);
  for (const target of targets) damageEnemy(state, build, target, damage, rng);
  pushCombatFeedback(combat, { type: "secondary", from: chest(combat), to: center, hit: targets.length > 0, value: radius, charge });
  addEvent(state, "secondaryReleased", { charge, targets: targets.length, damage, energySpent: build.secondaryEnergyCost });
  return true;
}

function damageLife(state: RunState, rawDamage: number): void {
  if (rawDamage <= 0) return;
  state.combat!.damageTaken += rawDamage; state.resources.life = Math.max(0, state.resources.life - rawDamage);
  pushCombatFeedback(state.combat!, { type: "playerHit", to: chest(state.combat!), value: Math.round(rawDamage) });
}

function damageCover(state: RunState, build: ResolvedCombatBuild, index: number, damage: number, rng: SeededRng): void {
  const c = state.combat!, current = c.coverHealth[index] ?? 0;
  const mitigated = damage * (1 - build.damageReduction) * (current > 0 ? 1 - build.coverReduction : 1);
  const absorbed = Math.min(current, mitigated);
  c.coverHealth[index] = Math.max(0, current - absorbed); c.damageTaken += absorbed;
  if (absorbed > 0) pushCombatFeedback(c, { type: "coverHit", to: { x: COVER_X[index]!, y: 1.15, z: 2.5 }, value: Math.round(absorbed) });
  if (current > 0 && c.coverHealth[index] === 0) {
    c.coverHealth[index] = Math.min(build.coverMax, build.eventCover.coverBreak ?? 0);
    const retaliation = build.eventDamage.coverBreak ?? 0;
    for (const target of c.enemies) damageEnemy(state, build, target, retaliation, rng);
    addEvent(state, "coverBreak", { coverIndex: index, retaliation });
  }
  if (index === c.playerCoverIndex) damageLife(state, mitigated - absorbed);
}

function spawnProjectile(c: CombatState, source: Vec2, target: Vec2, damage: number, spread = 0): void {
  if (c.projectiles.length >= config.limits.projectiles) return;
  const destination = { ...target, x: target.x + spread * 15 };
  const length = Math.max(0.01, distance3(source, destination)), speed = 0.14;
  c.projectiles.push({ id: entityId(c, "projectile"), position: { ...source },
    velocity: { x: (destination.x - source.x) / length * speed, y: (height(destination) - height(source)) / length * speed, z: (destination.z - source.z) / length * speed },
    damage, hostile: true, lifeTicks: 360 });
}

function updateBoss(state: RunState, build: ResolvedCombatBuild, boss: EnemyState, rng: SeededRng): void {
  const c = state.combat!, ratio = boss.hp / boss.maxHp;
  const phase: 1 | 2 | 3 = ratio <= 0.35 ? 3 : ratio <= 0.7 ? 2 : 1;
  if (phase > (boss.phase ?? 1)) {
    boss.phase = phase; boss.staggerTicks = 75;
    if (phase === 2) {
      boss.shield = 150;
      for (let i = 0; i < 6; i++) spawnSupport(c, rng);
    }
    else { const intact = c.coverHealth.map((hp, index) => ({ hp, index })).filter((cover) => cover.hp > 0);
      if (intact.length) { const cover = rng.pick(intact); damageCover(state, build, cover.index, cover.hp / Math.max(0.01, (1 - build.coverReduction) * (1 - build.damageReduction)), rng); } }
    addEvent(state, "bossPhase", { phase, healthRatio: ratio });
  }
  c.bossPhaseTicks[phase - 1]++;
  if ((boss.staggerTicks ?? 0) > 0) { boss.staggerTicks!--; return; }
  if (boss.attackCooldown > 0) return;
  boss.behavior = "windup"; boss.windupTicks = (boss.windupTicks ?? 0) + 1;
  if (boss.windupTicks < 60) return;
  for (let i = 0; i < phase + 2; i++) spawnProjectile(c, enemyCenter(boss), chest(c), 13 + phase * 2, (i - (phase + 1) / 2) * 0.12);
  boss.behavior = "recover"; boss.windupTicks = 0; boss.attackCooldown = phase === 3 ? 58 : phase === 2 ? 82 : 105;
}

function runDirector(c: CombatState, rng: SeededRng): void {
  if (c.horde.mode === "legacy") {
    while (c.enemies.length < 2 && c.spawnQueue.length && c.spawnQueue[0]!.delayTick <= c.tick) c.enemies.push(c.spawnQueue.shift()!);
    c.horde.stopped = c.spawnQueue.length === 0; return;
  }
  const stage = Math.min(2, Math.floor(c.tick / config.stageTicks));
  c.wave = stage + 1;
  const mainAlive = c.enemies.some((e) => e.hp > 0 && (e.type === "elite" || e.type === "boss"));
  if ((c.roomType === "combat" && c.tick >= c.horde.endTick) || (c.roomType !== "combat" && (!mainAlive || c.horde.supportSpawned >= config.limits.support))) {
    c.horde.stopped = true; c.horde.pending = 0; return;
  }
  if (c.tick >= c.horde.nextSpawnTick) {
    c.horde.pending += c.roomType === "combat" ? config.batchSizes[stage]! : 6;
    c.horde.nextSpawnTick += config.spawnIntervalTicks;
  }
  const cap = c.roomType === "combat" ? config.stageCaps[stage]! : config.limits.enemies;
  while (c.horde.pending > 0 && c.enemies.filter((e) => e.hp > 0).length < cap) {
    if (!spawnSupport(c, rng)) break;
    c.horde.pending--;
  }
}

function updateEnemies(state: RunState, build: ResolvedCombatBuild, rng: SeededRng): void {
  const c = state.combat!;
  let meleeActive = c.enemies.filter((e) => e.hp > 0 && e.behavior === "windup" && (e.type === "melee" || e.type === "minion")).length;
  let rangedActive = c.enemies.filter((e) => e.hp > 0 && e.behavior === "windup" && e.type !== "melee" && e.type !== "minion").length;
  const laneRanks = [0, 0, 0];
  for (const e of [...c.enemies]) {
    if (e.hp <= 0) continue;
    if (e.attackCooldown > 0) e.attackCooldown--;
    if (e.type === "boss") { updateBoss(state, build, e, rng); continue; }
    const melee = e.type === "melee" || e.type === "minion";
    const lane = e.targetCover ?? Math.max(0, Math.min(2, Math.round(e.position.x / 7.5) + 1));
    e.targetCover = lane;
    const serial = Number(e.id.replace(/\D/g, "")) || 0;
    const rank = melee ? laneRanks[lane]!++ : 0;
    const destX = COVER_X[lane]! + (melee ? ((rank % 3) - 1) * 1.35 : Math.sin(serial * 2.4) * 2.5);
    const destZ = melee ? 4.2 + Math.floor(rank / 3) * 1.25 : 7.5 + (serial % 6) * 1.2;
    if (e.behavior !== "windup") {
      const dx = destX - e.position.x, dz = destZ - e.position.z, length = Math.hypot(dx, dz);
      if (length > 0.12) { const speed = e.type === "flyer" ? 0.033 : melee ? 0.024 : 0.013; e.position.x += dx / length * speed; e.position.z += dz / length * speed; }
    }
    if (e.behavior === "windup") {
      e.windupTicks = (e.windupTicks ?? 1) - 1;
      if (e.windupTicks > 0) continue;
      if (melee) damageCover(state, build, lane, config.attack.meleeDamage, rng);
      else if (e.type === "summoner") {
        const cap = c.roomType === "combat" ? config.stageCaps[Math.min(2, Math.floor(c.tick / config.stageTicks))]! : config.limits.enemies;
        if (!c.horde.stopped && c.enemies.length < cap && (c.roomType === "combat" || c.horde.supportSpawned < config.limits.support)) {
          const child = makeEnemy(c, "minion", rng); child.position = { ...e.position, x: e.position.x + 0.7, y: 0 }; c.enemies.push(child); c.horde.supportSpawned++;
        }
      } else spawnProjectile(c, enemyCenter(e), { x: COVER_X[lane]!, y: 1.05, z: 1.1 }, e.type === "elite" ? 19 : config.attack.rangedDamage);
      e.behavior = "recover"; e.attackCooldown = config.attack.cooldown; continue;
    }
    if (e.attackCooldown > 0 || (melee && e.position.z > 5.8)) continue;
    if (melee ? meleeActive >= config.limits.meleeAttacks : rangedActive >= config.limits.rangedAttacks) continue;
    if (melee) meleeActive++; else rangedActive++;
    e.behavior = "windup"; e.windupTicks = melee ? config.attack.meleeWindup : config.attack.rangedWindup;
  }
  // Stable pair ordering keeps separation deterministic and keeps air/ground independent.
  for (let i = 0; i < c.enemies.length; i++) for (let j = i + 1; j < c.enemies.length; j++) {
    const a = c.enemies[i]!, b = c.enemies[j]!;
    if (a.hp <= 0 || b.hp <= 0 || a.type === "boss" || b.type === "boss" || a.layer !== b.layer) continue;
    const dx = b.position.x - a.position.x, dz = b.position.z - a.position.z, length = Math.hypot(dx, dz);
    if (length >= 1.2) continue;
    const amount = (1.2 - length) * 0.12, nx = length > 0.001 ? dx / length : 1, nz = length > 0.001 ? dz / length : 0;
    a.position.x -= nx * amount; a.position.z -= nz * amount; b.position.x += nx * amount; b.position.z += nz * amount;
  }
}

export function tickCombat(state: RunState, build: ResolvedCombatBuild, rng: SeededRng): void {
  const c = state.combat;
  if (!c) return;
  c.tick++;
  collectExperience(state);
  if (c.tick - c.lastKillTick > 120) c.combo = 0;
  if (c.cleared) { c.rewardReady = c.experienceOrbs.length === 0; return; }
  if (c.fireCooldown > 0) c.fireCooldown--;
  if (c.isCharging) c.chargeTicks = Math.min(90, c.chargeTicks + 1);
  else c.secondaryEnergy = Math.min(build.secondaryEnergyMax, c.secondaryEnergy + build.secondaryEnergyRegen / 60);
  if (c.reloadTicks > 0 && --c.reloadTicks === 0) {
    c.ammo = build.magazine; const index = c.playerCoverIndex;
    c.coverHealth[index] = Math.min(build.coverMax, c.coverHealth[index]! + (build.eventCover.reload ?? 0));
    for (const e of c.enemies) damageEnemy(state, build, e, build.eventDamage.reload ?? 0, rng);
    pushCombatFeedback(c, { type: "reloadComplete", to: chest(c) }); addEvent(state, "reloadComplete", { ammo: c.ammo });
  }
  runDirector(c, rng); updateEnemies(state, build, rng);
  for (const p of c.projectiles) {
    const from = { ...p.position, y: height(p.position, 1.15) };
    p.position = { x: from.x + p.velocity.x, y: height(from) + height(p.velocity), z: from.z + p.velocity.z }; p.lifeTicks--;
    if (!p.hostile) continue;
    const hits = c.coverHealth.map((hp, i) => ({ i, t: hp > 0 ? segmentBox(from, p.position, { x: COVER_X[i]! - 2.25, y: 0, z: 1.89 }, { x: COVER_X[i]! + 2.25, y: 1.68, z: 3.11 }) : undefined }))
      .filter((hit): hit is { i: number; t: number } => hit.t !== undefined).sort((a, b) => a.t - b.t);
    if (hits.length) { damageCover(state, build, hits[0]!.i, p.damage, rng); p.lifeTicks = 0; }
    else if (segmentBox(from, p.position, { x: c.playerPosition.x - 0.6, y: 0, z: 0.5 }, { x: c.playerPosition.x + 0.6, y: 2.3, z: 1.7 }) !== undefined) {
      damageLife(state, p.damage * (1 - build.damageReduction)); p.lifeTicks = 0;
    }
  }
  c.projectiles = c.projectiles.filter((p) => p.lifeTicks > 0); c.enemies = c.enemies.filter((e) => e.hp > 0);
  if (state.resources.life <= 0) { state.completed = true; state.victory = false; state.mode = "result"; addEvent(state, "runEnded", { victory: false, nodeId: state.currentNodeId }); return; }
  if (c.horde.stopped && c.spawnQueue.length === 0 && c.enemies.length === 0) {
    c.cleared = true; c.projectiles = []; c.isCharging = false; c.rewardReady = c.experienceOrbs.length === 0;
    addEvent(state, "roomCleared", { nodeId: state.currentNodeId, durationTicks: c.tick, damageTaken: Math.round(c.damageTaken), kills: c.kills });
  }
}

export function moveCover(state: RunState, build: ResolvedCombatBuild, direction: -1 | 1): void {
  const c = state.combat;
  if (!c || c.cleared) return;
  const index = Math.max(0, Math.min(2, c.playerCoverIndex + direction));
  if (index === c.playerCoverIndex) return;
  const from = { ...c.playerPosition }; c.playerCoverIndex = index; c.playerPosition.x = COVER_X[index]!;
  c.ammo = Math.min(build.magazine, c.ammo + (build.eventAmmo.leaveCover ?? 0));
  pushCombatFeedback(c, { type: "coverMove", from, to: { ...c.playerPosition } }); addEvent(state, "coverMoved", { coverIndex: index });
}
