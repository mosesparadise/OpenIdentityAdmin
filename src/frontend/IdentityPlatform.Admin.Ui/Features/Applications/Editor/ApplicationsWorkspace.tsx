import { useLayoutEffect, useRef } from "react";
import { Button } from "../../../Components/Button";
import type { SessionState } from "../../Session/observation";
import { ApplicationsPanel } from "../ApplicationsPanel";
import { focusList } from "./focus";
import { useEditor } from "./useEditor";
import { LifecycleForm } from "./LifecycleForm";
import { UpdateForm } from "./UpdateForm";
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
  const lifecycleButton = useRef<HTMLButtonElement>(null);
  useLayoutEffect(() => {
    if (state.kind === "details" && (previous.current === "disable-form" || previous.current === "enable-form")) lifecycleButton.current?.focus();
    else if (state.kind !== "list") heading.current?.focus();
    else if (previous.current !== "list") focusList(createButton.current, listHeading.current);
    previous.current = state.kind;
  }, [state]);
  const back = () => state.kind === "working" ? owner.current?.suspend() : owner.current?.list();
  if (state.kind === "list") return <>
    <h2 tabIndex={-1} ref={listHeading}>Application actions</h2>
    <div className="application-create-bar"><Button ref={createButton} disabled={session.kind !== "authenticated"} onClick={() => owner.current?.newForm()}>Create application</Button>
      <p>{session.kind === "authenticated" ? "Your current permissions are checked when you submit." : "Sign in to create an application."}</p></div>
    <ApplicationsPanel onDetails={id => { void owner.current?.details(id); }} />
  </>;
  const title = state.kind === "form" ? "Create application" : state.kind === "created" ? "Application created"
     : state.kind === "edit-form" ? "Edit application" : state.kind === "replacement-form" ? "Replace secret" : state.kind === "updated" ? "Application updated" : state.kind === "replaced" ? "Secret replaced"
     : state.kind === "disable-form" ? "Disable application" : state.kind === "enable-form" ? "Re-enable application"
    : state.kind === "details" ? "Application details" : state.kind === "working" ? state.operation === "create" ? "Creating application…" : state.operation === "edit" ? "Saving changes…" : state.operation === "replace" ? "Replacing secret…" : state.operation === "disable" ? "Disabling application…" : state.operation === "enable" ? "Re-enabling application…" : "Loading application details…" : "Review application status";
  return <section className="applications-card application-editor" aria-labelledby="editor-heading" aria-busy={state.kind === "working"}>
    <div role="status" aria-live="polite" aria-atomic="true"><h2 id="editor-heading" tabIndex={-1} ref={heading}>{title}</h2></div>
    {state.kind === "form" && <><p className="editor-help">Register a confidential web application for OpenID sign-in using authorization code and PKCE S256. Each sign-in requires fresh password/MFA and explicit consent.</p>
      <CreateForm submit={draft => owner.current?.create(draft) ?? Promise.resolve({})} cancel={back} /></>}
    {(state.kind === "edit-form" || state.kind === "replacement-form") && <UpdateForm details={state.details} action={state.kind === "edit-form" ? "edit" : "replace"}
      edit={draft => owner.current?.edit(draft) ?? Promise.resolve({})} replace={draft => owner.current?.replace(draft) ?? Promise.resolve({})} cancel={() => owner.current?.cancelForm()} />}
    {(state.kind === "disable-form" || state.kind === "enable-form") && <LifecycleForm details={state.details} enabled={state.kind === "enable-form"}
      submit={input => owner.current?.lifecycle(input) ?? Promise.resolve({})} cancel={() => owner.current?.cancelForm()} />}
    {state.kind === "working" && <p className="editor-help">{state.operation === "create" ? "Keep this page visible. Creation is sent once; an interrupted response may leave a created application without its one-time secret." : state.operation !== "read" ? "Keep this page visible. This change is sent once. An interrupted response may leave an unknown outcome; it will not be retried automatically." : "Checking current access and reading sanitized metadata."}</p>}
    {(state.kind === "details" || state.kind === "created" || state.kind === "updated" || state.kind === "replaced") && <DetailsView details={state.details} secret={state.kind === "created" || state.kind === "replaced" ? state.secret : undefined} dismiss={() => owner.current?.dismiss()} edit={() => owner.current?.open("edit")} replace={() => owner.current?.open("replace")} lifecycleRef={lifecycleButton} lifecycle={() => owner.current?.open(state.details.enabled ? "disable" : "enable")} />}
    {state.kind === "problem" && <ProblemView state={state} reload={() => { if (state.target) void owner.current?.details(state.target.id); }} />}
    {state.kind !== "form" && state.kind !== "edit-form" && state.kind !== "replacement-form" && state.kind !== "disable-form" && state.kind !== "enable-form" && <div className="actions"><Button secondary onClick={back}>{state.kind === "working" ? "Stop waiting" : "Back to applications"}</Button></div>}
  </section>;
}
