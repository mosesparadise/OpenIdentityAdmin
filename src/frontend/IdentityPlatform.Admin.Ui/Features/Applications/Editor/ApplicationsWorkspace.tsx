import { useLayoutEffect, useRef } from "react";
import { Button } from "../../../Components/Button";
import type { SessionState } from "../../Session/observation";
import { ApplicationsPanel } from "../ApplicationsPanel";
import { focusList } from "./focus";
import { useEditor } from "./useEditor";
import { CreateForm } from "./CreateForm";
import { DetailsView } from "./DetailsView";
import { ProblemView } from "./ProblemView";

/** Keeps create/details navigation in ephemeral memory on the existing native Aspire Applications route. */
export function ApplicationsWorkspace({ session }: { session: SessionState }) {
  const { state, owner } = useEditor(session);
  const heading = useRef<HTMLHeadingElement>(null);
  const createButton = useRef<HTMLButtonElement>(null);
  const listHeading = useRef<HTMLHeadingElement>(null);
  const previous = useRef(state.kind);
  useLayoutEffect(() => {
    if (state.kind !== "list") heading.current?.focus();
    else if (previous.current !== "list") focusList(createButton.current, listHeading.current);
    previous.current = state.kind;
  }, [state]);
  const back = () => state.kind === "working" ? owner.current?.suspend() : owner.current?.list();
  if (state.kind === "list") return <>
    <h2 tabIndex={-1} ref={listHeading}>Registered applications</h2>
    <div className="application-create-bar"><Button ref={createButton} disabled={session.kind !== "authenticated"} onClick={() => owner.current?.newForm()}>Create application</Button>
      <p>{session.kind === "authenticated" ? "Your current permissions are checked when you submit." : "Sign in to create an application."}</p></div>
    <ApplicationsPanel onDetails={id => { void owner.current?.details(id); }} />
  </>;
  const title = state.kind === "form" ? "Create application" : state.kind === "created" ? "Application created"
    : state.kind === "details" ? "Application details" : state.kind === "working" ? state.operation === "create" ? "Creating application…" : "Loading application details…" : "Review application status";
  return <section className="applications-card application-editor" aria-labelledby="editor-heading" aria-busy={state.kind === "working"}>
    <div role="status" aria-live="polite" aria-atomic="true"><h2 id="editor-heading" tabIndex={-1} ref={heading}>{title}</h2></div>
    {state.kind === "form" && <><p className="editor-help">Register a confidential web application for OpenID sign-in using authorization code and PKCE S256. Each sign-in requires fresh password/MFA and explicit consent.</p>
      <CreateForm submit={draft => owner.current?.create(draft) ?? Promise.resolve({})} cancel={back} /></>}
    {state.kind === "working" && <p className="editor-help">{state.operation === "create" ? "Keep this page visible. Creation is sent once; an interrupted response may leave a created application without its one-time secret." : "Checking current access and reading sanitized metadata."}</p>}
    {(state.kind === "details" || state.kind === "created") && <DetailsView details={state.details} secret={state.kind === "created" ? state.secret : undefined} dismiss={() => owner.current?.dismiss()} />}
    {state.kind === "problem" && <ProblemView state={state} />}
    {state.kind !== "form" && <div className="actions"><Button secondary onClick={back}>{state.kind === "working" ? "Stop waiting" : "Back to applications"}</Button></div>}
  </section>;
}
