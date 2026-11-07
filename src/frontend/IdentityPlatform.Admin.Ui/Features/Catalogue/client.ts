import createClient from "openapi-fetch";
import type { paths } from "../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { bounded } from "../Applications/Editor/client";
import { addressable, type Kind, type Input, type AudienceInput, type ScopeInput } from "./input";
import type { Result } from "./observation";
/** Uses only generated fixed BFF operations; browser state never receives or forwards an OAuth token. */
export function catalogueClient() {
  const client = createClient<paths>({ baseUrl: window.location.origin, fetch: bounded });
  const options = (signal: AbortSignal) => ({ signal: AbortSignal.any([signal, AbortSignal.timeout(10000)]), credentials: "same-origin" as const, cache: "no-store" as const, redirect: "error" as const });
  const target = (kind: Kind, id: string) => {
    if (!addressable(id)) throw new Error("Unsupported catalogue identity");
    return `${window.location.origin}/bff/api/v1/${kind === "scope" ? "scopes" : "audiences"}/${encodeURIComponent(id).replace(/[!'()*]/g, c => `%${c.charCodeAt(0).toString(16).toUpperCase()}`)}`;
  };
  const result = (data: unknown, response: Response): Result => ({ status: response.status, value: data, etag: response.headers.get("etag"), location: response.headers.get("location") });
  return {
    async list(kind: Kind, search: string, page: number, signal: AbortSignal, pageSize = 25): Promise<Result> {
      const route = kind === "scope" ? "/bff/api/v1/scopes" : "/bff/api/v1/audiences";
      const { data, response } = await client.GET(route, { ...options(signal), params: { query: { search, page, pageSize } } }); return result(data, response);
    },
    async details(kind: Kind, id: string, signal: AbortSignal): Promise<Result> {
      const route = kind === "scope" ? "/bff/api/v1/scopes/{id}" : "/bff/api/v1/audiences/{id}";
      const path = target(kind, id);
      const { data, response } = await client.GET(route, { ...options(signal), params: { path: { id } }, pathSerializer: () => path }); return result(data, response);
    },
    async token(kind: Kind, signal: AbortSignal): Promise<Result> {
      const route = kind === "scope" ? "/bff/api/v1/scopes/antiforgery" : "/bff/api/v1/audiences/antiforgery";
      const { data, response } = await client.GET(route, options(signal)); return result(data, response);
    },
    async save(kind: Kind, input: Input, proof: string, signal: AbortSignal, id?: string, version?: string): Promise<Result> {
      if (id) {
        const path = target(kind, id), route = kind === "scope" ? "/bff/api/v1/scopes/{id}" : "/bff/api/v1/audiences/{id}";
        const { data, response } = await client.PUT(route, { ...options(signal), params: { path: { id }, header: { "If-Match": version!, "X-CSRF-TOKEN": proof } }, pathSerializer: () => path, body: input }); return result(data, response);
      }
      if (kind === "scope") {
        const { data, response } = await client.POST("/bff/api/v1/scopes", { ...options(signal), params: { header: { "X-CSRF-TOKEN": proof } }, body: input as ScopeInput }); return result(data, response);
      }
      const { data, response } = await client.POST("/bff/api/v1/audiences", { ...options(signal), params: { header: { "X-CSRF-TOKEN": proof } }, body: input as AudienceInput }); return result(data, response);
    },
    async remove(kind: Kind, id: string, version: string, key: string, proof: string, signal: AbortSignal): Promise<Result> {
      const path = target(kind, id), route = kind === "scope" ? "/bff/api/v1/scopes/{id}" : "/bff/api/v1/audiences/{id}";
      const { data, response } = await client.DELETE(route, { ...options(signal), params: { path: { id }, header: { "If-Match": version, "X-CSRF-TOKEN": proof } }, pathSerializer: () => path, body: { confirmed: true, requestKey: key } }); return result(data, response);
    },
    async status(kind: Kind, jobId: string, signal: AbortSignal): Promise<Result> {
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(jobId)) throw new Error("Unsupported deletion identity");
      const route = kind === "scope" ? "/bff/api/v1/scopes/deletions/{jobId}" : "/bff/api/v1/audiences/deletions/{jobId}";
      const { data, response } = await client.GET(route, { ...options(signal), params: { path: { jobId } } }); return result(data, response);
    }
  };
}
export type CatalogueClient = ReturnType<typeof catalogueClient>;
