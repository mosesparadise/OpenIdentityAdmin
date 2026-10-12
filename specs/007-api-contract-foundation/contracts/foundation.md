# Foundation contract

Production hosts remain health/storage foundations. `/docs/scalar`, `/docs/swagger`, their assets and `/openapi/v1.json` or `/openapi/bff-v1.json` return404 in all environments. There is no enable-docs flag and no synthetic management operation in production. Health/readiness must succeed independently before absence can pass.

| Surface | Versioned prefix | Document | Security boundary |
| --- | --- | --- | --- |
| Admin API | `/api/v{version:apiVersion}` | `v1` | Bearer access token, independent current permission checks deferred to M02-D |
| BFF | `/bff/api/v{version:apiVersion}` | `bff-v1` | Same-origin cookie and unsafe-method antiforgery, actual enforcement deferred to M02 |

Both support explicit v1; BFF maps explicitly to downstream v1. Native URL versioning rejects missing/malformed/unsupported versions without default fallback. Record precise observed responses in verification.md before acceptance. No invented deprecation/sunset date. Protocol/session/callback/health routes remain outside this surface.

Non-shipped representative contracts contain a bounded page, nullable/composed schema, enum, UTC date, typed errors and conditional edit operation. Page default25/max100; editable GET publishes ETag, write requires If-Match and browser antiforgery; permission metadata differs by operation. Success/error responses cover400/401/403/404/409/412/428 with RFC9457 shapes. These are tooling fixtures, not real audited/concurrent resource operations.

Fixtures expose both named documents to each read-only viewer; operation IDs and document provenance are explicit. BFF-specific metadata is authored in its own host, not a blind path rename of a bearer contract. Client tests send BFF paths/cookies and antiforgery data only; any server-held OAuth token scheme/input/storage is a failing regression.

Compatibility verifies operation/schema/reference stability and separately verifies permissions, security alternatives, preconditions, cookie/antiforgery semantics and response headers. Diff unsupported semantics conservatively and require explicit review; do not silently approve a change the checker cannot interpret. Breaking public changes require a new major, not baseline regeneration alone.

## Deferred asset authorization hazard

Scalar.AspNetCore2.17.14 marks embedded asset endpoints AllowAnonymous. A future RequireAuthorization on its returned route group alone therefore cannot satisfy page/asset/JSON policy parity. M02/M04 activation must use a reviewed outer BFF path gate or protected asset delivery and prove direct asset denials. Current hosts map none of these routes. Swagger's string interceptor/plugin evaluation must not drive an unsafe-eval CSP exception; use a local bootstrap with directly constructed configuration.
