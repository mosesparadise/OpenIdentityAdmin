import createClient from "openapi-fetch";
import type { paths } from "../../../IdentityPlatform.Admin.Contracts/generated/bff-v1.browser";

const maximumResponseBytes = 4096;
const readDeadlineMs = 10_000;
/** Bounds native response bytes before the generated client parses JSON; redirects and unexpected media never become identity. */
async function boundedFetch(request: Request): Promise<Response> {
  const response = await fetch(request);
  if (!response.ok || response.redirected || response.status !== 200 ||
      response.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json" || !response.body)
    throw new Error("Session status unavailable");
  const reader = response.body.getReader();
  const parts: Uint8Array[] = [];
  let size = 0;
  try {
    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > maximumResponseBytes) throw new Error("Session response exceeds its bound");
      parts.push(value);
    }
  } finally { await reader.cancel(); reader.releaseLock(); }
  const bytes = new Uint8Array(size);
  let offset = 0;
  for (const part of parts) { bytes.set(part, offset); offset += part.byteLength; }
  return new Response(bytes, { status: response.status, headers: response.headers });
}

/** Uses only the generated same-origin read operation, with no token, automatic login, retry or browser persistence. */
export async function readSession(signal: AbortSignal): Promise<unknown> {
  const client = createClient<paths>({ baseUrl: window.location.origin, fetch: boundedFetch });
  const { data, response } = await client.GET("/bff/session", {
    credentials: "same-origin", cache: "no-store", redirect: "error",
    signal: AbortSignal.any([signal, AbortSignal.timeout(readDeadlineMs)])
  });
  if (!response.ok || data === undefined) throw new Error("Session status unavailable");
  return data;
}
