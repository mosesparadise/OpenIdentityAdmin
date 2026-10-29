import { useEffect, useRef, useState } from "react";
import { Button } from "../../Components/Button";
import { useApplications } from "./useApplications";

/** Presents actual registered applications and finite permission, expiry, empty and dependency states. */
export function ApplicationsPanel() {
  const { state, check } = useApplications();
  const heading = useRef<HTMLHeadingElement>(null);
  const requested = useRef(false);
  const reload: typeof check = query => { requested.current = true; check(query); };
  useEffect(() => { if (state.kind !== "loading" && requested.current) { requested.current = false; heading.current?.focus(); } }, [state]);
  const [search, setSearch] = useState("");
  const [applied, setApplied] = useState("");
  const [pageSize, setPageSize] = useState(25);
  const ready = state.kind === "ready";
  const loading = state.kind === "loading";
  const title = ready ? state.items.length ? "Registered applications" : "No matching applications"
    : loading ? "Loading applications…" : state.kind === "expired" ? "Sign in to view applications"
    : state.kind === "forbidden" ? "Application access is restricted" : "Applications are unavailable";
  const description = ready ? state.items.length ? `Page ${state.page}: ${state.items.length} ${state.items.length === 1 ? "application" : "applications"} currently registered with your identity provider.` : "Try a different client ID prefix."
    : loading ? "Checking your access and reading the current collection."
    : state.kind === "expired" ? "Your application access has ended or you are not signed in. Sign in again to continue."
    : state.kind === "forbidden" ? "Your account does not currently have permission to read applications. Contact your administrator."
    : "We couldn’t load this collection. Try again; previous results have been hidden.";
  return <section className="applications-card" aria-labelledby="application-state" aria-busy={loading}>
    <div role="status" aria-live="polite" aria-atomic="true"><h2 id="application-state" ref={heading} tabIndex={-1}>{title}</h2><p>{description}</p></div>
    <form className="application-filters" onSubmit={event => {
      event.preventDefault(); setApplied(search); reload({ search, page: 1, pageSize });
    }}><div><label htmlFor="application-search">Client ID starts with</label><input id="application-search" type="text" maxLength={100} value={search}
      onChange={event => setSearch(event.target.value)} aria-describedby="application-search-help" />
      <p id="application-search-help">Case-sensitive. Symbols are matched literally.</p></div>
      <div><label htmlFor="application-page-size">Per page</label><select id="application-page-size" value={pageSize} onChange={event => setPageSize(Number(event.target.value))}>
        <option value={25}>25</option><option value={50}>50</option><option value={100}>100</option></select></div>
      <Button type="submit">Search</Button></form>
    {ready && state.items.length > 0 && <div className="application-table-scroll" tabIndex={0} role="region" aria-label="Registered applications table">
      <table className="application-table"><caption className="visually-hidden">Registered applications, page {state.page}</caption>
        <thead><tr><th scope="col">Name</th><th scope="col">Client ID</th><th scope="col">Type</th></tr></thead>
        <tbody>{state.items.map(item => <tr key={item.id}><td>{item.displayName || "Unnamed application"}</td><td className="application-client-id">{item.clientId}</td><td>{item.clientType || "Not specified"}</td></tr>)}</tbody>
      </table></div>}
    {ready && <div className="application-pagination"><p>Page {state.page} · {state.items.length} {state.items.length === 1 ? "application" : "applications"}</p><div className="actions">
      <Button secondary disabled={state.page === 1} onClick={() => reload({ search: applied, page: state.page - 1, pageSize: state.pageSize })}>Previous</Button>
      <Button secondary disabled={!state.hasNextPage || state.page >= 10000} onClick={() => reload({ search: applied, page: state.page + 1, pageSize: state.pageSize })}>Next</Button>
    </div></div>}
    {!loading && <div className="actions">{state.kind === "expired" && <Button asChild><a href="/bff/login">Sign in</a></Button>}
      <Button secondary onClick={() => reload()}>{state.kind === "unavailable" ? "Try again" : "Reload applications"}</Button>
      {state.kind === "forbidden" && <a className="text-link" href="/session">Review your session</a>}</div>}
  </section>;
}
