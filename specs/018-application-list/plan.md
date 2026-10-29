# Implementation Plan: Protected application list

**Branch**: `121-protected-application-list` | **Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

## Summary

Deliver the next real user journey: Applications in the existing Aspire workspace, backed by independent Admin API authorization and actual native application records. Retain only the exact callback access credential in protected BFF ticket state; do not activate refresh or invent application data.

## Technical Context

**Language/Version**: .NET 10/C#; existing React/TypeScript stack.
**Primary Dependencies**: Locked OpenIddict 7.7.2 Validation/SystemNetHttp/AspNetCore; existing EF Core, Asp.Versioning, native client, generated browser client and shared tokens.
**Storage**: Existing Platform and AdminSession contexts through registered relational providers; PostgreSQL and SQL Server. No schema change is expected for protected ticket token properties or native catalogue reads.
**Testing**: Native MTP/xUnit, real provider/Aspire suites, Chromium/WebKit within A-009, frontend tests, contract generation and unchanged CI coverage/mutations.
**Target Platform**: Native Development-only Aspire Linux containers; Windows Docker Desktop user entry remains the supported target with actual Windows acceptance open.
**Project Type**: Browser/BFF/API vertical slice.
**Performance Goals**: Each accepted API read fits an original five-second authority budget including validation, lock waits, query and cleanup. Collection sizes default25/max100; bounded query and response payloads.
**Constraints**: Separate named native validation; server-held credentials only; no automatic retries/refresh; both providers; fail closed; fixed HTTPS forwarding; unchanged prior identities/coverage.
**Scale/Scope**: One collection endpoint per API surface and one Applications screen; 5,000-record representative catalogue is PRD scale, not a benchmark claim until measured.

## Constitution Check

Approved visible-first sequencing and current user continuation cover M02-D/M04 read dependencies. Architecture already specifies remote discovery plus native token/authorization entry validation. Production and external declarations remain unauthorized. Features stay within existing projects, native managers retain protocol ownership, no schema/history rewriting, real both-provider tests and relevant independent reviews required. No exception or new policy waiver is proposed. Post-design gates: independent architecture/security, test and UX source reviews pass. Implementation and scoped hosted acceptance are delivered in PR #118; verification records exact mixed-attempt provenance and remaining external gates.

## Project Structure

- `specs/018-application-list/`: specification, research, data model, contracts, tasks, quickstart, verification/checklists.
- `src/backend/IdentityPlatform.Admin.Api/Features/Authentication/` and `Features/Applications/`: explicit activation, native bearer validation/current policy, bounded versioned collection.
- `src/persistence/IdentityPlatform.Infrastructure/Features/Identity/`: narrow rollback-only authority ownership reusing existing policy/lock/grant helpers; no issuance proof or factor decryption in API.
- `src/backend/IdentityPlatform.Admin.Bff/Features/Applications/`: exact fixed-target forwarding and browser projection; existing BrowserSessions retains native callback provenance and protected ticket creation.
- `src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/`: generated client, request cancellation/state and accessible list/search/paging.
- Existing AppHost LocalDemo and Dockerfile: separately hosted API, trusted TLS and least-privilege Platform connection.
- Corresponding Host/Persistence/Administrator/AppHost/Contract/frontend tests and exact inventories.

## Security and ownership design

Native OpenIddict validates the configured issuer, signature, lifetime and API audience, with token and authorization entry checks and disabled entity caching. Require exact administrator scope/presenter and canonical subject/session bindings; an ID token or caller identity header grants nothing.

Current product policy uses a fresh child context with governance → profile → account locks in an explicitly read-only ceremony mode. Reuse current source-session and persisted ordinal permission semantics without initial-login age checks or issuing new authority. Buffer sanitized result; rollback/dispose before publication. A single original deadline covers native validation through final cleanup; nested scopes cannot reset it. Current profile native IDs/material and enabled state must match. API receives no issuer private keys or factor-decryption keys.

The callback's exact access token and original expiration are retained only inside the existing authenticated ticket envelope and original atomic browser establishment. Missing or expired access credential requires fresh sign-in. BFF reads require the current browser generation, protected ticket binding and original lifetimes; no renewal. Forward one fixed HTTPS GET, no redirects/cookies/retries, only server-owned Authorization and finite query values. Never forward browser identity headers. Bound/read/validate/project response, clear data on failure/expiry and recheck original deadlines before publication.

## Complexity Tracking

No blanket baseline expansion or speculative provider abstraction. Refactor only stable shared locking/grant mechanisms needed by the API read; retain existing operation-mode guards and add explicit regressions proving read mode cannot commit authentication/governance/maintenance work.
