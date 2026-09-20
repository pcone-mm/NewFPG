import type { EnemyState, Vec2 } from "./types";

export const height = (p: Vec2, fallback = 0): number => p.y ?? fallback;
export const distance3 = (a: Vec2, b: Vec2): number => Math.hypot(a.x - b.x, height(a) - height(b), a.z - b.z);
export const enemyCenter = (e: EnemyState): Vec2 => ({ ...e.position, y: height(e.position) + (e.type === "boss" ? 1.75 : 1) });
export const enemyRadius = (e: EnemyState): number => e.type === "boss" ? 1.65 : e.type === "elite" ? 1 : e.type === "summoner" ? 0.85 : 0.65;

/** Distance along a normalized ray to the first sphere contact. */
export function raySphere(origin: Vec2, direction: Vec2, center: Vec2, radius: number): number | undefined {
  const ox = origin.x - center.x, oy = height(origin) - height(center), oz = origin.z - center.z;
  const b = ox * direction.x + oy * height(direction) + oz * direction.z;
  const c = ox * ox + oy * oy + oz * oz - radius * radius;
  const discriminant = b * b - c;
  if (discriminant < 0) return undefined;
  const near = -b - Math.sqrt(discriminant), far = -b + Math.sqrt(discriminant);
  return far < 0 ? undefined : Math.max(0, near);
}

export function segmentBox(start: Vec2, end: Vec2, min: Vec2, max: Vec2): number | undefined {
  let near = 0, far = 1;
  for (const key of ["x", "y", "z"] as const) {
    const origin = start[key] ?? 0, delta = (end[key] ?? 0) - origin;
    const lo = min[key] ?? 0, hi = max[key] ?? 0;
    if (Math.abs(delta) < 1e-8) { if (origin < lo || origin > hi) return undefined; continue; }
    const a = (lo - origin) / delta, b = (hi - origin) / delta;
    near = Math.max(near, Math.min(a, b)); far = Math.min(far, Math.max(a, b));
    if (near > far) return undefined;
  }
  return near;
}
