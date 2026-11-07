# Feature Specification: Scope and audience catalogue

**Feature Branch**: `135-scope-audience-catalogue`
**Created**: 7 November 2026
**Status**: Selected for implementation; acceptance pending
**Input**: Implement M03-A–M03-C, including UI, dependencies and tests. The user selected consented email and email_verified from the current Identity account.

## User Scenarios & Testing

### User Story 1 — Maintain scopes and audiences (Priority: P1)

An authorized administrator uses the existing workspace to search, create, inspect,
edit and delete API audiences and identity/API scopes. Configuration shows its
actual enabled state, immutable protocol identity, references, version and claim policy.

**Independent Test**: Through native Aspire, create an audience and two scope kinds,
edit using current versions, reject a stale concurrent edit and reject deletion while
referenced. Observe exactly one sanitized audit per committed mutation on each provider.

**Acceptance Scenarios**:

1. **Given** current read/write permission, **when** a valid entry is created or edited,
   **then** native configuration, product policy and audit commit together.
2. **Given** a stale version, missing permission or reserved identity, **when** a write is
   attempted, **then** no mutation commits and the UI reports the specific safe failure.
3. **Given** a referenced scope or audience, **when** deletion is requested, **then** it
   is blocked without rewriting applications, grants or existing protocol identities.

### User Story 2 — Assign deliberate application scope policy (Priority: P1)

An administrator assigns enabled catalogue scopes to an existing managed application.
Its genuine password/MFA/consent journey displays the requested permitted scopes and
claim disclosure; the user can decline. Existing OpenID-only applications stay unchanged.

**Independent Test**: Assign scopes, sign in, consent, inspect exact granted scopes,
audience and permitted claims; change policy between consent and exchange and prove
stale decisions cannot gain authority or publish credentials.

**Acceptance Scenarios**:

1. **Given** enabled scopes assigned to an eligible application, **when** an eligible
   user requests and consents to them, **then** only their intersection is granted.
2. **Given** multiple API audiences, an unassigned/disabled scope or an invalid mapping,
   **when** authorization or exchange is attempted, **then** it fails closed.
3. **Given** changed application/scope/audience or claim policy, **when** an earlier
   pending decision or code is used, **then** it is rejected; fresh consent is required.
4. **Given** consented email disclosure, **when** claims are released, **then** only
   current email and its verification state are included; missing email emits neither.

### User Story 3 — Verify real tokens and current UserInfo (Priority: P1)

Application developers receive genuine scope-bound tokens and query UserInfo to obtain
only the matching subject and consented current identity claims. The catalogue is
useful through actual protocol behavior, not display metadata alone.

**Independent Test**: Validate signatures, issuer, type, subject, audience and claims;
exercise UserInfo GET/POST with genuine tokens on both providers. Wrong/expired/revoked
credentials, disabled accounts/applications/policies and machine tokens fail closed.

**Acceptance Scenarios**:

1. **Given** a valid managed access token, **when** UserInfo is called, **then** the exact
   associated subject and only currently permitted consented human claims are returned.
2. **Given** a token for another profile, an ID token, revocation or current denial,
   **when** UserInfo is called, **then** no human profile is disclosed.
3. **Given** a fixed administrator BFF login, **when** discovery advertises UserInfo,
   **then** login remains functional and no optional UserInfo request is sent by the BFF.

### Edge Cases

Ordinal duplicate identifiers; reserved system/special scopes and audiences; duplicate
or oversized scope/claim arrays; conflicting resource mappings; referenced deletion;
stale ETags; assignment versus deletion races; policy mutation during cached request,
consent, code exchange or UserInfo; forged private snapshot data; native-manager cache
staleness across processes; lost responses; audit/commit/disposal failure; original
request deadlines; missing/unconfirmed/changed email; scope escalation; wrong token
kind/audience/client; secret/PII exposure through logs, errors and browser storage.

## Requirements

### Functional Requirements

- **FR-001**: Provide searchable, paged audience and identity/API scope CRUD with immutable
  ordinal protocol identifiers and clear current state. Product FR-04/19; M03-A.
- **FR-002**: Independently enforce current scopes.read/write and audiences.read/write,
  recent MFA for mutations, original authorization deadlines and same-transaction audit.
  Product FR-06/11/12/17; M03-A.
- **FR-003**: Use explicit editable-resource versions; missing/stale preconditions reject.
  Block referenced deletion atomically with reference creation. Product FR-04/16; M03-A.
- **FR-004**: Native scope resources and application scope permissions are the authoritative
  protocol representation. Product policy must not silently adopt system/unmanaged rows.
  Product FR-04/17; M03-A/B.
- **FR-005**: An API scope has one enabled audience; an identity scope has none. Grant at
  most one API audience per authorization, resolved only from approved mappings. Product
  FR-04/09/11; M03-B.
- **FR-006**: Define a finite claim catalogue: stable opaque sub; consented email and
  email_verified from the current Identity account when email exists. No username,
  administrator roles/permissions, arbitrary user claims or internal credentials/state.
  Product FR-04/11; M03-B. Email is not an API authorization claim.
- **FR-007**: Assign additional scopes explicitly with application write authority and
  version checks; preserve all existing fixed/system and OpenID-only flows. Product
  FR-01/04/16; M03-B.
- **FR-008**: Grant only requested ∩ assigned ∩ enabled ∩ eligible ∩ consented scopes.
  Bind immutable policy decisions through request/consent/code/exchange; stale decisions
  fail closed and cannot silently enlarge consent. Product FR-04/08/09/11; M03-B/C.
- **FR-009**: Let the protocol library issue and validate all credentials; configure exact
  claim destinations and audience. Preserve durable consumption, replay denial, original
  deadlines and response publication guarantees. Product FR-09/11; M03-C.
- **FR-010**: Implement UserInfo GET/POST with exact subject, native access-token validation,
  current account/application/authorization/scope/audience checks and scope-filtered
  identity claims. Reject machine and unrelated fixed-profile tokens. Product FR-04/09;
  M03-C. Administrator BFF still sends no UserInfo request.
- **FR-011**: Keep browser credentials server-side; use current BFF session, native CSRF,
  fixed routes, strict bounded JSON and no automatic write retries. Regenerate compatible
  versioned contracts from the actual endpoints. Product FR-11/16/19.
- **FR-012**: Deliver responsive light/dark, keyboard/error/empty/loading/permission/stale
  states and native Aspire walkthroughs; retain manual/Windows acceptance separately.
  Product FR-19/21/24.
- **FR-013**: Preserve both provider migration histories and populated data, plugin
  boundaries, all existing required tests and module floors; add real provider/browser,
  race/failure, crypto/UserInfo and meaningful mutation evidence. Product FR-17/18/24.

### Key Entities

- **Audience**: Immutable resource identity, display/owner metadata, enabled state/version.
- **Scope**: Immutable native name/resources plus kind, enabled state, consent description,
  finite disclosure rules and version.
- **Application scope assignment**: Explicit native permissions, versioned with the application.
- **Consented policy snapshot**: Bound requested/granted scope policy for one human journey;
  never an authority source without matching current persisted state.

## Success Criteria

- **SC-001**: Real UI → catalogue → application assignment → password/MFA/consent → token
  → UserInfo journeys pass on PostgreSQL and SQL Server.
- **SC-002**: Every accepted mutation has exactly one atomic sanitized audit intent;
  stale/unauthorized/referenced-delete and injected precommit failures make no change.
- **SC-003**: Independent token validation observes exactly the approved scope, single
  audience and claim set, with no internal/admin claims or unwanted personal data.
- **SC-004**: Policy-change, replay, revocation and fault races cannot publish stale success;
  unrelated and previous OpenID-only applications remain usable.
- **SC-005**: Required tests, original coverage floors, mutation controls and independent
  reviews pass at the final head before ordinary squash delivery.

## Assumptions and boundaries

Approved PRD v0.2 and the user selection authorize this batch; no additional approval is
needed for settled implementation choices. Existing managed-client password/MFA and
explicit consent are reused; no ordinary-user onboarding, machine grant, refresh,
production, vendor documentation activation or external certification is introduced.
All six historical findings and existing human/Windows/Firefox/A-009/release gates remain
open. Scope-policy changes reject stale pending decisions; existing offline tokens retain
their documented expiry limits. UserInfo always rechecks current state.
