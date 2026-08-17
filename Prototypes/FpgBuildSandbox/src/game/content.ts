import rawContent from "./build-content.json";
import type {
  BlessingDefinition,
  EffectSpec,
  EnchantmentDefinition,
  FactionSynergyDefinition,
  FactionTag,
  ItemTypeTag,
  LineageId,
  MythologyTag,
  SpiritualItemDefinition,
  SynergyDefinition,
  TagSynergyDefinition,
} from "./types";

type RawSpiritualItemDefinition = Omit<SpiritualItemDefinition, "factionTags"> & { factionTags?: FactionTag[] };

interface BuildContentPack {
  lineages: Record<LineageId, { name: string; mark: string; color: string; summary: string }>;
  itemTypeMeta: Record<ItemTypeTag, { name: string; mark: string; color: string; summary: string }>;
  mythologyMeta: Record<MythologyTag, { name: string; mark: string; color: string; summary: string }>;
  spiritualItems: RawSpiritualItemDefinition[];
  blessings: BlessingDefinition[];
  enchantments: EnchantmentDefinition[];
  synergies: SynergyDefinition[];
  tagSynergies: TagSynergyDefinition[];
}

interface NormalizedBuildContentPack extends Omit<BuildContentPack, "spiritualItems"> {
  spiritualItems: SpiritualItemDefinition[];
}

const rawPack = rawContent as unknown as BuildContentPack;

function normalizeItem(item: RawSpiritualItemDefinition): SpiritualItemDefinition {
  const legacyFactionTags = [
    ...(item.lineage ? [item.lineage] : []),
    ...(item.mythologyTags ?? []),
  ];
  return {
    ...item,
    factionTags: [...new Set(item.factionTags?.length ? item.factionTags : legacyFactionTags)],
  };
}

const content: NormalizedBuildContentPack = {
  ...rawPack,
  spiritualItems: rawPack.spiritualItems.map(normalizeItem),
};
const allowedHandlers = new Set<EffectSpec["handler"]>(["statAdd", "statMultiply", "eventDamage", "eventCover", "eventAmmo"]);

function validateEffects(owner: string, effects: readonly EffectSpec[]): void {
  for (const effect of effects) {
    if (!allowedHandlers.has(effect.handler)) throw new Error(`Unsupported effect handler in ${owner}: ${String((effect as EffectSpec).handler)}`);
    if (!Number.isFinite(effect.value)) throw new Error(`Non-finite effect value in ${owner}`);
  }
}

function validateContentPack(pack: BuildContentPack): void {
  if (pack.spiritualItems.length !== 16 || pack.blessings.length !== 6 || pack.enchantments.length !== 6 || pack.synergies.length !== 4) {
    throw new Error("The test content pack must contain exactly 16 items, 6 blessings, 6 enchantments, and 4 synergies");
  }
  const ids = new Set<string>();
  const itemTypeIds = new Set(Object.keys(pack.itemTypeMeta));
  const factionIds = new Set([...Object.keys(pack.lineages), ...Object.keys(pack.mythologyMeta)]);
  for (const entry of [...pack.spiritualItems, ...pack.blessings, ...pack.enchantments]) {
    if (ids.has(entry.id)) throw new Error(`Duplicate content id: ${entry.id}`);
    ids.add(entry.id);
    validateEffects(entry.id, entry.effects);
  }
  for (const item of pack.spiritualItems) {
    if (!item.factionTags?.length) throw new Error(`Spiritual item must have at least one faction tag: ${item.id}`);
    for (const tag of item.factionTags) if (!factionIds.has(tag)) throw new Error(`Unknown faction tag on ${item.id}: ${tag}`);
    if (!item.itemTypeTags?.length) throw new Error(`Spiritual item must have at least one item type tag: ${item.id}`);
    for (const tag of item.itemTypeTags) if (!itemTypeIds.has(tag)) throw new Error(`Unknown item type tag on ${item.id}: ${tag}`);
  }
  for (const synergy of pack.synergies) {
    if (synergy.thresholds.length !== 2 || synergy.thresholds[0]?.count !== 2 || synergy.thresholds[1]?.count !== 4) {
      throw new Error(`Synergy thresholds must be 2 and 4: ${synergy.lineage}`);
    }
    for (const threshold of synergy.thresholds) validateEffects(`${synergy.lineage}:${threshold.count}`, threshold.effects);
  }
  for (const synergy of pack.tagSynergies) {
    if (synergy.thresholds.length !== 2 || synergy.thresholds[0]?.count !== 2 || synergy.thresholds[1]?.count !== 4) {
      throw new Error(`Tag synergy thresholds must be 2 and 4: ${synergy.id}`);
    }
    const knownTag = synergy.kind === "itemType" ? itemTypeIds.has(synergy.tag) : factionIds.has(synergy.tag);
    if (!knownTag) throw new Error(`Unknown tag synergy tag: ${synergy.id}`);
    for (const threshold of synergy.thresholds) validateEffects(`${synergy.id}:${threshold.count}`, threshold.effects);
  }
}

validateContentPack(content);

export const LINEAGE_META = content.lineages;
export const ITEM_TYPE_META = content.itemTypeMeta;
export const MYTHOLOGY_META = content.mythologyMeta;
export const FACTION_META = { ...content.lineages, ...content.mythologyMeta } as Record<FactionTag, { name: string; mark: string; color: string; summary: string }>;
export const SPIRITUAL_ITEMS = content.spiritualItems;
export const BLESSINGS = content.blessings;
export const ENCHANTMENTS = content.enchantments;
export const SYNERGIES = content.synergies;
export const TAG_SYNERGIES = content.tagSynergies;
export const FACTION_SYNERGIES: FactionSynergyDefinition[] = [
  ...content.synergies.map((synergy) => ({
    id: `legacy-lineage:${synergy.lineage}`,
    tag: synergy.lineage,
    name: synergy.name,
    description: synergy.description,
    thresholds: synergy.thresholds,
    testOnly: synergy.testOnly,
  })),
  ...content.tagSynergies
    .filter((synergy) => synergy.kind === "mythology")
    .map((synergy) => ({
      id: synergy.id,
      tag: synergy.tag as MythologyTag,
      name: synergy.name,
      description: synergy.description,
      thresholds: synergy.thresholds,
      testOnly: synergy.testOnly,
    })),
];
export const ITEM_TYPE_SYNERGIES = content.tagSynergies.filter((synergy) => synergy.kind === "itemType");

export const itemById = new Map(SPIRITUAL_ITEMS.map((item) => [item.id, item]));
export const blessingById = new Map(BLESSINGS.map((item) => [item.id, item]));
export const enchantmentById = new Map(ENCHANTMENTS.map((item) => [item.id, item]));
