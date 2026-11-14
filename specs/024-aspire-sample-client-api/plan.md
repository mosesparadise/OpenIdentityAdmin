# Implementation Plan: Interactive sample client and API

**Branch**: `146-aspire-sample-client-api` | **Date**: 14 November 2026
**Spec**: [spec.md](spec.md)

## Summary and technical context

Implement two Development-only sample hosts in `src/samples/`, using .NET10,
OpenIddict7.7.2 client/validation and existing shared presentation assets. Manual
administrator registration remains authoritative; an authenticated Aspire dashboard
command accepts its client ID/one-time secret through secret input. Public sample
routes never accept configuration. The API validates signed JWTs offline with zero
clock skew; the explicit UserInfo action demonstrates current authority separately.

Use bounded process-local native state and server tickets. This single-process sample
does not advertise durability or scale-out, introduce a database provider, or access
Platform/adminsessions. Both real database profiles still supply issuer acceptance.
Independent architecture review accepted this bounded storage choice and its required
CAS/copy/capacity/expiry invariants. No dependency upgrade is planned.

## Constitution check

Approved continuation and product choices are recorded in docs/approval.md. Native
OpenIddict retains protocol/cryptographic ownership. Tokens stay server-side; state
consumption and current session ownership remain necessary before response publication.
Both providers, existing histories and fixed BFF fences stay intact. Sample-only volatile
storage is explicit and cannot be activated outside Development/local-demo. Existing
security/module floors remain and new sample modules join coverage. No exception to
production persistence, trust, review or release policy is introduced.

## Structure and ownership

- `src/samples/IdentityPlatform.Sample.Client/`: thin Program/Composition, Features/
  Configuration, State, Sessions, Authentication, Presentation and ResourceAccess.
- `src/samples/IdentityPlatform.Sample.Api/`: thin Program/Composition, Features/
  Validation and Orders. No client secret, private issuer key or platform DB.
- Sample shared code is source-linked only for immutable profile/trust definitions,
  not business handlers or shared mutable state.
- `src/aspire/IdentityPlatform.AppHost/Features/LocalDemo/`: sample resources and an
  authenticated configuration command; existing native Docker stdin/process ownership.
- `scripts/local-demo/Dockerfile`: explicit non-root client/API stages and ordinary
  certificate trust. No custom user launch script.
- `tests/backend/IdentityPlatform.Host.Tests/Features/Samples/`: native store/session/
  transport and host negatives. `tests/aspire/IdentityPlatform.AppHost.Tests/
  Features/Samples/`: real managed registration/consent/native client/resource journey.
  AppHost tests prove command/resource/run ownership and cleanup.

## Decisions

1. Separate sample HTTPS origins on loopback: client sample.dev.localhost:17446 and
   resource orders.dev.localhost:17447; issuer remains issuer.dev.localhost:17443.
   Exact callback /callback; audience orders-api; API scope orders.read; identity
   disclosure scope sample.email. These are documented values created in Admin UI.
2. Configuration is one-shot per client process/run: dashboard input is bounded JSON
   sent only over Docker stdin to the inspected run/role/container ID. A fixed-path
   private tmpfs file is atomically created without overwrite. Only clientId/secret
   are input; issuer/API/callback are fixed. No argv/env/log secret. Uncertain outcome
   requires fresh run. Client serves waiting screen before configuration; authentication
   is lazily composed only after its immutable snapshot is available.
3. Native reference-state storage uses an IOpenIddictTokenStore adapter with detached
   entities and CAS. The native client owns encryption/signing, issuance and redemption;
   no custom token format, store-disable mode or native expiry override.
4. Native client uses client_secret_basic, code/S256/query, explicit openid/orders.read/
   sample.email scopes and DisablePushedAuthorizationRequests=true. Validate metadata
   destinations before JWKS or credential-bearing requests; no redirects/retries.
   Native optional automatic UserInfo is disabled; explicit profile action uses a
   fixed, bounded HTTPS GET and strict JSON/sub matching because pinned OpenIddict's
   standalone SendUserInfoRequestAsync API is internal. Do not reflect private APIs.
5. Opaque sample cookie and server ticket bound to original access-token expiry, no
   refresh/sliding extension. Login-attempt/browser binding and terminal logout prevent
   a late callback from resurrecting a logged-out browser. Per-session operations
   synchronize final publication against logout; no session recreation from stale renewal.
6. API accepts header bearer only, native RS256/issuer/type/audience/lifetime validation,
   then exact orders.read scope. Returns fixed synthetic orders. No live status checks:
   a revoked grant may remain acceptable until expiry, while UserInfo denies current state.
7. Render finite encoded fields with shared light/dark assets and strict CSP/no-store/
   referrer policy. POST actions require exact Origin and native antiforgery. Bound
   request bodies, headers, token/profile size, concurrency, store count and lifetimes.

## Implementation and verification

See tasks/data-model/contracts. Implement foundational controls with meaningful native
store/session tests before authentication. Validate end-to-end native callback behavior,
concurrent redemption, logout publication races, metadata substitution, secret handling
and startup failure. Use both providers and retained Chromium/WebKit; do not retry
unexplained failures or combine runs into acceptance. Review exact source then original
CI reports/coverage/mutations. All historical/manual/reliability/release gates remain open.

## Complexity

Two small separate hosts are necessary to demonstrate actual client/resource boundaries.
No generic repository, new provider switch, extra frontend framework or copied Admin
BFF ownership stack. Keep methods/classes within existing reviewed thresholds.
