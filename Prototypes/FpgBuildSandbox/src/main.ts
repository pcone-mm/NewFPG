import "./styles.css";
import { GameController } from "./game/GameController";
import { GameRenderer } from "./render/GameRenderer";
import { AppUi } from "./ui/AppUi";
import { AudioManager } from "./ui/AudioManager";

declare global {
  interface Window {
    __FPG_SANDBOX__: {
      controller: GameController;
      getSnapshot: () => ReturnType<GameController["getSnapshot"]>;
      clearCombat: () => void;
      fillAura: () => void;
      defeatCombat: () => void;
      worldToScreen: (x: number, z: number, y?: number) => { x: number; y: number };
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
};

const fixedStep = 1 / 60;
let lastTime = performance.now() / 1000;
let accumulator = 0;
let primaryHeld = false;

function tryPrimaryFire(autoReload = false): void {
  const serialBefore = controller.getSnapshot().state.combat?.nextFeedbackSerial;
  controller.dispatchAction({ type: "primary", autoReload });
  const combatAfter = controller.getSnapshot().state.combat;
  const serialAfter = combatAfter?.nextFeedbackSerial;
  if (serialBefore !== undefined && serialAfter !== undefined && serialAfter > serialBefore) audio.play("shoot");
  const feedback = combatAfter?.feedbackEvents.at(-1);
  if (feedback?.type === "primary" && feedback.hit && feedback.id.endsWith(`-${(serialAfter ?? 1) - 1}`)) audio.play("hit");
}

function frame(milliseconds: number): void {
  const now = milliseconds / 1000;
  const frameDelta = Math.min(0.25, now - lastTime);
  lastTime = now;
  accumulator += frameDelta;
  while (accumulator >= fixedStep) {
    controller.tick();
    if (primaryHeld && controller.getSnapshot().state.mode === "combat") tryPrimaryFire();
    accumulator -= fixedStep;
  }
  const snapshot = controller.getSnapshot();
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
  const snapshot = controller.getSnapshot();
  if (snapshot.state.mode !== "combat") return;
  const point = renderer.screenToWorld(event.clientX, event.clientY, snapshot);
  ui.updateCrosshairPosition(event.clientX, event.clientY);
  controller.dispatchAction({ type: "aim", ...point });
});

window.addEventListener("mousedown", (event) => {
  const snapshot = controller.getSnapshot();
  if (isUiInput(event) || snapshot.state.mode !== "combat") return;
  const point = renderer.screenToWorld(event.clientX, event.clientY, snapshot);
  ui.updateCrosshairPosition(event.clientX, event.clientY);
  controller.dispatchAction({ type: "aim", ...point });
  if (event.button === 0) {
    primaryHeld = true;
    tryPrimaryFire(true);
  } else if (event.button === 2) controller.dispatchAction({ type: "secondaryStart" });
});

window.addEventListener("mouseup", (event) => {
  if (event.button === 0) primaryHeld = false;
  if (event.button !== 2 || controller.getSnapshot().state.mode !== "combat") return;
  const energyBefore = controller.getSnapshot().state.combat?.secondaryEnergy;
  controller.dispatchAction({ type: "secondaryRelease" });
  const energyAfter = controller.getSnapshot().state.combat?.secondaryEnergy;
  if (energyBefore !== undefined && energyAfter !== undefined && energyAfter < energyBefore) audio.play("hit");
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
  if (controller.getSnapshot().state.mode === "combat") controller.pause("pause");
});
