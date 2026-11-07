import { useEffect, useRef, useState } from "react";
import { Button } from "../../Components/Button";
import { NativeSelect } from "../../Components/NativeSelect";
import { draft, type Kind, type Details, type Draft, type Errors, type Page } from "./input";
const labels: Record<keyof Draft, string> = { name: "Identifier", displayName: "Display name", owner: "Owner", enabled: "Enabled", kind: "Scope type", consentText: "Consent text", audienceId: "Audience", disclosure: "Identity claims" };
/** Collects complete finite catalogue policy with immutable identity/type on edit and explicit consent wording. */
export function CatalogueForm({ kind, base, audiences, choicesError, searchAudiences, submit, cancel }: {
  kind: Kind; base?: Details; audiences?: Page; choicesError?: string; searchAudiences: (search: string) => void;
  submit: (value: Draft) => Promise<Errors>; cancel: () => void;
}) {
  const [value, setValue] = useState(() => draft(base)), [errors, setErrors] = useState<Errors>({}), [search, setSearch] = useState("");
  const summary = useRef<HTMLDivElement>(null);
  useEffect(() => { if (Object.keys(errors).length) summary.current?.focus(); }, [errors]);
  const field = (name: "name" | "displayName" | "owner" | "consentText") => ({ id: `catalogue-${name}`, value: value[name], name,
    onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setValue(current => ({ ...current, [name]: event.target.value })),
    "aria-invalid": !!errors[name], "aria-describedby": errors[name] ? `catalogue-${name}-error` : undefined });
  const error = (name: keyof Draft) => errors[name] && <p className="field-error" id={`catalogue-${name}-error`}>{errors[name]}</p>;
  return <form className="application-editor-form" noValidate autoComplete="off" onSubmit={async event => { event.preventDefault(); setErrors(await submit(value)); }}>
    {!!Object.keys(errors).length && <div role="alert" tabIndex={-1} ref={summary} className="editor-notice"><h3>Check these fields</h3><ul>{(Object.keys(errors) as (keyof Draft)[]).map(key => <li key={key}><a href={`#catalogue-${key}`}>{labels[key]}: {errors[key]}</a></li>)}</ul></div>}
    <div><label htmlFor="catalogue-name">{kind === "scope" ? "Scope name" : "Resource identifier"} (required)</label><input {...field("name")} required readOnly={!!base} spellCheck={false} maxLength={kind === "scope" ? 100 : 200} />
      <p>The permanent, case-sensitive identifier. System identifiers are reserved.</p>{error("name")}</div>
    <div><label htmlFor="catalogue-displayName">Display name (required)</label><input {...field("displayName")} required maxLength={200} />{error("displayName")}</div>
    {kind === "audience" ? <div><label htmlFor="catalogue-owner">Owner or team (required)</label><input {...field("owner")} required maxLength={200} />{error("owner")}</div> : <>
      <div><label htmlFor="catalogue-kind">Scope type</label><NativeSelect id="catalogue-kind" value={value.kind} disabled={!!base} aria-invalid={!!errors.kind} aria-describedby={errors.kind ? "catalogue-kind-error" : undefined} onChange={event => setValue(current => ({ ...current, kind: event.target.value, audienceId: "", disclosure: "none" }))}>
        <option value="api">API access</option><option value="identity">Identity information</option></NativeSelect><p>The type cannot change after creation.</p>{error("kind")}</div>
      <div><label htmlFor="catalogue-consentText">Consent text (required)</label><textarea {...field("consentText")} rows={3} maxLength={500} required />
        <p>Describe clearly what the application may receive or access.</p>{error("consentText")}</div>
      {value.kind === "api" ? <div><label htmlFor="catalogue-audienceSearch">Find an audience</label><div className="actions"><input id="catalogue-audienceSearch" value={search} maxLength={100} onChange={event => setSearch(event.target.value)} /><Button type="button" secondary onClick={() => searchAudiences(search)}>Search audiences</Button></div>
        <label htmlFor="catalogue-audienceId">Audience (required)</label><NativeSelect id="catalogue-audienceId" value={value.audienceId} aria-invalid={!!errors.audienceId} aria-describedby={errors.audienceId ? "catalogue-audienceId-error" : undefined} onChange={event => setValue(current => ({ ...current, audienceId: event.target.value }))}>
          <option value="">Choose an audience</option>{value.audienceId && !audiences?.items.some(item => item.id === value.audienceId) && <option value={value.audienceId}>{base && "audienceId" in base && base.audienceId === value.audienceId ? base.resource : value.audienceId} (selected; outside these search results)</option>}
          {audiences?.items.map(item => <option key={item.id} value={item.id} disabled={!item.enabled}>{item.displayName} · {item.name}{item.enabled ? "" : " (disabled)"}</option>)}</NativeSelect>
        {audiences?.hasMore && <p>More audiences match. Narrow the search to find the one you need.</p>}{choicesError && <p role="status">Audience choices are unavailable. Check your access, or try the search again.</p>}{error("audienceId")}</div>
        : <div><label htmlFor="catalogue-disclosure">Identity claims</label><NativeSelect id="catalogue-disclosure" value={value.disclosure} aria-invalid={!!errors.disclosure} aria-describedby={errors.disclosure ? "catalogue-disclosure-error" : undefined} onChange={event => setValue(current => ({ ...current, disclosure: event.target.value }))}>
          <option value="none">No additional claims</option><option value="email">Email and verification status, after consent</option></NativeSelect><p>No username, administrator role or permission claims are included.</p>{error("disclosure")}</div>}
    </>}
    <label className="catalogue-check"><input id="catalogue-enabled" type="checkbox" checked={value.enabled} aria-invalid={!!errors.enabled} aria-describedby={errors.enabled ? "catalogue-enabled-error" : undefined} onChange={event => setValue(current => ({ ...current, enabled: event.target.checked }))} /> Enabled</label>{error("enabled")}
    <p>Changes require a recent password and authenticator sign-in.</p>
    <div className="actions"><Button type="submit">{base ? "Save changes" : `Create ${kind}`}</Button><Button type="button" secondary onClick={cancel}>Cancel</Button></div>
  </form>;
}
