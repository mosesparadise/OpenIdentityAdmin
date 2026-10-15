# B2 administrator protocol contract

Planned contract; no runtime support is claimed by this document. Applies only to the fixed administrator profile; existing pilot behavior remains separate.

## Fixed profile and endpoint ownership

| Item | Contract |
| --- | --- |
| Issuer | One configured canonical HTTPS issuer, ordinal exact binding; no caller-selected issuer/discovery URLs |
| Client / scope / resource | `identity-admin-bff`; exactly `openid identity.admin`; single resource `identity-admin-api` |
| Client type / consent | Confidential web, fixed first-party implicit consent for only that exact scope set |
| Callback | One exact configured HTTPS URI, no wildcard/fragment/userinfo; reserved for B3, implemented only in test-owned transport during B2 |
| Request | code, query, S256; explicit `prompt=none`; native-generated state/nonce/verifier; reject extra scopes/resource substitution and interactive prompt modes |
| PAR | POST form `/connect/par`, native-owned handling; per-client requirement/permission; authenticated request only |
| Authorization | GET/POST `/connect/authorize` after native validation; administrator request must resolve to its native PAR reference/client binding |
| Token | POST form `/connect/token`; code only; native client/verifier/code/redirect/presenter validation plus fresh administrator policy |
| Discovery/JWKS | Native canonical issuer discovery and configured key endpoint; advertise only enabled profiles/capabilities |
| Excluded grants/routes | No refresh/offline_access, end-session, UserInfo, client credentials, device, generic consent, management API or product BFF callback in B2 |

Global native support may include Basic for the unchanged pilot and private_key_jwt for B2, but administrator policy accepts only private_key_jwt and the pilot retains its own method contract. A global supported-method list is not per-client authorization.

## Backend assertion

The native client creates a fresh assertion for each intentional PAR or token request. Native validation owns signature and cryptography. Product checks after native validation require:

- `typ=client-authentication+jwt` (native equivalent `application/client-authentication+jwt` accepted); RS256 only, approved RSA public key of at least 3072 bits with fixed unique kid. No shared-secret, unsigned, symmetric, jku/x5u or embedded attacker-selected key fallback.
- Exact `iss=sub=identity-admin-bff`; exactly one `aud`, the canonical issuer string. A token/PAR endpoint audience is denied. Form client identity, authenticated application and assertion identity must agree ordinally.
- Required scalar integer `iat` and `exp`, optional integer `nbf`; `0 < exp-iat <= 60` seconds, `iat <= effectiveNow+30`, `exp > effectiveNow`; if nbf is present, it must be no later than effectiveNow and lie between iat and exp. Reject malformed/duplicate/reserved-claim ambiguity. Do not extend exp by library skew.
- Required nonempty ASCII jti of 16–128 characters (`A-Z`, `a-z`, digits, hyphen, underscore); native random GUID generation satisfies the format. Bound compact assertion to 8 KiB before parsing.
- Consume SHA-256 of an unambiguously length-prefixed client ID plus jti in one shared ledger across PAR/token; no raw assertion or jti is retained in diagnostics. Unique insertion/commit precedes authenticated request acceptance. Replay, capacity denial, cancellation or unknown completion cannot grant success. A later invalid grant does not restore proof usability.

`effectiveNow` never moves backward relative to the profile's committed replay watermark; observation/consumption is serialized in the same bounded transaction. Clock anomaly beyond the existing 30-second tolerance fails closed; taking the greater observed time within tolerance may shorten validity but cannot extend it. Cleanup retains a consumed entry through `exp+30`; stored timestamps and digest cannot be attacker-expanded beyond the validated bounds. No process-local replay fallback.

## Lifetime and current authority

| State/proof | Maximum and boundary |
| --- | --- |
| Password and factor admission freshness | Both ages strictly less than 300 seconds; equality denied; any shorter max_age applies to both. Future/rollback state follows existing account policy, never renewed by a protocol call. |
| PAR request | 60 seconds; reject at expiry even if native validation tolerates skew |
| Authorization code | 60 seconds, further bounded by original session expiry and admission freshness/requested-age deadline; explicit post-native equality check before exchange |
| Access / identity token | Five minutes each, shortened by original source-session expiry and applicable fixed registration policy; issuance does not renew password/factor proof |
| Native client callback state | Five minutes; explicit expiry-at-equality check before accepting callback output; one use, including error callbacks |
| Client assertion | 60 seconds, strict exp boundary above; new proof per intentional request |

Parse max_age as one nonnegative bounded integer; values above 300 cannot widen the ceiling, negative/duplicate/malformed values are invalid_request, and zero with prompt=none requires fresh interaction and returns login_required.

The current locked admission observation covers account enabled/lockout, enrollment active/not suspended, usable factor, exact security stamp/authentication and factor generations, full `pwd otp` session, idle/absolute expiry and at least one approved persisted administrator permission. Restricted recovery never qualifies. Do not trust browser-supplied authentication timestamps or permission claims. A session validation side effect may update ordinary activity only under existing account policy; it cannot reset proof time or source absolute expiry.

At authorization and exchange, validate native manager IDs and the entire fixed registration (enabled, exact redirect/permissions/requirements/key metadata/scope-resource binding). Disable live entity-cache shortcuts for these decisions; prove cross-instance committed changes are observed. Any implementation adjustment to shared manager caching must preserve existing pilot tests. A fresh admission result is internal to one issuance operation, not a signed reusable authority capability. The successful native code/token persistence commit is the issuance linearization point. Serialize the current session/account/grant/profile observation and final time check with that native persistence: a supported invalidation committing first denies issuance; issuance committing first may complete, while later API use still needs M02-D current-authority validation. Hold the required local ownership through native manager persistence/commit and commit before accepting the protocol response; never hold it over remote network calls. T005 must prove the exact native scoped-context/event/lock implementation of this rule, not select a weaker rule. An infeasible transaction boundary blocks that implementation path pending a reviewed design correction. Network delivery is not atomic with commit; response loss gives no automatic retry. Race tests report the actual commit order.

## Claims and token ownership

| Claim | Source and destination |
| --- | --- |
| `sub` | Exact persisted user identifier; native code, access and identity tokens |
| `sid` | Exact persisted administrator source-session public identifier; code, access and identity tokens, for later live validation/session binding |
| `auth_time` | Original password verification time, conservatively constrained by both password/factor freshness; code and identity token |
| `amr` | Validated full authentication methods (`pwd`, `otp`); code and identity token |
| Internal factor time / original source expiry / profile binding | Protected native code/private client-state properties as needed; never raw security stamps/factor secrets or public authorization claims |
| Native nonce, presenter, authorization/token IDs | Native processing/bindings retained; do not reconstruct from callback input or copy to unintended public destinations |

No roles, permission snapshots, arbitrary user claims, email/profile or refresh credentials are released. Access token is asymmetrically signed with access-token encryption disabled per approved resource-server contract; code/private state remain protected. Native client validates ID token for its client and never treats it as API authority. B2 does not implement a consuming Admin API or claim the five-second API enforcement target.

## Native client and transport

Explicit native core registration uses `AdminSessionDbContext`; state storage stays enabled. Global native client signing/encryption credentials and registration-specific assertion signing credentials have separate documented roles. Reuse bounded encrypted PKCS#12 loading/private-directory validation; private keys never reside in registration JWKS or output. Missing/corrupt/expired material fails closed; no transient key generation. Retained state signing-validation keys and decryptors remain available until all issued state expires. Operator replacement/restore has an explicit invalidation runbook; unsupported credential rotation is not claimed as automatic.

Require advertised issuer/PAR endpoint, code/query and S256 compatibility and approved asymmetric method/algorithm. Constrain endpoints to the configured issuer origin and canonical paths; no redirects to arbitrary origins or user-supplied discovery overrides. HTTPS trust stays enabled. Verify actual native negotiated behavior rather than relying on metadata alone.

For test-owned query callback transport use distinct `__Host-identity-admin-client-state` correlation prefix, Path `/`, no Domain, Secure/HttpOnly, SameSite=Lax and state-bound expiry. Preserve native cookie/state/nonce validation and single-use consumption; wrong/missing cookie denies. No B1 ticket or product browser cookie is issued by this test transport.

Remove automatic PAR/token retries, including default SystemNetHttp resilience retries. A lost response means no resubmission and no invented success; caller starts a fresh authorized flow when safe. Metadata GET/JWKS can use only explicitly bounded read behavior. Bound backchannel request to 10 seconds and whole native client exchange to its five-minute state lifetime; no timeout extension through retries. Test native HTTP call counts under cancellation/response loss.

## Errors, limits and evidence

Native OAuth errors are retained: invalid client/proof/replay → `invalid_client`; malformed request → `invalid_request`; invalid/reused/expired code → `invalid_grant`; missing/stale account proof in authorize → `login_required`; valid authentication without current permission → `access_denied`. On unavailable authority, no success is allowed: authorization uses a native temporary-unavailability response where safe, token/PAR use a protocol-safe error or sanitized 503. Pin exact native responses/statuses in the implementation inventory; never replace them with cookie redirects or ProblemDetails. No unvalidated redirect may receive an error containing private data.

Preserve 16 KiB request body, 16 form values, 128-character keys, 8 KiB values, 16 concurrent account requests and existing 10-second request / five-second authority operation bounds. Apply PAR form/media/multiplicity/rate guards before native handling; a mapped endpoint-only guard is insufficient. Retain the existing 600/minute process-local account guard and add shared replay capacity (10,000 retained assertion rows maximum, including unexpired tombstones, batch cleanup at most 100); this is not a distributed rate-limit or throughput claim. Do not evict live entries when full. A valid client's requests cannot consume unbounded persistent rows; native request/state creation and cleanup must also have bounded retention/capacity tests. For the fixed profile allow at most 128 live native PAR references and 1,024 live native callback states, with at most 10,000 retained rows in each category and cleanup batches of at most 100. Serialize quota check with native creation in its owning database so concurrent instances cannot exceed the cap; full/unknown capacity fails closed without deleting live state. Prove manager participation and rollback; add a narrow portable coordination row only if native transaction ownership requires it. This is a feature implementation prerequisite, not an assertion about current native defaults.

Use generated correlation IDs and finite outcome categories only. Logs, traces, test artifacts and error pages exclude assertion/code/state/token/verifier/private keys, raw query/body, connection strings and personal data. Tests use synthetic sentinels and scan retained output. Backup restore can resurrect native/replay state; invalidate affected registrations, grants, sessions and pending client state before enabling the restored boundary. No automatic production restore operation is authorized.


### Complete native storage budget

The server budget covers **all native tokens and authorizations created for the fixed B2 application**, not only live PAR references: at most 20,000 retained native token rows (including request/code/access-token entries and any other native types actually persisted) and 2,000 retained authorization rows. The 128-live-PAR and 10,000-retained-PAR sublimits also apply; count PAR rows within the total token cap, not as extra capacity. BFF client storage has at most 1,024 live and 10,000 retained callback-state rows. Assertion consumption's separate cap is 10,000 retained rows including tombstones. Expired/revoked/consumed rows count until safely deleted.

The owning database serializes budget check/reservation and native manager creation. Platform profile coordination owns server token/authorization budgets; a narrow Sessions coordination row may own native client-state budgets. The implementation inventory must prove the exact native transaction/event boundary and safe lock ordering with admission. Commit before protocol response acceptance; never hold database locks across remote HTTP. A rejected or definitely rolled-back creation releases its reservation; ambiguous commit retains capacity until bounded reconciliation reads durable state, with no duplicate creation/retry. A budget counter alone is not authoritative: reconcile manager-owned rows and reservations under the same ownership so drift cannot permit over-allocation.

Cleanup is at most 100 rows per owning invocation and respects native status/expiry/authorization dependencies. Never delete a live token, active request/state or authorization needed by any live token; native grace/retention rules may delay reclamation, in which case capacity denial is expected. Only scoped B2 rows are eligible; no pilot or unrelated token pruning. Cover quota equality and over-limit, concurrent reservations, failed cleanup, partial manager creation, rollback and unknown commit on both providers. This bounded B2 lifecycle is required even though a platform-wide cleanup/audit retention service remains later work.
