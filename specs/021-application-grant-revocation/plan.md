# Implementation Plan: Managed application grant revocation

**Branch**: `128-application-grant-revocation` | **Date**: 2026-11-05 | **Spec**: [spec.md](spec.md)

## Summary

Deliver explicit application grant revocation with durable acceptance, bounded worker
processing, protected progress and fresh approved recovery. The user selected disabled
until success and continuation after logout/MFA expiry with current account/permission
checks. Approval is recorded in docs/approval.md. No repeat of feature020 is required.

## Technical Context

.NET 10/EF Core 10/OpenIddict 7/Identity and React/TypeScript use the existing locked
packages. Both real relational provider projects and their existing migration histories
are retained. Native Aspire local-demo composes the Worker. No new provider abstraction,
queue broker, lease framework, token audience, refresh activation or external logout.

Existing owned five-second transaction/ten-second request budgets remain unchanged.
Each batch selects at most eight authorizations and eight tokens; eligible native states
are valid authorizations and valid/inactive tokens, independently of expiry. Already
invalid/redeemed/rejected/revoked entries are preserved. Revocation counts changed rows,
not guessed total grant counts. The disabled target and governance ordering make the
remaining set finite; no timestamp cursor or GUID cutoff is needed.

## Constitution Check

Pre-design: approved FR-01/06/09/10/11/12/17/18 and NFR-05, current-user choices and
existing native ownership govern this slice. All 23 principles remain applicable.
Post-design: native managers, server-held tokens, independent API permission/MFA,
short audit-atomic batches, equal provider migrations/tests, actual Aspire/browser
journeys and per-module coverage/mutation controls are specified. No exception or
release waiver is introduced. Independent review and required runtime evidence remain
conditions of delivery. Protocol outcomes and offline limits are stated separately.

## Project Structure

- `src/core/IdentityPlatform.Core/Features/ApplicationGrants/`: finite request/status
  DTOs, reason/state/error vocabulary and strict validation.
- `src/persistence/IdentityPlatform.Infrastructure/Features/ApplicationGrants/`: job,
  approval, audit detail, model, target eligibility, native batch selection/revocation,
  persisted authority and job operations. Reuse stable existing owned-store mechanisms.
- `src/backend/IdentityPlatform.Admin.Api/Features/ApplicationGrants/`: current authority,
  admission/continuation/status routes, bounded responses and versioned contracts.
- `src/backend/IdentityPlatform.Admin.Bff/Features/ApplicationGrants/`: fixed route
  forwarding and browser contract under existing cookie/CSRF/publication protections.
- `src/backend/IdentityPlatform.Worker/Features/ApplicationGrants/`: ordinary hosted
  processor, bounded polling, cancellation and safe telemetry. Thin host composition.
- `src/frontend/IdentityPlatform.Admin.Ui/Features/Applications/Editor/`: explicit
  revocation confirmation, progress/recovery and disabled-state explanations.
- Both migration `Platform/` directories, Administrator/Host/Persistence/contract/UI
  tests, native Aspire/browser journeys and `eng/` discovery/mutation inventories.

## Owned execution

Admission requires current `grants.revoke`, fresh MFA, immutable input and exact target
version. The target must be managed, non-system and disabled; preserve credential expiry.
Commit job, immutable approval and acceptance audit together. The current mutation
receipt may be factored to support a closed new intent; never give rollback-only reads
commit authority or reuse a live receipt for workers. Return `202` only after actual
owned cleanup and original publication checks. Ambiguous response recovery uses a scoped
idempotency key, not a blanket automatic mutation retry.

Worker candidate reads are bounded/no-tracking and carry no authority. Start a fresh
owned request/transaction and use governance → profile → effective actor locks, then
reread job/approval/target and current account/grants. A changed approval requires
reselection. Read account/factor metadata only: active enabled account, valid lockout
budget, current enrollment generation, one nonempty protected factor record; no key or
ciphertext access. This proves metadata eligibility, not factor decryptability. Reuse
persisted `GovernanceGrants`, never role defaults or recorded permission snapshots.

Materialize bounded scalar native IDs with exact application/status checks. Invoke
native `TryRevokeAsync` outside EF execution leases; false aborts the batch. Reload native
rows, verify revoked statuses, then atomically persist exact counters/revision/time and
batch audit. Reread effective actor authority and target before the one commit. Empty
remaining sets produce a separate successful reconciliation/audit, leaving the application
disabled. Re-enable under the same governance lock rejects any incomplete job.

All native/progress/audit state is transactional; no running flag grants ownership. Job
progress survives process loss. Unknown commits are reread in a fresh owned scope and
compared with the attempted revision before further work; no local attempted count is
published. A failed batch records a stopped outcome in a fresh serialized transaction
for the same effective approval, even if other batches advanced progress. Never overwrite
a newer approval or terminal success; reconcile unknown commits first. If storage prevents that record, do not
claim a terminal outcome; durable state remains authoritative. Fresh polling reconciles
eligible work after restart. Missing actor-lock records never allow unlocked work; a
separate governance-only stop path confirms absence without revoking entries.

## Recovery, status and idempotency

Stopped work requires a fresh authorized explicit continuation of the same job/scope.
The new approval becomes effective; prior approval history/counters remain immutable.
Account permission restoration alone cannot restart stopped work. Status needs current
`applications.read` plus `grants.revoke`; no fresh MFA for read-only status. This is current
independent API authorization, not possession of a job ID. UI polling stops on expiry.

Use canonical UUID request keys and a hash of canonical immutable intent. Bind keys to
actor/action/exact target for 24 hours; matching current authorized requests recover the
same job, changed hash conflicts. After expiry reject reuse of the retained key; do not
silently treat it as a new submission. No more than one incomplete job per application,
enforced under existing governance serialization. Store approvals as durable audit-linked
history; do not delete active records or revive jobs via cleanup. Retention changes are
outside this slice and existing audit retention/owner gates still apply.

## Verification and delivery

[Data model](data-model.md), [contracts](contracts/revocation.md), [quickstart](quickstart.md)
and [tasks](tasks.md) define exact obligations. Required tests cover actual native states
and isolated real OpenIddict entry validation, plus separate genuine issuer journeys,
permissions/MFA/logout/expiry, duplicate/restart/commit ambiguity, provider-observed races,
real audit/native-save faults, target protection and schema upgrades. UI/retained browser
and native Aspire worker paths must run. Preserve test identities and all coverage floors;
run behavioral mutation controls for security guards. Final full applicable CI, exact-head
review and evidence reconciliation precede normal squash delivery. Windows/manual,
Firefox/A-009 and historical/human/public-release gates remain open.
