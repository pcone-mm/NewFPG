import * as THREE from "three";
import type { CombatState, EnemyState, Vec2 } from "../game/types";
import { distance3, enemyCenter, height } from "../game/geometry";

/** Fixed-capacity instance batches: no geometry/material allocation per spawn or hit. */
class Batch {
  readonly mesh: THREE.InstancedMesh;
  private readonly transform = new THREE.Object3D();
  private readonly color = new THREE.Color();
  count = 0;
  constructor(scene: THREE.Scene, geometry: THREE.BufferGeometry, material: THREE.Material, readonly capacity: number) {
    this.mesh = new THREE.InstancedMesh(geometry, material, capacity);
    this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); this.mesh.frustumCulled = false; this.mesh.count = 0;
    scene.add(this.mesh);
  }
  put(x: number, y: number, z: number, sx: number, sy: number, sz: number, color: string, rx = 0, ry = 0, rz = 0): void {
    if (this.count >= this.capacity) return;
    this.transform.position.set(x, y, z); this.transform.scale.set(sx, sy, sz); this.transform.rotation.set(rx, ry, rz); this.transform.updateMatrix();
    this.mesh.setMatrixAt(this.count, this.transform.matrix); this.mesh.setColorAt(this.count++, this.color.set(color));
  }
  segment(a: THREE.Vector3, b: THREE.Vector3, width: number, color: string): void {
    if (this.count >= this.capacity) return;
    const direction = b.clone().sub(a);
    this.transform.position.copy(a).add(b).multiplyScalar(0.5); this.transform.scale.set(width, direction.length(), width);
    this.transform.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize()); this.transform.updateMatrix();
    this.mesh.setMatrixAt(this.count, this.transform.matrix); this.mesh.setColorAt(this.count++, this.color.set(color));
  }
  finish(): void { this.mesh.count = this.count; this.mesh.instanceMatrix.needsUpdate = true; if (this.mesh.instanceColor) this.mesh.instanceColor.needsUpdate = true; this.count = 0; }
}

interface Corpse { enemy: EnemyState; start: number }
export class SwarmPresentation {
  private readonly shell: Batch;
  private readonly limbs: Batch;
  private readonly glow: Batch;
  private readonly wings: Batch;
  private readonly bars: Batch;
  private readonly warnings: Batch;
  private readonly projectiles: Batch;
  private readonly orbs: Batch;
  private readonly halos: Batch;
  private readonly trails: Batch;
  private readonly corpses: Corpse[] = [];
  private previous = new Map<string, { points: Vec2[]; tick: number }>();
  private poses = new Map<string, { previous: Vec2; current: Vec2 }>();
  private poseTick = -1;
  constructor(scene: THREE.Scene) {
    const shellMat = new THREE.MeshStandardMaterial({ roughness: 0.5, metalness: 0.35, flatShading: true });
    const glowMat = new THREE.MeshBasicMaterial();
    this.shell = new Batch(scene, new THREE.IcosahedronGeometry(1, 0), shellMat, 700);
    this.limbs = new Batch(scene, new THREE.CylinderGeometry(1, 1, 1, 5), shellMat, 1600);
    this.glow = new Batch(scene, new THREE.IcosahedronGeometry(1, 0), glowMat, 400);
    this.wings = new Batch(scene, new THREE.SphereGeometry(1, 5, 3), new THREE.MeshStandardMaterial({ transparent: true, opacity: 0.72, roughness: 0.25, metalness: 0.3 }), 200);
    this.bars = new Batch(scene, new THREE.BoxGeometry(1, 1, 1), glowMat, 100);
    this.warnings = new Batch(scene, new THREE.RingGeometry(0.82, 1, 20), new THREE.MeshBasicMaterial({ side: THREE.DoubleSide, transparent: true, opacity: 0.85 }), 50);
    this.projectiles = new Batch(scene, new THREE.IcosahedronGeometry(1, 0), glowMat, 128);
    this.orbs = new Batch(scene, new THREE.IcosahedronGeometry(1, 0), glowMat, 128);
    this.halos = new Batch(scene, new THREE.IcosahedronGeometry(1, 1), new THREE.MeshBasicMaterial({ transparent: true, opacity: 0.15, blending: THREE.AdditiveBlending, depthWrite: false }), 128);
    this.trails = new Batch(scene, new THREE.CylinderGeometry(1, 0.15, 1, 5), new THREE.MeshBasicMaterial({ transparent: true, opacity: 0.55, blending: THREE.AdditiveBlending, depthWrite: false }), 384);
  }
  reset(): void { this.previous.clear(); this.poses.clear(); this.poseTick = -1; this.corpses.length = 0; }
  death(enemy: EnemyState, tick: number): void {
    if (this.corpses.length >= 48) this.corpses.shift();
    this.corpses.push({ enemy: { ...enemy, position: { ...enemy.position } }, start: tick });
  }
  private insect(e: EnemyState, tick: number, aim: Vec2, reduced: boolean, fade = 0): void {
    const root = e.position, big = e.type === "boss" ? 2.25 : e.type === "elite" ? 1.5 : e.type === "summoner" ? 1.2 : e.type === "minion" ? 0.7 : 1;
    const air = e.layer === "air" || e.type === "flyer";
    const t = tick / 60, seed = Number(e.id.replace(/\D/g, "")) || 0;
    const flinch = Math.max(0, 1 - (tick - (e.lastHitTick ?? -999)) / 9);
    const size = big * (1 - fade * 0.7), bodyY = e.type === "boss" ? 1.75 : 1;
    const bob = reduced ? 0 : (air ? Math.sin(t * 5 + seed) * 0.09 : Math.sin(t * 14 + seed) * 0.025);
    const rotation = new THREE.Matrix4().makeRotationY(Math.atan2(([-7.5, 0, 7.5][e.targetCover ?? 1] ?? 0) - root.x, 2.5 - root.z));
    if (fade > 0) rotation.multiply(new THREE.Matrix4().makeRotationZ(fade * Math.PI));
    const point = (x: number, y: number, z: number): THREE.Vector3 => new THREE.Vector3(x * size, y * size, z * size).applyMatrix4(rotation).add(new THREE.Vector3(root.x, height(root) + bodyY + bob - fade * 0.6, root.z));
    const color = flinch > 0.6 ? "#f5ffef" : e.type === "boss" ? "#a6372c" : e.type === "elite" ? "#b9994b" : e.type === "summoner" ? "#416354" : e.type === "ranged" ? "#386e62" : air ? "#387e72" : "#a84935";
    const body = point(0, 0, 0), head = point(0, -0.05, 0.58), abdomen = point(0, 0.02, -0.48);
    const yaw = Math.atan2(([-7.5, 0, 7.5][e.targetCover ?? 1] ?? 0) - root.x, 2.5 - root.z);
    this.shell.put(body.x, body.y, body.z, size * 0.5, size * 0.36 * (1 - flinch * 0.25), size * 0.63, color, 0, yaw, fade * Math.PI);
    this.shell.put(abdomen.x, abdomen.y, abdomen.z, size * (e.type === "summoner" ? 0.7 : 0.46), size * 0.34, size * 0.5, color, 0, yaw);
    this.shell.put(head.x, head.y, head.z, size * 0.3, size * 0.25, size * 0.29, "#1b2d28", 0, yaw);
    for (const side of [-1, 1]) {
      const eye = point(side * 0.18, 0.13, 0.78);
      this.glow.put(eye.x, eye.y, eye.z, size * 0.09, size * 0.08, size * 0.08, "#f7d875");
      this.limbs.segment(point(side * 0.16, 0.05, 0.75), point(side * 0.32, 0.3, 1), 0.027 * size, "#d2ad5b");
      for (let leg = 0; leg < 3; leg++) {
        const stride = reduced || e.behavior === "windup" ? 0 : Math.sin(t * 16 + leg * Math.PI + side + seed) * 0.22;
        const a = point(side * 0.34, -0.08, (leg - 1) * 0.32), b = point(side * 0.73, -0.17, (leg - 1) * 0.5 + stride), c = point(side * 0.96, -0.73, (leg - 1) * 0.5 - stride);
        this.limbs.segment(a, b, size * 0.045, color); this.limbs.segment(b, c, size * 0.034, "#b98d60");
      }
      if (air) {
        const wing = point(side * 0.67, 0.14, -0.1);
        this.wings.put(wing.x, wing.y, wing.z, size * 0.87, size * 0.035, size * 0.32, "#98e5d4", 0, yaw, reduced ? side * 0.15 : Math.sin(t * 65 + seed) * side * 0.65);
      }
      if (e.type === "elite" || e.type === "boss") {
        const horn = point(side * 0.44, 0.32, 0.4);
        this.shell.put(horn.x, horn.y, horn.z, size * 0.16, size * 0.48, size * 0.14, "#d7bb75", -0.4, yaw, side * 0.35);
      }
    }
    const weak = point(0, 0.12, 0.05);
    this.glow.put(weak.x, weak.y, weak.z, size * 0.13, size * 0.19, size * 0.26, e.shield > 0 ? "#81dcff" : "#f5d783");
    if (e.type === "ranged" || e.type === "summoner") for (let n = 0; n < (e.type === "summoner" ? 3 : 1); n++) {
      const sac = point((n - (e.type === "summoner" ? 1 : 0)) * 0.3, 0.36, -0.34);
      const pulse = e.behavior === "windup" ? 1.2 + Math.sin(t * 12) * 0.15 : 1;
      this.glow.put(sac.x, sac.y, sac.z, size * 0.23 * pulse, size * 0.28 * pulse, size * 0.28 * pulse, e.type === "summoner" ? "#b5c678" : "#e58345");
    }
    if (fade > 0) return;
    if (e.behavior === "windup") {
      this.warnings.put(root.x, height(root) + 0.12, root.z, big * 1.2, big * 1.2, 1, "#ff6149", -Math.PI / 2);
      if (air) this.warnings.put(root.x, 0.08, root.z, 0.6, 0.6, 1, "#ff6149", -Math.PI / 2);
    }
    if (e.type === "boss" || e.type === "elite" || tick - (e.lastHitTick ?? -999) < 70 || distance3(enemyCenter(e), aim) < 0.85) {
      const w = big * 1.5, ratio = Math.max(0, e.hp / e.maxHp), y = height(root) + bodyY + big * 0.85;
      this.bars.put(root.x, y, root.z, w, 0.065, 0.055, "#182722");
      this.bars.put(root.x + w * (1 - ratio) / 2, y + 0.018, root.z - 0.025, w * ratio, 0.047, 0.06, e.type === "boss" ? "#ef7255" : "#84d4b7");
      if (e.shield > 0) this.bars.put(root.x, y + 0.12, root.z, w, 0.04, 0.06, "#76c9ec");
    }
  }
  render(c: CombatState | undefined, reduced: boolean, interpolation = 1): void {
    if (c) {
      if (this.poseTick !== c.tick) {
        this.poseTick = c.tick;
        const live = new Set(c.enemies.map((e) => e.id));
        for (const id of this.poses.keys()) if (!live.has(id)) this.poses.delete(id);
        for (const e of c.enemies) this.poses.set(e.id, { previous: this.poses.get(e.id)?.current ?? { ...e.position }, current: { ...e.position } });
      }
      for (const e of c.enemies) if (e.hp > 0) {
        const pose = this.poses.get(e.id)!;
        const position = { x: THREE.MathUtils.lerp(pose.previous.x, pose.current.x, interpolation), y: THREE.MathUtils.lerp(height(pose.previous), height(pose.current), interpolation), z: THREE.MathUtils.lerp(pose.previous.z, pose.current.z, interpolation) };
        this.insect({ ...e, position }, c.tick, c.aim, reduced);
      }
      for (let i = this.corpses.length - 1; i >= 0; i--) {
        const corpse = this.corpses[i]!, age = (c.tick - corpse.start) / 32;
        if (age >= 1) this.corpses.splice(i, 1); else this.insect(corpse.enemy, c.tick, c.aim, reduced, reduced ? 0.9 : age);
      }
      for (const p of c.projectiles) this.projectiles.put(p.position.x, height(p.position, 1.15), p.position.z, 0.16, 0.16, 0.22, "#ff7750");
      const live = new Set<string>();
      for (const o of c.experienceOrbs) {
        live.add(o.id); const p = o.position, size = 0.14 + Math.min(0.12, o.value * 0.015);
        this.orbs.put(p.x, height(p), p.z, size, size * 1.3, size, "#a4ffe0");
        this.halos.put(p.x, height(p), p.z, size * 2.5, size * 2.5, size * 2.5, "#5debbc");
        const history = this.previous.get(o.id) ?? { points: [], tick: -1 };
        if (history.tick < 0 || c.tick - history.tick >= 4) { history.points.unshift({ ...p }); history.points.length = Math.min(3, history.points.length); history.tick = c.tick; this.previous.set(o.id, history); }
        const points = [p, ...history.points];
        if (o.age > 24) for (let i = 1; i < points.length; i++) {
          const a = points[i - 1]!, b = points[i]!;
          this.trails.segment(new THREE.Vector3(a.x, height(a), a.z), new THREE.Vector3(b.x, height(b), b.z), 0.1 / i, "#68efc0");
        }
      }
      for (const id of this.previous.keys()) if (!live.has(id)) this.previous.delete(id);
    }
    for (const batch of [this.shell, this.limbs, this.glow, this.wings, this.bars, this.warnings, this.projectiles, this.orbs, this.halos, this.trails]) batch.finish();
  }
}
