interface Point {
  x: number;
  y: number;
}

export class RitualCanvas {
  private readonly context: CanvasRenderingContext2D;
  private points: Point[] = [];
  private trace: Point[] = [];
  private nextPoint = 0;
  private drawing = false;
  private readonly resizeObserver: ResizeObserver;

  public constructor(private readonly canvas: HTMLCanvasElement, private readonly onComplete: () => void) {
    const context = canvas.getContext("2d");
    if (!context) throw new Error("Ritual canvas is unavailable");
    this.context = context;
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(canvas);
    canvas.addEventListener("pointerdown", (event) => this.pointerDown(event));
    canvas.addEventListener("pointermove", (event) => this.pointerMove(event));
    canvas.addEventListener("pointerup", () => this.pointerUp());
    canvas.addEventListener("pointercancel", () => this.pointerUp());
    this.resize();
  }

  private resize(): void {
    const rect = this.canvas.getBoundingClientRect();
    const ratio = Math.min(window.devicePixelRatio, 2);
    this.canvas.width = Math.max(1, Math.round(rect.width * ratio));
    this.canvas.height = Math.max(1, Math.round(rect.height * ratio));
    this.context.setTransform(ratio, 0, 0, ratio, 0, 0);
    const width = rect.width;
    const height = rect.height;
    this.points = [
      { x: width * 0.5, y: height * 0.14 },
      { x: width * 0.18, y: height * 0.78 },
      { x: width * 0.85, y: height * 0.34 },
      { x: width * 0.15, y: height * 0.34 },
      { x: width * 0.82, y: height * 0.78 },
    ];
    this.draw();
  }

  private localPoint(event: PointerEvent): Point {
    const rect = this.canvas.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }

  private near(point: Point, target: Point): boolean {
    return Math.hypot(point.x - target.x, point.y - target.y) <= 28;
  }

  private pointerDown(event: PointerEvent): void {
    const point = this.localPoint(event);
    if (!this.near(point, this.points[0]!)) return;
    this.canvas.setPointerCapture(event.pointerId);
    this.drawing = true;
    this.nextPoint = 1;
    this.trace = [this.points[0]!];
    this.draw();
  }

  private pointerMove(event: PointerEvent): void {
    if (!this.drawing) return;
    const point = this.localPoint(event);
    this.trace.push(point);
    const target = this.points[this.nextPoint];
    if (target && this.near(point, target)) {
      this.trace.push(target);
      this.nextPoint += 1;
      if (this.nextPoint >= this.points.length) {
        this.drawing = false;
        this.draw();
        window.setTimeout(this.onComplete, 180);
        return;
      }
    }
    this.draw();
  }

  private pointerUp(): void {
    if (!this.drawing) return;
    this.drawing = false;
    this.nextPoint = 0;
    this.trace = [];
    this.canvas.classList.remove("ritual-failed");
    void this.canvas.offsetWidth;
    this.canvas.classList.add("ritual-failed");
    this.draw();
  }

  private draw(): void {
    const rect = this.canvas.getBoundingClientRect();
    this.context.clearRect(0, 0, rect.width, rect.height);
    this.context.strokeStyle = "rgba(217, 232, 228, 0.18)";
    this.context.lineWidth = 1;
    this.context.beginPath();
    for (let index = 0; index < this.points.length; index += 1) {
      const point = this.points[index]!;
      if (index === 0) this.context.moveTo(point.x, point.y);
      else this.context.lineTo(point.x, point.y);
    }
    this.context.stroke();

    if (this.trace.length > 1) {
      this.context.strokeStyle = "#d5c16f";
      this.context.lineWidth = 4;
      this.context.lineCap = "round";
      this.context.lineJoin = "round";
      this.context.beginPath();
      this.context.moveTo(this.trace[0]!.x, this.trace[0]!.y);
      for (const point of this.trace.slice(1)) this.context.lineTo(point.x, point.y);
      this.context.stroke();
    }
    for (let index = 0; index < this.points.length; index += 1) {
      const point = this.points[index]!;
      const visited = index < this.nextPoint;
      this.context.fillStyle = visited ? "#d5c16f" : "#dbe8e5";
      this.context.strokeStyle = visited ? "#fff1a8" : "#4e8f7e";
      this.context.lineWidth = 3;
      this.context.beginPath();
      this.context.arc(point.x, point.y, visited ? 11 : 9, 0, Math.PI * 2);
      this.context.fill();
      this.context.stroke();
      this.context.fillStyle = "#101614";
      this.context.font = "700 12px sans-serif";
      this.context.textAlign = "center";
      this.context.textBaseline = "middle";
      this.context.fillText(String(index + 1), point.x, point.y + 0.5);
    }
  }
}
