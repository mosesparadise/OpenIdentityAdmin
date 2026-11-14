import { useEffect, useRef, useState } from "react";
import { Button } from "../../../Components/Button";
import { CreateFields } from "./CreateForm";
import { emptyDraft, type CloneDraft, type Draft, type Errors } from "./input";
import type { Observed } from "./observation";

/** Reviews one source version without editable callbacks/scopes and requires explicit new identity, expiry and confirmation. */
export function CloneForm({ base, submit, cancel }: { base: Observed & { scopes: string[] }; submit: (draft: CloneDraft) => Promise<Errors>; cancel: () => void }) {
  const [draft, setDraft] = useState<Draft>({ ...emptyDraft, displayName: base.details.displayName!, redirectUris: base.details.redirectUris.join("\n") });
  const [confirmed, setConfirmed] = useState(false);
  const [errors, setErrors] = useState<Errors>({});
  const summary = useRef<HTMLDivElement>(null);
  useEffect(() => { if (Object.keys(errors).length) summary.current?.focus(); }, [errors]);
  return <form className="application-editor-form" noValidate autoComplete="off" onSubmit={async event => {
    event.preventDefault(); setErrors(await submit({ confirmed, clientId: draft.clientId, displayName: draft.displayName, credentialExpiresAt: draft.credentialExpiresAt }));
  }}>
    <p>Clone configuration from <strong>{base.details.clientId}</strong>. The new application starts disabled and gets a new one-time secret. User grants, consent, sessions and history are not copied.</p>
    <CreateFields mode="clone" draft={draft} errors={errors} setDraft={setDraft} summary={summary} />
    <div><h3>Assigned scopes to copy</h3>{base.scopes.length ? <ul>{base.scopes.map(scope => <li key={scope}>{scope}</li>)}</ul> : <p>No additional scopes.</p>}
      <p>These exact names and callbacks are copied from the reviewed source version. Every assigned scope must still be eligible when cloning commits.</p></div>
    <div><label htmlFor="editor-confirmed"><input id="editor-confirmed" type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)}
      aria-invalid={!!errors.confirmed} aria-describedby={errors.confirmed ? "editor-confirmed-error" : undefined} /> I reviewed this configuration and want a separate disabled application with a new credential.</label>
      {errors.confirmed && <p id="editor-confirmed-error" className="field-error">{errors.confirmed}</p>}</div>
    <p>Save the new secret immediately. Hiding the page clears it; a lost secret requires explicit replacement.</p>
    <div className="actions"><Button type="submit">Clone application</Button><Button secondary type="button" onClick={cancel}>Cancel</Button></div>
  </form>;
}
