# M01-F decisions for confirmation

Status: both recommended decisions explicitly confirmed by the user on 12 October 2026. Existing v0.2 approval remains active. These questions implement the user's instruction to confirm new structure/pattern choices, not a new general approval requirement.

## 1. Deliver tooling before administrator authentication

**Recommendation:** implement the bounded foundation described in [spec.md](spec.md). Keep `/docs/*`, documentation assets and `/openapi/*` unmapped in shipped Admin.Api/Admin.Bff in every environment. No boolean setting or Development mode alone opens them. Keep actual management endpoint inventories empty until real features exist.

Why: M02-A–D must first provide privileged bootstrap/MFA, a real BFF session with server-held tokens, refresh/CSRF/origin protections and independent current-permission checks in the API. Those are absent today. Building temporary authentication here would duplicate or compromise that work.

What this delivers now:

- Explicit versioning and native document-generation infrastructure in the existing hosts, with protocol/health routes excluded.
- Deterministic actual-host inventories plus nonempty test-only representative contracts, each clearly identified.
- Contract validation, meaningful seeded compatibility failures and generated TypeScript compilation/request tests through a mandatory test-only runtime consumer against the synthetic HTTP fixture.
- Scalar and Swagger rendering components verified against the same representative contracts, read-only, locally served and themed.
- CI integration and independent review, without claiming a usable management SDK or administrator experience.

What remains pending:

- Actual authenticated same-origin BFF documentation/proxy, server-held upstream authorization, expired/revoked-session behavior and independent API denials: M02-A–D prerequisite and M01-F follow-through.
- Actual browser contract/client used by React: M02-E, with real resource operations in M04-D.
- Real management permission, ETag/idempotency/audit behavior: the milestone that implements each resource, not a fixture claim.
- Full FR-16/NFR-14 contract acceptance: remains partial until the real paths pass the approved gates.

Alternative: implement M02 administrator security before returning to contract tooling. This changes the next-item order and makes this increment much larger, but avoids any temporary fixture-only rendering work. Neither option permits anonymous production docs or bearer tokens in React.

## 2. Preserve projects and add bounded feature/test locations

**Recommendation:** use these locations, following the already approved Composition and explicit Setup conventions:

```text
src/backend/IdentityPlatform.Admin.Api/
  Features/ApiContracts/Setup.cs       # versioning, generation, export metadata
  Features/Documentation/Setup.cs      # explicit renderer composition; not mapped yet
src/backend/IdentityPlatform.Admin.Bff/
  Features/ApiContracts/Setup.cs       # browser-owned versions/contracts
  Features/Documentation/Setup.cs      # future protected docs composition
src/frontend/IdentityPlatform.Admin.Contracts/
  package.json, package-lock.json     # private tooling/consumer package; no React shell
  generated/                         # actual BFF output only; initially no resource API
  tests/                             # explicitly synthetic generated consumer fixtures
  README.md                          # provenance and incomplete product acceptance
tests/contracts/IdentityPlatform.Api.Contracts.Tests/
  Features/Versioning/
  Features/Generation/
  Features/Compatibility/
  Features/Documentation/
  Fixtures/                          # non-shipped endpoint/server/schema fixtures
```

The solution groups the test project under `/tests/contracts/` and frontend files under the existing `/src/frontend/`. No new production host, shared transport library, Core OpenAPI dependency or schema migration is proposed. Add subfolders only when they contain real responsibilities. Mainstream package configuration stays in `Directory.Packages.props`; existing `scripts/` and workflow own verification entry points.

The new test project uses xUnit v3/MTP and covers the production registration/generation components, not an independent imitation. Existing host/Aspire tests assert actual shipped routes remain absent on both providers. Test endpoint types are never referenced by production projects. The frontend package is private and is not published as a completed SDK. Fixture generated files never occupy its production `generated/` location.

**Implementation sequence:** both decisions are confirmed; finish and review the Spec Kit plan/contracts/tasks, then implement the approved scope. Existing approval covers routine package and test details once the structure/scope are confirmed; material security changes would require a separately documented decision.
