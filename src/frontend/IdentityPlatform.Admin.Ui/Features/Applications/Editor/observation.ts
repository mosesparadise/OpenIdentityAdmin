import type { components } from "../../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { callback, nativeCallback, nativeProfile, browserProfile, browserOrigin, type NativeInput, type BrowserInput, type MachineInput, machineProfile, text, utc, type CloneInput, type CreateInput, type EditInput, type ReplacementInput } from "./input";
export type Details = components["schemas"]["ApplicationDetails"];
export type Result = { status: number; value?: unknown; etag?: string | null; location?: string | null };
export type Observed = { details: Details; expires: number; etag: string | null };
export type Reason = "expired" | "forbidden" | "missing" | "invalid" | "conflict" | "stale" | "unavailable" | "cleared" | "display-expired";
const fields = ["id", "clientId", "displayName", "redirectUris", "profile", "editable", "credential", "expiresAt", "enabled", "retired", "retirement"];
/** Requires exact JSON members before copying a sanitized projection into UI state. */
export function object(value: unknown, keys: string[]): value is Record<string, unknown> {
  return !!value && typeof value === "object" && !Array.isArray(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key));
}
/** Maps only finite HTTP status, never raw problem details or exceptions. */
export function reason(status: number): Reason {
  return status === 401 ? "expired" : status === 403 ? "forbidden" : status === 404 ? "missing" : status === 400 ? "invalid" : status === 409 ? "conflict" : status === 412 || status === 428 ? "stale" : "unavailable";
}
/** Checks the strong tag's exact resource hash and portable positive revision, including initial revision one on create. */
export async function version(value: string | null | undefined, id: string, created: boolean): Promise<boolean> {
  if (!value || !/^"[0-9a-f]{64}-[1-9][0-9]{0,18}"$/.test(value)) return false;
  const revision = BigInt(value.slice(66, -1));
  if (revision > 9223372036854775807n || created && revision !== 1n) return false;
  const hash = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(id));
  const hex = Array.from(new Uint8Array(hash), byte => byte.toString(16).padStart(2, "0")).join("");
  return value === `"${hex}-${revision}"`;
}
/** Validates managed or explicitly read-only details and original server observation expiry; credentials never enter this projection. */
export async function observe(result: Result, id: string | null, now: number, input?: CreateInput | NativeInput | BrowserInput | MachineInput): Promise<Observed | null> {
  const dto = result.value;
  const browser = !!dto && typeof dto === "object" && "profile" in dto && dto.profile === browserProfile;
  if (!object(dto, browser ? [...fields, "origins"] : fields) || !text(dto.id, 400) || id !== null && dto.id !== id || !text(dto.clientId, 100) ||
      !(dto.displayName === null || typeof dto.displayName === "string" && dto.displayName.length <= 200) ||
      typeof dto.retired !== "boolean" || typeof dto.editable !== "boolean" || !Array.isArray(dto.redirectUris)) return null;
  const expires = utc(dto.expiresAt);
  if (!(expires > now && expires <= now + 300000)) return null;
  const origins = dto.origins;
  if (browser && (!Array.isArray(origins) || origins.length < 1 || origins.length > 10 || !origins.every(browserOrigin) || new Set(origins).size !== origins.length)) return null;
  let credential: Details["credential"] = null;
  if (dto.editable || dto.retired) {
    if (dto.retired ? dto.editable || dto.enabled !== false || input || !retirementReference(dto.retirement, dto.credential, expires, now) : dto.retirement !== null) return null;
    const machine = dto.profile === machineProfile;
    const native = dto.profile === nativeProfile;
    if (typeof dto.enabled !== "boolean" || !text(dto.displayName, 200) || /[\s\u0085]/u.test(dto.clientId) || (!native && !browser && !machine && dto.profile !== "managed-web-v1") ||
        (machine ? dto.redirectUris.length !== 0 : dto.redirectUris.length < 1) || dto.redirectUris.length > 10 || !dto.redirectUris.every(native ? nativeCallback : callback) || new Set(dto.redirectUris).size !== dto.redirectUris.length ||
        !await version(result.etag, dto.id, !!input) || dto.retired && BigInt(result.etag!.slice(66, -1)) < 2n) return null;
    if (native || browser) {
      if (dto.credential !== null) return null;
    } else {
      if (!object(dto.credential, ["issuedAt", "expiresAt"]) || !(wholeUtc(dto.credential.issuedAt) < wholeUtc(dto.credential.expiresAt))) return null;
      credential = { issuedAt: dto.credential.issuedAt as string, expiresAt: dto.credential.expiresAt as string };
    }
  } else if (dto.retirement !== null || dto.enabled !== null || input || dto.profile !== null || dto.credential !== null || dto.redirectUris.length || result.etag != null) return null;
  if (input && (dto.enabled !== true || dto.clientId !== input.clientId || dto.displayName !== input.displayName ||
      JSON.stringify([...dto.redirectUris].sort()) !== JSON.stringify([...("redirectUris" in input ? input.redirectUris : [])].sort()))) return null;
  if (input && ("credentialExpiresAt" in input
      ? !credential || dto.profile !== ("redirectUris" in input ? "managed-web-v1" : machineProfile) || utc(credential.expiresAt) !== utc(input.credentialExpiresAt, true) || utc(credential.expiresAt) <= now
      : "origins" in input ? !browser || JSON.stringify(origins) !== JSON.stringify([...input.origins].sort()) : dto.profile !== nativeProfile)) return null;
  return { details: { id: dto.id, clientId: dto.clientId, displayName: dto.displayName as string | null,
    redirectUris: [...dto.redirectUris] as string[], profile: dto.profile as string | null, editable: dto.editable,
    ...(browser ? { origins: [...origins as string[]] } : {}), credential, retired: dto.retired, retirement: dto.retirement === null ? null : { ...(dto.retirement as NonNullable<Details["retirement"]>) }, enabled: dto.enabled as boolean | null, expiresAt: dto.expiresAt as string }, expires, etag: result.etag ?? null };
}
/** Accepts only the native one-time secret shape and exact creation response, independently of generated static types. */
export async function created(result: Result, input: CreateInput | MachineInput, now: number): Promise<(Observed & { secret: string }) | null> {
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

/** Binds a committed successor to the exact original managed identity/profile and positive aggregate revision. */
async function successor(result: Result, base: Observed, now: number): Promise<Observed | null> {
  if (result.status !== 200 || base.expires <= now || !base.details.editable || !base.etag || !await version(base.etag, base.details.id, false)) return null;
  const next = await observe(result, base.details.id, now);
  if (!next || !next.details.editable || next.details.clientId !== base.details.clientId || next.details.profile !== base.details.profile ||
      JSON.stringify(next.details.origins) !== JSON.stringify(base.details.origins) ||
      !next.etag || BigInt(next.etag.slice(66, -1)) !== BigInt(base.etag.slice(66, -1)) + 1n) return null;
  return next;
}
/** PUT changes exactly the submitted presentation/callback set and never changes credential dates, even when already expired. */
export async function edited(result: Result, base: Observed, input: EditInput, now: number): Promise<Observed | null> {
  const next = await successor(result, base, now);
  return next && next.details.enabled === base.details.enabled && next.details.displayName === input.displayName && sameCallbacks(next.details.redirectUris, input.redirectUris) &&
    sameCredential(next.details.credential, base.details.credential) ? next : null;
}
/** Requires either two absent public credentials or matching confidential issuance and expiry instants. */
function sameCredential(left: Details["credential"], right: Details["credential"]): boolean {
  if (!left || !right) return left === right;
  return utc(left.issuedAt) === utc(right.issuedAt) && utc(left.expiresAt) === utc(right.expiresAt);
}
/** Replacement accepts one new native secret and exact expiry while keeping all noncredential configuration unchanged. */
export async function replaced(result: Result, base: Observed, input: ReplacementInput, now: number): Promise<(Observed & { secret: string }) | null> {
  if (!base.details.credential || !input.confirmed || !object(result.value, ["application", "clientSecret"]) || typeof result.value.clientSecret !== "string" ||
      !/^[A-Za-z0-9+/]{43}=$/.test(result.value.clientSecret)) return null;
  const secret = result.value.clientSecret;
  if (btoa(atob(secret)) !== secret || atob(secret).length !== 32) return null;
  const next = await successor({ ...result, value: result.value.application }, base, now);
  return next && next.details.enabled === base.details.enabled && next.details.displayName === base.details.displayName && sameCallbacks(next.details.redirectUris, base.details.redirectUris) &&
    utc(next.details.credential!.expiresAt) === utc(input.credentialExpiresAt, true) && utc(input.credentialExpiresAt, true) > now &&
    utc(next.details.credential!.issuedAt) >= utc(base.details.credential!.issuedAt)
    ? { ...next, secret } : null;
}
/** Callback ordering is not authority; exact membership is preserved without normalization. */
function sameCallbacks(left: readonly string[], right: readonly string[]): boolean { return JSON.stringify([...left].sort()) === JSON.stringify([...right].sort()); }

/** Accepts only the requested opposite lifecycle state while preserving every non-lifecycle field and credential deadline. */
export async function lifecycleChanged(result: Result, base: Observed, enabled: boolean, now: number): Promise<Observed | null> {
  const next = await successor(result, base, now);
  return next && base.details.enabled === !enabled && next.details.enabled === enabled &&
    next.details.displayName === base.details.displayName && sameCallbacks(next.details.redirectUris, base.details.redirectUris) &&
    sameCredential(next.details.credential, base.details.credential) ? next : null;
}

/** Binds new disabled clone identity, reviewed callbacks and fresh credential expiry without accepting source identity or inherited lifecycle. */
export async function cloned(result: Result, base: Observed, input: CloneInput, now: number): Promise<(Observed & { secret: string }) | null> {
  if (result.status !== 201 || !input.confirmed || base.expires <= now || !base.details.editable || !base.details.credential ||
      !object(result.value, ["application", "clientSecret"]) || typeof result.value.clientSecret !== "string" ||
      !/^[A-Za-z0-9+/]{43}=$/.test(result.value.clientSecret)) return null;
  const secret = result.value.clientSecret;
  if (btoa(atob(secret)) !== secret || atob(secret).length !== 32) return null;
  const next = await observe({ ...result, value: result.value.application }, null, now);
  return next && next.details.editable && next.details.enabled === false && next.details.credential && next.details.profile === base.details.profile && next.details.id !== base.details.id &&
    await version(next.etag, next.details.id, true) && next.details.clientId === input.clientId && next.details.displayName === input.displayName &&
    sameCallbacks(next.details.redirectUris, base.details.redirectUris) && utc(next.details.credential!.expiresAt) === utc(input.credentialExpiresAt, true) &&
    utc(input.credentialExpiresAt, true) > now ? { ...next, secret } : null;
}

/** Requires an exact immutable job reference and whole-second retirement chronology for retained managed details. */
function retirementReference(value: unknown, credential: unknown, expires: number, now: number): boolean {
  if (!object(value, ["jobId", "retiredAt"]) || typeof value.jobId !== "string" ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value.jobId) ||
      value.jobId === "00000000-0000-0000-0000-000000000000" || (credential !== null && !object(credential, ["issuedAt", "expiresAt"]))) return false;
  const retired = wholeUtc(value.retiredAt);
  return (!credential || retired >= wholeUtc(credential.issuedAt)) && retired <= now && retired < expires;
}
