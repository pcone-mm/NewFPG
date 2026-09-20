import * as THREE from "three";
import type { CombatState, GameSnapshot } from "../game/types";
import { height } from "../game/geometry";
import { SwarmPresentation } from "./SwarmPresentation";
import { CombatEffects } from "./CombatEffects";

function createForestBitmap(): HTMLCanvasElement {
  const canvas = document.createElement("canvas");
  canvas.width = 1280;
  canvas.height = 720;
  const context = canvas.getContext("2d");
  if (!context) throw new Error("2D canvas is unavailable");

  context.fillStyle = "#17221f";
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = "#21352f";
  context.fillRect(0, 185, canvas.width, 535);
  context.fillStyle = "#315047";
  context.beginPath();
  context.moveTo(0, 420);
  context.lineTo(160, 340);
  context.lineTo(330, 390);
  context.lineTo(520, 285);
  context.lineTo(700, 370);
  context.lineTo(900, 300);
  context.lineTo(1080, 360);
  context.lineTo(1280, 260);
  context.lineTo(1280, 720);
  context.lineTo(0, 720);
  context.closePath();
  context.fill();

  context.fillStyle = "rgba(211, 231, 225, 0.09)";
  context.beginPath();
  context.moveTo(470, 0);
  context.lineTo(570, 0);
  context.lineTo(430, 620);
  context.lineTo(250, 620);
  context.closePath();
  context.fill();
  context.beginPath();
  context.moveTo(805, 0);
  context.lineTo(875, 0);
  context.lineTo(1050, 620);
  context.lineTo(900, 620);
  context.closePath();
  context.fill();

  context.fillStyle = "#1c2c27";
  for (const [x, y, radius] of [[110, 170, 105], [270, 112, 125], [520, 145, 150], [760, 95, 138], [1010, 150, 155], [1210, 110, 118]] as const) {
    context.beginPath();
    context.arc(x, y, radius, 0, Math.PI * 2);
    context.fill();
  }

  const trunks = [72, 185, 322, 480, 630, 795, 965, 1128, 1234];
  for (let index = 0; index < trunks.length; index += 1) {
    const x = trunks[index] as number;
    const width = index % 3 === 1 ? 74 : 46;
    context.fillStyle = index % 2 === 0 ? "#14201d" : "#192722";
    context.beginPath();
    context.moveTo(x - width, 720);
    context.lineTo(x - width * 0.72, 0);
    context.lineTo(x + width * 0.44, 0);
    context.lineTo(x + width, 720);
    context.closePath();
    context.fill();
    context.fillStyle = "rgba(79, 142, 121, 0.18)";
    context.fillRect(x - width * 0.48, 35, Math.max(5, width * 0.12), 660);
  }

  context.fillStyle = "rgba(214, 229, 226, 0.12)";
  context.fillRect(0, 355, 1280, 36);
  context.fillStyle = "rgba(214, 229, 226, 0.08)";
  context.fillRect(160, 470, 980, 48);
  context.fillStyle = "rgba(205, 81, 59, 0.78)";
  for (const x of [210, 1040]) {
    context.fillRect(x - 3, 430, 6, 70);
    context.beginPath();
    context.arc(x, 425, 11, 0, Math.PI * 2);
    context.fill();
  }
  context.fillStyle = "rgba(199, 171, 103, 0.36)";
  for (let index = 0; index < 22; index += 1) {
    const x = (index * 173 + 91) % 1280;
    const y = 140 + ((index * 97) % 390);
    context.fillRect(x, y, 2, 2);
  }
  return canvas;
}

function material(color: THREE.ColorRepresentation, emissive: THREE.ColorRepresentation = "#000000"): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({ color, emissive, roughness: 0.62, metalness: 0.14 });
}

interface CoverHealthDisplay {
  group: THREE.Group;
  fill: THREE.Mesh;
  label: THREE.Sprite;
  width: number;
  lastHp: number;
  lastMax: number;
}

function createCoverHealthLabel(): THREE.Sprite {
  const canvas = document.createElement("canvas");
  canvas.width = 384;
  canvas.height = 72;
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    depthTest: false,
    depthWrite: false,
  }));
  sprite.scale.set(2.8, 0.52, 1);
  sprite.renderOrder = 21;
  return sprite;
}

function updateCoverHealthLabel(label: THREE.Sprite, coverIndex: number, hp: number, maxHp: number, ratio: number): void {
  const material = label.material as THREE.SpriteMaterial;
  const canvas = material.map?.image as HTMLCanvasElement | undefined;
  const context = canvas?.getContext("2d");
  if (!canvas || !context) return;
  context.clearRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = "rgba(7, 15, 13, 0.88)";
  context.fillRect(2, 2, canvas.width - 4, canvas.height - 4);
  context.strokeStyle = ratio <= 0.25 ? "#c74f3b" : ratio <= 0.55 ? "#d6ae59" : "#80bdc8";
  context.lineWidth = 3;
  context.strokeRect(2, 2, canvas.width - 4, canvas.height - 4);
  context.fillStyle = "#e7efec";
  context.font = "600 26px 'Microsoft YaHei', sans-serif";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(`掩体 ${coverIndex + 1}  ·  ${Math.ceil(hp)} / ${Math.round(maxHp)}`, canvas.width / 2, canvas.height / 2 + 1);
  if (material.map) material.map.needsUpdate = true;
}


export class GameRenderer {
  private readonly renderer: THREE.WebGLRenderer;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(40, 1, 0.1, 120);
  private readonly player = new THREE.Group();
  private readonly aimMarker: THREE.Mesh;
  private readonly covers: THREE.Group[] = [];
  private readonly coverHealthDisplays: CoverHealthDisplay[] = [];
  private readonly mistStrips: THREE.Mesh[] = [];
  private readonly chargeRing: THREE.Mesh;
  private readonly chargeCore: THREE.Mesh;
  private readonly chargeOrbit: THREE.Mesh;
  private readonly chargeSparks: THREE.Mesh[] = [];
  private readonly chargeLight: THREE.PointLight;
  private readonly reloadRing: THREE.Mesh;
  private readonly raycaster = new THREE.Raycaster();
  private readonly reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  private readonly resizeObserver: ResizeObserver;
  private readonly swarm: SwarmPresentation;
  private readonly pooledEffects: CombatEffects;
  private lastCombat?: CombatState;
  private lastDeathSerial = -1;
  private lastTick = -1;
  private previousPlayerX = 0;
  private lastTickTime = 0;
  private readonly frameTimes: number[] = [];
  private lastFrameTime = 0;

  public constructor(private readonly host: HTMLElement) {
    this.scene.background = new THREE.Color("#101614");
    this.scene.fog = new THREE.Fog("#15201e", 25, 65);
    this.camera.position.set(0, 10, -18);
    this.camera.lookAt(0, 3.2, 10);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.domElement.className = "game-canvas";
    this.renderer.domElement.dataset.renderState = "ready";
    this.renderer.domElement.addEventListener("webglcontextlost", (event) => {
      event.preventDefault();
      this.renderer.domElement.dataset.renderState = "lost";
      window.dispatchEvent(new CustomEvent("fpg:webgl-lost"));
    });
    this.renderer.domElement.addEventListener("webglcontextrestored", () => {
      this.renderer.domElement.dataset.renderState = "ready";
      window.dispatchEvent(new CustomEvent("fpg:webgl-restored"));
    });
    host.appendChild(this.renderer.domElement);

    this.scene.add(new THREE.HemisphereLight("#d7e5e1", "#12201a", 1.55));
    const keyLight = new THREE.DirectionalLight("#dbe7e5", 2.6);
    keyLight.position.set(-7, 15, -5);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.set(1024, 1024);
    keyLight.shadow.camera.left = -18;
    keyLight.shadow.camera.right = 18;
    keyLight.shadow.camera.top = 24;
    keyLight.shadow.camera.bottom = -5;
    this.scene.add(keyLight);
    const redLight = new THREE.PointLight("#c74f3b", 8, 18, 2);
    redLight.position.set(9, 4, 14);
    this.scene.add(redLight);

    const backgroundTexture = new THREE.CanvasTexture(createForestBitmap());
    backgroundTexture.colorSpace = THREE.SRGBColorSpace;
    const background = new THREE.Mesh(new THREE.PlaneGeometry(48, 27), new THREE.MeshBasicMaterial({ map: backgroundTexture, fog: false }));
    background.position.set(0, 8.8, 31);
    background.rotation.y = Math.PI;
    this.scene.add(background);

    const ground = new THREE.Mesh(new THREE.PlaneGeometry(42, 36), material("#25332d"));
    ground.rotation.x = -Math.PI / 2;
    ground.position.set(0, -0.06, 9);
    ground.receiveShadow = true;
    this.scene.add(ground);
    const centralPath = new THREE.Mesh(new THREE.PlaneGeometry(14, 32), material("#35413b"));
    centralPath.rotation.x = -Math.PI / 2;
    centralPath.position.set(0, -0.03, 9);
    centralPath.receiveShadow = true;
    this.scene.add(centralPath);

    for (const x of [-19, -16, 16, 19]) {
      const trunk = new THREE.Mesh(new THREE.CylinderGeometry(1.15, 1.75, 19, 9), material("#1b2521"));
      trunk.position.set(x, 8, 22 + Math.abs(x) * 0.2);
      trunk.rotation.z = x < 0 ? -0.08 : 0.08;
      trunk.castShadow = true;
      this.scene.add(trunk);
    }

    for (const x of [-7.5, 0, 7.5]) {
      const cover = new THREE.Group();
      const base = new THREE.Mesh(new THREE.BoxGeometry(4.2, 1.5, 1.05), material("#59635b"));
      base.position.y = 0.75;
      base.castShadow = true;
      base.receiveShadow = true;
      cover.add(base);
      const cap = new THREE.Mesh(new THREE.BoxGeometry(4.5, 0.22, 1.22), material("#8a7350"));
      cap.position.y = 1.57;
      cover.add(cap);
      const indicator = new THREE.Mesh(
        new THREE.RingGeometry(1.85, 2.05, 36),
        new THREE.MeshBasicMaterial({ color: "#78cbbb", transparent: true, opacity: 0.72, side: THREE.DoubleSide, depthWrite: false }),
      );
      indicator.name = "cover-indicator";
      indicator.rotation.x = -Math.PI / 2;
      indicator.position.y = 0.04;
      indicator.visible = false;
      cover.add(indicator);
      const healthGroup = new THREE.Group();
      healthGroup.name = "cover-health";
      healthGroup.position.set(0, 2.02, -0.64);
      const healthWidth = 3.35;
      const healthBackground = new THREE.Mesh(
        new THREE.BoxGeometry(healthWidth, 0.14, 0.06),
        new THREE.MeshBasicMaterial({ color: "#111a17", transparent: true, opacity: 0.92, depthTest: false, depthWrite: false }),
      );
      healthBackground.renderOrder = 20;
      const healthFill = new THREE.Mesh(
        new THREE.BoxGeometry(healthWidth, 0.14, 0.07),
        new THREE.MeshBasicMaterial({ color: "#80bdc8", depthTest: false, depthWrite: false }),
      );
      healthFill.renderOrder = 21;
      healthFill.position.z = -0.04;
      const healthLabel = createCoverHealthLabel();
      healthLabel.position.set(0, 0.3, -0.08);
      healthGroup.add(healthBackground, healthFill, healthLabel);
      healthGroup.visible = false;
      cover.add(healthGroup);
      cover.position.set(x, 0, 2.5);
      this.scene.add(cover);
      this.covers.push(cover);
      this.coverHealthDisplays.push({ group: healthGroup, fill: healthFill, label: healthLabel, width: healthWidth, lastHp: -1, lastMax: -1 });
    }

    const playerBody = new THREE.Mesh(new THREE.CapsuleGeometry(0.56, 1.1, 5, 10), material("#43a28c", "#0c3b31"));
    playerBody.position.y = 1;
    playerBody.castShadow = true;
    this.player.add(playerBody);
    const playerRing = new THREE.Mesh(new THREE.TorusGeometry(0.82, 0.06, 8, 32), material("#c7ab67", "#54401c"));
    playerRing.rotation.x = Math.PI / 2;
    playerRing.position.y = 0.08;
    this.player.add(playerRing);
    const weapon = new THREE.Group();
    const weaponBody = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.18, 1.25), material("#d5dfdc", "#183a32"));
    weaponBody.position.z = 0.48;
    weaponBody.castShadow = true;
    weapon.add(weaponBody);
    const weaponCore = new THREE.Mesh(new THREE.BoxGeometry(0.32, 0.3, 0.42), material("#b99d61", "#3c3118"));
    weaponCore.position.z = -0.04;
    weapon.add(weaponCore);
    weapon.position.set(0.48, 1.18, 0.18);
    this.player.add(weapon);
    this.player.position.set(0, 0, 1.1);
    this.scene.add(this.player);
    this.swarm = new SwarmPresentation(this.scene);
    this.pooledEffects = new CombatEffects(this.scene);

    this.aimMarker = new THREE.Mesh(
      new THREE.RingGeometry(0.35, 0.47, 24),
      new THREE.MeshBasicMaterial({ color: "#e4eeee", transparent: true, opacity: 0.82, side: THREE.DoubleSide, depthWrite: false }),
    );
    this.aimMarker.rotation.x = -Math.PI / 2;
    this.aimMarker.position.set(0, 0.04, 11);
    this.scene.add(this.aimMarker);

    this.chargeRing = new THREE.Mesh(
      new THREE.RingGeometry(0.72, 0.83, 40),
      new THREE.MeshBasicMaterial({ color: "#d7c16f", transparent: true, opacity: 0.75, side: THREE.DoubleSide, depthWrite: false }),
    );
    this.chargeRing.rotation.x = -Math.PI / 2;
    this.chargeRing.visible = false;
    this.chargeRing.renderOrder = 7;
    this.scene.add(this.chargeRing);

    this.chargeCore = new THREE.Mesh(
      new THREE.SphereGeometry(0.68, 18, 12),
      new THREE.MeshBasicMaterial({ color: "#ffe08a", transparent: true, opacity: 0.45, wireframe: true, depthWrite: false, blending: THREE.AdditiveBlending }),
    );
    this.chargeCore.visible = false;
    this.chargeCore.renderOrder = 12;
    this.scene.add(this.chargeCore);

    this.chargeOrbit = new THREE.Mesh(
      new THREE.TorusGeometry(0.92, 0.055, 8, 48),
      new THREE.MeshBasicMaterial({ color: "#f2ca65", transparent: true, opacity: 0.8, depthWrite: false, blending: THREE.AdditiveBlending }),
    );
    this.chargeOrbit.visible = false;
    this.chargeOrbit.renderOrder = 12;
    this.scene.add(this.chargeOrbit);
    for (let index = 0; index < 6; index += 1) {
      const spark = new THREE.Mesh(
        new THREE.SphereGeometry(index % 2 === 0 ? 0.1 : 0.07, 8, 6),
        new THREE.MeshBasicMaterial({ color: index % 2 === 0 ? "#fff0ae" : "#e9b84e", transparent: true, opacity: 0.9, depthWrite: false, blending: THREE.AdditiveBlending }),
      );
      spark.visible = false;
      spark.renderOrder = 13;
      this.scene.add(spark);
      this.chargeSparks.push(spark);
    }
    this.chargeLight = new THREE.PointLight("#eec45d", 0, 7, 2);
    this.chargeLight.visible = false;
    this.scene.add(this.chargeLight);

    this.reloadRing = new THREE.Mesh(
      new THREE.TorusGeometry(0.88, 0.035, 6, 36, Math.PI * 1.55),
      new THREE.MeshBasicMaterial({ color: "#8ecdc0", transparent: true, opacity: 0.82, depthWrite: false }),
    );
    this.reloadRing.rotation.x = Math.PI / 2;
    this.reloadRing.visible = false;
    this.reloadRing.renderOrder = 7;
    this.scene.add(this.reloadRing);

    for (let index = 0; index < 3; index += 1) {
      const strip = new THREE.Mesh(
        new THREE.PlaneGeometry(32 - index * 5, 0.8 + index * 0.35),
        new THREE.MeshBasicMaterial({ color: "#c8d9d5", transparent: true, opacity: 0.035 + index * 0.015, depthWrite: false }),
      );
      strip.rotation.x = -Math.PI / 2;
      strip.position.set(0, 0.08 + index * 0.03, 8 + index * 5);
      this.scene.add(strip);
      this.mistStrips.push(strip);
    }

    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(host);
    this.resize();
  }


  private resize(): void {
    const width = Math.max(1, this.host.clientWidth), height = Math.max(1, this.host.clientHeight);
    this.camera.aspect = width / height; this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
  }

  public worldToScreen(x: number, z: number, y = 1.2): { x: number; y: number } {
    const rect = this.renderer.domElement.getBoundingClientRect();
    const projected = new THREE.Vector3(x, y, z).project(this.camera);
    return { x: rect.left + (projected.x * 0.5 + 0.5) * rect.width, y: rect.top + (-projected.y * 0.5 + 0.5) * rect.height };
  }

  public screenToWorld(clientX: number, clientY: number, _snapshot?: GameSnapshot): { x: number; y: number; z: number } {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.raycaster.setFromCamera(new THREE.Vector2((clientX - rect.left) / rect.width * 2 - 1, -(clientY - rect.top) / rect.height * 2 + 1), this.camera);
    const ray = this.raycaster.ray;
    const point = new THREE.Vector3();
    // Keep aim on a fixed firing plane. Moving the cursor to an enemy's depth
    // creates implicit target snapping and makes the ray feel sticky.
    ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), -12), point);
    return { x: THREE.MathUtils.clamp(point.x, -16, 16), y: THREE.MathUtils.clamp(point.y, 0.4, 9), z: 12 };
  }

  public diagnostics(): { calls: number; triangles: number; geometries: number; textures: number; medianFrameMs: number; p95FrameMs: number; gpu: string } {
    const samples = [...this.frameTimes].sort((a, b) => a - b);
    const gl = this.renderer.getContext(), extension = gl.getExtension("WEBGL_debug_renderer_info");
    return { calls: this.renderer.info.render.calls, triangles: this.renderer.info.render.triangles,
      geometries: this.renderer.info.memory.geometries, textures: this.renderer.info.memory.textures,
      medianFrameMs: samples[Math.floor(samples.length * 0.5)] ?? 0, p95FrameMs: samples[Math.floor(samples.length * 0.95)] ?? 0,
      gpu: extension ? String(gl.getParameter(extension.UNMASKED_RENDERER_WEBGL)) : String(gl.getParameter(gl.RENDERER)) };
  }

  public render(snapshot: GameSnapshot, elapsedSeconds: number): void {
    if (this.lastFrameTime) { this.frameTimes.push((elapsedSeconds - this.lastFrameTime) * 1000); if (this.frameTimes.length > 300) this.frameTimes.shift(); }
    this.lastFrameTime = elapsedSeconds;
    const c = snapshot.state.combat;
    if (c !== this.lastCombat) {
      this.lastCombat = c; this.lastDeathSerial = -1; this.lastTick = -1;
      this.swarm.reset(); this.pooledEffects.reset();
      this.previousPlayerX = c?.playerPosition.x ?? 0; this.player.position.x = this.previousPlayerX;
    }
    const tick = c?.tick ?? 0, t = tick / 60;
    if (c) {
      if (this.lastTick !== tick) {
        this.previousPlayerX = this.player.position.x; this.lastTick = tick; this.lastTickTime = elapsedSeconds;
      }
      const alpha = snapshot.state.mode === "combat" ? Math.min(1, (elapsedSeconds - this.lastTickTime) * 60 + 0.5) : 1;
      this.player.position.set(THREE.MathUtils.lerp(this.previousPlayerX, c.playerPosition.x, alpha), 0, c.playerPosition.z);
      this.player.rotation.y = Math.atan2(c.aim.x - c.playerPosition.x, c.aim.z - c.playerPosition.z);
      this.aimMarker.position.set(c.aim.x, height(c.aim, 1), c.aim.z);
      this.aimMarker.quaternion.copy(this.camera.quaternion);
      this.pooledEffects.consume(c.feedbackEvents, this.reducedMotion);
      for (const event of c.feedbackEvents) {
        const serial = event.serial ?? Number(event.id.split("-").at(-1));
        if (serial <= this.lastDeathSerial) continue;
        this.lastDeathSerial = serial;
        if (event.type !== "enemyDeath" || !event.to) continue;
        const type = event.enemyType ?? "melee";
        this.swarm.death({ id: event.targetId ?? event.id, type, position: { ...event.to, y: height(event.to) - (type === "boss" ? 1.75 : 1) },
          layer: type === "flyer" ? "air" : "ground", hp: 0, maxHp: 1, shield: 0, attackCooldown: 0, spawnTick: 0 }, event.tick);
      }
      const ratio = Math.min(1, c.chargeTicks / 75), charging = c.isCharging;
      this.chargeRing.visible = charging;
      this.chargeRing.position.set(this.player.position.x, 0.1, this.player.position.z); this.chargeRing.scale.setScalar(0.7 + ratio * 1.25);
      (this.chargeRing.material as THREE.MeshBasicMaterial).opacity = 0.35 + ratio * 0.6;
      this.chargeCore.visible = charging; this.chargeCore.position.set(this.player.position.x, 1.15, this.player.position.z);
      this.chargeCore.scale.setScalar(0.72 + ratio * 0.8); this.chargeCore.rotation.y = this.reducedMotion ? 0 : t * 1.5;
      (this.chargeCore.material as THREE.MeshBasicMaterial).opacity = 0.28 + ratio * 0.62;
      this.chargeOrbit.visible = charging; this.chargeOrbit.position.set(this.player.position.x, 1.12, this.player.position.z);
      this.chargeOrbit.rotation.set(Math.PI / 2 + 0.28, 0, this.reducedMotion ? 0 : -t * 2.4); this.chargeOrbit.scale.setScalar(0.85 + ratio * 0.48);
      this.chargeLight.visible = charging; this.chargeLight.position.set(this.player.position.x, 1.3, this.player.position.z); this.chargeLight.intensity = 2 + ratio * 6;
      for (let i = 0; i < this.chargeSparks.length; i++) {
        const spark = this.chargeSparks[i]!, angle = i / 6 * Math.PI * 2 + (this.reducedMotion ? 0 : t * 3), radius = 0.76 + ratio * 0.62;
        spark.visible = charging; spark.position.set(this.player.position.x + Math.cos(angle) * radius, 0.7 + i % 3 * 0.42, this.player.position.z + Math.sin(angle) * radius);
      }
      this.reloadRing.visible = c.reloadTicks > 0; this.reloadRing.position.set(this.player.position.x, 0.14, this.player.position.z); this.reloadRing.rotation.z = this.reducedMotion ? 0 : -t * 4;
      for (let i = 0; i < this.covers.length; i++) {
        const cover = this.covers[i]!, hp = c.coverHealth[i] ?? 0;
        cover.visible = hp > 0;
        const indicator = cover.getObjectByName("cover-indicator"); if (indicator) indicator.visible = c.playerCoverIndex === i;
        const display = this.coverHealthDisplays[i]!;
        display.group.visible = cover.visible && c.playerCoverIndex === i;
        if (display.group.visible) {
          const maxHp = Math.max(1, snapshot.build.coverMax);
          const ratio = THREE.MathUtils.clamp(hp / maxHp, 0, 1);
          display.fill.scale.x = ratio;
          display.fill.position.x = -display.width * (1 - ratio) * 0.5;
          const fillMaterial = display.fill.material as THREE.MeshBasicMaterial;
          fillMaterial.color.set(ratio <= 0.25 ? "#c74f3b" : ratio <= 0.55 ? "#d6ae59" : "#80bdc8");
          if (display.lastHp !== hp || display.lastMax !== maxHp) {
            updateCoverHealthLabel(display.label, i, hp, maxHp, ratio);
            display.lastHp = hp;
            display.lastMax = maxHp;
          }
        }
      }
    } else {
      for (const object of [this.chargeRing, this.chargeCore, this.chargeOrbit, this.chargeLight, this.reloadRing, ...this.chargeSparks]) object.visible = false;
      for (let i = 0; i < this.covers.length; i++) {
        const cover = this.covers[i]!;
        cover.visible = true;
        cover.getObjectByName("cover-indicator")!.visible = false;
        this.coverHealthDisplays[i]!.group.visible = false;
      }
    }
    this.aimMarker.visible = snapshot.state.mode === "combat";
    this.swarm.render(c, this.reducedMotion, snapshot.state.mode === "combat" ? Math.min(1, (elapsedSeconds - this.lastTickTime) * 60) : 1);
    this.pooledEffects.render(tick, this.reducedMotion);
    for (let i = 0; i < this.mistStrips.length; i++) this.mistStrips[i]!.position.x = this.reducedMotion ? 0 : Math.sin(t * 0.1 + i) * 1.2;
    this.renderer.render(this.scene, this.camera);
  }
}
