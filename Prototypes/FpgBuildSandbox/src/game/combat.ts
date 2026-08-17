import { SeededRng } from "./rng";
import type { AnalyticsEvent, CombatFeedbackEvent, CombatState, EnemyState, ResolvedCombatBuild, RunState, Vec2 } from "./types";

const COVER_X = [-7.5, 0, 7.5] as const;
const COVER_Z = 2.5;
const COVER_HALF_WIDTH = 2.25;
const COVER_HALF_DEPTH = 0.61;
const SPAWNS: Vec2[] = [{ x: -6.5, z: 10.5 }, { x: -2.2, z: 13.2 }, { x: 2.5, z: 11.5 }, { x: 6.2, z: 14 }];
const NORMAL_WAVES = 3;
const ENEMIES_PER_WAVE = 7;

function enemy(id: string, type: EnemyState["type"], position: Vec2, hp: number, delayTick: number): EnemyState & { delayTick: number } {
  return { id, type, position: { ...position }, hp, maxHp: hp, shield: 0, attackCooldown: 60, spawnTick: 0, delayTick };
}

export function pushCombatFeedback(combat: CombatState, event: Omit<CombatFeedbackEvent, "id" | "tick">): void {
  combat.feedbackEvents.push({
    ...event,
    id: `feedback-${combat.tick}-${combat.nextFeedbackSerial}`,
    tick: combat.tick,
  });
  combat.nextFeedbackSerial += 1;
  if (combat.feedbackEvents.length > 32) combat.feedbackEvents.splice(0, combat.feedbackEvents.length - 32);
}

export function createCombat(roomType: CombatState["roomType"], rng: SeededRng, build: ResolvedCombatBuild): CombatState {
  const queue: Array<EnemyState & { delayTick: number }> = [];
  if (roomType === "combat") {
    const types: EnemyState["type"][] = ["melee", "ranged", "ranged", "summoner", "melee", "ranged", "melee"];
    for (let wave = 0; wave < NORMAL_WAVES; wave += 1) {
      for (let slot = 0; slot < ENEMIES_PER_WAVE; slot += 1) {
        const index = wave * ENEMIES_PER_WAVE + slot;
        const type = types[(slot + wave + rng.int(types.length)) % types.length] as EnemyState["type"];
        queue.push(enemy(`enemy-${index}`, type, SPAWNS[index % SPAWNS.length] as Vec2, type === "summoner" ? 68 : 52, wave * 240));
      }
    }
  } else if (roomType === "elite") {
    queue.push(enemy("elite-0", "elite", SPAWNS[1] as Vec2, 260, 0));
    queue.push(enemy("elite-guard-1", "ranged", SPAWNS[0] as Vec2, 70, 120));
    queue.push(enemy("elite-guard-2", "melee", SPAWNS[3] as Vec2, 78, 240));
  } else {
    const boss = enemy("boss-0", "boss", { x: 0, z: 12 }, 880, 0);
    boss.phase = 1;
    queue.push(boss);
  }
  return {
    tick: 0,
    roomType,
    roomStartedTick: 0,
    wave: 1,
    totalWaves: roomType === "combat" ? NORMAL_WAVES : roomType === "boss" ? 3 : 1,
    spawnQueue: queue,
    enemies: [],
    projectiles: [],
    nextFeedbackSerial: 0,
    feedbackEvents: [],
    playerCoverIndex: 1,
    coverHealth: [build.coverMax, build.coverMax, build.coverMax],
    playerPosition: { x: 0, z: 1.1 },
    aim: { x: 0, z: 11 },
    ammo: build.magazine,
    reloadTicks: 0,
    fireCooldown: 0,
    secondaryEnergy: build.secondaryEnergyMax,
    chargeTicks: 0,
    isCharging: false,
    cleared: false,
    rewardReady: false,
    damageDealt: 0,
    damageTaken: 0,
    bossPhaseTicks: [0, 0, 0],
    spiritWellAvailable: true,
  };
}

function distance(a: Vec2, b: Vec2): number {
  return Math.hypot(a.x - b.x, a.z - b.z);
}

function addEvent(state: RunState, type: string, data: AnalyticsEvent["data"]): void {
  state.analytics.push({ tick: state.combat?.tick ?? 0, type, data });
}

function damageEnemy(state: RunState, target: EnemyState, rawDamage: number, rng: SeededRng): void {
  const combat = state.combat!;
  let damage = rawDamage;
  if (target.shield > 0) {
    const absorbed = Math.min(target.shield, damage);
    target.shield -= absorbed;
    damage -= absorbed;
  }
  if (damage > 0) target.hp -= damage;
  combat.damageDealt += rawDamage;
  if (target.hp > 0) return;
  target.hp = 0;
  const aura = target.type === "boss" ? 0 : target.type === "elite" ? 45 : 22;
  state.resources.aura = Math.min(state.resources.auraRequired, state.resources.aura + aura);
  state.resources.currency += target.type === "elite" ? 55 : target.type === "boss" ? 100 : 12 + rng.int(8);
  if (rng.next() < 0.08) {
    state.resources.rerolls += 1;
    state.resources.consumables += 1;
    addEvent(state, "consumableDrop", { enemyType: target.type });
  }
  addEvent(state, "enemyDefeated", { enemyType: target.type, aura });
}

export function firePrimary(state: RunState, build: ResolvedCombatBuild, rng: SeededRng): boolean {
  const combat = state.combat;
  if (!combat || combat.cleared || combat.reloadTicks > 0 || combat.fireCooldown > 0 || combat.ammo <= 0) return false;
  combat.ammo -= 1;
  combat.fireCooldown = build.fireCooldownTicks;
  const ranked = combat.enemies.filter((target) => target.hp > 0).map((target) => ({ target, aimDistance: distance(target.position, combat.aim) })).sort((a, b) => a.aimDistance - b.aimDistance);
  const candidate = ranked[0];
  let hit = false;
  let weakpoint = false;
  let damage = 0;
  let targetPoint = { ...combat.aim };
  if (candidate && candidate.aimDistance <= 1.7) {
    hit = true;
    weakpoint = candidate.aimDistance <= 0.48;
    targetPoint = { ...candidate.target.position };
    damage = build.primaryDamage * (weakpoint ? build.weakpointMultiplier : 1);
    if (weakpoint) damage += build.eventDamage.weakpoint ?? 0;
    if (combat.ammo === 0) damage += build.eventDamage.lastShot ?? 0;
    damageEnemy(state, candidate.target, damage, rng);
    addEvent(state, "shotHit", { weakpoint, damage: Math.round(damage) });
  } else addEvent(state, "shotMiss", {});
  pushCombatFeedback(combat, {
    type: "primary",
    from: { ...combat.playerPosition },
    to: targetPoint,
    hit,
    weakpoint,
    value: Math.round(damage),
  });
  return true;
}

export function releaseSecondary(state: RunState, build: ResolvedCombatBuild, rng: SeededRng): boolean {
  const combat = state.combat;
  if (!combat || !combat.isCharging) return false;
  if (combat.secondaryEnergy < build.secondaryEnergyCost) {
    combat.isCharging = false;
    combat.chargeTicks = 0;
    return false;
  }
  const chargeRatio = Math.min(1, combat.chargeTicks / 75);
  combat.isCharging = false;
  combat.chargeTicks = 0;
  combat.secondaryEnergy = Math.max(0, combat.secondaryEnergy - build.secondaryEnergyCost);
  combat.fireCooldown = Math.max(combat.fireCooldown, 18);
  const center = combat.aim;
  const targets = combat.enemies.filter((target) => target.hp > 0 && distance(target.position, center) <= 2.2 + chargeRatio * 1.3);
  const damage = build.secondaryDamage * (0.45 + chargeRatio * 0.75) + (build.eventDamage.charge ?? 0);
  for (const target of targets) damageEnemy(state, target, damage, rng);
  pushCombatFeedback(combat, {
    type: "secondary",
    from: { ...combat.playerPosition },
    to: { ...center },
    hit: targets.length > 0,
    value: Math.round(damage),
    charge: chargeRatio,
  });
  addEvent(state, "secondaryReleased", {
    charge: Number(chargeRatio.toFixed(2)),
    targets: targets.length,
    damage: Math.round(damage),
    energySpent: build.secondaryEnergyCost,
    energyRemaining: Math.round(combat.secondaryEnergy),
  });
  return true;
}

function damageLife(state: RunState, combat: CombatState, rawDamage: number): void {
  if (rawDamage <= 0) return;
  combat.damageTaken += rawDamage;
  state.resources.life = Math.max(0, state.resources.life - rawDamage);
  pushCombatFeedback(combat, { type: "playerHit", to: { ...combat.playerPosition }, value: Math.round(rawDamage) });
}

function damageCover(state: RunState, build: ResolvedCombatBuild, coverIndex: number, rawDamage: number): number {
  const combat = state.combat!;
  const currentCover = combat.coverHealth[coverIndex] ?? 0;
  const mitigatedDamage = rawDamage * (1 - build.damageReduction);
  if (currentCover <= 0) return mitigatedDamage;
  const coverDamage = mitigatedDamage * (1 - build.coverReduction);
  const absorbed = Math.min(currentCover, coverDamage);
  combat.coverHealth[coverIndex] = Math.max(0, currentCover - absorbed);
  combat.damageTaken += absorbed;
  pushCombatFeedback(combat, { type: "coverHit", to: { x: COVER_X[coverIndex] as number, z: COVER_Z }, value: Math.round(coverDamage) });
  if (combat.coverHealth[coverIndex] <= 0) {
    combat.coverHealth[coverIndex] = Math.min(build.coverMax, build.eventCover.coverBreak ?? 0);
    const retaliation = build.eventDamage.coverBreak ?? 0;
    if (retaliation > 0) for (const target of combat.enemies) target.hp -= retaliation;
    addEvent(state, "coverBreak", { coverIndex, retaliation });
  }
  return Math.max(0, coverDamage - absorbed);
}

function damageAtCurrentCover(state: RunState, build: ResolvedCombatBuild, rawDamage: number): void {
  const combat = state.combat!;
  const lifeDamage = damageCover(state, build, combat.playerCoverIndex, rawDamage);
  damageLife(state, combat, lifeDamage);
}

function segmentIntersectsAabb(start: Vec2, end: Vec2, minX: number, maxX: number, minZ: number, maxZ: number): boolean {
  let near = 0;
  let far = 1;
  for (const [origin, delta, min, max] of [
    [start.x, end.x - start.x, minX, maxX],
    [start.z, end.z - start.z, minZ, maxZ],
  ] as const) {
    if (Math.abs(delta) < 1e-8) {
      if (origin < min || origin > max) return false;
      continue;
    }
    const first = (min - origin) / delta;
    const second = (max - origin) / delta;
    near = Math.max(near, Math.min(first, second));
    far = Math.min(far, Math.max(first, second));
    if (near > far) return false;
  }
  return true;
}

function segmentHitsPlayer(start: Vec2, end: Vec2, player: Vec2): boolean {
  const dx = end.x - start.x;
  const dz = end.z - start.z;
  const lengthSquared = dx * dx + dz * dz;
  const projection = lengthSquared === 0 ? 0 : Math.max(0, Math.min(1, ((player.x - start.x) * dx + (player.z - start.z) * dz) / lengthSquared));
  return distance({ x: start.x + dx * projection, z: start.z + dz * projection }, player) < 0.7;
}

function spawnProjectile(combat: CombatState, source: Vec2, target: Vec2, damage: number, spread = 0): void {
  if (combat.projectiles.length >= 32) return;
  const angle = Math.atan2(target.z - source.z, target.x - source.x) + spread;
  const speed = 0.11;
  combat.projectiles.push({
    id: `projectile-${combat.tick}-${combat.projectiles.length}`,
    position: { ...source },
    velocity: { x: Math.cos(angle) * speed, z: Math.sin(angle) * speed },
    damage,
    hostile: true,
    lifeTicks: 220,
  });
}

function updateBoss(state: RunState, build: ResolvedCombatBuild, boss: EnemyState, rng: SeededRng): void {
  const combat = state.combat!;
  const ratio = boss.hp / boss.maxHp;
  const desiredPhase: 1 | 2 | 3 = ratio <= 0.35 ? 3 : ratio <= 0.7 ? 2 : 1;
  if (desiredPhase > (boss.phase ?? 1)) {
    boss.phase = desiredPhase;
    boss.staggerTicks = 75;
    if (desiredPhase === 2) {
      boss.shield = 150;
      for (let index = 0; index < 2; index += 1) {
        combat.enemies.push({ ...enemy(`boss-summon-${combat.tick}-${index}`, "minion", SPAWNS[index * 3] as Vec2, 55, 0), spawnTick: combat.tick });
      }
    } else {
      const intactCovers = combat.coverHealth.map((health, index) => ({ health, index })).filter((cover) => cover.health > 0);
      if (intactCovers.length > 0) {
        const destroyed = rng.pick(intactCovers);
        combat.coverHealth[destroyed.index] = 0;
        pushCombatFeedback(combat, { type: "coverHit", to: { x: COVER_X[destroyed.index] as number, z: 2.5 }, value: Math.round(destroyed.health) });
        addEvent(state, "coverDestroyed", { coverIndex: destroyed.index, source: "bossPhase" });
      }
    }
    addEvent(state, "bossPhase", { phase: desiredPhase, healthRatio: Number(ratio.toFixed(2)) });
  }
  combat.bossPhaseTicks[(boss.phase ?? 1) - 1] += 1;
  if ((boss.staggerTicks ?? 0) > 0) {
    boss.staggerTicks = (boss.staggerTicks ?? 0) - 1;
    return;
  }
  if (boss.attackCooldown > 0) return;
  const phase = boss.phase ?? 1;
  const fan = phase === 1 ? 3 : phase === 2 ? 4 : 5;
  for (let index = 0; index < fan; index += 1) spawnProjectile(combat, boss.position, combat.playerPosition, 13 + phase * 2, (index - (fan - 1) / 2) * 0.12);
  boss.attackCooldown = phase === 3 ? 58 : phase === 2 ? 82 : 105;
  if (phase === 2 && combat.enemies.length < 6 && rng.next() < 0.4) {
    combat.enemies.push({ ...enemy(`boss-minion-${combat.tick}`, "minion", rng.pick(SPAWNS), 48, 0), spawnTick: combat.tick });
  }
  void build;
}

export function tickCombat(state: RunState, build: ResolvedCombatBuild, rng: SeededRng): void {
  const combat = state.combat;
  if (!combat || combat.cleared) return;
  combat.tick += 1;
  if (combat.fireCooldown > 0) combat.fireCooldown -= 1;
  if (combat.isCharging) combat.chargeTicks = Math.min(90, combat.chargeTicks + 1);
  else combat.secondaryEnergy = Math.min(build.secondaryEnergyMax, combat.secondaryEnergy + build.secondaryEnergyRegen / 60);
  if (combat.reloadTicks > 0) {
    combat.reloadTicks -= 1;
    if (combat.reloadTicks === 0) {
      combat.ammo = build.magazine;
      const coverIndex = combat.playerCoverIndex;
      combat.coverHealth[coverIndex] = Math.min(build.coverMax, (combat.coverHealth[coverIndex] ?? 0) + (build.eventCover.reload ?? 0));
      const reloadDamage = build.eventDamage.reload ?? 0;
      if (reloadDamage > 0) for (const target of combat.enemies) damageEnemy(state, target, reloadDamage, rng);
      pushCombatFeedback(combat, { type: "reloadComplete", to: { ...combat.playerPosition } });
      addEvent(state, "reloadComplete", { ammo: combat.ammo });
    }
  }

  while (combat.enemies.length < 2 && combat.spawnQueue.length > 0 && (combat.spawnQueue[0]?.delayTick ?? Infinity) <= combat.tick) {
    const spawned = combat.spawnQueue.shift()!;
    spawned.spawnTick = combat.tick;
    combat.enemies.push(spawned);
    combat.wave = Math.min(combat.totalWaves, Math.floor(spawned.delayTick / 240) + 1);
  }

  for (const target of combat.enemies) {
    if (target.hp <= 0) continue;
    if (target.attackCooldown > 0) target.attackCooldown -= 1;
    if (target.type === "boss") {
      updateBoss(state, build, target, rng);
      continue;
    }
    if (target.type === "melee" || target.type === "minion") {
      target.position.z -= target.type === "minion" ? 0.025 : 0.018;
      if (target.position.z <= 3.25 && target.attackCooldown <= 0) {
        damageAtCurrentCover(state, build, target.type === "minion" ? 10 : 16);
        target.position.z += 1.6;
        target.attackCooldown = 90;
      }
    } else if (target.type === "summoner") {
      if (target.attackCooldown <= 0 && combat.enemies.length < 7) {
        combat.enemies.push({ ...enemy(`summon-${combat.tick}`, "minion", { x: target.position.x + (rng.next() - 0.5) * 2, z: target.position.z - 1 }, 38, 0), spawnTick: combat.tick });
        target.attackCooldown = 170;
      }
    } else if (target.attackCooldown <= 0) {
      spawnProjectile(combat, target.position, combat.playerPosition, target.type === "elite" ? 19 : 12);
      if (target.type === "elite") {
        spawnProjectile(combat, target.position, combat.playerPosition, 14, -0.12);
        spawnProjectile(combat, target.position, combat.playerPosition, 14, 0.12);
      }
      target.attackCooldown = target.type === "elite" ? 78 : 115;
    }
  }

  for (const projectile of combat.projectiles) {
    const previousPosition = { ...projectile.position };
    projectile.position.x += projectile.velocity.x;
    projectile.position.z += projectile.velocity.z;
    projectile.lifeTicks -= 1;
    if (!projectile.hostile) continue;
    const hitCoverIndex = combat.coverHealth.findIndex((health, index) => health > 0 && segmentIntersectsAabb(
      previousPosition,
      projectile.position,
      (COVER_X[index] as number) - COVER_HALF_WIDTH,
      (COVER_X[index] as number) + COVER_HALF_WIDTH,
      COVER_Z - COVER_HALF_DEPTH,
      COVER_Z + COVER_HALF_DEPTH,
    ));
    if (hitCoverIndex >= 0) {
      const overflow = damageCover(state, build, hitCoverIndex, projectile.damage);
      if (hitCoverIndex === combat.playerCoverIndex) damageLife(state, combat, overflow);
      projectile.position.z = COVER_Z + COVER_HALF_DEPTH;
      projectile.lifeTicks = 0;
    } else if (segmentHitsPlayer(previousPosition, projectile.position, combat.playerPosition)) {
      damageLife(state, combat, projectile.damage * (1 - build.damageReduction));
      projectile.lifeTicks = 0;
    }
  }
  combat.projectiles = combat.projectiles.filter((projectile) => projectile.lifeTicks > 0);
  combat.enemies = combat.enemies.filter((target) => target.hp > 0);

  if (state.resources.life <= 0) {
    state.completed = true;
    state.victory = false;
    state.mode = "result";
    addEvent(state, "runEnded", { victory: false, nodeId: state.currentNodeId });
    return;
  }
  if (combat.spawnQueue.length === 0 && combat.enemies.length === 0) {
    combat.cleared = true;
    combat.rewardReady = true;
    addEvent(state, "roomCleared", { nodeId: state.currentNodeId, durationTicks: combat.tick, damageTaken: Math.round(combat.damageTaken) });
  }
}

export function moveCover(state: RunState, build: ResolvedCombatBuild, direction: -1 | 1): void {
  const combat = state.combat;
  if (!combat || combat.cleared) return;
  const nextIndex = Math.max(0, Math.min(2, combat.playerCoverIndex + direction));
  if (nextIndex === combat.playerCoverIndex) return;
  const previousPosition = { ...combat.playerPosition };
  combat.playerCoverIndex = nextIndex;
  combat.playerPosition.x = COVER_X[nextIndex] as number;
  combat.ammo = Math.min(build.magazine, combat.ammo + (build.eventAmmo.leaveCover ?? 0));
  pushCombatFeedback(combat, { type: "coverMove", from: previousPosition, to: { ...combat.playerPosition } });
  addEvent(state, "coverMoved", { coverIndex: nextIndex });
}
