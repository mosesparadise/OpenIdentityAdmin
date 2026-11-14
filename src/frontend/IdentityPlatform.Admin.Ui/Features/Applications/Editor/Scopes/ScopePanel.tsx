import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { flushSync } from "react-dom";
import { Button } from "../../../../Components/Button";
import type { Observed } from "../observation";
import { bindLifecycle } from "../lifecycle";
import { scopeClient } from "./client";
import { ScopeController, type ScopeState } from "./controller";

/** Keeps deliberate scope assignment in the existing editor with explicit consent consequences and safe recovery. */
export function ScopePanel({ base, reload }: { base: Observed; reload: () => void }) {
  const [state, setState] = useState<ScopeState>({ kind: "loading" });
  const [search, setSearch] = useState("");
  const [confirm, setConfirm] = useState(false);
  const [invalid, setInvalid] = useState(false);
  const controller = useRef<ScopeController | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const focus = useRef<{ initial: boolean; active: Element | null } | null>(null);
  const reloadRef = useRef(reload); reloadRef.current = reload;
  useEffect(() => {
    const owner = new ScopeController(scopeClient(), base, setState, () => reloadRef.current()); controller.current = owner;
    const unbind = bindLifecycle(owner, window, document, flushSync); void owner.load();
    return () => { unbind(); owner.dispose(); controller.current = null; };
  }, [base.details.id, base.etag, base.expires]);
  useLayoutEffect(() => {
    const request = focus.current;
    if (!request || state.kind === "ready") return;
    const owns = request.initial ? document.activeElement === request.active || !request.active?.isConnected && document.activeElement === document.body : document.activeElement === heading.current;
    if (owns) heading.current?.focus();
    focus.current = state.kind === "sending" && owns ? { initial: false, active: heading.current } : null;
  }, [state.kind]);
  const ready = state.kind === "ready" ? state : null;
  return <section className="editor-notice" aria-labelledby="application-scopes-heading" aria-busy={state.kind === "loading" || state.kind === "sending" || ready?.searching}>
    <h3 id="application-scopes-heading" tabIndex={-1} ref={heading}>Application scopes</h3>
    <p id="scope-assignment-warning">Assigned scopes may be requested during sign-in. Users review and approve each request after password and MFA. Assignment changes invalidate pending sign-ins and codes; applications must start a fresh sign-in.</p>
    <p>Scope changes require current application write permission and a fresh MFA sign-in. Existing grants are not revoked.</p>
    <div role="status" aria-live="polite" aria-atomic="true">
      {state.kind === "loading" && <p>Reading current scope assignments…</p>}
      {state.kind === "sending" && <p>Saving your scope assignment once. If interrupted, check current state before submitting again.</p>}
      {state.kind === "saved" && <p>Scope assignment saved. Reloading current application details…</p>}
      {state.kind === "cleared" && <p>Scope assignments were cleared from this view. Reload application details to continue.</p>}
      {ready && <p>{ready.assignment.scopes.length ? "Saved assignment: additional scopes require explicit user consent at sign-in." : "Saved assignment: no additional scopes. The original OpenID-only profile remains eligible."}</p>}
    </div>
    {state.kind === "problem" && <div role="alert"><p>{state.reason === "forbidden" ? "Current permissions and fresh MFA are required. Sign in again if needed." : state.reason === "expired" ? "Sign in again to manage scope assignments." : state.reason === "stale" ? "The application changed. Reload its details and review the assignment before saving." : state.reason === "conflict" ? "The application or selected scope policy cannot be assigned. Review enabled scopes and audiences, then reload." : "The scope assignment could not be confirmed."}</p>
      {state.uncertain && <p>Your change may have committed. Reload to read the current assignment; this will not repeat the write.</p>}
      <Button secondary onClick={reload}>Reload application details</Button><a href="/bff/login">Sign in with fresh MFA</a></div>}
    {state.kind === "cleared" && <Button secondary onClick={reload}>Reload application details</Button>}
    {ready && <>
      <h4>Selected additional scopes ({ready.selected.length}/32)</h4>
      {ready.selected.length ? <ul>{ready.selected.map(name => <li key={name}><span className="application-client-id">{name}</span>{" "}<Button secondary onClick={() => { controller.current?.toggle(name, false); setConfirm(false); heading.current?.focus(); }}>Remove {name}</Button></li>)}</ul> : <p>No additional scopes selected. Saving this selection removes any existing assignments.</p>}
      <form className="application-editor-form" onSubmit={event => { event.preventDefault(); void controller.current?.search(search, 1); }}>
        <label htmlFor="assignment-scope-search">Search catalogue scopes by name prefix</label>
        <input id="assignment-scope-search" value={search} maxLength={100} onChange={event => setSearch(event.target.value)} autoComplete="off" />
        <Button type="submit" secondary disabled={ready.searching}>Search scopes</Button>
      </form>
      {ready.catalogueDenied ? <p>Catalogue read permission is required to browse new scopes. You can still remove current assignments.</p> : <>
        <fieldset disabled={ready.searching}><legend>Available catalogue scopes</legend>
          {ready.choices.length ? ready.choices.map(item => <label className="editor-confirmation" key={item.id}><input type="checkbox" checked={ready.selected.includes(item.name)}
            disabled={!ready.selected.includes(item.name) && (!item.enabled || ready.selected.length >= 32)}
            onChange={event => { controller.current?.toggle(item.name, event.target.checked); setConfirm(false); }} />{item.name} — {item.displayName}{!item.enabled && " (disabled)"}</label>) : <p>No matching scopes.</p>}
        </fieldset>
        <div className="actions"><Button secondary disabled={ready.searching || ready.page === 1} onClick={() => { void controller.current?.search(ready.search, ready.page - 1); }}>Previous scopes</Button>
          <span>Scope page {ready.page}</span><Button secondary disabled={ready.searching || !ready.hasMore} onClick={() => { void controller.current?.search(ready.search, ready.page + 1); }}>Next scopes</Button></div>
      </>}
      <form className="application-editor-form" onSubmit={event => {
        event.preventDefault(); if (!confirm) { setInvalid(true); heading.current?.focus(); return; }
        focus.current = { initial: true, active: document.activeElement };
        setInvalid(false); setConfirm(false); void controller.current?.save(true);
      }}>
        <label className="editor-confirmation" htmlFor="assignment-confirm"><input id="assignment-confirm" type="checkbox" checked={confirm} onChange={event => setConfirm(event.target.checked)} aria-invalid={invalid} aria-describedby={invalid ? "assignment-error scope-assignment-warning" : "scope-assignment-warning"} />I confirm replacing this application’s additional scopes and understand the sign-in effect.</label>
        {invalid && <p id="assignment-error" role="alert">Confirm the scope change before saving.</p>}
        <Button type="submit" disabled={ready.searching}>Save scope assignment</Button>
      </form>
    </>}
  </section>;
}
