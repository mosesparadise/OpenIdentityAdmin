import type { components } from "../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";

export type Application = components["schemas"]["ApplicationSummary"];
export type Query = { search: string; page: number; pageSize: number };
export type ApplicationState =
  | { kind: "loading" | "expired" | "forbidden" | "unavailable" }
  | { kind: "ready"; items: Application[]; page: number; pageSize: number; hasNextPage: boolean; expires: number };
export type Result = { status: number; value?: unknown };
const fields = ["items", "page", "pageSize", "hasNextPage", "expiresAt"];
const itemFields = ["id", "clientId", "displayName", "clientType"];
const timestamp = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$/;

/** Checks the finite public projection before retaining any application data in memory. */
export function observe(result: Result, query: Query, now: number): ApplicationState {
  if (result.status === 401) return { kind: "expired" };
  if (result.status === 403) return { kind: "forbidden" };
  if (result.status !== 200 || !object(result.value, fields)) return { kind: "unavailable" };
  const dto = result.value;
  if (dto.page !== query.page || dto.pageSize !== query.pageSize || typeof dto.hasNextPage !== "boolean" ||
      !Array.isArray(dto.items) || dto.items.length > query.pageSize || (dto.hasNextPage && dto.items.length !== query.pageSize) ||
      typeof dto.expiresAt !== "string" || !timestamp.test(dto.expiresAt)) return { kind: "unavailable" };
  const expires = Date.parse(dto.expiresAt);
  if (!Number.isFinite(expires) || new Date(expires).toISOString().slice(0, 19) !== dto.expiresAt.slice(0, 19) ||
      expires > now + 300_000) return { kind: "unavailable" };
  if (expires <= now) return { kind: "expired" };
  const items: Application[] = [];
  const ids = new Set<string>();
  for (const item of dto.items) {
    if (!object(item, itemFields) || !text(item.id, 400) || !text(item.clientId, 100) ||
        !optionalText(item.displayName, 200) || !optionalText(item.clientType, 50) ||
        !item.clientId.startsWith(query.search) || ids.has(item.id)) return { kind: "unavailable" };
    ids.add(item.id);
    items.push({ id: item.id, clientId: item.clientId, displayName: item.displayName, clientType: item.clientType });
  }
  return { kind: "ready", items, page: query.page, pageSize: query.pageSize, hasNextPage: dto.hasNextPage, expires };
}

/** Requires the exact JSON object members, excluding arrays and missing fields. */
function object(value: unknown, keys: string[]): value is Record<string, unknown> {
  return !!value && typeof value === "object" && !Array.isArray(value) &&
    Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key));
}
/** Preserves literal identifiers without trimming or interpreting their contents. */
function text(value: unknown, maximum: number): value is string { return typeof value === "string" && value.length > 0 && value.length <= maximum; }
/** Accepts nullable display metadata without converting missing fields to invented labels. */
function optionalText(value: unknown, maximum: number): value is string | null { return value === null || typeof value === "string" && value.length <= maximum; }
