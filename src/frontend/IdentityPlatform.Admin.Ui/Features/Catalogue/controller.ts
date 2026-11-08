import type { CatalogueClient } from "./client";
import { validate, type Kind, type Details, type Draft, type Errors, type Page, type Job } from "./input";
import { details, page, job, expiry, type Result } from "./observation";
import { object, reason, type Reason } from "../Applications/Editor/observation";
import type { SessionState } from "../Session/observation";
export type CatalogueState = { kind: "idle" | "loading" | "working" } | { kind: "list"; page: Page; search: string } |
  { kind: "details"; details: Details } | { kind: "form"; base?: Details; audiences?: Page; choicesError?: Reason } |
  { kind: "confirm"; details: Details } | { kind: "job"; job: Job } | { kind: "problem"; reason: Reason; uncertain: boolean; recoverable: boolean; job?: Job };
const pollInterval = 1500;
class ProofFailure extends Error { constructor(readonly status: number) { super("Catalogue proof unavailable"); } }
/** Owns ephemeral catalogue state, exact pending intent and one nonrenewing observation lifetime. */
export class CatalogueController {
  state: CatalogueState = { kind: "idle" };
  private abort?: AbortController;
  private generation = 0;
  private deadline = 0;
  private observed = 0;
  private subject?: string;
  private expiryTimer?: ReturnType<typeof setTimeout>;
  private poll?: ReturnType<typeof setTimeout>;
  private pending?: { id: string; version: string; key: string };
  private knownJob?: Job;
  private disposed = false;
  constructor(private readonly kind: Kind, private readonly client: CatalogueClient, private readonly publish: (state: CatalogueState) => void,
    private readonly now = Date.now, private readonly key = () => crypto.randomUUID()) { }
  /** Accepts a currently checked session but never extends an existing observation through status refresh. */
  authorize(session: SessionState): void {
    if (session.kind !== "authenticated" || session.expires <= this.now()) { this.subject = undefined; this.suspend(); return; }
    if (this.subject !== session.subject) { this.cancel(); this.pending = undefined; this.knownJob = undefined; this.emit({ kind: "idle" }); this.subject = session.subject; this.deadline = session.expires; this.observed = this.now(); }
    else this.deadline = Math.min(this.deadline, session.expires);
    this.arm();
  }
  /** Loads one bounded page explicitly; stale responses never replace a newer navigation. */
  async list(search = "", number = 1): Promise<void> {
    if (search.length > 100 || /[\u0000-\u001f\u007f-\u009f]/u.test(search) || !Number.isInteger(number) || number < 1 || number > 10000) return;
    this.pending = undefined; this.knownJob = undefined;
    await this.read(async signal => {
      const result = await this.client.list(this.kind, search, number, signal), value = page(result, this.kind, search, number, this.now());
      return value ? { kind: "list", page: value, search } : this.failure(result);
    });
  }
  /** Reads current detail/version independently; no previous draft is automatically resubmitted. */
  async details(id: string): Promise<void> {
    this.pending = undefined; this.knownJob = undefined;
    await this.read(async signal => {
      const result = await this.client.details(this.kind, id, signal), value = result.status === 200 ? details(result, this.kind, id, this.now()) : null;
      return value ? { kind: "details", details: value } : this.failure(result);
    });
  }
  /** Opens a blank or exact current editor without manufacturing a fresh resource version. */
  form(edit = false): void {
    if (!this.current() || edit && this.state.kind !== "details") return;
    const base = edit && this.state.kind === "details" ? this.state.details : undefined;
    this.cancel(); this.pending = undefined; this.emit({ kind: "form", base });
    if (this.kind === "scope") void this.choices();
  }
  /** Fetches a bounded searchable audience page while retaining the form and its original authority ceiling. */
  async choices(search = ""): Promise<void> {
    if (this.state.kind !== "form" || !this.current() || search.length > 100) return;
    const base = this.state.base, operation = this.begin();
    try {
      const result = await this.client.list("audience", search, 1, operation.signal, 100);
      if (!this.live(operation.generation)) return;
      const value = page(result, "audience", search, 1, this.now(), 100);
      this.emit(value ? this.bound(value, { kind: "form", base, audiences: value }) : { kind: "form", base, choicesError: reason(result.status) });
    } catch { if (this.live(operation.generation)) this.emit({ kind: "form", base, choicesError: "unavailable" }); }
  }
  /** Sends a validated create/edit once after native proof acquisition, preserving original preconditions across both awaits. */
  async save(value: Draft): Promise<Errors> {
    const checked = validate(this.kind, value);
    if (!checked.input || this.state.kind !== "form" || !this.current()) return checked.errors;
    const input = checked.input, base = this.state.base, operation = this.begin(); let dispatched = false;
    this.emit({ kind: "working" });
    try {
      const proof = await this.proof(operation.signal);
      if (!this.live(operation.generation)) return {};
      dispatched = true;
      const result = await this.client.save(this.kind, input, proof, operation.signal, base?.id, base?.version);
      if (!this.live(operation.generation)) return {};
      const value = result.status === (base ? 200 : 201) ? details(result, this.kind, base?.id, this.now(), input) : null;
      this.emit(value && (!base || value.version !== base.version) ? this.bound(value, { kind: "details", details: value }) : this.failure(result, result.status >= 500 || result.status < 300));
    } catch (error) { if (this.live(operation.generation)) this.emit(this.failure({ status: error instanceof ProofFailure ? error.status : 503 }, dispatched)); }
    return {};
  }
  /** Requires an explicit confirmation screen and currently unreferenced details before freezing deletion intent. */
  confirm(): void {
    if (!this.current() || this.state.kind !== "details" || this.state.details.referenced) return;
    const value = this.state.details; this.cancel(); this.emit({ kind: "confirm", details: value });
  }
  /** Creates one fresh request identity only for a newly confirmed deletion; recovery reuses the exact original intent. */
  async remove(confirmed: boolean): Promise<void> {
    if (!confirmed || !this.current() || this.state.kind !== "confirm") return;
    this.pending = { id: this.state.details.id, version: this.state.details.version, key: this.key() };
    await this.sendDeletion();
  }
  /** Explicitly recovers ambiguous acceptance with the same key/version; no timer or failed response triggers mutation retry. */
  async recover(): Promise<void> { if (this.pending && this.state.kind === "problem" && this.state.recoverable) await this.sendDeletion(); }
  /** Sends the frozen original deletion at most once per explicit user action, then polls only read-only status. */
  private async sendDeletion(): Promise<void> {
    if (!this.pending || !this.current()) return;
    const wasUncertain = this.state.kind === "problem" && this.state.uncertain;
    const intent = this.pending, operation = this.begin(); let dispatched = false;
    this.emit({ kind: "working" });
    try {
      const proof = await this.proof(operation.signal);
      if (!this.live(operation.generation)) return;
      dispatched = true;
      const result = await this.client.remove(this.kind, intent.id, intent.version, intent.key, proof, operation.signal);
      if (!this.live(operation.generation)) return;
      const value = result.status === 202 ? job(result, this.kind, intent.id, this.now()) : null;
      if (!value) { this.emit(this.failure(result, wasUncertain || result.status >= 500 || result.status < 300)); return; }
      this.pending = undefined; this.showJob(value);
    } catch (error) { if (this.live(operation.generation)) this.emit(this.failure({ status: error instanceof ProofFailure ? error.status : 503 }, dispatched || wasUncertain)); }
  }
  /** Returns whether this status response still owns the view; stale reads cannot authorize focus changes or renew observation. */
  async refresh(): Promise<boolean> {
    if (!this.current() || !this.knownJob) return false;
    const original = this.knownJob, operation = this.begin();
    try {
      const result = await this.client.status(this.kind, original.id, operation.signal);
      if (!this.live(operation.generation)) return false;
      const value = result.status === 200 ? job(result, this.kind, original.targetId, this.now(), original.id) : null;
      if (value && value.createdAt === original.createdAt && Date.parse(value.observedAt) >= Date.parse(original.observedAt) &&
          (original.state === "queued" || value.state === original.state && value.failure === original.failure)) this.showJob(value); else this.emit(this.failure(result));
      return true;
    } catch { if (this.live(operation.generation)) { this.emit(this.failure({ status: 503 })); return true; } return false; }
  }
  /** Clears a hidden or expired view synchronously; returning does not replay a read or mutation. */
  suspend(): void { this.cancel(); clearTimeout(this.expiryTimer); this.subject = undefined; this.pending = undefined; this.knownJob = undefined; this.deadline = 0; this.emit({ kind: "problem", reason: "cleared", uncertain: false, recoverable: false }); }
  /** Applies rollback/equality checks even when a background timer was throttled. */
  checkDeadline(): void { this.current(); }
  /** Releases local ownership without publishing after React unmount. */
  dispose(): void { this.disposed = true; this.cancel(); clearTimeout(this.expiryTimer); this.pending = undefined; }
  private async read(work: (signal: AbortSignal) => Promise<CatalogueState>): Promise<void> {
    if (!this.current()) return;
    const operation = this.begin(); this.emit({ kind: "loading" });
    try { const value = await work(operation.signal); if (this.live(operation.generation)) this.emit(value.kind === "list" ? this.bound(value.page, value) : value.kind === "details" ? this.bound(value.details, value) : value); }
    catch { if (this.live(operation.generation)) this.emit(this.failure({ status: 503 })); }
  }
  private async proof(signal: AbortSignal): Promise<string> {
    const result = await this.client.token(this.kind, signal);
    if (result.status !== 200 || !object(result.value, ["requestToken"]) || typeof result.value.requestToken !== "string" || !/^[A-Za-z0-9_-]{1,4096}$/.test(result.value.requestToken)) throw new ProofFailure(result.status === 200 ? 503 : result.status);
    return result.value.requestToken;
  }
  private bound(value: { expiresAt: string }, state: CatalogueState): CatalogueState {
    this.deadline = Math.min(this.deadline, expiry(value.expiresAt, this.now()));
    if (!this.current()) return this.state;
    this.arm(); return state;
  }
  private showJob(value: Job): void { this.knownJob = value; this.emit(this.bound(value, { kind: "job", job: value })); if (this.state.kind === "job" && value.state === "queued") this.poll = setTimeout(() => { void this.refresh(); }, pollInterval); }
  private failure(result: Result, uncertain = false): CatalogueState { return { kind: "problem", reason: reason(result.status), uncertain, recoverable: uncertain && !!this.pending, job: this.knownJob }; }
  private begin() { this.cancel(); this.abort = new AbortController(); return { generation: this.generation, signal: this.abort.signal }; }
  private cancel(): void { ++this.generation; this.abort?.abort(); clearTimeout(this.poll); }
  private live(generation: number): boolean { return this.current() && generation === this.generation; }
  private current(): boolean {
    if (this.disposed) return false;
    const now = this.now();
    if (!this.subject || now < this.observed || now >= this.deadline || !Number.isFinite(this.deadline)) {
      if (this.subject) this.suspend(); return false;
    }
    this.observed = now; return true;
  }
  private arm(): void { clearTimeout(this.expiryTimer); this.expiryTimer = setTimeout(() => this.checkDeadline(), Math.max(0, this.deadline - this.now())); }
  private emit(value: CatalogueState): void { if (!this.disposed) { this.state = value; this.publish(value); } }
}
