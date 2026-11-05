# Feature Specification: Managed application grant revocation

**Feature Branch**: `128-application-grant-revocation`

**Feature Directory**: `specs/021-application-grant-revocation`

**Created**: 2026-11-05

**Status**: Draft; one lifecycle choice remains unanswered. No implementation acceptance.

**Input**: User requested the next task after application disable/re-enable delivery,
then selected: “Continue under recorded approval; recheck account and permission.”
This is the separate grant-revocation follow-on, not a repetition of feature020.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Explicitly revoke an application's stored grants (Priority: P1)

An authorized administrator confirms the application and reason with fresh MFA,
receives a durable operation reference, and sees truthful progress to completion.
Disabling an application alone continues to retain its grants.

**Why this priority**: Delivers the separate destructive operation users need without
confusing blocked new sign-ins with revocation of stored authority.

**Independent Test**: Through application details, submit one eligible application,
observe completion, verify its stored grants and tokens are revoked, and verify a
second application's records and the target's configuration remain unchanged.

**Acceptance Scenarios**:

1. **Given** current revocation permission, fresh MFA, a current application version
   and an eligible target, **When** the administrator explicitly confirms a reason,
   **Then** a durable operation is accepted and its reference and progress are shown.
2. **Given** a stale version, protected or unmanaged target, missing permission or
   expired MFA, **When** submission is attempted, **Then** no revocation job is created.
3. **Given** accepted work, **When** progress is displayed, **Then** committed counts
   distinguish authorizations and tokens, and acceptance is never labelled completion.
4. **Given** completion, **When** the administrator reads the result, **Then** all
   eligible records in the selected scope are revoked, unrelated records are unchanged,
   and limits concerning offline tokens and sessions in other applications are visible.
5. **Given** a lost submission response and still-current access, **When** the same actor retries the same request identity,
   **Then** the existing job is returned; changed intent conflicts instead of retargeting it.

### User Story 2 - Continue approved work without an open browser (Priority: P1)

An administrator can sign out after accepting work. Processing continues under the
recorded approval while the account and revocation permission remain eligible.

**Why this priority**: Large operations must survive normal browser and worker lifetimes
without preserving an expired sign-in as live authority.

**Independent Test**: Submit a multi-batch job, sign out and allow the original MFA
freshness to expire; restart processing and verify exact durable progress and completion.

**Acceptance Scenarios**:

1. **Given** an accepted job and an eligible actor, **When** sign-out or MFA expiry
   occurs, **Then** subsequent batches can continue without another password/MFA ceremony.
2. **Given** an accepted job, **When** the actor loses account eligibility or revocation
   permission, **Then** subsequent work stops; prior committed progress remains truthful.
3. **Given** interrupted processing, **When** workers restart or contend for the job,
   **Then** no committed work is lost, falsely counted or applied to another application.

### User Story 3 - Understand and recover incomplete work (Priority: P2)

An authorized administrator can distinguish pending, partially completed, stopped and
completed work, inspect safe failure information, and explicitly recover eligible work.

**Why this priority**: Partial success must not masquerade as completion or require
manual database edits to recover.

**Independent Test**: Fail a later batch after earlier progress, read the durable
result, and exercise a freshly authorized continuation of the same fixed scope.

**Acceptance Scenarios**:

1. **Given** a failed batch, **When** status is read, **Then** its uncommitted changes
   are absent and earlier committed counts remain visible with a safe failure category.
2. **Given** work stopped for lost authority, **When** the original permission is
   restored, **Then** it does not silently restart; a current authorized administrator
   must explicitly confirm continuation with fresh MFA, with both approvals retained.
3. **Given** a continuation request, **When** account, permission, target or scope
   validation fails, **Then** no additional records are changed.
4. **Given** knowledge of an operation identifier alone, **When** status is requested
   without current access, **Then** private operation metadata is not disclosed.

### Edge Cases

- Zero eligible entries; exactly one batch; one more than a batch; already invalid entries.
- Authorization-less request tokens and tokens linked to authorizations.
- Concurrent duplicate submissions, revocation, issuance, re-enable and permission removal.
- Process loss or acknowledgement loss before/after commit; audit write failure; cancellation.
- Deleted, disabled, suspended or locked actor; invalid factor state; removed permission.
- Protected applications whose display/client names have changed; expired target credentials.
- A successful earlier batch followed by a stopped job; safe explicit continuation.
- Clock rollback, unavailable database and unknown commit outcome; no fabricated completion.

## Requirements *(mandatory)*

### Functional Requirements

- **GR-001**: Revocation MUST be an explicit action separate from disable/re-enable,
  identifying the exact application, current version and bounded reason before acceptance.
- **GR-002**: Acceptance and explicit continuation MUST require current account eligibility,
  current `grants.revoke` and fresh MFA. No other application permission implies revocation.
- **GR-003**: The system MUST protect administrator/pilot and unmanaged applications by
  durable identity as well as reserved names; expired credentials alone MUST NOT exclude cleanup.
- **GR-004**: [NEEDS CLARIFICATION: Must the application be disabled and remain disabled
  until revocation succeeds, or may it remain enabled while only a precisely bounded
  pre-existing set is revoked? The existing question is pending; neither option is approved.]
- **GR-005**: The selected scope MUST include eligible stored authorizations and tokens
  independently, including tokens without an authorization link. Already invalid records
  MUST NOT become valid. Other applications and target credentials/configuration are preserved.
- **GR-006**: Acceptance MUST persist immutable intent, approving actor, reason, target,
  scope, time and correlation, returning a durable job reference and protected status location.
  Recorded approval MUST NOT retain or replay sign-in credentials or bearer tokens.
- **GR-007**: Accepted work MUST continue after sign-out or original MFA expiry while
  rechecking current account eligibility and actual revocation permission for every batch.
  The actor of the currently effective acceptance/continuation approval is checked;
  earlier approvers remain immutable history. Concurrent authority changes and commits
  MUST have a proved serial order.
- **GR-008**: Each bounded batch MUST commit native changes, exact progress and its audit
  together. A failed batch MUST NOT erase or misreport earlier committed progress.
- **GR-009**: Restart and competing workers MUST preserve exactly accounted committed
  progress. Unknown commit outcomes MUST be reconciled against durable state before further work.
- **GR-010**: Repeated identical submissions MUST recover the same accepted operation within
  the documented idempotency window, scoped to actor, action and exact target. Duplicate
  recovery MUST recheck current account/access and MUST NOT disclose another actor's job.
  Reuse for different intent MUST conflict. Submission
  retries MUST NOT create duplicate work or duplicate acceptance audit records.
- **GR-011**: Status MUST show committed authorization/token counts, pending/completed/stopped
  state and bounded safe errors. Unknown totals MUST NOT be shown as exact percentages.
  Reads MUST require current `applications.read` and `grants.revoke`, not identifier possession.
- **GR-012**: Authority-denied or failed work MUST require explicit fresh confirmation to
  continue its fixed remaining scope; no silent revival after authority restoration. Recovery
  MUST preserve approval history, completed batches and target restrictions. Process restart
  alone is not an authority failure and may resume work still eligible under GR-007.
- **GR-013**: The UI MUST distinguish acceptance from completion and explain that revoking
  stored authority cannot erase offline tokens or sessions owned by other applications.
- **GR-014**: Committed-entry revocation enforcement MUST meet the existing five-second
  platform boundary requirement where that entry is validated. This does not promise whole-job
  completion within five seconds or invent a supported resource audience.
- **GR-015**: The existing local-demo entry point MUST support the complete action/status
  journey. Keyboard, focus, light/dark and retained browser behavior remain required.

### Key Entities *(include if feature involves data)*

- **Revocation job**: Durable reference, exact target/scope, state, committed counts,
  bounded failure category, concurrency marker and progress position.
- **Recorded approval**: Immutable actor/intent/reason/time/correlation, admission proof
  metadata and explicit continuation linkage; no credential values.
- **Batch audit**: Atomic record of committed progress and outcome linked to job/approval.
- **Stored authorization and token**: Existing platform-owned native lifecycle records;
  revocation changes their lifecycle, not their ownership or protocol identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every accepted operation has a recoverable reference; every duplicate matching
  submission by the same currently authorized actor in the documented window returns
  that same operation, with no cross-actor disclosure.
- **SC-002**: At completion, 100% of eligible records in the fixed scope are revoked;
  zero unrelated records, credentials or application configuration fields are changed.
- **SC-003**: Sign-out and original MFA expiry do not interrupt eligible accepted work;
  account/permission loss prevents the next batch after that change takes effect.
- **SC-004**: Across injected failures and restart scenarios, reported counts match
  independently observed committed changes, with zero uncommitted successes reported.
- **SC-005**: Relevant platform validation rejects committed revoked entries within five
  seconds, measured from batch commit, while unaffected control entries remain usable.
- **SC-006**: The local demonstration completes submission, progress, recovery and result
  journeys through the ordinary application UI without scripts or manual database changes.

## Assumptions and dependencies

This draft makes no selection for GR-004. Explicit recovery with fresh approval and
current read-plus-revoke status access are proposed conservative rules for this slice,
not additional user answers. They remain subject to the packet's design review.

Depends on accepted feature019/020, approved FR-01/06/09/10/11/12/17/18 and NFR-05,
and the existing current-authority, audit, persistence and local-demo contracts.
Implementation details and evidence requirements are in [research.md](research.md).
The user-selected continuation rule is recorded above; planning readiness is tracked
in [checklists/requirements.md](checklists/requirements.md).

General grant catalogues, subject-wide revocation, application clone/delete, dormant
refresh activation, other client profiles, offline token erasure and relying-party
logout are outside this slice. Historical findings and manual/external release gates
remain open. No runtime acceptance or completed implementation is claimed.
