import {
  Activity,
  ArrowRight,
  Backpack,
  BatteryMedium,
  ChevronRight,
  CircleDot,
  Coins,
  Combine,
  Crown,
  Clock3,
  Crosshair,
  Download,
  Gem,
  Gauge,
  Heart,
  History,
  Home,
  Map as MapIcon,
  Orbit,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  Shield,
  ShieldCheck,
  ShoppingBag,
  Skull,
  Sparkles,
  Swords,
  Target,
  Volume2,
  VolumeX,
  X,
  Zap,
  createIcons,
} from "lucide";
import { BLESSINGS, FACTION_META, FACTION_SYNERGIES, ITEM_TYPE_META, ITEM_TYPE_SYNERGIES, enchantmentById, itemById } from "../game/content";
import { describeOffer, GameController } from "../game/GameController";
import type { EffectEvent, EffectSpec, EffectStat, FactionTag, GameMode, GameSnapshot, ItemForm, ItemTypeTag, OwnedItem, ResolvedCombatBuild, RewardOffer, RoomNode } from "../game/types";
import { AudioManager } from "./AudioManager";
import { RitualCanvas } from "./RitualCanvas";

const ROOM_NAMES: Record<RoomNode["type"], string> = {
  start: "启程",
  combat: "战斗",
  shop: "商店",
  experience: "灵气",
  merge: "合灵",
  recast: "重铸",
  elite: "精英",
  boss: "首领",
};

const REWARD_NAMES: Record<RoomNode["rewardKind"], string> = {
  item: "灵物",
  enchantment: "灵蕴",
  blessing: "神眷",
  currency: "资源",
  none: "功能",
};

const ITEM_FORM_NAMES: Record<ItemForm, string> = {
  weapon: "武器",
  relic: "器物",
  armor: "护具",
  charm: "佩饰",
};

type ConceptKind = RoomNode["rewardKind"] | "faction" | "mixed";

const CONCEPT_META: Record<ConceptKind, { label: string; icon: string }> = {
  item: { label: "灵物", icon: "gem" },
  enchantment: { label: "灵蕴", icon: "sparkles" },
  blessing: { label: "神眷", icon: "crown" },
  currency: { label: "消耗品", icon: "refresh-cw" },
  none: { label: "功能", icon: "circle-dot" },
  faction: { label: "流派", icon: "crown" },
  mixed: { label: "构筑", icon: "sparkles" },
};

const uiIcons = {
  Activity,
  ArrowRight,
  Backpack,
  BatteryMedium,
  ChevronRight,
  CircleDot,
  Coins,
  Combine,
  Crown,
  Clock3,
  Crosshair,
  Download,
  Gem,
  Gauge,
  Heart,
  History,
  Home,
  Map: MapIcon,
  Orbit,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  Shield,
  ShieldCheck,
  ShoppingBag,
  Skull,
  Sparkles,
  Swords,
  Target,
  Volume2,
  VolumeX,
  X,
  Zap,
};

function escapeHtml(value: string): string {
  return value.replace(/[&<>'"]/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character] ?? character);
}

function iconButton(icon: string, label: string, action: string, extraClass = ""): string {
  return `<button class="icon-button ${extraClass}" type="button" data-action="${action}" aria-label="${label}" title="${label}"><i data-lucide="${icon}"></i></button>`;
}

function conceptLabel(kind: ConceptKind, label = CONCEPT_META[kind].label, compact = false): string {
  const meta = CONCEPT_META[kind];
  return `<span class="concept-label concept-${kind} ${compact ? "compact" : ""} ${label ? "" : "icon-only"}"><i data-lucide="${meta.icon}"></i>${label ? `<span>${escapeHtml(label)}</span>` : ""}</span>`;
}

function conceptSeal(kind: ConceptKind, detail = ""): string {
  const meta = CONCEPT_META[kind];
  return `<span class="concept-seal concept-${kind}"><i data-lucide="${meta.icon}"></i>${detail ? `<small>${escapeHtml(detail)}</small>` : ""}</span>`;
}

function combinationTooltip(
  name: string,
  category: string,
  summary: string,
  count: number,
  thresholds: ReadonlyArray<{ count: 2 | 4; label: string; effects: EffectSpec[] }>,
  extraClass: string,
): string {
  const rows = thresholds.length
    ? thresholds.map((threshold) => `<span class="tooltip-effect-row ${count >= threshold.count ? "active" : ""}"><b>${threshold.count}件 · ${escapeHtml(threshold.label)}</b><em>${escapeHtml(threshold.effects.map(effectSpecText).join("；"))}</em></span>`).join("")
    : `<span class="tooltip-effect-row empty"><b>组合效果</b><em>当前测试内容尚未配置数量组合效果</em></span>`;
  return `<span class="mechanic-tooltip combination-tooltip ${extraClass}" role="tooltip"><strong>${escapeHtml(name)} · ${escapeHtml(category)}</strong><small>${escapeHtml(summary)}</small><span class="tooltip-collection">当前已收集 <b>${count}</b> 件</span><span class="tooltip-effect-list">${rows}</span></span>`;
}

function factionTooltip(tag: FactionTag, build?: ResolvedCombatBuild): string {
  const meta = FACTION_META[tag];
  const count = build?.factionCounts[tag] ?? 0;
  const thresholds = FACTION_SYNERGIES
    .filter((synergy) => synergy.tag === tag)
    .flatMap((synergy) => synergy.thresholds);
  return combinationTooltip(`${meta.name}流派`, "流派标签", meta.summary, count, thresholds, "faction-tooltip");
}

function factionMark(tag: FactionTag, compact = false, build?: ResolvedCombatBuild): string {
  const meta = FACTION_META[tag];
  return `<span class="faction-mark ${compact ? "faction-mark-compact" : ""} mechanic-term" tabindex="0" style="--faction:${meta.color}" aria-label="${escapeHtml(`${meta.name}流派：已收集 ${build?.factionCounts[tag] ?? 0} 件；${meta.summary}`)}"><b>${escapeHtml(meta.mark)}</b>${compact ? "" : `<span>${escapeHtml(meta.name)}</span>`}${factionTooltip(tag, build)}</span>`;
}

function itemTypeTooltip(tag: ItemTypeTag, build?: ResolvedCombatBuild): string {
  const meta = ITEM_TYPE_META[tag];
  const count = build?.itemTypeCounts[tag] ?? 0;
  const thresholds = ITEM_TYPE_SYNERGIES
    .filter((synergy) => synergy.tag === tag)
    .flatMap((synergy) => synergy.thresholds);
  return combinationTooltip(meta.name, "物品类型", meta.summary, count, thresholds, "item-type-tooltip");
}

function tagMark(tag: ItemTypeTag, compact = false, build?: ResolvedCombatBuild): string {
  const meta = ITEM_TYPE_META[tag];
  return `<span class="tag-mark tag-itemType ${compact ? "compact" : ""} mechanic-term" tabindex="0" data-item-type="${tag}" style="--tag-color:${meta.color}" aria-label="${escapeHtml(`${meta.name}，物品类型：${meta.summary}`)}"><b>${escapeHtml(meta.mark)}</b>${compact ? "" : `<span>${escapeHtml(meta.name)}</span>`}${itemTypeTooltip(tag, build)}</span>`;
}

function tagChips(tags: readonly ItemTypeTag[], build?: ResolvedCombatBuild): string {
  return tags.map((tag) => tagMark(tag, true, build)).join("");
}

function rarityName(rarity: string): string {
  return rarity === "mythic" ? "神品" : rarity === "rare" ? "珍品" : "凡品";
}

function rarityClass(rarity: string): string {
  return `rarity-${rarity === "mythic" || rarity === "rare" ? rarity : "common"}`;
}

function itemStars(level: number): string {
  const safeLevel = Math.max(1, Math.min(3, Math.round(level)));
  return `<span class="item-stars" aria-label="${safeLevel}阶">${[1, 2, 3].map((star) => `<span class="${star <= safeLevel ? "filled" : "empty"}">★</span>`).join("")}</span>`;
}

interface CharacterStatMeta {
  key: keyof Pick<ResolvedCombatBuild,
    "lifeMax" | "coverMax" | "magazine" | "primaryDamage" | "secondaryDamage" |
    "secondaryEnergyMax" | "secondaryEnergyCost" | "secondaryEnergyRegen" | "reloadTicks" |
    "fireCooldownTicks" | "weakpointMultiplier" | "coverReduction" | "damageReduction" | "auraGain">;
  label: string;
  icon: string;
  description: string;
  format: (value: number) => string;
}

const numberValue = (value: number): string => Number.isInteger(value) ? String(value) : value.toFixed(1);
const percentValue = (value: number): string => `${Math.round(value * 100)}%`;

const CHARACTER_STATS: CharacterStatMeta[] = [
  { key: "lifeMax", label: "生命上限", icon: "heart", description: "角色可承受的生命伤害上限。当前掩体被摧毁后，溢出的伤害才会扣除生命。", format: numberValue },
  { key: "coverMax", label: "掩体耐久上限", icon: "shield", description: "三个掩体各自拥有的最大耐久。切换掩体时会读取目标掩体自己的当前耐久。", format: numberValue },
  { key: "magazine", label: "弹匣容量", icon: "battery-medium", description: "主武器一次装填的弹药数。弹药耗尽后再次攻击会自动开始换弹。", format: numberValue },
  { key: "primaryDamage", label: "主射伤害", icon: "crosshair", description: "左键每发命中造成的基础伤害，命中弱点后再乘以弱点倍率。", format: numberValue },
  { key: "secondaryDamage", label: "副射伤害", icon: "zap", description: "右键蓄力攻击的基础伤害。蓄力越久，范围与最终伤害越高。", format: numberValue },
  { key: "secondaryEnergyMax", label: "灵能上限", icon: "battery-medium", description: "右键副射使用的蓝色资源上限，未蓄力时会持续恢复。", format: numberValue },
  { key: "secondaryEnergyCost", label: "副射消耗", icon: "zap", description: "每次成功释放右键蓄力攻击所消耗的灵能。灵能不足时无法释放。", format: numberValue },
  { key: "secondaryEnergyRegen", label: "灵能恢复", icon: "activity", description: "每秒自动恢复的灵能数值，蓄力期间暂停恢复。", format: (value) => `${numberValue(value)}/秒` },
  { key: "reloadTicks", label: "换弹时间", icon: "clock-3", description: "从开始换弹到弹匣装满所需时间。换弹期间无法进行主射。", format: (value) => `${(value / 60).toFixed(2)}秒` },
  { key: "fireCooldownTicks", label: "主射频率", icon: "gauge", description: "按住左键时主武器每秒最多发射的次数。", format: (value) => `${(60 / value).toFixed(1)}发/秒` },
  { key: "weakpointMultiplier", label: "弱点倍率", icon: "target", description: "主射命中敌人弱点时，对基础主射伤害使用的倍率。", format: (value) => `${value.toFixed(2)}x` },
  { key: "coverReduction", label: "掩体防护", icon: "shield-check", description: "敌方攻击命中当前掩体时，先按此比例降低掩体承受的伤害。", format: percentValue },
  { key: "damageReduction", label: "全局减伤", icon: "shield-check", description: "敌方伤害进入掩体或生命结算前统一降低的比例。", format: percentValue },
  { key: "auraGain", label: "经验获取倍率", icon: "orbit", description: "击杀敌人与场景交互获得经验时使用的倍率。经验满后可按 G 聚气。", format: (value) => `${value.toFixed(2)}x` },
];

const EVENT_META: Record<EffectEvent, { label: string; description: string }> = {
  reload: { label: "完成换弹", description: "换弹计时结束、弹匣装满时触发。" },
  lastShot: { label: "弹匣末弹", description: "主射消耗弹匣最后一发且命中时触发。" },
  weakpoint: { label: "命中弱点", description: "主射射线命中敌人弱点判定区域时触发。" },
  leaveCover: { label: "切换掩体", description: "角色成功移动到另一个未被摧毁的掩体时触发。" },
  coverBreak: { label: "掩体破坏", description: "当前掩体耐久首次降至 0 时触发。" },
  charge: { label: "释放蓄力", description: "右键蓄力攻击成功消耗灵能并释放时触发。" },
};

const EFFECT_STAT_TERMS: Record<EffectStat, { label: string; description: string }> = {
  lifeMax: { label: "生命上限", description: "角色可承受的生命伤害上限；掩体无法吸收的伤害会扣除生命。" },
  coverMax: { label: "掩体耐久", description: "每个掩体分别保存当前耐久；耐久降至 0 后该掩体会被摧毁。" },
  magazine: { label: "弹匣容量", description: "主武器完成换弹后可持有的最大弹药数。" },
  primaryDamage: { label: "主射伤害", description: "左键主射每发命中的基础伤害，命中弱点后再计算弱点倍率。" },
  secondaryDamage: { label: "副射伤害", description: "右键蓄力攻击释放时使用的基础伤害。" },
  reloadSpeed: { label: "换弹速度", description: "决定主武器完成换弹所需时间；数值越高，换弹时间越短。" },
  fireRate: { label: "主射速度", description: "决定按住左键时两次主射之间的冷却时间。" },
  weakpointMultiplier: { label: "弱点倍率", description: "主射命中敌人弱点时，对主射基础伤害使用的倍率。" },
  coverReduction: { label: "掩体防护", description: "敌方攻击命中当前掩体时，掩体受到的伤害降低比例。" },
  auraGain: { label: "经验获取倍率", description: "击杀敌人和场景交互获得经验时使用的倍率。" },
  damageReduction: { label: "全局减伤", description: "敌方伤害进入掩体或生命结算前统一降低的比例。" },
};

const ACTION_MECHANICS: Record<"追加伤害" | "恢复掩体" | "补充弹药", string> = {
  "追加伤害": "满足触发条件后，在原本伤害之外增加的独立伤害。",
  "恢复掩体": "恢复角色当前所在掩体的耐久，但不会超过掩体耐久上限。",
  "补充弹药": "直接补充当前弹匣中的弹药，但不会超过弹匣容量。",
};

const EFFECT_STAT_NAMES: Record<EffectStat, string> = {
  lifeMax: "生命上限",
  coverMax: "掩体耐久上限",
  magazine: "弹匣容量",
  primaryDamage: "主射伤害",
  secondaryDamage: "副射伤害",
  reloadSpeed: "换弹速度",
  fireRate: "主射速度",
  weakpointMultiplier: "弱点倍率",
  coverReduction: "掩体防护",
  auraGain: "经验获取",
  damageReduction: "全局减伤",
};

function effectSpecText(effect: EffectSpec): string {
  if (effect.handler === "statAdd") {
    const percentStats: EffectStat[] = ["coverReduction", "damageReduction"];
    const value = percentStats.includes(effect.stat)
      ? `+${Math.round(effect.value * 100)}%`
      : effect.stat === "weakpointMultiplier" ? `+${effect.value.toFixed(2)}x` : `+${numberValue(effect.value)}`;
    return `${EFFECT_STAT_NAMES[effect.stat]} ${value}`;
  }
  if (effect.handler === "statMultiply") return `${EFFECT_STAT_NAMES[effect.stat]} x${effect.value.toFixed(2)}`;
  const event = EVENT_META[effect.event].label;
  if (effect.handler === "eventDamage") return `${event}：追加 ${numberValue(effect.value)} 伤害`;
  if (effect.handler === "eventCover") return `${event}：修复 ${numberValue(effect.value)} 掩体耐久`;
  return `${event}：补充 ${numberValue(effect.value)} 发弹药`;
}

function mechanicTerm(label: string, detail: string): string {
  return `<span class="mechanic-term" tabindex="0" aria-label="${escapeHtml(`${label}：${detail}`)}"><span>${escapeHtml(label)}</span><span class="mechanic-tooltip" role="tooltip"><strong>${escapeHtml(label)}</strong><small>${escapeHtml(detail)}</small></span></span>`;
}

function statMechanicTerm(stat: EffectStat): string {
  const meta = EFFECT_STAT_TERMS[stat];
  return mechanicTerm(meta.label, meta.description);
}

function actionMechanicTerm(label: keyof typeof ACTION_MECHANICS): string {
  return mechanicTerm(label, ACTION_MECHANICS[label]);
}

function additiveValue(stat: EffectStat, value: number): string {
  if (["reloadSpeed", "fireRate", "coverReduction", "damageReduction"].includes(stat)) return `+${Math.round(value * 100)}%`;
  if (stat === "weakpointMultiplier" || stat === "auraGain") return `+${value.toFixed(2)}x`;
  return `+${numberValue(value)}`;
}

function statEffectDescription(effect: Extract<EffectSpec, { handler: "statAdd" | "statMultiply" }>): string {
  const term = `“${statMechanicTerm(effect.stat)}”`;
  const value = effect.handler === "statAdd" ? additiveValue(effect.stat, effect.value) : `×${effect.value.toFixed(2)}`;
  if (effect.stat === "coverMax") return `每个掩体的${term}上限 ${value}`;
  if (effect.stat === "lifeMax") return `角色的${term} ${value}`;
  if (effect.stat === "magazine") return `主武器的${term} ${value}`;
  if (effect.stat === "primaryDamage") return `主武器的${term} ${value}`;
  if (effect.stat === "secondaryDamage") return `蓄力攻击的${term} ${value}`;
  return `${term} ${value}`;
}

function effectDescription(effect: EffectSpec): string {
  if (effect.handler === "statAdd" || effect.handler === "statMultiply") return statEffectDescription(effect);
  const event = EVENT_META[effect.event];
  const trigger = `“${mechanicTerm(event.label, event.description)}”时`;
  if (effect.handler === "eventDamage") return `${trigger}，${actionMechanicTerm("追加伤害")} +${numberValue(effect.value)}`;
  if (effect.handler === "eventCover") return `${trigger}，${actionMechanicTerm("恢复掩体")} +${numberValue(effect.value)}`;
  return `${trigger}，${actionMechanicTerm("补充弹药")} +${numberValue(effect.value)} 发`;
}

function effectDescriptionList(effects: readonly EffectSpec[], fallback = "暂无可生效的属性或机制。"): string {
  if (!effects.length) return escapeHtml(fallback);
  return `<span class="generated-description">${effects.map((effect) => `<span>${effectDescription(effect)}。</span>`).join("")}</span>`;
}

export class AppUi {
  private readonly root = document.createElement("div");
  private readonly mobileGuard = document.createElement("section");
  private confirmingItemId?: string;
  private lastSnapshot?: GameSnapshot;
  public currentMode: GameMode = "title";

  public constructor(
    host: HTMLElement,
    private readonly controller: GameController,
    private readonly audio: AudioManager,
  ) {
    this.root.className = "ui-root";
    this.mobileGuard.className = "mobile-guard";
    this.mobileGuard.innerHTML = `<div class="mobile-seal">飞</div><h1>请使用桌面浏览器</h1><p>当前战斗原型最低支持 1280 × 720，移动端暂不提供触屏操作。</p>`;
    host.append(this.root, this.mobileGuard);
    this.root.addEventListener("click", (event) => this.handleClick(event));
    this.root.addEventListener("keydown", (event) => {
      const keyboardEvent = event as KeyboardEvent;
      if (!['Enter', ' '].includes(keyboardEvent.key)) return;
      const focusedMechanic = (keyboardEvent.target as HTMLElement).closest(".mechanic-term");
      if (focusedMechanic?.closest(".offer-card")) return;
      const actionTarget = (keyboardEvent.target as HTMLElement).closest<HTMLElement>("[data-action][role='button']");
      if (!actionTarget || actionTarget.hasAttribute("disabled")) return;
      keyboardEvent.preventDefault();
      actionTarget.click();
    });
    this.root.addEventListener("dragstart", (event) => this.handleDragStart(event as DragEvent));
    this.root.addEventListener("dragover", (event) => this.handleDragOver(event as DragEvent));
    this.root.addEventListener("drop", (event) => this.handleDrop(event as DragEvent));
    this.root.addEventListener("dragend", () => this.clearDragState());
    window.addEventListener("fpg:webgl-lost", () => {
      const warning = document.createElement("div");
      warning.className = "context-warning";
      warning.textContent = "图形上下文已暂停，等待恢复";
      this.root.appendChild(warning);
    });
    window.addEventListener("fpg:webgl-restored", () => this.root.querySelector(".context-warning")?.remove());
  }

  public render(snapshot: GameSnapshot): void {
    this.lastSnapshot = snapshot;
    this.currentMode = snapshot.state.mode;
    const screen = this.screenFor(snapshot);
    const mutedIcon = this.audio.isMuted() ? "volume-x" : "volume-2";
    const hasRunUi = snapshot.state.mode !== "title";
    this.root.classList.toggle("run-active", hasRunUi);
    this.root.innerHTML = `${screen}${hasRunUi ? this.experienceBar(snapshot) : ""}<div class="global-tools">${iconButton(mutedIcon, this.audio.isMuted() ? "开启声音" : "静音", "sound")}</div>`;
    createIcons({ icons: uiIcons, attrs: { "stroke-width": 1.7, width: 18, height: 18 } });
    if (snapshot.state.mode === "ritual") {
      const canvas = this.root.querySelector<HTMLCanvasElement>("#ritual-canvas");
      if (canvas) new RitualCanvas(canvas, () => {
        this.audio.play("ritual");
        this.controller.completeRitual();
      });
    }
  }

  public updateCrosshairPosition(clientX: number, clientY: number): void {
    const indicator = this.root.querySelector<HTMLElement>("[data-reload-crosshair]");
    if (!indicator) return;
    const bounds = this.root.getBoundingClientRect();
    indicator.style.left = `${clientX - bounds.left}px`;
    indicator.style.top = `${clientY - bounds.top}px`;
  }

  public updateHud(snapshot: GameSnapshot): void {
    const experienceRatio = snapshot.state.resources.aura / snapshot.state.resources.auraRequired;
    this.setText("experience-value", `${Math.floor(snapshot.state.resources.aura)} / ${snapshot.state.resources.auraRequired}`);
    this.setMeter("experience-meter", experienceRatio);
    const experienceBar = this.root.querySelector<HTMLElement>("[data-experience-bar]");
    if (experienceBar) experienceBar.classList.toggle("ready", experienceRatio >= 1);
    if (snapshot.state.mode !== "combat") return;
    const { state, build } = snapshot;
    const combat = state.combat;
    if (!combat) return;
    const coverHealth = combat.coverHealth[combat.playerCoverIndex] ?? 0;
    this.setText("hud-cover-label", `掩体 ${combat.playerCoverIndex + 1}`);
    this.setText("hud-life", `${Math.ceil(state.resources.life)} / ${Math.round(build.lifeMax)}`);
    this.setText("hud-cover", coverHealth <= 0 ? `0 / ${Math.round(build.coverMax)} · 已毁` : `${Math.ceil(coverHealth)} / ${Math.round(build.coverMax)}`);
    this.setText("hud-ammo", combat.reloadTicks > 0 ? "换弹中" : `${combat.ammo} / ${build.magazine}`);
    this.setText("hud-energy", `${Math.floor(combat.secondaryEnergy)} / ${Math.round(build.secondaryEnergyMax)}`);
    this.setMeter("life-meter", state.resources.life / build.lifeMax);
    this.setMeter("cover-meter", coverHealth / build.coverMax);
    this.setMeter("energy-meter", combat.secondaryEnergy / build.secondaryEnergyMax);
    const weaponState = combat.reloadTicks > 0 ? "reload" : combat.isCharging ? "charge" : "ready";
    const weaponRatio = weaponState === "reload"
      ? 1 - combat.reloadTicks / Math.max(1, build.reloadTicks)
      : weaponState === "charge" ? combat.chargeTicks / 75 : 0;
    this.setText("hud-weapon-state", weaponState === "reload" ? "换弹" : weaponState === "charge" ? `蓄力 ${Math.min(100, Math.round(weaponRatio * 100))}%` : "武器就绪");
    this.setMeter("weapon-action-meter", weaponRatio);
    const weaponAction = this.root.querySelector<HTMLElement>("[data-weapon-state]");
    if (weaponAction) weaponAction.dataset.weaponState = weaponState;
    this.root.querySelector<HTMLElement>("[data-reload-crosshair]")?.classList.toggle("visible", combat.reloadTicks > 0);
    const objective = this.root.querySelector<HTMLElement>("[data-hud-objective]");
    if (objective) objective.textContent = combat.cleared ? "房间已肃清" : combat.roomType === "boss" ? `压制首领 · 阶段 ${combat.enemies.find((enemy) => enemy.type === "boss")?.phase ?? 1}` : `肃清敌群 · 第 ${combat.wave}/${combat.totalWaves} 波`;
    const prompt = this.root.querySelector<HTMLElement>("[data-hud-prompt]");
    if (prompt) {
      let promptState = "hidden";
      let promptMarkup = "";
      if (combat.rewardReady) {
        promptState = "reward";
        promptMarkup = `<button type="button" class="context-action" data-action="interact"><i data-lucide="sparkles"></i><span>回收房间遗物</span></button>`;
      } else if (state.resources.aura >= state.resources.auraRequired) {
        promptState = "gather";
        promptMarkup = `<button type="button" class="context-action" data-action="gather"><i data-lucide="orbit"></i><span>经验已满 · 按 G 聚气</span></button>`;
      } else if (combat.spiritWellAvailable) {
        promptState = "well";
        promptMarkup = `<button type="button" class="context-action" data-action="interact"><i data-lucide="circle-dot"></i><span>灵脉可吸纳</span></button>`;
      }
      if (prompt.dataset.promptState !== promptState) {
        prompt.dataset.promptState = promptState;
        prompt.innerHTML = promptMarkup;
        prompt.classList.toggle("visible", promptState !== "hidden");
        createIcons({ icons: uiIcons, attrs: { "stroke-width": 1.7, width: 18, height: 18 } });
      }
    }
  }

  private setText(id: string, value: string): void {
    const element = this.root.querySelector<HTMLElement>(`#${id}`);
    if (element) element.textContent = value;
  }

  private setMeter(id: string, ratio: number): void {
    const meter = this.root.querySelector<HTMLElement>(`#${id}`);
    if (meter) meter.style.setProperty("--meter", String(Math.max(0, Math.min(1, ratio))));
  }

  private experienceBar(snapshot: GameSnapshot): string {
    const { aura, auraRequired } = snapshot.state.resources;
    const ratio = Math.max(0, Math.min(1, aura / auraRequired));
    return `<div class="global-experience ${ratio >= 1 ? "ready" : ""}" data-experience-bar data-testid="experience-bar">
      <span class="experience-label"><i data-lucide="orbit"></i><b>经验</b></span>
      <em class="experience-track" id="experience-meter" style="--meter:${ratio}"></em>
      <span class="experience-value" id="experience-value">${Math.floor(aura)} / ${auraRequired}</span>
      <strong class="experience-ready"><kbd>G</kbd> 聚气</strong>
    </div>`;
  }

  private screenFor(snapshot: GameSnapshot): string {
    switch (snapshot.state.mode) {
      case "title": return this.titleScreen(snapshot);
      case "combat": return this.combatHud(snapshot);
      case "map": return this.mapScreen(snapshot);
      case "reward": return this.rewardScreen(snapshot);
      case "enchantTarget": return this.enchantmentTargetScreen(snapshot);
      case "ritual": return this.ritualScreen();
      case "shop": return this.shopScreen(snapshot);
      case "function": return this.functionScreen(snapshot);
      case "build": return this.buildScreen(snapshot);
      case "pause": return this.pauseScreen(snapshot);
      case "result": return this.resultScreen(snapshot);
      default: return "";
    }
  }

  private titleScreen(snapshot: GameSnapshot): string {
    return `<section class="screen screen-title" data-testid="title-screen">
      <div class="title-lockup">
        <span class="eyebrow">非正式测试内容 · 构筑验证</span>
        <h1>飞光录</h1>
        <p>一层构筑沙盒</p>
      </div>
      <div class="title-actions">
        <label class="seed-field"><span>本局种子</span><input id="seed-input" type="text" value="${snapshot.state.seed === "preview" ? "jianmu-001" : escapeHtml(snapshot.state.seed)}" maxlength="40" /></label>
        <button class="command-button primary" type="button" data-action="start"><i data-lucide="play"></i><span>开始新局</span></button>
        ${this.controller.hasSavedRun() ? `<button class="command-button" type="button" data-action="continue"><i data-lucide="history"></i><span>继续本局</span></button>` : ""}
      </div>
    </section>`;
  }

  private combatHud(snapshot: GameSnapshot): string {
    const { state, build } = snapshot;
    const combat = state.combat!;
    const coverHealth = combat.coverHealth[combat.playerCoverIndex] ?? 0;
    return `<section class="combat-ui" data-testid="combat-hud">
      <div class="objective-chip"><span class="eyebrow">${ROOM_NAMES[combat.roomType]}</span><strong data-hud-objective>${combat.roomType === "boss" ? "压制首领 · 阶段 1" : `肃清敌群 · 第 ${combat.wave}/${combat.totalWaves} 波`}</strong></div>
      <div class="resource-strip"><span><i data-lucide="coins"></i>${state.resources.currency}</span><span><i data-lucide="refresh-cw"></i>${state.resources.rerolls}</span><span title="灵物"><i data-lucide="gem"></i>${state.items.length}</span></div>
      <div class="vitals-cluster">
        <div class="vital-row life"><i data-lucide="heart"></i><div><span>生命</span><b id="hud-life">${Math.ceil(state.resources.life)} / ${Math.round(build.lifeMax)}</b><em class="meter" id="life-meter" style="--meter:${state.resources.life / build.lifeMax}"></em></div></div>
        <div class="vital-row cover"><i data-lucide="shield"></i><div><span id="hud-cover-label">掩体 ${combat.playerCoverIndex + 1}</span><b id="hud-cover">${coverHealth <= 0 ? `0 / ${Math.round(build.coverMax)} · 已毁` : `${Math.ceil(coverHealth)} / ${Math.round(build.coverMax)}`}</b><em class="meter" id="cover-meter" style="--meter:${coverHealth / build.coverMax}"></em></div></div>
        <div class="vital-row energy"><i data-lucide="zap"></i><div><span>灵能</span><b id="hud-energy">${Math.floor(combat.secondaryEnergy)} / ${Math.round(build.secondaryEnergyMax)}</b><em class="meter" id="energy-meter" style="--meter:${combat.secondaryEnergy / build.secondaryEnergyMax}"></em></div></div>
        <div class="combat-numbers"><span><i data-lucide="crosshair"></i><small>主武器</small><b id="hud-ammo">${combat.ammo} / ${build.magazine}</b></span></div>
        <div class="weapon-action" data-weapon-state="ready" data-testid="weapon-state"><span id="hud-weapon-state">武器就绪</span><em class="meter" id="weapon-action-meter" style="--meter:0"></em></div>
      </div>
      <nav class="combat-tools" aria-label="局内面板">
        ${iconButton("map", "查看地图", "map")}
        ${iconButton("backpack", "查看构筑", "build")}
        ${iconButton("pause", "暂停", "pause")}
      </nav>
      <div class="hud-prompt" data-hud-prompt></div>
      <div class="reload-crosshair" data-reload-crosshair data-testid="reload-crosshair" aria-label="换弹中"><i data-lucide="refresh-cw"></i></div>
      <div class="build-signals">${[...snapshot.build.activeFactionSynergies, ...snapshot.build.activeItemTypeSynergies].slice(-3).map((name) => `<span>${escapeHtml(name)}</span>`).join("")}</div>
    </section>`;
  }

  private modalHeader(kicker: string, title: string, closeAction?: string, concept?: ConceptKind): string {
    return `<header class="modal-header"><div><span class="eyebrow">${escapeHtml(kicker)}</span><h2>${concept ? conceptLabel(concept, title, true) : escapeHtml(title)}</h2></div>${closeAction ? iconButton("x", "关闭", closeAction) : ""}</header>`;
  }

  private mapScreen(snapshot: GameSnapshot): string {
    const isOverlay = Boolean(snapshot.state.modeBeforeOverlay);
    const nodes = snapshot.state.floor.nodes.map((node) => {
      const enabled = node.status === "available" && !isOverlay;
      return `<button type="button" class="map-node ${node.status} type-${node.type}" style="grid-column:${node.column + 1};grid-row:${node.row + 1}" data-action="route:${node.id}" ${enabled ? "" : "disabled"} data-testid="map-node-${node.id}">
        <span class="node-icon"><i data-lucide="${this.roomIcon(node.type)}"></i></span>
        <strong>${escapeHtml(node.label)}</strong>
        <small><span>${ROOM_NAMES[node.type]}</span>${conceptLabel(node.rewardKind, REWARD_NAMES[node.rewardKind], true)}</small>
      </button>`;
    }).join("");
    return `<section class="screen overlay-screen map-screen" data-testid="map-screen">
      ${this.modalHeader("一层 · 建木下界", "前路已定", isOverlay ? "close" : undefined)}
      <div class="map-legend"><span>房间与奖励在选路前固定</span><b>路线 ${snapshot.state.visitedNodeIds.length}/6</b></div>
      <div class="map-grid"><div class="route-spine"></div>${nodes}</div>
      <footer class="map-footer">种子 <code>${escapeHtml(snapshot.state.seed)}</code></footer>
    </section>`;
  }

  private roomIcon(type: RoomNode["type"]): string {
    return type === "combat" ? "swords" : type === "boss" ? "skull" : type === "elite" ? "crown" : type === "shop" ? "shopping-bag" : type === "experience" ? "orbit" : type === "merge" ? "combine" : type === "recast" ? "refresh-cw" : "sparkles";
  }

  private rewardScreen(snapshot: GameSnapshot): string {
    const reward = snapshot.state.pendingReward!;
    const title = reward.kind === "blessing" ? "诸神垂鉴" : reward.kind === "enchantment" ? "择取灵蕴" : reward.kind === "item" ? "择取灵物" : "聚气所得";
    const concept: ConceptKind = reward.kind === "mixed" ? "mixed" : reward.kind;
    const showsBackpack = reward.offers.some((offer) => offer.kind === "item");
    return `<section class="screen overlay-screen reward-screen" data-testid="reward-screen">
      ${this.modalHeader(reward.source === "opening" ? "本局初始方向" : reward.source === "ritual" ? "聚气完成" : "房间奖励", title, undefined, concept)}
      <div class="reward-layout ${showsBackpack ? "with-backpack" : ""}">
        <div class="offer-grid offer-count-${reward.offers.length}">${reward.offers.map((offer) => this.offerCard(offer, `reward:${offer.id}`, snapshot.build)).join("")}</div>
        ${showsBackpack ? this.backpackDock(snapshot) : ""}
      </div>
      <footer class="reward-footer"><span>${showsBackpack ? "将灵物拖入右侧背包完成领取；点击卡片也可直接放入" : reward.source === "ritual" ? "灵物与灵蕴共同进入候选" : "选择将在确认后立即生效"}</span><button class="command-button" type="button" data-action="reroll" ${snapshot.state.resources.rerolls <= 0 ? "disabled" : ""}><i data-lucide="refresh-cw"></i><span>重投</span><b>${snapshot.state.resources.rerolls}</b></button></footer>
    </section>`;
  }

  private offerCard(offer: RewardOffer, action: string, build: ResolvedCombatBuild, shop = false, disabled = false): string {
    const content = describeOffer(offer);
    const item = offer.kind === "item" ? itemById.get(offer.definitionId) : undefined;
    const blessing = offer.kind === "blessing" ? BLESSINGS.find((candidate) => candidate.id === offer.definitionId) : undefined;
    const enchantment = offer.kind === "enchantment" ? enchantmentById.get(offer.definitionId) : undefined;
    const effects = item?.effects ?? blessing?.effects ?? enchantment?.effects ?? [];
    const kindClass = `kind-${offer.kind}`;
    const rarity = item?.rarity ?? "common";
    const backpackFull = Boolean(item && !this.controller.canAcceptItem(item.id));
    const isDisabled = disabled || Boolean(offer.sold) || backpackFull;
    const canDrag = Boolean(item && action.startsWith("reward:") && !isDisabled);
    const mark = item
      ? `<span class="offer-symbol">${conceptSeal("item")}<span class="offer-faction">${factionMark(item.factionTags[0]!, true, build)}</span></span>`
      : blessing
        ? conceptSeal("blessing", blessing.deity.slice(0, 1))
        : enchantment
          ? conceptSeal("enchantment")
          : conceptSeal("currency");
    const detail = item
      ? `<div class="tag-columns"><span><b>流派</b><span class="mechanic-tags">${item.factionTags.map((tag) => factionMark(tag, false, build)).join("")}</span></span><span><b>类型</b><span class="mechanic-tags">${item.itemTypeTags.map((tag) => tagMark(tag, false, build)).join("")}</span></span></div>`
      : enchantment
        ? `<div class="tag-columns"><span><b>适用</b>${enchantment.compatibleForms.map((form) => ITEM_FORM_NAMES[form]).join(" / ")}</span><span><b>装配</b>选择背包内灵物</span></div>`
        : blessing
          ? `<div class="tag-columns"><span><b>神祇</b>${escapeHtml(blessing.deity)}</span><span><b>作用</b>本局全局生效</span></div>`
          : `<div class="tag-columns"><span><b>作用</b>增加一次重投</span></div>`;
    return `<article class="offer-card ${kindClass} ${item ? rarityClass(rarity) : ""} ${offer.sold ? "sold" : ""} ${backpackFull ? "backpack-full" : ""}" data-action="${action}" role="button" tabindex="${isDisabled ? -1 : 0}" ${isDisabled ? "disabled aria-disabled=\"true\"" : ""} ${canDrag ? `draggable="true" data-drag-offer="${offer.id}"` : ""} data-testid="offer-${offer.kind}">
      <span class="test-ribbon">测试内容</span>
      <div class="offer-mark">${mark}</div>
      <span class="offer-kind">${conceptLabel(offer.kind, offer.kind === "item" ? "灵物" : CONCEPT_META[offer.kind].label, true)}${item ? `<span class="rarity-chip ${rarityClass(rarity)}"><i data-lucide="gem"></i>${rarityName(rarity)}</span>${itemStars(1)}` : ""}</span>
      <h3>${escapeHtml(content.name)}</h3>
      <p class="item-description">${effects.length ? effectDescriptionList(effects) : escapeHtml(content.description)}</p>
      ${detail}
      ${canDrag ? `<span class="drag-instruction"><i data-lucide="backpack"></i>拖入背包</span>` : backpackFull ? `<span class="drag-instruction full"><i data-lucide="backpack"></i>背包已满</span>` : ""}
      ${shop ? `<span class="price"><i data-lucide="coins"></i>${offer.sold ? "已购" : offer.price}</span>` : ""}
    </article>`;
  }

  private backpackDock(snapshot: GameSnapshot): string {
    const isFull = snapshot.state.items.length >= snapshot.state.backpackCapacity;
    return `<aside class="backpack-dock ${isFull ? "is-full" : ""}" data-backpack-drop data-testid="reward-backpack">
      <header><span>${conceptLabel("item", "背包", true)}</span><b>${snapshot.state.items.length}/${snapshot.state.backpackCapacity}</b></header>
      <p>${isFull ? "容量已满；仅允许会立即三合一升阶的同名灵物" : "将选中的灵物拖放至任意空格"}</p>
      ${this.backpackGrid(snapshot, { compact: true })}
    </aside>`;
  }

  private enchantmentTargetScreen(snapshot: GameSnapshot): string {
    const enchantment = snapshot.state.pendingEnchantmentId ? enchantmentById.get(snapshot.state.pendingEnchantmentId) : undefined;
    return `<section class="screen overlay-screen target-screen" data-testid="enchantment-target-screen">
      ${this.modalHeader("在背包中完成灵蕴组装", enchantment?.name ?? "选择承载灵物", undefined, "enchantment")}
      <div class="enchantment-assembly">
        <aside class="pending-enchantment">${conceptSeal("enchantment")}<div><span>待装配灵蕴</span><strong>${escapeHtml(enchantment?.name ?? "未知灵蕴")}</strong><p class="item-description">${effectDescriptionList(enchantment?.effects ?? [], enchantment?.description)}</p></div></aside>
        <section class="assembly-backpack"><header><h3>${conceptLabel("item", "选择背包中的承载灵物")}</h3><b>${snapshot.state.items.length}/${snapshot.state.backpackCapacity}</b></header>${this.backpackGrid(snapshot, { action: "attach", compatibleForms: enchantment?.compatibleForms })}</section>
      </div>
      ${this.confirmingItemId ? `<div class="confirm-strip"><span>此灵物已装配灵蕴，替换原有灵蕴？</span><button class="command-button danger" type="button" data-action="confirm-attach:${this.confirmingItemId}">确认替换</button><button class="text-button" type="button" data-action="cancel-confirm">取消</button></div>` : ""}
    </section>`;
  }

  private shopScreen(snapshot: GameSnapshot): string {
    return `<section class="screen overlay-screen shop-screen" data-testid="shop-screen">
      ${this.modalHeader("行脚商店", "以灵铢换取构筑", undefined)}
      <div class="shop-balance"><i data-lucide="coins"></i><span>持有灵铢</span><b>${snapshot.state.resources.currency}</b></div>
      <div class="offer-grid offer-count-5">${snapshot.state.shopOffers.map((offer) => this.offerCard(offer, `shop:${offer.id}`, snapshot.build, true, (offer.price ?? 0) > snapshot.state.resources.currency)).join("")}</div>
      <footer class="reward-footer"><span>离开后商店关闭</span><button class="command-button primary" type="button" data-action="leave-function"><span>继续前行</span><i data-lucide="arrow-right"></i></button></footer>
    </section>`;
  }

  private functionScreen(snapshot: GameSnapshot): string {
    const node = snapshot.state.floor.nodes.find((candidate) => candidate.id === snapshot.state.currentNodeId)!;
    if (node.type === "experience") {
      return `<section class="screen overlay-screen function-screen" data-testid="function-screen">${this.modalHeader("灵气藏", "灵气已充盈")}
        <div class="function-emblem"><i data-lucide="orbit"></i></div><p>当前灵气槽已经填满，可在下一场战斗中进行聚气。</p>
        <button class="command-button primary" type="button" data-action="leave-function"><span>继续前行</span><i data-lucide="arrow-right"></i></button>
      </section>`;
    }
    const action = node.type === "merge" ? "merge" : "recast";
    const title = node.type === "merge" ? "合灵台" : "重铸台";
    const subtitle = node.type === "merge" ? "为选中灵物增加一份同阶副本" : "替换为同品质灵物，并保留灵阶";
    return `<section class="screen overlay-screen function-screen" data-testid="function-screen">
      ${this.modalHeader("一次性功能节点", title, undefined, "item")}<p class="surface-intro">${subtitle}</p>
      <div class="station-backpack">${snapshot.state.items.length ? this.backpackGrid(snapshot, { action }) : `<div class="empty-state">当前没有可操作的灵物</div>`}</div>
      ${snapshot.state.items.length ? "" : `<button class="command-button" type="button" data-action="leave-function">继续前行</button>`}
    </section>`;
  }

  private backpackGrid(snapshot: GameSnapshot, options: { compact?: boolean; action?: "attach" | "merge" | "recast"; compatibleForms?: ItemForm[] } = {}): string {
    const slots = Array.from({ length: snapshot.state.backpackCapacity }, (_, index) => {
      const owned = snapshot.state.items[index];
      if (!owned) return `<span class="backpack-slot empty" data-backpack-slot="${index}"><small>${String(index + 1).padStart(2, "0")}</small><i data-lucide="gem"></i></span>`;
      return this.backpackItem(owned, options, snapshot.build);
    }).join("");
    return `<div class="backpack-grid ${options.compact ? "compact" : ""}" data-testid="backpack-grid">${slots}</div>`;
  }

  private backpackItem(owned: OwnedItem, options: { compact?: boolean; action?: "attach" | "merge" | "recast"; compatibleForms?: ItemForm[] }, build: ResolvedCombatBuild): string {
    const definition = itemById.get(owned.definitionId)!;
    const enchantment = owned.enchantmentId ? enchantmentById.get(owned.enchantmentId) : undefined;
    const compatible = !options.compatibleForms || options.compatibleForms.includes(definition.form);
    const action = options.action && compatible ? `${options.action}:${owned.instanceId}` : undefined;
    const actionLabel = options.action === "attach" ? (enchantment ? "替换灵蕴" : "装配灵蕴") : options.action === "merge" ? "增加同阶副本" : options.action === "recast" ? "重铸灵物" : "";
    const element = action ? "button" : "article";
    return `<${element} ${action ? `type="button" data-action="${action}" aria-label="${escapeHtml(`${actionLabel}：${definition.name}`)}"` : ""} class="backpack-slot occupied ${rarityClass(definition.rarity)} ${options.compact ? "compact-item" : ""} ${compatible ? "" : "incompatible"}" data-item-id="${owned.instanceId}" title="${escapeHtml(`${definition.name} · ${definition.effects.map(effectSpecText).join("；")}`)}">
      <span class="slot-faction">${factionMark(definition.factionTags[0]!, true, build)}</span>
      <span class="slot-rank">${itemStars(owned.level)}<small class="slot-rarity">${rarityName(definition.rarity)}</small></span>
      <strong>${escapeHtml(definition.name)}</strong>
      ${options.compact ? "" : `<span class="slot-tags">${tagChips(definition.itemTypeTags, build)}${definition.factionTags.map((tag) => factionMark(tag, true, build)).join("")}</span>`}
      ${options.compact ? "" : `<span class="slot-description">${effectDescriptionList(definition.effects)}</span>`}
      ${enchantment ? `<span class="slot-enchantment filled"><i data-lucide="sparkles"></i>${escapeHtml(enchantment.name.replace("试制灵蕴·", ""))}</span>` : ""}
      ${options.action ? `<small class="slot-action">${compatible ? actionLabel : "形态不兼容"}</small>` : ""}
    </${element}>`;
  }

  private buildScreen(snapshot: GameSnapshot): string {
    return `<section class="screen overlay-screen build-screen" data-testid="build-screen">
      ${this.modalHeader("本局构筑 · 模拟已暂停", "角色面板与背包", "close", "mixed")}
      <div class="build-layout">
        <section class="character-panel" data-testid="character-panel">
          <header><h3><i data-lucide="activity"></i>角色最终属性</h3><span>悬停属性查看机制</span></header>
          <div class="character-stat-grid">${CHARACTER_STATS.map((meta) => this.characterStat(snapshot.build, meta)).join("")}</div>
          <h3 class="section-heading"><i data-lucide="sparkles"></i>特殊触发属性</h3>
          <div class="special-stat-grid">${(Object.keys(EVENT_META) as EffectEvent[]).map((event) => this.specialStat(snapshot.build, event)).join("")}</div>
        </section>
        <section class="build-inventory"><header><h3>${conceptLabel("item", "灵物背包")}<span>${snapshot.state.items.length}/${snapshot.state.backpackCapacity}</span></h3><small>每件灵物占用一格；同名同阶三合一后会释放格子</small></header>
          ${this.backpackGrid(snapshot)}
        </section>
        <aside class="build-effects-panel"><h3>${conceptLabel("blessing")}<span>${snapshot.state.blessings.length}</span></h3>
          <div class="blessing-list">${snapshot.state.blessings.map((id) => {
            const blessing = BLESSINGS.find((candidate) => candidate.id === id)!;
            return `<article>${conceptLabel("blessing", "", true)}<div><small>${escapeHtml(blessing.deity)}</small><strong>${escapeHtml(blessing.name)}</strong><p class="item-description">${effectDescriptionList(blessing.effects)}</p></div></article>`;
          }).join("") || `<div class="empty-state compact-empty">尚未获得神眷</div>`}</div>
          ${this.itemTypeSynergySection(snapshot)}
          ${this.factionSynergySection(snapshot)}
        </aside>
      </div>
    </section>`;
  }

  private itemTypeSynergySection(snapshot: GameSnapshot): string {
    const rows = ITEM_TYPE_SYNERGIES.map((synergy) => {
      const count = snapshot.build.itemTypeCounts[synergy.tag as ItemTypeTag];
      const meta = ITEM_TYPE_META[synergy.tag as ItemTypeTag];
      return `<article class="synergy-row tag-synergy-row ${count >= 2 ? "has-synergy" : ""}" style="--lineage:${meta.color}">${tagMark(synergy.tag as ItemTypeTag, false, snapshot.build)}<div class="synergy-track"><span style="--progress:${Math.min(1, count / 4)}"></span><i class="threshold t2 ${count >= 2 ? "active" : ""}">2</i><i class="threshold t4 ${count >= 4 ? "active" : ""}">4</i></div><b>${count}/4</b><p>${escapeHtml(synergy.description)}</p></article>`;
    }).join("");
    return `<section class="tag-synergy-group"><h3><i data-lucide="gem"></i>物品类型羁绊</h3><div class="synergy-panel">${rows}</div></section>`;
  }

  private factionSynergySection(snapshot: GameSnapshot): string {
    const rows = FACTION_SYNERGIES.map((synergy) => {
      const count = snapshot.build.factionCounts[synergy.tag];
      const meta = FACTION_META[synergy.tag];
      return `<article class="synergy-row faction-synergy-row ${count >= 2 ? "has-synergy" : ""}" style="--lineage:${meta.color}">${factionMark(synergy.tag, false, snapshot.build)}<div class="synergy-track"><span style="--progress:${Math.min(1, count / 4)}"></span><i class="threshold t2 ${count >= 2 ? "active" : ""}">2</i><i class="threshold t4 ${count >= 4 ? "active" : ""}">4</i></div><b>${count}/4</b><p>${escapeHtml(synergy.description)}</p></article>`;
    }).join("");
    return `<section class="tag-synergy-group faction-synergy-group"><h3><i data-lucide="crown"></i>流派羁绊 <small>悬停流派徽记查看收集效果</small></h3><div class="synergy-panel">${rows}</div></section>`;
  }

  private characterStat(build: ResolvedCombatBuild, meta: CharacterStatMeta): string {
    const value = build[meta.key];
    return `<article class="character-stat" tabindex="0" aria-label="${escapeHtml(`${meta.label}：${meta.format(value)}。${meta.description}`)}">
      <i data-lucide="${meta.icon}"></i><span>${escapeHtml(meta.label)}</span><b>${escapeHtml(meta.format(value))}</b>
      <span class="mechanic-tooltip stat-tooltip" role="tooltip"><strong>${escapeHtml(meta.label)}</strong><small>${escapeHtml(meta.description)}</small><em>当前最终值 ${escapeHtml(meta.format(value))}</em></span>
    </article>`;
  }

  private specialStat(build: ResolvedCombatBuild, event: EffectEvent): string {
    const effects: string[] = [];
    const damage = build.eventDamage[event] ?? 0;
    const cover = build.eventCover[event] ?? 0;
    const ammo = build.eventAmmo[event] ?? 0;
    if (damage) effects.push(`追加 ${numberValue(damage)} 伤害`);
    if (cover) effects.push(`修复 ${numberValue(cover)} 掩体`);
    if (ammo) effects.push(`补充 ${numberValue(ammo)} 发弹药`);
    const active = effects.length > 0;
    const meta = EVENT_META[event];
    return `<article class="special-stat ${active ? "active" : "inactive"}" tabindex="0" aria-label="${escapeHtml(`${meta.label}：${effects.join("；") || "未激活"}。${meta.description}`)}"><span>${escapeHtml(meta.label)}</span><b>${active ? escapeHtml(effects.join(" · ")) : "未激活"}</b><span class="mechanic-tooltip stat-tooltip" role="tooltip"><strong>${escapeHtml(meta.label)}</strong><small>${escapeHtml(meta.description)}</small><em>${active ? escapeHtml(effects.join("；")) : "当前构筑没有此触发效果"}</em></span></article>`;
  }

  private pauseScreen(snapshot: GameSnapshot): string {
    return `<section class="screen overlay-screen pause-screen" data-testid="pause-screen">
      ${this.modalHeader("模拟已暂停", "歇息片刻")}
      <div class="pause-actions">
        <button class="command-button primary" type="button" data-action="close"><i data-lucide="play"></i><span>返回战斗</span></button>
        <button class="command-button" type="button" data-action="map"><i data-lucide="map"></i><span>查看地图</span></button>
        <button class="command-button" type="button" data-action="build"><i data-lucide="backpack"></i><span>查看构筑</span></button>
        <button class="command-button" type="button" data-action="download"><i data-lucide="download"></i><span>导出数据</span></button>
        <button class="command-button" type="button" data-action="restart"><i data-lucide="rotate-ccw"></i><span>同种子重开</span></button>
        <button class="text-button" type="button" data-action="title"><i data-lucide="home"></i><span>返回标题</span></button>
      </div>
      <div class="pause-summary"><span>种子 <code>${escapeHtml(snapshot.state.seed)}</code></span><span>已走过 ${snapshot.state.visitedNodeIds.length} 个节点</span><span>记录 ${snapshot.state.analytics.length} 条事件</span></div>
    </section>`;
  }

  private ritualScreen(): string {
    return `<section class="screen ritual-screen" data-testid="ritual-screen">
      ${this.modalHeader("战斗已暂停", "一笔聚气")}
      <canvas id="ritual-canvas" data-testid="ritual-canvas" aria-label="五点一笔画仪式"></canvas>
      <p>从第一处灵枢起笔，依次贯通五处灵枢。</p>
    </section>`;
  }

  private resultScreen(snapshot: GameSnapshot): string {
    const combatEvents = snapshot.state.analytics.filter((event) => event.type === "roomCleared");
    const totalTicks = combatEvents.reduce((sum, event) => sum + Number(event.data.durationTicks ?? 0), 0);
    return `<section class="screen overlay-screen result-screen" data-testid="result-screen">
      <span class="result-seal">${snapshot.state.victory ? "胜" : "败"}</span>
      <span class="eyebrow">${snapshot.state.victory ? "一层验证完成" : "本局终止"}</span>
      <h1>${snapshot.state.victory ? "蜃木魇已伏" : "灵光暂熄"}</h1>
      <div class="result-metrics"><span><small>经历节点</small><b>${snapshot.state.visitedNodeIds.length}</b></span><span><small>战斗时间</small><b>${Math.round(totalTicks / 60)}s</b></span><span><small>${conceptLabel("item", "最终灵物", true)}</small><b>${snapshot.state.items.length}</b></span><span><small>${conceptLabel("faction", "激活组合", true)}</small><b>${snapshot.build.activeFactionSynergies.length + snapshot.build.activeItemTypeSynergies.length}</b></span></div>
      <div class="result-actions"><button class="command-button primary" type="button" data-action="restart"><i data-lucide="rotate-ccw"></i><span>同种子重开</span></button><button class="command-button" type="button" data-action="download"><i data-lucide="download"></i><span>下载分析 JSON</span></button><button class="text-button" type="button" data-action="title">返回标题</button></div>
    </section>`;
  }

  private handleClick(event: Event): void {
    const clickedMechanic = (event.target as HTMLElement).closest(".mechanic-term");
    if (clickedMechanic?.closest(".offer-card")) return;
    const target = (event.target as HTMLElement).closest<HTMLElement>("[data-action]");
    if (!target || target.hasAttribute("disabled") || target.getAttribute("aria-disabled") === "true") return;
    const action = target.dataset.action ?? "";
    if (action === "sound") {
      this.audio.toggle();
      if (this.lastSnapshot) this.render(this.lastSnapshot);
      return;
    }
    if (action === "start") {
      const seed = this.root.querySelector<HTMLInputElement>("#seed-input")?.value;
      this.controller.startNewRun(seed);
      return;
    }
    if (action === "continue") { this.controller.continueRun(); return; }
    if (action === "close") { this.controller.resume(); return; }
    if (action === "pause") { this.controller.pause("pause"); return; }
    if (action === "build") {
      if (this.currentMode === "pause") this.controller.resume();
      this.controller.pause("build");
      return;
    }
    if (action === "map") {
      if (this.currentMode === "pause") this.controller.resume();
      this.controller.pause("map");
      return;
    }
    if (action === "gather") { this.controller.dispatchAction({ type: "gather" }); return; }
    if (action === "interact") { this.controller.dispatchAction({ type: "interact" }); return; }
    if (action === "reroll") { if (this.controller.rerollReward()) this.audio.play("reward"); return; }
    if (action === "leave-function") { this.controller.dispatchAction({ type: "leaveFunction" }); return; }
    if (action === "restart") { this.controller.restartSameSeed(); return; }
    if (action === "title") { this.controller.returnToTitle(); return; }
    if (action === "download") { this.downloadAnalytics(); return; }
    if (action === "cancel-confirm") {
      this.confirmingItemId = undefined;
      if (this.lastSnapshot) this.render(this.lastSnapshot);
      return;
    }
    const [kind, id] = action.split(":");
    if (!id) return;
    if (kind === "reward" || kind === "shop") {
      if (this.controller.selectReward(id)) this.audio.play("reward");
    } else if (kind === "route") this.controller.interactWithNode(id);
    else if (kind === "attach") {
      const owned = this.lastSnapshot?.state.items.find((item) => item.instanceId === id);
      if (owned?.enchantmentId) {
        this.confirmingItemId = id;
        if (this.lastSnapshot) this.render(this.lastSnapshot);
      } else this.controller.dispatchAction({ type: "attachEnchantment", itemInstanceId: id });
    } else if (kind === "confirm-attach") {
      this.confirmingItemId = undefined;
      this.controller.dispatchAction({ type: "attachEnchantment", itemInstanceId: id, confirmReplace: true });
    } else if (kind === "merge") this.controller.dispatchAction({ type: "mergeCopy", itemInstanceId: id });
    else if (kind === "recast") this.controller.dispatchAction({ type: "recast", itemInstanceId: id });
  }

  private handleDragStart(event: DragEvent): void {
    const source = (event.target as HTMLElement | null)?.closest<HTMLElement>("[data-drag-offer]");
    const offerId = source?.dataset.dragOffer;
    if (!source || !offerId || source.hasAttribute("disabled") || !event.dataTransfer) {
      event.preventDefault();
      return;
    }
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/x-fpg-offer", offerId);
    source.classList.add("dragging");
    this.root.classList.add("is-dragging-item");
  }

  private handleDragOver(event: DragEvent): void {
    const dock = (event.target as HTMLElement | null)?.closest<HTMLElement>("[data-backpack-drop]");
    if (!dock || !this.root.classList.contains("is-dragging-item")) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = "move";
    dock.classList.add("drag-over");
  }

  private handleDrop(event: DragEvent): void {
    const dock = (event.target as HTMLElement | null)?.closest<HTMLElement>("[data-backpack-drop]");
    if (!dock) return;
    event.preventDefault();
    const offerId = event.dataTransfer?.getData("text/x-fpg-offer");
    this.clearDragState();
    if (offerId && this.controller.selectReward(offerId)) this.audio.play("reward");
    else {
      dock.classList.add("drop-rejected");
      window.setTimeout(() => dock.classList.remove("drop-rejected"), 320);
    }
  }

  private clearDragState(): void {
    this.root.classList.remove("is-dragging-item");
    this.root.querySelectorAll(".dragging, .drag-over").forEach((element) => element.classList.remove("dragging", "drag-over"));
  }

  private downloadAnalytics(): void {
    const data = this.controller.downloadAnalytics();
    const url = URL.createObjectURL(new Blob([data], { type: "application/json" }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `fpg-build-${this.controller.getSnapshot().state.seed}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
