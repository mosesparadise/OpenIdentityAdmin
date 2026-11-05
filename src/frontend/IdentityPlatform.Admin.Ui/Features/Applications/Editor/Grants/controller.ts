import { object, reason, type Observed, type Reason } from "../observation";
import { utc } from "../input";
import { accepted, page, type Job, type GrantInput } from "./observation";
import type { GrantClient } from "./client";
export type GrantState =
  | { kind: "loading" | "sending" | "cleared" }
  | { kind: "ready"; jobs: Job[] }
  | { kind: "problem"; reason: Reason; uncertain: boolean };
type Intent = { key: string; input: GrantInput; jobId?: string };
const interval = 2000;
/** Owns ephemeral confirmation/progress and one original access deadline. Automatic work is read-only. */
export class GrantController {
  private state: GrantState = { kind: "loading" };
  private generation = 0;
  private abort?: AbortController;
  private poll?: ReturnType<typeof setTimeout>;
  private expiry?: ReturnType<typeof setTimeout>;
  private disposed = false;
  private intent?: Intent;
  private permitted = false;
  private deadline: number;
  constructor(private client: GrantClient, private base: Observed, private publish: (value: GrantState) => void,
    private now = Date.now, private newKey = () => crypto.randomUUID()) {
    this.deadline = base.expires;
    this.expiry = setTimeout(() => this.checkDeadline(), Math.max(0, this.deadline - this.now()));
  }
  /** Reads current permissions and committed status; failed polling stops until an explicit user refresh. */
  async refresh(): Promise<void> {
    if (this.disposed || this.state.kind === "sending" || this.intent) return;
    const operation = this.begin(); this.emit({ kind: "loading" });
    try {
      const result = await this.client.recent(this.base.details.id, operation.signal);
      if (!this.live(operation.generation)) return;
      if (result.status !== 200) { this.fail(reason(result.status)); return; }
      const jobs = await page(result, this.base.details.id, this.now());
      if (!this.live(operation.generation)) return;
      if (!jobs) { this.fail("unavailable"); return; }
      this.deadline = Math.min(this.deadline, utc((result.value as { expiresAt: string }).expiresAt));
      this.arm(); this.permitted = true; this.emit({ kind: "ready", jobs });
      if (jobs.some(job => job.state === "queued" || job.state === "processing")) this.poll = setTimeout(() => { void this.refresh(); }, interval);
    } catch { if (this.live(operation.generation)) this.fail("unavailable"); }
  }
  /** Creates one new intent only after current read permission and explicit finite confirmation; recovery retains the same scope. */
  async submit(reason: string, confirm: boolean, jobId?: string): Promise<boolean> {
    if (!this.permitted || this.state.kind !== "ready" || this.base.details.enabled !== false || !this.base.etag ||
        confirm !== true || !["maintenance", "security", "configuration"].includes(reason)) return false;
    const incomplete = this.state.jobs.find(job => job.state !== "succeeded");
    if (jobId ? !incomplete || incomplete.id !== jobId || incomplete.state !== "stopped" : !!incomplete) return false;
    this.intent = { key: this.newKey(), input: { reason: reason as GrantInput["reason"], confirm: true, ...(incomplete ? { jobVersion: incomplete.version } : {}) }, jobId };
    await this.send(); return true;
  }
  /** Explicitly checks ambiguous acceptance using the original intent/key and fresh CSRF; never invents another job. */
  async recover(): Promise<void> {
    if (this.state.kind === "problem" && this.state.uncertain && this.intent) await this.send();
  }
  /** Retains exact payload and application version for idempotent recovery without replaying browser credentials. */
  private async send(): Promise<void> {
    if (!this.intent || !this.base.etag || this.disposed) return;
    const intent = this.intent, wasUncertain = this.state.kind === "problem" && this.state.uncertain, operation = this.begin();
    let dispatched = false; this.emit({ kind: "sending" });
    try {
      const proof = await this.client.token(operation.signal);
      if (!this.live(operation.generation)) return;
      if (proof.status !== 200 || !object(proof.value, ["requestToken"]) || typeof proof.value.requestToken !== "string" ||
          !/^[A-Za-z0-9_-]{1,4096}$/.test(proof.value.requestToken)) { if (!wasUncertain) this.intent = undefined; this.fail(proof.status === 200 ? "unavailable" : reason(proof.status), wasUncertain); return; }
      dispatched = true;
      const result = await this.client.submit(this.base.details.id, this.base.etag, intent.key, intent.input, proof.value.requestToken, operation.signal, intent.jobId);
      if (!this.live(operation.generation)) return;
      if (result.status !== 202) {
        const uncertain = wasUncertain || result.status >= 500;
        if (!uncertain) this.intent = undefined;
        this.fail(reason(result.status), uncertain); return;
      }
      const value = await accepted(result, this.base.details.id, intent.key, this.now(), intent.jobId);
      if (!this.live(operation.generation)) return;
      if (!value) { this.fail("unavailable", true); return; }
      this.intent = undefined; this.deadline = Math.min(this.deadline, utc(value.expiresAt)); this.arm();
      this.emit({ kind: "ready", jobs: [value] });
      if (wasUncertain) { await this.refresh(); return; }
      if (value.state === "queued" || value.state === "processing") this.poll = setTimeout(() => { void this.refresh(); }, interval);
    } catch { if (this.live(operation.generation)) { if (!dispatched && !wasUncertain) this.intent = undefined; this.fail("unavailable", dispatched || wasUncertain); } }
  }
  /** Clears data at the original authority deadline, including when browser timers were throttled. */
  checkDeadline(): void { if (!this.disposed && this.now() >= this.deadline) this.suspend(); }
  /** Synchronously erases jobs and pending keys before visibility/history capture; returning does not replay a request. */
  suspend(): void { this.cancel(); clearTimeout(this.expiry); this.intent = undefined; this.permitted = false; this.emit({ kind: "cleared" }); this.disposed = true; }
  /** Drains local ownership on navigation without publishing into an unmounted React component. */
  dispose(): void { this.cancel(); clearTimeout(this.expiry); this.intent = undefined; this.permitted = false; this.disposed = true; }
  private begin() { this.cancel(); this.abort = new AbortController(); return { generation: this.generation, signal: this.abort.signal }; }
  private live(generation: number): boolean { this.checkDeadline(); return !this.disposed && this.generation === generation; }
  private cancel(): void { ++this.generation; this.abort?.abort(); clearTimeout(this.poll); }
  private fail(reason: Reason, uncertain = false): void { this.cancel(); this.permitted = false; this.emit({ kind: "problem", reason, uncertain }); }
  private emit(value: GrantState): void { this.state = value; this.publish(value); }
  private arm(): void { clearTimeout(this.expiry); this.expiry = setTimeout(() => this.checkDeadline(), Math.max(0, this.deadline - this.now())); }
}
