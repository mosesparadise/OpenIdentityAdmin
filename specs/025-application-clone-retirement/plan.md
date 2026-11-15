# Implementation Plan: Application cloning and permanent retirement

**Branch**: `148-application-clone-delete` | **Date**: 2026-11-14 | **Spec**: [spec.md](spec.md)

## Summary
Complete M04-A through separately reviewed clone and retirement increments. Clone creates
a disabled managed registration with a fresh one-time credential and reviewed callbacks/
eligible assigned scopes. Retirement retains identity/history, immediately stops issuance
and admits durable grant revocation atomically. The user selected permanent retirement.

## Technical Context
- C#/.NET 10, pinned SDK 10.0.404; existing TypeScript/React workspace.
- Existing locked Identity, OpenIddict 7.7.2, EF Core 10, Aspire 13.6.5 and approved
  Playwright 1.63; no dependency update.
- Real PostgreSQL and SQL Server; additive migrations in both provider histories.
- Native .NET tests, Node UI checks, generated contracts, real Aspire hosts,
  Chromium/WebKit, exact inventories, coverage floors and causal mutations.
- Existing bounded request/observation deadlines, no mutation retry, server-held OAuth
  tokens, antiforgery, exact resource/version binding, current authority and finite audit.
- Existing bounded pages/batches; retirement filter executes before offset/lookahead.
- Supported managed confidential-web profile only. Windows manual evidence stays open.

## Constitution Check
Before research: pass for approved FR01/M04-A scope and selected retention semantics.
After design: native managers retain protocol/credential ownership; existing governance
ownership orders mutations with issuance. Independent API/BFF authority remains mandatory.
Additive schema preserves native identities/restrictive history FKs. No verification or
complexity exception proposed. Independent architecture/test/security/API/UX reviews and
actual both-provider evidence are required; planning does not constitute runtime acceptance.

## Project Structure
Documents: spec, plan, research, data-model, contracts/management, quickstart, tasks,
analysis and delivery evidence in `specs/025-application-clone-retirement`.
Code: existing Features/Applications and Features/ApplicationGrants in Core, Infrastructure,
Admin.Api, Admin.Bff and Admin.Ui. Issuer checks remain in managed registration/login.
Migrations remain in existing provider projects; tests extend matching host, administrator,
browser and contract features. Preserve generated-contract tooling and pinned Spec Kit.

## Implementation sequence
1. Clone: confirmed exact intent; current source/catalogue snapshots; shared native creation
   with new secret and disabled policy; source/destination/audit verification; fixed API/BFF
   routes; reviewed UI form/one-time output; test, independently review and squash.
2. Retirement: terminal state and immutable receipt linked to a new grant approval; atomic
   disable/revision/audit/job admission; scoped replay; issuer/mutation guards; pre-page
   filtering and retained detail/recovery UI; test, independently review and squash.
3. Reconcile original exact-head evidence and update roadmap/tasks/handoff per increment.
   Clone completion never marks retirement complete.

## Complexity Tracking
No exception requested. Extract cohesive source-observation, creation, audit and admission
helpers where needed to respect 40/300 review thresholds. Do not grow legacy allowances.
