# Feature Specification: Administrator session refresh

**Feature Branch**: `112-bff-refresh-plan`  
**Created**: 2026-10-28  
**Status**: Reviewed planning packet; no refresh implementation or runtime acceptance evidence  
**Input**: Continue the next roadmap item, M02-C, after native Aspire delivery. The user requested “Please proceed” following that recommendation.

## Authority and scope

The approved v0.2 [architecture](../../docs/approved-v0.2/02-architecture.md), [protocol/security policy](../../docs/approved-v0.2/03-protocol-security.md), [verification plan](../../docs/approved-v0.2/05-verification-operations.md) and [approval record](../../docs/approval.md) already authorize administrator refresh. This packet develops that approved scope; it does not change the original authentication policy. The current instruction authorizes preparing and delivering this reviewed packet. Implementation follows its prerequisites under the existing roadmap authority, without treating a draft as runtime evidence.

M02-C covers one fixed administrator client and the existing local session. M02-D independently owns live Admin API authorization and five-second enforcement; M02-E owns React and the management proxy. Ordinary-client refresh (M06-B), FAPI, arbitrary relying-party logout, production activation and external declarations remain outside this feature. Existing B3 and Aspire work is reused.

## User Scenarios & Testing

### User Story 1 — Continue an eligible administrator session (Priority: P1)

An administrator who has completed fresh password and MFA can continue an active session when a short-lived access credential expires, without revealing credentials to the browser or extending the original sign-in deadline.

**Why this priority**: A valid local cookie currently supplies local status only; it does not provide renewable API credentials.

**Independent Test**: Complete genuine sign-in, advance beyond access expiry while within all session limits, renew, and inspect protected server state and browser output.

**Acceptance Scenarios**:

1. **Given** a live eligible session, **When** its access credential needs renewal, **Then** one replacement is committed server-side and the browser receives only minimal session status.
2. **Given** an expired session, disabled account/client, revoked grant, invalidated factor or removed administrator eligibility, **When** renewal is attempted, **Then** no new usable credentials are issued.
3. **Given** an existing sign-in, **When** renewal succeeds, **Then** original authentication time and absolute deadlines remain unchanged. An explicit new sign-in still requires new password and MFA.

### User Story 2 — Survive concurrency and uncertain delivery safely (Priority: P1)

Two tabs or two application instances must not redeem the same refresh credential independently. A process crash or lost response must not result in credential reuse or restoration of obsolete session state.

**Why this priority**: Refresh rotation changes authority at a remote boundary, so local retries can look like theft and compromise a session.

**Independent Test**: Race two independently running BFF nodes and inject faults before send, after issuer commit and before/after local commit; inspect native redemption, successor state and actual send counts.

**Acceptance Scenarios**:

1. **Given** two simultaneous renewals, **When** one owns the durable operation, **Then** only it may send; a contender may use its committed successor or receive a bounded busy response.
2. **Given** a dispatched operation whose result cannot be established, **When** an owner times out or restarts, **Then** no node retries that refresh credential; the local session becomes unusable and remote cleanup is reported separately.
3. **Given** reuse of a redeemed refresh credential, **When** the issuer detects it, **Then** the compromised authorization and its linked credentials become unusable for further issuance. An unrelated authorization remains valid.

### User Story 3 — End refresh access with honest logout outcomes (Priority: P1)

Local logout ends the browser's access even if remote services fail. A late refresh response cannot restore the session. The administrator can distinguish local completion from incomplete remote credential revocation or issuer sign-out.

**Why this priority**: Cookie deletion, local revocation, grant revocation and issuer logout are different actions.

**Independent Test**: Race refresh and logout in both commit orders, interrupt remote revocation, then test local state and native grant/token state separately.

**Acceptance Scenarios**:

1. **Given** local logout commits first, **When** a refresh response arrives, **Then** the response cannot install credentials or recreate browser authority.
2. **Given** remote cleanup fails, **When** local logout has committed, **Then** local access remains denied and the UI reports the unconfirmed remote outcome.
3. **Given** confirmed authorization revocation, **When** any linked refresh credential is used, **Then** issuance is denied, including a successor whose earlier response was lost.

## Edge cases

Expiry at equality; clock rollback; scope omission/narrowing/escalation; proof ages over five minutes during continuing sessions; duplicate claims/parameters; changed client keys or registration; source-session idle expiry; storage/key outage; quota exhaustion; audit rollback; process death before or after dispatch; lease expiry while a paused owner resumes; response accepted after local logout/replacement; committed local refresh with lost acknowledgement; replay concurrent with successor issuance; cleanup racing live state; restored stale databases; old software against expanded schema.

## Requirements

### Functional Requirements

- **FR-001**: Only the fixed administrator client and a genuinely established administrator session may obtain refresh capability. Initial sign-in retains fresh password/MFA, native client proof, code/PKCE/PAR and exact binding. Existing non-refresh B2/pilot flows remain independently valid.
- **FR-002**: Access and refresh credentials MUST remain protected server-side and bound to the exact subject, source session, local ticket, client and authorization. Browser responses, cookies, URLs, logs and traces MUST contain none of their values or private metadata.
- **FR-003**: Refresh MUST recheck current account, lockout, factor/authentication generations, source session, administrator eligibility, application, authorization and scope/resource policy. Preserve original authentication context; never manufacture a fresh MFA time. Continuing refresh does not impose the initial five-minute ceremony age ceiling; privileged actions retain their separate recent-MFA checks.
- **FR-004**: Use rolling refresh with no reuse leeway. Detected reuse MUST invalidate the compromised authorization and linked credentials under the same issuer ordering as issuance; token-only revocation is insufficient. Native protocol validation and cryptography remain authoritative.
- **FR-005**: Refresh MUST NOT expand previously granted scopes/resources. Rebuild releasable claims from current policy. No stale role/permission snapshot becomes management authority.
- **FR-006**: Preserve five-minute access/identity lifetimes, administrator refresh/session absolute maximum eight hours, session idle maximum thirty minutes and any shorter existing policy/source deadline. Renewal MUST NOT extend an original absolute deadline. Reads/polling are not user activity and MUST NOT renew.
- **FR-007**: Two nodes MUST serialize refresh per session through durable ownership. Commit ownership before send; fence publication by operation, ticket revision and browser generation. Do not hold database transactions across network calls, use process-local locks as sole protection or let expired ownership authorize a second redemption.
- **FR-008**: Distinguish failure before dispatch, dispatched/uncertain outcome, known local commit and failed publication. No automatic refresh retry, reacquisition of a dispatched credential or fallback to obsolete credentials. Unknown remote outcome requires local denial and separately tracked remote cleanup.
- **FR-009**: Local logout/replacement MUST atomically invalidate local refresh state with the ticket/generation. Remote authorization revocation and issuer-assisted logout remain separately observable; neither a redirect nor an ordinary revocation HTTP success proves family invalidation. Late responses cannot resurrect state.
- **FR-010**: Browser refresh MUST require authenticated exact-generation cookies, deliberate same-origin antiforgery protection and bounded framing. Preserve secure host cookies, trusted proxy rules, no permissive CORS and no browser bearer authority. Expose no arbitrary URL, scope, client or credential selector.
- **FR-011**: Refresh/revocation changes MUST carry atomic sanitized audit intent in their owning database; cross-store operations MUST NOT claim distributed atomicity. Protect stored credentials with distinct purposes and retain required keys until protected state expires or is invalidated.
- **FR-012**: Use existing project-level relational provider contracts, additive migrations and least-privilege owners for both PostgreSQL and SQL Server. Preserve databases, existing migration histories, source ownership and frozen compatibility producers.
- **FR-013**: Bound requests, storage, cleanup, cancellation and contention. Capacity/authority/audit/key failure MUST deny success without eviction of live state. Recovery from a restored snapshot MUST invalidate affected authority before activation.
- **FR-014**: Require both-provider native protocol, actual two-process/TLS, browser, concurrency, failure, contract, mutation and coverage evidence with independent review. Preserve A-009's exact deferrals and the approved Playwright 1.63.0 compatibility exception; neither is a general test waiver.
- **FR-015**: Keep feature delivery, M02-D/API authority, M02-E/React, Windows walkthrough, historical findings, human assessments and public release status distinct. This packet alone enables no refresh runtime or production surface.

### Key Entities

- **Credential generation**: protected current server credentials and immutable sign-in/authorization binding for one local ticket.
- **Refresh operation**: durable ownership and dispatch/publication outcome for exactly one generation.
- **Authorization family**: native issuer authorization and its associated credentials; the unit invalidated on detected reuse.
- **Remote cleanup intent**: durable protected target and finite outcome for authorization revocation, separate from issuer cookie logout.

## Success Criteria

- **SC-001**: In every defined two-node race, no more than one refresh send occurs per credential generation and no losing/stale owner publishes a usable successor.
- **SC-002**: All before/at/after deadline and current-eligibility denials issue zero usable successor credentials; successful renewal preserves original authentication and absolute limits.
- **SC-003**: Every fault case demonstrates its reached boundary, exact durable outcome and zero unauthorized retry; logout/replay denies further local renewal and confirmed remote revocation denies linked refresh use.
- **SC-004**: Browser and retained-output inspections disclose zero OAuth credential values; every invalid origin/CSRF/generation/substitution case changes no authority.
- **SC-005**: Exact final-source evidence passes both providers and all required nondeferred checks, with actual discovered identities, unchanged coverage floors and reviewed critical-decision mutations. No prior B3/Aspire pass is counted as refresh execution.

## Clarifications and dependencies

The prior user choices still govern: every explicit sign-in requires new password and MFA; local and issuer-assisted logout are both required; relational EF providers remain project-registerable; both existing databases/history must survive. Approved lifetimes and no-reuse policy supply the decisions here; no new user preference is required. Native extension feasibility is technical prerequisite work, not assumed existing behavior.

[Research](research.md), [plan](plan.md), [data model](data-model.md), [contracts](contracts/refresh.md), [tasks](tasks.md) and [verification](verification.md) define the implementation boundary. All six historical findings, 48 human rows, 345 not-assessed controls and fourteen viewer findings remain open. Actual Windows feature 014 T014 remains unexecuted. A-004 stays expired; A-005/A-006 remain concluded.
