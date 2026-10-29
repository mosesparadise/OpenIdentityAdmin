import { Button } from "../../Components/Button";
import { Icon } from "../../Components/Icon";
import type { SessionState } from "./observation";

/** Describes only a currently observed local session, never current management permission or remote logout success. */
export function SessionPanel({ state, check, detail = false }: { state: SessionState; check: () => void; detail?: boolean }) {
  const live = state.kind === "authenticated";
  const loading = state.kind === "loading";
  const title = live ? "You’re signed in" : loading ? "Checking your session…" : state.kind === "unavailable"
    ? "We couldn’t check your session" : state.kind === "elapsed" ? "Your last checked session has expired" : "Not signed in";
  const description = live ? "Your local session is active. You can review its details or sign out below."
    : loading ? "Confirming the status of this browser session." : state.kind === "unavailable"
    ? "Try checking again. Your previous session details are hidden until we can verify them."
    : state.kind === "elapsed" ? "Check your session or sign in again to continue." : "Sign in with your password and verification code to view your account session.";
  return <section className="session-card" aria-labelledby="session-state" aria-busy={loading}>
    <div className="session-icon"><Icon name={live ? "check" : "shield"} /></div>
    <div className="session-content"><div role="status" aria-live="polite" aria-atomic="true"><h2 id="session-state">{title}</h2><p>{description}</p></div>
      {live && detail && <dl className="session-details"><div><dt>Account ID</dt><dd className="account-id">{state.subject}</dd></div>
        <div><dt>Session valid until</dt><dd><time dateTime={new Date(state.expires).toISOString()}>{new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "long" }).format(state.expires)}</time></dd></div></dl>}
      <div className="actions">
        {live && !detail && <Button asChild><a href="/session">View session <Icon name="arrow" /></a></Button>}
        {live && <Button asChild secondary><a href="/bff/logout"><Icon name="exit" />Sign out</a></Button>}
        {!live && !loading && <Button asChild><a href="/bff/login">Sign in <Icon name="arrow" /></a></Button>}
        {(detail || state.kind === "unavailable" || state.kind === "elapsed") && <Button secondary disabled={loading} onClick={check}>{state.kind === "unavailable" ? "Check again" : "Check session"}</Button>}
      </div>
    </div>
  </section>;
}
