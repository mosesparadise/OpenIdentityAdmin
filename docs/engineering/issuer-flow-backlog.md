# Issuer flow backlog and research boundary

**Feature Branch**: `002-issuer-foundation`  
**Created**: 2026-10-10  
**Status**: Feature 004 implements the bounded Development login/code pilot. T031/PR #14 and T032/PR #15 are merged; PR #16 supplies the T021 human-assessment handoff, whose execution remains open. [Feature 006](../../specs/006-issuer-lifecycle/tasks.md) delivers ISS-003/M01-E through three sequential PRs: restart continuity, two-instance behavior, then key overlap/retirement. PR #17 delivered restart as squash `a6b4dfb`. PR #18 adds accepted two-instance source evidence (21 checks / 1,097 ordinary cases); final documentation-head checks and squash delivery are recorded on that PR. See the [current pilot matrix](../../specs/004-first-party-login/acceptance-matrix.md) and [lifecycle evidence](../../specs/006-issuer-lifecycle/verification.md). No complete issuer or release acceptance is claimed.
**Input**: Continue after merged PR #1 with the unfinished issuer portion of approved roadmap milestone 001. The local feature number 002 does not mean roadmap milestone 002 (secure administrator BFF), which depends on this work.

## Current allocation of this original issuer backlog

| Requirement | Implemented evidence / remaining obligation |
| --- | --- |
| ISS-001 | Feature 004 metadata/JWKS reflects the fixed pilot; future capabilities must have their own acceptance. |
| ISS-002 | Feature 002 durable distinct credentials/encrypted protection is accepted; this alone does not prove ISS-003. |
| ISS-003 | **M01-E partial:** restart delivered by [PR #17](https://github.com/mosesparadise/OpenIdentityAdmin/pull/17); genuine two-process continuity, native single-use/replay, current eligibility and either-node loss have both-provider source acceptance in [PR #18](https://github.com/mosesparadise/OpenIdentityAdmin/pull/18). Its final delivery gate remains separate. Public signing/encryption overlap/retirement is still unbuilt; this harness does not establish production deployment/failover guarantees. |
| ISS-004 | LOGIN-001/T023 transactional synthetic bootstrap and conflicts/rollback accepted. |
| ISS-005 | LOGIN-003/008 password, lockout, form/cookie, audit and bounded request evidence; later administrator authentication remains separate. |
| ISS-006 | Minimal API decisions preserve OpenIddict validation/native state; T031 proves the specified transport denials and forwarding-header invariants. |
| ISS-007 | Mandatory S256 and downgrade/proof controls pass; A-001 certification compatibility is still external. |
| ISS-008 | Bound/fresh/one-use interactions, tabs and atomic capacity accepted through T030. |
| ISS-009 | Password/redemption current-state evidence and T032 representative new/resumed/silent authorization restrictions pass on both providers; no concurrent administration/offline revocation guarantee. |
| ISS-010 | Independent public-key token/claim/binding validation accepted for the fixed pilot; native lifecycle claims follow the contract. |
| ISS-011 | Six-cell automated UI/visual evidence accepted; qualified screen-reader/password-manager/WCAG and timed usability remain. |
| ISS-012 | Accepted 1,097-case distribution checkpoint plus both-provider mutations/thirteen-input coverage; full manual/product/operational acceptance remains. |

The original scenarios below remain requirements. SC-004 restart/overlap is only complete when all feature 006 stories have accepted evidence; a restart pass alone cannot close it. The distribution checkpoint establishes deliberately concurrent attempts on two actual nodes and checked native outcomes; it does not force a particular internal database interleaving. M01-F versioned management contracts, Scalar/Swagger and generated clients remain a separate obligation.

## User Scenarios & Testing

### User Story 1 — Start and restart a correctly configured issuer (Priority: P1)

An engineer runs an isolated issuer using durable, purpose-separated credentials and a canonical secure identity. Restarting its process preserves valid protected state; missing or unsuitable credentials fail safely.

**Why this priority**: A code-flow demonstration is meaningful only when it uses real protocol protection and stable cryptographic identity.

**Independent Test**: Start each supported provider profile with supplied credential material; verify public metadata and restart interoperability; prove invalid material cannot start an enabled issuer.

**Acceptance Scenarios**:
1. Given valid, distinct credentials and a canonical secure issuer, startup publishes only implemented capabilities and public signing material.
2. Given missing, unreadable, expired, wrongly protected or unsuitable credentials, startup fails without exposing their values or creating substitute keys.
3. Given an existing session and unredeemed authorization code, restart with the same stores preserves their documented validity; a previously redeemed code remains unusable.
4. Given staged replacement signing credentials, new public material is available before use; retiring verification/decryption material remains usable through the overlap window.

### User Story 2 — Sign in through a bound first-party interaction (Priority: P1)

A synthetic non-administrator signs into a fixed, preapproved first-party application through the actual authentication form. The interaction stays bound to its request and browser and cannot be redirected to an arbitrary destination.

**Why this priority**: It proves the future issuer boundary without granting premature administrator access.

**Independent Test**: A browser follows a genuine authorization request, submits the protected form, receives a code at the exact registered callback, and the test relying party exchanges it while keeping tokens server-side.

**Acceptance Scenarios**:
1. Explicit local initialization creates a non-administrator and approved client/scope through their owners; repeating it does not reset credentials or duplicate data. Production rejects demonstration seeding.
2. Correct credentials create a fresh local session; incorrect credentials, lockout, disabled users and second-factor requirements do not bypass eligibility checks. Errors are generic.
3. A forged form, expired/tampered/replayed interaction, different-browser resume or changed callback cannot produce an authenticated authorization response.
4. Parallel browser tabs have separately bound interactions. Fresh-login and maximum-age requests complete without loops; silent requests requiring interaction return protocol errors.
5. The slate/indigo form has light/dark themes, responsive layouts, visible focus, accessible errors, password-manager support and a strict content policy.

### User Story 3 — Issue only correctly bound, eligible tokens (Priority: P1)

A registered first-party client exchanges its single-use code using S256 proof and receives only approved claims and one approved resource audience. Changes to current eligibility take effect when a later authorization/redemption decision reads that state.

**Why this priority**: A successful demonstration must include denial paths and replay boundaries, not just token output.

**Independent Test**: Run positive, malformed, tampering, current-state, expiry and concurrency scenarios against both actual database providers; validate resulting tokens independently.

**Acceptance Scenarios**:
1. Valid code/S256 requests succeed with the expected issuer, subject, audience, nonce, authentication time, signature and expiry.
2. Missing/plain/unknown PKCE, wrong verifier/client/redirect, denied permissions, unsupported grants or malformed requests fail through protocol validation.
3. Reused or expired codes fail; concurrent redemption produces at most one successful response. Failure after redemption requires a new authorization flow, never an automatic exchange retry.
4. Deleted/disabled/locked users, invalidated sessions/security stamps, revoked authorizations and narrowed client/scope eligibility prevent later redemption.
5. Raw identity credentials, security stamps, internal session bookkeeping and administrator roles never appear in public tokens or unrestricted logs.
6. Dependency outage causes bounded failure without token issuance or authentication fallback; readiness fails and liveness remains independent.

### Edge Cases

Untrusted forwarded headers, invalid issuer path/query, method/content-type mismatch, duplicate form submission, lost cookies, browser back/refresh, multiple tabs, `prompt=none/login/consent`, zero/negative/malformed/overflowing `max_age`, query overlay on cached requests, clock/expiry boundaries, two issuer instances, consumed-code response loss, key overlap/retirement, interrupted bootstrap, and stale per-process state.

## Requirements

- **ISS-001**: Publish an exact configured HTTPS issuer and only the implemented authorization-code/query-response, scope, authentication and signing capabilities. Preserve standard unversioned protocol paths. Traces FR-09/22/24, NFR-01/08/14.
- **ISS-002**: Require durable, distinct signing, token-encryption and at-rest Data Protection credentials with a private issuer-specific key ring; fail safely without generated runtime fallback. Traces FR-15/18, NFR-01/06/08.
- **ISS-003**: Prove restart, two-instance and staged overlap behavior for sessions, codes and signed tokens. Retain historical decryption material until all protected artifacts expire. Traces FR-15/18, NFR-06/14.
- **ISS-004**: Provide an explicit, idempotent, auditable Development-only synthetic bootstrap, with externally supplied/generated non-default secrets and no administrative role or public bootstrap endpoint. Traces FR-07/12/18.
- **ISS-005**: Verify passwords through the existing identity system; enforce lockout, generic failures, form forgery protection, secure host cookies, bounded request rates and session replacement. Traces FR-07/09/24, NFR-01/08.
- **ISS-006**: Preserve library-owned request validation, code protection/redemption and response generation. Application handlers make current policy decisions using explicitly separated browser/server schemes. Traces FR-09/22.
- **ISS-007**: Enforce S256 PKCE for every code client; advertise and accept no plain or exempt certification-client mode. Traces FR-02/09/25 and A-001.
- **ISS-008**: Bind short-lived interactions to validated server-held requests, browser correlation and individual completion state; enforce expiry, one-use completion, prompt/freshness behavior and safe local resumption. Explicit consent requests are denied until consent UI exists. Traces FR-07/08/09.
- **ISS-009**: Recheck persisted user, session, authorization, application and scope eligibility at each issuance boundary. Preserve the code subject/binding independently of any ambient browser cookie. Traces FR-02/04/05/08/09.
- **ISS-010**: Release only approved claims with explicit token destinations and a single fixed mapped API audience. Keep native token/authorization storage and encrypted codes. Traces FR-04/09, NFR-01/08.
- **ISS-011**: Supply shared slate/indigo light/dark tokens, accessible responsive login/error states and strict same-origin assets/content policy. Theme persistence contains only the preference. Traces FR-19, NFR-09/12.
- **ISS-012**: Execute meaningful unit, real-provider, HTTP protocol, actual-process/browser, failure, concurrency and targeted decision-mutation checks; retain complete reports and unchanged per-module coverage floors. Traces FR-17/21/22/24/25, NFR-11/14.

### Key Entities

Native account/application/scope/authorization/token records; application-owned browser interaction and local authentication session; sanitized security event; external credential reference and protected key ring. Public credential metadata never includes private material.

## Success Criteria

- **SC-001**: Every accepted issuer requirement maps to passing evidence or remains explicitly incomplete; no skipped required check is accepted.
- **SC-002**: All accepted positive and negative journeys behave equivalently on both supported providers; no mocked database substitutes for these checks.
- **SC-003**: No tampered/expired/replayed interaction or code yields an unauthorized issuance; simultaneous code redemption has at most one winner.
- **SC-004**: Restart/overlap exercises retain only the documented valid state and expose no private credentials through public metadata or reports.
- **SC-005**: Browser login works in both themes at desktop/mobile sizes with keyboard and automated accessibility checks; no OAuth tokens persist in browser storage.
- **SC-006**: Operational dependency failures complete within the documented request deadline, produce no tokens, and preserve safe liveness/readiness behavior.

## Assumptions and scope

The user approved v0.2/A-001 and explicitly requested continuation after PR #1. This is an internal non-administrator issuer demonstrator, not deployable R1 or a substitute for administrator MFA. Public registration, consent management, recovery, refresh/PAR/device/machine grants, UserInfo/logout, usable Admin BFF/API, React administration and full production assurance remain later accepted work. Do not advertise absent capabilities. Baseline API-version tooling remains a separately tracked milestone-001 obligation; this protocol slice alone does not close the whole roadmap milestone. Basic OP compatibility remains an external public-release blocker under A-001.

Current-state enforcement is at persisted decision boundaries; this slice does not promise undocumented atomic ordering against simultaneous administrative mutations or immediate revocation at offline resource validators. No production deployment, external certification action or upstream communication is authorized by this feature.
