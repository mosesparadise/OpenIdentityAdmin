# Implementation plan: Administrator foundation

**Branch**: 021-administrator-mfa-design | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

## Summary

Deliver M02-A as three user-approved sequential reviewed squash PRs. The first implements explicit protected bootstrap and dormant permission/governance storage. Later PRs implement TOTP/restricted recovery and current-permission/last-admin enforcement. No administrator API/BFF/docs exposure occurs here.

## Technical context

Use the existing locked .NET10/EF10/ASP.NET Core Identity/OpenIddict stack, both existing PostgreSQL/SQL Server provider migrations, xUnit.v3/MTP and Aspire. No new production host, mediator, generic repository or package upgrade. Runtime bootstrap uses the existing platform persistence managers and a single transaction, with a portable governance UPDATE lock. Private input is bounded redirected stdin. See [research](research.md), [data model](data-model.md) and [command contract](contracts/bootstrap.md).

## Constitution check

Approved v0.2 authorizes routine implementation; user clarified sequencing/MFA/structure on12October2026. Features/Setup and thin Programs remain. Native managers own credentials and role mutation; both providers and sanitized atomic audit are mandatory. Initial account is disabled/pending; no security claims arise from schema or role labels. Independent architecture/security/test review is required before source acceptance. Existing coverage, test deadlines and certification/vendor/manual gates remain unchanged. No constitutional exception proposed.

## Structure

- src/core/IdentityPlatform.Core/Features/Identity/Permissions: finite permission definitions and v1 bundles.
- src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Persistence: governance/receipt/enrollment model and configuration.
- src/migrations/IdentityPlatform.Migrator/Features/Bootstrap/Initialize: input contract, bootstrap orchestration and Setup.
- Existing Migrator Composition/Features Setup: explicit operation dispatch; upgrade remains default.
- Existing provider migration projects and SchemaCatalog: additive schema and readiness.
- tests/aspire/IdentityPlatform.Administrator.Tests/Features/Bootstrap and Fixtures: isolated both-provider actual-process/manager/transaction evidence.
- Existing host/core/architecture tests: pure parsing/catalogue/configuration/composition invariants where useful.

## Delivery boundaries

Increment1 supplies an initialization record and pending account, not a login ceremony. Acceptance requires actual command invocation, true Identity password verification, provider race/rollback/restart evidence, no-op after later account changes and no pilot bypass. No authenticator material is generated prematurely. Increment2 begins only after increment1's normal merge and its own detailed interaction/secret-protection design. Increment3 cannot claim unimplemented management endpoints or five-second BFF/API enforcement.

## Validation

See [test strategy](test-strategy.md) and [quickstart](quickstart.md). Strict selection must isolate Administrator from ordinary issuer invocations, require every expected test identity, and add its provider coverage artifacts to the mandatory merge set. Keep previous invocation limits and production module floors. Review seeded missing-case/substitution failures and regression tests for all reproduced defects.

## Second-increment design checkpoint

[MFA design](mfa-design.md) specifies the interaction/state model, native protected-store integration, exact limits, distributed replay/lock order, session invalidation, recovery loss handling, cleanup/backpressure and requirement-linked evidence. This is T016 documentation, not account implementation. A documentation checkpoint does not change the three approved implementation increments. No new production project or external package is proposed.

The current constitution is 1.1.0. All new/changed implementation must follow the principal-engineer rules, meaningful XML/public examples, Given_When_Then tests, explicit values and 40/300 review thresholds. Legacy debt remains feature 009 T013–T016. No security exception or scope amendment is proposed.

## Open work before implementation acceptance

Execute T017/T018 only after T016 design convergence, with actual native managers, both providers and all three browser engines. The detailed work packets in tasks.md remain unchecked until verified. Complete the usable-admin predicate and supported exception route in T019 before increment3. All-factor-loss emergency recovery is unavailable; adding a reset route requires its own explicit approval.
