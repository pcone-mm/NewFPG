import * as THREE from "three";
import type { CombatFeedbackEvent, Vec2 } from "../game/types";
import { height } from "../game/geometry";

interface Particle { origin: THREE.Vector3; velocity: THREE.Vector3; color: THREE.Color; start: number; duration: number; size: number }
interface Pulse { position: Vec2; start: number; radius: number; color: THREE.Color; absorb: boolean }
interface NumberSlot { sprite: THREE.Sprite; canvas: HTMLCanvasElement; context: CanvasRenderingContext2D; targetId?: string; value: number; start: number; tick: number; priority: boolean; origin: THREE.Vector3; weak: boolean }
export class CombatEffects {
  private readonly particles: Particle[] = [];
  private readonly mesh: THREE.InstancedMesh;
  private readonly transform = new THREE.Object3D();
  private readonly numbers: NumberSlot[] = [];
  private readonly pulses: Pulse[] = [];
  private readonly rings: THREE.InstancedMesh;
  private lastSerial = -1;
  constructor(scene: THREE.Scene) {
    this.mesh = new THREE.InstancedMesh(new THREE.IcosahedronGeometry(1, 0), new THREE.MeshBasicMaterial({ transparent: true, opacity: 0.85, depthWrite: false }), 512);
    this.mesh.frustumCulled = false; this.mesh.count = 0; this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); scene.add(this.mesh);
    this.rings = new THREE.InstancedMesh(new THREE.RingGeometry(0.94, 1, 32), new THREE.MeshBasicMaterial({ side: THREE.DoubleSide, transparent: true, opacity: 0.6, depthWrite: false, blending: THREE.AdditiveBlending }), 32);
    this.rings.frustumCulled = false; this.rings.count = 0; scene.add(this.rings);
    for (let i = 0; i < 64; i++) {
      const canvas = document.createElement("canvas"); canvas.width = 192; canvas.height = 96;
      const context = canvas.getContext("2d")!;
      const texture = new THREE.CanvasTexture(canvas); texture.colorSpace = THREE.SRGBColorSpace;
      const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, depthTest: false, depthWrite: false, transparent: true }));
      sprite.visible = false; sprite.renderOrder = 20; scene.add(sprite);
      this.numbers.push({ sprite, canvas, context, value: 0, start: -999, tick: -999, priority: false, origin: new THREE.Vector3(), weak: false });
    }
  }
  reset(lastSerial = -1): void { this.lastSerial = lastSerial; this.particles.length = 0; this.pulses.length = 0; for (const slot of this.numbers) { slot.start = -999; slot.sprite.visible = false; } }
  private particle(position: Vec2, tick: number, color: string, i: number, speed: number, duration: number, size = 0.07): void {
    if (this.particles.length >= 512) this.particles.shift();
    const angle = i * 2.399963, y = Math.sin(i * 1.7);
    this.particles.push({ origin: new THREE.Vector3(position.x, height(position), position.z), velocity: new THREE.Vector3(Math.cos(angle), y, Math.sin(angle)).multiplyScalar(speed),
      color: new THREE.Color(color), start: tick, duration, size });
  }
  private damageNumber(e: CombatFeedbackEvent): void {
    if (!e.to || !e.value) return;
    let slot = this.numbers.find((n) => n.targetId === e.targetId && e.tick - n.tick < 12 && e.tick - n.start < 42);
    const merge = Boolean(slot), priority = e.enemyType === "boss" || e.enemyType === "elite";
    slot ??= this.numbers.find((n) => e.tick - n.start >= 54);
    slot ??= [...this.numbers].filter((n) => !n.priority || priority).sort((a, b) => a.start - b.start)[0];
    if (!slot) return;
    slot.value = (merge ? slot.value : 0) + e.value; slot.start = e.tick; slot.tick = e.tick; slot.targetId = e.targetId; slot.priority = priority; slot.weak = Boolean(e.weakpoint || (merge && slot.weak));
    slot.origin.set(e.to.x, e.worldY ?? height(e.to) + 0.8, e.to.z); slot.sprite.visible = true;
    const ctx = slot.context; ctx.clearRect(0, 0, 192, 96); ctx.textAlign = "center"; ctx.textBaseline = "middle"; ctx.font = `800 ${slot.weak ? 60 : 52}px sans-serif`;
    ctx.strokeStyle = "#101917"; ctx.lineWidth = 9; ctx.strokeText(String(Math.round(slot.value)), 96, 48);
    ctx.fillStyle = slot.weak ? "#ffe38b" : "#f3fff9"; ctx.fillText(String(Math.round(slot.value)), 96, 48); slot.sprite.material.map!.needsUpdate = true;
  }
  consume(events: readonly CombatFeedbackEvent[], reduced: boolean): void {
    for (const e of events) {
      const serial = e.serial ?? Number(e.id.split("-").at(-1));
      if (serial <= this.lastSerial) continue;
      this.lastSerial = serial;
      if (e.type === "enemyDamage") this.damageNumber(e);
      if (!e.to) continue;
      if (e.type === "secondary" || e.type === "explosion" || e.type === "experienceCollected") {
        if (this.pulses.length >= 32) this.pulses.shift();
        this.pulses.push({ position: { ...e.to }, start: e.tick, radius: e.type === "experienceCollected" ? 1.2 : e.value ?? 2, color: new THREE.Color(e.type === "experienceCollected" ? "#81ffcb" : "#ffd774"), absorb: e.type === "experienceCollected" });
      }
      if (e.type === "primary" || e.type === "secondary") {
        if (e.from) {
          const steps = e.type === "primary" ? 12 : 28;
          for (let i = 0; i < steps; i++) {
            const t = i / steps;
            this.particle({ x: e.from.x + (e.to.x - e.from.x) * t, y: height(e.from) + (height(e.to) - height(e.from)) * t, z: e.from.z + (e.to.z - e.from.z) * t }, e.tick, e.type === "primary" ? "#b9f6df" : "#ffe4a0", i, 0, e.type === "primary" ? 7 : 18, e.type === "primary" ? 0.048 : 0.13);
          }
        }
      }
      if (e.type === "enemyDamage" || e.type === "enemyDeath" || e.type === "secondary" || e.type === "explosion" || e.type === "experienceCollected" || e.type === "coverHit") {
        const count = e.type === "secondary" ? 48 : e.type === "enemyDeath" ? 12 : e.type === "experienceCollected" ? 5 : 7;
        const color = e.type === "experienceCollected" ? "#8fffd0" : e.type === "enemyDeath" ? "#c37350" : e.weakpoint || e.type === "secondary" || e.type === "explosion" ? "#ffe290" : "#d0fff1";
        for (let i = 0; i < count; i++) this.particle(e.to, e.tick, color, i + serial, reduced ? 0.025 : e.type === "secondary" ? (e.value ?? 4) / 20 : 0.07, e.type === "secondary" ? 28 : 20, e.type === "enemyDeath" ? 0.14 : 0.075);
      }
    }
  }
  render(tick: number, reduced: boolean): void {
    let rings = 0;
    for (let i = this.pulses.length - 1; i >= 0; i--) {
      const pulse = this.pulses[i]!, progress = (tick - pulse.start) / 25;
      if (progress >= 1) { this.pulses.splice(i, 1); continue; }
      const scale = pulse.radius * (pulse.absorb ? 1 - progress * 0.8 : 0.2 + progress * 0.8);
      this.transform.position.set(pulse.position.x, height(pulse.position), pulse.position.z);
      this.transform.rotation.set(pulse.absorb ? 0 : -Math.PI / 2, 0, 0); this.transform.scale.setScalar(scale); this.transform.updateMatrix();
      this.rings.setMatrixAt(rings, this.transform.matrix); this.rings.setColorAt(rings++, pulse.color.clone().multiplyScalar(1 - progress));
    }
    this.rings.count = rings; this.rings.instanceMatrix.needsUpdate = true; if (this.rings.instanceColor) this.rings.instanceColor.needsUpdate = true;
    let count = 0;
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i]!, age = tick - p.start, life = age / p.duration;
      if (life >= 1) { this.particles.splice(i, 1); continue; }
      this.transform.position.copy(p.origin).addScaledVector(p.velocity, age); this.transform.scale.setScalar(p.size * (1 - life)); this.transform.rotation.set(age * 0.1, i, age * 0.15); this.transform.updateMatrix();
      this.mesh.setMatrixAt(count, this.transform.matrix); this.mesh.setColorAt(count++, p.color);
    }
    this.mesh.count = count; this.mesh.instanceMatrix.needsUpdate = true; if (this.mesh.instanceColor) this.mesh.instanceColor.needsUpdate = true;
    for (const n of this.numbers) {
      const p = (tick - n.start) / 54; n.sprite.visible = p >= 0 && p < 1; if (!n.sprite.visible) continue;
      n.sprite.position.copy(n.origin); n.sprite.position.y += p * (reduced ? 0.25 : 1.05);
      const size = (n.weak ? 1.65 : 1.3) * (reduced ? 1 : 1 + Math.sin(Math.min(1, p * 5) * Math.PI) * 0.16);
      n.sprite.scale.set(size, size * 0.5, 1); n.sprite.material.opacity = Math.min(1, (1 - p) * 3);
    }
  }
}
