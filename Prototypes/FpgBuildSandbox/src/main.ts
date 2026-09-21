import "./styles.css";
import { GameController } from "./game/GameController";
import { AIM_RAY_ORIGIN } from "./game/combat";
import { GameRenderer } from "./render/GameRenderer";
import { AppUi } from "./ui/AppUi";
import { AudioManager } from "./ui/AudioManager";
import type { CombatState } from "./game/types";

declare global {
  interface Window {
    __FPG_SANDBOX__: {
      controller: GameController;
      getSnapshot: () => ReturnType<GameController["getSnapshot"]>;
      clearCombat: () => void;
      fillAura: () => void;
      defeatCombat: () => void;
      worldToScreen: (x: number, z: number, y?: number) => { x: number; y: number };
      diagnostics: () => ReturnType<GameRenderer["diagnostics"]>;
    };
  }
}

const app = document.querySelector<HTMLElement>("#app");
if (!app) throw new Error("Missing #app host");

const controller = new GameController();
const renderer = new GameRenderer(app);
const audio = new AudioManager();
const ui = new AppUi(app, controller, audio);
controller.subscribe((snapshot) => ui.render(snapshot));

window.__FPG_SANDBOX__ = {
  controller,
  getSnapshot: () => controller.getSnapshot(),
  clearCombat: () => controller.debugClearCombat(),
  fillAura: () => controller.debugFillAura(),
  defeatCombat: () => controller.debugDefeatCombat(),
  worldToScreen: (x, z, y) => renderer.worldToScreen(x, z, y),
  diagnostics: () => renderer.diagnostics(),
};

const fixedStep = 1 / 60;
let lastTime = performance.now() / 1000;
let accumulator = 0;
let primaryHeld = false;
let secondaryHeld = false;
let pointer: { x: number; y: number } | undefined;
let audioCombat: CombatState | undefined;
let audioSerial = -1;

function tryPrimaryFire(autoReload = false): void {
  controller.dispatchAction({ type: "primary", autoReload });
}

function updateAim(clientX: number, clientY: number, snapshot: ReturnType<GameController["getSnapshot"]>): void {
  const point = renderer.screenToWorld(clientX, clientY, snapshot);
  controller.dispatchAction({ type: "aim", ...point, origin: AIM_RAY_ORIGIN });
}

function frame(milliseconds: number): void {
  const now = milliseconds / 1000;
  const frameDelta = Math.min(0.25, now - lastTime);
  lastTime = now;
  accumulator += frameDelta;
  while (accumulator >= fixedStep) {
    controller.tick();
    if (pointer && controller.getSnapshot().state.mode === "combat") {
      updateAim(pointer.x, pointer.y, controller.getSnapshot());
    }
    if (primaryHeld && controller.getSnapshot().state.mode === "combat") tryPrimaryFire(true);
    if (secondaryHeld && controller.getSnapshot().state.mode === "combat") controller.dispatchAction({ type: "secondaryStart" });
    accumulator -= fixedStep;
  }
  const snapshot = controller.getSnapshot();
  if (snapshot.state.mode !== "combat") { primaryHeld = false; secondaryHeld = false; }
  if (audioCombat !== snapshot.state.combat) { audioCombat = snapshot.state.combat; audioSerial = -1; }
  for (const event of audioCombat?.feedbackEvents ?? []) {
    const serial = event.serial ?? Number(event.id.split("-").at(-1));
    if (serial <= audioSerial) continue;
    audioSerial = serial;
    if (event.type === "primary") audio.play("shoot");
    else if (event.type === "enemyDamage" || event.type === "secondary") audio.play("hit");
    else if (event.type === "enemyDeath") audio.play("kill");
    else if (event.type === "experienceCollected") audio.play("collect");
  }
  if (ui.currentMode !== snapshot.state.mode) ui.render(snapshot);
  ui.updateHud(snapshot);
  renderer.render(snapshot, now);
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

function isUiInput(event: Event): boolean {
  const target = event.target as HTMLElement | null;
  return Boolean(target?.closest("button, input, textarea, select"));
}

window.addEventListener("mousemove", (event) => {
  pointer = { x: event.clientX, y: event.clientY };
  const snapshot = controller.getSnapshot();
  if (snapshot.state.mode !== "combat") return;
  ui.updateCrosshairPosition(event.clientX, event.clientY);
  updateAim(event.clientX, event.clientY, snapshot);
});

window.addEventListener("mousedown", (event) => {
  pointer = { x: event.clientX, y: event.clientY };
  const snapshot = controller.getSnapshot();
  if (isUiInput(event) || snapshot.state.mode !== "combat") return;
  ui.updateCrosshairPosition(event.clientX, event.clientY);
  updateAim(event.clientX, event.clientY, snapshot);
  if (event.button === 0) {
    primaryHeld = true;
    tryPrimaryFire(true);
  } else if (event.button === 2) {
    secondaryHeld = true;
    controller.dispatchAction({ type: "secondaryStart" });
  }
});

window.addEventListener("mouseup", (event) => {
  if (event.button === 0) primaryHeld = false;
  if (event.button === 2) {
    secondaryHeld = false;
    if (controller.getSnapshot().state.mode === "combat") controller.dispatchAction({ type: "secondaryRelease" });
  }
});

window.addEventListener("contextmenu", (event) => event.preventDefault());
window.addEventListener("keydown", (event) => {
  if (isUiInput(event) && event.key !== "Escape") return;
  const key = event.key.toLowerCase();
  if (key === "f5") {
    event.preventDefault();
    controller.restartSameSeed();
    return;
  }
  if (key === "escape") {
    event.preventDefault();
    const mode = controller.getSnapshot().state.mode;
    if (["pause", "build"].includes(mode) || (mode === "map" && controller.getSnapshot().state.modeBeforeOverlay)) controller.resume();
    else controller.pause("pause");
    return;
  }
  if (key === "b") { event.preventDefault(); controller.getSnapshot().state.mode === "build" ? controller.resume() : controller.pause("build"); }
  else if (key === "m") { event.preventDefault(); controller.getSnapshot().state.mode === "map" ? controller.resume() : controller.pause("map"); }
  else if (key === "a") controller.dispatchAction({ type: "moveCover", direction: 1 });
  else if (key === "d") controller.dispatchAction({ type: "moveCover", direction: -1 });
  else if (key === "r") controller.dispatchAction({ type: "reload" });
  else if (key === "g") controller.dispatchAction({ type: "gather" });
  else if (key === "e") controller.dispatchAction({ type: "interact" });
});

window.addEventListener("blur", () => {
  primaryHeld = false;
  secondaryHeld = false;
  if (controller.getSnapshot().state.mode === "combat") controller.pause("pause");
});
