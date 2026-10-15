# Implementation plan: Durable administrator session foundation

**Branch**: `049-bff-session-foundation` | **Date**: 15 October 2026 | **Spec**: [spec.md](spec.md)

## Summary

Feature 010 is the first internal M02-B slice: a durable protected ticket store with expiry, concurrency, revocation and bounded cleanup. Keep browser/management routes disabled. This PR delivers the design and task packet only; runtime tasks remain unchecked until actual evidence exists.

## Technical context

Use the pinned .NET 10/EF Core 10/ASP.NET Core and xUnit v3/MTP stack, both existing provider migration projects, Aspire and strict CI inventories. No dependency upgrade or new production project is needed for the store. Future OpenIddict client/PAR integration needs compatible locked-package evidence.

Use native `AuthenticationTicket`/`TicketSerializer`, not an alternate claims/token format. Protect the serialized ticket and binding/deadline/revision envelope through a dedicated BFF Data Protection boundary. [Data model](data-model.md) and [internal contract](contracts/ticket-store.md) define limits and failure behavior.

Multiple BFF instances share the selected session database and BFF ring. There is no throughput or five-second API-enforcement claim. Retain the existing 30-second database command limit and propagate cancellation. Cleanup handles at most 100 eligible rows per invocation. Preserve existing test execution deadlines; if a new partition is necessary, review its explicit bounded selection and mandatory coverage before execution.

## Constitution check

No exception is proposed. Current user authority advances M02-B preparation within approved v0.2. Planning acceptance does not declare runtime or activation tasks complete.

- Keep feature-local ownership and the existing hierarchy. BFF cannot reference AuthServer handlers or acquire platform business-write privileges.
- Extract only stable key-ring validation/loading mechanics into narrow Infrastructure code when reuse is necessary; preserve issuer behavior/negative controls. Do not duplicate that subsystem or share configured rings/application names.
- Both providers, additive migrations, least-privilege grants, cancellation, races, atomic audit intent and failure behavior accompany implementation.
- New code must meet documentation, test naming and size rules. Never renew/copy the expired witness allowance.
- Exact-source applicable CI and independent review precede normal squash. This planning-only change receives the existing prose checks and supplies no product execution/coverage credit.
- Historical findings, human assessment and activation/public-release gates stay open. No expired diagnostic approval is revived.

Recheck these principles against the actual implementation diff before its acceptance.

## Project structure

| Existing path | Planned responsibility |
| --- | --- |
| `src/backend/IdentityPlatform.Admin.Bff/Features/Sessions/` | Native serialization, handle lifecycle, BFF protection/configuration and orchestration; internal registration |
| `src/persistence/IdentityPlatform.Infrastructure/Features/Sessions/Persistence/` | Ticket/audit entities, mappings and narrow persistence operations |
| `src/persistence/IdentityPlatform.Infrastructure/Features/Protection/` | Stable shared protection mechanics only, if extracted; separate caller-owned credentials/purpose |
| Existing AuthServer issuer-protection features | Preserve behavior/configuration and controls when adapting to shared mechanics |
| Existing provider migration projects under `Sessions/` | Additive ticket/audit migrations and snapshots; preserve native client-state tables |
| Existing Migrator and `Infrastructure/Features/Health/SchemaCatalog.cs` | Explicit upgrade/readiness and session-runtime grants |
| `tests/backend/IdentityPlatform.Host.Tests/Features/BffSessions/` | Bounds, native serialization, protection/configuration and route isolation |
| `tests/persistence/IdentityPlatform.Persistence.Tests/Features/BffSessions/` | Provider migration/model/grant contracts |
| `tests/aspire/IdentityPlatform.AppHost.Tests/Features/BffSessions/` | Real provider, restart/two-instance, concurrency and failure behavior |
| `eng/test-matrix.json` and existing verification/coverage configuration | Exact inventory and mandatory coverage inputs; no avoidance exclusions |

No new test project is assumed. Reuse existing fixture ownership. Synthetic test probes must remain in test code and never appear in production routes.

## Delivery sequence

1. Deliver this reviewed specification/dependency packet as a prose-only PR.
2. Implement the complete store slice, including key ownership, both migrations, atomic intent, failure/race regressions and operations guidance; demonstrate unchanged production route inventory.
3. Resolve the executable-delivery dependency in [research](research.md). PR #50 is not a BFF code dependency, but its exception retirement is a shared quality-gate dependency. Do not merge executable work around the failed gate.
4. Prepare distinct administrator issuer/client and browser integration from the later packages below. They inherit no runtime acceptance from this store.

## Later M02-B–E work packages

| Package | Concrete deliverable | Acceptance dependency |
| --- | --- | --- |
| B2 — Administrator issuer/client | Distinct current account/session/MFA admission; fixed manager-backed client/scope/audience registration; native code/S256/PAR and `private_key_jwt` integration | Prove pinned library support and both-provider denial/replay matrix; never broaden pilot eligibility. Pull only the required fixed protocol subset forward from M03/M04; catalogue UI is not prerequisite. |
| B3 — Browser session journey | Login, exact callback, durable native client state, secure host cookie, fresh handle, local and accurately bounded remote logout | Accepted B1/B2; route-specific CSRF/correlation/nonce/redirect/TLS checks, safe themed error states and real three-engine evidence |
| C — Refresh/request boundary | Two-node serialization, no-reuse replay policy, response loss, same-origin/CSRF and fixed proxy/header rules | Native issuer refresh/revocation and live eligibility; no browser tokens. Ticket CAS is not OAuth refresh serialization. |
| D — Independent Admin API | Remote issuer/audience/type plus token/authorization-store validation and current account/session/permission checks | Measured both-provider, cross-instance five-second enforcement, fail-closed storage and no direct-HTTP bypass |
| E — React shell | Generated BFF browser contract/client and same-origin Aspire UI | Accepted BFF/API boundaries, real browser-to-issuer journeys and no browser token storage |

CSRF/fixation/origin protection accompanies the first route that needs it; placement under M02-C does not permit an unprotected B3 route. Local logout commits ticket invalidation even when remote logout fails and reports the remaining consequence accurately. Refresh issuance stays disabled until its safe lifecycle exists. Missing remote logout remains open M02-B work, not a completed feature claim.

## Verification

[Quickstart](quickstart.md) gives prerequisites/evidence capture. [Tasks](tasks.md) maps requirements to source and tests. [Verification](verification.md) separates planning checks from unexecuted runtime acceptance. Independent architecture/test/security review applies to both packet and source.
