import { object, version, type Result } from "../observation";
import { utc } from "../input";
import { protocol } from "../../../Catalogue/input";
export type Assignment = { scopes: string[]; etag: string; expires: number };
/** Accepts only exact sorted names, a resource-bound version and bounded original observation time; extra output fields fail closed. */
export async function assignment(result: Result, id: string, now: number, prior?: Assignment, expected?: string[]): Promise<Assignment | null> {
  const value = result.value;
  if (result.status !== 200 || !object(value, ["applicationId", "scopes", "expiresAt"]) || value.applicationId !== id ||
      !Array.isArray(value.scopes) || value.scopes.length > 32 || !value.scopes.every(name => protocol(name)) ||
      new Set(value.scopes).size !== value.scopes.length || JSON.stringify(value.scopes) !== JSON.stringify([...value.scopes].sort()) ||
      !await version(result.etag, id, false)) return null;
  const expires = utc(value.expiresAt);
  if (!(expires > now && expires <= now + 300000) || prior && (prior.expires <= now ||
      BigInt(result.etag!.slice(66, -1)) !== BigInt(prior.etag.slice(66, -1)) + 1n || JSON.stringify(value.scopes) !== JSON.stringify(expected))) return null;
  return { scopes: [...value.scopes] as string[], etag: result.etag!, expires };
}
