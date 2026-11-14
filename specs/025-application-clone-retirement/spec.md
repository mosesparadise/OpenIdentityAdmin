# Feature Specification: Application cloning and permanent retirement

**Feature Branch**: `148-application-clone-delete`
**Created**: 14 November 2026
**Status**: Selected for implementation; design and verification pending
**Input**: Continue the next roadmap item after feature024. Complete M04-A's remaining
clone and controlled-delete journeys as separately reviewed increments. Product
FR-01/03/04/08/10/11/12/16/17/19/21/24; NFR-01/02/05/08/09/10/11/14.

## Clarifications

### Session 2026-11-14

- Q: Should Delete application permanently retire it with retained history, or physically
  delete it after references can safely be cleared? → A: Permanent retirement with
  history retained. Stop issuance, revoke grants, hide the normal list entry, preserve
  history and reserve its client ID.

## User Scenarios & Testing

### User Story 1 — Clone a managed application (Priority: P1)

An authorized administrator reviews a managed application's reusable configuration
and creates a separate registration with a new client ID, display name and credential
expiry. The new application has a fresh one-time secret and no inherited grants,
consent, sessions or history. It begins disabled until explicitly enabled.

**Why this priority**: Reuse an established integration configuration without copying
its credentials or authority. This is independently deliverable before retirement.
**Independent Test**: Clone through the administrator UI, enable the new registration,
and complete a fresh real sign-in using its own credential and consent.

**Acceptance Scenarios**:

1. **Given** a current supported source, **when** the administrator reviews the exact
   callbacks and assigned scopes and confirms new identity/name/expiry, **then** one
   new disabled application is created with those reviewed settings and a newly
   generated secret shown once; the source remains unchanged.
2. **Given** changed source configuration, invalid/disabled assigned policy, duplicate
   destination ID, insufficient current permission or stale MFA, **when** cloning is
   submitted, **then** no new application or success audit is committed.
3. **Given** a successful clone, **when** its one-time output is lost, **then** the UI
   explains explicit secret replacement rather than automatically repeating cloning
   or replaying secret material.
4. **Given** a clone subsequently enabled by an authorized administrator, **when** it
   signs in, **then** its own credential, new password/MFA and explicit consent are
   required; the source credential and existing source grant cannot authenticate it.

### User Story 2 — Permanently retire an application (Priority: P1)

An administrator chooses Delete application, reviews that it means permanent
retirement with retained history, and confirms the exact target/version and reason.
The application immediately becomes permanently unavailable for new issuance, its
grants are revoked through durable work, and it disappears from the normal list.

**Why this priority**: Safely remove an integration from service without silently
deleting referenced security evidence or allowing its client identity to be reused.
**Independent Test**: Retire a genuinely used application, observe durable revocation,
and demonstrate denial of old requests/codes and new sign-in on both providers.

**Acceptance Scenarios**:

1. **Given** an eligible managed application, **when** retirement is confirmed with
   current disable and grant-revocation permissions and fresh MFA, **then** permanent
   retirement, issuance disablement, revision advancement, audit and durable revocation
   admission commit atomically; a failure commits none of them.
2. **Given** a retired application, **when** normal list/search/page queries run, **then** it
   is excluded before paging; existing history/status links remain usable for authorized
   administrators and the client ID remains reserved.
3. **Given** retirement races with issuance or an administrator mutation, **when** the
   operations finish, **then** their serialized result respects the irreversible
   boundary; no later re-enable, edit, secret replacement, assignment or clone-source
   operation can restore or copy the retired registration.
4. **Given** an already-issued token validated offline elsewhere, **when** retirement
   completes, **then** the UI explains that offline access can persist until expiry
   and remote application sessions are not erased; current-authority UserInfo denies.

### User Story 3 — Observe and recover retirement revocation (Priority: P2)

An administrator can follow pending/stopped/completed grant revocation for a retired
application and explicitly resume eligible stopped work through a separately navigable bounded Retired applications view, including
after reload/re-login or response loss. Retirement is never undone
by worker failure, lost permission, logout, MFA expiry or an ambiguous response.

**Why this priority**: Deletion must remain observable and safe across interruptions.
**Independent Test**: Stop/restart the worker and remove/restore actor permission while
work is pending; observe permanent retirement and controlled recovery without duplication.

**Acceptance Scenarios**:

1. **Given** a confirmed retirement, **when** the browser logs out or its MFA ages,
   **then** the approved grant job may continue under the established account and
   revocation-permission rechecks; it cannot clear retirement or re-enable issuance.
2. **Given** stopped revocation, **when** an eligible administrator explicitly continues
   the exact current job with fresh MFA, **then** bounded work resumes with new recorded
   approval and no duplicate retirement or discarded prior evidence.
3. **Given** an incomplete existing grant job, **when** retirement is requested,
   **then** the request is rejected with a clear action to finish/recover that job;
   it neither silently adopts its approval nor creates competing grant work.

### Edge Cases

Protected system/pilot/unmanaged registrations; stale or substituted source/version;
disabled or expired-credential clone source; retired clone source; changed or missing
scopes/audiences; absent scope assignment; independent current permissions; lost fresh
MFA during waits; original publication deadline; duplicate identity on both collations;
grant-job replay/key expiry; concurrent retirement/edit/enable/issuance; retained native
tokens/interactions/policy references; audit or post-save mutation faults; unknown
commit/disposal results; worker cancellation/permission loss; keyboard/focus and
light/dark/narrow layout; missing/stale history navigation; no browser secret retention.

## Requirements

### Functional Requirements

- **FR-001**: Support the existing managed confidential web profile only. Exclude
  protected system/pilot and unmanaged registrations from both new mutations.
- **FR-002**: Cloning copies only the reviewed supported profile, exact callbacks and
  explicit additional scope configuration. Require a new unique immutable client ID,
  explicit display name and future credential expiry. Never copy credentials/hashes,
  grants, consent, tokens, sessions, audit history or lifecycle approval.
- **FR-003**: Generate a fresh one-time clone secret through the established credential
  mechanism; initialize the clone disabled with its own independent revision/history.
  Enabling remains a separately authorized existing action.
- **FR-004**: Bind clone intent to the source's exact observed version and current
  supported state. Revalidate every assigned scope/audience before committing; reject
  the entire clone on current ineligibility or drift after the owned catalogue snapshot
  rather than silently omitting scopes. Eligible policy edits before that snapshot are allowed.
- **FR-005**: Cloning requires current application-write authority and fresh MFA. Copying
  configuration does not grant catalogue-edit authority. Preserve independent API/BFF,
  request-forgery, original-deadline, atomic-audit and safe publication guarantees.
- **FR-006**: Delete means irreversible permanent retirement, not physical deletion or
  legal erasure. Require explicit target/version/reason confirmation, current application
  disable and grant-revocation permissions, and fresh MFA at admission.
- **FR-007**: Atomically persist retirement, disable issuance, advance the application
  version, record sanitized audit and admit durable grant revocation. Reject competing
  incomplete grant jobs; do not reuse their approval implicitly.
- **FR-008**: Retired registrations cannot be enabled, edited, have secrets replaced,
  receive assignments or be used as clone sources. Retain native identity and policy,
  reserve the client ID, preserve grant/consent/audit references and existing retention.
  No physical purge or new retention policy is introduced.
- **FR-009**: Exclude retired applications from normal list/search before pagination.
  Preserve authorized bounded detail/history/status and explicit grant-recovery access,
  including a separate Retired applications view discoverable after reload/re-login or lost response.
- **FR-010**: Keep retirement permanent throughout pending/stopped/completed revocation.
  Preserve the established confirmed-job continuation after logout/MFA expiry while
  checking current actor account and revocation permission for worker batches. Explicit
  continuation requires current authorization and fresh MFA; completion never re-enables.
- **FR-011**: Enforce retirement at all current-authority issuance, exchange, consent
  and UserInfo boundaries, including serialized in-flight work. Distinguish offline
  token validity until expiry and remote sessions from current issuer authority.
- **FR-012**: Provide explicit confirmation, loading, stale/denied/ambiguous-outcome,
  secret-loss, retired and revocation recovery states with keyboard/focus and responsive
  light/dark presentation. Do not automatically retry credential or destructive actions.
- **FR-013**: Preserve both relational providers and existing migration histories,
  supported profiles, native manager ownership, all prior test identities and unchanged
  coverage floors. Verify meaningful negatives, races, failure/recovery and real browser
  journeys with actual provider persistence and representative causal mutations.
- **FR-014**: Deliver clone and retirement as separately reviewed normal squash
  increments, with original exact-head evidence reconciliation, coherent tasks/roadmap/
  handoff updates and explicit remaining gates. No production or refresh activation.

### Key Entities

- **Clone intent**: Exact source/version and new destination identity, name, expiry and
  confirmation; no copied secret or user grant.
- **Retirement intent**: Exact target/version, actor, reason, request identity and
  immutable destructive approval.
- **Retired application**: Irreversible retained identity/policy, disabled issuance,
  reserved client ID and linked durable grant work/history.
- **Revocation status**: Existing bounded progress, approval and recovery information,
  separate from the already committed permanent retirement decision.

## Success Criteria

- **SC-001**: An administrator clones a supported application through the ordinary
  workspace, enables it and completes a fresh integration sign-in using only its own
  newly issued credential and consent.
- **SC-002**: Every retired application is absent from normal listings, remains
  unmodifiable and cannot regain issuer authority; its identity/history stay reserved.
- **SC-003**: Pending, stopped and resumed retirement revocation are observable after
  interruption without duplicate destructive approval or loss of audit history.
- **SC-004**: Both supported databases and retained browsers pass all selected real
  journeys and failure/race controls; original evidence and independent reviews pass
  before an increment is marked delivered.
- **SC-005**: Previously accepted application/sample journeys remain available, and
  no existing coverage floor, assertion, deadline or external gate is weakened.

## Assumptions and boundaries

The user's next-roadmap instruction selects M04-A continuation; the explicit retirement
answer resolves deletion semantics. Cloning omits existing credentials but generates
a fresh credential for the existing profile; it does not introduce a credentialless
profile. The disabled initial clone is a deliberate review-before-activation default.
Windows/manual, Firefox/A-009, six historical findings, reliability qualifications,
refresh/vendor/production/public-release gates remain open. No historical diagnostic
approval is renewed. Application-user onboarding and other client profiles stay separate.
