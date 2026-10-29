import createClient from "openapi-fetch";
import type { paths } from "../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";
import type { Query, Result } from "./observation";

const maximumResponseBytes = 512 * 1024;
/** Bounds bytes before generated JSON parsing; error bodies never enter UI state. */
async function boundedFetch(request: Request): Promise<Response> {
  const response = await fetch(request);
  if (response.redirected) throw new Error("Applications unavailable");
  if (response.status !== 200) {
    await response.body?.cancel();
    return new Response(null, { status: response.status });
  }
  if (response.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json" || !response.body)
    throw new Error("Applications unavailable");
  const reader = response.body.getReader();
  const parts: Uint8Array[] = [];
  let size = 0;
  try {
    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > maximumResponseBytes) throw new Error("Applications exceed response bound");
      parts.push(value);
    }
  } finally { await reader.cancel(); reader.releaseLock(); }
  const bytes = new Uint8Array(size);
  let offset = 0;
  for (const part of parts) { bytes.set(part, offset); offset += part.byteLength; }
  return new Response(bytes, { status: 200, headers: response.headers });
}

/** Reads the generated same-origin BFF operation with cookies and no browser-held OAuth credential. */
export async function readApplications(query: Query, signal: AbortSignal): Promise<Result> {
  const client = createClient<paths>({ baseUrl: window.location.origin, fetch: boundedFetch });
  const { data, response } = await client.GET("/bff/api/v1/applications", {
    params: { query }, credentials: "same-origin", cache: "no-store", redirect: "error",
    signal: AbortSignal.any([signal, AbortSignal.timeout(10_000)])
  });
  return { status: response.status, value: data };
}
