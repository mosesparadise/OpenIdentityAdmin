import type { components } from "../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";

export type SessionDto = components["schemas"]["BrowserSessionStatus"];
export type SessionState =
  | { kind: "loading" | "anonymous" | "elapsed" | "unavailable" }
  | { kind: "authenticated"; subject: string; expires: number };
const maximumSubjectLength = 512;
const statusFields = new Set(["authenticated", "subject", "expiresAt"]);
const utcTimestamp = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$/;

/** Validates wire data before it can become a local observation; generated types are not runtime authority. */
export function observe(value: unknown, now: number): SessionState {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("Invalid session response");
  const dto = value as Record<string, unknown>;
  if (Object.keys(dto).some(key => !statusFields.has(key))) throw new Error("Unexpected session response");
  if (dto.authenticated === false && dto.subject == null && dto.expiresAt == null) return { kind: "anonymous" };
  if (dto.authenticated !== true || typeof dto.subject !== "string" || !dto.subject.trim() ||
      dto.subject.length > maximumSubjectLength || typeof dto.expiresAt !== "string" || !utcTimestamp.test(dto.expiresAt))
    throw new Error("Incomplete session response");
  const expires = Date.parse(dto.expiresAt);
  if (!Number.isFinite(expires) || new Date(expires).toISOString().slice(0, 19) !== dto.expiresAt.slice(0, 19)) throw new Error("Invalid session expiry");
  return expires <= now ? { kind: "elapsed" } : { kind: "authenticated", subject: dto.subject, expires };
}
