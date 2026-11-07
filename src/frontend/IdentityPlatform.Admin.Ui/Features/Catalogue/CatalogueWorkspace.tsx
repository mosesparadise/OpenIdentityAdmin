import { useLayoutEffect, useRef, useState } from "react";
import { Button } from "../../Components/Button";
import type { SessionState } from "../Session/observation";
import type { Kind } from "./input";
import { useCatalogue } from "./useCatalogue";
import { CatalogueForm } from "./CatalogueForm";
const failures = { none: "", stale: "The entry changed. Reload it before deciding whether to delete it.", referenced: "The entry is now in use and was kept.", authority: "Your account or permission changed. The entry was kept.", expired: "The original approval expired. The entry was kept.", missing: "The entry was already absent." };
/** Presents actual catalogue operations and durable deletion status without treating admission as completed deletion. */
export function CatalogueWorkspace({ kind, session }: { kind: Kind; session: SessionState }) {
  const { state, owner, reload } = useCatalogue(kind, session), heading = useRef<HTMLHeadingElement>(null);
  const [search, setSearch] = useState(""), [confirmed, setConfirmed] = useState(false);
  const plural = kind === "scope" ? "scopes" : "audiences";
  const checkStatus = async (control: HTMLButtonElement) => {
    const current = owner.current, applied = await current?.refresh();
    if (applied && current === owner.current && (document.activeElement === control || !control.isConnected && document.activeElement === document.body)) heading.current?.focus();
  };
  useLayoutEffect(() => { heading.current?.focus(); setConfirmed(false); }, [state.kind]);
  if (session.kind !== "authenticated") return <section className="applications-card"><h2>Sign in to manage {plural}</h2><p>Your current permissions are checked when you open or change an entry.</p><a className="button" href="/bff/login">Sign in</a></section>;
  const title = state.kind === "form" ? `${state.base ? "Edit" : "Create"} ${kind}` : state.kind === "details" ? `${kind === "scope" ? "Scope" : "Audience"} details` :
    state.kind === "confirm" ? `Confirm ${kind} deletion` : state.kind === "job" ? state.job.state === "succeeded" ? "Deletion completed" : state.job.state === "queued" ? "Deletion queued" : "Deletion was not performed" :
      state.kind === "working" ? "Submitting change…" : state.kind === "loading" ? "Loading catalogue…" : state.kind === "problem" ? "Review catalogue status" : `Manage ${plural}`;
  return <section className="applications-card application-editor" aria-labelledby="catalogue-heading" aria-busy={state.kind === "loading" || state.kind === "working"}>
    <div role="status" aria-live="polite" aria-atomic="true"><h2 id="catalogue-heading" tabIndex={-1} ref={heading}>{title}</h2></div>
    {state.kind === "list" && <>
      <div className="application-create-bar"><Button onClick={() => owner.current?.form()}>Create {kind}</Button></div>
      <form className="catalogue-search" onSubmit={event => { event.preventDefault(); reload(search); }}><label htmlFor="catalogue-search">Search by identifier prefix</label><div className="actions"><input id="catalogue-search" value={search} maxLength={100} onChange={event => setSearch(event.target.value)} /><Button type="submit" secondary>Search</Button></div></form>
      {state.page.items.length ? <ul className="catalogue-list">{state.page.items.map(item => <li key={item.id}><div><strong>{item.displayName}</strong><span>{item.name}</span><span className="catalogue-status">{item.enabled ? "Enabled" : "Disabled"}</span></div><Button secondary onClick={() => { void owner.current?.details(item.id); }}>View {item.displayName}</Button></li>)}</ul> : <p>No {plural} match this search. Create an entry to get started.</p>}
      <div className="actions"><Button secondary disabled={state.page.page === 1} onClick={() => reload(state.search, Number(state.page.page) - 1)}>Previous page</Button><span>Page {state.page.page}</span><Button secondary disabled={!state.page.hasMore} onClick={() => reload(state.search, Number(state.page.page) + 1)}>Next page</Button></div>
    </>}
    {state.kind === "form" && <CatalogueForm kind={kind} base={state.base} audiences={state.audiences} choicesError={state.choicesError} searchAudiences={value => { void owner.current?.choices(value); }} submit={value => owner.current?.save(value) ?? Promise.resolve({})} cancel={() => reload()} />}
    {state.kind === "details" && <>
      <dl className="catalogue-details"><dt>Display name</dt><dd>{state.details.displayName}</dd><dt>Identifier</dt><dd>{"name" in state.details ? state.details.name : state.details.resource}</dd><dt>Status</dt><dd>{state.details.enabled ? "Enabled" : "Disabled"}</dd>
        {"owner" in state.details ? <><dt>Owner</dt><dd>{state.details.owner}</dd></> : <><dt>Scope type</dt><dd>{state.details.kind === "api" ? "API access" : "Identity information"}</dd><dt>Consent text</dt><dd>{state.details.consentText}</dd><dt>Audience</dt><dd>{state.details.resource ?? "None"}</dd><dt>Identity claims</dt><dd>{state.details.disclosure === "email" ? "Consented email and verification status" : "None"}</dd></>}
        <dt>References</dt><dd>{state.details.referenced ? "In use; deletion is unavailable" : "No current or retained references found"}</dd></dl>
      <div className="actions"><Button onClick={() => owner.current?.form(true)}>Edit {kind}</Button><Button secondary disabled={state.details.referenced} onClick={() => owner.current?.confirm()}>Delete {kind}</Button></div>
    </>}
    {state.kind === "confirm" && <form onSubmit={event => { event.preventDefault(); void owner.current?.remove(confirmed); }}>
      <p>Delete <strong>{state.details.displayName}</strong>? Deletion completes only if your original approval remains valid and the entry is still unused.</p>
      <label className="catalogue-check"><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} required /> I confirm deletion of this {kind}.</label>
      <div className="actions"><Button type="submit" disabled={!confirmed}>Confirm deletion</Button></div>
    </form>}
    {state.kind === "job" && <><p>{state.job.state === "succeeded" ? "The entry was deleted and the result was recorded." : state.job.state === "queued" ? "The request was accepted. Deletion has not completed yet. Status checks do not extend the original approval." : failures[state.job.failure]}</p>
      {state.job.state === "queued" && <Button secondary onClick={event => { void checkStatus(event.currentTarget); }}>Check deletion status</Button>}</>}
    {state.kind === "problem" && <div role="alert"><p>{state.reason === "forbidden" ? "This action requires current permission and, for changes, a recent password and authenticator sign-in. Sign in again or check your access." : state.reason === "expired" ? "Sign in again before continuing." : state.reason === "missing" ? "The entry or deletion job was not found." : state.reason === "conflict" ? "The entry is referenced or conflicts with current state. Reload it before trying a change." : state.reason === "stale" ? "The entry changed. Reload it before submitting." : state.reason === "invalid" ? "The request was not accepted. Review the entry and try again." : state.reason === "cleared" ? "This view was cleared after hiding the page or reaching its observation deadline. Reload to check current access." : "The result is unavailable."}</p>
      {state.uncertain && <p>The change may have completed. It will not be retried automatically.</p>}{state.recoverable && <Button secondary onClick={() => { void owner.current?.recover(); }}>Check original deletion request</Button>}{state.job && <Button secondary onClick={event => { void checkStatus(event.currentTarget); }}>Check deletion status</Button>}<a className="text-link" href="/bff/login">Sign in again</a></div>}
    {(state.kind === "loading" || state.kind === "working") && <p>{state.kind === "working" ? "This change is sent once. Stopping the wait does not cancel a change already accepted by the server." : "Checking current access and reading catalogue entries."}</p>}
    {state.kind !== "list" && state.kind !== "form" && <div className="actions"><Button secondary onClick={() => reload()}>Back to {plural}</Button></div>}
  </section>;
}
