# Sample interfaces and controls

Development/local-demo only. No public management or configuration endpoint.

| Boundary | Contract | Required evidence |
| --- | --- | --- |
| Dashboard configure-sample | Authenticated Aspire command; exact resource/run/immutable container; secret fields→bounded stdin; fixed private no-overwrite file | C01: bad context/arguments/foreign/replaced/stopped target, duplicate and uncertain config, clearing inputs |
| GET / | Waiting/setup directions or finite session summary; no-store encoded HTML | C02: no secret/token echo; light/dark/keyboard/narrow viewport |
| POST /login | Exact Origin + native antiforgery; fixed challenge/scopes; fresh password/MFA | C03: CSRF, wrong origin, changed targets, native state/correlation/S256, replay and capacity |
| GET /callback | Native query callback; state consumed once; match browser attempt; fixed return / | C04: invalid/state/nonce/signature/issuer/expired/concurrent/late-after-logout; no open redirect |
| POST /profile | Session + Origin/antiforgery; fixed UserInfo GET bearer header; strict bounded projection/sub match | C05: policy/revocation denial, absent/false/changed email, oversized/malformed/mismatched sub, logout race |
| POST /orders | Session + Origin/antiforgery; fixed sample API bearer header | C06: success/401/403/unavailable, expiry/logout race; no browser bearer forwarding |
| POST /logout | Origin/antiforgery; terminal current session+pending attempt removal | C07: replay/back navigation, independent second browser, concurrent renewal/callback |
| GET /orders (API) | Native bearer-only RS256 signature/type/issuer/audience/lifetime + exact orders.read scope | C08: no token/ID token/wrong issuer/audience/algorithm/scope/expired/malformed/query or form bearer/duplicate header/mixed transport; no profile/PII |
| GET /health | Finite readiness only, no configuration or secrets | C09: wait/config/restart/partial startup/disposal on both provider profiles |

C10: native store tests prove detached copies, unique reference IDs, capacity, bounded
payload/properties, cancellation, CAS and real manager concurrent redemption.
C11: discovery/JWKS/credential transport validates fixed destinations and rejects
redirects, unexpected metadata, oversized output and retry; signing-key refresh stays
native and fails closed on unavailable trust.
C12: native cookie/server ticket and response publication tests prove expiry, no
sliding extension, terminal logout and no revival from renewal.
C13: all previously discovered cases and module floors remain; new client/API modules
and original full provider/browser reports are required. AI reviews are not human acceptance.

Offline API revocation is bounded by token expiry with zero accepted clock skew.
Current UserInfo may deny immediately after a policy/authority change. Local sample
logout neither revokes a grant nor changes another application's session.

Publication ordering and retained-state total bounds are normative in data-model.md.
