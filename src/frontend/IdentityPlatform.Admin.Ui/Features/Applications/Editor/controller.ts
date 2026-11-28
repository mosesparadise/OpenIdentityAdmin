import { retired } from "./retirement";
import { assignment } from "./Scopes/observation";
import type { SessionState } from "../../Session/observation";
import type { EditorClient } from "./client";
import { validate, validateNative, validateBrowser, validateMachine, validatePublicKey, machineProfile, keyedMachineProfile, type MachineInput, type BrowserInput, type RegistrationType, nativeProfile, type CreateInput, validateClone, type CloneDraft, type Draft, type Errors, type EditDraft, type ReplacementDraft, type LifecycleInput, type LifecycleDraft, validateEdit, validateReplacement, utc } from "./input";
import { created, cloned, edited, replaced, keyReplaced, lifecycleChanged, object, observe, reason, type Observed, type Reason } from "./observation";
export type EditorAction = "edit" | "replace" | "disable" | "enable" | "clone" | "retire";
export type Outcome = "key-unknown" | "browser-created" | "browser-unknown" | "native-created" | "native-unknown" | "not-sent" | "unknown" | "created" | "updated" | "replaced" | "cloned";
export type EditorState =
  | { kind: "list" | "form" }
  | { kind: "working"; operation: "read" | "create" | "create-native" | "create-browser" | "create-key" | EditorAction }
  | ({ kind: "details" | "edit-form" | "replacement-form" | "disable-form" | "enable-form" | "retire-form" | "updated" } & Observed)
  | ({ kind: "clone-form"; scopes: string[] } & Observed)
  | ({ kind: "created" | "replaced" | "cloned"; secret: string } & Observed)
  | { kind: "problem"; reason: Reason; outcome: Outcome; target?: { id: string; action: EditorAction; key?: boolean; clientId?: string } };

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
  private target?: { id: string; action: EditorAction; key?: boolean; clientId?: string };
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
    const recovery = this.target?.id === id ? { target: this.target, outcome: this.outcome() } : undefined;
    this.cancel(); this.target = recovery?.target; this.completed = recovery?.outcome;
    if (!this.currentSession()) { this.fail("expired", this.outcome()); return; }
    const operation = this.begin("read");
    try {
      const result = await this.client.details(id, operation.signal);
      if (!this.live(operation.version)) return;
      if (result.status !== 200) { this.fail(reason(result.status), this.outcome()); return; }
      const value = await observe(result, id, this.now());
      if (!this.live(operation.version)) return;
      if (!value || value.expires <= this.now()) { this.fail("unavailable", this.outcome()); return; }
      const expires = Math.min(value.expires, this.session!.expires);
      this.target = undefined; this.completed = undefined;
      this.emit({ ...value, expires, kind: "details" }); this.arm(expires);
    } catch { if (this.live(operation.version)) this.fail("unavailable", this.outcome()); }
  }
  /** Sets the single-send fence before token acquisition; any post-dispatch non-success remains potentially committed. */
  async create(draft: Draft, profile: RegistrationType = "web"): Promise<Errors> {
    if (this.stopped || this.state.kind !== "form") return {};
    const keyed = profile === "keyed-machine";
    const publicKey = keyed ? validatePublicKey(draft) : undefined;
    const checked = keyed || profile === "machine" ? validateMachine(draft, this.now()) : profile === "browser" ? validateBrowser(draft) : profile === "native" ? validateNative(draft) : validate(draft, this.now());
    if (!checked.input || keyed && !publicKey?.key) return { ...checked.errors, ...publicKey?.errors };
    if (!this.currentSession()) { this.fail("expired", "not-sent"); return {}; }
    const input = checked.input;
    const confidential = (value: typeof input): value is CreateInput | MachineInput => "credentialExpiresAt" in value;
    const operation = this.begin(keyed ? "create-key" : profile === "browser" ? "create-browser" : profile === "native" ? "create-native" : "create");
    try {
      const proof = await this.client.token(operation.signal);
      if (!this.live(operation.version)) return {};
      if (proof.status !== 200) { this.fail(reason(proof.status), "not-sent"); return {}; }
      if (!object(proof.value, ["requestToken"]) || typeof proof.value.requestToken !== "string" ||
          !/^[A-Za-z0-9_-]{1,4096}$/.test(proof.value.requestToken)) { this.fail("unavailable", "not-sent"); return {}; }
      if (confidential(input) && !(utc(input.credentialExpiresAt, true) > this.now())) { this.fail("invalid", "not-sent"); return {}; }
      this.dispatched = true;
      const result = confidential(input) ? ("redirectUris" in input ? await this.client.create(input, proof.value.requestToken, operation.signal) :
        publicKey?.key ? await this.client.createMachineKey({ application: input, publicKey: publicKey.key }, proof.value.requestToken, operation.signal) :
        await this.client.createMachine(input, proof.value.requestToken, operation.signal)) :
        "origins" in input ? await this.client.createBrowser(input as BrowserInput, proof.value.requestToken, operation.signal) :
        await this.client.createNative(input, proof.value.requestToken, operation.signal);
      if (!this.live(operation.version)) return {};
      if (result.status !== 201) { this.fail(reason(result.status), "unknown"); return {}; }
      const value = confidential(input) && !keyed ? await created(result, input, this.now()) : await observe(result, null, this.now(), input, keyed);
      if (!this.live(operation.version)) return {};
      if (!value) { this.fail("unavailable", "unknown"); return {}; }
      const expires = Math.min(value.expires, this.session!.expires, value.details.credential ? utc(value.details.credential.expiresAt) : value.expires);
      if (expires <= this.now()) { this.fail("display-expired", "unknown"); return {}; }
      this.dispatched = false; this.completed = keyed ? "updated" : profile === "browser" ? "browser-created" : profile === "native" ? "native-created" : "created";
      this.emit("secret" in value && typeof value.secret === "string" ? { ...value, expires, kind: "created", secret: value.secret } : { ...value, expires, kind: "details" }); this.arm(expires);
    } catch { if (this.live(operation.version)) this.fail("unavailable", this.dispatched ? "unknown" : "not-sent"); }
    return {};
  }
  /** Opens an explicit mutation form while dropping secret output and preserving original observation authority. */
  open(action: Exclude<EditorAction, "clone">): void {
    this.checkDeadline();
    if (this.stopped || !["details", "created", "updated", "replaced", "cloned"].includes(this.state.kind) || !("details" in this.state) || !this.state.details.editable) return;
    const { details, expires, etag } = this.state;
    if (details.profile === keyedMachineProfile && action === "edit") return;
    if (action === "replace" && !details.credential) return;
    this.cancel(); this.completed = undefined; this.target = { id: details.id, action, ...(action === "replace" && details.profile === keyedMachineProfile ? { key: true } : {}) };
    this.emit({ kind: action === "edit" ? "edit-form" : action === "replace" ? "replacement-form" : action === "disable" ? "disable-form" : action === "retire" ? "retire-form" : "enable-form", details: structuredClone(details), expires, etag }); this.arm(expires);
  }
  /** Reads assignment names against the same source version before presenting clone confirmation; late responses cannot reopen a cleared editor. */
  async openClone(): Promise<void> {
    this.checkDeadline();
    if (this.stopped || !["details", "created", "updated", "replaced", "cloned"].includes(this.state.kind) || !("details" in this.state) || !this.state.details.editable) return;
    if (!this.state.details.credential || this.state.details.profile === machineProfile || this.state.details.profile === keyedMachineProfile) return;
    const base: Observed = { details: structuredClone(this.state.details), expires: this.state.expires, etag: this.state.etag };
    this.completed = undefined; this.target = { id: base.details.id, action: "clone" };
    const operation = this.begin("read", base.expires);
    try {
      const result = await this.client.scopes(base.details.id, operation.signal);
      if (!this.live(operation.version)) return;
      if (result.status !== 200) { this.fail(reason(result.status), "not-sent"); return; }
      const selected = await assignment(result, base.details.id, this.now());
      if (!this.live(operation.version)) return;
      if (!selected) { this.fail("unavailable", "not-sent"); return; }
      if (selected.etag !== base.etag) { this.fail("stale", "not-sent"); return; }
      const expires = Math.min(base.expires, selected.expires, this.session!.expires);
      if (expires <= this.now()) { this.fail("display-expired", "not-sent"); return; }
      this.emit({ ...base, scopes: selected.scopes, expires, kind: "clone-form" }); this.arm(expires);
    } catch { if (this.live(operation.version)) this.fail("unavailable", "not-sent"); }
  }
  /** Sends a confirmed clone once; neither source grants nor credential material enters the request. */
  async clone(draft: CloneDraft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "clone-form") return {};
    const base = this.state, checked = validateClone({ ...draft }, base.details.redirectUris, this.now());
    if (!checked.input) return checked.errors;
    const input = checked.input;
    this.target = { id: base.details.id, action: "clone", clientId: input.clientId };
    return this.mutate(base, "clone", (proof, signal) => this.client.clone(base.details.id, base.etag!, input, proof, signal),
      result => cloned(result, base, input, this.now()), utc(input.credentialExpiresAt, true));
  }
  /** Cancels a draft without another read or restoring a previously displayed secret. */
  cancelForm(): void {
    this.checkDeadline();
    if (this.state.kind !== "edit-form" && this.state.kind !== "replacement-form" && this.state.kind !== "disable-form" && this.state.kind !== "enable-form" && this.state.kind !== "clone-form" && this.state.kind !== "retire-form") return;
    const { details, expires, etag } = this.state;
    this.emit({ kind: "details", details, expires, etag });
  }
  /** Submits only presentation changes against the original strong version. */
  async edit(draft: EditDraft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "edit-form") return {};
    const checked = validateEdit(draft, this.state.details.profile === nativeProfile, this.state.details.profile === machineProfile);
    if (!checked.input) return checked.errors;
    const input = checked.input, base = this.state;
    return this.mutate(base, "edit", (token, signal) => this.client.edit(base.details.id, base.etag!, input, token, signal),
      result => edited(result, base, input, this.now()));
  }
  /** Replaces a secret once, with explicit confirmation and a newly chosen future expiry. */
  async replace(draft: ReplacementDraft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "replacement-form") return {};
    const checked = validateReplacement({ ...draft }, this.now());
    if (this.state.details.profile === keyedMachineProfile) {
      const { key, errors } = validatePublicKey(draft);
      if (checked.errors.confirmed) checked.errors.confirmed = "Confirm that the current key will stop authenticating.";
      if (!checked.input || !key) return { ...checked.errors, ...errors };
      const base = this.state, expiry = checked.input.credentialExpiresAt;
      return this.mutate(base, "replace", (token, signal) => this.client.replaceKey(base.details.id, base.etag!,
        { publicKey: key, credentialExpiresAt: expiry, confirm: true }, token, signal),
        result => keyReplaced(result, base, expiry, this.now()), utc(expiry, true));
    }
    if (!checked.input) return checked.errors;
    const input = checked.input, base = this.state;
    return this.mutate(base, "replace", (token, signal) => this.client.replace(base.details.id, base.etag!, input, token, signal),
      result => replaced(result, base, input, this.now()), utc(input.credentialExpiresAt, true));
  }
  /** Sends one explicit lifecycle command; neither a stale result nor an ambiguous outcome can trigger a retry. */
  async lifecycle(input: LifecycleDraft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "disable-form" && this.state.kind !== "enable-form") return {};
    if (input.confirm !== true) return { confirmed: "Confirm the lifecycle change before continuing." };
    if (input.reason !== "maintenance" && input.reason !== "security" && input.reason !== "configuration") return { reason: "Choose a listed reason." };
    const frozen: LifecycleInput = { reason: input.reason, confirm: true };
    const base = this.state, enabled = base.kind === "enable-form";
    return this.mutate(base, enabled ? "enable" : "disable",
      (token, signal) => this.client.lifecycle(base.details.id, base.etag!, frozen, enabled, token, signal),
      result => lifecycleChanged(result, base, enabled, this.now()));
  }
  /** Confirms permanent retirement once; lost responses are recovered by reading retained details, never automatic resubmission. */
  async retire(input: LifecycleDraft): Promise<Errors> {
    if (this.stopped || this.state.kind !== "retire-form") return {};
    if (input.confirm !== true) return { confirmed: "Confirm permanent retirement before continuing." };
    if (input.reason !== "maintenance" && input.reason !== "security" && input.reason !== "configuration") return { reason: "Choose a listed reason." };
    const frozen: LifecycleInput = { reason: input.reason, confirm: true }, base = this.state, key = crypto.randomUUID();
    return this.mutate(base, "retire", (token, signal) => this.client.retire(base.details.id, base.etag!, key, frozen, token, signal),
      result => retired(result, base, key, this.now()));
  }
  /** Owns a single update dispatch, binding every asynchronous boundary to the unchanged base deadline. */
  private async mutate(base: Observed, action: EditorAction,
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
      if (result.status !== (action === "clone" ? 201 : action === "retire" ? 202 : 200)) { this.fail(reason(result.status), "unknown"); return {}; }
      const value = await inspect(result);
      if (!this.live(operation.version)) return {};
      if (!value) { this.fail("unavailable", "unknown"); return {}; }
      const expires = Math.min(base.expires, value.expires, this.session!.expires, secretExpiry ?? Infinity);
      if (expires <= this.now()) { this.fail("display-expired", "unknown"); return {}; }
      this.dispatched = false; this.completed = value.secret === undefined ? "updated" : action === "clone" ? "cloned" : "replaced";
      if (action === "clone") this.target = { id: value.details.id, action: "replace" };
      this.emit(value.secret === undefined ? { ...value, expires, kind: "updated" } : { ...value, secret: value.secret, expires, kind: action === "clone" ? "cloned" : "replaced" }); this.arm(expires);
    } catch { if (this.live(operation.version)) this.fail("unavailable", this.dispatched ? "unknown" : "not-sent"); }
    return {};
  }
  /** Drops the secret reference and removes its view, retaining only a separately validated sanitized projection. */
  dismiss(): void {
    this.checkDeadline();
    if (this.state.kind !== "created" && this.state.kind !== "replaced" && this.state.kind !== "cloned") return;
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
  private begin(operation: "read" | "create" | "create-native" | "create-browser" | "create-key" | EditorAction, deadline = this.session!.expires) {
    this.cancel(); this.deadline = deadline; this.abort = new AbortController(); this.emit({ kind: "working", operation }); this.arm(deadline);
    return { version: this.generation, signal: this.abort.signal };
  }
  /** Refuses superseded results both before and after asynchronous runtime validation. */
  private live(version: number): boolean {
    if (this.stopped || version !== this.generation) return false;
    this.checkDeadline(); return version === this.generation && this.currentSession();
  }
  private currentSession(): boolean { return !!this.session && this.session.expires > this.now(); }
  private outcome(): Outcome { return this.dispatched ? this.unknownOutcome() : ["created", "updated", "replaced", "cloned"].includes(this.state.kind) ? this.state.kind as Outcome : this.state.kind === "problem" ? this.state.outcome : this.completed ?? "not-sent"; }
  private unknownOutcome(): Outcome { return this.target?.key || this.state.kind === "working" && this.state.operation === "create-key" ? "key-unknown" : this.state.kind === "working" && this.state.operation === "create-browser" ? "browser-unknown" : this.state.kind === "working" && this.state.operation === "create-native" ? "native-unknown" : "unknown"; }
  private fail(reason: Reason, outcome: Outcome): void { if (outcome === "unknown") outcome = this.unknownOutcome(); this.cancel(); this.emit({ kind: "problem", reason, outcome, ...(this.target ? { target: this.target } : {}) }); }
  private cancel(): void { ++this.generation; this.abort?.abort(); clearTimeout(this.timer); this.dispatched = false; this.deadline = undefined; }
  private emit(value: EditorState): void { this.state = value; this.publish(value); }
  private arm(expires: number): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => { this.checkDeadline(); if (this.state.kind !== "list" && this.state.kind !== "problem") this.arm(expires); }, Math.max(0, expires - this.now()));
  }
}
