import { Button } from "../../../Components/Button";
import type { EditorState } from "./controller";
/** Describes uncertainty without claiming rollback or presenting an automatic mutation retry. */
export function ProblemView({ state, reload }: { state: Extract<EditorState, { kind: "problem" }>; reload?: () => void }) {
  const publicClient = state.outcome === "browser-created" || state.outcome === "browser-unknown" || state.outcome === "native-created" || state.outcome === "native-unknown";
  const lifecycle = state.target?.action === "disable" || state.target?.action === "enable";
  const retiring = state.target?.action === "retire";
  const cloning = state.target?.action === "clone";
  const sensitive = retiring || cloning || lifecycle || state.target?.action === "replace";
  const message = state.reason === "expired" ? "Your session observation has ended. Sign in again to continue."
    : state.reason === "display-expired" ? publicClient ? "This display has expired. Return to applications to read current details." : "This display has expired. Return to applications to read current details; a secret’s expiry does not end your session."
    : state.reason === "forbidden" ? state.target?.action === "replace" ? "Secret replacement requires current credential permission and a recent MFA sign-in. Sign in again to refresh your authentication; contact your administrator if access remains restricted." : retiring ? "Retirement requires both lifecycle and grant-revocation permissions and a recent MFA sign-in." : cloning ? "Cloning requires current application-write permission and a recent MFA sign-in. Sign in again or contact your administrator." : lifecycle ? "Changing application status requires current lifecycle permission and a recent MFA sign-in. Sign in again or contact your administrator." : "Your current permissions do not allow this operation."
    : state.reason === "missing" ? "This registration could not be found."
    : state.reason === "stale" ? "This version is no longer current. Reload current details before making another change. Your draft will not be reapplied."
    : state.reason === "conflict" ? "The server reported a registration conflict."
    : state.reason === "invalid" ? "The server could not accept this request."
    : state.reason === "cleared" ? "The editor was cleared when you left or hid this view."
    : "We couldn’t confirm the requested operation.";
  return <div className="editor-notice"><p>{message}</p>
    {retiring && <p>After reloading or signing in again, use <a href="/retired-applications">Retired applications</a> to find retained details and the exact revocation job. Recovery does not require the original browser request key.</p>}
    {(state.outcome === "unknown" || state.outcome === "native-unknown" || state.outcome === "browser-unknown") && <p><strong>{state.target?.action === "edit" ? "Your changes may have been saved." : state.target?.action === "replace" ? "The secret may have changed." : retiring ? "The application may already be permanently retired and revocation accepted." : lifecycle ? "The application status may have changed." : "The application may have been created."}</strong> {cloning ? `Check the applications list for client ID ${state.target?.clientId ?? "you submitted"} before another explicit clone. The source details do not report clone completion.` : state.target ? "Reload current details before explicitly making another change." : "Check the applications list and details before attempting another create."} {!publicClient && !retiring && !lifecycle && state.target?.action !== "edit" && "An undisplayed secret cannot be recovered. You can explicitly replace it from current details."}</p>}
    {(state.outcome === "created" || state.outcome === "replaced" || state.outcome === "cloned") && <p>The {state.outcome === "created" ? "application was created" : state.outcome === "cloned" ? "application was cloned" : "secret was replaced"}, but its one-time secret has been removed from this view. Details cannot retrieve it; you can explicitly replace it from current details.</p>}
    {state.outcome === "browser-created" && <p>The browser application was created. Reload details to read its callbacks and allowed origins.</p>}
    {state.outcome === "native-created" && <p>The native application was created. Reload details to read its registered callbacks.</p>}
    {state.outcome === "updated" && <p>Your changes were saved. Reload details to read the current configuration.</p>}
    {state.outcome === "not-sent" && <p>No {state.target?.action === "edit" ? "edit" : state.target?.action === "replace" ? "secret replacement" : retiring ? "retirement" : lifecycle ? "lifecycle" : "creation"} request was sent by this operation.</p>}
    {state.target && !(cloning && state.outcome === "unknown") && state.reason !== "expired" && <div className="actions"><Button onClick={reload}>Reload current details</Button></div>}
    {(state.reason === "expired" || state.reason === "forbidden" && sensitive) && <div className="actions"><Button asChild><a href="/bff/login">Sign in</a></Button></div>}
  </div>;
}
