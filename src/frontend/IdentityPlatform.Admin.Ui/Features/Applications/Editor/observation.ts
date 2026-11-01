import type { components } from "../../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { callback, text, utc, type CreateInput } from "./input";
export type Details = components["schemas"]["ApplicationDetails"];
export type Result = { status: number; value?: unknown; etag?: string | null };
export type Observed = { details: Details; expires: number; etag: string | null };
export type Reason = "expired" | "forbidden" | "missing" | "invalid" | "conflict" | "unavailable" | "cleared" | "display-expired";
const fields = ["id", "clientId", "displayName", "redirectUris", "profile", "editable", "credential", "expiresAt"];
/** Requires exact JSON members before copying a sanitized projection into UI state. */
export function object(value: unknown, keys: string[]): value is Record<string, unknown> {
  return !!value && typeof value === "object" && !Array.isArray(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key));
}
/** Maps only finite HTTP status, never raw problem details or exceptions. */
export function reason(status: number): Reason {
  return status === 401 ? "expired" : status === 403 ? "forbidden" : status === 404 ? "missing" : status === 400 ? "invalid" : status === 409 ? "conflict" : "unavailable";
}
/** Checks the strong tag's exact resource hash and portable positive revision, including initial revision one on create. */
async function version(value: string | null | undefined, id: string, created: boolean): Promise<boolean> {
  if (!value || !/^"[0-9a-f]{64}-[1-9][0-9]{0,18}"$/.test(value)) return false;
  const revision = BigInt(value.slice(66, -1));
  if (revision > 9223372036854775807n || created && revision !== 1n) return false;
  const hash = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(id));
  const hex = Array.from(new Uint8Array(hash), byte => byte.toString(16).padStart(2, "0")).join("");
  return value === `"${hex}-${revision}"`;
}
/** Validates managed or explicitly read-only details and original server observation expiry; credentials never enter this projection. */
export async function observe(result: Result, id: string | null, now: number, input?: CreateInput): Promise<Observed | null> {
  const dto = result.value;
  if (!object(dto, fields) || !text(dto.id, 400) || id !== null && dto.id !== id || !text(dto.clientId, 100) ||
      !(dto.displayName === null || typeof dto.displayName === "string" && dto.displayName.length <= 200) ||
      typeof dto.editable !== "boolean" || !Array.isArray(dto.redirectUris)) return null;
  const expires = utc(dto.expiresAt);
  if (!(expires > now && expires <= now + 300000)) return null;
  let credential: Details["credential"] = null;
  if (dto.editable) {
    if (!text(dto.displayName, 200) || /[\s\u0085]/u.test(dto.clientId) || dto.profile !== "managed-web-v1" || dto.redirectUris.length < 1 || dto.redirectUris.length > 10 ||
        !dto.redirectUris.every(callback) || new Set(dto.redirectUris).size !== dto.redirectUris.length ||
        !object(dto.credential, ["issuedAt", "expiresAt"]) || !(wholeUtc(dto.credential.issuedAt) < wholeUtc(dto.credential.expiresAt)) ||
        !await version(result.etag, dto.id, !!input)) return null;
    credential = { issuedAt: dto.credential.issuedAt as string, expiresAt: dto.credential.expiresAt as string };
  } else if (input || dto.profile !== null || dto.credential !== null || dto.redirectUris.length || result.etag != null) return null;
  if (input && (dto.clientId !== input.clientId || dto.displayName !== input.displayName ||
      JSON.stringify([...dto.redirectUris].sort()) !== JSON.stringify([...input.redirectUris].sort()) || utc(credential!.expiresAt) !== utc(input.credentialExpiresAt, true) ||
      !(utc(credential!.expiresAt) > now))) return null;
  return { details: { id: dto.id, clientId: dto.clientId, displayName: dto.displayName as string | null,
    redirectUris: [...dto.redirectUris] as string[], profile: dto.profile as string | null, editable: dto.editable,
    credential, expiresAt: dto.expiresAt as string }, expires, etag: result.etag ?? null };
}
/** Accepts only the native one-time secret shape and exact creation response, independently of generated static types. */
export async function created(result: Result, input: CreateInput, now: number): Promise<(Observed & { secret: string }) | null> {
  if (result.status !== 201 || !object(result.value, ["application", "clientSecret"]) ||
      typeof result.value.clientSecret !== "string" || !/^[A-Za-z0-9+/]{43}=$/.test(result.value.clientSecret)) return null;
  const secret = result.value.clientSecret;
  if (btoa(atob(secret)) !== secret || atob(secret).length !== 32) return null;
  const observed = await observe({ ...result, value: result.value.application }, null, now, input);
  return observed ? { ...observed, secret } : null;
}

/** Native DateTimeOffset JSON may use either UTC suffix; managed metadata remains whole-second. */
function wholeUtc(value: unknown): number {
  return typeof value === "string" && /T\d{2}:\d{2}:\d{2}(?:Z|\+00:00)$/.test(value) ? utc(value) : NaN;
}
