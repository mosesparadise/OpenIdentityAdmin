import type { components } from "../../../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { object, version } from "../observation";
import { utc } from "../input";
export type Job = components["schemas"]["GrantJobView"];
export type GrantInput = components["schemas"]["GrantRevocation"];
export type GrantResult = { status: number; value?: unknown; etag?: string | null; location?: string | null };
const fields = ["id", "applicationId", "version", "state", "failure", "authorizationCount", "tokenCount", "createdAt", "updatedAt", "expiresAt"];
const states = ["queued", "processing", "stopped", "succeeded"];
const failures = ["none", "authority", "target", "storage", "clock"];
const maximumCount = 9223372036854775807n;
/** Accepts only canonical, nonempty request/job UUIDs, never alternate Guid parser forms. */
export function key(value: unknown): value is string {
  return typeof value === "string" && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value) && value !== "00000000-0000-0000-0000-000000000000";
}
/** Keeps progress exact beyond JavaScript's safe-integer range and rejects aliases or overflow. */
export function count(value: unknown): value is string {
  return typeof value === "string" && /^(0|[1-9][0-9]{0,18})$/.test(value) && BigInt(value) <= maximumCount;
}
/** Rejects partial, cross-target, inconsistent or expired output before it can become visible. */
export async function job(value: unknown, application: string, now: number, id?: string): Promise<Job | null> {
  if (!object(value, fields) || !key(value.id) || value.applicationId !== application || id !== undefined && value.id !== id ||
      !states.includes(value.state as string) || !failures.includes(value.failure as string) ||
      (value.state === "stopped") !== (value.failure !== "none") || !count(value.authorizationCount) || !count(value.tokenCount) ||
      typeof value.version !== "string" || !await version(value.version, value.id, false)) return null;
  const created = utc(value.createdAt), updated = utc(value.updatedAt), expires = utc(value.expiresAt);
  if (!(created >= 0 && updated >= created && created % 1000 === 0 && updated % 1000 === 0 && expires > now && expires <= now + 300000)) return null;
  return structuredClone(value) as Job;
}
/** Checks a bounded recent page without trusting its static generated type or extending the original display deadline. */
export async function page(result: GrantResult, application: string, now: number): Promise<Job[] | null> {
  const value = result.value;
  if (result.status !== 200 || !object(value, ["items", "expiresAt"]) || !Array.isArray(value.items) || value.items.length > 10 ||
      !(utc(value.expiresAt) > now && utc(value.expiresAt) <= now + 300000)) return null;
  const jobs = await Promise.all(value.items.map(item => job(item, application, now)));
  if (jobs.some(item => !item || item.expiresAt !== value.expiresAt) || new Set(jobs.map(item => item!.id)).size !== jobs.length) return null;
  return jobs as Job[];
}
/** Binds a committed response to the exact explicit request and code-owned status location. */
export async function accepted(result: GrantResult, application: string, requestKey: string, now: number, id?: string): Promise<Job | null> {
  if (result.status !== 202 || !object(result.value, ["job", "requestKey"]) || result.value.requestKey !== requestKey) return null;
  const value = await job(result.value.job, application, now, id);
  return value && result.etag === value.version && result.location === `/bff/api/v1/applications/${encodeURIComponent(application).replace(/[!'()*]/g, character => `%${character.charCodeAt(0).toString(16).toUpperCase()}`)}/grant-revocations/${value.id}` ? value : null;
}
