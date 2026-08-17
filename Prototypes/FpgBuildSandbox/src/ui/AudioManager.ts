export type SoundCue = "shoot" | "hit" | "reward" | "ritual" | "danger";

export class AudioManager {
  private context?: AudioContext;
  private muted = localStorage.getItem("fpg-build-sandbox:muted") === "true";

  public isMuted(): boolean {
    return this.muted;
  }

  public toggle(): boolean {
    this.muted = !this.muted;
    localStorage.setItem("fpg-build-sandbox:muted", String(this.muted));
    return this.muted;
  }

  public play(cue: SoundCue): void {
    if (this.muted) return;
    this.context ??= new AudioContext();
    const now = this.context.currentTime;
    const oscillator = this.context.createOscillator();
    const gain = this.context.createGain();
    const frequencies: Record<SoundCue, [number, number, number]> = {
      shoot: [150, 90, 0.055],
      hit: [260, 150, 0.075],
      reward: [420, 720, 0.22],
      ritual: [230, 520, 0.34],
      danger: [95, 72, 0.28],
    };
    const [start, end, duration] = frequencies[cue];
    oscillator.type = cue === "danger" ? "sawtooth" : cue === "reward" || cue === "ritual" ? "sine" : "triangle";
    oscillator.frequency.setValueAtTime(start, now);
    oscillator.frequency.exponentialRampToValueAtTime(Math.max(1, end), now + duration);
    gain.gain.setValueAtTime(cue === "shoot" ? 0.055 : 0.075, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    oscillator.connect(gain).connect(this.context.destination);
    oscillator.start(now);
    oscillator.stop(now + duration);
  }
}
