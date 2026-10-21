# Implementation Plan: Administrator browser sessions (B3)

**Branch**: `068-administrator-browser-plan` | **Date**: 21 October 2026, Europe/London | **Spec**: [spec.md](spec.md)

**Input**: Feature 011 T027 and the approved M02-B roadmap. Planning only; all executable tasks remain unperformed.

## Summary

Build the browser lifecycle on delivered B1 storage and B2 native protocol ownership. A separate short-lived browser login intent precedes a fresh B2 challenge; a fixed-client signed session-binding contract supplies B1's original source deadline. Durable Sessions-owned ordering prevents stale establishment after logout/replacement. Product cookie/CSRF/status routes and local logout are followed by a native issuer-assisted logout dependency. Full B3 acceptance includes both logout boundaries; later refresh, live API authorization and React remain separate.

## Technical Context

**Language/Version**: Existing C#/.NET 10 SDK 10.0.401, pinned repository versions; no upgrade in this packet.

**Primary Dependencies**: Existing ASP.NET Core Identity/authentication/antiforgery/Data Protection, OpenIddict 7.7.1 native server/client, EF Core provider registry and Aspire test harness.

**Storage**: Issuer Platform and BFF Sessions, separate least-privilege runtime owners. Additive PostgreSQL/SQL Server migrations in existing assemblies; no database recreation or migration-history changes.

**Testing**: Existing native test infrastructure, real provider fixtures, Playwright Chromium/Firefox/WebKit, contract/theme/accessibility, concurrency/failure/mutation and raw evidence verification. No required test is replaced by a direct fabricated principal.

**Target Platform**: Existing Linux/HTTPS service composition; trusted disposable certificates for tests.

**Project Type**: Existing backend BFF and issuer features, no new generic provider or protocol framework.

**Performance Goals**: Preserve existing 10-second request, five-second authority-operation and bounded backchannel controls. Login intent maximum 300 seconds; finite storage/cleanup bounds in [data model](data-model.md). This is not a throughput or production capacity claim.

**Constraints**: Firefox T028 before B3 executable work; explicit native feasibility before integration; product decisions in [spec](spec.md) before dependent work; no blanket retry, cross-service transaction, browser OAuth credentials or BFF Platform access.

**Scale/Scope**: One fixed administrator client, one exact HTTPS callback, one fixed resume destination, one exact registered post-logout return. Current B2 noninteractive flows and pilot remain isolated.

## Constitution Check

Pre-research: approved v0.2 and feature 011 T027 authorize this planning boundary. Source review and design cause no runtime activation or external declarations. Existing gates do not prevent writing/reviewing the packet.

Post-design: native protocol ownership, explicit named schemes, Sessions-only BFF privileges, provider portability, real-provider evidence, unchanged deadlines/floors, secret handling, traceability and independent reviews are required below. Feature-local composition preserves thin Programs. No constitution exception is proposed. Unproven native hooks/atomic composition are blocking implementation prerequisites, not waived requirements. Human/external controls remain open.

## Project Structure

### Documentation (this feature)

`spec.md`, `research.md`, `plan.md`, `data-model.md`, `contracts/browser-session.md`, `tasks.md`, `quickstart.md`, `verification.md`, `checklists/requirements.md` and `checklists/security-boundaries.md` form the planning packet.

### Source Code (repository root; planned locations)

| Boundary | Planned location and responsibility |
| --- | --- |
| Browser login/callback/status/logout | `src/backend/IdentityPlatform.Admin.Bff/Features/BrowserSessions/` with local Setup and explicit feature composition |
| Native client integration | Existing `src/backend/IdentityPlatform.Admin.Bff/Features/Authentication/Protocol/`; add constrained B3 bindings, preserve B2 allowlist for unbound flows |
| Canonical ticket adapter | Existing `src/backend/IdentityPlatform.Admin.Bff/Features/Sessions/`; reuse B1 policy/codec/store, prove atomic participation |
| Issuer continuation/end-session | `src/backend/IdentityPlatform.AuthServer/Features/Protocol/Administrator/` and `Features/Accounts/` with explicit administrator journey integration |
| Owned persistence | `src/persistence/IdentityPlatform.Infrastructure/Features/Sessions/` and `Features/Identity/`; native manager/context participation, bounded portable concurrency |
| Schema | Existing `src/migrations/IdentityPlatform.Migrations.PostgreSql/` and `IdentityPlatform.Migrations.SqlServer/` Platform/Sessions directories |
| Contracts | Existing contract tooling under `eng/contracts/`, new BFF browser contract owned by its feature; native protocol routes stay separate |
| Verification | `tests/backend/IdentityPlatform.Host.Tests/Features/BrowserSessions/`, existing session tests, and `tests/aspire/IdentityPlatform.Administrator.Tests/Features/BrowserSessions/`; inventory actual new paths/identities before claiming counts |

No broad shared abstraction or duplicated OpenIddict implementation is justified. Provider-neutral behavior stays in existing contracts; each provider owns necessary schema/locking implementation through the delivered extension points.

## Ordered delivery

1. Complete and normally deliver this reviewed planning packet as feature 011 T027. It contains no B3 code.
2. Complete feature 011 T028: restore Firefox and its coverage inputs, resolve/review the actual history-evidence obligation and run both providers/all engines. Keep unrelated historical findings and human/public-release gates open. Do not start B3 executable work before this gate.
3. Inventory pinned native event order and prove intent preservation, signed binding validation, end-session handling and B1/shared Sessions transaction feasibility. Publish concrete source/test mappings and reviewed dispositions; an infeasible adapter blocks integration.
4. Implement issuer intent/ceremony binding and fixed-client claims with native extensions; preserve exact B2 behavior for ordinary noninteractive requests.
5. Implement Sessions coordination, canonical ticket adapter, product endpoint ownership and cookie/CSRF/status/local logout. Establishment and logout need the durable winning-generation checks before any product cookie is usable.
6. Implement the native issuer end-session dependency and safe return handling. Local outcomes remain independently truthful; no token or credential enters browser-visible logout transport.
7. Complete both-provider/all-engine acceptance, failure/mutation/coverage/evidence reviews, then normal exact-head squash. Keep activation and M02-C/D/E external dependencies visible.

Stories are independently testable but share security contracts. No product enablement occurs from a partial story. Native feasibility, data ownership and scenario inventory precede source work that depends on them; root owns integration and review dispositions.

## Design references and limits

[Research](research.md) records source gaps and alternatives. [Contract](contracts/browser-session.md) defines observable behavior and failure ownership. [Data model](data-model.md) defines transitions, quotas and retention. [Tasks](tasks.md) and [verification](verification.md) map requirements to planned evidence. [Quickstart](quickstart.md) separates available prose checks from future runtime validation.

The accepted B2 release of `sub`/`sid`/`auth_time`/`amr` is not sufficient to create a correct B1 ticket because original source expiry is private. The proposed B3-only claim extension needs explicit review; no existing callback test proves it. Similarly, B1 CAS is not an existing callback/logout transaction and account logout is not an existing end-session endpoint. No table or checklist in this packet asserts otherwise.
