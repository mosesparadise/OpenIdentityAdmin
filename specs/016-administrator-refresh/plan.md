# Implementation Plan: Administrator session refresh

**Branch**: `112-bff-refresh-plan` | **Date**: 2026-10-28 | **Spec**: [spec.md](spec.md)

## Summary

Add the approved M02-C capability to existing B3: server-held credentials, issuer-owned rolling refresh, durable two-node BFF coordination, no-reuse authorization invalidation and truthful logout cleanup. First deliver this planning packet, then characterize native integration before dependent runtime work. M02-D live API authorization, M02-E proxy/React and production enablement remain separate.

## Technical Context

- C#/.NET10 stable major-line SDK selection (`global.json`); baseline observed SDK10.0.403/runtime10.0.14. Use repository locks, not new upgrades.
- OpenIddict7.7.2, ASP.NET Core/EF10.0.14, existing Identity, Data Protection, native manager/dispatcher ownership and provider contracts.
- Platform stores issuer authority; Sessions stores browser tickets, protected credentials and refresh coordination. BFF retains Sessions-only privileges.
- Both real PostgreSQL and SQL Server; additive migrations in existing provider projects. No direct native-table business writes, replacement database or migration-history rewrite.
- Existing Aspire13.6.4/Kestrel/native test fixtures; Playwright1.63.0. A-009 exact Firefox/WebKit-certificate deferrals remain visible.
- Preserve 10-second overall request, five-second owned authority-operation bound and original remaining deadline through child scopes. No timeout extensions or blanket security-mutation retries.
- Initial refresh triggers at access expiry. Existing thirty-minute idle/eight-hour absolute caps and any shorter original source limits dominate. Current activity may advance only at successful owned issuance/local installation; authentication time never changes.

## Constitution Check

Pre-design: approved v0.2, existing roadmap continuation and this explicit next-item instruction authorize the planning packet. There is no new production, release or policy exception.

Post-design: native manager ownership, strict token secrecy, current authority, independent issuer concurrency, no cross-service transaction, project-level providers, exact history, both-provider real-boundary tests, atomic local audit and independent reviews are explicit. No critical review finding may be deferred into integration. Technical feasibility tests may fail; failure leaves dependent tasks open rather than relaxes the contract. A-009 applies only to its listed browser evidence.

## Project Structure

| Boundary | Planned location / ownership |
| --- | --- |
| Refresh coordinator and protected browser request | `src/backend/IdentityPlatform.Admin.Bff/Features/SessionRefresh/` with feature-local Setup |
| Native client integration | Existing `Features/Authentication/Protocol/` and `Ownership/` in BFF; extend native scope/deadline/assertion transport without weakening callback validation |
| Callback credential capture and logout | Existing `Features/BrowserSessions/` in BFF; explicit opt-in refresh profile and atomic handoff |
| Credential/operation persistence | `src/persistence/IdentityPlatform.Infrastructure/Features/Sessions/Refresh/`; narrow persistence operations using BFF-owned protection codec |
| Refresh current authority and family invalidation | Existing issuer `src/backend/IdentityPlatform.AuthServer/Features/Protocol/Administrator/` and Infrastructure `Features/Identity/ProtocolAdmission/`, `Features/ProtocolStorage/` |
| Fixed registration upgrade | `src/migrations/IdentityPlatform.Migrator/Features/AdministratorProtocol/Upgrade/`; native managers, exact old/new profile validation |
| Provider migrations | Existing `IdentityPlatform.Migrations.PostgreSql` and `IdentityPlatform.Migrations.SqlServer` Platform/Sessions directories and provider extension points |
| Tests | `tests/backend/IdentityPlatform.Host.Tests/Features/SessionRefresh/`, `tests/aspire/IdentityPlatform.Administrator.Tests/Features/SessionRefresh/`, existing `Features/BrowserSessions/`, `tests/persistence/IdentityPlatform.Persistence.Tests/Features/ProviderCompatibility/` |
| Contracts and CI | Existing `eng/contracts/`, `.github/workflows/`, discovery/evidence scripts and coverage register; register actual new inventory before claiming counts |

Paths marked new are planned, not existing capabilities. Reuse stable policy/transaction infrastructure; no generic repository, custom OAuth framework or project proliferation.

## Ordered delivery

1. T001–T002: this reviewed prose packet and exact documentation checks. No application code, configuration, generated contract or dependency change.
2. T003–T004: native feasibility plus detailed threat/scenario/ownership inventory. Run meaningful installed-library/real-provider controls and resolve all critical findings before integration.
3. T005–T007: additive storage/protection/provider-history controls and native profile upgrade. The feature remains dormant; existing profile behavior remains exact.
4. T008–T010 (US1) alongside the prerequisite-ordered T012–T014 (US2): issuer refresh authority/rolling issuance and callback credential capture, durable dispatch/fencing and replay/family invalidation. Tests precede each implementation; T014 depends on T009.
5. T011 follows completed T010/T013 for the protected demand boundary and contract tests; T015 follows T010/T013/T014 for two-node publication and actual process/wire failure. No usable refresh feature is accepted without these safety cases.
6. T016–T018 (US3): local logout/replacement invalidation, remote family cleanup, truthful UI and failure/concurrency tests.
7. T019–T022: genuine browser/TLS/operations, finite discovery/coverage/mutation, documentation/review and exact-head full acceptance before normal squash. All three stories are needed for coherent M02-C delivery.

Existing B3 local demo stays functional during preparatory commits. Do not enable new refresh profile by default while dependencies are partial. A final explicit Development local-demo opt-in may exercise accepted refresh without activating Admin API/React or production.

## Complexity and risk

The new boundary is two durable stores plus remote non-idempotent exchange. It cannot guarantee atomic commit or response delivery across them. The safe recovery choice is terminal local denial after possible dispatch with unknown result; a fresh sign-in is required. Revocation confirmation must handle a retained predecessor after an unknown successor, and a paused owner must never regain publication rights. [Data model](data-model.md) and [contract](contracts/refresh.md) specify the observable rules. T003 is a real stop condition for dependent integration if native ownership cannot meet them.
