import { useEffect, useRef, useState } from "react";
import { Button } from "../../../Components/Button";
import { emptyDraft, type Draft, type Errors } from "./input";
const labels: Record<keyof Draft, string> = { clientId: "Client ID", displayName: "Display name", redirectUris: "HTTPS callbacks", credentialExpiresAt: "Secret expiry (UTC)" };
/** Presents explicit, bounded registration choices with linked errors and no default credential lifetime. */
export function CreateForm({ submit, cancel }: { submit: (draft: Draft) => Promise<Errors>; cancel: () => void }) {
  const [draft, setDraft] = useState<Draft>({ ...emptyDraft });
  const [errors, setErrors] = useState<Errors>({});
  const summary = useRef<HTMLDivElement>(null);
  useEffect(() => { if (Object.keys(errors).length) summary.current?.focus(); }, [errors]);
  return <form className="application-editor-form" noValidate autoComplete="off" onSubmit={async event => { event.preventDefault(); setErrors(await submit({ ...draft })); }}>
    <CreateFields draft={draft} errors={errors} setDraft={setDraft} summary={summary} />
    <div className="editor-notice"><h3>Save the secret immediately after creation</h3><p>The generated secret is shown once. Hiding it, switching tabs, leaving this page or session expiry removes it from this view. It cannot be retrieved later. Secret replacement is not yet available in this workspace.</p></div>
    <div className="actions"><Button type="submit">Create application</Button><Button secondary type="button" onClick={cancel}>Cancel</Button></div>
  </form>;
}

/** Renders the same linked field/error projection for initial and failed local submissions. */
export function CreateFields({ draft, errors, setDraft, summary }: { draft: Draft; errors: Errors; setDraft: React.Dispatch<React.SetStateAction<Draft>>; summary: React.RefObject<HTMLDivElement | null> }) {
  const field = (name: keyof Draft) => ({ id: `editor-${name}`, name, value: draft[name],
    onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setDraft(value => ({ ...value, [name]: event.target.value })),
    "aria-invalid": !!errors[name], "aria-describedby": `editor-${name}-help${errors[name] ? ` editor-${name}-error` : ""}` });
  const error = (name: keyof Draft) => errors[name] && <p className="field-error" id={`editor-${name}-error`}>{errors[name]}</p>;
  return <>
    {!!Object.keys(errors).length && <div className="editor-notice" role="alert" tabIndex={-1} ref={summary}><h3>Check these fields</h3><ul>{(Object.keys(errors) as (keyof Draft)[]).map(name => <li key={name}><a href={`#editor-${name}`}>{labels[name]}: {errors[name]}</a></li>)}</ul></div>}
    <div><label htmlFor="editor-clientId">Client ID <span>(required)</span></label><input {...field("clientId")} maxLength={100} required spellCheck={false} />
      <p id="editor-clientId-help">A unique, permanent identifier. Case-sensitive; no spaces. It cannot be renamed.</p>{error("clientId")}</div>
    <div><label htmlFor="editor-displayName">Display name <span>(required)</span></label><input {...field("displayName")} maxLength={200} required />
      <p id="editor-displayName-help">The application name shown during sign-in and consent.</p>{error("displayName")}</div>
    <div><label htmlFor="editor-redirectUris">HTTPS callbacks <span>(required)</span></label><textarea {...field("redirectUris")} rows={4} maxLength={20489} required spellCheck={false} placeholder="https://app.example/callback" />
      <p id="editor-redirectUris-help">One exact HTTPS URL per line, up to 10. No spaces, credentials or fragments. Values are not trimmed or changed.</p>{error("redirectUris")}</div>
    <div><label htmlFor="editor-credentialExpiresAt">Secret expiry (UTC) <span>(required)</span></label><input {...field("credentialExpiresAt")} maxLength={20} required spellCheck={false} placeholder="2030-12-31T23:59:59Z" />
      <p id="editor-credentialExpiresAt-help">Choose a future time in UTC: YYYY-MM-DDTHH:mm:ssZ. There is no default expiry or local-time conversion.</p>{error("credentialExpiresAt")}</div>
  </>;
}
