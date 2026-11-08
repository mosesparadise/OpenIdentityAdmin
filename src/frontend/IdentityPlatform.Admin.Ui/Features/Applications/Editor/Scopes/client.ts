import createClient from "openapi-fetch";
import type { paths } from "../../../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { bounded, editorClient } from "../client";
import { addressable } from "../input";
import { catalogueClient } from "../../../Catalogue/client";
import type { Result } from "../observation";
/** Uses the generated assignment subresource with fixed origin, native application CSRF and one send per explicit submission. */
export function scopeClient() {
  const client = createClient<paths>({ baseUrl: window.location.origin, fetch: bounded });
  const options = (signal: AbortSignal) => ({ signal: AbortSignal.any([signal, AbortSignal.timeout(10000)]), credentials: "same-origin" as const, cache: "no-store" as const, redirect: "error" as const });
  const target = (id: string) => {
    if (!addressable(id)) throw new Error("Unsupported application identity");
    return `${window.location.origin}/bff/api/v1/applications/${encodeURIComponent(id).replace(/[!'()*]/g, c => `%${c.charCodeAt(0).toString(16).toUpperCase()}`)}/scopes`;
  };
  return {
    token: editorClient().token,
    list: (search: string, page: number, signal: AbortSignal) => catalogueClient().list("scope", search, page, signal),
    async read(id: string, signal: AbortSignal): Promise<Result> {
      const path = target(id);
      const { data, response } = await client.GET("/bff/api/v1/applications/{id}/scopes", { ...options(signal), params: { path: { id } }, pathSerializer: () => path });
      return { status: response.status, value: data, etag: response.headers.get("etag") };
    },
    async save(id: string, version: string, scopes: string[], proof: string, signal: AbortSignal): Promise<Result> {
      const path = target(id);
      const { data, response } = await client.PUT("/bff/api/v1/applications/{id}/scopes", { ...options(signal),
        params: { path: { id }, header: { "If-Match": version, "X-CSRF-TOKEN": proof } }, pathSerializer: () => path, body: { scopes } });
      return { status: response.status, value: data, etag: response.headers.get("etag") };
    }
  };
}
export type ScopeClient = ReturnType<typeof scopeClient>;
