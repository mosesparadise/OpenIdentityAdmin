import createClient from "openapi-fetch";
import type { paths } from "../../../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { bounded, editorClient } from "../client";
import { addressable } from "../input";
import { key, type GrantInput, type GrantResult } from "./observation";
const collection = "/bff/api/v1/applications/{id}/grant-revocations";
/** Sends one fixed same-origin operation using generated types, native CSRF and an explicit caller-owned key; never retries. */
export function grantClient() {
  const client = createClient<paths>({ baseUrl: window.location.origin, fetch: bounded });
  const options = (signal: AbortSignal) => ({ credentials: "same-origin" as const, cache: "no-store" as const, redirect: "error" as const,
    signal: AbortSignal.any([signal, AbortSignal.timeout(10000)]) });
  const target = (id: string, jobId?: string) => {
    if (!addressable(id) || jobId !== undefined && !key(jobId)) throw new Error("Unsupported revocation identity");
    return `${window.location.origin}/bff/api/v1/applications/${encodeURIComponent(id).replace(/[!'()*]/g, character => `%${character.charCodeAt(0).toString(16).toUpperCase()}`)}/grant-revocations${jobId ? `/${jobId}/continue` : ""}`;
  };
  return {
    token: editorClient().token,
    async recent(id: string, signal: AbortSignal): Promise<GrantResult> {
      const path = target(id);
      const { data, response } = await client.GET(collection, { ...options(signal), params: { path: { id } }, pathSerializer: () => path });
      return { status: response.status, value: data };
    },
    async submit(id: string, version: string, requestKey: string, input: GrantInput, csrf: string, signal: AbortSignal, jobId?: string): Promise<GrantResult> {
      if (!key(requestKey)) throw new Error("Unsupported revocation key");
      const path = target(id, jobId);
      const headers = { "If-Match": version, "Idempotency-Key": requestKey, "X-CSRF-TOKEN": csrf };
      const { data, response } = jobId
        ? await client.POST("/bff/api/v1/applications/{id}/grant-revocations/{jobId}/continue", { ...options(signal), params: { path: { id, jobId }, header: headers }, pathSerializer: () => path, body: input })
        : await client.POST(collection, { ...options(signal), params: { path: { id }, header: headers }, pathSerializer: () => path, body: input });
      return { status: response.status, value: data, etag: response.headers.get("etag"), location: response.headers.get("location") };
    }
  };
}
export type GrantClient = ReturnType<typeof grantClient>;
