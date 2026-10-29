# Feature Specification: Protected application list

**Feature Branch**: `121-protected-application-list`
**Created**: 2026-10-29
**Status**: Internally delivered in PR #118; human and release acceptance remain open
**Input**: The user requested the next item after delivery of the visible workspace: the protected application list, followed separately by creation/editing.

## User Scenarios & Testing

### User Story 1 — Browse real applications (Priority: P1)

An administrator signs into the existing workspace, opens Applications and sees actual registered applications, with client identifier, display name and client type. Search and paging make the catalogue useful without exposing credentials.

**Why this priority**: This is the first visible management data slice after the delivered workspace.
**Independent Test**: A genuinely enrolled administrator signs in and reads existing registrations through the complete browser/service path on each supported database.

**Acceptance Scenarios**:

1. **Given** current application-read permission and valid sign-in, **when** Applications opens, **then** real persisted registrations appear with bounded pagination and no credential material.
2. **Given** more than one page of registrations, **when** search or paging changes, **then** stable ordered matching results and accurate navigation are returned.
3. **Given** no matching registrations, **when** the search completes, **then** the screen shows a useful empty state without fabricated records.

### User Story 2 — Current access controls (Priority: P1)

Administrative data remains available only while authentication and current permission allow it, including requests made directly to the service.

**Why this priority**: A visible list must not bypass independent management authorization.
**Independent Test**: Direct and browser requests reject invalid credentials and observe committed revocation/permission changes across instances within five seconds.

**Acceptance Scenarios**:

1. **Given** no valid session or an expired/revoked access token, **when** data is requested, **then** authentication is required and no application data is disclosed.
2. **Given** a valid identity without the current application-read grant, **when** data is requested, **then** access is forbidden even if an old token contains a role claim.
3. **Given** a committed account, source-session, application, token, authorization or permission change, **when** either instance receives a subsequent request, **then** access reflects that change within five seconds.
4. **Given** unavailable authority storage, **when** data is requested, **then** the request fails closed without a stale-data fallback.

### User Story 3 — Usable failure and navigation states (Priority: P2)

The Applications screen works with the existing responsive workspace, appearance settings and session/logout controls.

**Why this priority**: The user must be able to test and understand the delivered slice locally.
**Independent Test**: Retained browser engines exercise loading, success, empty, forbidden, signed-out, unavailable and expired states with keyboard, reflow and accessibility checks.

**Acceptance Scenarios**:

1. **Given** a delayed or superseded request, **when** search, navigation or sign-out changes the current view, **then** obsolete results cannot restore hidden application data.
2. **Given** an expired credential or failed read, **when** the screen updates, **then** previously displayed data is cleared and a clear sign-in or retry action is offered.
3. **Given** local Aspire initialization, **when** the user completes enrollment and sign-in, **then** Applications is available without custom scripts or a separate frontend build.

### Edge Cases

Reject duplicate/unknown query parameters, oversized search/page values, invalid versions, token type/issuer/audience/scope/presenter mismatches, ambiguous subject/session claims, body-bearing collection GETs, cross-origin browser requests, expired protected credentials and redirected/malformed/oversized downstream responses. Preserve original lifetimes, cancellation and no automatic authentication renewal. Handle ordinal identifiers consistently across providers; no secrets in results, browser storage, logs or retained screenshots.

## Requirements

### Functional Requirements

- **FR-001**: Expose a real read-only application catalogue with client identifier, display name and client type; no write/details/credential action is introduced. PRD FR-01.
- **FR-002**: Support bounded search and stable paging with default size 25, maximum 100, and explicit pagination metadata. PRD FR-01/16.
- **FR-003**: Independently validate genuine access credentials and current account, source-session, application, token, authorization and `applications.read` permission; committed changes take effect across instances within five seconds. PRD FR-11/NFR-01/05.
- **FR-004**: Retain access credentials only in protected server-side session state and forward them only to the fixed management destination; never use identity credentials or browser-supplied identity headers as management authority. PRD FR-11/NFR-08.
- **FR-005**: Preserve original session/access lifetimes and explicit fresh sign-in; refresh remains disabled. Distinguish authentication, permission and unavailable outcomes without leaking internal values. PRD FR-11.
- **FR-006**: Publish separate explicitly versioned browser and management contracts and regenerate the browser client; preserve existing routes/contracts. PRD FR-16/NFR-14.
- **FR-007**: Deliver responsive, themed, accessible Applications navigation and loading/success/empty/forbidden/unavailable/expired states; clear stale results. PRD FR-19/NFR-09.
- **FR-008**: Run through the existing native Aspire disposable lifecycle with both real database providers, retaining prior enrollment/login/logout and migration-history behavior. PRD FR-17/21/24.

### Key Entities

- **Application summary**: Safe projection of an existing registered application; excludes credentials, token values, key material and arbitrary extension properties.
- **Protected access credential**: Original server-held access token associated with the exact validated callback, browser session, subject and source session; cannot renew authentication.
- **Current authority observation**: Request-local current status and persisted permission used for one bounded read, never an enduring permission grant.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A genuine administrator can open Applications after native local initialization and see the real BFF registration on both supported databases.
- **SC-002**: Every defined denial and failure scenario discloses zero application records or credential values.
- **SC-003**: Both service instances enforce committed authority changes within five seconds in retained integration evidence.
- **SC-004**: Search/paging and all defined UI states pass retained-engine functional, keyboard, theme and automated accessibility checks; qualified manual and Windows acceptance remain explicitly open.
- **SC-005**: All prior required test identities remain covered; scoped required tests, unchanged coverage gates and independent reviews pass before normal squash delivery.

## Scope and dependencies

The already approved architecture determines remote discovery/signature validation plus native token/authorization store checks and current policy. Existing protocol access tokens target the management resource, but B3 currently discards them; protected retention is a required dependency. Keep existing registrations visible as actual records; do not seed invented catalogue rows. Creation/editing, full application policy lifecycle, refresh activation, documentation-vendor activation and production release remain separate. Ordinary bounded presentation choices follow the approved contract; material policy choices require clarification before affected code. Historical findings, Firefox/A-009, human and release gates remain open.
