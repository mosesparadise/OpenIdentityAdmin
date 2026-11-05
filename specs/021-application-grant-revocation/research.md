# Grant-revocation research

**Date**: 2026-11-05. **Baseline and observed remote main**:
`dce682f9812d228b41ae7fad1dcb1f6636f40d38` (rechecked with `git ls-remote`).
Branch `128-application-grant-revocation` is isolated from retained delivery worktrees.
Feature020 was accepted in PR122 and its delivery record in PR123. This packet does
not repeat that work or revive any expired diagnostic approval.

## Product decisions

The user explicitly selected continuation under recorded approval with current account
and permission checks after sign-out/MFA expiry. The user subsequently selected the recommendation: disabled until success. GR-004 is
resolved; implementation and its required dependencies/tests proceed under the recorded
scope in docs/approval.md.

Proposed recovery: an authority-denied or failed job stops until a currently eligible
administrator explicitly confirms the same remaining scope with fresh MFA. Keep both
approvals and original completed progress. Each later batch checks the actor of the
currently effective approval; earlier actors remain immutable audit history. Duplicate
recovery requires current authorization and actor/action/exact-target scoped idempotency.
Mere worker restart can continue eligible
accepted work. Proposed status permission is both `applications.read` and `grants.revoke`.
These are reviewable conservative design rules, not fabricated user choices.

## Architecture findings

| Boundary | Existing code / contract | Required treatment |
| --- | --- | --- |
| Native storage | Infrastructure `Features/ProtocolStorage`, `OwnedAuthorizationStore`, owned token stores | Invoke native managers in owned transactions; native bulk methods do not establish bounded processing. Broad `Prune` remains forbidden. |
| Serialization | `ManagedRegistration.LockAsync`, `ManagedDurableToken`, `ManagedIssuance`, `ProtocolAdmissionOwnership` | Reuse existing governance lock ordering at admission and each batch, not across the entire job. |
| Current request authority | `ApiMutationAuthority`, `ApplicationMutationOperation` | Preserve independent API admission and original request deadlines; introduce distinct durable approval/current worker authority instead of renewing a live receipt. |
| Current grants | `GovernanceGrants.ReadCoreAsync` | Read persisted role membership and exact permissions; missing stored grants never fall back to bootstrap defaults. |
| Account eligibility | `CeremonyAccount.IsActive`, `CeremonyBudget.IsAvailable`, `CeremonyClock.TryRead`, `GovernanceUsability.IsEligibleAsync` | Check active account, lockout, current enrollment/factor generation and exactly one nonempty protected-factor record without loading its ciphertext; this proves metadata eligibility, not cryptographic decryptability. Do not reuse session predicates wholesale. |
| Protected target | `ApplicationMutationWriter.RequireMutableAsync`, administrator profile and pilot manifest | Protect persisted IDs and case-insensitive reserved names, including name drift. Use cleanup-specific eligibility: normal managed registration admission rejects disabled/expired targets. |
| Worker composition | Worker `Features/Setup.cs`, `Features/Storage/Setup.cs` | Worker currently has storage/readiness, not a durable processor. Add feature composition and test startup/disposal through ordinary Aspire. |
| HTTP/browser | Approved04 management conventions | Durable `202` job ID/status URL, scoped idempotency, bounded errors; separate versioned API/BFF contracts and generated browser types. Preserve current cookies/CSRF and independent API authority. |
| Native selection | `ManagedTokenBinding.Bound` | Cached request tokens may have no authorization ID; bounded authorization and token selection must be independent. |

Prefer one short transaction holding serialization from authoritative reread through
selection, native lifecycle operations, progress and audit commit. This can avoid a
speculative lease framework. A persisted running flag is not ownership. If processing
escapes that transaction, claims require fencing and stale-worker rejection evidence.
Retain the existing five-second transaction and ten-second request budgets. Validate
batch size against those budgets; never stretch them to complete an entire job.

Every advance must follow successful native lifecycle results and fresh persisted status.
Use exact application identity and bounded stable selection on both providers. Preserve
already-invalid states; count committed transitions truthfully and distinguish already
ineligible entries. Reconcile unknown commits in a new owned scope, preserving existing
cleanup/quarantine discipline. Do not add blanket security-mutation retries.

## Selected lifecycle and rejected alternative

**Require disabled until success (selected):** governance-serialized
disable gives a finite target set. Admission and every batch enforce disabled state;
re-enable rejects outstanding incomplete work, including stopped/failed jobs, until
successful reconciliation. Explicit recovery prevents an undocumented permanent dead end.
Completion never automatically enables the application. Existing disable permission is
separate: revoking an already disabled application needs `grants.revoke`; automatically
disabling as part of submission would require both permissions and is not proposed.

**Allow while enabled (rejected for this slice):** define durable membership/generation for the pre-existing set
and descendants created from selected grants. Native GUID order or second-resolution
creation timestamps alone do not establish a safe cutoff. Equal times, clock rollback,
concurrent issuance and finite completion need additional issuer design and tests. Do not
silently reuse the simpler disabled algorithm if this option is selected.

## Required evidence before implementation acceptance

These are planned obligations, not executed tests or assigned test IDs. Final tasks,
exact case identities and control links follow clarification and the plan/analyze stages.

| Requirements | Required verification |
| --- | --- |
| GR-001–004,006 | Genuine current admin/password/MFA, per-action permissions, stale/missing versions, protected persisted IDs/name drift, expired credentials, exact reason/target; API and BFF origin/CSRF/current-generation negatives. |
| GR-005,008,014 | Independent native authorization/token reads; authorization-less tokens, zero/one/batch/batch+1 sets, already invalid states, exact application/collation isolation. Native lifecycle result and atomic audit failure controls. |
| GR-007,012 | Logout/MFA expiry continuation; account disable/suspension/deletion/lockout/factor invalidity and permission removal stop; both orders of permission change versus batch commit; no automatic revival, explicit fresh continuation. |
| GR-008–010 | Two actual worker roots with provider-observed lock contention; process loss before/after commit; lost acknowledgement, audit/native-save failure, cancellation/deadlines, restart, duplicate submissions and conflicting hashes. |
| GR-011,013,015 | Sanitized status and authorization; UI admission versus completion, bounded error/recovery states, keyboard/focus/a11y/light/dark; actual worker through ordinary native Aspire on both providers and retained browser engines. |
| All | Additive fresh/populated migrations preserving both histories, strict contracts/generated client compatibility, new startup/job coverage, existing test inventory/floors, behavioral mutation controls and exact-head independent review. |

Native revocation evidence must be causal. Old code rejection after disable/re-enable
can result solely from application revision mismatch (`ManagedTokenBinding.ReadAsync`).
Managed OpenID-only tokens already fail Admin API audience validation. Neither proves
native entry revocation. Require fresh persisted status plus real OpenIddict
`ValidateTokenEntry` / `ValidateAuthorizationEntry` behavior in a narrowly scoped owned
test harness isolating those checks, with unrevoked controls. Separately test genuine
cross-instance issuer lifecycle behavior. Never enable dormant refresh/introspection,
widen audiences or expose a production test endpoint to manufacture an oracle.

Measure the five-second entry-validation requirement from the relevant batch commit,
not `202` acceptance. Full-job throughput needs separately reported representative
cardinalities and elapsed time. Offline JWTs and external application sessions retain
existing lifetime/skew and ownership limitations.

Reusable tests include ManagedApplications `ManagedCacheTestContext`, `ManagedTokenFlow`,
`ManagedTokenOracle`, `ManagedLifecycleRaceTests`, provider `GovernanceLock`/`NativeBlockedRace`,
Applications `ApplicationLifecycleFailureTests`/`ApplicationHttpUpdateEvidence`, and
SessionRefresh `NativeCleanupSelection` (test-only, not an existing product processor).
Preserve `eng/*-test-matrix.json` inventories, selection controls and all module floors.

## Evidence limits

Architecture, security and test research were read-only; no runtime/SQL experiment,
build, browser, full CI or new application acceptance occurred. Local pinned browsers
remain unavailable from earlier observations; no download or policy workaround was
attempted. Firefox/A-009, actual Windows/manual accessibility, all six historical
findings and human/vendor/production/public-release gates remain open. Current managed
environment reports enforced network policy and current observations; this is not proof
of successful access to an individual registry or new browser availability.
