# Feature Specification: First-party login and S256 code flow

**Feature Branch**: `004-first-party-login`
**Created**: 2026-10-10
**Status**: Specified under approved v0.2 and A-001; implementation acceptance pending.
**Input**: Merge PR #3 using squash, then proceed to M01-D in the approved roadmap.

## User Scenarios & Testing

### User Story 1 — Initialize an isolated first-party pilot (Priority: P1)

An engineer explicitly initializes a Development environment with a synthetic non-administrator, a preapproved application and one resource scope. Its public identity and supported capabilities match its configuration.

**Why this priority**: A genuine login needs reproducible registration and durable credential prerequisites, without a public bootstrap mechanism.

**Independent Test**: Initialize both database profiles twice; observe one unchanged account/application/scope and auditable initialization. Production and incomplete configuration fail safely.

**Acceptance Scenarios**:
1. Valid explicit initialization creates the fixed pilot records without administrator privileges. Repeating the same input does not reset passwords, secrets, disable flags or lockout.
2. Conflicting existing records, missing credentials or non-Development initialization reject without partial changes or disclosure.
3. An enabled pilot publishes its exact secure issuer, protocol locations and only implemented capabilities; public keys contain no private material. The default health-only profile remains available.

### User Story 2 — Sign in through a bound interaction (Priority: P1)

A synthetic user signs into the approved application through a protected, accessible form. Separate tabs remain separate requests, and the system never sends them to an arbitrary destination.

**Why this priority**: The actual browser and password boundary must exist before any administrator journey.

**Independent Test**: Complete the real form through the exact registered callback, then exercise credential, forgery, freshness and interaction abuse cases on both providers.

**Acceptance Scenarios**:
1. Correct credentials create a new local session. Unknown users, wrong passwords, disabled/locked accounts, role-bearing accounts and accounts requiring a second factor receive the same generic denial. Passwords never reappear in response markup.
2. Missing/invalid form protection, expired/tampered/replayed interactions and another browser's interaction cannot authenticate. Parameters cannot overwrite the original validated request.
3. Two tabs retain independent interactions and can each complete a flow. A delayed completion whose browser session was replaced fails safely and requires a new request; it never switches subjects. Fresh-login and zero maximum-age requests each complete once without a loop; silent requests requiring interaction receive the appropriate protocol error. Explicit consent is denied until its journey exists.
4. Light/dark/system themes, keyboard focus, labels, password-manager support, error focus and mobile reflow work under restrictive browser policies. Browser storage contains only theme preference, never credentials or tokens.

### User Story 3 — Exchange one eligible, correctly bound code (Priority: P1)

The application exchanges its code with the required proof and receives only approved claims. Account, session and registration restrictions committed before a later decision are respected.

**Why this priority**: Successful issuance alone cannot prove the authentication boundary.

**Independent Test**: Independently validate tokens, then exercise malformed requests, expiry, replay, concurrent exchange and current-state changes on both providers.

**Acceptance Scenarios**:
1. The valid journey returns the expected issuer, subject, client identity, resource audience, nonce, original authentication time, signature and expiry. Internal bookkeeping and role/credential material remain private.
2. Missing/plain/unknown proof methods, wrong verifier/client/redirect, disallowed scopes/permissions, unsupported grants and malformed requests are denied. An unregistered callback never receives a redirect.
3. Expired/consumed codes fail. Exactly one of two simultaneous valid exchanges succeeds; a later replay fails. Losing a successful response requires a new authorization flow, without exchange retries.
4. User deletion/disable/lockout, role/second-factor changes, security-stamp/session invalidation, authorization revocation and narrowed application/scope eligibility prevent later issuance. An unrelated browser cookie cannot change the code subject.
5. Dependency failure produces bounded safe failure without tokens or authentication fallback; readiness fails while liveness remains independent. Diagnostics do not disclose passwords, proof verifiers, codes, tokens or correlation secrets.

### Edge Cases

Untrusted forwarded headers; missing cookies; duplicate form submission; back/refresh; parallel tabs; `prompt=none/login/consent/select_account`; zero/negative/malformed/overflowing `max_age`; cached-request overlays; method/content-type/body limits; exact expiry boundaries; stale process state; response loss; cancellation and outage.

## Requirements

### Functional Requirements

- **LOGIN-001**: Explicit Development-only initialization is transactional, idempotent and audited; no default secret, public bootstrap or administrative privilege. Traces ISS-004, FR-07/12/18/24.
- **LOGIN-002**: The enabled pilot requires durable protection and accurate secure discovery/public keys, code/query flow and mandatory S256 only. Traces ISS-001/002/006/007, FR-02/09/15/22/25, A-001.
- **LOGIN-003**: Password verification enforces current eligibility, lockout, generic failures, protected forms, secure host cookies, bounded requests and fresh session IDs. Traces ISS-005, FR-07/09/24, NFR-01/08.
- **LOGIN-004**: Five-minute interactions are individually browser-bound and completed once; original requests and freshness remain bound through callback. No free-form return URL is accepted. Traces ISS-008, FR-07/08/09.
- **LOGIN-005**: At authorization and redemption, persisted account/session/application/scope and authorization state permit issuance. Preserve code subject independently of browser state. Traces ISS-009, FR-02/04/05/08/09.
- **LOGIN-006**: Codes expire in 60 seconds and redeem once; access/identity tokens last five minutes with explicit claims and the single approved resource/client audience respectively. Traces ISS-006/007/010, FR-04/09, NFR-01/08.
- **LOGIN-007**: Shared slate/indigo light/dark/system tokens, responsive accessible login/error states and strict browser policy use same-origin assets. Traces ISS-011, FR-19, NFR-09/12.
- **LOGIN-008**: Safe diagnostics accompany every boundary. Bootstrap, password-attempt state, session replacement, interaction completion and consumption atomically persist sanitized audit intent. Native protocol consumption/revocation remains library-owned and can survive cancellation or later response failure; it is not claimed atomically audited here. Application decisions use a ten-second cooperative cancellation budget, with separately bounded native database operations and no abandoned background continuation. Traces ISS-005/012, FR-12/17/18, NFR-01/08/14.
- **LOGIN-009**: Every accepted invariant has positive/negative boundary evidence, both-provider semantics, real browser journeys, failure/concurrency and critical-decision mutation tests; no skipped-as-passed cases. Traces ISS-012, FR-17/21/22/24/25, NFR-02/11/12/14.

### Key Entities

Native account/application/scope/authorization/token; short-lived browser interaction; revocable local session; sanitized audit intent. The test application owns its correlation state and keeps exchanged tokens on its server.

## Success Criteria

- **SC-001**: Both database profiles complete the genuine login journey and every accepted denial case without skipped required checks.
- **SC-002**: Tampering/expiry/replay cases fail safely; concurrent valid exchange has exactly one winner.
- **SC-003**: Every requirement maps to tasks/tests/evidence; existing coverage and static-analysis floors remain enforced.
- **SC-004**: Browser journeys pass both themes at desktop/mobile widths, keyboard/errors and detectable accessibility checks; visual baselines receive review.
- **SC-005**: The stopped-database fault experiment finishes within ten seconds plus one second scheduling tolerance without credential disclosure; other fault tests record bounded observed completion and native consumption semantics. Metadata never advertises absent journeys.

## Assumptions

This is an internal **Development-only non-administrator pilot**, not production R1 or administrator MFA. Local sessions have a 15-minute non-sliding lifetime, no remember-me; successful verification replaces the previous browser session. This conservative pilot lifetime does not change approved administrator policy.

Approved v0.2/A-001 already authorizes this planned increment. Feature 002 supplies durable credentials; feature 003 supplies assurance routing, not assessed controls. Public registration/recovery, consent UI, refresh/PAR/device/machine grants, UserInfo/logout, Admin BFF/React and versioned management contracts remain allocated to later tasks. M01-E retains restart/two-instance/key-overlap acceptance. Certification and qualified human/manual accessibility assessment remain public-release gates; automation cannot close them.

Current-state enforcement occurs after committed changes at decision boundaries; no undocumented atomic ordering against concurrent administration or immediate revocation at offline validators is promised. No production deployment or external certification communication is authorized.
