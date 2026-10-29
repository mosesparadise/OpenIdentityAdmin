import { observe, type SessionState } from "./observation";

const maximumTimerDelay = 2_147_483_647;
/** Owns one ephemeral observation; generation and lifecycle fences reject late reads without renewing server state. */
export class SessionController {
  private generation = 0;
  private abort?: AbortController;
  private timer?: ReturnType<typeof setTimeout>;
  private stopped = false;
  private state: SessionState = { kind: "loading" };
  constructor(private readonly load: (signal: AbortSignal) => Promise<unknown>,
    private readonly publish: (value: SessionState) => void, private readonly now = Date.now) {}

  /** Starts one read, clearing identity first; failed or superseded requests cannot retain or restore it. */
  async check(): Promise<void> {
    if (this.stopped) return;
    this.cancel();
    const version = ++this.generation;
    const request = new AbortController();
    this.abort = request;
    this.emit({ kind: "loading" });
    try {
      const response = await this.load(request.signal);
      if (this.stopped || request.signal.aborted || version !== this.generation) return;
      this.emit(observe(response, this.now()));
      this.armExpiry();
    } catch {
      if (!this.stopped && !request.signal.aborted && version === this.generation) this.emit({ kind: "unavailable" });
    }
  }

  /** Rechecks local time on visibility/pageshow; throttled timers must not preserve an expired identity. */
  checkDeadline(): void {
    if (this.state.kind === "authenticated" && this.state.expires <= this.now()) {
      this.cancel();
      ++this.generation;
      this.emit({ kind: "elapsed" });
    }
  }

  /** Clears identity before BFCache capture and aborts pending reads; a restore requires a fresh observation. */
  suspend(): void {
    this.cancel();
    ++this.generation;
    this.emit({ kind: "loading" });
  }

  /** Permanently closes this mounted owner so a late completion cannot update a replacement page. */
  dispose(): void {
    this.stopped = true;
    this.cancel();
    ++this.generation;
  }

  /** Cancels only this read and local display timer; it performs no server authentication mutation. */
  private cancel(): void { this.abort?.abort(); clearTimeout(this.timer); }
  /** Publishes a minimal state with no retained wire response or credential-bearing exception. */
  private emit(state: SessionState): void { this.state = state; this.publish(state); }
  /** Removes the observation at its original deadline; never polls or extends a server session. */
  private armExpiry(): void {
    if (this.state.kind !== "authenticated") return;
    this.timer = setTimeout(() => { this.checkDeadline(); this.armExpiry(); },
      Math.min(maximumTimerDelay, Math.max(0, this.state.expires - this.now())));
  }
}
