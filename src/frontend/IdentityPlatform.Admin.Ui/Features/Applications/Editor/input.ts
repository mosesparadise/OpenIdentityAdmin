import type { components } from "../../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
export type CreateInput = components["schemas"]["ApplicationCreate"];
export type EditInput = components["schemas"]["ApplicationEdit"];
export type ReplacementInput = components["schemas"]["ApplicationSecretReplacement"];
export type EditDraft = Pick<Draft, "displayName" | "redirectUris">;
export type ReplacementDraft = { confirmed: boolean; credentialExpiresAt: string };
export type Draft = { clientId: string; displayName: string; redirectUris: string; credentialExpiresAt: string };
export type Errors = Partial<Record<keyof Draft | "confirmed", string>>;
export const emptyDraft: Draft = { clientId: "", displayName: "", redirectUris: "", credentialExpiresAt: "" };
const whitespace = /[\s\u0085]/u;
const controls = /[\u0000-\u001f\u007f-\u009f]/u;

/** Rejects malformed UTF-16 rather than allowing serialization to replace identity characters. */
export function text(value: unknown, maximum: number): value is string {
  if (typeof value !== "string" || !value.trim() || value.length > maximum || controls.test(value)) return false;
  for (let i = 0; i < value.length; i++) {
    const unit = value.charCodeAt(i);
    if (unit >= 0xd800 && unit <= 0xdbff) {
      const next = value.charCodeAt(++i);
      if (!(next >= 0xdc00 && next <= 0xdfff)) return false;
    } else if (unit >= 0xdc00 && unit <= 0xdfff) return false;
  }
  return true;
}
/** Preserves exact canonical HTTPS callbacks, without trimming, implicit encoding or normalization. */
export function callback(value: unknown): value is string {
  if (!text(value, 2048) || whitespace.test(value) || /%(?![\da-f]{2})/i.test(value)) return false;
  try { const uri = new URL(value); return uri.protocol === "https:" && !uri.username && !uri.password && !uri.hash && uri.href === value; }
  catch { return false; }
}
/** Accepts an actual UTC instant and rejects calendar/parser aliases. */
export function utc(value: unknown, whole = false): number {
  if (typeof value !== "string" || !(whole ? /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/ : /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$/).test(value) || value.startsWith("0000")) return NaN;
  const time = Date.parse(value);
  return Number.isFinite(time) && new Date(time).toISOString().slice(0, 19) === value.slice(0, 19) ? time : NaN;
}
/** Builds only the approved generated input fields; explicit expiry has no inferred duration or timezone conversion. */
export function validate(draft: Draft, now: number): { input?: CreateInput; errors: Errors } {
  const errors: Errors = {};
  if (!text(draft.clientId, 100) || whitespace.test(draft.clientId)) errors.clientId = "Enter 1–100 characters without whitespace or control characters.";
  if (!text(draft.displayName, 200)) errors.displayName = "Enter a name of 1–200 characters without control characters.";
  const redirectUris = draft.redirectUris.split(/\r?\n/);
  if (redirectUris.length > 10 || redirectUris.some(value => !callback(value)) || new Set(redirectUris).size !== redirectUris.length)
    errors.redirectUris = "Enter 1–10 different, exact HTTPS callbacks, one per line, without spaces, credentials or fragments.";
  if (!(utc(draft.credentialExpiresAt, true) > now)) errors.credentialExpiresAt = "Enter a future UTC date and time as YYYY-MM-DDTHH:mm:ssZ.";
  const input = { clientId: draft.clientId, displayName: draft.displayName, redirectUris, credentialExpiresAt: draft.credentialExpiresAt };
  if (new TextEncoder().encode(JSON.stringify(input)).length > 32768) errors.redirectUris = "The registration exceeds the 32 KiB request limit.";
  return Object.keys(errors).length ? { errors } : { errors, input };
}
/** Keeps the literal antiforgery endpoint and URL dot segments out of details navigation. */
export function addressable(id: string): boolean { return text(id, 400) && id !== "." && id !== ".." && id.toLowerCase() !== "antiforgery"; }

/** Validates only mutable presentation fields; neither identity nor credential lifecycle can enter PUT input. */
export function validateEdit(draft: EditDraft): { input?: EditInput; errors: Errors } {
  const errors: Errors = {};
  if (!text(draft.displayName, 200)) errors.displayName = "Enter a name of 1–200 characters without control characters.";
  const redirectUris = draft.redirectUris.split(/\r?\n/);
  if (redirectUris.length > 10 || redirectUris.some(value => !callback(value)) || new Set(redirectUris).size !== redirectUris.length)
    errors.redirectUris = "Enter 1–10 different, exact HTTPS callbacks, one per line, without spaces, credentials or fragments.";
  const input = { displayName: draft.displayName, redirectUris };
  if (new TextEncoder().encode(JSON.stringify(input)).length > 32768) errors.redirectUris = "The change exceeds the 32 KiB request limit.";
  return Object.keys(errors).length ? { errors } : { errors, input };
}
/** Requires a fresh explicit confirmation and expiry, without accepting a previous or caller-selected secret. */
export function validateReplacement(draft: ReplacementDraft, now: number): { input?: ReplacementInput; errors: Errors } {
  const errors: Errors = {};
  if (draft.confirmed !== true) errors.confirmed = "Confirm that the current secret will stop working immediately.";
  if (!(utc(draft.credentialExpiresAt, true) > now)) errors.credentialExpiresAt = "Enter a future UTC date and time as YYYY-MM-DDTHH:mm:ssZ.";
  return Object.keys(errors).length ? { errors } : { errors, input: { confirmed: true, credentialExpiresAt: draft.credentialExpiresAt } };
}
