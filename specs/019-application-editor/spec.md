# Feature Specification: Application creation and editing

**Feature Branch**: `123-application-editor`
**Created**: 2026-10-29
**Status**: Initial-slice choices accepted; design and implementation in progress
**Input**: After PRs #118/#119 delivered the protected Applications list, the user requested “Please proceed with the next item.” The authorized visible-first sequence continues with application creation/editing and required dependencies.

## User Scenarios & Testing

### User Story 1 — Register a usable application (Priority: P1)

An authorized administrator opens Applications, creates a registration using the selected supported profile, reviews its effective configuration and completes a genuine sign-in using that registration. A saved record alone does not establish working integration.

**Why this priority**: It turns the delivered catalogue into a useful onboarding journey.
**Independent Test**: Create through the real workspace, observe the saved details/list, then complete the new client's real authorization-code journey on both supported databases.

**Acceptance Scenarios**:

1. **Given** current application-write authority, **when** valid supported configuration is submitted, **then** one registration and its sanitized audit intent commit together and the administrator sees its immutable client identity and effective settings.
2. **Given** that newly registered client, **when** the selected user completes required authentication and consent, **then** the client receives only its approved identity/access output, with no administrator roles or management authority.
3. **Given** a duplicate client identity or unsupported configuration, **when** creation is attempted, **then** the operation fails without partial registration or misleading success.
4. **Given** a credential-bearing create response is lost, **when** the operator returns to the application, **then** no stored plaintext can be retrieved or silently replayed; the supported explicit recovery action is described.

### User Story 2 — Edit configuration without losing another change (Priority: P1)

An authorized administrator opens sanitized application details, changes supported editable fields and saves using the version that was read. The client identifier remains immutable.

**Why this priority**: Safe edits are necessary to maintain a usable registration.
**Independent Test**: Two independently hosted editors submit the same version; exactly one update commits, the other receives a stale-version result, and the issuer honors the successful new configuration.

**Acceptance Scenarios**:

1. **Given** current authority and an unchanged version, **when** supported fields are saved, **then** application policy/native configuration and one audit intent commit atomically with a new version.
2. **Given** a missing or stale version, **when** an edit is submitted, **then** the request is rejected without overwriting the current record.
3. **Given** an immutable client-ID change or a system-owned registration, **when** a generic edit is attempted, **then** it is rejected and the existing protocol bindings remain unchanged.
4. **Given** a changed callback or narrowed permission, **when** old and new client requests are made, **then** the old configuration no longer grants issuance and the supported new configuration works.

### User Story 3 — Understand failures and protect sensitive input (Priority: P2)

Creation and editing retain the existing workspace's appearance, accessible forms and original sign-in lifetime, with clear validation, permission, stale-edit and ambiguous-outcome states.

**Why this priority**: The administrator must be able to test the actual journey locally and recover safely from ordinary failures.
**Independent Test**: Real browser journeys exercise keyboard navigation, error focus, narrow screens, both themes, permission removal, expiry and lost responses without exposing secrets in storage or evidence.

**Acceptance Scenarios**:

1. **Given** invalid fields, **when** submission fails validation, **then** accessible errors identify the fields without echoing credentials.
2. **Given** expired or revoked authentication/current permission, **when** a mutation is attempted, **then** no registration change commits and the UI offers the applicable sign-in or access-denied outcome.
3. **Given** an unavailable or interrupted dependency, **when** the result is ambiguous, **then** the UI does not automatically repeat the mutation or claim rollback without evidence.

### Edge Cases

Concurrent duplicate creates, stale/malformed/missing preconditions, ordinal identifiers on both providers, immutable ID edits, native-manager internal saves followed by audit failure, cancellation before/after commit, original credential expiry while waiting for locks, lost one-time output, reserved system identity/binding substitution, unsupported profiles/grants/scopes, unsafe callbacks, malformed/oversized JSON, cross-origin requests, antiforgery tokens from another browser generation, redirects/header substitution in the proxy, and sensitive-data disclosure through details/errors/logs/storage.

## Requirements

### Functional Requirements

- **FR-001**: Deliver real application creation and sanitized details/editing from the existing Applications workspace, with immutable unique client IDs. Product FR-01/19.
- **FR-002**: Offer only profiles and credential modes backed by implemented protocol behavior. The user selected a confidential web application using authorization code with PKCE S256 and a generated one-time shared secret. Other client profiles and asymmetric credentials remain separate. Product FR-02/03/09.
- **FR-003**: Enforce only approved scopes, audiences, claims and consent. The user selected OpenID sign-in with only a stable subject identifier and explicit consent. Ordinary API scopes/audiences and claim-policy catalogue remain separate. Product FR-04/08/09.
- **FR-004**: Implement the actual authentication prerequisites for the selected user journey. The user selected existing administrator accounts, fresh password/MFA for each sign-in and explicit consent. The application receives no administrator roles, permissions, source-session identity or management authority. Ordinary-user onboarding remains separate. Product FR-05/07/09/11.
- **FR-005**: Independently require current `applications.write` inside the mutation transaction; enforce the separately specified credential permission and recent-MFA rules for replacement. Specify initial credential issuance authorization explicitly in the selected profile plan. A read-only or stale bearer/session grants no mutation authority. Product FR-06/11.
- **FR-006**: Commit native application/product policy/version and sanitized audit intent atomically, using the same owned persistence context even when managers save internally. Record actor/action/target/outcome/UTC time/correlation without credentials or request-body dumps. Product FR-12/17.
- **FR-007**: Details expose an editable-resource ETag; updates require If-Match, returning 428 when missing and 412 when stale. Duplicate/domain conflicts return 409. Client identity cannot be renamed in place. Product FR-01/16.
- **FR-008**: Keep fixed system administrator/pilot registrations outside generic mutation, protecting both reserved identities and persisted native bindings. Preserve their existing protocols and all list read-only invariants. Product FR-01/11.
- **FR-009**: Protect browser mutations with current session-generation binding, exact same origin, native antiforgery validation, bounded JSON and fixed downstream routes. Keep OAuth credentials server-side; never automatically retry a mutation after ambiguous response loss. Product FR-11/16.
- **FR-010**: If shared-secret issuance is selected, hash through native managers, return newly issued material once with no-store, omit it from later reads/logs/audit/export/storage, and provide an explicit lost-output recovery path without plaintext replay. Specify enforced expiry and initial/replacement authorization before implementation. Product FR-03/12.
- **FR-011**: Publish additive versioned management/browser contracts and regenerate the real browser client, including preconditions, permissions, antiforgery and finite errors. Vendor documentation activation remains separate. Product FR-16.
- **FR-012**: Verify create → details → edit → actual new-client sign-in through native Aspire on both real providers and retained browsers. Preserve existing identities, provider migration histories and unchanged module coverage obligations; add meaningful mutation controls. Product FR-17/18/19/21/24.

### Key Entities

- **Managed application**: Native protocol identity plus the supported bounded product policy and one concurrency boundary; excludes system-owned registrations from generic writes.
- **Application edit version**: Opaque current version used to prevent lost updates across instances and provider implementations.
- **Audit intent**: Sanitized durable record committed with the corresponding mutation.
- **Credential metadata**: Public lifecycle information; never a recoverable shared secret or private key.
- **Client authentication/consent interaction**: A bounded request tied to the selected application, user and approved scope set; never a copy of administrator API authority.

## Success Criteria

- **SC-001**: A genuine administrator creates and edits a new registration and completes its selected sign-in journey on both supported databases.
- **SC-002**: Every accepted mutation has exactly one corresponding durable audit intent; injected audit failure leaves neither a partial native registration change nor a successful audit claim.
- **SC-003**: Two writers using one version produce exactly one successful update; stale and unauthorized operations produce no changes.
- **SC-004**: Required negative cases disclose no credential values or administrator claims and cannot alter system-owned registrations.
- **SC-005**: All defined UI states pass retained-engine functional, keyboard, reflow and automated accessibility checks; qualified manual and actual Windows acceptance remain explicitly separate.
- **SC-006**: Existing required identities remain covered, both-provider/browser checks and unchanged coverage/mutation gates pass, and independent reviews pass before normal squash delivery.

## Confirmed boundaries and decisions

The new request continues the approved creation/editing sequence; it does not activate refresh, production, certification or vendor documentation. On 29 October 2026 the user answered “all recommended”, explicitly selecting the three choices above. Implementation includes their necessary issuer, authentication, consent and management-write dependencies. Approved invariants already settle immutable IDs, current permissions, same-transaction audit, native manager ownership and version preconditions; those do not require repeated permission. Clone, disable/delete and unrelated full lifecycle remain separately tracked unless a selected prerequisite requires an explicitly documented bounded part. All six historical findings, human/Windows/Firefox/A-009 and public-release gates remain open.
