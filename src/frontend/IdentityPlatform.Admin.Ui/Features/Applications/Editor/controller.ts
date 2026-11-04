import type { SessionState } from "../../Session/observation";
import type { EditorClient } from "./client";
import { validate, type Draft, type Errors, type EditDraft, type ReplacementDraft, validateEdit, validateReplacement, utc } from "./input";
import { created, edited, replaced, object, observe, reason, type Observed, type Reason } from "./observation";
export type Outcome = "not-sent" | "unknown" | "created" | "updated" | "replaced";
export type EditorState =
  | { kind: "list" | "form" }
  | { kind: "working"; operation: "read" | "create" | "edit" | "replace" }
  | ({ kind: "details" | "edit-form" | "replacement-form" | "updated" } & Observed)
  | ({ kind: "created" | "replaced"; secret: string } & Observed)
  | { kind: "problem"; reason: Reason; outcome: Outcome; target?: { id: string; action: "edit" | "replace" } };

/** Owns one ephemeral editor operation and never retries a mutation or retains a credential-bearing wire DTO. */
export class EditorController {
  private state: EditorState = { kind: "list" };
  private session?: { subject: string; expires: number };
  private generation = 0;
  private abort?: AbortController;
  private timer?: ReturnType<typeof setTimeout>;
  private stopped = false;
  private dispatched = false;
  private deadline?: number;
  private completed?: Outcome;
  private target?: { id: string; action: "edit" | "replace" };
  constructor(private readonly client: EditorClient, private readonly publish: (state: EditorState) => void, private readonly now = Date.now) {}

  /** Binds the editor to the actual session observation; the BFF independently owns and verifies its durable browser generation. */
  authorize(value: SessionState): void {
    if (this.stopped) return;
    const next = value.kind === "authenticated" && value.expires > this.now() ? { subject: value.subject, expires: value.expires } : undefined;
    if (this.session?.subject !== next?.subject || this.session?.expires !== next?.expires) {
      const outcome = this.outcome(); this.cancel();
      if (this.state.kind !== "list") this.emit({ kind: "problem", reason: "expired", outcome, ...(this.target ? { target: this.target } : {}) });
    }
    this.session = next;
  }
  /** Starts only an explicit new form; list-read success never grants write authority. */
  newForm(): void {
    if (this.stopped) return;
    if (this.dispatched) { this.fail("cleared", "unknown"); return; }
    this.cancel(); this.target = undefined; this.completed = undefined;
    if (!this.currentSession()) { this.emit({ kind: "problem", reason: "expired", outcome: "not-sent" }); return; }
    this.emit({ kind: "form" }); this.arm(this.session!.expires);
  }
  /** Discards all editor data before returning to a fresh list view. */
  list(): void { if (!this.stopped) { if (this.dispatched) { this.fail("cleared", "unknown"); return; } this.cancel(); this.target = undefined; this.completed = undefined; this.emit({ kind: "list" }); } }
  /** Reads only sanitized details and suppresses every late response, including asynchronous ETag verification. */
  async details(id: string): Promise<void> {
    if (this.stopped) return;
    if (this.dispatched) { this.fail("cleared", "unknown"); return; }
    this.cancel(); this.target = undefined; this.completed = undefined;
    if (!this.currentSession()) { this.emit({ kind: "problem", reason: "expired", outcome: "not-sent" }); return; }
    const operation = this.begin("read");
    try {
      const result = await this.client.details(id, operation.signal);
      if (!this.live(operation.version)) return;
      if (result.status !== 200) { this.fail(reason(result.status), "not-sent"); return; }
      const value = await observe(result, id, this.now());
      if (!this.live(operation.version)) return;
      if (!value || value.expires <= this.now()) { this.fail("unavailable", "not-sent"); return; }
      const expires = Math.min(value.expires, this.session!.expires);
      this.emit({ ...value, expires, kind: "details" }); this.arm(expires);
    } catch { if (this.live(operation.version)) this.fail("unavailable", "not-sent"); }
  }
  /** Sets the single-send fence before token acquisition; any post-dispatch non-success remains potentially committed. */
  async create(draft: Draft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "form") return {};
    const checked = validate({ ...draft }, this.now());
    if (!checked.input) return checked.errors;
    if (!this.currentSession()) { this.fail("expired", "not-sent"); return {}; }
    const operation = this.begin("create");
    try {
      const proof = await this.client.token(operation.signal);
      if (!this.live(operation.version)) return {};
      if (proof.status !== 200) { this.fail(reason(proof.status), "not-sent"); return {}; }
      if (!object(proof.value, ["requestToken"]) || typeof proof.value.requestToken !== "string" ||
          !/^[A-Za-z0-9_-]{1,4096}$/.test(proof.value.requestToken)) { this.fail("unavailable", "not-sent"); return {}; }
      if (!(utc(checked.input.credentialExpiresAt, true) > this.now())) { this.fail("invalid", "not-sent"); return {}; }
      this.dispatched = true;
      const result = await this.client.create(checked.input, proof.value.requestToken, operation.signal);
      if (!this.live(operation.version)) return {};
      if (result.status !== 201) { this.fail(reason(result.status), "unknown"); return {}; }
      const value = await created(result, checked.input, this.now());
      if (!this.live(operation.version)) return {};
      if (!value) { this.fail("unavailable", "unknown"); return {}; }
      const expires = Math.min(value.expires, this.session!.expires, utc(value.details.credential!.expiresAt));
      if (expires <= this.now()) { this.fail("display-expired", "unknown"); return {}; }
      this.dispatched = false; this.completed = "created"; this.emit({ ...value, expires, kind: "created" }); this.arm(expires);
    } catch { if (this.live(operation.version)) this.fail("unavailable", this.dispatched ? "unknown" : "not-sent"); }
    return {};
  }
  /** Opens an explicit mutation form while dropping secret output and preserving original observation authority. */
  open(action: "edit" | "replace"): void {
    this.checkDeadline();
    if (this.stopped || !["details", "created", "updated", "replaced"].includes(this.state.kind) || !("details" in this.state) || !this.state.details.editable) return;
    const { details, expires, etag } = this.state;
    this.cancel(); this.completed = undefined; this.target = { id: details.id, action };
    this.emit({ kind: action === "edit" ? "edit-form" : "replacement-form", details: structuredClone(details), expires, etag }); this.arm(expires);
  }
  /** Cancels a draft without another read or restoring a previously displayed secret. */
  cancelForm(): void {
    this.checkDeadline();
    if (this.state.kind !== "edit-form" && this.state.kind !== "replacement-form") return;
    const { details, expires, etag } = this.state;
    this.emit({ kind: "details", details, expires, etag });
  }
  /** Submits only presentation changes against the original strong version. */
  async edit(draft: EditDraft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "edit-form") return {};
    const checked = validateEdit({ ...draft });
    if (!checked.input) return checked.errors;
    const input = checked.input, base = this.state;
    return this.mutate(base, "edit", (token, signal) => this.client.edit(base.details.id, base.etag!, input, token, signal),
      result => edited(result, base, input, this.now()));
  }
  /** Replaces a secret once, with explicit confirmation and a newly chosen future expiry. */
  async replace(draft: ReplacementDraft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "replacement-form") return {};
    const checked = validateReplacement({ ...draft }, this.now());
    if (!checked.input) return checked.errors;
    const input = checked.input, base = this.state;
    return this.mutate(base, "replace", (token, signal) => this.client.replace(base.details.id, base.etag!, input, token, signal),
      result => replaced(result, base, input, this.now()), utc(input.credentialExpiresAt, true));
  }
  /** Owns a single update dispatch, binding every asynchronous boundary to the unchanged base deadline. */
  private async mutate(base: Observed, action: "edit" | "replace",
    send: (token: string, signal: AbortSignal) => Promise<import("./observation").Result>,
    inspect: (result: import("./observation").Result) => Promise<(Observed & { secret?: string }) | null>, secretExpiry?: number): Promise<Errors> {
    this.checkDeadline();
    if (this.state.kind === "problem") return {};
    const operation = this.begin(action, Math.min(base.expires, this.session!.expires));
    try {
      const proof = await this.client.token(operation.signal);
      if (!this.live(operation.version)) return {};
      if (proof.status !== 200) { this.fail(reason(proof.status), "not-sent"); return {}; }
      if (!object(proof.value, ["requestToken"]) || typeof proof.value.requestToken !== "string" || !/^[A-Za-z0-9_-]{1,4096}$/.test(proof.value.requestToken)) { this.fail("unavailable", "not-sent"); return {}; }
      if (secretExpiry !== undefined && secretExpiry <= this.now()) { this.fail("invalid", "not-sent"); return {}; }
      this.dispatched = true;
      const result = await send(proof.value.requestToken, operation.signal);
      if (!this.live(operation.version)) return {};
      if (result.status !== 200) { this.fail(reason(result.status), "unknown"); return {}; }
      const value = await inspect(result);
      if (!this.live(operation.version)) return {};
      if (!value) { this.fail("unavailable", "unknown"); return {}; }
      const expires = Math.min(base.expires, value.expires, this.session!.expires, secretExpiry ?? Infinity);
      if (expires <= this.now()) { this.fail("display-expired", "unknown"); return {}; }
      this.dispatched = false; this.completed = value.secret === undefined ? "updated" : "replaced";
      this.emit(value.secret === undefined ? { ...value, expires, kind: "updated" } : { ...value, secret: value.secret, expires, kind: "replaced" }); this.arm(expires);
    } catch { if (this.live(operation.version)) this.fail("unavailable", this.dispatched ? "unknown" : "not-sent"); }
    return {};
  }
  /** Drops the secret reference and removes its view, retaining only a separately validated sanitized projection. */
  dismiss(): void {
    this.checkDeadline();
    if (this.state.kind !== "created" && this.state.kind !== "replaced") return;
    const { details, expires, etag } = this.state;
    this.emit({ kind: "details", details, expires, etag });
  }
  /** Checks original authority/output deadlines after throttling; it never refreshes the session or retries a request. */
  checkDeadline(): void {
    if (this.stopped || this.state.kind === "list") return;
    const deadline = "expires" in this.state ? this.state.expires : this.deadline ?? this.session?.expires ?? 0;
    if (!this.currentSession()) this.fail("expired", this.outcome());
    else if (deadline <= this.now()) this.fail("display-expired", this.outcome());
  }
  /** Clears data synchronously before hiding/history capture, invalidating even transports that ignore cancellation. */
  suspend(): void {
    if (this.stopped) return;
    const outcome = this.outcome(); this.cancel();
    if (this.state.kind !== "list") this.emit({ kind: "problem", reason: "cleared", outcome, ...(this.target ? { target: this.target } : {}) });
  }
  /** Ends ownership permanently and drops all references without a network request. */
  dispose(): void { this.cancel(); this.session = undefined; this.target = undefined; this.completed = undefined; this.state = { kind: "list" }; this.stopped = true; }
  /** Captures the original authority deadline before any awaited operation. */
  private begin(operation: "read" | "create" | "edit" | "replace", deadline = this.session!.expires) {
    this.cancel(); this.deadline = deadline; this.abort = new AbortController(); this.emit({ kind: "working", operation }); this.arm(deadline);
    return { version: this.generation, signal: this.abort.signal };
  }
  /** Refuses superseded results both before and after asynchronous runtime validation. */
  private live(version: number): boolean {
    if (this.stopped || version !== this.generation) return false;
    this.checkDeadline(); return version === this.generation && this.currentSession();
  }
  private currentSession(): boolean { return !!this.session && this.session.expires > this.now(); }
  private outcome(): Outcome { return this.dispatched ? "unknown" : ["created", "updated", "replaced"].includes(this.state.kind) ? this.state.kind as Outcome : this.state.kind === "problem" ? this.state.outcome : this.completed ?? "not-sent"; }
  private fail(reason: Reason, outcome: Outcome): void { this.cancel(); this.emit({ kind: "problem", reason, outcome, ...(this.target ? { target: this.target } : {}) }); }
  private cancel(): void { ++this.generation; this.abort?.abort(); clearTimeout(this.timer); this.dispatched = false; this.deadline = undefined; }
  private emit(value: EditorState): void { this.state = value; this.publish(value); }
  private arm(expires: number): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => { this.checkDeadline(); if (this.state.kind !== "list" && this.state.kind !== "problem") this.arm(expires); }, Math.max(0, expires - this.now()));
  }
}
