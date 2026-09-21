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
  context.fillText(hp <= 0 ? `掩体 ${coverIndex + 1}  ·  已毁` : `掩体 ${coverIndex + 1}  ·  ${Math.ceil(hp)} / ${Math.round(maxHp)}`, canvas.width / 2, canvas.height / 2 + 1);
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
  private readonly aimVisualPoint = new THREE.Vector3(0, 0.08, 12);
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
      const structure = new THREE.Group();
      structure.name = "cover-structure";
      const body = new THREE.Mesh(new THREE.BoxGeometry(4.15, 1.28, 0.9), material("#53635c", "#0b241e"));
      body.name = "cover-body";
      body.position.y = 0.72;
      body.castShadow = true;
      body.receiveShadow = true;
      structure.add(body);
      const frontPlate = new THREE.Mesh(new THREE.BoxGeometry(3.76, 1.02, 0.12), material("#68766c", "#102d24"));
      frontPlate.name = "cover-front-plate";
      frontPlate.position.set(0, 0.72, -0.51);
      frontPlate.castShadow = true;
      structure.add(frontPlate);
      for (const side of [-1, 1]) {
        const post = new THREE.Mesh(new THREE.BoxGeometry(0.3, 1.72, 1.08), material("#3b4d46", "#071b17"));
        post.position.set(side * 1.88, 0.86, 0);
        post.castShadow = true;
        post.receiveShadow = true;
        structure.add(post);
      }
      const cap = new THREE.Mesh(new THREE.BoxGeometry(4.55, 0.2, 1.2), material("#927b55", "#302414"));
      cap.name = "cover-cap";
      cap.position.y = 1.72;
      cap.castShadow = true;
      structure.add(cap);
      const lowerRail = new THREE.Mesh(new THREE.BoxGeometry(4.0, 0.12, 1.0), material("#8a7350", "#2d2415"));
      lowerRail.position.set(0, 0.14, -0.08);
      structure.add(lowerRail);
      const inlay = material("#4f9a86", "#123e32");
      for (const inlayX of [-1.3, 0, 1.3]) {
        const strip = new THREE.Mesh(new THREE.BoxGeometry(0.055, 0.82, 0.035), inlay);
        strip.position.set(inlayX, 0.72, -0.585);
        structure.add(strip);
      }
      const boltMaterial = material("#b99d61", "#49391c");
      for (const boltX of [-1.64, 1.64]) for (const boltY of [0.38, 1.18]) {
        const bolt = new THREE.Mesh(new THREE.SphereGeometry(0.07, 8, 6), boltMaterial);
        bolt.position.set(boltX, boltY, -0.59);
        structure.add(bolt);
      }
      cover.add(structure);

      const crackMaterial = new THREE.LineBasicMaterial({ color: "#c55443", transparent: true, opacity: 0.86, depthTest: false });
      const damage = new THREE.Group();
      damage.name = "cover-damage";
      for (const points of [
        [new THREE.Vector3(-1.55, 1.24, -0.595), new THREE.Vector3(-0.78, 0.86, -0.6), new THREE.Vector3(-0.98, 0.33, -0.605)],
        [new THREE.Vector3(0.42, 1.48, -0.595), new THREE.Vector3(0.12, 1.03, -0.6), new THREE.Vector3(0.52, 0.52, -0.605), new THREE.Vector3(0.28, 0.23, -0.61)],
      ]) {
        const crack = new THREE.Line(new THREE.BufferGeometry().setFromPoints(points), crackMaterial);
        crack.renderOrder = 18;
        damage.add(crack);
      }
      for (const [chipX, chipY, chipScale] of [[-1.34, 0.26, 1], [1.2, 1.3, 0.75]] as const) {
        const chip = new THREE.Mesh(new THREE.BoxGeometry(0.3 * chipScale, 0.16 * chipScale, 0.16), material("#37463f"));
        chip.position.set(chipX, chipY, -0.63);
        chip.rotation.z = chipX < 0 ? -0.24 : 0.18;
        damage.add(chip);
      }
      damage.visible = false;
      cover.add(damage);

      const severe = new THREE.Group();
      severe.name = "cover-severe";
      const breach = new THREE.Mesh(new THREE.BoxGeometry(1.25, 0.72, 0.14), new THREE.MeshBasicMaterial({ color: "#17241f", transparent: true, opacity: 0.92 }));
      breach.position.set(0.12, 0.78, -0.63);
      severe.add(breach);
      for (const side of [-1, 1]) {
        const brokenPlate = new THREE.Mesh(new THREE.BoxGeometry(1.42, 0.5, 0.16), material("#46564e", "#0a211b"));
        brokenPlate.position.set(side * 1.24, 0.58, -0.62);
        brokenPlate.rotation.z = side * 0.16;
        brokenPlate.castShadow = true;
        severe.add(brokenPlate);
      }
      severe.visible = false;
      cover.add(severe);

      const destroyed = new THREE.Group();
      destroyed.name = "cover-destroyed";
      const rubbleMaterial = material("#43534b", "#0a1b16");
      for (const [rubbleX, rubbleY, rubbleZ, rubbleScale, rubbleRotation] of [
        [-1.65, 0.16, 0.05, 0.72, -0.18], [-0.62, 0.1, -0.1, 0.5, 0.24], [0.42, 0.13, 0.08, 0.84, -0.12], [1.55, 0.2, -0.02, 0.62, 0.16],
      ] as const) {
        const rubble = new THREE.Mesh(new THREE.BoxGeometry(0.9 * rubbleScale, 0.28 * rubbleScale, 0.72 * rubbleScale), rubbleMaterial);
        rubble.position.set(rubbleX, rubbleY, rubbleZ);
        rubble.rotation.z = rubbleRotation;
        rubble.rotation.y = rubbleRotation * 0.7;
        rubble.castShadow = true;
        destroyed.add(rubble);
      }
      for (const side of [-1, 1]) {
        const brokenPost = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.82, 0.92), material("#59675d", "#0b211b"));
        brokenPost.position.set(side * 1.75, 0.42, 0);
        brokenPost.rotation.z = side * 0.32;
        brokenPost.castShadow = true;
        destroyed.add(brokenPost);
      }
      destroyed.visible = false;
      cover.add(destroyed);

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
    // The combat ray uses this same fixed camera ray origin. Keeping the
    // endpoint on its z=12 plane makes the visible marker and hit direction
    // identical without target snapping or low-angle clamping.
    const point = new THREE.Vector3();
    if (!ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), -12), point)) return { x: 0, y: 1, z: 12 };
    // Low cursor positions intersect the z=12 plane below the ground. Keep
    // the actual combat endpoint unchanged, but place the visible reticle on
    // the ground intersection so it never disappears underground.
    if (point.y < 0.08) {
      const groundPoint = new THREE.Vector3();
      if (ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 1, 0), -0.08), groundPoint)) this.aimVisualPoint.copy(groundPoint);
      else this.aimVisualPoint.copy(point).setY(0.08);
    } else {
      this.aimVisualPoint.copy(point);
    }
    return { x: point.x, y: point.y, z: point.z };
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
      this.aimMarker.position.copy(this.aimVisualPoint);
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
        const maxHp = Math.max(1, snapshot.build.coverMax);
        const ratio = THREE.MathUtils.clamp(hp / maxHp, 0, 1);
        const stage = ratio <= 0 ? 3 : ratio >= 0.7 ? 0 : ratio >= 0.3 ? 1 : 2;
        cover.visible = true;
        const structure = cover.getObjectByName("cover-structure");
        const body = cover.getObjectByName("cover-body");
        const frontPlate = cover.getObjectByName("cover-front-plate");
        const cap = cover.getObjectByName("cover-cap");
        const damage = cover.getObjectByName("cover-damage");
        const severe = cover.getObjectByName("cover-severe");
        const destroyed = cover.getObjectByName("cover-destroyed");
        if (structure) {
          structure.visible = stage < 3;
          structure.scale.y = stage === 2 ? 0.78 : 1;
          structure.position.y = stage === 2 ? -0.18 : 0;
        }
        if (body) body.visible = stage < 2;
        if (frontPlate) frontPlate.visible = stage < 2;
        if (cap) cap.rotation.z = stage === 2 ? -0.08 : 0;
        if (damage) { damage.visible = stage === 1; damage.scale.set(stage === 1 ? 1.18 : 1, stage === 1 ? 1.12 : 1, 1); }
        if (severe) { severe.visible = stage === 2; severe.scale.setScalar(stage === 2 ? 1.1 : 1); }
        if (destroyed) destroyed.visible = stage === 3;
        const indicator = cover.getObjectByName("cover-indicator"); if (indicator) indicator.visible = c.playerCoverIndex === i;
        const display = this.coverHealthDisplays[i]!;
        display.group.visible = c.playerCoverIndex === i;
        if (display.group.visible) {
          display.fill.scale.x = ratio;
          display.fill.position.x = -display.width * (1 - ratio) * 0.5;
          const fillMaterial = display.fill.material as THREE.MeshBasicMaterial;
          fillMaterial.color.set(ratio < 0.3 ? "#c74f3b" : ratio < 0.7 ? "#d6ae59" : "#80bdc8");
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
        const structure = cover.getObjectByName("cover-structure");
        if (structure) { structure.visible = true; structure.scale.y = 1; structure.position.y = 0; }
        const body = cover.getObjectByName("cover-body"); if (body) body.visible = true;
        const frontPlate = cover.getObjectByName("cover-front-plate"); if (frontPlate) frontPlate.visible = true;
        const cap = cover.getObjectByName("cover-cap"); if (cap) cap.rotation.z = 0;
        const damage = cover.getObjectByName("cover-damage"); if (damage) { damage.visible = false; damage.scale.set(1, 1, 1); }
        const severe = cover.getObjectByName("cover-severe"); if (severe) { severe.visible = false; severe.scale.setScalar(1); }
        const destroyed = cover.getObjectByName("cover-destroyed"); if (destroyed) destroyed.visible = false;
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
