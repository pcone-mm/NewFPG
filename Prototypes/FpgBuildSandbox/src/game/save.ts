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
      feedbackEvents: raw.combat.feedbackEvents ?? [],
    } : undefined,
    visitedNodeIds: raw.visitedNodeIds ?? [raw.floor.startNodeId],
    completed: raw.completed ?? false,
    lastSavedAt: raw.lastSavedAt ?? Date.now(),
  } as RunState;
}

export function clearSave(): void {
  if (typeof localStorage !== "undefined") localStorage.removeItem(SAVE_KEY);
}
