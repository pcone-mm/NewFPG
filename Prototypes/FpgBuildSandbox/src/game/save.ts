import { SCHEMA_VERSION, type PlayerRunResources, type RunState } from "./types";

const SAVE_KEY = "fpg-build-sandbox:run";

export function saveRun(state: RunState): void {
  state.lastSavedAt = Date.now();
  if (typeof localStorage === "undefined") return;
  localStorage.setItem(SAVE_KEY, JSON.stringify(state));
}

export function loadRun(): RunState | undefined {
  if (typeof localStorage === "undefined") return undefined;
  const raw = localStorage.getItem(SAVE_KEY);
  if (!raw) return undefined;
  try {
    const parsed = JSON.parse(raw) as Partial<RunState>;
    return migrateSave(parsed);
  } catch {
    return undefined;
  }
}

export function migrateSave(raw: Partial<RunState>): RunState | undefined {
  if (!raw.seed || !raw.floor || !raw.resources || !raw.items || !raw.blessings || !raw.rng || !raw.analytics) return undefined;
  if ((raw.schemaVersion ?? 0) > SCHEMA_VERSION) return undefined;
  const legacyResources = raw.resources as PlayerRunResources & { barrier?: number };
  const { barrier: legacyBarrier, ...resources } = legacyResources;
  // The current prototype tuning uses an 80-point gather threshold. Existing
  // local runs keep their aura but adopt the new threshold on load.
  resources.auraRequired = Math.min(resources.auraRequired ?? 80, 80);
  const coverHealth: [number, number, number] = raw.combat?.coverHealth
    ? [...raw.combat.coverHealth]
    : [100, 100, 100];
  if (!raw.combat?.coverHealth && raw.combat) coverHealth[raw.combat.playerCoverIndex] = legacyBarrier ?? 100;
  return {
    ...raw,
    schemaVersion: SCHEMA_VERSION,
    resources,
    shopOffers: raw.shopOffers ?? [],
    backpackCapacity: raw.backpackCapacity ?? 12,
    nextItemSerial: raw.nextItemSerial ?? raw.items.length,
    combat: raw.combat ? {
      ...raw.combat,
      coverHealth,
      secondaryEnergy: raw.combat.secondaryEnergy ?? 100,
      nextFeedbackSerial: raw.combat.nextFeedbackSerial ?? 0,
      feedbackEvents: [],
      horde: raw.combat.horde ?? { mode: "legacy", endTick: 0, nextSpawnTick: 0, pending: 0, supportSpawned: 0, stopped: false },
      experienceOrbs: raw.combat.experienceOrbs ?? [],
      nextEntitySerial: raw.combat.nextEntitySerial ?? 100000,
      consumableDrops: raw.combat.consumableDrops ?? 0,
      kills: raw.combat.kills ?? 0,
      combo: raw.combat.combo ?? 0,
      lastKillTick: raw.combat.lastKillTick ?? -999,
      lastCollectTick: raw.combat.lastCollectTick ?? -999,
    } : undefined,
    visitedNodeIds: raw.visitedNodeIds ?? [raw.floor.startNodeId],
    completed: raw.completed ?? false,
    lastSavedAt: raw.lastSavedAt ?? Date.now(),
  } as RunState;
}

export function clearSave(): void {
  if (typeof localStorage !== "undefined") localStorage.removeItem(SAVE_KEY);
}
