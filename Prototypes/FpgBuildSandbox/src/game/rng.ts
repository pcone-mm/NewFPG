function hashString(value: string): number {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0 || 0x9e3779b9;
}

export function deriveSeed(seed: string, stream: string): number {
  return hashString(`${seed}::${stream}`);
}

export class SeededRng {
  public constructor(public state: number) {
    if (!state) this.state = 0x6d2b79f5;
  }

  public next(): number {
    let value = (this.state += 0x6d2b79f5);
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    const result = ((value ^ (value >>> 14)) >>> 0) / 4294967296;
    this.state >>>= 0;
    return result;
  }

  public int(maxExclusive: number): number {
    if (maxExclusive <= 0) throw new Error("maxExclusive must be positive");
    return Math.floor(this.next() * maxExclusive);
  }

  public pick<T>(values: readonly T[]): T {
    if (values.length === 0) throw new Error("Cannot pick from an empty collection");
    return values[this.int(values.length)] as T;
  }

  public shuffle<T>(values: readonly T[]): T[] {
    const result = [...values];
    for (let index = result.length - 1; index > 0; index -= 1) {
      const swapIndex = this.int(index + 1);
      [result[index], result[swapIndex]] = [result[swapIndex] as T, result[index] as T];
    }
    return result;
  }
}
