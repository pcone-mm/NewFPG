import * as THREE from "three";
import type { CombatFeedbackEvent, EnemyState, GameSnapshot, ProjectileState } from "../game/types";

interface TransientEffect {
  object: THREE.Object3D;
  startedAt: number;
  duration: number;
  update: (progress: number) => void;
}

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

function createEnemyMesh(enemy: EnemyState): THREE.Group {
  const group = new THREE.Group();
  const color = enemy.type === "boss" ? "#cb513b" : enemy.type === "elite" ? "#c7ab67" : enemy.type === "melee" || enemy.type === "minion" ? "#b94d45" : enemy.type === "summoner" ? "#8a6aa7" : "#d5deda";
  let geometry: THREE.BufferGeometry;
  if (enemy.type === "boss") geometry = new THREE.OctahedronGeometry(1.65, 1);
  else if (enemy.type === "summoner") geometry = new THREE.ConeGeometry(0.7, 1.8, 6);
  else if (enemy.type === "ranged") geometry = new THREE.DodecahedronGeometry(0.67, 0);
  else geometry = new THREE.CapsuleGeometry(enemy.type === "elite" ? 0.78 : 0.54, enemy.type === "elite" ? 1.3 : 0.85, 4, 8);
  const body = new THREE.Mesh(geometry, material(color, enemy.type === "boss" ? "#4e1711" : "#07110e"));
  body.castShadow = true;
  body.position.y = enemy.type === "boss" ? 1.75 : 1;
  group.add(body);

  const weakpoint = new THREE.Mesh(new THREE.SphereGeometry(enemy.type === "boss" ? 0.26 : 0.16, 12, 8), material("#f0eee6", "#79c9b6"));
  weakpoint.position.set(0, enemy.type === "boss" ? 2 : 1.35, -0.55);
  group.add(weakpoint);

  const healthWidth = enemy.type === "boss" ? 3.2 : enemy.type === "elite" ? 2 : 1.45;
  const healthY = enemy.type === "boss" ? 3.75 : enemy.type === "elite" ? 2.65 : 2.05;
  const healthBackground = new THREE.Mesh(
    new THREE.PlaneGeometry(healthWidth, 0.11),
    new THREE.MeshBasicMaterial({ color: "#111715", transparent: true, opacity: 0.9, depthTest: false }),
  );
  healthBackground.position.set(0, healthY, 0);
  healthBackground.renderOrder = 8;
  group.add(healthBackground);
  const healthFill = new THREE.Mesh(
    new THREE.PlaneGeometry(healthWidth, 0.065),
    new THREE.MeshBasicMaterial({ color: enemy.type === "boss" ? "#d65b45" : "#6ec2ac", depthTest: false }),
  );
  healthFill.position.set(0, healthY, -0.01);
  healthFill.renderOrder = 9;
  healthFill.name = "health-fill";
  healthFill.userData.width = healthWidth;
  group.add(healthFill);
  if (enemy.type === "boss") {
    const ring = new THREE.Mesh(new THREE.TorusGeometry(2.2, 0.07, 8, 48), material("#c7ab67", "#5f4a22"));
    ring.rotation.x = Math.PI / 2;
    ring.position.y = 1.2;
    ring.name = "phase-ring";
    group.add(ring);
  }
  group.userData.enemyType = enemy.type;
  return group;
}

export class GameRenderer {
  private readonly renderer: THREE.WebGLRenderer;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(38, 1, 0.1, 120);
  private readonly player = new THREE.Group();
  private readonly aimMarker: THREE.Mesh;
  private readonly enemyMeshes = new Map<string, THREE.Group>();
  private readonly projectileMeshes = new Map<string, THREE.Mesh>();
  private readonly covers: THREE.Group[] = [];
  private readonly mistStrips: THREE.Mesh[] = [];
  private readonly chargeRing: THREE.Mesh;
  private readonly chargeCore: THREE.Mesh;
  private readonly chargeOrbit: THREE.Mesh;
  private readonly chargeSparks: THREE.Mesh[] = [];
  private readonly chargeLight: THREE.PointLight;
  private readonly reloadRing: THREE.Mesh;
  private readonly handledFeedbackIds = new Set<string>();
  private readonly transientEffects: TransientEffect[] = [];
  private lastCombatNodeId?: string;
  private readonly raycaster = new THREE.Raycaster();
  private readonly groundPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
  private readonly reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  private readonly resizeObserver: ResizeObserver;

  public constructor(private readonly host: HTMLElement) {
    this.scene.background = new THREE.Color("#101614");
    this.scene.fog = new THREE.Fog("#15201e", 25, 65);
    this.camera.position.set(0, 5.2, -20);
    this.camera.lookAt(0, 3.4, 9.5);

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

    for (const x of [-14, -11, 11, 14]) {
      const trunk = new THREE.Mesh(new THREE.CylinderGeometry(1.15, 1.75, 19, 9), material("#1b2521"));
      trunk.position.set(x, 8, 12 + Math.abs(x) * 0.2);
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
      cover.position.set(x, 0, 2.5);
      this.scene.add(cover);
      this.covers.push(cover);
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
    const width = Math.max(1, this.host.clientWidth);
    const height = Math.max(1, this.host.clientHeight);
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
  }

  public worldToScreen(x: number, z: number, y = 1.2): { x: number; y: number } {
    const rect = this.renderer.domElement.getBoundingClientRect();
    const projected = new THREE.Vector3(x, y, z).project(this.camera);
    return {
      x: rect.left + (projected.x * 0.5 + 0.5) * rect.width,
      y: rect.top + (-projected.y * 0.5 + 0.5) * rect.height,
    };
  }

  public screenToWorld(clientX: number, clientY: number, snapshot?: GameSnapshot): { x: number; z: number } {
    const rect = this.renderer.domElement.getBoundingClientRect();
    const combat = snapshot?.state.mode === "combat" ? snapshot.state.combat : undefined;
    if (combat) {
      let nearest: { enemy: EnemyState; dx: number; dy: number; distance: number } | undefined;
      for (const enemy of combat.enemies) {
        if (enemy.hp <= 0) continue;
        const screen = this.worldToScreen(enemy.position.x, enemy.position.z, enemy.type === "boss" ? 1.9 : 1.15);
        const dx = clientX - screen.x;
        const dy = clientY - screen.y;
        const distance = Math.hypot(dx, dy);
        if (distance <= 76 && (!nearest || distance < nearest.distance)) nearest = { enemy, dx, dy, distance };
      }
      if (nearest) {
        return {
          x: nearest.enemy.position.x + nearest.dx * 0.022,
          z: nearest.enemy.position.z + nearest.dy * 0.022,
        };
      }
    }
    const pointer = new THREE.Vector2(((clientX - rect.left) / rect.width) * 2 - 1, -(((clientY - rect.top) / rect.height) * 2 - 1));
    this.raycaster.setFromCamera(pointer, this.camera);
    const target = new THREE.Vector3();
    if (this.raycaster.ray.intersectPlane(this.groundPlane, target)) return { x: THREE.MathUtils.clamp(target.x, -12, 12), z: THREE.MathUtils.clamp(target.z, 3, 18) };
    return { x: 0, z: 11 };
  }

  private addTransient(object: THREE.Object3D, startedAt: number, duration: number, update: (progress: number) => void): void {
    this.scene.add(object);
    this.transientEffects.push({ object, startedAt, duration, update });
  }

  private createLine(from: THREE.Vector3, to: THREE.Vector3, color: THREE.ColorRepresentation, opacity: number): THREE.Line {
    return new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([from, to]),
      new THREE.LineBasicMaterial({ color, transparent: true, opacity, depthWrite: false }),
    );
  }

  private createBeam(from: THREE.Vector3, to: THREE.Vector3, color: THREE.ColorRepresentation): THREE.Mesh {
    const direction = to.clone().sub(from);
    const beam = new THREE.Mesh(
      new THREE.CylinderGeometry(0.1, 0.24, direction.length(), 12, 1, true),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, depthWrite: false, blending: THREE.AdditiveBlending }),
    );
    beam.position.copy(from).add(to).multiplyScalar(0.5);
    beam.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize());
    beam.renderOrder = 11;
    return beam;
  }

  private createGroundPulse(position: { x: number; z: number }, color: THREE.ColorRepresentation): THREE.Mesh {
    const pulse = new THREE.Mesh(
      new THREE.RingGeometry(0.2, 0.29, 28),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, side: THREE.DoubleSide, depthWrite: false }),
    );
    pulse.rotation.x = -Math.PI / 2;
    pulse.position.set(position.x, 0.09, position.z);
    pulse.renderOrder = 10;
    return pulse;
  }

  private createImpactBurst(position: { x: number; z: number }, weakpoint: boolean, elapsedSeconds: number): void {
    const color = weakpoint ? "#ffe792" : "#a6f0df";
    const burst = new THREE.Group();
    burst.position.set(position.x, weakpoint ? 1.42 : 1.08, position.z - 0.08);
    burst.renderOrder = 14;

    const core = new THREE.Mesh(
      new THREE.OctahedronGeometry(weakpoint ? 0.25 : 0.19, 0),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 1, depthWrite: false, depthTest: false, blending: THREE.AdditiveBlending }),
    );
    core.renderOrder = 14;
    burst.add(core);

    for (let index = 0; index < 2; index += 1) {
      const ring = new THREE.Mesh(
        new THREE.TorusGeometry(weakpoint ? 0.42 : 0.32, 0.026, 6, 28),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.92, depthWrite: false, depthTest: false, blending: THREE.AdditiveBlending }),
      );
      ring.rotation.z = index * Math.PI * 0.5 + Math.PI * 0.25;
      ring.renderOrder = 14;
      burst.add(ring);
    }

    const sparkCount = weakpoint ? 10 : 7;
    for (let index = 0; index < sparkCount; index += 1) {
      const angle = (index / sparkCount) * Math.PI * 2 + (weakpoint ? 0.18 : 0);
      const spark = new THREE.Mesh(
        new THREE.BoxGeometry(0.035, weakpoint ? 0.42 : 0.3, 0.035),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.95, depthWrite: false, depthTest: false, blending: THREE.AdditiveBlending }),
      );
      spark.position.set(Math.cos(angle) * 0.38, Math.sin(angle) * 0.38, 0);
      spark.rotation.z = angle - Math.PI * 0.5;
      spark.userData.direction = new THREE.Vector2(Math.cos(angle), Math.sin(angle));
      spark.renderOrder = 14;
      burst.add(spark);
    }

    const light = new THREE.PointLight(color, weakpoint ? 7 : 4, 4, 2);
    light.position.z = -0.35;
    burst.add(light);
    const duration = weakpoint ? 0.32 : 0.23;
    this.addTransient(burst, elapsedSeconds, duration, (progress) => {
      const eased = 1 - (1 - progress) * (1 - progress);
      core.scale.setScalar(1 + eased * (weakpoint ? 3.2 : 2.2));
      core.rotation.z = progress * Math.PI * 0.75;
      for (const child of burst.children) {
        if (child === core || child === light) continue;
        const mesh = child as THREE.Mesh;
        const direction = mesh.userData.direction as THREE.Vector2 | undefined;
        if (direction) {
          const travel = eased * (weakpoint ? 0.75 : 0.55);
          mesh.position.x = direction.x * (0.38 + travel);
          mesh.position.y = direction.y * (0.38 + travel);
          mesh.scale.y = 1 - progress * 0.65;
        } else mesh.scale.setScalar(1 + eased * (weakpoint ? 2.1 : 1.45));
        (mesh.material as THREE.MeshBasicMaterial).opacity = 0.95 * (1 - progress);
      }
      (core.material as THREE.MeshBasicMaterial).opacity = 1 - progress;
      light.intensity = (weakpoint ? 7 : 4) * (1 - progress);
    });
  }

  private consumeFeedback(events: readonly CombatFeedbackEvent[], elapsedSeconds: number): void {
    for (const event of events) {
      if (this.handledFeedbackIds.has(event.id)) continue;
      this.handledFeedbackIds.add(event.id);
      if (event.type === "primary" && event.from && event.to) {
        const tracerMaterialOpacity = event.hit ? 0.95 : 0.55;
        const tracer = this.createLine(
          new THREE.Vector3(event.from.x, 1.25, event.from.z + 0.7),
          new THREE.Vector3(event.to.x, event.weakpoint ? 1.4 : 1.05, event.to.z),
          event.weakpoint ? "#fff1a6" : "#9be1d1",
          tracerMaterialOpacity,
        );
        this.addTransient(tracer, elapsedSeconds, 0.16, (progress) => {
          (tracer.material as THREE.LineBasicMaterial).opacity = tracerMaterialOpacity * (1 - progress);
        });
        const muzzle = new THREE.Mesh(
          new THREE.SphereGeometry(0.2, 8, 6),
          new THREE.MeshBasicMaterial({ color: "#f4d77d", transparent: true, opacity: 0.95, depthWrite: false }),
        );
        muzzle.position.set(event.from.x, 1.25, event.from.z + 0.72);
        this.addTransient(muzzle, elapsedSeconds, 0.1, (progress) => {
          muzzle.scale.setScalar(1 + progress * 2.5);
          (muzzle.material as THREE.MeshBasicMaterial).opacity = 0.95 * (1 - progress);
        });
        if (event.hit) {
          this.createImpactBurst(event.to, Boolean(event.weakpoint), elapsedSeconds);
          const pulse = this.createGroundPulse(event.to, event.weakpoint ? "#fff1a6" : "#70c8b2");
          this.addTransient(pulse, elapsedSeconds, 0.28, (progress) => {
            pulse.scale.setScalar(1 + progress * (event.weakpoint ? 4.8 : 3.2));
            (pulse.material as THREE.MeshBasicMaterial).opacity = 0.9 * (1 - progress);
          });
        }
      } else if (event.type === "secondary" && event.from && event.to) {
        const charge = event.charge ?? 0;
        const color = event.hit ? "#ffe28a" : "#a8d9d0";
        const from = new THREE.Vector3(event.from.x, 1.28, event.from.z + 0.7);
        const to = new THREE.Vector3(event.to.x, 0.72, event.to.z);
        const beam = this.createBeam(from, to, color);
        this.addTransient(beam, elapsedSeconds, 0.68, (progress) => {
          beam.scale.x = 1 + progress * (1.6 + charge);
          beam.scale.z = 1 + progress * (1.6 + charge);
          (beam.material as THREE.MeshBasicMaterial).opacity = 0.9 * (1 - progress);
        });
        const burst = new THREE.Mesh(
          new THREE.SphereGeometry(0.68, 16, 10),
          new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.92, wireframe: true, depthWrite: false, blending: THREE.AdditiveBlending }),
        );
        burst.position.copy(to);
        burst.renderOrder = 12;
        this.addTransient(burst, elapsedSeconds, 0.72, (progress) => {
          burst.scale.setScalar(0.6 + progress * (3.2 + charge * 2));
          burst.rotation.y = progress * Math.PI;
          (burst.material as THREE.MeshBasicMaterial).opacity = 0.92 * (1 - progress);
        });
        const pulse = this.createGroundPulse(event.to, color);
        this.addTransient(pulse, elapsedSeconds, 0.7, (progress) => {
          pulse.scale.setScalar(1 + progress * (10 + charge * 9));
          (pulse.material as THREE.MeshBasicMaterial).opacity = 0.88 * (1 - progress);
        });
      } else if (event.type === "coverMove" && event.from && event.to) {
        const trail = this.createLine(
          new THREE.Vector3(event.from.x, 0.18, event.from.z),
          new THREE.Vector3(event.to.x, 0.18, event.to.z),
          "#65c2aa",
          0.75,
        );
        this.addTransient(trail, elapsedSeconds, 0.3, (progress) => {
          (trail.material as THREE.LineBasicMaterial).opacity = 0.75 * (1 - progress);
        });
      } else if ((event.type === "reloadComplete" || event.type === "playerHit" || event.type === "coverHit") && event.to) {
        const color = event.type === "playerHit" ? "#d85643" : event.type === "coverHit" ? "#e0bd69" : "#8ed3c3";
        const pulse = this.createGroundPulse(event.to, color);
        this.addTransient(pulse, elapsedSeconds, 0.35, (progress) => {
          pulse.scale.setScalar(1 + progress * 5);
          (pulse.material as THREE.MeshBasicMaterial).opacity = 0.85 * (1 - progress);
        });
      }
    }
    while (this.handledFeedbackIds.size > 256) this.handledFeedbackIds.delete(this.handledFeedbackIds.values().next().value as string);
  }

  private updateTransients(elapsedSeconds: number): void {
    for (let index = this.transientEffects.length - 1; index >= 0; index -= 1) {
      const effect = this.transientEffects[index]!;
      const progress = (elapsedSeconds - effect.startedAt) / effect.duration;
      if (progress < 1) {
        effect.update(Math.max(0, progress));
        continue;
      }
      this.scene.remove(effect.object);
      effect.object.traverse((child) => {
        const renderable = child as THREE.Mesh;
        renderable.geometry?.dispose();
        const childMaterial = renderable.material as THREE.Material | THREE.Material[] | undefined;
        if (Array.isArray(childMaterial)) for (const entry of childMaterial) entry.dispose();
        else childMaterial?.dispose();
      });
      this.transientEffects.splice(index, 1);
    }
  }

  private syncEnemy(enemy: EnemyState): void {
    let group = this.enemyMeshes.get(enemy.id);
    if (!group) {
      group = createEnemyMesh(enemy);
      group.position.set(enemy.position.x, 0, enemy.position.z);
      this.enemyMeshes.set(enemy.id, group);
      this.scene.add(group);
    }
    group.position.x = THREE.MathUtils.lerp(group.position.x, enemy.position.x, 0.28);
    group.position.z = THREE.MathUtils.lerp(group.position.z, enemy.position.z, 0.28);
    const body = group.children[0] as THREE.Mesh;
    const healthRatio = Math.max(0.35, enemy.hp / enemy.maxHp);
    body.scale.y = 0.88 + healthRatio * 0.12;
    const phaseRing = group.getObjectByName("phase-ring");
    if (phaseRing) phaseRing.visible = enemy.shield > 0 || (enemy.phase ?? 1) > 1;
    const healthFill = group.getObjectByName("health-fill") as THREE.Mesh | undefined;
    if (healthFill) {
      const ratio = THREE.MathUtils.clamp(enemy.hp / enemy.maxHp, 0.001, 1);
      healthFill.scale.x = ratio;
      healthFill.position.x = -((healthFill.userData.width as number) * (1 - ratio)) / 2;
    }
  }

  private syncProjectile(projectile: ProjectileState): void {
    let mesh = this.projectileMeshes.get(projectile.id);
    if (!mesh) {
      mesh = new THREE.Mesh(new THREE.SphereGeometry(0.14, 8, 6), material(projectile.hostile ? "#d05241" : "#dbe7e5", projectile.hostile ? "#891d16" : "#4fa18e"));
      this.projectileMeshes.set(projectile.id, mesh);
      this.scene.add(mesh);
    }
    mesh.position.set(projectile.position.x, 1.15, projectile.position.z);
  }

  public render(snapshot: GameSnapshot, elapsedSeconds: number): void {
    const combat = snapshot.state.combat;
    if (combat) {
      if (this.lastCombatNodeId !== snapshot.state.currentNodeId) {
        this.lastCombatNodeId = snapshot.state.currentNodeId;
        this.handledFeedbackIds.clear();
      }
      this.player.position.x = THREE.MathUtils.lerp(this.player.position.x, combat.playerPosition.x, 0.24);
      this.player.position.z = combat.playerPosition.z;
      this.player.rotation.y = Math.atan2(combat.aim.x - this.player.position.x, combat.aim.z - this.player.position.z);
      this.aimMarker.position.x = combat.aim.x;
      this.aimMarker.position.z = combat.aim.z;
      this.aimMarker.visible = snapshot.state.mode === "combat";
      this.consumeFeedback(combat.feedbackEvents, elapsedSeconds);
      const chargeRatio = Math.min(1, combat.chargeTicks / 75);
      const charging = snapshot.state.mode === "combat" && combat.isCharging;
      this.chargeRing.visible = charging;
      this.chargeRing.position.set(this.player.position.x, 0.1, this.player.position.z);
      this.chargeRing.scale.setScalar(0.7 + chargeRatio * 1.25);
      (this.chargeRing.material as THREE.MeshBasicMaterial).opacity = 0.35 + chargeRatio * 0.6;
      this.chargeCore.visible = charging;
      this.chargeCore.position.set(this.player.position.x, 1.15, this.player.position.z);
      this.chargeCore.scale.setScalar(0.72 + chargeRatio * 0.8);
      this.chargeCore.rotation.y = elapsedSeconds * 1.5;
      (this.chargeCore.material as THREE.MeshBasicMaterial).opacity = 0.28 + chargeRatio * 0.62;
      this.chargeOrbit.visible = charging;
      this.chargeOrbit.position.set(this.player.position.x, 1.12, this.player.position.z);
      this.chargeOrbit.rotation.x = Math.PI / 2 + 0.28;
      this.chargeOrbit.rotation.z = -elapsedSeconds * 2.4;
      this.chargeOrbit.scale.setScalar(0.85 + chargeRatio * 0.48);
      this.chargeLight.visible = charging;
      this.chargeLight.position.set(this.player.position.x, 1.3, this.player.position.z);
      this.chargeLight.intensity = charging ? 2 + chargeRatio * 8 : 0;
      for (let index = 0; index < this.chargeSparks.length; index += 1) {
        const spark = this.chargeSparks[index]!;
        const motion = this.reducedMotion ? 0 : elapsedSeconds * (2.2 + chargeRatio * 1.8);
        const angle = (index / this.chargeSparks.length) * Math.PI * 2 + motion;
        const radius = 0.76 + chargeRatio * 0.62;
        spark.visible = charging;
        spark.position.set(
          this.player.position.x + Math.cos(angle) * radius,
          0.68 + (index % 3) * 0.42 + Math.sin(angle * 2) * 0.16,
          this.player.position.z + Math.sin(angle) * radius,
        );
        spark.scale.setScalar(0.7 + chargeRatio * 1.25);
      }
      this.reloadRing.visible = snapshot.state.mode === "combat" && combat.reloadTicks > 0;
      this.reloadRing.position.set(this.player.position.x, 0.14, this.player.position.z);
      this.reloadRing.rotation.z = -elapsedSeconds * 4;
      const activeEnemyIds = new Set(combat.enemies.map((enemy) => enemy.id));
      for (const enemy of combat.enemies) this.syncEnemy(enemy);
      for (const [id, group] of this.enemyMeshes) {
        if (!activeEnemyIds.has(id)) {
          this.scene.remove(group);
          this.enemyMeshes.delete(id);
        }
      }
      const activeProjectileIds = new Set(combat.projectiles.map((projectile) => projectile.id));
      for (const projectile of combat.projectiles) this.syncProjectile(projectile);
      for (const [id, mesh] of this.projectileMeshes) {
        if (!activeProjectileIds.has(id)) {
          this.scene.remove(mesh);
          mesh.geometry.dispose();
          (mesh.material as THREE.Material).dispose();
          this.projectileMeshes.delete(id);
        }
      }
      for (let index = 0; index < this.covers.length; index += 1) {
        const cover = this.covers[index]!;
        const health = combat.coverHealth[index] ?? 0;
        const ratio = THREE.MathUtils.clamp(health / snapshot.build.coverMax, 0, 1);
        cover.visible = health > 0;
        cover.scale.y = 0.72 + ratio * 0.28;
        const indicator = cover.getObjectByName("cover-indicator") as THREE.Mesh | undefined;
        if (indicator) indicator.visible = health > 0 && combat.playerCoverIndex === index && snapshot.state.mode === "combat";
      }
    } else {
      this.aimMarker.visible = false;
      this.chargeRing.visible = false;
      this.chargeCore.visible = false;
      this.chargeOrbit.visible = false;
      this.chargeLight.visible = false;
      for (const spark of this.chargeSparks) spark.visible = false;
      this.reloadRing.visible = false;
      for (const [, group] of this.enemyMeshes) this.scene.remove(group);
      this.enemyMeshes.clear();
      for (const cover of this.covers) {
        cover.visible = true;
        cover.scale.y = 1;
        const indicator = cover.getObjectByName("cover-indicator");
        if (indicator) indicator.visible = false;
      }
    }
    this.updateTransients(elapsedSeconds);
    if (!this.reducedMotion) {
      if (!combat) this.player.rotation.y = Math.sin(elapsedSeconds * 1.6) * 0.035;
      this.aimMarker.rotation.z = elapsedSeconds * 0.45;
      for (let index = 0; index < this.mistStrips.length; index += 1) this.mistStrips[index]!.position.x = Math.sin(elapsedSeconds * (0.09 + index * 0.025) + index) * 2.2;
      for (const group of this.enemyMeshes.values()) {
        const ring = group.getObjectByName("phase-ring");
        if (ring) ring.rotation.z = elapsedSeconds * 0.6;
      }
    }
    this.renderer.render(this.scene, this.camera);
  }
}
