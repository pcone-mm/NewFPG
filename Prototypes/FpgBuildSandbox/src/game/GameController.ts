import { BLESSINGS, ENCHANTMENTS, SPIRITUAL_ITEMS, enchantmentById, itemById } from "./content";
import { resolveBuild } from "./buildResolver";
import { createCombat, firePrimary, moveCover, pushCombatFeedback, releaseSecondary, tickCombat } from "./combat";
import { completeNode, generateFloor, selectNode } from "./floor";
import { attachEnchantment, canGrantItem, grantItem, rarityPrice, recastItem } from "./inventory";
import { deriveSeed, SeededRng } from "./rng";
import { loadRun, saveRun } from "./save";
import {
  SCHEMA_VERSION,
  type GameAction,
  type GameMode,
  type GameSnapshot,
  type PendingReward,
  type RewardOffer,
  type RoomNode,
  type RunState,
  type RngStreamsState,
} from "./types";

type RngStreamName = keyof RngStreamsState;
type Listener = (snapshot: GameSnapshot) => void;

function freshSeed(): string {
  return `${Date.now().toString(36)}-${Math.floor(Math.random() * 0xffffff).toString(36)}`;
}

function makeStreams(seed: string): RngStreamsState {
  return {
    map: deriveSeed(seed, "map"),
    encounter: deriveSeed(seed, "encounter"),
    reward: deriveSeed(seed, "reward"),
    reroll: deriveSeed(seed, "reroll"),
    boss: deriveSeed(seed, "boss"),
  };
}

export class GameController {
  private state: RunState;
  private build = resolveBuild([], []);
  private listeners = new Set<Listener>();

  public constructor() {
    this.state = this.createState("preview", "title");
  }

  public subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    listener(this.getSnapshot());
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    const snapshot = this.getSnapshot();
    for (const listener of this.listeners) listener(snapshot);
  }

  private createState(seed: string, mode: GameMode): RunState {
    const rng = makeStreams(seed);
    const mapRng = new SeededRng(rng.map);
    const floor = generateFloor(mapRng);
    rng.map = mapRng.state;
    return {
      schemaVersion: SCHEMA_VERSION,
      seed,
      runId: `run-${seed}`,
      mode,
      floor,
      currentNodeId: floor.startNodeId,
      visitedNodeIds: [floor.startNodeId],
      resources: { life: 100, aura: 0, auraRequired: 100, currency: 80, rerolls: 1, consumables: 0 },
      items: [],
      backpackCapacity: 12,
      nextItemSerial: 0,
      blessings: [],
      shopOffers: [],
      rng,
      analytics: [],
      completed: false,
      lastSavedAt: Date.now(),
    };
  }

  private useRng<T>(stream: RngStreamName, action: (rng: SeededRng) => T): T {
    const rng = new SeededRng(this.state.rng[stream]);
    const result = action(rng);
    this.state.rng[stream] = rng.state;
    return result;
  }

  private record(type: string, data: Record<string, string | number | boolean | string[]> = {}): void {
    this.state.analytics.push({ tick: this.state.combat?.tick ?? 0, type, data });
  }

  private refreshBuild(): void {
    this.build = resolveBuild(this.state.items, this.state.blessings);
    this.state.resources.life = Math.min(this.state.resources.life, this.build.lifeMax);
    if (this.state.combat) {
      this.state.combat.ammo = Math.min(this.state.combat.ammo, this.build.magazine);
      this.state.combat.secondaryEnergy = Math.min(this.state.combat.secondaryEnergy, this.build.secondaryEnergyMax);
      for (let index = 0; index < this.state.combat.coverHealth.length; index += 1) {
        this.state.combat.coverHealth[index] = Math.min(this.state.combat.coverHealth[index] ?? 0, this.build.coverMax);
      }
    }
  }

  public startNewRun(seed = freshSeed()): void {
    this.state = this.createState(seed.trim() || freshSeed(), "reward");
    this.build = resolveBuild([], []);
    this.state.pendingReward = this.createReward("opening", "blessing", 3, "map", "reward");
    this.record("runStarted", { seed: this.state.seed });
    saveRun(this.state);
    this.notify();
  }

  public continueRun(): boolean {
    const loaded = loadRun();
    if (!loaded) return false;
    this.state = loaded;
    this.refreshBuild();
    this.notify();
    return true;
  }

  public hasSavedRun(): boolean {
    return Boolean(loadRun());
  }

  public restartSameSeed(): void {
    this.startNewRun(this.state.seed);
  }

  public returnToTitle(): void {
    this.state.mode = "title";
    this.state.modeBeforeOverlay = undefined;
    this.notify();
  }

  public getSnapshot(): GameSnapshot {
    return { state: this.state, build: this.build };
  }

  public canAcceptItem(definitionId: string, level: 1 | 2 | 3 = 1): boolean {
    return canGrantItem(this.state, definitionId, level);
  }

  public tick(): void {
    if (this.state.mode !== "combat" || !this.state.combat) return;
    const stream: RngStreamName = this.state.combat.roomType === "boss" ? "boss" : "encounter";
    this.useRng(stream, (rng) => tickCombat(this.state, this.build, rng));
    if (this.state.completed) saveRun(this.state);
  }

  public dispatchAction(action: GameAction): void {
    const combat = this.state.combat;
    switch (action.type) {
      case "aim":
        if (this.state.mode === "combat" && combat) combat.aim = { x: action.x, z: action.z };
        break;
      case "moveCover":
        if (this.state.mode === "combat") moveCover(this.state, this.build, action.direction);
        break;
      case "primary":
        if (this.state.mode === "combat" && combat) {
          if (combat.ammo <= 0 && action.autoReload) this.startReload(true);
          else this.useRng(combat.roomType === "boss" ? "boss" : "encounter", (rng) => firePrimary(this.state, this.build, rng));
        }
        break;
      case "secondaryStart":
        if (this.state.mode === "combat" && combat && !combat.cleared && combat.reloadTicks === 0 && combat.fireCooldown === 0 && combat.secondaryEnergy >= this.build.secondaryEnergyCost) {
          combat.isCharging = true;
          combat.chargeTicks = 0;
        }
        break;
      case "secondaryRelease":
        if (this.state.mode === "combat") this.useRng(combat?.roomType === "boss" ? "boss" : "encounter", (rng) => releaseSecondary(this.state, this.build, rng));
        break;
      case "reload":
        if (this.state.mode === "combat") this.startReload();
        break;
      case "gather":
        if (this.state.mode === "combat" && this.state.resources.aura >= this.state.resources.auraRequired) {
          this.state.mode = "ritual";
          this.record("ritualStarted", { aura: this.state.resources.aura });
        }
        break;
      case "interact":
        this.interactWithNode();
        return;
      case "attachEnchantment":
        this.attachPendingEnchantment(action.itemInstanceId, Boolean(action.confirmReplace));
        return;
      case "mergeCopy":
        this.useMergeStation(action.itemInstanceId);
        return;
      case "recast":
        this.useRecastStation(action.itemInstanceId);
        return;
      case "leaveFunction":
        this.finishUtilityNode();
        return;
      default:
        action satisfies never;
    }
  }

  private startReload(automatic = false): boolean {
    const combat = this.state.combat;
    if (!combat || combat.reloadTicks > 0 || combat.ammo >= this.build.magazine) return false;
    combat.isCharging = false;
    combat.chargeTicks = 0;
    combat.reloadTicks = this.build.reloadTicks;
    pushCombatFeedback(combat, { type: "reloadStart", to: { ...combat.playerPosition } });
    this.record("reloadStarted", { ammo: combat.ammo, automatic });
    return true;
  }

  private createReward(
    source: PendingReward["source"],
    kind: PendingReward["kind"],
    count: number,
    returnMode: GameMode,
    stream: "reward" | "reroll",
    nodeId?: string,
  ): PendingReward {
    const offers = this.useRng(stream, (rng) => this.generateOffers(kind, count, rng));
    this.record("rewardOffered", { source, kind, offers: offers.map((offer) => offer.definitionId) });
    return { source, kind, offers, returnMode, nodeId };
  }

  private generateOffers(kind: PendingReward["kind"], count: number, rng: SeededRng): RewardOffer[] {
    const ownedBlessings = new Set(this.state.blessings);
    const itemIds = rng.shuffle(SPIRITUAL_ITEMS.map((item) => item.id));
    const blessingIds = rng.shuffle(BLESSINGS.filter((blessing) => !ownedBlessings.has(blessing.id)).map((blessing) => blessing.id));
    const compatibleEnchantments = ENCHANTMENTS.filter((enchantment) => this.state.items.some((owned) => {
      const item = itemById.get(owned.definitionId);
      return item && enchantment.compatibleForms.includes(item.form);
    }));
    const enchantmentIds = rng.shuffle(compatibleEnchantments.map((enchantment) => enchantment.id));
    let choices: Array<{ kind: RewardOffer["kind"]; definitionId: string }> = [];
    if (kind === "item") choices = itemIds.map((definitionId) => ({ kind: "item", definitionId }));
    else if (kind === "blessing") choices = blessingIds.map((definitionId) => ({ kind: "blessing", definitionId }));
    else if (kind === "enchantment") choices = enchantmentIds.map((definitionId) => ({ kind: "enchantment", definitionId }));
    else {
      const mixed = [
        ...itemIds.slice(0, compatibleEnchantments.length > 0 ? 3 : count).map((definitionId) => ({ kind: "item" as const, definitionId })),
        ...enchantmentIds.slice(0, 2).map((definitionId) => ({ kind: "enchantment" as const, definitionId })),
        ...itemIds.slice(3).map((definitionId) => ({ kind: "item" as const, definitionId })),
      ];
      choices = rng.shuffle(mixed);
    }
    return choices.slice(0, count).map((choice, index) => ({ id: `offer-${choice.kind}-${choice.definitionId}-${index}-${rng.state.toString(36)}`, ...choice }));
  }

  public selectReward(offerId: string): boolean {
    if (this.state.mode === "shop") return this.purchaseOffer(offerId);
    if (this.state.mode !== "reward" || !this.state.pendingReward) return false;
    const offer = this.state.pendingReward.offers.find((candidate) => candidate.id === offerId);
    if (!offer) return false;
    if (offer.kind === "item" && !canGrantItem(this.state, offer.definitionId)) {
      this.record("backpackFull", { source: this.state.pendingReward.source, definitionId: offer.definitionId });
      return false;
    }
    this.record("rewardSelected", { source: this.state.pendingReward.source, kind: offer.kind, definitionId: offer.definitionId });
    if (offer.kind === "item") {
      grantItem(this.state, offer.definitionId);
      this.refreshBuild();
      this.finalizeReward();
    } else if (offer.kind === "blessing") {
      if (!this.state.blessings.includes(offer.definitionId)) this.state.blessings.push(offer.definitionId);
      this.refreshBuild();
      this.finalizeReward();
    } else if (offer.kind === "enchantment") {
      this.state.pendingEnchantmentId = offer.definitionId;
      this.state.pendingAttachReturnMode = this.state.pendingReward.returnMode;
      this.state.mode = "enchantTarget";
    }
    this.notify();
    return true;
  }

  private attachPendingEnchantment(itemInstanceId: string, confirmReplace: boolean): void {
    const enchantmentId = this.state.pendingEnchantmentId;
    if (this.state.mode !== "enchantTarget" || !enchantmentId) return;
    const outcome = attachEnchantment(this.state, itemInstanceId, enchantmentId, confirmReplace);
    if (outcome === "confirm") return;
    if (outcome !== "attached") return;
    this.record("enchantmentAttached", { enchantmentId, itemInstanceId, replaced: confirmReplace });
    this.state.pendingEnchantmentId = undefined;
    this.refreshBuild();
    if (this.state.pendingReward) this.finalizeReward();
    else {
      this.state.mode = this.state.pendingAttachReturnMode ?? "shop";
      this.state.pendingAttachReturnMode = undefined;
      saveRun(this.state);
    }
    this.notify();
  }

  private finalizeReward(): void {
    const pending = this.state.pendingReward;
    if (!pending) return;
    this.state.pendingReward = undefined;
    this.state.pendingAttachReturnMode = undefined;
    if (pending.source === "opening") {
      completeNode(this.state.floor, this.state.floor.startNodeId);
      this.state.mode = "map";
    } else if (pending.source === "ritual") this.state.mode = "combat";
    else {
      completeNode(this.state.floor, pending.nodeId ?? this.state.currentNodeId);
      this.state.combat = undefined;
      this.state.mode = "map";
    }
    saveRun(this.state);
  }

  public rerollReward(): boolean {
    const pending = this.state.pendingReward;
    if (this.state.mode !== "reward" || !pending || this.state.resources.rerolls <= 0) return false;
    this.state.resources.rerolls -= 1;
    const replacement = this.createReward(pending.source, pending.kind, pending.offers.length, pending.returnMode, "reroll", pending.nodeId);
    this.state.pendingReward = replacement;
    this.record("rewardRerolled", { source: pending.source });
    this.notify();
    return true;
  }

  public completeRitual(): void {
    if (this.state.mode !== "ritual" || this.state.resources.aura < this.state.resources.auraRequired) return;
    this.state.resources.aura -= this.state.resources.auraRequired;
    this.state.pendingReward = this.createReward("ritual", "mixed", 5, "combat", "reward", this.state.currentNodeId);
    this.state.mode = "reward";
    this.record("ritualCompleted", {});
    this.notify();
  }

  public interactWithNode(nodeId?: string): void {
    if (nodeId) {
      if (this.state.mode !== "map" || this.state.modeBeforeOverlay) return;
      const node = selectNode(this.state.floor, nodeId);
      this.state.currentNodeId = node.id;
      this.state.visitedNodeIds.push(node.id);
      this.record("routeSelected", { nodeId: node.id, roomType: node.type, rewardKind: node.rewardKind });
      this.enterNode(node);
      saveRun(this.state);
      this.notify();
      return;
    }
    if (this.state.mode !== "combat" || !this.state.combat) return;
    if (!this.state.combat.cleared && this.state.combat.spiritWellAvailable) {
      this.state.combat.spiritWellAvailable = false;
      const gained = Math.round(24 * this.build.auraGain);
      this.state.resources.aura = Math.min(this.state.resources.auraRequired, this.state.resources.aura + gained);
      this.record("spiritWell", { gained });
      this.notify();
      return;
    }
    if (!this.state.combat.rewardReady) return;
    const currentNode = this.getCurrentNode();
    if (currentNode.type === "boss") {
      completeNode(this.state.floor, currentNode.id);
      this.state.completed = true;
      this.state.victory = true;
      this.state.mode = "result";
      this.record("runEnded", { victory: true, rooms: this.state.visitedNodeIds.length, damageDealt: Math.round(this.state.combat.damageDealt) });
      saveRun(this.state);
    } else {
      const kind = currentNode.rewardKind === "blessing" ? "blessing" : currentNode.rewardKind === "enchantment" ? "enchantment" : "item";
      const count = kind === "blessing" ? 3 : 5;
      this.state.pendingReward = this.createReward("room", kind, count, "map", "reward", currentNode.id);
      this.state.mode = "reward";
    }
    this.notify();
  }

  private enterNode(node: RoomNode): void {
    if (node.type === "combat" || node.type === "elite" || node.type === "boss") {
      const roomType = node.type === "combat" ? "combat" : node.type;
      const stream: RngStreamName = roomType === "boss" ? "boss" : "encounter";
      this.state.combat = this.useRng(stream, (rng) => createCombat(roomType, rng, this.build));
      this.state.resources.life = Math.min(this.build.lifeMax, this.state.resources.life + 18);
      this.state.mode = "combat";
      return;
    }
    if (node.type === "shop") {
      this.state.shopOffers = this.createShopOffers();
      this.state.mode = "shop";
      return;
    }
    if (node.type === "experience") {
      this.state.resources.aura = this.state.resources.auraRequired;
      this.record("experiencePack", { aura: this.state.resources.aura });
    }
    this.state.mode = "function";
  }

  private createShopOffers(): RewardOffer[] {
    return this.useRng("reward", (rng) => {
      const itemDefinitions = rng.shuffle(SPIRITUAL_ITEMS).slice(0, 3);
      const enchantments = ENCHANTMENTS.filter((enchantment) => this.state.items.some((owned) => {
        const definition = itemById.get(owned.definitionId);
        return definition && enchantment.compatibleForms.includes(definition.form);
      }));
      const enchantment = enchantments.length > 0 ? rng.pick(enchantments) : undefined;
      const offers: RewardOffer[] = itemDefinitions.map((definition, index) => ({ id: `shop-item-${index}-${definition.id}`, kind: "item", definitionId: definition.id, price: rarityPrice(definition.rarity) }));
      if (enchantment) offers.push({ id: `shop-enchantment-${enchantment.id}`, kind: "enchantment", definitionId: enchantment.id, price: 70 });
      else {
        const fallback = itemDefinitions[0]!;
        offers.push({ id: `shop-item-extra-${fallback.id}`, kind: "item", definitionId: fallback.id, price: rarityPrice(fallback.rarity) });
      }
      offers.push({ id: "shop-consumable-reroll", kind: "currency", definitionId: "consumable_reroll", price: 30 });
      return offers;
    });
  }

  private purchaseOffer(offerId: string): boolean {
    const offer = this.state.shopOffers.find((candidate) => candidate.id === offerId);
    if (!offer || offer.sold || (offer.price ?? 0) > this.state.resources.currency) return false;
    if (offer.kind === "item" && !canGrantItem(this.state, offer.definitionId)) {
      this.record("backpackFull", { source: "shop", definitionId: offer.definitionId });
      return false;
    }
    this.state.resources.currency -= offer.price ?? 0;
    offer.sold = true;
    if (offer.kind === "item") {
      grantItem(this.state, offer.definitionId);
      this.refreshBuild();
    } else if (offer.kind === "enchantment") {
      this.state.pendingEnchantmentId = offer.definitionId;
      this.state.pendingAttachReturnMode = "shop";
      this.state.mode = "enchantTarget";
    } else {
      this.state.resources.rerolls += 1;
      this.state.resources.consumables += 1;
    }
    this.record("shopPurchase", { kind: offer.kind, definitionId: offer.definitionId, price: offer.price ?? 0 });
    saveRun(this.state);
    this.notify();
    return true;
  }

  private useMergeStation(itemInstanceId: string): void {
    const node = this.getCurrentNode();
    if (this.state.mode !== "function" || node.type !== "merge") return;
    const source = this.state.items.find((item) => item.instanceId === itemInstanceId);
    if (!source) return;
    if (!canGrantItem(this.state, source.definitionId, source.level)) {
      this.record("backpackFull", { source: "merge", definitionId: source.definitionId });
      return;
    }
    grantItem(this.state, source.definitionId, source.level);
    this.record("mergeStation", { definitionId: source.definitionId, level: source.level });
    this.refreshBuild();
    this.finishUtilityNode();
  }

  private useRecastStation(itemInstanceId: string): void {
    const node = this.getCurrentNode();
    if (this.state.mode !== "function" || node.type !== "recast") return;
    const result = this.useRng("reward", (rng) => recastItem(this.state, itemInstanceId, rng));
    if (!result) return;
    this.record("recast", { itemInstanceId, definitionId: result.definitionId, level: result.level });
    this.refreshBuild();
    this.finishUtilityNode();
  }

  private finishUtilityNode(): void {
    if (this.state.mode !== "shop" && this.state.mode !== "function") return;
    const node = this.getCurrentNode();
    completeNode(this.state.floor, node.id);
    this.state.mode = "map";
    this.state.shopOffers = [];
    saveRun(this.state);
    this.notify();
  }

  private getCurrentNode(): RoomNode {
    const node = this.state.floor.nodes.find((candidate) => candidate.id === this.state.currentNodeId);
    if (!node) throw new Error(`Missing current node: ${this.state.currentNodeId}`);
    return node;
  }

  public pause(panel: "pause" | "build" | "map" = "pause"): void {
    if (["title", "reward", "enchantTarget", "ritual", "result", "shop", "function"].includes(this.state.mode)) return;
    if (!this.state.modeBeforeOverlay) this.state.modeBeforeOverlay = this.state.mode;
    this.state.mode = panel;
    this.notify();
  }

  public resume(): void {
    if (!this.state.modeBeforeOverlay) return;
    this.state.mode = this.state.modeBeforeOverlay;
    this.state.modeBeforeOverlay = undefined;
    this.notify();
  }

  public downloadAnalytics(): string {
    const payload = {
      schemaVersion: SCHEMA_VERSION,
      seed: this.state.seed,
      runId: this.state.runId,
      victory: this.state.victory,
      visitedNodeIds: this.state.visitedNodeIds,
      finalBuild: { items: this.state.items, blessings: this.state.blessings, resolved: this.build },
      events: this.state.analytics,
    };
    return JSON.stringify(payload, null, 2);
  }

  public debugClearCombat(): void {
    if (!this.state.combat) return;
    this.state.combat.spawnQueue = [];
    this.state.combat.enemies = [];
    this.state.combat.projectiles = [];
    this.state.combat.cleared = true;
    this.state.combat.rewardReady = true;
    this.notify();
  }

  public debugFillAura(): void {
    this.state.resources.aura = this.state.resources.auraRequired;
    this.notify();
  }

  public debugDefeatCombat(): void {
    if (this.state.mode !== "combat" || !this.state.combat) return;
    this.state.resources.life = 0;
    this.tick();
    this.notify();
  }
}

export function describeOffer(offer: RewardOffer): { name: string; description: string; meta: string } {
  if (offer.kind === "item") {
    const definition = itemById.get(offer.definitionId);
    return definition ? { name: definition.name, description: definition.description, meta: `${definition.rarity} · ${definition.form}` } : { name: "未知灵物", description: "", meta: "" };
  }
  if (offer.kind === "blessing") {
    const definition = BLESSINGS.find((candidate) => candidate.id === offer.definitionId);
    return definition ? { name: definition.name, description: definition.description, meta: definition.deity } : { name: "未知神眷", description: "", meta: "" };
  }
  if (offer.kind === "enchantment") {
    const definition = enchantmentById.get(offer.definitionId);
    return definition ? { name: definition.name, description: definition.description, meta: definition.compatibleForms.join(" / ") } : { name: "未知灵蕴", description: "", meta: "" };
  }
  return { name: "重投符", description: "增加一次当前奖励重投次数。", meta: "消耗品" };
}
