import { enchantmentById, itemById, SPIRITUAL_ITEMS } from "./content";
import { SeededRng } from "./rng";
import type { OwnedItem, Rarity, RunState } from "./types";

function nextInstanceId(state: RunState): string {
  const id = `spirit-${state.nextItemSerial.toString(36)}`;
  state.nextItemSerial += 1;
  return id;
}

export function canGrantItem(state: RunState, definitionId: string, level: 1 | 2 | 3 = 1): boolean {
  if (state.items.length < state.backpackCapacity) return true;
  if (level >= 3) return false;
  return state.items.filter((item) => item.definitionId === definitionId && item.level === level).length >= 2;
}

export function grantItem(state: RunState, definitionId: string, level: 1 | 2 | 3 = 1, enchantmentId?: string): OwnedItem {
  if (!itemById.has(definitionId)) throw new Error(`Unknown spiritual item: ${definitionId}`);
  if (!canGrantItem(state, definitionId, level)) throw new Error("Backpack is full");
  const owned: OwnedItem = { instanceId: nextInstanceId(state), definitionId, level, enchantmentId };
  state.items.push(owned);
  mergeEligibleItems(state, definitionId, level);
  return owned;
}

export function mergeEligibleItems(state: RunState, definitionId: string, level: 1 | 2 | 3): void {
  if (level >= 3) return;
  const matches = state.items.filter((item) => item.definitionId === definitionId && item.level === level);
  if (matches.length < 3) return;
  const consumed = matches.slice(0, 3);
  const carriedEnchantment = consumed.find((item) => item.enchantmentId)?.enchantmentId;
  const consumedIds = new Set(consumed.map((item) => item.instanceId));
  state.items = state.items.filter((item) => !consumedIds.has(item.instanceId));
  const promoted = grantItem(state, definitionId, (level + 1) as 2 | 3, carriedEnchantment);
  state.analytics.push({ tick: state.combat?.tick ?? 0, type: "merge", data: { definitionId, fromLevel: level, toLevel: promoted.level } });
}

export function attachEnchantment(state: RunState, itemInstanceId: string, enchantmentId: string, confirmReplace = false): "attached" | "confirm" | "incompatible" {
  const item = state.items.find((candidate) => candidate.instanceId === itemInstanceId);
  const itemDefinition = item ? itemById.get(item.definitionId) : undefined;
  const enchantment = enchantmentById.get(enchantmentId);
  if (!item || !itemDefinition || !enchantment || !enchantment.compatibleForms.includes(itemDefinition.form)) return "incompatible";
  if (item.enchantmentId && !confirmReplace) return "confirm";
  item.enchantmentId = enchantmentId;
  return "attached";
}

export function recastItem(state: RunState, itemInstanceId: string, rng: SeededRng): OwnedItem | undefined {
  const item = state.items.find((candidate) => candidate.instanceId === itemInstanceId);
  if (!item) return undefined;
  const definition = itemById.get(item.definitionId);
  if (!definition) return undefined;
  const enchantment = item.enchantmentId ? enchantmentById.get(item.enchantmentId) : undefined;
  const candidates = SPIRITUAL_ITEMS.filter((candidate) =>
    candidate.rarity === definition.rarity
    && candidate.id !== definition.id
    && (!enchantment || enchantment.compatibleForms.includes(candidate.form)),
  );
  if (candidates.length === 0) return undefined;
  item.definitionId = rng.pick(candidates).id;
  return item;
}

export function rarityPrice(rarity: Rarity): number {
  return rarity === "common" ? 55 : rarity === "rare" ? 90 : 140;
}
