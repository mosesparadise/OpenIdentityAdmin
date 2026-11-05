# Feature Specification: Managed application lifecycle

**Feature Branch**: `126-application-lifecycle`
**Created**: 2026-11-04
**Status**: Scope selected; implementation in progress, acceptance unclaimed.
**Input**: Continue the next task from the delivered application-editor handoff: plan the next visible application lifecycle slice.

## Authority and scope

The approved v0.2 FR-01, FR-06, FR-09, FR-11, FR-12, FR-16–FR-19 and NFR-02,
NFR-05, NFR-08, NFR-11–NFR-14 govern this slice. See
[approval](../../docs/approval.md), [roadmap M04](../../docs/roadmap.md),
[approved management contracts](../../docs/approved-v0.2/04-data-api-contracts.md)
and the delivered [feature019](../019-application-editor/spec.md).
Ordinary work within that approved scope is already authorized.

This packet specifies disable/re-enable for registrations created by the existing managed
confidential-web profile. It does not claim that the controls are available on main.
Clone, delete, other client profiles, bulk grant revocation and dormant refresh remain separate work.

## Clarifications

### Session 2026-11-05

- Q: Should this slice stop new issuance or also include existing grant revocation? → A: The user
  selected “Stop new issuance; add grant revocation separately.” The reviewed issuance-only scope
  is selected; implementation and required tests may proceed under the existing delivery authority.

## User Scenarios & Testing

### User Story 1 — Stop an application's new sign-ins (Priority: P1)

An application administrator opens a managed application's details, reviews the effect of
disabling it, selects a reason, confirms and sees its disabled state.

**Independent Test**: Create a working registration, disable it through the ordinary application
screen, and demonstrate denial through a separate real issuer instance.

**Acceptance Scenarios**:

1. **Given** an enabled managed registration and current lifecycle permission,
   **when** its current version is disabled, **then** its state/version and one sanitized audit
   record change together while identity, callbacks and credential remain intact.
2. **Given** a committed disable, **when** a fresh sign-in, cached request, pending password/MFA/
   consent step or outstanding code is presented, **then** the issuer rejects further issuance.
3. **Given** a missing permission, stale version, system registration or unsupported registration,
   **when** disable is attempted directly or through the browser, **then** no lifecycle change occurs.

### User Story 2 — Restore new sign-ins deliberately (Priority: P1)

An application administrator re-enables a managed registration after resolving its problem.
The screen distinguishes enabled status from credential validity.

**Independent Test**: Re-enable a disabled registration and complete a new fresh-password/MFA/
consent journey with its still-valid credential; independently retry pre-disable artifacts.

**Acceptance Scenarios**:

1. **Given** a disabled registration, **when** its current version is re-enabled with current
   lifecycle permission, **then** it receives a new version and one audit event without replacing
   its credential or extending its expiry.
2. **Given** an old request, human continuation or authorization code,
   **when** it is retried after re-enable, **then** it remains invalid; only a new journey can succeed.
3. **Given** an expired credential, **when** the registration is re-enabled,
   **then** credential-dependent issuance remains denied and the UI still shows the expiry.

### User Story 3 — Understand failures and recover safely (Priority: P2)

An administrator can distinguish a stale screen, lost permission and an uncertain response,
then reload current state before deciding what to do next.

**Independent Test**: Exercise a concurrent edit and lost response through the actual BFF/UI,
then inspect persisted state and audit independently.

**Acceptance Scenarios**:

1. **Given** competing mutations from the same version, **when** they contend,
   **then** at most one transition commits; the other reports a precondition failure.
2. **Given** an audit/storage failure before commit, **when** lifecycle mutation fails,
   **then** the original state/version remain usable and there is no partial accepted audit.
3. **Given** an uncertain response or expired browser authority,
   **when** the UI handles it, **then** it neither reports success nor automatically retries,
   and offers a deliberate fresh read when permitted.

### Edge Cases

- Repeating the same state with the current version is a conflict with no new version or audit.
  Reusing an older version is a stale-precondition failure, even if state now matches.
- Both state transitions invalidate earlier continuations. Neither resets history nor renews
  consent, identity proofs, credential expiry or browser authority.
- A disabled registration remains visible, editable and eligible for explicit credential
  replacement under those actions' existing separate permissions. Neither action re-enables it.
- Issuance linearizes at its governance-protected commit. Its sealed response may arrive after
  a later disable commits; the UI must not claim that earlier issuance never happened.
  Issuance ordered after disable must fail closed. A different enabled registration remains usable.
- Native-only and system-owned registrations remain protected even after name/profile drift.
- Cancellation, clock rollback, deadline equality, database outage and unknown commit outcome
  retain the existing bounded, fail-closed behavior.

## Requirements

### Functional Requirements

- **FR-001**: Expose explicit disable and re-enable actions and current lifecycle state for supported
  managed registrations. Protect built-in/system registrations and never auto-adopt native-only records.
- **FR-002**: Require the existing current `applications.disable` permission and persisted MFA
  freshness within five minutes for either transition, rechecked throughout the owned operation
  without renewing the original deadline. Read/write/credential permissions alone are insufficient.
- **FR-003**: Require explicit confirmation, a bounded selected reason and the current application
  version. Reject stale, missing, malformed and same-state requests without mutation.
- **FR-004**: Commit state, a monotonically advanced application version, and exactly one sanitized
  actor/action/target/reason/time/correlation audit together; independently verify persisted values.
- **FR-005**: Preserve native identity, profile, callbacks, credential hash/revision/expiry and
  existing migration histories. Preserve ordinary edit/replacement behavior while disabled.
- **FR-006**: Deny new admission and further issuance for disabled registrations at every applicable
  current managed issuer boundary, including lock waits and the final issuance commit. Preserve
  existing sealed-response publication checks; a response committed before disable may arrive later.
  Database failure denies new admission/issuance. Other enabled registrations remain usable.
- **FR-007**: Prevent pre-transition cached requests, ceremonies, consent and codes from becoming
  usable after either transition. Re-enable requires a fresh journey and never repairs expired credentials.
- **FR-008**: Define and prove both legal orderings of issuance versus lifecycle mutation across
  independent instances. A request begun after committed disable observes it; maximum published
  enforcement bound remains five seconds. Do not relabel earlier completed issuance as prevented.
- **FR-009**: Retain independent API authorization, same-origin BFF protection, current session/
  anti-forgery generation, fixed routes, bounded input, original deadlines and one-send semantics.
- **FR-010**: Display the effects accurately before confirmation: new issuance stops; this operation
  does not bulk-revoke stored grants, erase relying-party sessions or invalidate offline-validated
  tokens already issued. Do not infer disabled-client enforcement from a token's wrong audience.
- **FR-011**: Provide accessible keyboard/focus, loading, confirming, saving, conflict, denied,
  unavailable and uncertain-result states in existing light/dark responsive application details.
  Suppress late responses after navigation, logout, generation change or authority expiry.
- **FR-012**: Preserve both database providers equally with fresh-install and populated-upgrade
  tests, real contention, atomic failure tests and an independently observed issuer journey.
- **FR-013**: Extend versioned API/BFF contracts and generated clients, exact test inventories,
  coverage/mutation evidence and ordinary Aspire demos without weakening existing regression gates.
- **FR-014**: Record effects, failure recovery, migration/rollback limits and acceptance evidence.
  Keep manual Windows/accessibility, Firefox, A-009, six historical findings and release gates open.

### Key Entities

- **Managed application**: stable identity, lifecycle state and advancing aggregate version,
  alongside independently managed credential validity.
- **Lifecycle decision**: explicit desired state, reason and expected version from a current administrator.
- **Audit intent**: one immutable accepted transition with sanitized provenance and selected reason.
- **Pending sign-in authority**: request/ceremony/code tied to the application version that admitted it.

## Success Criteria

- **SC-001**: Both provider demos show create → working sign-in → disable → denied new issuance →
  re-enable → working fresh sign-in, while the prior request/code remains rejected.
- **SC-002**: Every permission/version/system-protection, race and injected rollback scenario has
  the specified exact persisted outcome on both providers, with no losing mutation or accepted audit.
- **SC-003**: Independent instances enforce committed disable within five seconds; completed
  pre-disable issuance, stored grant state and offline-token limitations are reported separately.
- **SC-004**: Retained Chromium/WebKit scenarios cover both themes, keyboard/focus and failure
  recovery; required contract, inventory, coverage and mutation gates pass on the exact candidate.
- **SC-005**: The ordinary Aspire launch remains sufficient. Documentation clearly distinguishes
  internal automated acceptance from the outstanding manual and external release gates.

## Assumptions and dependencies

- Re-enable is the reversible counterpart of disable, governed by the same lifecycle permission;
  it is never a general editor field. Explicit confirmation applies to both actions.
- Reasons are a closed selection (maintenance, security, configuration), not free text that could
  contain credentials or personal data. The wire/audit design is detailed in the contract.
- The user selected issuance disable/re-enable on 5 November 2026. Bulk grant revocation remains
  a separate follow-on requiring durable jobs, separate grant authority, bounded progress,
  restart/concurrency behavior and partial-failure acceptance.
- Accepted feature019 remains the regression baseline; no accepted implementation is restarted.
