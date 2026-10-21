# B3 source research and design decisions

21 October 2026, Europe/London. Read-only research against main `2da9ef03dda7d2e7f019ad5f813b8d0515b2b9cf`, following B2 squash `42214382f5b6246332fe9b1ecfe00463a5ed9965`. These are design decisions and source observations, not executed B3 evidence.

## R1 — Separate human login from the native exchange

**Observation:** `Admin.Bff/Features/Authentication/Protocol/NativeClientChallenge.cs` admits only `prompt=none` and optional bounded `max_age`. `AuthServer/Features/Accounts/Mfa/Login.cshtml.cs` rejects login query parameters; the administrator journey returns to its fixed security destination. The pilot password-only interaction is not administrator authority. Existing PAR lasts 60 seconds; client state lasts 300 seconds. Existing administrator interaction and freshness ceilings are 300 seconds (`Core/Features/Identity/Authentication/AdministratorPolicy.cs`).

**Decision:** use a separate pre-OAuth login intent, with a fixed issuer entry and fixed BFF resume route. Only after actual bound password/MFA completion does the BFF start a fresh ordinary B2 native challenge. Initial navigation grants no OAuth authority; authority requires the real issuer ceremony, authenticated native PAR and matching issuer session/browser bindings. The new intent never accepts a browser-selected client or return URL. Its maximum lifetime is 300 seconds from original creation and is not restarted by resumption. Each existing component's shorter deadline still wins.

The native PAR extension carries the expected opaque intent/completion pair for the fixed client only. Store it in protected native request/code/client state, validate it before admission and bind it to the new source session. A stolen reference alone cannot authenticate. An issuer completion is attached once to one native authorization request under owned persistence; replay, failed/unknown commit and capacity denial fail closed. The final native ID-token binding lets the BFF compare its original intent after cryptographic validation.

**Rejected:** stretching PAR/state through human login; replaying a consumed error callback; accepting `auth_time >= startedAt` as ceremony proof; a general return URL; changing all B2 requests to interactive prompts; reusing pilot cookie authority. A `prompt=login` switch alone does not solve native expiry.

**Implementation gate:** demonstrate pinned OpenIddict 7.7.1 handler order, constrained parameter preservation and transaction ownership before executable integration. This design does not assert that those adapters already exist. If the native mechanism cannot meet the contract, correct and independently review the design before proceeding; do not silently weaken the binding.

## R2 — Authenticated original source deadline

**Observation:** `AuthServer/Features/Protocol/Administrator/AdministratorPrincipal.cs` places `sub`, `sid`, `auth_time` and `amr` in their public destinations, but keeps `oi_admin_source_expiry` and factor time code-private. `JourneyCompletion.CreateSession` supplies the actual immutable issuer deadline. [B1](../010-bff-session-foundation/contracts/ticket-store.md) explicitly requires that deadline, not access-token expiry. The opaque `sid` contains no derivable expiry or independent revocation permission.

**Decision:** add two tightly scoped native ID-token claims for the B3-bound exchange only: `admin_session_expires_at` (scalar integer UTC epoch seconds, exact persisted source deadline) and `admin_login_intent` (canonical SHA-256 digest of the original intent). The issuer computes them; browser input cannot provide either value. They accompany the already validated `sub`/`sid`, client audience and nonce. They are not permissions or API claims. Do not expose internal `oi_` claim names, factor secrets, stamps or arbitrary properties. Existing B2 requests without a B3 binding retain the delivered minimal claims.

Strict client validation rejects duplicate/malformed/missing claims, wrong intent, a source binding inconsistent with the issuer-completed intent, expired source deadline and a deadline outside the existing issuer session policy. Signature validation alone is insufficient without native issuer/audience/nonce/state checks. Construct a fresh canonical B1 principal, retaining only the documented local identity and protected server-held token properties. Do not sign in OpenIddict's merged principal wholesale.

This is a new B3 fixed-client contract, explicitly reviewed as a change to B2's current claim allowlist; it is not already available or an assertion of later current authority. Supported invalidation before B2 issuance denies issuance. Later source revocation/shortening is not automatically visible in B1; M02-D's current-permission/five-second API boundary remains separate.

## R3 — Durable browser ordering and uncertain completion

**Observation:** `Admin.Bff/Features/Sessions/SessionTicketStore.cs`, `SessionTicketPolicy.cs` and `SessionTicketCodec.cs` provide committed create, protected payload, fresh random handles, expiry, CAS renewal and revocation. They do not coordinate an outstanding callback against a later browser logout. The B2 callback executor is test-owned; product endpoint ownership is absent.

**Decision:** a Sessions-owned browser generation coordinates start/replacement, establishment and local logout. The browser context is not itself authentication. Establishment compares the original generation, consumes the intent and creates the ticket in one owned Sessions transaction with an authenticated audit intent. Logout/replacement advances the generation and revokes the previous ticket under the same ordering. Preserve B1's no-resurrection and commit-before-result guarantees; prove shared context/transaction participation before adapting it. No lock spans issuer HTTP. Unknown completion retains reservations/tombstones until safe bounded reconciliation; no automatic callback/token replay or replacement cookie is issued.

A response may be lost after ticket commit. The resulting orphan must expire/clean up within its original bounds; a new explicit sign-in can revoke the prior generation's stored ticket. In-flight reads that preceded committed revoke are not retroactively cancelled. An already-sent stale cookie may arrive late in a network race; durable generation validation must render it unusable. Do not promise HTTP response ordering.

## R4 — Local first, native issuer-assisted logout

**Observation:** B1 revoke is local only. `AuthServer/Features/Accounts/Security/Logout.cshtml.cs` and issuer journey logout require account antiforgery and revoke one issuer source session. They do not provide OIDC end-session. B2 advertises neither end-session nor refresh. Approved architecture requires local logout to remain effective when remote completion fails; R1 includes RP-initiated logout, while arbitrary-client front/back-channel logout belongs to R1.1.

**Decision:** include a bounded native end-session dependency in full B3. Commit local revocation/generation invalidation first. Native issuer end-session validates the one fixed client and registered post-logout URI, then requires explicit issuer antiforgery-protected confirmation and a current administrator cookie matching the expected source-session selector. A selector is never authority to revoke another session. Reuse the issuer's audited single-session revocation path. An already absent session can be reported separately from newly revoked or unverifiable; no all-session or grant revocation claim follows.

Prefer a native `client_id`/`logout_hint` flow with a narrowly interpreted opaque source-session selector and explicit confirmation, so an ID/access token never has to travel through the browser. The BFF retains token material server-side. Native end-session parameter validation, exact return URI and state/correlation remain mandatory. Do not invent a hand-signed logout token or claim that redirect arrival proves remote revocation. The pinned library's no-ID-token-hint validation path must be proven before this dependency is implemented. The BFF always reports remote completion as unverified from a browser return; it does not need a bespoke attestation endpoint. The issuer can describe its own committed result, and provider/browser tests independently inspect actual issuer revocation. This distinction permits real native remote logout without inventing proof at the BFF.

**Rejected:** treating a local cookie clear as issuer logout; cross-site GET logout mutation; passing an ID token through a browser query; using BFF Platform credentials; silently deferring issuer logout while marking all B3 complete. Refresh remains disabled; later M02-C must integrate any refresh capability into logout before enabling it.

## R5 — Evidence and provider continuity

The existing EF provider registry and separate migration assemblies remain authoritative. Additive Platform/Sessions changes are owned by their respective services; BFF runtime keeps Sessions-only rights. Native current-permission checks remain issuer-owned, not copied into browser claims.

Firefox restoration is the next executable prerequisite: feature 011 T028 restores all original 55 coverage inputs plus new obligations (current B2 has 50 with seven deferred, therefore at least 57 before further additions), all six browser/provider cells, unchanged floors, and a reviewed disposition of the actual history-evidence failure. A green rerun alone does not establish historical causation. No B3 implementation starts under the expired B2-only deferral.

## Product-choice disposition

The user explicitly selected new password/MFA for every sign-in and both local and issuer-assisted logout on 21 October 2026. The specification and tasks incorporate those answers. No reuse of still-fresh authority or local-only full-B3 acceptance is permitted.

## R6 — Explicit fixed-registration upgrade

Source review of `AdministratorRegistrationPolicy` shows the delivered manifest has authorization/token/PAR permissions, zero post-logout URIs and exact permission/settings matching. The initializer refuses drift. Adding end-session metadata out of band would make current admission fail.

Add an explicit audited, version/precondition-bound upgrade of the fixed administrator registration under existing profile ownership. Accept only the exact delivered manifest/revision or the exact already-upgraded manifest; arbitrary drift still denies. Preserve any disabled state, advance the profile revision so existing code-principal revision checks deny old authorization codes, and explicitly revoke scoped pending PAR requests through native managers in the owned upgrade transaction. Pending PAR does not already carry the code-principal revision check; prove its separate invalidation before accepting the new profile. Register only the fixed end-session permission and exact HTTPS post-logout URI. Do not repair on startup, silently reenable a client or mutate unrelated clients. Both-provider tests must cover old-to-new, idempotent repeat, stale precondition, drift, disabled profile, rollback/unknown commit and separate old-code/old-PAR denial. This is a B3 implementation prerequisite linked to B2 FR-004/007, not a generic registration redesign.
