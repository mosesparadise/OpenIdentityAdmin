import { observe, type ApplicationState, type Query, type Result } from "./observation";

/** Owns ephemeral list data and rejects superseded, suspended and expired responses. */
export class ApplicationController {
  private generation = 0;
  private abort?: AbortController;
  private timer?: ReturnType<typeof setTimeout>;
  private stopped = false;
  private state: ApplicationState = { kind: "loading" };
  constructor(private readonly load: (query: Query, signal: AbortSignal) => Promise<Result>,
    private readonly publish: (state: ApplicationState) => void, private readonly now = Date.now) {}

  /** Clears the previous collection before a bounded read; changing filters cannot resurrect an older page. */
  async check(query: Query): Promise<void> {
    if (this.stopped) return;
    this.cancel();
    const version = ++this.generation;
    const request = new AbortController();
    this.abort = request;
    this.emit({ kind: "loading" });
    try {
      const result = await this.load(query, request.signal);
      if (this.stopped || request.signal.aborted || version !== this.generation) return;
      this.emit(observe(result, query, this.now()));
      this.armExpiry();
    } catch {
      if (!this.stopped && !request.signal.aborted && version === this.generation) this.emit({ kind: "unavailable" });
    }
  }

  /** Clears collection data when background timer throttling delayed the original deadline. */
  checkDeadline(): void {
    if (this.state.kind === "ready" && this.state.expires <= this.now()) {
      this.cancel(); ++this.generation; this.emit({ kind: "expired" });
    }
  }
  /** Removes protected data before BFCache capture; restoration requires another server read. */
  suspend(): void { this.cancel(); ++this.generation; this.emit({ kind: "loading" }); }
  /** Closes this owner permanently without mutating any server session. */
  dispose(): void { this.stopped = true; this.cancel(); ++this.generation; }
  /** Cancels the current network read and display timer together. */
  private cancel(): void { this.abort?.abort(); clearTimeout(this.timer); }
  /** Retains only the checked public projection, never a raw response or exception. */
  private emit(state: ApplicationState): void { this.state = state; this.publish(state); }
  /** Enforces the original server deadline without polling or token refresh. */
  private armExpiry(): void {
    if (this.state.kind !== "ready") return;
    this.timer = setTimeout(() => { this.checkDeadline(); this.armExpiry(); }, Math.max(0, this.state.expires - this.now()));
  }
}
