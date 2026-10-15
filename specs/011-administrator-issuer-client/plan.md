# Implementation plan: Administrator issuer/client integration

**Branch**: `053-admin-issuer-client` | **Date**: 15 October 2026 | **Spec**: [spec.md](spec.md)

## Summary

Deliver B2's fixed administrator protocol profile and native confidential-client integration after B1 squash `ca410c01` and delivery record `b17e4a7`. This packet is prose planning, not running B2 code. Implement the complete bounded issuer/client path with real noninteractive administrator admission and test-owned native callback transport. B3 owns production browser login/callback/session/logout. Do not repeat the B1 store or governance implementation.

## Technical context

Pinned .NET 10.0.401 SDK, OpenIddict 7.7.1, ASP.NET Identity/EF Core, xUnit v3/MTP and native Aspire; PostgreSQL and SQL Server are equal. Keep current package versions. Add only the required 7.7.1 client ASP.NET Core/SystemNetHttp package references and compatible resolved dependencies after locked restore/advisory/license review; the current BFF project has no client package. Presence of library APIs is source evidence, not interoperability evidence.

The current server is composed only by the Development pilot. Separate shared server setup from profile-owned policy, so disabled, pilot-only, administrator-only and both-enabled modes have deliberate behavior. Keep one OpenIddict server pipeline and named account schemes. Production defaults remain health-only; no new endpoint is enabled by a missing/implicit setting. Tests opt into the B2 boundary explicitly; production BFF composition remains dormant.

## Constitution check

Approved v0.2 and current continuation authorize this work; no amendment or exception is proposed. All 23 principles apply. Feature-local modules remain within existing projects, managers own native state, shared persistence preserves database rights and native lifecycle. BFF never reads the platform identity database. Current authorization is checked by AuthServer; B1 tickets and prior cookie validation are not authorization capabilities. New/changed code must meet documentation, Given–When–Then, named-value and size rules without baseline expansion. Both-provider, real host, failure/concurrency and security mutation evidence accompany implementation. Human/activation/release gates remain open.

## Source layout and ownership

Paths below name planned additions unless research identifies them as existing. Names may refine without creating new project layers; record actual source/test IDs before execution.

| Existing project / feature path | Responsibility |
| --- | --- |
| `AuthServer/Features/Protocol/Setup.cs`, `Features/Setup.cs`, composition startup context | One protocol composition with explicit profile switches; preserve pilot routes/settings and account isolation |
| `AuthServer/Features/Protocol/Administrator/` | Fixed profile, validated registration policy, administrator authorize/code-exchange decisions and claim destinations |
| `AuthServer/Features/Protocol/ClientAssertions/` | Post-native asymmetric proof policy, durable consumption, PAR/token extraction consistency and finite outcomes |
| `Infrastructure/Features/Identity/ProtocolAdmission/` | Bounded current session/account/factor/grant observation, using existing authority checks/lock discipline rather than duplicating them |
| `Infrastructure/Features/Identity/Persistence/` | Fixed-profile manifest and assertion replay rows; existing native OpenIddict stores remain authoritative |
| Existing platform provider migration projects and `src/migrations/IdentityPlatform.Migrator/Features/` | Explicit additive schema/grants and fixed manager-backed registration initialization with atomic audit |
| `Admin.Bff/Features/Authentication/` | Internal opt-in native client registration, durable client-owned key loading and no-retry transport policy; no production callback route |
| `Host.Tests/Features/AdministratorProtocol/` | Composition, options, claim/proof bounds, dormant route inventory and seeded controls |
| `AppHost.Tests/Features/AdministratorProtocol/` | Real two-instance issuer/client/provider journeys, current-authority denial, replay and failure scenarios |
| `tests/shared/` and existing browser harness | Test-owned callback composition and existing real password/TOTP journey; never a production identity injection route |
| Existing verification selectors, matrix, mutation manifests and coverage consumers | Actual discovery, mandatory bounded groups, new mutation targets and unchanged floors |

Use the actual existing project prefixes (`src/backend/`, `src/persistence/`, `src/migrations/`); no new assembly hierarchy. The initializer belongs to `src/migrations/IdentityPlatform.Migrator/Features/`.

## Delivery sequence

1. Finish source/library research, specification, clarification, contract/data model and tasks; independently review this packet and squash after proportional checks. Feature 010 T022 completes only that planning obligation.
2. Inventory the exact executable diff and host/middleware/manager transaction boundaries. Implement fixed profile registration, additive replay state, current admission and native client together with denial cases; no helper-only B2 completion claim.
3. Run focused pure/native/provider evidence, then full exact-head CI and independently reconcile raw reports, discovery, mutation results and coverage. Host the native work where capacity exists; local Docker storage is still insufficient. Do not run paused historical diagnostics.
4. Normally squash only after acceptance. Record B2's scoped result, then prepare B3's request-bound fresh login and product callback/cookie/logout journey. No release or production configuration change follows automatically.

## Key decisions

### Fixed profile and initialization

The fixed client is `identity-admin-bff`; scope is `identity.admin`; resource is `identity-admin-api`. Require exactly `openid identity.admin`, response type code, query response, S256, `prompt=none` and native PAR. The one exact HTTPS callback is operator-configured and manifest-bound; reject fragments/userinfo, wildcards and loopback HTTP exceptions. It is a reserved B3 callback URI, exercised only by test-owned transport in B2. This does not create a production callback.

Provision through application/scope managers, using their existing scoped platform context/transaction. Include public JWKS, confidential web type, implicit first-party consent, exact permissions and client-specific PAR/PKCE requirements. Do not set global PAR enforcement (it would change pilot behavior). Product manifest binds manager IDs, callback, enabled/revision and approved key metadata; it is not a second authoritative scope/redirect store. An exact repeat is a no-op; drift/collision/disablement is an error requiring an explicit reviewed operator change. Audit failure rolls back new native rows and product intent; verify manager saves participate. No web-start seeding or migration. Registration/key changes must invalidate outstanding policy bindings and use a separate audited compare-and-replace operation if introduced; no automatic repair path.

### Current administrator admission

Reuse existing `JourneySessionState`, `JourneySessionProof`, ceremony clock/connection/lock rules and `GovernanceGrants` to observe current full account/session/factor authority and exact persisted approved permissions under one bounded operation. At least one approved permission is required; specialist/Auditor roles qualify. Do not call the heavyweight whole-population survivor query merely to log in, and do not introduce an `admin.access` permission or require the PlatformAdministrator survivor bundle.

The issuer authenticates only the explicit administrator cookie scheme on this profile; it then performs a fresh admission observation. Earlier `AdministratorCookieEvents` validation is not a reusable permit. Code redemption independently rechecks the native principal's subject/source-session and current authority, registration, authorization status and exact scopes/resources. Reuse validated bindings rather than replacing a native code principal with a raw Identity principal. Include a final eligibility/time check with native issuance persistence. The successful code/token persistence commit is the linearization point: supported account/session/grant/profile invalidation committing first must deny issuance; issuance committing first may complete. T005 must prove shared native manager context/transaction/event ownership and safe lock ordering through commit before response acceptance. It cannot relax this product rule if integration is difficult. Do not keep database locks across HTTP backchannels or use stale cached authority. Race tests prove actual commit order, without claiming atomic delivery across network calls or future API authorization.

Both password and factor proof must be within the existing 300-second ceiling, and any shorter max_age constrains both. B2 requires `prompt=none`; missing/expired proof gives native `login_required`. Unsupported prompt/consent values give a protocol error without creating an interaction. Native request caching alone is not fresh-login continuation. B3 must add and prove bound login/consent resumption before enabling it.

### Proof validation and replay

Native 7.7.1 verifies signature, registered key, client identity and audience. Its assertion audience is the canonical issuer, not the PAR/token endpoint. Its native proof validation does not supply the required durable single-use ledger. After native verification, require the exact contract's type, RS256 key, bounded jti/iat/nbf/exp and stricter time checks; do not assume server token options change assertion-validation skew.

Persist one unique digest of a length-prefixed client ID and jti across PAR and token, not one ledger per endpoint. Commit consumption before accepting the authenticated request. A later grant failure does not make that proof reusable. A unique collision is denial; unavailable or ambiguous consumption never succeeds or auto-retries. Enforce capacity before accepting another entry; cleanup must not remove a live proof or reset replay state on restart. Both-provider races, lost-response behavior and targeted replay-bypass mutation are mandatory.

Keep native request/code/state storage enabled. Distinguish pushed requests from authorization-request caching, and prove native consumption semantics with actual two-instance sequential/concurrent cases. Do not synthesize a custom code, JWT or PAR response. If actual pinned behavior cannot meet the required single-use contract, stop that affected acceptance task and implement only the narrowly reviewed application policy needed; no unsupported claim or library upgrade by assumption.

### Native BFF client boundary

Use native 7.7.1 client ASP.NET Core + SystemNetHttp integration, durable `AdminSessionDbContext` state-token store and explicit registration ID. Private authentication signing key and client state-token signing/encryption keys are BFF-owned, distinct from issuer credentials and the B1 key-wrapping role; reuse narrow protected-path/certificate loading mechanics. Keep retained decryptors through the state lifetime. Fail startup on missing/corrupt/unusable material; no development/ephemeral fallback.

Require PAR, S256 and private_key_jwt explicitly; reject discovery incompatibility rather than downgrading. Configure no automatic HTTP retries for PAR and token: native SystemNetHttp's default resilience pipeline retries four times and must not resubmit a security mutation after a lost response. Safe read-only metadata behavior is separately bounded. Test actual outbound counts and fresh proof per intentional operation, not only option values.

Native callback processing must verify state/correlation, issuer/client, nonce, signature/type/audience and token binding. Test-owned transport can wire native callback handlers over real HTTPS and persistent client state; it must not write a B1 ticket, issue a product BFF cookie or stand in for B3 browser acceptance. Use real existing administrator login/MFA to obtain the issuer cookie. No handcrafted administrator ticket or test backdoor replaces authentication evidence.

### Operational bounds

Contract limits and failures are in [contracts/administrator-protocol.md](contracts/administrator-protocol.md); product/native state ownership and cleanup are in [data-model](data-model.md). Preserve existing request body/concurrency/deadline controls and place PAR protection before library request processing, not only on a mapped endpoint. No raw assertion/code/state/token/query or private exception is logged. Store failure uses protocol-safe denial/unavailability; no cookie redirect or management ProblemDetails at backchannels.

## Verification and acceptance

[Tasks](tasks.md) and [quickstart](quickstart.md) enumerate planned evidence. All new cases must be discovered before fixed inventory counts are published; never fabricate expected totals. Add a mandatory bounded native partition if necessary through the reviewed existing selector pattern; do not lengthen existing invocation/job timeouts. Retain all 55 existing coverage inputs and nine floors; explicitly add any new required collector/module inputs. New client composition and admission/proof/manager code require coverage and review even if production activation stays off.

New security mutants must demonstrate omitted current authority, assertion-replay admission and profile/client binding fail at the intended decisive oracle, with same-source ordinary/restored controls on both providers. Existing 42 issuer/administrator/governance mutations remain mandatory but cannot be called new B2 mutation coverage. Real browser completion is scoped to the existing issuer login plus test-owned protocol transport; B3 still requires complete product browser/CSRF/fixation/logout acceptance across all engines.
