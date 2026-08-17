import { BLESSINGS, ENCHANTMENTS, FACTION_META, FACTION_SYNERGIES, ITEM_TYPE_META, ITEM_TYPE_SYNERGIES, SPIRITUAL_ITEMS, blessingById, enchantmentById, itemById } from "./content";
import type { EffectSpec, FactionTag, ItemTypeTag, OwnedItem, ResolvedCombatBuild } from "./types";

const BASE_BUILD: ResolvedCombatBuild = {
  lifeMax: 100,
  coverMax: 100,
  magazine: 8,
  primaryDamage: 12,
  secondaryDamage: 28,
  secondaryEnergyMax: 100,
  secondaryEnergyCost: 35,
  secondaryEnergyRegen: 8,
  reloadTicks: 78,
  fireCooldownTicks: 12,
  weakpointMultiplier: 1.5,
  coverReduction: 0.35,
  auraGain: 1,
  damageReduction: 0,
  eventDamage: {},
  eventCover: {},
  eventAmmo: {},
  factionCounts: Object.fromEntries(Object.keys(FACTION_META).map((tag) => [tag, 0])) as Record<FactionTag, number>,
  activeFactionSynergies: [],
  itemTypeCounts: Object.fromEntries(Object.keys(ITEM_TYPE_META).map((tag) => [tag, 0])) as Record<ItemTypeTag, number>,
  activeItemTypeSynergies: [],
};

function applyEffects(build: ResolvedCombatBuild, effects: readonly EffectSpec[], scale = 1): void {
  for (const effect of effects) {
    if (effect.handler === "statAdd") {
      const value = effect.value * scale;
      if (effect.stat === "reloadSpeed") build.reloadTicks /= 1 + value;
      else if (effect.stat === "fireRate") build.fireCooldownTicks /= 1 + value;
      else build[effect.stat] += value;
      continue;
    }
    if (effect.handler === "statMultiply") {
      const multiplier = 1 + (effect.value - 1) * scale;
      if (effect.stat === "reloadSpeed") build.reloadTicks /= multiplier;
      else if (effect.stat === "fireRate") build.fireCooldownTicks /= multiplier;
      else build[effect.stat] *= multiplier;
      continue;
    }
    const bucket = effect.handler === "eventDamage" ? build.eventDamage : effect.handler === "eventCover" ? build.eventCover : build.eventAmmo;
    bucket[effect.event] = (bucket[effect.event] ?? 0) + effect.value * scale;
  }
}

export function resolveBuild(items: readonly OwnedItem[], blessingIds: readonly string[]): ResolvedCombatBuild {
  const build: ResolvedCombatBuild = structuredClone(BASE_BUILD);
  const uniqueFactionItems = new Map<FactionTag, Set<string>>();
  const uniqueItemTypeItems = new Map<ItemTypeTag, Set<string>>();

  for (const owned of items) {
    const definition = itemById.get(owned.definitionId);
    if (!definition) continue;
    for (const tag of definition.factionTags) {
      const definitions = uniqueFactionItems.get(tag) ?? new Set<string>();
      definitions.add(definition.id);
      uniqueFactionItems.set(tag, definitions);
    }
    for (const tag of definition.itemTypeTags) {
      const definitions = uniqueItemTypeItems.get(tag) ?? new Set<string>();
      definitions.add(definition.id);
      uniqueItemTypeItems.set(tag, definitions);
    }
    applyEffects(build, definition.effects, owned.level);
    if (owned.enchantmentId) {
      const enchantment = enchantmentById.get(owned.enchantmentId);
      if (enchantment && enchantment.compatibleForms.includes(definition.form)) applyEffects(build, enchantment.effects);
    }
  }

  for (const [tag, definitions] of uniqueFactionItems) build.factionCounts[tag] = definitions.size;
  for (const [tag, definitions] of uniqueItemTypeItems) build.itemTypeCounts[tag] = definitions.size;

  for (const blessingId of new Set(blessingIds)) {
    const blessing = blessingById.get(blessingId);
    if (blessing) applyEffects(build, blessing.effects);
  }

  for (const synergy of FACTION_SYNERGIES) {
    const count = build.factionCounts[synergy.tag];
    for (const threshold of synergy.thresholds) {
      if (count >= threshold.count) {
        applyEffects(build, threshold.effects);
        build.activeFactionSynergies.push(`${synergy.name}·${threshold.label}`);
      }
    }
  }

  for (const synergy of ITEM_TYPE_SYNERGIES) {
    const count = build.itemTypeCounts[synergy.tag as ItemTypeTag];
    for (const threshold of synergy.thresholds) {
      if (count >= threshold.count) {
        applyEffects(build, threshold.effects);
        build.activeItemTypeSynergies.push(`${synergy.name}·${threshold.label}`);
      }
    }
  }

  build.magazine = Math.max(1, Math.round(build.magazine));
  build.reloadTicks = Math.max(24, Math.round(build.reloadTicks));
  build.fireCooldownTicks = Math.max(4, Math.round(build.fireCooldownTicks));
  build.coverReduction = Math.min(0.75, build.coverReduction);
  build.damageReduction = Math.min(0.6, build.damageReduction);
  return build;
}

export function getContentCounts(): { items: number; blessings: number; enchantments: number; synergies: number } {
  return { items: SPIRITUAL_ITEMS.length, blessings: BLESSINGS.length, enchantments: ENCHANTMENTS.length, synergies: FACTION_SYNERGIES.length + ITEM_TYPE_SYNERGIES.length };
}
