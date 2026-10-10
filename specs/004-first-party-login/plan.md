# Implementation Plan: First-party login and S256 code flow

**Branch**: `004-first-party-login` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

## Summary

Deliver M01-D as an explicit Development-only pilot using existing native Identity/OpenIddict persistence and external issuer protection. Razor Pages own credential forms; Minimal APIs own application decisions after OpenIddict validation. No administrator access or production-enable switch is introduced.

## Technical Context

.NET 10/EF 10, OpenIddict 7.7.1, Aspire 13.6.1 and native xUnit v3/MTP retain existing pins. Add pinned Microsoft.Playwright 1.63.0 for browser acceptance and compatible independent JWT validation for the test relying party. PostgreSQL 18/SQL Server 2022 remain equal; existing runtime roles and migrations remain separate. Add no generic repository, mediator framework or production dependency for testing.

Internal scale: one preapproved confidential first-party client, one scope/resource and synthetic non-administrator. Application decisions have a ten-second cooperative cancellation budget. Pilot database connect and command timeouts are five seconds each; this does not imply a universal ten-second native transaction deadline. Never abandon scoped work with Task.WhenAny/WaitAsync. No performance/SLA claim. All credential and token responses remain private, no automatic exchange retry. TLS validation remains enabled in clients/browsers.

## Constitution Check

Pre-design: all 18 principles reviewed. Approval exists; Features, native manager ownership, named schemes, token secrecy, equal providers, audit, operations, tests, stable dependencies and independent reviews apply directly. Management versioning is M01-F; React/BFF is M02; neither is implied by this pilot. Theme assets are a shared infrastructure exception already allowed by the architecture. No exception requested.

Post-design: binding/state transitions, lifetime, concurrency, errors and provider/browser matrices are specified below and in contracts/data-model. Code redemption remains native and cannot be retried or replaced by custom SQL. Current-state guarantees stop at documented committed decision boundaries. M01-E, human accessibility and external assurance gates remain visible. Design review must resolve critical findings before affected implementation.

## Project Structure

- `src/backend/IdentityPlatform.AuthServer/Features/Pilot/`: enablement and explicit bootstrap command; a CLI invocation of this host, never an HTTP route or startup auto-seed.
- `.../Features/Protocol/Authorize/`, `.../Token/`, `.../Policy/`: Minimal handlers and shared current eligibility/claims policy.
- `.../Features/Accounts/Login/`, `.../Shared/`: Razor feature root, interaction/session decisions and UI assets.
- `.../Features/Protocol/Protection/`: bounded request middleware and diagnostic suppression.
- `src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Persistence/`: account enable flag and session/interaction/audit model configuration.
- `src/migrations/*/Platform/`: generated native migrations and snapshots for both providers; update SchemaCatalog.
- `src/frontend/IdentityPlatform.DesignTokens/`: canonical semantic CSS and framework-neutral theme assets linked into STS build/publish; future React imports the same source.
- `src/aspire/IdentityPlatform.AppHost/`: optional pilot composition with explicit bootstrap resource and no persistent test volume.
- `tests/backend/IdentityPlatform.Host.Tests/Features/`: deterministic policy, configuration, state/claim and safe diagnostics cases.
- `tests/aspire/IdentityPlatform.Issuer.Tests/Features/{Protocol,Browser}/`: one shared actual Aspire environment per selected provider, separate HTTP/browser contexts per case. A serialized xUnit collection owns shared persisted state; each mutating case restores its baseline in finally through fresh scopes. Failed cleanup invalidates/recreates the environment; outage runs last in a separate environment. Only explicit concurrency cases run simultaneous requests. Test relying party holds state/nonce/verifier and tokens server-side with independent public-key validation.
- `eng/test-matrix.json`, `scripts/verify.py`, `.github/workflows/verify.yml`: strict discovered/executed counts, child-process coverage and browser provisioning.

## Request and state design

`Pilot:Enabled=true` is accepted only in Development with complete issuer protection. Missing flag preserves health-only baseline. Explicit `--initialize-pilot` requires externally supplied username/password/client secret and one exact HTTPS redirect; managers create fixed-profile records in a transaction with bootstrap audit. A singleton versioned PilotManifest binds native user/application/scope IDs, exact username and callback. Existing native pilot records without that manifest, partial records or any descriptor/identity drift are conflicts. Validate exact confidential client/implicit first-party consent/callback/permission/requirement sets and scope/resource mapping; verify supplied hashes without updating them. Disabled/locked/role-bearing/MFA-enabled existing accounts cause a safe conflict, never repair. Current issuance is restricted to this owned pilot identity. Concurrent initialization has one complete winner; a loser fails without partial state and may be explicitly rerun. No public endpoint.

Register only authorization-code/query, `/connect/authorize`, `/connect/token`, `/connect/jwks` and discovery. Require PKCE globally and constrain CodeChallengeMethods to S256. Enable native authorization-request caching and five-minute request-token lifetime. Explicitly configure code60s/token5m lifetimes. Keep codes encrypted; issue signed access JWTs. Use supplied certificates only.

Read restored request via GetOpenIddictServerRequest and retrieve RequestTokenPrincipal from named server Authenticate properties. Its native token ID keys one durable interaction; never deserialize raw OAuth input into an independent protocol implementation. Cached requests ignore overlays. Validate max_age's original string syntax explicitly (nonnegative integer within supported range); parsed null must not silently accept malformed input.

Each interaction has a 256-bit opaque identifier and independent Secure/HttpOnly/SameSite=Lax host correlation cookie. Persist only its digest and original validated native request URI/client, plus five-minute expiry. At most eight live correlation cookies per browser are accepted; excess requests fail safely. No caller-provided return URL. Login GET/POST operates only on the persisted interaction and antiforgery-protected form. Native server authentication properties contain tokens and are never copied into browser tickets.

Use CheckPasswordSignInAsync with lockout-on-failure, explicit current IsEnabled/lockout/second-factor/role checks and a dummy password verification path for unknown/early-ineligible users. Generic error text and bounded inputs/rates avoid an obvious enumeration oracle; no timing-constant claim. Successful login atomically replaces the prior browser session, completes that interaction once and inserts audit intent; only then issue a fresh application cookie. Cookie uses a fresh ClaimsPrincipal (opaque subject/session/auth time only), secure host prefix, 15-minute non-sliding expiry and no remember-me.

Authorization accepts current eligible sessions. After per-interaction correlation-cookie possession is established, a Pending interaction may atomically transition directly to Consumed using an eligible existing session when no freshness requirement applies; this never fabricates credential-completion proof. prompt=login/max_age=0 require completion of that same interaction; completion cannot be inferred from another tab's cookie. Store/verify completed session and subject, and consume the interaction atomically before native sign-in. prompt=none returns login_required when interaction is necessary; consent/select_account requests fail explicitly. Failed completion/resume requires a new authorization request, without attempting to unconsume state.

Token handler authenticates the named OpenIddict server scheme and retains the validated code principal's native binding/nonce/authorization claims. Re-read current user/session/client/scope eligibility; check every stored code scope even when exchange omits scope. No browser-cookie substitution. Keep EF native token tracking intact through native redemption. Define destinations only for permitted public claims; private session/stamp/native claims never receive public destinations. One resource audience; no role/password/security-stamp leakage.

## Security, audit and operational boundaries

Application-owned bootstrap, password-attempt state, session replacement and interaction completion share a scoped database transaction with sanitized audit intent (fixed event, actor/target type and bounded opaque identifiers, fixed outcome, independent random correlation ID, UTC time; no username/password/body/token/OAuth state). Interaction consumption also includes intent in its conditional-update transaction. Identity lockout changes participate in that transaction. A failed transaction cannot emit an authenticated cookie. Native OpenIddict token/request/authorization state transitions remain library-owned; native replay mitigation may consume/revoke state even when later response processing fails. Never promise rollback on client cancellation, detach/reload tokens or retry a code exchange. Correlation/request-token creation is not an administrator configuration change.

Configure early fixed global concurrency/rate bounds and per-account Identity lockout; no unbounded IP partition cache. Do not trust forwarded headers in this pilot. Limit body/form/field lengths. Return safe terminal UI/protocol failure on storage faults; no fallback authentication. Deadline bounds application waits; native security mutations retain the library's cancellation semantics and database command bounds.

Suppress sensitive OpenIddict/ASP.NET request/auth/form and EF diagnostics with a PostConfigure logger filter guard that also defeats more-specific overrides/config reload. Existing traces remain health-only. Positive-control tests prove unrelated safe diagnostic events are still captured. Response policy includes no-store, no-referrer, frame-ancestors none, object/base restrictions and nonce-authorized same-origin UI assets. Browser form redirect behavior must be exercised; never relax to wildcard form-action.

## Verification and delivery

[Tasks](tasks.md) link LOGIN requirements to named test groups. Chromium on both providers blocks PR acceptance; Firefox/WebKit on both providers are explicit nightly/dependency matrix obligations with actual TLS trust. Never enable IgnoreHTTPSErrors or install browsers during test discovery. xUnit native MTP and outer child-process coverage retain per-module90% lines/80% branches. Add negative control/mutation cases for S256, interaction browser/session binding, current eligibility and native replay. Native code concurrency asserts exactly one winner, then replay failure.

Review deterministic light/dark desktop/mobile/error snapshots; test keyboard, no password echo, no browser token storage, generic errors, CSP, antiforgery and accessibility without blanket scanner exclusions. Qualified manual screen-reader/password-manager review remains recorded separately. Independent architecture/security/test/UX/Aspire reviews inspect actual evidence. All required checks use the final commit; update roadmap and PR without claiming whole M01 complete.

## Executable test policy

The pilot supports only client_secret_basic, explicitly constrained and asserted in discovery. Missing/wrong secrets, body-only credentials and conflicting Basic/body client identity are denied. Test RP uses Basic over trusted TLS; no public-client exception.

Expire application sessions/interactions by setting their persisted expiry in fresh test-only database scopes, combined with deterministic policy boundary tests. Native code expiry gets one real 60-second lifetime wait per provider, bounded by the test deadline; native cryptographic validation is not mocked. Authentication-age tests use stored original authentication times. No test clock or state mutation HTTP route enters the product.

Complete T017's relying-party fixture before T015 browser journeys. T013 assets can be reviewed independently, but US2 acceptance depends on the real callback/exchange fixture and US3 token handler. A shared fixed pilot account/client is never used concurrently across unrelated tests. Credential/registration mutations restore exact captured values through their owning managers, with cleanup failure terminating further use of that environment.
