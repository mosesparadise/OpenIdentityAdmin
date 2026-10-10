# Implementation Plan: Reproducible Platform Foundation

**Branch**: `001-platform-foundation` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

## Summary

Implement the independent infrastructure portion of approved milestone 001: locked solution, native Identity/OpenIddict persistence, separate provider migrations, dedicated Migrator, safe ServiceDefaults and Aspire provider profiles. Prove configuration/persistence with real-provider and distributed tests. This does not accept the issuer or complete later management/browser features.

## Technical Context

- Language/runtime: C#/.NET SDK 10.0.401, runtime/EF/Identity 10.0.12.
- Dependencies: OpenIddict 7.7.1, Npgsql EF 10.0.3, Aspire 13.6.1, xUnit v3 4.0.2/MTP 2.5.1, Testcontainers 4.16.0. Resolve/pin the complete compatible graph.
- Storage: PostgreSQL 18 and SQL Server 2022, exact tested container digests; separate platform/BFF contexts and migration histories.
- Tests: unit, architecture, native manager/persistence against both real databases and Aspire.Hosting.Testing. No InMemory/SQLite substitution.
- Platform: Linux/.NET and a Docker-compatible runtime. Local Aspire does not select production hosting.
- Performance: healthy or explicit failure within 180 seconds after images are available. Product load targets remain unmeasured.
- Scope: configuration, native storage, migrator, health-only host shells, worker, AppHost, build/review/test infrastructure. No fake login, seeded tokens or public management stubs.

## Constitution Check

Approval is recorded; feature boundaries, equal provider support, meaningful tests, safe secrets, operability and locked dependencies are included. No protocol bypass is introduced. A-001 permits implementation milestones to proceed while Basic OP suite compatibility remains an external certification dependency. This infrastructure slice alone does not pass full milestone 001. No exception to security settings or public-release gates is required for this scope.

## Project Structure

Core/Features/Configuration; Infrastructure/Features/Identity/Persistence and Persistence contexts; separate Migrations.PostgreSql/Migrations.SqlServer; Migrator/Features/Database/Upgrade; ServiceDefaults; AuthServer, Admin.Api, Admin.Bff and Worker composition roots; AppHost. All use the IdentityPlatform prefix under src/. Matching Core.Tests, Persistence.Tests, Architecture.Tests Host.Tests and AppHost.Tests live under tests/. Composition/generated artefacts are the approved structural exceptions to Features.

## Complexity Tracking

No constitution exception. Two migration assemblies are required to verify both production providers rather than assuming provider-neutral DDL.

## Authorized tooling/layout update

Execute FND-009/010 on this branch and PR. Place AppHost/ServiceDefaults in src/aspire; four runtime hosts in src/backend; Core in src/core; provider migration assemblies and Migrator in src/migrations; Infrastructure in src/persistence. Reserve src/frontend with a scope README, mirrored as a solution item. Group tests under aspire/backend/core/persistence/architecture. Keep project/assembly names stable.

Use xunit.v3.mtp-v2 4.0.2 (the xUnit v3 product's current stable package version), MTP 2.5.1 and Microsoft.Testing.Extensions.TrxReport 2.5.1. Configure global.json native runner and executable test projects. Replace VSTest command switches with MTP options, preserving provider selection, deadlines, strict reports and external dotnet-coverage child instrumentation. Validate actual runner help and selected cases before treating matrix results as complete. Remove obsolete license exceptions only after inspecting the resolved graph.

Sequence: record requirements/research/tasks; move projects and update graph/solution; migrate dependencies/runner; build and adapt tests for v3; execute all matrix cells/coverage/supply chain; independent review; document evidence and update PR. Approved-v0.2 snapshots and earlier evidence remain immutable.
