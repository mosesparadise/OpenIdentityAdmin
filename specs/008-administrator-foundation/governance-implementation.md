# T020/T021 — internal administrator governance execution packet

**Current correction checkpoint (13 October 2026):** implementation was already present at the handoff and is not repeated. The user's current failure-fix request covers the [reviewed test/evidence corrections](governance-failure-corrections.md), followed by required full verification for conditional delivery. Production source and the approved T019 scope are unchanged. The earlier hold below records A-004's expiry; that amendment is not renewed.

**Delivery hold (13 October 2026):** A-004 expired on incomplete required SQL mutation evidence in PR48. T020/T021 remain incomplete and unmerged; no runtime continuation is authorized. [Verification and proposed diagnostics](governance-verification.md). Earlier implementation checkpoints below remain historical observations, not current delivery authority.

Status: pre-code architecture/security/test design review converged; publication precedes runtime coding. A-004 was explicitly approved on 13 October 2026; all four historical findings and human/public-release gates remain open. This packet refines the approved [T019 design](governance-design.md), not its scope. The user approved the feature-local Governance folders, three test partitions and dedicated test-only process driver on 13 October 2026. Runtime implementation is now in progress; no runtime acceptance or completed T020/T021 delivery is claimed.

## Outcome and boundaries

Implement authoritative current grants, configured usable administrators and a fixed internal command boundary. Cover GOV-01–12 and ADMIN-004–009/011/012 with native Identity managers, protected active factors, real PostgreSQL/SQL Server and observable distributed transactions. No management HTTP route, CLI operation, new production host, recovery bypass, second-administrator onboarding, API/BFF/React authentication or certification claim is added.

Preserve the existing project groups, thin Programs, explicit Features/Setup registration, pinned packages, xUnit v3/MTP, five-second operation ceiling, 660-second test invocations and all existing native test identities. Current baseline: 429 unit-profile cases, 644 issuer native cases, 510 administrator native cases (242 HTTP/store and 13 browser cases per provider), 82 lifecycle cases and 49 mandatory coverage inputs. These inventories describe the baseline, not acceptance of new code.

## Concrete decisions

### Current grants and usable accounts

Current grants are an immutable operation-local snapshot, never a reusable authorization capability. Read native users, memberships, role identities and claims while holding the existing governance/account serialization boundary. Separate ReadCommitted queries cannot mix different committed governance states. Read-only evaluation disposes without committing technical lock updates or changing AuthorityRevision.

Validate IDs, role names/normalization and the reserved permission type/value with ordinal semantics after retrieval on both databases. Duplicate relevant memberships/role claims, ambiguous reserved identities, unknown or out-of-bundle permissions and permission claims on custom roles deny. Legitimate cross-role overlap unions. Unrelated user/token/nonpermission claims grant nothing. Persisted removals stay removed; the code catalogue validates values and never reconstructs missing grants.

Use a distinct usable-platform predicate: real enabled/lockout-enabled native user, native password presence, native TwoFactorEnabled, nonempty security stamp, Active/non-suspended coherent enrollment, positive generations, valid revisions/budgets/clock fence, readable protected **active** factor and all 25 grants persisted on the exact PlatformAdministrator role itself. Neither a live session nor recovery codes affect the population predicate. Reject ambient staged/recovery/installation factor selection for governance; staged material cannot cover a corrupt or missing active factor.

### Supported command catalogue

| Command | Required current permissions | Target/version binding |
| --- | --- | --- |
| AssignBundle / RemoveBundle | users.write + administrators.assign | Exact user and reserved role; user/role concurrency stamps, enrollment revision, AuthorityRevision |
| AddGrant / RemoveGrant | roles.permissions + administrators.assign | Exact reserved role and one finite allowed permission; role concurrency stamp and AuthorityRevision |
| EnableAccount / DisableAccount | users.disable + administrators.assign | Exact already coherent privileged account; user concurrency stamp, enrollment revision, AuthorityRevision |
| SuspendAccount / ResumeAccount | users.disable + administrators.assign | Same account/version contract; never promote Pending/RecoveryRequired to Active |

Unsupported action values, reserved role rename/delete, user deletion, arbitrary manager dispatch and unknown permissions deny. User deletion/onboarding remain M05. A request is copied into a validated immutable snapshot before discovery; irrelevant/contradictory fields and noncanonical identifiers deny. A no-op returns a fixed NoChange outcome, rolls back, and creates no accepted audit, semantic version increment or committed replay reservation.

Requests contain versions for explicit mutation targets only. Witness IDs/versions and derived affected membership sets are server-owned. Membership commands bind both native user and role versions; role-grant commands derive every affected account from current native membership. All required account rows must already exist. A pending nominee is not silently initialized or counted as usable.

One outer cancellation source covers validation-dependent database discovery and transaction work within the existing five seconds. Nested transaction cancellation remains linked to that budget; discovery does not receive an additional five seconds. Native user and role managers must propagate the transaction token through actual provider calls. No retry or timeout increase is allowed.

### Ordered locking and projected state

Before opening the transaction, discover and snapshot the complete sorted actor/target/affected-account/witness lock set. Acquire the existing governance singleton first, then required account locks in ordinal order. This increment requires no browser/admission allocation. Reread relationships and state after acquisition; a changed set requiring an undeclared lock denies as stale. Never append earlier locks or retry.

Use feature-owned bounds of 256 accounts in the complete distinct actor/target/affected/witness lock-plan union (not separately truncated subsets), 64 memberships per evaluated account, 128 role records and 128 claims per evaluated role; each read takes limit plus one and denies overflow. Read relevant records before exact ordinal filtering so aliases and malformed authority cannot disappear differently under provider collations. The role-record bound is an explicit conservative v1 internal capacity limit; future custom-role scale requires separately reviewed retrieval semantics. No truncated set can establish authority or survival. Boundary and overflow controls exercise each bound; these limits do not promise future bulk-administration scale.

Validate expected AuthorityRevision and native target/enrollment versions before native proof work. Native password verification can rehash and change stamps: any legitimate manager write stays inside the transaction, and the original stored session must still validate afterward. Never refresh/rebind an old session to make verification pass. Changed security bindings deny and require normal authentication. The proof captures the post-verification native state only after the original intent and same session remain valid.

Authorize the actor against current pre-mutation state. Evaluate projected survival, preserving at least one locked usable PlatformAdministrator for ordinary voluntary destructive commands. Removing a PlatformAdministrator grant cannot be rescued by another member because the shared role would be incomplete for all members. Check actual post-write survival against the same locked witnesses before commit; do not require an intentionally self-demoted/disabled actor to retain the authority just exercised.

Disable/suspend advances authentication generation and reuses JourneyRevocation to revoke administrator/local sessions and pending interactions atomically. Enable/resume never unrevokes old sessions, resets failures, creates a factor or promotes enrollment to Active. Existing mandatory lockout/restricted recovery may still make the population zero after a safely committed voluntary change; no count override suppresses security enforcement.

### Fresh proof, replay and failure handling

Governance authorization and command execution occur in one transaction; there is no new persisted challenge, HTTP step-up form or externally constructible proof. A private factory creates GovernanceProof only from the same current JourneySessionProof, native password verification and unused active TOTP. Bind the immutable command, original expected versions, exact actor/session/targets, locked snapshot and transaction ownership. Preserve the strict existing freshness ceiling and future-time denial. Consume once immediately before protected writes; recheck proof and original session time before commit using the existing time-only contract after intentional self-revocation, without requiring the revoked actor to retain its pre-write authority.

Extract the narrow common native replay/reservation mechanism from CeremonyReplay so existing interaction proof and new governance proof factories share the same protected ledger, bounded capacity, safe pruning and corruption handling. Preserve the interaction-bound factory's existing checks. Never fabricate an AdministratorInteraction or relabel CeremonyProof as governance authorization. The shared mechanism alone is not a caller's authorization capability.

Wrong password/TOTP commits only the actor's existing durable failure/lockout policy and a fixed denial audit when safe, never target writes or AuthorityRevision. Password-only success never clears factor failures. A successful operation commits its full-proof budget reset, replay reservation, native/product writes, semantic version and accepted audit together. Policy/version/session/no-change denials roll back; operational/protection/cancellation failures cannot be converted to accepted mutations or retain partial target state.

### Atomic version and audit

Add portable nonnegative AuthorityRevision to the existing governance singleton, default zero, without changing BootstrapConsumed, receipts, grants or technical Revision. It increments exactly once for an accepted committed authority command. Reads, lock acquisition, no-op/denied commands, ordinary session activity and rollback do not increment it. All semantic, authentication, enrollment and affected session generation/revision advances check overflow and deny rather than wrap. Add both provider migrations/snapshots and readiness expectations before use; runtime credentials retain DML-only privileges.

Extend CeremonyTransaction with a narrow typed governance commit path backed by a private-factory commit capability. Its private factory derives actor/target/action from the bound command, requires the same live transaction and once-consumed proof, validates actual intended post-write state and the locked survivor, and rechecks fresh time and the exact AuthorityRevision transition. Callers cannot supply a success flag, audit record or asserted survivor result. Keep raw commit inaccessible; neither maintenance commit nor a free-form callback can authorize governance. A feature-owned writer creates one SecurityAuditIntent with fixed action/outcome, actual actor, actual user/role target, accepted timestamp and nonsecret correlation. No payload/credential serialization. Actual audit INSERT denial must roll back prior manager writes, proof/reset and semantic revision. Lost response means unknown completion; reconcile state/version/audit rather than replaying the command.

## Source inventory and ownership

New names below are the planned cohesive owners; refinements require packet/reviewer reconciliation before changing a boundary. Generated migration timestamp paths are recorded when scaffolding produces them; generated schema diffs still require review.

| Existing project / feature | Planned files and responsibility |
| --- | --- |
| Core/Features/Identity/Governance | GovernanceAction, GovernanceRequest, GovernanceVersions, GovernanceOutcome, GovernancePermissions: finite command/permission contracts, immutable snapshots and pure validation |
| Infrastructure/Features/Identity/Governance | Setup; GovernanceService (bounded orchestration); GovernanceDiscovery/Locks (immutable complete plan and stale-set checks); GovernanceGrants (persisted exact grants); GovernanceUsability (separate population predicate); GovernanceProof/Commit (private capabilities); GovernanceMutation (checked native/product transitions); GovernanceAudit (fixed actor/target records); GovernanceRoleManager (native cancellation ownership); GovernanceLimits (feature-owned capacity bounds) |
| Existing Ceremonies | CeremonyReplay plus extracted shared replay reservation; CeremonyTransaction typed governed commit; CeremonyBudget narrow full-governance-proof completion integration; retain all interaction-bound proof paths |
| Existing Factors/Journeys | Explicit active-material selection guard; reuse JourneySessionProof and JourneyRevocation without cross-feature handler calls; changes only where a reusable mechanism is required |
| Existing Persistence | AdministratorGovernance.AuthorityRevision and AdministratorModelConfiguration; preserve native constraints and rows |
| Existing migrations/health | AddAdministratorAuthority migration/designer/snapshot in PostgreSql and SqlServer Platform folders; SchemaCatalog readiness |
| AuthServer existing account composition | Explicit governance Setup registration only when real administrator factors/ceremonies are registered; no new mapping/Program statements |
| Administrator.Tests | Features/Governance/Policy, Commands, Failures; shared Fixtures/Governance scenario/snapshot/native-state/process helpers; exact inventory below |
| Test-only Governance.Driver | Thin Program + fixture-owned composition/operation runner; actual internal commands in owned child processes using redirected synthetic input, fixed safe output and observed process identity/cleanup; no HTTP listener |
| Verification tooling | test_selection/verify/matrix/workflow/coverage expected inputs and negative controls; governance mutation manifest/runner integration and restoration controls; no prior identity removal |

No package update or new production host is proposed. Reuse the existing BootstrapFixture runtime-role database leases, FactorServices/issuer material, native factor oracle, database waiters, command gates and owned process utilities. The test-only driver gets narrowly scoped internal visibility; production APIs are not made public for tests.

## Verification inventory and evidence

The [reconciled scenario inventory](governance-test-inventory.md) names native methods/rows, GOV families, provider scope, decisive oracles and partitions: Governance.Policy74, Governance.Commands68 and Governance.Failures49 additions per provider (191 total). Actual administrator ordinary discovery is433/provider and full native discovery892 across both providers, retaining browser13/provider. The final same-path comparison preserves all510 baseline native IDs, display names, type signatures and traits and adds382. The original approved174-case/provider estimate (74/62/38; full858) is historical planning, not the current inventory or execution acceptance.

Proposed stories are Governance.Policy, Governance.Commands and Governance.Failures in the existing Administrator project. Existing selectors use exact namespaces, so all three require explicit selection/CLI/matrix/workflow updates and seeded missing/duplicate/substituted evidence controls. Six additional both-provider coverage inputs would change 49 to 55 while preserving existing inputs and all invocation deadlines. Existing browser partitions remain unchanged. Actual native discovery and required manifests must match the reconciled inventory exactly; counts cannot substitute for identity equality.

Use independently scoped real stores for bulk policy controls and an actual test-owned child executor for command contention/crash/lost-response evidence. Killing an idle AuthServer cannot prove rollback of an in-process test command. At least the selected two-node cases must use actual executing child processes and observed database waiters; Task.WhenAll alone is insufficient. Existing real AuthServer sessions supply session-provenance/HTTP exposure controls where relevant, not a new management endpoint.

Add the six GOV-12 mutation boundaries with exact independently named decisive cases: bypass current grants, omit witness lock, skip last-administrator check, accept stale proof, permit unaudited commit and omit AuthorityRevision write. Require clean baseline, each demonstrated bypass failure, exact native identities, restoration and both providers. For the omitted-witness-lock mutant, arrange the decisive race after the authoritative witness observation and before protected commit: the kill must demonstrate an unsafe committed transition, not an initial waiter assertion timeout. SQL ReadCommitted reads may block even without the intended UPDATE lock; retain the ordinary observed-lock positive control independently. No early timeout or infrastructure exception counts as a killed mutant. Final evidence must retain all four existing mutation suite/provider obligations alongside the new governance mutations.

Before acceptance: syntax-aware quality/format/static checks, strict native inventory comparison, all required provider/browser/contract/failure/mutation suites, every expected coverage input and nine module floors, independent architecture/security/test/Aspire review, resolved comments and exact-head normal squash. Record actual execution, artifacts and limits. A-004's recurrence/expiry terms apply throughout; no historical cause or human observation is inferred from passing CI.

## Delivery bookkeeping

T020/T021 remain unchecked until complete verified delivery. Their implementation and tests are developed together; a partial mechanism is not whole-governance acceptance. This packet and the approval record were published before coding; final implementation review dispositions remain required. Normal reviewed task PRs retain Conventional Commit titles and squash commits. Update roadmap, schema/runbook implications and verification records with actual results; leave M02-B–E, M05 and public-release gates separate.

## Pre-code review record

Architecture review accepted the scoped boundaries after clarifying role-manager cancellation, read consistency, native password rehash, no-op behavior and disable/suspend revocation. Security review required complete-union capacity limits and explicit private commit-factory checks; both are incorporated. The independent test reviewer prepared and reconciled the exact 174-case/provider inventory, including capacity boundaries, cross-ceremony replay, manager ownership, no-op and reactivation controls. The user confirmed both structure choices. Architecture and security reviewers completed cross-artifact review with no remaining blocking design findings; the test reviewer reconciled all 43 method groups and 174 distinct cases per provider. The witness-lock mutation oracle is explicitly a demonstrated unsafe commit, never a waiter timeout. No runtime test result is claimed.

Spec Kit prerequisites identify feature 008; all 11 requirements-checklist entries are checked, with no extension hooks configured. Checklist completion establishes requirements quality only. Local instruction/assurance integrity, link-existence and diff checks passed for the packet; no runtime implementation or new discovery was performed.

## Implementation review refinements

The command uses a provider savepoint after locks and initial session/intent validation, before native credentials. An actual password/TOTP rejection rolls verification side effects back before clearing tracking, discarding old capabilities and reloading the same stored session. Only then can a private failed-attempt capability apply the normal actor budget and denial audit. Savepoint, rollback, cancellation, protection, stale-session and unsafe-time failures abort the whole operation. A native password rehash changes the security stamp in the pinned Identity implementation: the original session must fail and the whole command rolls back; it is never repaired through the savepoint.

Technical governance/account lock revisions are acquired before that savepoint. A counted credential denial retains exactly those coordination increments, including locked target/witness counters; target semantic/security state and AuthorityRevision stay unchanged. The fifth failed attempt intentionally commits lockout: final validation uses the original session's time-only contract after checking it fully before the failure transition. Accepted commands also recheck proof/session time after audit persistence.

Capacity exhaustion is a policy/availability denial, not an incorrect password/TOTP attempt; it rolls back without consuming a failure budget. Current permission observations require eligible configured account state independently of full-platform survivor membership. Enable/resume preflight does not count the newly reactivated target; actual post-write evaluation verifies the persisted state. This avoids asking the native active-factor store to read a still-suspended pre-write account.

Architecture/security/test review identified these refinements during implementation. Add explicit both-provider regressions for savepoint/pruning rollback, fifth-attempt lockout, full-ledger denial, native rehash, stale capabilities and foreign manager ownership. The final inventory must reconcile these controls before acceptance; the original 174-case estimate is not an acceptance ceiling.


## Runtime implementation checkpoint — 13 October 2026

The approved packet was published at `e5f43f6fac9d2d6532bd931d6cae994a6a80a799` before coding. Internal service, native manager ownership, provider migrations, command/proof/denial capabilities and tests are now implemented locally. The refined [inventory](governance-test-inventory.md) records actual native discovery and precise mutation credit. T020/T021 remain incomplete pending final source review, all exact-head provider/browser/mutation evidence, coverage and normal PR squash. The ten driver rows/provider cover eight transport controls and two real-process lifetime controls without database access. They establish cleanup of the owned fixed Linux process group, not arbitrary fork or escaped-process containment; see the inventory for the exact boundary.

Independent implementation review strengthened actor coherence, provider-savepoint enforcement, denial rollback, durable mutation oracles, secret-safe failure diagnostics, native replay-window controls and owned-task cleanup. The test-only executor uses isolated complete build output to avoid loading its differently versioned dependencies from the xUnit host directory. No production host, route, credential argument, or management activation is added. Native populated-schema downgrade/upgrade tests explicitly restore the prior database layout; they do not claim that an old executable generated the fixture data.

Local PostgreSQL Policy74, Commands68 and the selected22-case driver/process/failure cohort pass, as do four production endpoint-inventory controls and ordinary unit431. Static analysis including seeded controls passes. These are development receipts only. Both-provider CI, existing browser controls and mutation baselines/kills/restoration remain mandatory. A-004 remains subject to its exact recurrence and expiry terms; all four historical findings and all human/public-release gates remain unresolved.

### Final scoped source reviews

Independent architecture review found no unresolved source issue in complete ordinal locking, relationship revalidation, projected/actual survivor checks, mandatory security ordering or the closed production route boundary. Independent security review accepted the bounded driver transport/lifetime controls and the corrected constructor-failure cleanup. Test review required durable effect evidence for current-grant, survivor, stale-proof and audit mutations; the version-writer oracle explicitly proves only an uncommitted invariant and rollback. These are AI source reviews, not human PR approval, executed SQL Server evidence, mutation verdicts or external security assessment. All required execution and exact-head delivery gates remain pending.

All three ordinary local PostgreSQL governance partitions now pass (74/68/49, no skipped cases). The version2 administrator manifest binds complete/filtered native type and trait metadata without pinning path-dependent IDs. Seeded selector/mutation-tooling controls, final exact-head CI and actual native mutation evidence must still pass before acceptance.
