# Feature Specification: Administrator foundation

**Feature Branch**: `018-administrator-foundation`

**Created**: 2026-10-12

**Status**: Increment 1 merged in PR #21; increment 2 detailed design reviewed; account implementation and increment 3 remain pending

**Input**: Proceed to the next roadmap item, M02-A: protected operator-supplied first-administrator bootstrap, minimum MFA/recovery and permissions. Preserve approved Features/composition conventions, comprehensive tests, independent review and squash delivery.

## User Scenarios & Testing

### User Story 1 — Initialize the first administrator safely (Priority: P1)

A trusted deployment operator initializes exactly one first administrator through a deliberate, private, one-time operation using operator-supplied credentials. Normal startup never creates a privileged user. Initialization leaves the account restricted to enrollment until it demonstrates the required authentication factors.

**Why this priority**: Every later administration journey needs an accountable starting identity; a default password or repeatable privilege-granting operation would compromise all subsequent controls.

**Independent Test**: On a fresh installation, run protected initialization, verify the intended account and durable completion record, repeat and race the operation, and prove that password-only access still cannot authorize administration or use the existing pilot as a bypass.

**Acceptance Scenarios**:

1. **Given** an eligible uninitialized installation and valid operator inputs, **when** initialization completes, **then** exactly one intended account, intended initial permission bundle, completion record and sanitized audit intent commit together; the account is enrollment-only.
2. **Given** completed initialization, **when** the same operation repeats, **then** it reports the prior completion without resetting credentials, adding privilege or creating another account. A conflicting request is rejected without mutation.
3. **Given** two concurrent initialization requests, **when** both contend for the installation, **then** at most one commits; the loser cannot overwrite the winner or promote a pre-existing account.
4. **Given** invalid input, cancellation, persistence/audit failure or process interruption, **when** initialization cannot complete, **then** it reports a safe failure and leaves no partial privileged identity. An ambiguous commit outcome is reconciled against the durable completion record before any repeat operation.
5. **Given** an enrollment-only account, **when** password-only login, direct HTTP, pilot-client authorization or a forged role/permission claim attempts privileged access, **then** no administrator session or token is issued.
6. **Given** ordinary runtime startup, **when** bootstrap input is absent, **then** no administrator is created, promoted or reset. Existing synthetic pilot provisioning remains separate and never grants administrator privileges.

### User Story 2 — Establish and recover administrator authentication (Priority: P1)

The intended administrator proves possession of the required factors before gaining an eligible identity session. Enrollment and recovery are short-lived, single-use, bound to the initiating account and browser, and provide no unrestricted administrative authority.

**Why this priority**: Bootstrap is not usable administrator authentication; enrollment, loss of an authenticator and recovery must not create alternate password-only access paths.

**Independent Test**: Exercise real browser enrollment, login, failed/replayed factor verification and recovery on both supported database providers, including simultaneous requests across instances.

**Acceptance Scenarios**:

1. **Given** a valid restricted enrollment interaction, **when** the administrator proves the required factor, **then** activation and its audit intent commit atomically and the enrollment interaction cannot be reused.
2. **Given** an eligible administrator, **when** password and required factor verification succeed, **then** a new identity session records the actual authentication method and verification time. Password-only or stale-factor proof cannot satisfy recent-MFA policy.
3. **Given** an expired, swapped, replayed, brute-forced or cross-origin enrollment/login/recovery interaction, **when** it is submitted, **then** it is rejected without granting a session or consuming another browser's valid interaction.
4. **Given** a valid recovery code and the required account proof, **when** recovery succeeds, **then** the code is consumed exactly once, prior relevant sessions are invalidated, and only a restricted authenticator-recovery interaction is granted. Administrative access requires successful replacement-factor verification.
5. **Given** an unknown, disabled or locked account, an invalid code or unavailable dependency, **when** authentication/recovery is attempted, **then** the public response does not disclose account existence and authorization fails closed.
6. **Given** an authenticator change or recovery-code regeneration, **when** it is authorized, **then** obsolete proofs/codes cease to work and a sanitized audit records the transition; secrets never enter logs, tokens, screenshots or diagnostic artifacts.

### User Story 3 — Evaluate current authority and preserve recoverability (Priority: P1)

Administrative authority comes from current enabled-account state and current allowlisted permission bundles. A transactional governance boundary prevents concurrent changes from silently removing every usable platform administrator. Sensitive transitions require separate permission and recent MFA.

**Why this priority**: Durable role labels alone neither authorize real actions nor prevent two administrators from simultaneously removing each other's authority.

**Independent Test**: Exercise policy evaluation and governance transitions against real stores, including competing actors, stale versions, removed permissions, lockout/disable/recovery states and injected audit failures. Real Admin API bearer/BFF acceptance remains M02-B–D.

**Acceptance Scenarios**:

1. **Given** each initial role bundle, **when** current authority is evaluated, **then** only its allowlisted permissions are returned; unknown permissions, user-supplied claims and stale token roles cannot elevate authority.
2. **Given** permission removal or account ineligibility, **when** current authority is evaluated subsequently, **then** it reflects committed state or denies if that state cannot be read.
3. **Given** two administrators concurrently attempting changes that would remove the last usable administrator, **when** both execute, **then** a common governance boundary serializes the decisions; at least one safe authorized recovery route remains.
4. **Given** insufficient permission, missing/replayed/stale MFA or a stale concurrency version, **when** a privileged transition is attempted, **then** it is denied with no mutation or successful audit intent.
5. **Given** account security must be enforced, **when** lockout or compromise response makes an administrator unusable, **then** last-administrator protection never bypasses lockout/MFA or preserves an unsafe session. Recovery follows the documented authorized route; bootstrap is not reopened.

### Edge Cases

- Existing normalized username/email collision, existing privileged memberships without a completion record, changed identity inputs after commit, accidental production use of synthetic pilot seed data.
- Connection loss before commit versus commit success with lost process output; bounded duplicate invocation versus automatic retries of sensitive operations.
- Concurrent bootstrap, enrollment confirmation, TOTP reuse, recovery redemption, authenticator replacement and role/account mutation on different nodes.
- Clock-window boundaries, factor replay within the accepted window, failed-attempt budgets, session fixation, forged recent-MFA metadata and security-stamp changes.
- All authenticators lost, the sole administrator locked/disabled, backup restoration and missing protection keys. A general emergency reset is not implicitly authorized by initial bootstrap.
- Accessible error/focus/reflow/theme states without sensitive enrollment material in retained browser captures.

## Requirements

### Functional Requirements

- **ADMIN-001**: Initialization MUST require explicit trusted-operator action and protected operator-supplied credentials, with no default password, public bootstrap endpoint, startup seed or automatic existing-user promotion. Trace: FR-18, FR-07, NFR-08.
- **ADMIN-002**: Initialization MUST atomically persist account/initial authority, one-time completion state and sanitized audit intent; competing and repeated requests MUST not add privilege or reset credentials. Trace: FR-06, FR-12, FR-17, FR-18.
- **ADMIN-003**: Bootstrapped accounts MUST remain enrollment-only until required factor proof completes; no existing login/client path may bypass this restriction. Trace: FR-07, FR-09, FR-11.
- **ADMIN-004**: Administrator authentication MUST require the selected MFA method with replay prevention, bounded attempts, expiry, account/browser binding and truthful authentication time/method. Trace: FR-07, NFR-01, NFR-08.
- **ADMIN-005**: Recovery MUST use single-use proof, resist enumeration, invalidate relevant sessions and produce only a restricted recovery interaction until the replacement factor is proven. Trace: FR-07, FR-12, NFR-05, NFR-08.
- **ADMIN-006**: Enrollment, replacement, activation, recovery and privileged authority changes MUST commit their sanitized audit intent with the state transition. Failure MUST not leave partially privileged state. Trace: FR-12, FR-17.
- **ADMIN-007**: Initial role bundles MUST follow the approved personas. Current permission evaluation MUST reject unknown/reserved authority and fail closed on unavailable state. Trace: FR-05, FR-06, FR-11.
- **ADMIN-008**: Privileged mutations MUST require separate permission, recent verified MFA and concurrency checks; shared governance MUST prevent concurrent last-administrator removal. Trace: FR-05, FR-06, NFR-05.
- **ADMIN-009**: Both database providers MUST pass the same initialization, upgrade, restart, concurrency, failure and recovery semantics. Trace: FR-17, FR-18, FR-24, NFR-02.
- **ADMIN-010**: Account UI MUST use the approved accessible light/dark design and preserve password-manager, keyboard, responsive and error-state behavior. Trace: FR-19, NFR-09, NFR-12.
- **ADMIN-011**: Every implemented boundary MUST have positive, deny, failure/concurrency and permanent regression evidence, with unchanged coverage/no-skips rules and relevant independent review. Trace: FR-21–23, NFR-11.
- **ADMIN-012**: This feature MUST NOT expose the Admin API/BFF, documentation viewers or unrestricted management operations before their own security acceptance. Trace: FR-11, FR-16; roadmap M02-B–D and M04-D.

### Key Entities

- **Bootstrap completion**: Immutable installation-level record identifying the completed initialization operation and intended account, without retaining its password or recovery secrets.
- **Administrator enrollment state**: Restricted, active or recovery-required state, with a version and the security prerequisites for transition.
- **Permission bundle**: Named, versioned, allowlisted authority associated with an approved persona; role membership alone does not prove MFA.
- **Governance state**: Shared concurrency boundary protecting decisions affecting usable platform administrators and durable bootstrap closure.
- **Factor proof / restricted interaction**: Expiring account/browser-bound proof, replay state, attempt budget, authentication time and permitted next transition.
- **Security audit intent**: Actor/action/target/outcome/time/correlation evidence committed with the mutation, excluding credential/factor material.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Concurrent initialization yields exactly one intended initial administrator and one successful completion; a repeat operation changes no credentials or authority.
- **SC-002**: Every required negative authentication/recovery scenario issues zero unrestricted administrator sessions or tokens; cross-node replay permits at most one successful use of single-use proof.
- **SC-003**: Each committed security transition has one matching successful audit intent; injected rollback produces neither committed mutation nor successful intent.
- **SC-004**: The same required persistence scenarios pass on both providers without skips, with all retained module coverage floors met.
- **SC-005**: All implemented critical browser journeys pass in Chromium, Firefox and WebKit in light/dark and narrow/desktop layouts; automated checks do not substitute for outstanding human accessibility acceptance.
- **SC-006**: Current-policy and governance tests reject stale/insufficient authority and demonstrate no unguarded concurrent last-administrator removal. Full five-second API/BFF enforcement remains explicitly allocated to M02-D.

## Assumptions and dependencies

The approved v0.2 baseline and subsequent user instructions remain authoritative. This draft proposes no production deployment, certification claim, default credentials or Duende migration. Passkeys remain an R1 obligation even if their implementation stays in M05. M02-B/C own BFF protocol/session behavior; M02-D owns real bearer/current-permission acceptance. The M01-F vendor gate is unchanged.

The user confirmed on 12 October 2026:

- **D1 — Delivery**: three sequential reviewed PRs: bootstrap/storage, TOTP/recovery, then current-permission/governance enforcement.
- **D2 — MFA scope**: password plus TOTP and recovery codes now; passkeys remain in M05. Recovery grants only restricted factor replacement.
- **D3 — Structure**: preserve existing projects; Migrator `Features/Bootstrap/Initialize`, AuthServer `Features/Accounts`, Core/Infrastructure `Features/Identity`, plus the dedicated `tests/aspire/IdentityPlatform.Administrator.Tests` project under the Aspire solution folder.

See [decision brief](decision-brief.md), [research](research.md) and [plan](plan.md). Increment 1 is merged. The second increment is detailed in [MFA design](mfa-design.md); T016 completed independent architecture/security/test design review; see verification.md. Account code and runtime evidence remain T017/T018. Third-increment governance still requires T019 before its code begins.
