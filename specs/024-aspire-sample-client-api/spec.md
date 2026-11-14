# Feature Specification: Interactive sample client and API

**Feature Branch**: `146-aspire-sample-client-api`
**Created**: 14 November 2026
**Status**: Selected for implementation; design and runtime verification in progress
**Input**: The user requested the next task after the recommendation to add an Aspire
sample client and API for interactive consent, token and UserInfo testing. Product
FR-02/04/08/09/16/17/19/21/24; roadmap M04-D integration examples.

## User Scenarios & Testing

### User Story 1 — Run and connect the sample (Priority: P1)

An integration developer starts the ordinary disposable local demo and opens a separate
sample application showing its setup state, exact callback and required access.

**Why this priority**: It provides a visible integration without writing client code.
**Independent Test**: Complete the selected setup workflow in a fresh run without a
custom shell/PowerShell script or database edits.

**Acceptance Scenarios**:

1. **Given** an initialized run, **when** the sample is opened, **then** a usable setup
   or ready screen appears with actionable instructions and no credential disclosure.
2. **Given** incomplete/invalid configuration, **when** sign-in is attempted, **then**
   it remains unavailable with a safe explanation.
3. **Given** a stopped run, **when** a fresh run starts, **then** old sample cookies,
   callbacks and configuration cannot authenticate the browser.

### User Story 2 — Sign in and inspect consented results (Priority: P1)

The developer signs in through the real issuer, completes new password and MFA proofs,
reviews requested scopes and chooses Allow or Cancel. After Allow, the sample shows
a bounded summary and an explicit current-profile action.

**Why this priority**: It makes consent and UserInfo observable in a manual journey.
**Independent Test**: Demonstrate Allow, Cancel and current UserInfo retrieval with
one managed registration, including absent email and explicitly consented email.

**Acceptance Scenarios**:

1. **Given** an eligible registration, **when** consent is allowed, **then** the sample
   creates its own session only after native validation and shows the matching subject
   and approved scope/audience summary.
2. **Given** Cancel or an invalid/replayed callback, **when** the browser returns,
   **then** no authenticated sample session is created.
3. **Given** an authenticated sample, **when** current profile is requested, **then**
   only the matching subject and currently permitted consented email are displayed;
   denial clears the displayed profile and explains the next action.
4. **Given** local sample logout, **when** it completes, **then** the sample cannot
   reuse its session/tokens. A second browser session and the shared run configuration
   remain usable. No issuer-wide or other-application logout is claimed.

### User Story 3 — Call a protected sample API (Priority: P1)

The developer requests synthetic orders. A separate API accepts only valid credentials
for its exact audience and required scope. The UI explains its revocation behavior.

**Why this priority**: Successful sign-in alone does not prove resource access.
**Independent Test**: Exercise allowed/denied real process calls and compare API and
current-state UserInfo behavior after an issuer policy change.

**Acceptance Scenarios**:

1. **Given** valid resource credentials and scope, **when** the API is called, **then**
   only fixed synthetic orders are returned.
2. **Given** missing/expired credentials, ID tokens, wrong issuer/audience or missing
   scope, **when** the API is called, **then** no protected data is returned.
3. **Given** changed issuer authority after issuance, **when** API and UserInfo are
   called, **then** each follows its documented current-state/expiry contract.

### Edge Cases

Absent registration; expired/replaced secret; disabled client/scope; cross-origin setup;
malicious issuer/API/return URL; wrong state/nonce/verifier; callback replay/concurrency;
expiry during calls; logout racing API/UserInfo response; sample restart; partial-startup
cleanup; issuer/API outage; signing-key rotation/discovery failure; changed UserInfo
subject or email;
missing/unconfirmed email; oversized responses; back navigation after logout;
secrets in logs/telemetry/HTML/errors/storage; dark/light and narrow viewport states.

## Requirements

### Functional Requirements

- **FR-001**: Start separate sample application/API resources through ordinary Aspire
  under the existing explicit disposable local-demo lifecycle. No custom PowerShell,
  persistent default data or production activation.
- **FR-002**: Configure registrations through the existing administrator UI, followed by
  protected local sample credential entry.
  Preserve native manager ownership, authorization/audit rules and secret handling.
- **FR-003**: Use the supported managed confidential code/S256 profile, exact HTTPS
  callback and native authentication/state/nonce/issuer/signature validation. Each
  explicit sign-in requires fresh password/MFA and explicit consent.
- **FR-004**: Isolate sample browser sessions, credentials and callback state from the
  administrator BFF. Tokens remain server-side; callback state is bounded and one-use;
  sessions have absolute expiry, protected cookies, CSRF controls and cleanup.
- **FR-005**: Show only finite validated summaries, never raw credentials or token
  payloads. UserInfo must match the validated ID-token subject; display only permitted
  email and boolean email_verified, including false for unconfirmed email and neither
  field when email is absent. Clear stale profile data on denial/logout/expiry. Email
  is not API authorization. Distinguish current profile from issuance-time ID-token claims.
- **FR-006**: Independently validate exact issuer, token kind, signature, audience,
  lifetime and scope at the API. Use ordinary public-key JWT validation with explicit expiry limits; it does not
  perform live issuer authority checks on every request.
- **FR-007**: Use fixed trusted destinations, bounded responses/deadlines and no
  automatic security-operation retries. Both user input and discovered metadata must
  be unable to redirect transport to untrusted discovery, JWKS, token, UserInfo or API
  destinations. Do not follow unapproved HTTP redirects.
- **FR-008**: Local logout ends only the current browser session and destroys its retained
  tokens and pending callback authority. Shared run configuration and other browser
  sessions remain intact. It does not revoke grants or end remote sessions. Managed issuer-assisted logout is
  outside this increment; the existing profile has no post-logout redirect support.
- **FR-009**: Provide accessible setup/ready/loading/signed-out/denied/expired/outage
  states, keyboard operation and responsive light/dark presentation.
- **FR-010**: Preserve both providers, migration histories, fixed BFF no-UserInfo
  guards, current issuer behavior and existing tests. No refresh/machine activation,
  ordinary-user onboarding, production API or general-purpose client SDK.
- **FR-011**: Verify real setup→sign-in→consent→UserInfo→API→logout on both providers
  and retained Chromium/WebKit, plus expiry/replay/secrecy/lifecycle/failure negatives.
  Maintain existing module floors and meaningful new coverage/mutation controls.
- **FR-012**: Document setup, observable API/UserInfo differences, teardown and limits.
  Require exact-head checks and independent architecture/security/Aspire/test/UX review
  before normal reviewed squash delivery.

### Key Entities

- **Sample configuration**: One run's client identity, protected credential, exact
  callback and requested scopes; no administrator authority.
- **Login attempt**: Bounded native correlation/state tied to a browser, consumed at
  most once; abandoned attempts expire.
- **Sample session**: Opaque browser reference to server-held identity/credential state,
  with absolute lifetime and logout/cleanup.
- **API result**: Fixed synthetic orders, no production/account records.
- **Displayed profile**: Finite validated identity fields and observation time, cleared
  when the corresponding current-authority call is denied.

## Success Criteria

- **SC-001**: A developer completes the documented journey from a fresh ordinary run
  without custom scripts, database edits or writing client code.
- **SC-002**: Both providers and retained browsers pass the real journey and required
  negative cases deny without protected output.
- **SC-003**: Browser state, rendered output and retained logs expose no sample OAuth
  tokens/credentials apart from deliberate setup-secret input; that input is neither
  echoed nor persisted by the page after submission.
- **SC-004**: API/UserInfo differences match the selected contract; logout/restart/expiry
  cannot reuse a sample session.
- **SC-005**: Required exact-head checks, unchanged existing floors, new module floors
  and independent reviews pass before delivery is marked complete.

## Assumptions and boundaries

The next-task instruction authorizes M04-D integration-example continuation. On
14 November 2026 the user replied “Please proceed” to the two stated recommendations:
manual administrator registration and public-key JWT API validation. Both recommendations
are now selected; no further confirmation is needed for routine implementation. Existing
synthetic administrator accounts supply the supported human sign-in. Shared design
and the disposable local lifecycle remain applicable. Windows/manual/Firefox/A-009,
six historical findings, reliability qualifications, refresh/vendor/production/public
release gates remain open. No SQL diagnostic approval is renewed. See [investigation](research.md) and [work status](tasks.md).
