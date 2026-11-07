import { object } from "../Applications/Editor/observation";
import { text, utc } from "../Applications/Editor/input";
import { addressable, protocol, validate, draft, type Details, type Page, type Job, type Kind, type Input } from "./input";
export type Result = { status: number; value?: unknown; etag?: string | null; location?: string | null };
const audienceFields = ["id", "resource", "displayName", "owner", "enabled", "version", "referenced", "expiresAt"];
const scopeFields = ["id", "name", "displayName", "kind", "enabled", "consentText", "audienceId", "resource", "disclosure", "version", "referenced", "expiresAt"];
const jobFields = ["id", "kind", "targetId", "state", "failure", "createdAt", "observedAt", "expiresAt"];
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
/** Checks finite observation time before any protected catalogue projection enters UI state. */
export function expiry(value: unknown, now: number): number {
  const parsed = utc(value); return parsed > now && parsed <= now + 300000 ? parsed : NaN;
}
/** Requires exact public fields, policy combinations and the matching opaque response version. */
export function details(result: Result, kind: Kind, id: string | undefined, now: number, input?: Input): Details | null {
  const value = result.value;
  if (!object(value, kind === "scope" ? scopeFields : audienceFields) || !addressable(value.id) || id !== undefined && value.id !== id ||
      !text(value.displayName, 200) || typeof value.enabled !== "boolean" || typeof value.referenced !== "boolean" ||
      typeof value.version !== "string" || !/^"[0-9a-f]{64}"$/.test(value.version) || value.version !== result.etag || !Number.isFinite(expiry(value.expiresAt, now))) return null;
  if (kind === "scope" && (!protocol(value.name) || !["identity", "api"].includes(value.kind as string) ||
      !["none", "email"].includes(value.disclosure as string) || !text(value.consentText, 500) ||
      (value.kind === "api" ? !addressable(value.audienceId) || !protocol(value.resource, true) || value.disclosure !== "none" : value.audienceId !== null || value.resource !== null))) return null;
  if (kind === "audience" && (!protocol(value.resource, true) || !text(value.owner, 200))) return null;
  const output = value as unknown as Details, checked = validate(kind, draft(output));
  if (!checked.input || input && Object.entries(input).some(([key, expected]) => value[key] !== expected)) return null;
  return { ...output };
}
/** Checks one exact requested page with bounded unique identifiers and protocol names, never just a generated TypeScript assertion. */
export function page(result: Result, kind: Kind, search: string, number: number, now: number, size = 25): Page | null {
  const value = result.value;
  if (result.status !== 200 || !object(value, ["items", "page", "pageSize", "hasMore", "expiresAt"]) || value.page !== number || value.pageSize !== size ||
      typeof value.hasMore !== "boolean" || !Number.isFinite(expiry(value.expiresAt, now)) || !Array.isArray(value.items) ||
      value.items.length > size || value.hasMore && value.items.length !== size ||
      !value.items.every(item => object(item, ["id", "name", "displayName", "enabled"]) && addressable(item.id) &&
        protocol(item.name, kind === "audience") && item.name.startsWith(search) && text(item.displayName, 200) && typeof item.enabled === "boolean") ||
      new Set(value.items.map(item => item.id)).size !== value.items.length) return null;
  return value as unknown as Page;
}
/** Binds durable status to the requested kind/target/job and admits only coherent finite terminal outcomes. */
export function job(result: Result, kind: Kind, target: string, now: number, id?: string): Job | null {
  const value = result.value;
  if (!object(value, jobFields) || typeof value.id !== "string" || !guid.test(value.id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(value.id) ||
      id !== undefined && value.id !== id || value.kind !== kind || value.targetId !== target || !addressable(target) ||
      !Number.isFinite(expiry(value.expiresAt, now)) || !Number.isFinite(utc(value.createdAt)) || !(utc(value.createdAt) > 0 && utc(value.observedAt) >= utc(value.createdAt) && utc(value.observedAt) <= now) ||
      (value.state === "queued" || value.state === "succeeded" ? value.failure !== "none" : value.state !== "rejected" ||
        !["stale", "referenced", "authority", "expired", "missing"].includes(value.failure as string))) return null;
  if (result.status === 202 && result.location !== `/bff/api/v1/${kind === "scope" ? "scopes" : "audiences"}/deletions/${value.id}`) return null;
  return value as unknown as Job;
}
