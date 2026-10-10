# Implementation Plan: Explicit host and feature composition

**Branch**: `005-host-composition` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

## Summary

Extract startup wiring into explicit extensions and feature Setup classes for all six production entry points. Preserve observable behavior and resource ownership. The user confirmed the pattern and sequence; this is authorized structural work before feature 004 T026.

## Technical Context

.NET 10 / C#, existing EF Core 10/OpenIddict 7.7.1/Aspire 13.6.1 locks, xUnit.v3 + MTP, real PostgreSQL and SQL Server, Chromium Playwright. No new dependency, schema, configuration source, protocol, API or theme change. Four web hosts, one one-shot generic host and one local distributed host. Existing request/bootstrap/migration/CI deadlines remain unchanged. Handwritten composition remains in coverage; existing nine-module 90% line/80% branch floors remain.

## Constitution Check

Before research and after design: pass. Approval recorded in v0.2 and the current user decisions. Features retain ownership; OpenIddict/Identity own security mechanics. Both providers and real browser regression are required. No secret-bearing record serialization, temporary service provider, global mutable startup state, generic repository, new project references, certification claim or unapproved scope exception. Architecture, test and security/Aspire reviews apply. The main agent owns edits and integration.

## Project Structure

Keep the solution folders and grouped src/backend, src/aspire, src/migrations, src/persistence, src/core and src/frontend layout.

- Each web host: `Composition/WebApplicationExtensions.cs` exposes AddConfigurations, ConfigureServices, ConfigureApplication; `Features/Setup.cs` delegates explicit registration/mapping. Existing shared hosting/telemetry/health defaults are the AddConfigurations phase (no new configuration providers).
- Each web host: `Features/Storage/Setup.cs` owns its existing persistence/readiness registration. Auth/API/Worker use platform storage; BFF uses the separate session store.
- AuthServer: `Composition/StartupContext.cs` owns filtered arguments, initialization mode and externally loaded credentials. A `using` in Program surrounds the `await using` application. Extensions receive that context explicitly, with no service locator during registration. `Composition/DatabaseConfigurationException.cs` normalizes only the existing database registration failure boundary; existing issuer/pilot exception categories remain safe and distinct.
- AuthServer: `Features/Setup.cs` composes issuer protection and pilot activation; `Features/Issuer/Setup.cs`, `Features/Pilot/Setup.cs`, `Features/Accounts/Setup.cs`, `Features/Accounts/Login/Setup.cs`, `Features/Protocol/Setup.cs`, `Features/Protocol/Protection/Setup.cs`, and Authorize/Token Setup classes own feature registration/mapping. Retain existing leaf namespaces and Razor/asset paths. Existing security adapter helpers remain cohesive mechanisms. One authoritative pilot registration is shared by production and fixtures.
- Migrator: `Composition/HostApplicationExtensions.cs`, `Features/Setup.cs`, `Features/Database/Setup.cs` and local-role Setup validate local-only options, register persistence and execute the existing DatabaseUpgrade once with the three-minute deadline. No Host.Run and no HTTP pipeline. The existing LocalRuntimeCredentials object is passed through DI without new serialization or logging; its pre-existing type is unchanged.
- AppHost: `Composition/DistributedApplicationExtensions.cs` adapts builder configuration/resource setup; `Features/Setup.cs` composes `Features/Database/Setup.cs`, `Features/Services/Setup.cs` and existing `Features/Pilot/Setup.cs`. Small typed resource results carry graph references explicitly. No graph reconstruction or secret resolution at composition time. Build/Run remain visible in Program; no empty post-build ConfigureApplication shim.
- Tests remain in current Features folders; new composition lifetime tests go under `tests/backend/IdentityPlatform.Host.Tests/Features/Issuer/`.
- Feature documentation: spec, plan, research, data-model, contracts/startup, quickstart, tasks, quality checklist and verification under `specs/005-host-composition/`.

## Preservation contracts

See [contracts/startup.md](contracts/startup.md). In particular ConfigureApplication verifies issuer protection before initialization; initialization mode skips all HTTP mapping/middleware/assets. Pilot middleware remains routing → timeouts → request protection → safe status pages → limiter → authentication → authorization → protocol/accounts, followed by health. No temporary providers or additional loggers. Credentials stay alive through host disposal and are released on configuration/build/verification/bootstrap failure. Unexpected errors retain previous propagation instead of being mislabeled as invalid configuration.

## Verification and delivery

Run existing static/architecture/unit/process checks, add targeted missing lifetime/startup/graph assertions, then both complete provider suites including Chromium and aggregated child-process coverage in CI. Use existing fixtures for one-shot process evidence where possible; avoid adding a second database profile startup just to test composition. Record commands actually run and distinguish local constraints from CI evidence. Independently review the final diff. Publish a separate Conventional Commit PR; leave T026 pending and do not merge this new PR without user authorization.
