import { object, version, type Observed, type Result } from "./observation";
import { job } from "./Grants/observation";
import { utc } from "./input";
/** Requires the exact original intent, one terminal successor and the linked current job before projecting retained details. */
export async function retired(result: Result, base: Observed, requestKey: string, now: number): Promise<Observed | null> {
  const value = result.value;
  if (result.status !== 202 || base.expires <= now || !base.details.editable || base.details.retired || !base.etag ||
      !object(value, ["applicationId", "applicationVersion", "retiredAt", "requestKey", "job"]) ||
      value.applicationId !== base.details.id || value.requestKey !== requestKey || typeof value.applicationVersion !== "string" ||
      !await version(value.applicationVersion, base.details.id, false) || !await version(base.etag, base.details.id, false) ||
      BigInt(value.applicationVersion.slice(66, -1)) !== BigInt(base.etag.slice(66, -1)) + 1n) return null;
  const linked = await job(value.job, base.details.id, now);
  const at = utc(value.retiredAt);
  const target = encodeURIComponent(base.details.id).replace(/[!'()*]/g, character => `%${character.charCodeAt(0).toString(16).toUpperCase()}`);
  if (!linked || result.etag !== linked.version || result.location !== `/bff/api/v1/applications/${target}/grant-revocations/${linked.id}` ||
      at !== utc(linked.createdAt) || at > now || (base.details.credential && at < utc(base.details.credential.issuedAt))) return null;
  const expires = Math.min(base.expires, utc(linked.expiresAt));
  return { details: { ...structuredClone(base.details), enabled: false, editable: false, retired: true,
    retirement: { jobId: linked.id, retiredAt: value.retiredAt as string }, expiresAt: new Date(expires).toISOString() }, expires, etag: value.applicationVersion };
}
