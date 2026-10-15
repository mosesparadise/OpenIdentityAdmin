# Feature specification: Durable administrator session foundation

**Feature branch**: `049-bff-session-foundation`  
**Created**: 15 October 2026  
**Status**: Planning; no runtime implementation accepted  
**Input**: The user approved preparing M02-B's implementation scope and resolving its sequencing dependencies: “Proceed with your recommendation. I want to see real progress.”

This is the first bounded implementation slice of roadmap M02-B, supporting PRD FR-11, FR-12 (partial session audit intent), FR-17, FR-18, FR-21, FR-22 and NFR-02, NFR-08, NFR-11, NFR-14. Its outcome is a durable, protected server-side ticket store that later administrator login can use. It does not itself sign a browser in or authorize a management action. [Sequencing and remaining M02 work](research.md) distinguish planning delivery, internal implementation and activation.

## User scenarios and testing

### US1 — Preserve protected sessions across instances (P1)

An operator needs administrator session state to survive a service restart and be readable by another correctly configured instance without exposing its credentials.

**Independent test**: Create synthetic state through the store, dispose the first service instance, and read through a second instance against the same database and protected keys. Repeat for each supported database. No browser login shortcut is credited as authentication evidence.

**Acceptance scenarios**:

1. Given valid synthetic identity/session bindings, creation returns a fresh opaque handle only after durable commit. A second instance returns the exact protected state for that handle.
2. Given another session's protected state, tampering, a different protection boundary or unavailable decryption material, retrieval returns no usable ticket and reveals no private contents.
3. Given a failed or cancelled write, the caller receives no successful ticket. A lost commit response never causes automatic creation retry or reuse of a caller-chosen handle.

### US2 — Enforce expiry and irreversible local revocation (P1)

An administrator's stored session must stop being usable when its idle or absolute deadline is reached, or when local revocation commits.

**Independent test**: Control time and race two independent store instances against the same row. Record successful commits and resulting persisted state on both databases.

**Acceptance scenarios**:

1. Given a newly created ticket, it is unavailable at the exact idle deadline of 30 minutes or absolute deadline of eight hours, whichever comes first. Shorter supplied limits are honored.
2. Given a current ticket revision, renewal may extend the idle deadline but never the original absolute deadline or an earlier upstream expiry. Reading alone does not renew activity.
3. Given competing renewals, at most one update with the same expected revision commits. Given revocation and renewal in either order, revocation leaves the handle unusable and stale renewal cannot recreate it.
4. Given time earlier than the persisted creation or last-activity time, retrieval and renewal fail closed; time rollback does not extend a session.

### US3 — Operate the store without activating administration (P2)

An operator needs isolated permissions, recoverable protected keys and bounded cleanup while administrator access remains unavailable until the complete login and authorization work is accepted.

**Independent test**: Exercise runtime database credentials, repeated migration, key restart/rotation/loss, cleanup and actual endpoint inventory. Check that no new login, callback, proxy or management endpoint appears.

**Acceptance scenarios**:

1. Given the session runtime account, ticket operations succeed but identity-store reads, schema writes and migration-history mutation are denied on both databases.
2. Given expired/revoked rows, bounded cleanup removes only eligible rows and cannot enable stale renewal after deletion.
3. Given missing, plaintext, corrupt or incompatible configured key material, enabled store initialization fails safely; it does not silently substitute an ephemeral or issuer-shared protection boundary.
4. Given the default application composition, no browser or management access is activated by adding this internal capability.

### Edge cases

Unknown/oversized handles; oversized protected payload; empty bindings; unsupported payload version; swapped rows; missing keys; cancellation before commit and unknown commit outcome; renewal at exact expiry; revocation after a previously successful read; cleanup racing renewal/revocation; dependency failure; clock rollback; rolling wrapper-key rotation; restore of an older database snapshot.

## Requirements

### Functional requirements

- **FR-001**: Create fresh unpredictable handles owned by the store. Never accept a caller-selected handle, expose a database key as a credential, or reuse a revoked handle.
- **FR-002**: Protect ticket and token contents at rest, cryptographically bound to their handle, subject and source authentication-session binding. Require the ticket's own identity/session to match those bindings on creation, replacement and retrieval. Reject cross-session substitution and malformed content.
- **FR-003**: Preserve valid state across independent instances/restarts using the selected production database provider and durable protected keys. Both providers have equal acceptance criteria.
- **FR-004**: Enforce a maximum 30-minute idle and eight-hour absolute lifetime. Persist and authenticate the originally selected shorter idle duration; renewal cannot widen it. Never extend the original absolute or source authentication-session deadline; access-token lifetime is a separate constraint. The stored ticket's own validity must agree with the store's effective window. Equality with a deadline means expired; reads do not renew.
- **FR-005**: Make renewal conditional on the expected stored revision and live state. Revocation is idempotent and must win over stale renewal; deletion must never permit resurrection.
- **FR-006**: Bound all input sizes, database work and cleanup; honor cancellation and fail closed on unavailable storage, invalid state or clock rollback. Do not retry security mutations automatically.
- **FR-007**: Use additive provider migrations and session-only runtime permissions. Web startup must not migrate schema. Document upgrade, rollback compatibility and restore invalidation.
- **FR-008**: Keep authentication, current administrator eligibility, permission checks, remote token revocation and HTTP activation outside this store's authority. A successful read is not authorization.
- **FR-009**: Commit minimal session-owned audit intent atomically with each successful create/renew/revoke transition; audit failure rolls back that transition. Retain finite sanitized outcomes, target/revision and operation correlation without tokens or full handles. Never log protected payloads, credentials, connection strings or private identity data. This partial PRD FR-12 contribution does not establish authenticated actor attribution, external delivery or the complete administration audit service.
- **FR-010**: Use a dedicated BFF protection application/purpose and separately configured durable encrypted key ring. Verify startup, restart, retained decryptors, rotation, corruption and key loss; do not share issuer cookies or credential ownership.
- **FR-011**: Provide bounded removal of expired/revoked rows and document retained-state limits. Unknown commit outcomes require reauthentication/reconciliation, not silent retries.

### Key entities

- **Stored administrator ticket**: protected credential state, opaque-handle binding, subject/source-session binding, creation/activity/deadline timestamps, portable concurrency revision and revocation state.
- **Protection boundary**: BFF-specific application/purpose, protected persisted keys and retained decryptors; distinct from the issuer's boundary.

## Success criteria

- **SC-001**: Every restart, tampering, substitution, expiry and race scenario passes against both supported providers with no missing/skipped required case.
- **SC-002**: After revocation commits, every newly initiated load for that handle returns no usable ticket; stale renewals cannot revive it, including after cleanup.
- **SC-003**: Neither stored rows nor retained logs contain the synthetic plaintext token sentinels. The full handle appears only in the intended in-process creation result, never in persisted rows or logs.
- **SC-004**: No administrator login, callback, proxy, documentation access or management endpoint is added by this slice.
- **SC-005**: Independent architecture/test/security review, exact-head applicable CI and a normal squash merge precede implementation acceptance. Planning checks alone satisfy none of SC-001–004.

## Assumptions

The approved v0.2 architecture supplies the database/session separation and protection requirements. The current user instruction advances scope preparation; it does not waive any acceptance gate. [Research](research.md) records what exists and what is missing. All six historical findings, 48 human assessment rows and activation/public-release blockers remain open. A-004 is expired; A-005/A-006 are concluded. No diagnostic approval is renewed.

The foundation is exercised through internal service calls with synthetic data until later issuer/client and browser work is accepted. It does not prove administrator MFA, five-second API enforcement, refresh serialization, remote logout, browser token secrecy or whole-milestone acceptance.
