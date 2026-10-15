# B2 source and dependency decisions

15 October 2026. Repository baseline `b17e4a7969c4b0e437f14b1e8f25a4d1f0ae93f7`; B1 source squash `ca410c01` is accepted, not repeated. Remote main was verified before planning and the worktree was clean. The managed runtime reports current enforced HTTP policy, including the two Microsoft regional hosts. No image pull, database experiment or local native test was required for this research.

## Repository evidence

| Inspected source | Observation / consequence |
| --- | --- |
| `Directory.Packages.props`, BFF `.csproj` and lock files | OpenIddict server/EF 7.7.1 is pinned; the BFF has no native client package or registration. A locked client dependency change is future implementation, not a planning result. |
| `AuthServer/Features/Setup.cs`, `Features/Pilot/Setup.cs`, `Features/Protocol/Setup.cs` | Server registration/mapping currently depends on the Development pilot. It enables code/S256, Basic client authentication and authorization-request caching, but no PAR. Administrator-only composition exposes no protocol. |
| `Features/Protocol/Policy/PilotEligibility.cs` | Pilot requires its fixed manifest, no MFA and no roles. Keep this policy unchanged; dispatch administrator decisions separately. |
| `Features/Accounts/Shared/AccountSchemes.cs`, `AdministratorCookieEvents.cs` | Separate administrator scheme, host cookie, every-request durable validation and one-shot issuance already exist. Cookie validation alone is not a protocol authority permit. |
| `Infrastructure/Features/Identity/Journeys/JourneySessions.cs`, `JourneySessionState.cs`, `JourneySessionProof.cs` | Existing current account/enrollment/factor/stamp/generation/session/time checks and bounded native lock ownership. Reuse these mechanics coherently with grants; do not compose two stale detached observations. |
| `Infrastructure/Features/Identity/Governance/GovernanceGrants.cs`, `GovernanceService.cs` | Exact persisted permission vocabulary/role grants exist. Login needs at least one recognized permission; the full survivor population query and command-bound mutation proof are not reusable login authority. |
| `Features/Pilot/PilotBootstrap.cs` | Manager-backed transactional initialization and exact-match/no-repair behavior supply a precedent. New fixed administrator profile still needs its own manifest, audit and both-provider controls. |
| `Features/Protocol/Token/ClientIdentityConsistency.cs`, protection setup | Original-body/Basic identity consistency and form-media controls currently cover token/authorize. Extend deliberate PAR extraction without accepting mixed methods or duplicates. |
| `Infrastructure/Persistence/PersistenceRegistration.cs`, `AdminSessionDbContext` | Session-only context/native tables exist, but context registration alone supplies no OpenIddict client/core managers. Keep BFF away from platform identity/business state. |
| Existing `AdministratorEnvironment` / `AdministratorJourney` test harness | Real password, enrollment, TOTP, antiforgery and cookie journey can establish the positive issuer session. Test-owned native callback transport must not replace it with a forged principal. |

## Pinned upstream capability evidence

OpenIddict tag 7.7.1 resolves to commit [`9146a148b3333570704a0211d037cd318093770a`](https://github.com/openiddict/openiddict-core/tree/9146a148b3333570704a0211d037cd318093770a/src). Independent protocol/security source review inspected the following files at that commit. Line references identify this snapshot; they are not runtime results.

| Pinned source | Supported fact / product obligation |
| --- | --- |
| `OpenIddict.Server/OpenIddictServerHandlers.cs` 607–911 | Native signature/iss/sub/client/audience validation; jti is checked if supplied, not required. No durable assertion-use ledger is installed by this pipeline. Require product jti/lifetime/single-use policy after native verification. |
| `OpenIddict.Server/OpenIddictServerHandlers.Protection.cs` 946–997 | Assertion lifetime uses `exp + skew < now` and fresh validation parameters. Do not infer strict equality or product assertion skew from server-token options. Enforce explicit post-native bounds. |
| `OpenIddict.Server/OpenIddictServerHandlers.Authentication.cs` 1905–1964, 3623–3666 | Native per-client PAR requirement and endpoint permission. Use these rather than global PAR enforcement on the pilot. |
| `OpenIddict.Client/OpenIddictClientHandlers.cs` 2772–2833, 6635–6660 | Native assertions use canonical issuer audience and fresh GUID jti. The audience is not the PAR/token endpoint. |
| `OpenIddict.Client/OpenIddictClientHandlers.Protection.cs` 1082–1092 | Native generation emits `client-authentication+jwt`, compatible with native server type checks; no custom JWT-type shim. |
| `OpenIddict.Client/OpenIddictClientHandlers.cs` 6259–6295, 5506–5594 | PAR is automatic only when advertised; PKCE can have no common method. Product must require PAR/S256 and reject metadata downgrade. |
| Same client handlers 5664–5838, 812–864 | Stored reference state binds issuer/registration/redirect/grant/response/scopes/nonce/verifier/correlation and is consumed before later callback checks. Default state lifetime is 15 minutes; use the explicit five-minute bound. |
| `OpenIddict.Client/OpenIddictClientHandlers.Protection.cs` 913–931 | Registration signing credentials serve assertions; global client signing/encryption credentials serve state. B1 Data Protection wrapping is a separate role. |
| `OpenIddict.Client.AspNetCore/OpenIddictClientAspNetCoreOptions.cs` 77–84; handlers 356–460, 742–814 | Native secure/HttpOnly correlation defaults use SameSite=None; configure the fixed query-profile host cookie deliberately and retain native validation. |
| `OpenIddict.Client.SystemNetHttp/OpenIddictClientSystemNetHttpOptions.cs` 48–59; configuration 108–117 | Default resilience pipeline has four retries without a method restriction. Explicitly remove automatic PAR/token retry; verify actual request counts under lost responses. |

Current source supports choosing native integration. It does **not** prove B2 interoperability, multi-node replay denial, package compatibility in this solution or any production activation. Exact locked dependency resolution (including IdentityModel), native event ordering and generated request values must be verified in T004/T005 before implementation claims.

## Decisions and rejected alternatives

| Decision | Rationale | Alternative rejected |
| --- | --- | --- |
| Fixed administrator profile with explicit no-interaction requests | Useful issuer/client boundary using existing real account sessions; leaves B3's request-bound fresh login visible | Relaxing pilot or wiring an unbound login redirect |
| Current session and exact persisted permission admission | Includes specialist/Auditor roles while denying stale role claims | Requiring the full platform-admin survivor bundle or inventing admin.access |
| Native code/S256/PAR/private_key_jwt | Approved ADR-06; source supports the mechanisms | Handcrafted JWT/code/PAR state, Basic fallback |
| Product assertion consumption ledger | Native code/state replay does not cover client assertions | In-memory cache or treating jti presence as replay protection |
| Registration/issuer pin and required metadata | Prevents PAR/PKCE downgrade and dynamic outbound targets | Discovery presence alone as proof of product policy |
| Distinct native client credentials plus session store | Preserves component/key-role ownership and restart | Reusing issuer secrets, assuming B1 wrapping supplies signing/encryption |
| No automatic mutation retries | Default native HTTP behavior conflicts with existing security-mutation policy | Retrying PAR/token after ambiguous response |
| Default-disabled issuer boundary; dormant product BFF callback | Scoped internal acceptance without claiming B3/API | Opening the product admin UI based on source/library review |

No product clarification is required from the user: approved documents, current continuation and existing security ceilings resolve the bounded decisions. No material architecture change or diagnostic-approval renewal is proposed. Later implementation must surface evidence-backed incompatibilities rather than silently weakening this contract.
