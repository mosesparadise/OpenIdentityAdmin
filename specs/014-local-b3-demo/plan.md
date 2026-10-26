# Implementation Plan: Disposable local administrator demo

**Branch**: `108-local-b3-demo` | **Date**: 2026-10-26 | **Spec**: [spec.md](spec.md)

## Summary

Expose the accepted administrator browser slice through an explicit Development profile controlled by Windows PowerShell and Aspire with Docker Desktop Linux containers. Keep native Identity/OpenIddict, protection, storage and ceremony implementations. Default startup remains unchanged.

## Technical Context

**Language/Version**: C#/.NET SDK 10.0.401, runtime 10.0.12; PowerShell 7.4 or later.
**Primary Dependencies**: pinned Aspire 13.6.1, OpenIddict 7.7.1, EF Core 10.0.12.
**Storage**: existing PostgreSQL/SQL Server provider adapters; disposable containers; private Linux tmpfs protection files.
**Testing**: existing xUnit MTP Host/AppHost suites, native both-provider tests and Chromium/WebKit browser matrix; unchanged floors and mutations.
**Target Platform**: Windows Docker Desktop Linux containers; Linux CI provides separate executable evidence.
**Project Type**: local distributed identity application.
**Performance Goals**: existing bounded request/transaction/readiness deadlines; no retries of mutations.
**Constraints**: no WSL terminal, no bootstrap secrets in args/configuration, no insecure TLS, no persistent volumes, no fixture authentication.
**Scale/Scope**: one disposable local run; fixed issuer/BFF identities and one initialized administrator.

## Constitution Check

Before implementation: development approval is recorded; security/library/provider invariants preserved; feature-local composition, safe diagnostics, test/reviewer traceability required. No new domain schema or production activation. Source/required evidence remain unaccepted until execution. After design: supported host APIs only; no private ASP.NET host property workaround; genuine redirected stdin bootstrap and normal certificate validation mandatory. Architecture, test, security and Aspire independent reviews required.

## Project Structure

- `specs/014-local-b3-demo/`: specification, research, contracts, tasks, quickstart and verification.
- `src/core/IdentityPlatform.Core/Features/`: pure local profile policy where shared.
- `src/persistence/IdentityPlatform.Infrastructure/Features/`: shared private material adapter where reused.
- `src/backend/IdentityPlatform.AuthServer/Features/`: explicit local B3 activation.
- `src/backend/IdentityPlatform.Admin.Bff/Features/`: explicit local native client/protection owner and middleware composition.
- `src/aspire/IdentityPlatform.AppHost/Features/`: container resource graph, ephemeral material and least-privilege wiring.
- `scripts/`: Windows initialization entry and Linux public registration helper.
- `tests/backend/IdentityPlatform.Host.Tests/`, `tests/aspire/`: normal runtime, configuration, filesystem, graph, native and browser controls.

## Complexity Tracking

No constitution exception requested. Independent architecture review selected one generic BFF host for enabled and disabled modes: existing owned B3 middleware, explicit health endpoints, inert unknown paths, no global authentication middleware. Existing WebApplicationBuilder service wrappers remain for contract fixtures.

## Native CI partition

Observed SQL Server aggregate runtime exceeded the existing 660-second invocation after adding the demo to Foundation. Schedule the new scenario in its own mandatory `LocalDemo` group for both providers. Preserve Foundation8, BffSessions10, LocalDemo1, exact complete discovery/selection, nine-minute scenario, 660-second invocation and 20-minute job budgets. Require both additional coverage inputs; no case or existing floor is removed.
