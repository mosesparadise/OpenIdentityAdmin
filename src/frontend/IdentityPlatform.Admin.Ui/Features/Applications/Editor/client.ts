import createClient from "openapi-fetch";
import type { paths } from "../../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import { addressable, type CreateInput } from "./input";
import type { Result } from "./observation";
const collection = "/bff/api/v1/applications";
/** Bounds successful JSON before parsing and discards all remote error bodies, cookies and exception text. */
async function bounded(request: Request): Promise<Response> {
  const response = await fetch(request);
  if (response.redirected) { await response.body?.cancel(); throw new Error("Application response rejected"); }
  if (response.status !== 200 && response.status !== 201) { await response.body?.cancel(); return new Response(null, { status: response.status }); }
  if (response.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json" ||
      !response.headers.get("cache-control")?.split(",").some(value => value.trim().toLowerCase() === "no-store") ||
      response.headers.get("x-content-type-options") !== "nosniff" || !response.body) {
    await response.body?.cancel(); throw new Error("Application response rejected");
  }
  const reader = response.body.getReader(); const parts: Uint8Array[] = []; let size = 0;
  try { for (;;) { const { value, done } = await reader.read(); if (done) break;
    size += value.byteLength; if (size > 524288) throw new Error("Application response too large"); parts.push(value);
  } } finally { await reader.cancel(); reader.releaseLock(); }
  const bytes = new Uint8Array(size); let offset = 0;
  for (const part of parts) { bytes.set(part, offset); offset += part.byteLength; }
  return new Response(bytes, { status: response.status, headers: response.headers });
}
/** Uses generated operations with fixed origin, cookies, bounded cancellation and no redirect, bearer forwarding or retry. */
export function editorClient() {
  const client = createClient<paths>({ baseUrl: window.location.origin, fetch: bounded });
  const options = (signal: AbortSignal) => ({ credentials: "same-origin" as const, cache: "no-store" as const, redirect: "error" as const,
    signal: AbortSignal.any([signal, AbortSignal.timeout(10000)]) });
  return {
    async token(signal: AbortSignal): Promise<Result> {
      const { data, response } = await client.GET("/bff/api/v1/applications/antiforgery", options(signal));
      return { status: response.status, value: data };
    },
    async details(id: string, signal: AbortSignal): Promise<Result> {
      if (!addressable(id)) throw new Error("Unsupported application identity");
      const encoded = encodeURIComponent(id).replace(/[!'()*]/g, character => `%${character.charCodeAt(0).toString(16).toUpperCase()}`);
      const { data, response } = await client.GET("/bff/api/v1/applications/{id}", { ...options(signal), params: { path: { id } }, pathSerializer: () => `${window.location.origin}${collection}/${encoded}` });
      return { status: response.status, value: data, etag: response.headers.get("etag") };
    },
    async create(input: CreateInput, token: string, signal: AbortSignal): Promise<Result> {
      const { data, response } = await client.POST(collection, { ...options(signal), params: { header: { "X-CSRF-TOKEN": token } }, body: input });
      return { status: response.status, value: data, etag: response.headers.get("etag") };
    }
  };
}
export type EditorClient = ReturnType<typeof editorClient>;
