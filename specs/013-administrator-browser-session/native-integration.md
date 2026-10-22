# B3 native identity and state implementation

22 October 2026. First executable B3 slice under [A-009](../../docs/amendments/A-009-roadmap-browser-deferral.md), based on verified main `f0f5460c482029286dd0113be0f5a1cba37a691e`. PR #69 and its separate restoration/diagnostic work remain preserved. This implements the identity-validation/projection portion of T007 and proves the identity/state portion of T003; neither whole task is complete.

## Implemented behavior

`Admin.Bff/Features/BrowserSessions/BrowserSessionIdentity` validates the original B3 source-session deadline and expected login-intent digest against the exact native-validated ID token and principal. `BrowserSessionClaims` rejects missing, duplicate, case-aliased, array-shaped, wrongly typed or noncanonical required fields. Subject/session are bounded to the existing B1 limit. Authentication methods are exactly `pwd` and `otp`; password proof freshness is strictly less than 300 seconds, with the existing 30-second future-clock tolerance. Source expiry equality denies and its supplied original value is preserved, subject to the eight-hour policy ceiling. It is never derived from ID-token expiry, password time or session ID.

The accepted result constructs a new single identity in `IdentityPlatform.Admin.Bff.Session`, containing only `sub` and `sid`, with refresh and persistence disabled. It copies no native identity, role, permission, actor, token or arbitrary properties. The existing B1 policy/codec still owns protected payload bounds and the minimum source/absolute/idle deadline. Tests exercise that actual codec, not an invented persistence substitute.

`BrowserSessionIntent` reads one unique canonical private `oi_b3_login_intent` expectation from protected native state. A real native state preparation/signing/encryption/validation round trip proves that this field survives without widening the B2 PAR parameter allowlist.

These internal components are dormant. No production handler, callback, endpoint or cookie is registered by this slice. No completed browser login, database transaction, issuer claim issuance or durable state redemption is claimed.

## Native ownership and parsing evidence

- Pinned SDK: 10.0.401; OpenIddict: 7.7.1. The Host lock/restored assets actually resolve `Microsoft.IdentityModel.JsonWebTokens` 8.19.2; the central declaration alone is not the resolved-version evidence. Dependencies and lock files are unchanged.
- Signed fixture JSON passes through native `ResolveTokenValidationParameters`, the existing fixed-profile `NativeClientValidationParameters`, `ValidateIdentityModelToken`, and `MapInternalClaims`. These focused controls prove actual native cryptographic parsing and local claim policy. They do **not** execute the complete authentication dispatcher, audience/presenter/nonce/correlation processing or an HTTP callback.
- IdentityModel's payload dictionary can overwrite duplicate JSON fields and materialize array elements as scalar claims. Therefore principal-only multiplicity checks are insufficient. After native acceptance, the supplemental guard uses native `JsonWebToken.EncodedPayload` and IdentityModel decoding, then bounded `JsonDocument.EnumerateObject()` to inspect the original field multiplicity and shapes. Accepted values must also match the native principal. This adds no signature verifier or replacement protocol parser. Encrypted ID-token shapes are unsupported by this fixed slice and denied.
- Real native protected-state proof uses `PrepareLoginStateTokenPrincipal`, existing `NativeClientRetainedKeyTests.GenerateAsync` and `ValidateAsync`. This proves protected representation survival, not a database consume/commit.
- Future callback integration belongs after all native identity validation and the existing `NativeClientAuthenticationCompletion` receipt check, before `PopulateMergedPrincipal`. The product owner must supply the exact validated backchannel token/principal and protected original state expectation. Calling the pure adapter with a fabricated principal is not authentication or an authority receipt.

## Verification and traceability

66 new Host executions pass with zero skips. They cover signed type/shape/duplicate/alias defects, native rejection of an array-shaped subject, exact protected expectation, source/freshness/skew equality boundaries, same-value native type substitution, identity/actor multiplicity, 400/401-character bindings, projection isolation and the real B1 codec. Full native crypto acceptance is asserted for shape cases that reach the adapter; the native array-subject rejection is a distinct negative case. Date-shape mutations use otherwise-valid values to avoid a false pass from unrelated expiration.

| Scenario | New test class | Evidence scope |
| --- | --- | --- |
| B3-AUTH-03 / FR-003/004/013 | `BrowserSessionIdentityTests` | Signed payload/native principal validation; original expiry, method and protected-intent checks. |
| B3-AUTH-04 / FR-004/013 | `BrowserSessionProjectionTests` | Fresh canonical identity, bounds, no native property/claim authority copying. |
| B3-AUTH-02/03 / FR-002–004 | `BrowserSessionIntentTests` | Real native protected-state preservation and strict expectation parsing. |

The Host inventory increases from 1,126 to 1,192. New production files belong to the already required BFF coverage module; all 15 floors and 50 active collector inputs remain unchanged. Firefox's seven inputs remain visibly deferred. The known WebKit certificate-evidence case is explicitly deferred on both providers under A-009; all other WebKit and Chromium checks remain required. No skipped case is counted as passing.

Initial focused failures exposed fixture assumptions: native identities use `CaseSensitiveClaimsIdentity`, the reused state validator requires its fixed synthetic subject, `IsPersistent=false` omits its optional property, and array-shaped `sub` is rejected natively before projection. Corrections retain those native behaviors and strict assertions. Code review also corrected freshness equality and strengthened otherwise-valid malformed-date controls. Final build has zero warnings/errors; code-quality controls pass with no allowance changes.

Full Host passes 1,192/1,192 with zero skips and all 211 Python controls pass. Independent architecture/test and security source reviews pass. Hosted verification remains pending and belongs in `verification.md` and the delivery PR after execution. This document is not evidence of both-provider or browser execution for B3.

## Remaining native integration work

1. Constrained intent/completion preservation through authenticated PAR, issuer completion attachment, private code state and B3-only ID-token issuance; actual event ordering and current-authority rechecks.
2. Shared Sessions ownership for generation, intent consumption, ticket and audit. Existing `SessionTicketStore.CreateAsync` opens a separate scope/commit; wrapping it in another transaction does not provide B3 atomicity. Prove a shared context/transaction before product establishment.
3. Native no-ID-token-hint end-session validation, exact client/return/session binding and issuer confirmation.
4. Audited fixed-registration upgrade preserving disabled state, separate old-code revision rejection and old-PAR native invalidation.
5. Product routes, cookie/CSRF/generation integration and both-provider concurrency/failure verification, followed by both logout boundaries.

Continue these implementation dependencies. Firefox/Playwright diagnostics and upstream response are deferred under A-009 and do not prevent this work. Historical findings, human assessment and production/public-release acceptance remain open.
