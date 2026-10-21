# Feature Specification: Administrator browser sessions (B3)

**Feature Branch**: `068-administrator-browser-plan` (feature directory `013-administrator-browser-session`)

**Created**: 21 October 2026, Europe/London

**Status**: Planning; no B3 runtime acceptance or activation.

**Input**: Continue the approved roadmap after coherent B2 delivery; feature 011 T027 requests request-bound fresh login, product callback/B1 ticket establishment, cookie/CSRF/fixation protection and local/accurately bounded remote logout, with three-engine acceptance and issuer-policy dependencies.

## User Scenarios & Testing

### User Story 1 — Deliberate administrator sign-in (Priority: P1)

An eligible administrator deliberately starts sign-in, completes the issuer's password and MFA journey, and returns to the intended administrator application in the same browser. Another browser, tab or account cannot substitute its login completion.

**Why this priority**: B2 provides a validated native exchange, but does not establish a product browser session.

**Independent Test**: Exercise a real issuer password/TOTP journey and native confidential-client exchange, then prove that only its initiating browser receives a newly committed B1 session.

**Acceptance Scenarios**:

1. **Given** an explicitly initiated, unexpired sign-in, **When** the administrator completes its bound password/MFA ceremony and native exchange, **Then** the BFF issues a fresh opaque cookie only after the canonical server ticket commits.
2. **Given** a missing, replayed, expired, substituted or cancelled intent, **When** a callback arrives, **Then** no ticket or cookie is established and no token exchange is automatically retried.
3. **Given** a supported authority invalidation committed before issuer issuance, **When** the exchange is attempted, **Then** the existing B2 authority checks deny it. A later local ticket read is not proof of current API permission.

### User Story 2 — Safe browser session use (Priority: P1)

The administrator can inspect minimal sign-in status and use the application's session without exposing OAuth credentials to browser scripts. Stale tabs, replacement and failures do not extend the original authentication deadline or restore a logged-out session.

**Why this priority**: A successful exchange alone does not provide safe cookies, browser mutation protection or session lifecycle coordination.

**Independent Test**: Inspect actual browser cookies, network traffic and storage; exercise session expiry and callback/replacement/logout races on both databases and all three browser engines.

**Acceptance Scenarios**:

1. **Given** a committed session, **When** the browser requests status, **Then** it receives only the documented minimal identity/status contract, never tokens, handles, permission snapshots or private protocol properties.
2. **Given** an expired/revoked ticket or unavailable session store, **When** a protected browser endpoint executes, **Then** it does not authenticate the request or redirect an API call into an HTML sign-in flow.
3. **Given** a logout or replacement that wins the durable browser coordination order, **When** an older callback finishes, **Then** it cannot create a usable replacement session or restore the previous generation.

### User Story 3 — Honest, bounded sign-out (Priority: P1)

An administrator signs out of the local application and can complete the supported issuer sign-out flow. The application distinguishes a committed local sign-out from an incomplete remote sign-out.

**Why this priority**: Cookie deletion, local ticket revocation and issuer session termination have different effects.

**Independent Test**: Revoke the actual local ticket; attempt native issuer end-session under success, cancellation and transport failure; prove independent local and issuer outcomes without cross-site mutation.

**Acceptance Scenarios**:

1. **Given** a valid protected sign-out request, **When** local revocation commits, **Then** subsequent local use is denied even if issuer logout fails or the return redirect is lost.
2. **Given** failed or ambiguous local commit, **When** a sign-out response is produced, **Then** it cannot claim durable local revocation merely because a cookie was cleared.
3. **Given** an invalid hint, mismatched source session, unregistered return URI or cross-site mutation, **When** remote logout is requested, **Then** it does not revoke an unrelated session or follow an attacker-controlled redirect.

### Edge Cases

Expiry at equality; clock rollback; completed MFA after an expired initial PAR/state; concurrent tabs; browser login CSRF and account swapping; recovery/pilot cookie substitution; duplicate claims; changed registration/keys; callback consumed before ticket-write failure; commit succeeded but response/cookie lost; logout before callback completion; stale antiforgery tokens after replacement; unavailable issuer, database or retained key; restored database reviving consumed state; capacity exhausted without evicting live state.

## Requirements

### Functional Requirements

- **FR-001**: Sign-in MUST start through deliberate, antiforgery-protected same-origin interaction. A request-bound, browser-bound, bounded, single-use login intent MUST connect the initiating BFF browser to the completed administrator ceremony and final native exchange. No arbitrary return URL or pilot/recovery authority is accepted.
- **FR-002**: Freshness MUST preserve B2's strict password-and-factor age ceiling of 300 seconds and any shorter requested bound. Every explicit sign-in MUST require a new password/MFA ceremony, even with an existing fresh issuer session. Existing PAR (60 seconds), code (60 seconds) and client-state (300 seconds) deadlines MUST NOT be lengthened or revived to accommodate human login. A completed login uses a fresh native B2 challenge when the earlier request cannot legally continue.
- **FR-003**: The issuer MUST supply an authenticated binding of exact subject, opaque source-session identifier, original source-session deadline and completed login intent to the fixed client's validated exchange. B1 ticket creation MUST NOT infer that deadline from token expiry, session-identifier syntax, browser input or a new eight-hour allowance.
- **FR-004**: The product callback MUST own the existing native validation/consumption boundary and construct a new canonical B1 ticket from an explicit allowlist. Native state, issuer/client, signature, audience, nonce, correlation and code binding remain mandatory. OAuth credentials stay server-held; the browser receives only a new opaque local handle.
- **FR-005**: The BFF MUST commit ticket creation before issuing a session cookie. Callback consumption, issuer exchange and local persistence MUST NOT be represented as one cross-service transaction. Failed/unknown completion and lost response MUST NOT trigger automatic replay, duplicate establishment or token exchange retry.
- **FR-006**: Browser cookies MUST be host-only, Secure, HttpOnly, path `/`, with bounded lifetime, separate session/correlation purposes and explicit SameSite policy. Server expiry and revocation remain authoritative. Status reads do not renew; any renewal preserves B1's original idle policy, source deadline and absolute cap.
- **FR-007**: Browser mutations, including sign-in, replacement and local logout, MUST enforce antiforgery and same-origin policy. The native callback is a narrowly defined state/correlation-protected exception, not a general CSRF bypass. No permissive CORS or browser bearer-token authentication is introduced.
- **FR-008**: Durable browser-generation coordination MUST prevent fixation, accidental account replacement and stale-callback resurrection after logout/replacement. B1 revision/CAS semantics MUST remain intact; they alone are not sufficient browser-operation coordination.
- **FR-009**: Local logout MUST revoke the local ticket and invalidate its pending login generation before claiming success, independently of remote logout completion. Existing in-flight reads and uncertain commits MUST be described accurately; cookie clearing alone is not server revocation.
- **FR-010**: Issuer-assisted logout MUST use a reviewed native end-session contract with exact client/hint/session binding, protected confirmation and registered post-logout destination. No arbitrary-client front/back-channel logout, all-session logout or refresh revocation is claimed. Local-only delivery MUST NOT be called full B3/M02-B completion.
- **FR-011**: Session mutations MUST retain atomic sanitized audit intents with actor provenance from validated server authority. Operational output MUST exclude credentials, raw callback queries, personal data and secret state. Bounded orphan/tombstone cleanup, restore invalidation and retained-key behavior MUST be specified and tested.
- **FR-012**: BFF runtime MUST retain Sessions-only privileges; issuer-owned changes remain in Platform. New persistence MUST use the project-level relational EF provider contracts with additive migrations for PostgreSQL and SQL Server, preserving existing databases and migration histories.
- **FR-013**: All existing B2 noninteractive behavior, native validation, replay/capacity/no-retry invariants, named schemes and pilot isolation MUST remain covered. The new fixed-client claim/continuation/end-session contracts MUST receive architecture, test, security and API review before their executable integration.
- **FR-014**: Firefox restoration under feature 011 T028 MUST precede B3 executable work. Acceptance requires actual real-database, real-TLS, real-password/MFA browser execution for Chromium, Firefox and WebKit, both themes/accessibility and failure/concurrency evidence; no skipped required case counts as a pass.
- **FR-015**: Product authentication MUST NOT stand in for M02-C refresh or M02-D live API authorization. No refresh grant, API proxy, React activation, production enablement or public-release acceptance follows from this planning packet. Historical findings, human assessments and external gates remain explicit.

### Key Entities

- **Browser operation generation**: server-owned ordering for intentional sign-in/replacement/logout in one BFF browser context; not an authenticated identity or permission.
- **Login intent / issuer continuation**: bounded one-use records linking the client request and browser proof to an actually completed administrator ceremony. Opaque references are selectors, not independent authority.
- **Authenticated session binding**: issuer-attested subject, source session, original deadline and completed-intent binding available only to the fixed client after native validation.
- **B1 ticket**: existing protected, server-held canonical authentication ticket; fresh random handle in browser, digest in persistence, immutable upstream deadline.
- **Logout continuation**: bounded one-use remote completion correlation, distinct from local revocation and from browser authentication.

## Success Criteria

- **SC-001**: Every accepted sign-in in the six provider/browser cells maps to exactly one intended ceremony and committed local session; every defined substitution, replay, cancellation and expiry case establishes none.
- **SC-002**: Browser inspections and synthetic-secret scans reveal zero OAuth credentials or private protocol state in application-readable storage, rendered pages or retained diagnostics. Protocol transport itself is narrowly distinguished from application exposure.
- **SC-003**: All defined replacement/logout/callback orderings preserve the winning generation; all before/at/after deadline tests preserve B1 and B2 bounds without retry or resurrection.
- **SC-004**: Local and issuer logout outcomes are independently demonstrated, including partial failure; no test reports remote termination from a local cookie deletion or redirect alone.
- **SC-005**: Actual native test identities, required coverage inputs/floors, meaningful security mutations and independent source/evidence reviews pass on the exact final head before normal squash. Planning acceptance does not count as runtime evidence.

## Assumptions and dependencies

No database/provider redesign is needed: feature 012 and coherent B2 are delivered. Technical choices are recorded with source evidence in [research](research.md); unproven native integration is an implementation prerequisite, not a claim of existing capability.

On 21 October 2026, the user explicitly selected **“Require new password and MFA for each sign-in”** and **“Include local and issuer-assisted logout.”** Every explicit B3 sign-in therefore requires a new bound ceremony; an existing fresh issuer session is not sufficient. Full B3 includes the issuer end-session dependency. These decisions do not waive Firefox restoration, native feasibility, evidence or activation/release gates.

The approved [PRD](../../docs/approved-v0.2/00-prd.md), [architecture](../../docs/approved-v0.2/02-architecture.md), [security](../../docs/approved-v0.2/03-protocol-security.md), [verification](../../docs/approved-v0.2/05-verification-operations.md), [B1 contract](../010-bff-session-foundation/contracts/ticket-store.md), [B2 contract](../011-administrator-issuer-client/contracts/administrator-protocol.md), [A-007](../../docs/amendments/A-007-initial-phase-browser-scope.md) and [approval record](../../docs/approval.md) remain governing dependencies. Six historical findings, 48 human rows, 345 not-assessed assurance controls and activation/production/public-release gates remain open.
