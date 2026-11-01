import { Button } from "../../../Components/Button";
import type { EditorState } from "./controller";
/** Describes uncertainty without claiming rollback or presenting an automatic mutation retry. */
export function ProblemView({ state }: { state: Extract<EditorState, { kind: "problem" }> }) {
  const message = state.reason === "expired" ? "Your session observation has ended. Sign in again to continue."
    : state.reason === "display-expired" ? "This display has expired. Return to applications to read current details; a secret’s expiry does not end your session."
    : state.reason === "forbidden" ? "Your current permissions do not allow this operation."
    : state.reason === "missing" ? "This registration could not be found."
    : state.reason === "conflict" ? "The server reported a registration conflict."
    : state.reason === "invalid" ? "The server could not accept this request."
    : state.reason === "cleared" ? "The editor was cleared when you left or hid this view."
    : "We couldn’t confirm the requested operation.";
  return <div className="editor-notice"><p>{message}</p>
    {state.outcome === "unknown" && <p><strong>The application may have been created.</strong> Check the applications list and details before attempting another create. The original secret cannot be retrieved, and secret replacement is not yet available here.</p>}
    {state.outcome === "created" && <p>The application was created, but its one-time secret has been removed from this view. Details cannot retrieve it; secret replacement is not yet available here.</p>}
    {state.outcome === "not-sent" && <p>No creation request was sent by this operation.</p>}
    {state.reason === "expired" && <div className="actions"><Button asChild><a href="/bff/login">Sign in</a></Button></div>}
  </div>;
}
