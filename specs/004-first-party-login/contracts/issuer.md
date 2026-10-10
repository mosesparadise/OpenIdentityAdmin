# Pilot interfaces

Enabled only with `Pilot:Enabled=true` in Development and complete existing issuer protection. Default disabled leaves health routes only. Explicit `--initialize-pilot` command consumes externally supplied Pilot username/password/client secret/HTTPS redirect; no secrets in command-line examples or checked-in configuration. Exact configuration keys and invocation are finalized in the implementation runbook before acceptance.

| Interface | Contract |
| --- | --- |
| GET /.well-known/openid-configuration | Canonical configured HTTPS issuer; only implemented code/query/S256 and registered scopes/client authentication. No public administrative docs here. |
| GET /connect/jwks | Public signing keys only; no encryption/wrapping private material. |
| GET or POST /connect/authorize | Native OpenIddict parsing/validation/caching; registered callback only. Named server scheme handles SignIn/Forbid; application uses its separate cookie only for eligibility. |
| POST /connect/token | Form-encoded authorization_code, client_secret_basic authenticated confidential client and mandatory S256 verifier; missing/wrong/body-only/conflicting credentials denied. Native errors/status/cache headers; no retry. |
| GET or POST /account/login | Only an opaque persisted interaction ID; protected form posts username/password and antiforgery. No OAuth parameter round-trip/free-form returnUrl. Generic failures; password never echoed. |
| GET /account/complete | If needed for restrictive form-action redirect behavior, only a bound same-origin interaction completion page; trusted server-held continuation, never caller-supplied link. |
| Health | Existing /alive and /health behavior retained. |

No UserInfo/logout/refresh/PAR/device/machine/management endpoint is advertised. Protocol routes remain unversioned. Consent/select_account are explicitly unsupported. Strict max_age nonnegative integer syntax; prompt=none cannot display UI, prompt=login/max_age=0 need fresh completion of this request. Known OAuth errors complete through OpenIddict; infrastructure faults use sanitized failure and never tokens.

Cookie contract: __Host-prefixed, Path=/, Secure, HttpOnly, no Domain, SameSite=Lax; application ticket nonpersistent15min. Correlation cookies expire5min and are individually named; maximum eight live browser interactions. Form antiforgery has its own protected host cookie. Tokens stay in the test RP server; browser receives only code/state through the registered OIDC callback as the protocol requires.

UI contract: slate/indigo semantic tokens, system/light/dark preference only, no remote fonts/scripts, restrictive CSP/no-store/no-referrer, explicit labels/summary/focus/paste support and 320px reflow. Same-origin form completion is tested before trusting cross-origin redirect behavior.

## Native claims clarification

The approved protocol-security document's exclusion of “internal authorization bookkeeping” is interpreted alongside its native entry-validation requirement: application-owned session/security bookkeeping remains private. OpenIddict 7.7.1's narrowly enumerated lifecycle references remain library-owned metadata. We do not remove claims needed by native entry validation or assert that every `oi_` claim is absent.

| Public token | Allowed native lifecycle claims |
| --- | --- |
| Access | `oi_au_id` (authorization entry), `oi_tkn_id` (token entry), `oi_prst` (presenter/client ID) |
| ID | `oi_au_id`, `oi_tkn_id` |

Entry identifiers must be nonempty and bounded; presenter must equal the pilot client. Separate exact claim allowlists reject all other `oi_` names and all application session, stamp, credential, authenticator and role claims. This clarification was independently source-reviewed against native access/ID principal preparation and token-entry creation; it does not expand application claims or alter lifecycle handling.

S256 challenges require canonical 32-byte base64url encoding. Verifiers require 43–128 ASCII unreserved characters. The Minimal API guards run after native validation/request caching and before application login/issuance; native OpenIddict still performs proof hashing, comparison and code redemption.
