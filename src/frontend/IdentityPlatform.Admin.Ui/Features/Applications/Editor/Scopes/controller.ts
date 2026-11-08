import { object, reason, type Observed, type Reason } from "../observation";
import { page as cataloguePage } from "../../../Catalogue/observation";
import type { Page } from "../../../Catalogue/input";
import { assignment, type Assignment } from "./observation";
import type { ScopeClient } from "./client";
type Ready = { kind: "ready"; assignment: Assignment; selected: string[]; choices: Page["items"]; search: string; page: number; hasMore: boolean; searching: boolean; catalogueDenied: boolean };
export type ScopeState = Ready | { kind: "loading" | "sending" | "cleared" | "saved" } | { kind: "problem"; reason: Reason; uncertain: boolean };
/** Owns one original authority lifetime, explicit replacement intent and cancellation without replaying writes or hidden-page state. */
export class ScopeController {
  private state: ScopeState = { kind: "loading" };
  private generation = 0;
  private abort?: AbortController;
  private timer?: ReturnType<typeof setTimeout>;
  private disposed = false;
  private deadline: number;
  constructor(private client: ScopeClient, private base: Observed, private publish: (state: ScopeState) => void,
    private changed: () => void, private now = Date.now) { this.deadline = base.expires; this.arm(); }
  /** Requires current assignment and the same application version before allowing any selection or write. */
  async load(): Promise<void> {
    if (this.disposed || this.state.kind === "sending") return;
    const operation = this.begin(); this.emit({ kind: "loading" });
    try {
      const result = await this.client.read(this.base.details.id, operation.signal);
      if (!this.live(operation.generation)) return;
      if (result.status !== 200) { this.fail(reason(result.status)); return; }
      const value = await assignment(result, this.base.details.id, this.now());
      if (!this.live(operation.generation)) return;
      if (!value) { this.fail("unavailable"); return; }
      if (value.expires <= this.now()) { this.fail("expired"); return; }
      if (value.etag !== this.base.etag) { this.fail("stale"); return; }
      this.deadline = Math.min(this.deadline, value.expires); this.arm();
      this.emit({ kind: "ready", assignment: value, selected: [...value.scopes], choices: [], search: "", page: 1, hasMore: false, searching: false, catalogueDenied: false });
      await this.search("", 1);
    } catch { if (this.live(operation.generation)) this.fail("unavailable"); }
  }
  /** Retains selections across bounded catalogue searches and allows explicit removal even without catalogue read permission. */
  async search(search: string, number: number): Promise<void> {
    if (this.state.kind !== "ready" || this.disposed || search.length > 100 || !Number.isSafeInteger(number) || number < 1) return;
    const before = this.state, operation = this.begin(); this.emit({ ...before, searching: true });
    try {
      const result = await this.client.list(search, number, operation.signal);
      if (!this.live(operation.generation) || this.state.kind !== "ready") return;
      if (result.status === 403) { this.emit({ ...this.state, choices: [], hasMore: false, searching: false, catalogueDenied: true }); return; }
      const choices = cataloguePage(result, "scope", search, number, this.now());
      if (!choices) { this.fail(reason(result.status)); return; }
      this.deadline = Math.min(this.deadline, Date.parse(choices.expiresAt)); this.arm();
      this.emit({ ...this.state, choices: choices.items, search, page: number, hasMore: choices.hasMore, searching: false, catalogueDenied: false });
    } catch { if (this.live(operation.generation)) this.fail("unavailable"); }
  }
  /** Admits only currently visible eligible options or removals from the selected set; stale list responses cannot replace selected names. */
  toggle(name: string, selected: boolean): void {
    this.checkDeadline(); if (this.disposed || this.state.kind !== "ready") return;
    const names = this.state.selected;
    if (selected && (!this.state.choices.some(item => item.name === name && item.enabled) || names.length >= 32 || names.includes(name))) return;
    this.emit({ ...this.state, selected: selected ? [...names, name].sort() : names.filter(value => value !== name) });
  }
  /** Acquires native application CSRF then sends one frozen exact replacement under the original resource version. */
  async save(confirmed: boolean): Promise<void> {
    this.checkDeadline(); if (this.disposed || this.state.kind !== "ready" || this.state.searching || !confirmed) return;
    const before = this.state, names = [...before.selected].sort(), operation = this.begin(); let sent = false;
    this.emit({ kind: "sending" });
    try {
      const proof = await this.client.token(operation.signal);
      if (!this.live(operation.generation)) return;
      if (proof.status !== 200 || !object(proof.value, ["requestToken"]) || typeof proof.value.requestToken !== "string" || !/^[A-Za-z0-9_-]{1,4096}$/.test(proof.value.requestToken)) { this.fail(proof.status === 200 ? "unavailable" : reason(proof.status)); return; }
      sent = true;
      const result = await this.client.save(this.base.details.id, before.assignment.etag, names, proof.value.requestToken, operation.signal);
      if (!this.live(operation.generation)) return;
      if (result.status !== 200) { this.fail(reason(result.status), result.status === 401 || result.status >= 500); return; }
      const value = await assignment(result, this.base.details.id, this.now(), before.assignment, names);
      if (!this.live(operation.generation)) return;
      if (!value) { this.fail("unavailable", true); return; }
      if (value.expires <= this.now()) { this.fail("expired", true); return; }
      this.emit({ kind: "saved" }); this.changed();
    } catch { if (this.live(operation.generation)) this.fail("unavailable", sent); }
  }
  checkDeadline(): void { if (!this.disposed && this.now() >= this.deadline) this.suspend(); }
  suspend(): void { this.cancel(); clearTimeout(this.timer); this.emit({ kind: "cleared" }); this.disposed = true; }
  dispose(): void { this.cancel(); clearTimeout(this.timer); this.disposed = true; }
  private arm(): void { clearTimeout(this.timer); this.timer = setTimeout(() => this.checkDeadline(), Math.max(0, this.deadline - this.now())); }
  private begin() { this.cancel(); this.abort = new AbortController(); return { generation: this.generation, signal: this.abort.signal }; }
  private live(generation: number) { this.checkDeadline(); return !this.disposed && this.generation === generation; }
  private cancel() { ++this.generation; this.abort?.abort(); }
  private fail(reason: Reason, uncertain = false) { this.cancel(); this.emit({ kind: "problem", reason, uncertain }); }
  private emit(state: ScopeState) { this.state = state; this.publish(state); }
}
