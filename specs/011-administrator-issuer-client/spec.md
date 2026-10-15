# Feature specification: Administrator issuer/client integration

**Feature branch**: `053-admin-issuer-client`

**Created**: 15 October 2026

**Status**: Planning; B2 runtime implementation and acceptance not started.

**Input**: After B1 delivery in PR #52 and its record in PR #54, the user said “Please proceed” with B2 administrator issuer/client integration. This packet fulfills feature 010 T022 under approved v0.2.

B2 lets the administration backend obtain a tightly bound administrator credential from an already authenticated, currently eligible administrator session. It establishes the protocol boundary needed for the later browser journey; it does not open management access. It contributes to PRD FR-03/04/07/09/11/12/17/18/21/22 and NFR-02/05/08/11/14. FR-03/04/12 are fixed-registration/session contributions, not complete catalogue or audit-service delivery; NFR-05 live API enforcement remains M02-D.

## User scenarios and testing

### US1 — Admit a currently eligible administrator (P1)

An administrator with a recent complete sign-in can let the fixed administration backend obtain a credential for the intended administration resource without sharing credentials with the browser or ordinary applications.

**Independent test**: Complete the existing real password/TOTP account journey with a synthetic enrolled administrator, then perform a noninteractive administration authorization and one code exchange on each provider. A test-owned callback captures the result without activating the product browser session.

**Acceptance scenarios**:

1. Given an active current session and at least one approved persisted administrator permission, when the fixed backend requests its exact scopes/resource, then authorization and code exchange succeed with matching subject/session and bounded authentication time.
2. Given missing, stale, restricted-recovery, disabled, locked, revoked, replaced-factor or invalidated session state, when admission or exchange occurs, then no usable administrator credential is issued.
3. Given a valid session whose last approved permission is removed between authorization and exchange, when the code is presented, then exchange is denied. Specialist and auditor permission holders are eligible without acquiring full platform-administrator rights.
4. Given an ordinary pilot identity or token, when it is used on the administrator path, then admission fails and the pilot retains its original eligibility and behavior.

### US2 — Bind every exchange to the intended backend (P1)

An operator needs one explicit first-party registration whose credentials, request destination and granted resource cannot be substituted or silently repaired.

**Independent test**: Initialize the fixed registration twice, alter one authoritative field, and demonstrate fail-closed startup/admission. Exercise malformed, cross-client, cross-endpoint and concurrent replay requests against two issuer instances on each provider.

**Acceptance scenarios**:

1. Given the fixed registration and valid backend proof, when a bound pushed request and matching code are used once, then only the registered destination and single allowed resource are accepted.
2. Given reused backend proof, request reference, authorization code or callback state, when presented sequentially or concurrently on another instance, then no second successful use occurs.
3. Given unknown or disabled registration, altered keys, extra scopes/resources, wrong redirect, wrong proof key, mixed authentication methods or invalid proof lifetime, when processing occurs, then the request is denied without falling back to a weaker method.
4. Given a partial initialization, audit failure or ambiguous commit result, when the operation ends, then no implicit retry, credential disclosure or silent repair occurs.

### US3 — Keep integration isolated and recoverable (P2)

An operator can validate the integration using durable component-owned keys and state without enabling a browser application or changing existing pilot users.

**Independent test**: Restart independent backend instances over the same session database/keys; complete one pending native client exchange and reject its replay. Demonstrate key/storage failure, cancellation, upgrade/grants and default-disabled endpoint inventories on both providers.

**Acceptance scenarios**:

1. Given persisted callback state and retained decryptors, when another instance completes the exchange, then issuer, client, state, nonce, subject and token binding remain validated.
2. Given missing/corrupt keys, unavailable database or cancelled work, when the boundary is used, then it fails closed within its documented bounds, with no raw credential or request material in retained diagnostics.
3. Given default production configuration, when hosts start, then no new browser login/callback/proxy or management route is available and no schema or registration is created at web startup.

### Edge cases

Concurrent proof reuse at PAR and token; case-variant identities; duplicate form fields; oversized assertions/state; key replacement during an exchange; expired request/code/state at equality; clock rollback/future timestamps; loss of the last permission; changed security stamp or factor generation; consent/prompt values outside the fixed subset; cancellation after replay consumption; database loss after commit; backup restore containing previously consumed credentials.

## Requirements

### Functional requirements

- **FR-001**: Preserve a distinct administrator protocol profile and named authentication boundaries. Never relax pilot eligibility, infer administrator authority from B1 tickets, or let ordinary clients obtain administrator claims/resources.
- **FR-002**: Require a fresh current full administrator session, current enabled/unlocked account, active valid factor/security bindings and at least one exact approved persisted administrator permission at authorization and code exchange. Password and factor proof must both have ages strictly below 300 seconds (equality denied); earlier source-session expiry or a requested shorter age wins. Recovery-only proof is insufficient.
- **FR-003**: Offer only the noninteractive subset in B2: requests explicitly ask for no interaction. Missing/freshness-required authentication yields the protocol login-required result; unsupported interaction/consent modes fail explicitly without opening an unbound login route. Request-bound fresh login belongs to B3.
- **FR-004**: Provision one fixed confidential first-party client, exact HTTPS callback, exact identity/API scope set and one administration audience through authoritative managers. Initialization is explicit, idempotent only for an exact match, atomic with sanitized audit intent and fails on preexisting collision/drift. Disabled or changed state is never repaired or re-enabled by startup.
- **FR-005**: Require the registered asymmetric client proof at pushed authorization and token exchange, code flow with S256 proof key, and a pushed request for the administrator profile. Reject credential/method ambiguity, unregistered algorithms/keys and external key-location parameters. Preserve native protocol validation and errors.
- **FR-006**: Require bounded authenticated proof identifiers/timestamps and durable atomic single-use detection across endpoints and instances. Treat client-proof, pushed-request, code and callback-state replay as separate obligations. Storage failure or unknown consumption cannot authorize use or trigger automatic retry.
- **FR-007**: Revalidate exact registration, scope/resource, source session and current authority before issuing a code and before exchanging it; preserve the native code's subject, presenter, nonce and authorization binding. Do not elevate scope or copy arbitrary identity claims. Revoked/invalid authorization or code is denied. Issuance linearizes at its persistence commit: supported invalidation committed first must deny it; network delivery is not atomic with that commit.
- **FR-008**: Issue only required subject, session and authentication-context claims with explicit destinations. Administrator access credentials target one fixed resource and cannot serve as identity credentials; identity credentials target the fixed client. No roles/permission snapshots, security stamps, factor material, refresh token or ordinary-user profile data are released by this slice.
- **FR-009**: Keep code, access and identity lifetimes within 60 seconds, five minutes and five minutes respectively, shortened by applicable original authentication-session and policy deadlines. Reads/exchange cannot refresh authentication proof or extend source authority. Request and callback-state limits are explicit in the contract; expiry at equality is denied.
- **FR-010**: Use a native confidential client with separate component-owned durable signing/encryption material and persisted callback state. Validate real HTTPS issuer/discovery, audience, token type, signatures, state, nonce and code binding; no ephemeral fallback, caller-selected issuer or browser-held OAuth/client secrets.
- **FR-011**: Include additive migrations/grants for new product state, bounded cleanup without deleting still-live replay entries, cancellation/deadline behavior, sanitized outcomes, restart/key rotation/loss and restore invalidation. Both supported providers have equal acceptance criteria.
- **FR-012**: Keep the implementation default-disabled and product browser/API/React surfaces dormant. Test-owned transport cannot be reachable from production composition. Discovery reflects only enabled capabilities; do not advertise unimplemented refresh, end-session or broader grants.
- **FR-013**: Preserve every existing test, coverage floor, diagnostic hold and human/release gate. [A-007](../../docs/amendments/A-007-initial-phase-browser-scope.md) explicitly defers Firefox execution through internal B2; it does not remove tests or close findings. New security-critical behavior includes meaningful negative, concurrency and mutation evidence; exact-head applicable CI and independent architecture/test/security reviews precede squash acceptance.

### Key entities

- **Fixed registration**: stable client/scope/resource bindings, exact callback, approved public key metadata, explicit enabled state and portable revision.
- **Admission observation**: current subject/session, original password/factor times, expiry and exact current permissions; internal and short-lived, never reusable authorization.
- **Consumed client proof**: bounded hashed binding and expiry; unique across endpoints/instances, retained until it cannot authorize again.
- **Protocol request/code/authorization and client callback state**: separately owned native records with binding, expiry and consumption semantics.

## Success criteria

- **SC-001**: All accepted administrator positive/negative journeys pass on both providers; no required missing, failed or skipped case counts as acceptance.
- **SC-002**: Every concurrent duplicate proof/request/code/state attempt has at most one successful use across two instances, including restart; each independent replay boundary has its own evidence.
- **SC-003**: Committed account/session/factor/permission/registration invalidation before a new admission/exchange denies that operation. A stored session or old permission claim cannot override current state. This does not establish live API enforcement.
- **SC-004**: Default host route inventory and pilot behavior remain unchanged; no production browser or management access follows from B2 acceptance.
- **SC-005**: Retained evidence contains no raw tokens, assertions, private keys or passwords; upgrade, storage/key loss, timeout/cancellation and restore consequences are documented and demonstrated for the scoped boundary.
- **SC-006**: The implementation has traceable requirements, reviewed source/negative evidence, all applicable exact-head checks and a normal squash merge before it is called delivered. Planning checks satisfy none of the runtime criteria.

## Assumptions and clarified scope

Approved v0.2 and the current user continuation authorize routine design and implementation; no new approval is manufactured for this packet. B1 and governance are accepted internal dependencies. This packet is feature 010 T022's specification deliverable; B2 implementation remains separately tracked in this feature's tasks. The fixed first-party consent policy grants only the exact reviewed scope set and provides no general consent UI/catalogue.

The 300-second admission freshness ceiling follows the existing administrator security policy. B2 proves an already authenticated issuer account session; B3 supplies request-bound fresh-login continuation and actual browser session establishment/logout. Refresh issuance/serialization, live Admin API permission enforcement, React, full audit export/retention and production activation remain later work. All six historical findings, 48 human rows and public-release gates remain open; A-004 is expired and A-005/A-006 concluded.
