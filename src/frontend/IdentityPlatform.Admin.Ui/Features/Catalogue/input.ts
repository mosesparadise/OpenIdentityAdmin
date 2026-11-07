import type { components } from "../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { text } from "../Applications/Editor/input";
export type Kind = "scope" | "audience";
export type AudienceInput = components["schemas"]["AudienceInput"];
export type ScopeInput = components["schemas"]["ScopeInput"];
export type Input = AudienceInput | ScopeInput;
export type Audience = components["schemas"]["AudienceDetails"];
export type Scope = components["schemas"]["ScopeDetails"];
export type Details = Audience | Scope;
export type Page = components["schemas"]["CataloguePage"];
export type Job = components["schemas"]["CatalogueDeletionDetails"];
export type Errors = Partial<Record<keyof Draft, string>>;
export type Draft = { name: string; displayName: string; owner: string; enabled: boolean; kind: string; consentText: string; audienceId: string; disclosure: string };
const reserved = new Set(["openid", "offline_access", "profile", "phone", "address", "identity.admin", "identity-admin-api", "identity-admin-bff", "pilot_api"]);
/** Mirrors the deliberately finite protocol grammar without trimming or changing case. */
export function protocol(value: unknown, resource = false): value is string {
  return typeof value === "string" && value.length <= (resource ? 200 : 100) &&
    (resource ? /^[A-Za-z0-9][A-Za-z0-9._:/-]*$/ : /^[A-Za-z0-9][A-Za-z0-9._:-]*$/).test(value) &&
    !reserved.has(value.toLowerCase()) && !value.toLowerCase().startsWith("identityplatform");
}
/** Rejects route aliases before encoding one opaque identifier into a fixed browser path. */
export function addressable(value: unknown): value is string {
  return text(value, 100) && !/[\s\u0085]/u.test(value) && ![".", "..", "antiforgery", "deletions"].includes(value.toLowerCase());
}
/** Creates explicit editable fields without copying authority, revisions or unknown response properties into an input. */
export function draft(details?: Details): Draft {
  return { name: details ? "name" in details ? details.name : details.resource : "", displayName: details?.displayName ?? "",
    owner: details && "owner" in details ? details.owner : "", enabled: details?.enabled ?? true,
    kind: details && "kind" in details ? details.kind : "api", consentText: details && "consentText" in details ? details.consentText : "",
    audienceId: details && "audienceId" in details ? details.audienceId ?? "" : "", disclosure: details && "disclosure" in details ? details.disclosure : "none" };
}
/** Validates finite identity/API policies locally; the API independently rechecks current audiences and all authority. */
export function validate(kind: Kind, value: Draft): { input?: Input; errors: Errors } {
  const errors: Errors = {};
  if (typeof value.enabled !== "boolean") errors.enabled = "Choose whether the entry is enabled.";
  if (!protocol(value.name, kind === "audience")) errors.name = "Enter a unique supported identifier. System names are reserved.";
  if (!text(value.displayName, 200)) errors.displayName = "Enter a display name of 1–200 characters.";
  if (kind === "audience") {
    if (!text(value.owner, 200)) errors.owner = "Enter an owner or team of 1–200 characters.";
    return { errors, ...(Object.keys(errors).length ? {} : { input: { resource: value.name, displayName: value.displayName, owner: value.owner, enabled: value.enabled } }) };
  }
  if (!text(value.consentText, 500)) errors.consentText = "Enter clear consent text of 1–500 characters.";
  if (value.kind !== "identity" && value.kind !== "api") errors.kind = "Choose an identity or API scope.";
  if (value.kind === "api" && !addressable(value.audienceId)) errors.audienceId = "Choose an audience.";
  if (!["none", "email"].includes(value.disclosure) || value.kind === "api" && value.disclosure !== "none" ||
      value.name.toLowerCase() === "email" && (value.name !== "email" || value.kind !== "identity" || value.disclosure !== "email")) errors.disclosure = "The email scope must be an identity scope sharing email and its verification status.";
  const input: ScopeInput = { name: value.name, displayName: value.displayName, enabled: value.enabled, kind: value.kind as ScopeInput["kind"],
    consentText: value.consentText, audienceId: value.kind === "api" ? value.audienceId : null, disclosure: value.disclosure as ScopeInput["disclosure"] };
  return { errors, ...(Object.keys(errors).length ? {} : { input }) };
}
