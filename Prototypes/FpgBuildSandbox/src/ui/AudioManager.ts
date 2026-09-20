export type SoundCue = "shoot" | "hit" | "reward" | "ritual" | "danger" | "kill" | "collect";

export class AudioManager {
  private context?: AudioContext;
  private muted = localStorage.getItem("fpg-build-sandbox:muted") === "true";
  private voices = 0;
  private lastCue = new Map<SoundCue, number>();
  private collectNote = 0;

  public isMuted(): boolean {
    return this.muted;
  }

  public toggle(): boolean {
    this.muted = !this.muted;
    localStorage.setItem("fpg-build-sandbox:muted", String(this.muted));
    return this.muted;
  }

  public play(cue: SoundCue): void {
    if (this.muted || this.voices >= 8) return;
    this.context ??= new AudioContext();
    const now = this.context.currentTime;
    const last = this.lastCue.get(cue) ?? -99;
    if (now - last < (cue === "collect" ? 0.065 : 0.04)) return;
    this.lastCue.set(cue, now);
    if (this.context.state === "suspended") void this.context.resume();
    if (cue === "collect") this.collectNote = now - last > 0.8 ? 0 : (this.collectNote + 1) % 8;
    const oscillator = this.context.createOscillator();
    const gain = this.context.createGain();
    const frequencies: Record<SoundCue, [number, number, number]> = {
      shoot: [150, 90, 0.055],
      hit: [260, 150, 0.075],
      reward: [420, 720, 0.22],
      ritual: [230, 520, 0.34],
      danger: [95, 72, 0.28],
      kill: [110, 55, 0.12],
      collect: [440 * Math.pow(2, this.collectNote / 12), 660 * Math.pow(2, this.collectNote / 12), 0.1],
    };
    const [start, end, duration] = frequencies[cue];
    oscillator.type = cue === "danger" ? "sawtooth" : cue === "reward" || cue === "ritual" ? "sine" : "triangle";
    oscillator.frequency.setValueAtTime(start, now);
    oscillator.frequency.exponentialRampToValueAtTime(Math.max(1, end), now + duration);
    gain.gain.setValueAtTime(cue === "shoot" ? 0.055 : 0.075, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    oscillator.connect(gain).connect(this.context.destination);
    this.voices++;
    oscillator.onended = () => { this.voices--; oscillator.disconnect(); gain.disconnect(); };
    oscillator.start(now);
    oscillator.stop(now + duration);
  }
}
