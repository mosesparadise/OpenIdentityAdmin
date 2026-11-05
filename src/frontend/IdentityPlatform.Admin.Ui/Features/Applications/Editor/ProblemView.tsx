import { Button } from "../../../Components/Button";
import type { EditorState } from "./controller";
/** Describes uncertainty without claiming rollback or presenting an automatic mutation retry. */
export function ProblemView({ state, reload }: { state: Extract<EditorState, { kind: "problem" }>; reload?: () => void }) {
  const lifecycle = state.target?.action === "disable" || state.target?.action === "enable";
  const sensitive = lifecycle || state.target?.action === "replace";
  const message = state.reason === "expired" ? "Your session observation has ended. Sign in again to continue."
    : state.reason === "display-expired" ? "This display has expired. Return to applications to read current details; a secret’s expiry does not end your session."
    : state.reason === "forbidden" ? state.target?.action === "replace" ? "Secret replacement requires current credential permission and a recent MFA sign-in. Sign in again to refresh your authentication; contact your administrator if access remains restricted." : lifecycle ? "Changing application status requires current lifecycle permission and a recent MFA sign-in. Sign in again or contact your administrator." : "Your current permissions do not allow this operation."
    : state.reason === "missing" ? "This registration could not be found."
    : state.reason === "stale" ? "This version is no longer current. Reload current details before making another change. Your draft will not be reapplied."
    : state.reason === "conflict" ? "The server reported a registration conflict."
    : state.reason === "invalid" ? "The server could not accept this request."
    : state.reason === "cleared" ? "The editor was cleared when you left or hid this view."
    : "We couldn’t confirm the requested operation.";
  return <div className="editor-notice"><p>{message}</p>
    {state.outcome === "unknown" && <p><strong>{state.target?.action === "edit" ? "Your changes may have been saved." : state.target?.action === "replace" ? "The secret may have changed." : lifecycle ? "The application status may have changed." : "The application may have been created."}</strong> {state.target ? "Reload current details before explicitly making another change." : "Check the applications list and details before attempting another create."} {!lifecycle && state.target?.action !== "edit" && "An undisplayed secret cannot be recovered. You can explicitly replace it from current details."}</p>}
    {(state.outcome === "created" || state.outcome === "replaced") && <p>The {state.outcome === "created" ? "application was created" : "secret was replaced"}, but its one-time secret has been removed from this view. Details cannot retrieve it; you can explicitly replace it from current details.</p>}
    {state.outcome === "updated" && <p>Your changes were saved. Reload details to read the current configuration.</p>}
    {state.outcome === "not-sent" && <p>No {state.target?.action === "edit" ? "edit" : state.target?.action === "replace" ? "secret replacement" : lifecycle ? "lifecycle" : "creation"} request was sent by this operation.</p>}
    {state.target && state.reason !== "expired" && <div className="actions"><Button onClick={reload}>Reload current details</Button></div>}
    {(state.reason === "expired" || state.reason === "forbidden" && sensitive) && <div className="actions"><Button asChild><a href="/bff/login">Sign in</a></Button></div>}
  </div>;
}
