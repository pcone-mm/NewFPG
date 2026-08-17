export const SCHEMA_VERSION = 4;

export type Rarity = "common" | "rare" | "mythic";
export type LineageId = "ember" | "veil" | "storm" | "ward";
export type ItemForm = "weapon" | "relic" | "armor" | "charm";
export type ItemTypeTag = "weapon" | "tome" | "mirror" | "ornament" | "instrument" | "ritualVessel" | "herb" | "wineVessel" | "dailyRelic" | "deceasedRelic" | "jianmuFruit" | "jianmuBranch" | "ore" | "mythicArtifact" | "oddity";
export type MythologyTag = "solar" | "earth" | "thunder" | "wind" | "water" | "nether";
/** A single namespace for all build-faction labels. Legacy lineage ids remain valid labels during migration. */
export type FactionTag = LineageId | MythologyTag;
export type TagSynergyKind = "itemType" | "mythology";
export type RoomType = "start" | "combat" | "shop" | "experience" | "merge" | "recast" | "elite" | "boss";
export type RewardKind = "item" | "enchantment" | "blessing" | "currency" | "none";
export type GameMode = "title" | "map" | "combat" | "ritual" | "reward" | "enchantTarget" | "shop" | "function" | "build" | "pause" | "result";
export type EffectStat =
  | "lifeMax"
  | "coverMax"
  | "magazine"
  | "primaryDamage"
  | "secondaryDamage"
  | "reloadSpeed"
  | "fireRate"
  | "weakpointMultiplier"
  | "coverReduction"
  | "auraGain"
  | "damageReduction";
export type EffectEvent = "reload" | "lastShot" | "weakpoint" | "leaveCover" | "coverBreak" | "charge";

export type EffectSpec =
  | { handler: "statAdd"; stat: EffectStat; value: number }
  | { handler: "statMultiply"; stat: EffectStat; value: number }
  | { handler: "eventDamage"; event: EffectEvent; value: number }
  | { handler: "eventCover"; event: EffectEvent; value: number }
  | { handler: "eventAmmo"; event: EffectEvent; value: number };

export interface SpiritualItemDefinition {
  id: string;
  name: string;
  description: string;
  rarity: Rarity;
  form: ItemForm;
  factionTags: FactionTag[];
  /** @deprecated Content migration input. Use factionTags in new content. */
  lineage?: LineageId;
  /** @deprecated Content migration input. Use factionTags in new content. */
  itemTypeTags: ItemTypeTag[];
  mythologyTags?: MythologyTag[];
  keywords: string[];
  effects: EffectSpec[];
  testOnly: true;
}

export interface OwnedItem {
  instanceId: string;
  definitionId: string;
  level: 1 | 2 | 3;
  enchantmentId?: string;
}

export interface BlessingDefinition {
  id: string;
  name: string;
  deity: string;
  description: string;
  effects: EffectSpec[];
  testOnly: true;
}

export interface EnchantmentDefinition {
  id: string;
  name: string;
  description: string;
  compatibleForms: ItemForm[];
  effects: EffectSpec[];
  testOnly: true;
}

export interface SynergyDefinition {
  lineage: LineageId;
  name: string;
  description: string;
  thresholds: Array<{ count: 2 | 4; effects: EffectSpec[]; label: string }>;
  testOnly: true;
}

export interface FactionSynergyDefinition {
  id: string;
  tag: FactionTag;
  name: string;
  description: string;
  thresholds: Array<{ count: 2 | 4; effects: EffectSpec[]; label: string }>;
  testOnly: true;
}

export interface TagSynergyDefinition {
  id: string;
  kind: TagSynergyKind;
  tag: ItemTypeTag | MythologyTag;
  name: string;
  description: string;
  thresholds: Array<{ count: 2 | 4; effects: EffectSpec[]; label: string }>;
  testOnly: true;
}

export interface RewardOffer {
  id: string;
  kind: RewardKind;
  definitionId: string;
  price?: number;
  sold?: boolean;
}

export interface RoomNode {
  id: string;
  column: number;
  row: number;
  type: RoomType;
  rewardKind: RewardKind;
  label: string;
  next: string[];
  status: "locked" | "available" | "current" | "complete";
}

export interface FloorGraph {
  nodes: RoomNode[];
  startNodeId: string;
  bossNodeId: string;
}

export interface RngStreamsState {
  map: number;
  encounter: number;
  reward: number;
  reroll: number;
  boss: number;
}

export interface Vec2 {
  x: number;
  z: number;
}

export interface EnemyState {
  id: string;
  type: "melee" | "ranged" | "summoner" | "minion" | "elite" | "boss";
  position: Vec2;
  hp: number;
  maxHp: number;
  shield: number;
  attackCooldown: number;
  spawnTick: number;
  phase?: 1 | 2 | 3;
  staggerTicks?: number;
}

export interface ProjectileState {
  id: string;
  position: Vec2;
  velocity: Vec2;
  damage: number;
  hostile: boolean;
  lifeTicks: number;
}

export type CombatFeedbackType = "primary" | "secondary" | "coverMove" | "coverHit" | "reloadStart" | "reloadComplete" | "playerHit";

export interface CombatFeedbackEvent {
  id: string;
  tick: number;
  type: CombatFeedbackType;
  from?: Vec2;
  to?: Vec2;
  hit?: boolean;
  weakpoint?: boolean;
  value?: number;
  charge?: number;
}

export interface CombatState {
  tick: number;
  roomType: "combat" | "elite" | "boss";
  roomStartedTick: number;
  wave: number;
  totalWaves: number;
  spawnQueue: Array<EnemyState & { delayTick: number }>;
  enemies: EnemyState[];
  projectiles: ProjectileState[];
  nextFeedbackSerial: number;
  feedbackEvents: CombatFeedbackEvent[];
  playerCoverIndex: number;
  coverHealth: [number, number, number];
  playerPosition: Vec2;
  aim: Vec2;
  ammo: number;
  reloadTicks: number;
  fireCooldown: number;
  secondaryEnergy: number;
  chargeTicks: number;
  isCharging: boolean;
  cleared: boolean;
  rewardReady: boolean;
  damageDealt: number;
  damageTaken: number;
  bossPhaseTicks: [number, number, number];
  spiritWellAvailable: boolean;
}

export interface PlayerRunResources {
  life: number;
  aura: number;
  auraRequired: number;
  currency: number;
  rerolls: number;
  consumables: number;
}

export interface PendingReward {
  source: "opening" | "ritual" | "room";
  kind: "mixed" | "item" | "enchantment" | "blessing";
  offers: RewardOffer[];
  returnMode: GameMode;
  nodeId?: string;
}

export interface AnalyticsEvent {
  tick: number;
  type: string;
  data: Record<string, string | number | boolean | string[]>;
}

export interface RunState {
  schemaVersion: number;
  seed: string;
  runId: string;
  mode: GameMode;
  modeBeforeOverlay?: GameMode;
  floor: FloorGraph;
  currentNodeId: string;
  visitedNodeIds: string[];
  resources: PlayerRunResources;
  items: OwnedItem[];
  backpackCapacity: number;
  nextItemSerial: number;
  blessings: string[];
  pendingEnchantmentId?: string;
  pendingAttachReturnMode?: GameMode;
  pendingReward?: PendingReward;
  shopOffers: RewardOffer[];
  combat?: CombatState;
  rng: RngStreamsState;
  analytics: AnalyticsEvent[];
  completed: boolean;
  victory?: boolean;
  lastSavedAt: number;
}

export interface ResolvedCombatBuild {
  lifeMax: number;
  coverMax: number;
  magazine: number;
  primaryDamage: number;
  secondaryDamage: number;
  secondaryEnergyMax: number;
  secondaryEnergyCost: number;
  secondaryEnergyRegen: number;
  reloadTicks: number;
  fireCooldownTicks: number;
  weakpointMultiplier: number;
  coverReduction: number;
  auraGain: number;
  damageReduction: number;
  eventDamage: Partial<Record<EffectEvent, number>>;
  eventCover: Partial<Record<EffectEvent, number>>;
  eventAmmo: Partial<Record<EffectEvent, number>>;
  factionCounts: Record<FactionTag, number>;
  activeFactionSynergies: string[];
  itemTypeCounts: Record<ItemTypeTag, number>;
  activeItemTypeSynergies: string[];
}

export interface GameSnapshot {
  state: Readonly<RunState>;
  build: Readonly<ResolvedCombatBuild>;
}

export type GameAction =
  | { type: "aim"; x: number; z: number }
  | { type: "moveCover"; direction: -1 | 1 }
  | { type: "primary"; autoReload?: boolean }
  | { type: "secondaryStart" }
  | { type: "secondaryRelease" }
  | { type: "reload" }
  | { type: "gather" }
  | { type: "interact" }
  | { type: "attachEnchantment"; itemInstanceId: string; confirmReplace?: boolean }
  | { type: "mergeCopy"; itemInstanceId: string }
  | { type: "recast"; itemInstanceId: string }
  | { type: "leaveFunction" };
