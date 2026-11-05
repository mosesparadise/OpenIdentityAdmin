# Implementation Plan: Managed application lifecycle

**Branch**: `126-application-lifecycle` | **Date**: 2026-11-04 | **Spec**: [spec.md](spec.md)

## Summary

Add explicit versioned disable/re-enable to existing application details. Reuse current mutation
ownership and managed issuer revision bindings. The user selected issuance-only scope on 5 November 2026. Implementation follows the
reviewed design; bulk grant revocation remains separate.

## Technical Context

- Existing .NET 10, EF Core, OpenIddict, Identity, React/TypeScript and pinned compatible dependencies;
  no package upgrade or new database abstraction is needed.
- PostgreSQL and SQL Server through the existing provider projects, additive Platform migrations,
  real both-provider upgrade/transaction/issuer tests.
- API/BFF contracts and generated browser types; native Aspire local-demo remains the launch path.
- Existing request, transaction, cleanup, browser and authority deadlines remain unchanged.
  Five seconds is the existing maximum disable-enforcement acceptance bound, not a new cache lifetime.
- Managed confidential-web code/S256/OpenID-only profile. Refresh stays disabled; no new resource
  audience, introspection, UserInfo or external session-management capability is introduced.

## Constitution Check

Approved scope and current authorization are in [spec.md](spec.md). Principles 2–7 and 14–15:
feature-local commands, thin hosts, native managers, server-held tokens, existing provider seams,
atomic audit and generated contracts. Principles 8–10 and 13: explicit lifecycle effects, bounded
failure handling, both-provider real races and end-to-end acceptance. Principles 16–18: retained
browser/UX evidence and independent architecture/test/security reviews. Principles 19–23:
40/300 maintainability thresholds, documented methods, Given/When/Then names, named finite values,
and proportionate fail-closed verification. No exception or release claim is proposed.

Pre-design review identified missing lifecycle storage/reason persistence and unsafe default
dispatch branches; the design below addresses them. Post-design architecture/test/security reviews passed; the user has resolved the scope decision. Reviewing this packet
does not mark the implementation or outstanding release gates complete.

## Project Structure

- `src/core/IdentityPlatform.Core/Features/Applications/`: lifecycle inputs, finite reasons, safe
  state projection and validation.
- `src/persistence/IdentityPlatform.Infrastructure/Features/Applications/`: policy, lifecycle
  audit detail/model, immutable command, explicit writer dispatch and persisted-state verification.
- `src/backend/IdentityPlatform.Admin.Api/Features/Applications/`: lifecycle routes, exact current
  permission/MFA selection, operation/contract and bounded details projection.
- `src/backend/IdentityPlatform.Admin.Bff/Features/Applications/`: fixed action routes, protected
  finite request/result handling and browser contract.
- `src/backend/IdentityPlatform.AuthServer/Features/Protocol/ManagedApplications/` and existing
  Infrastructure managed-account paths: current policy enforcement, revision-bound continuations
  and final publication; retain existing ownership boundaries.
- `src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/Editor/`: explicit lifecycle
  confirmation/state/error handling integrated into current details, not a second application editor.
- Both `src/migrations/IdentityPlatform.Migrations.*/Platform/` assemblies and corresponding
  Administrator, Host, contract, frontend and native AppHost demo tests.
- Design artifacts: this plan, [research](research.md), [data model](data-model.md),
  [contracts](contracts/lifecycle.md), [quickstart](quickstart.md), [tasks](tasks.md)
  and reviewer-owned checklists.

## Transaction and issuer design

Keep governance-first locking shared by mutations, managed cache admission, human steps,
durable consumption and final issuance. Lifecycle commands use the existing owned
`ApplicationMutationOperation` transaction, exact actor/source authority and publication receipt.
Explicit action dispatch selects `applications.disable` for both transitions; require the existing
five-minute persisted MFA freshness bound for these sensitive actions, without renewing it during waits.
Write/credential permission alone never authorizes lifecycle changes.

Check exact target, supported profile and system binding, then expected version and expected
source state. Advance aggregate Revision on each real transition; preserve CredentialRevision,
hash and expiry. A same-state current request is 409; stale version takes precedence as 412.
Stage a finite action/reason audit, verify persisted state/audit/reason after all saves, recheck
current authority and original deadlines, commit once, dispose actual ownership, then publish.
No automatic retry or durable success receipt is added.

Reject disabled registrations in `ManagedRegistration.ReadAsync`; include state in snapshot
matching and update predicates. Preserve fresh native reload, profile drift/expiry checks,
governance ordering and nondecreasing observation time. Existing application revision bindings
invalidate requests, human interactions and codes on both transitions. Do not rewrite old
bindings or reopen consumed native artifacts. If disable commits between durable consumption
and final issuance, consumption remains durable and issuance is denied.

A previously completed issuance may already have left the platform before disable commits.
Issuance is ordered at its governance-protected commit; its sealed response may also arrive after
the later disable commits. Do not add an unlocked post-commit Enabled check or imply that already
committed issuance can be retracted. The contract does not promise erasure of that response,
offline tokens or relying-party sessions.
Native stored grants are untouched by this bounded mutation; optional bulk revocation would
require a separate durable job design and current grant-revocation authority.

## Verification and delivery

Use [tasks.md](tasks.md) and its traceability table. Preserve feature019's accepted test identities
and evidence. Extend exact inventories only after actual discovery; use provider-observed waits
and independent roots for contention. Test real audit INSERT denial and post-save failure, not
only mocked exception paths. Add narrowly scoped mutation controls for lifecycle guards,
revision/current-authority checks and audit rollback.

Retain the feature019 evidence distinction for MFA timing: BFF-issued bearer authority can expire
before factor verification plus five minutes. Prove isolated MFA-expiry denial at the existing
component authority seam and genuine HTTPS denial at the earlier native deadline. Do not extend
native lifetimes to manufacture an unreachable HTTP-only MFA boundary.

Retain Chromium/WebKit and native Aspire demo coverage, existing deadline budgets and precise
Firefox/A-009 exclusions. The current cloud cannot download the pinned local browsers; do not
retry blocked downloads. Use hosted jobs for affected real-browser acceptance. A documentation
packet may use the existing proven documentation-only selection; executable changes require
full applicable hosted verification and exact source/tested-merge reconciliation before squash.

## Complexity Tracking

No new global service/repository, alternate issuer path or worker is needed for this proposed
scope. If bulk revocation is selected, add its concrete persistence/job/restart/authorization
requirements before implementation instead of hiding a synchronous scan inside disable.
