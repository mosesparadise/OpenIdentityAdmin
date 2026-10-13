# Delivery roadmap and task register

This is the living view of **all approved work**, as of 13 October 2026. It supplements the immutable [v0.2 plan](approved-v0.2/06-spec-kit-plan.md), [PRD](approved-v0.2/00-prd.md) and [approval record](approval.md). Dates are status dates, not delivery estimates. No estimate is invented before a feature's dependencies and acceptance work are understood.

**Current merged capability (`main`):** native persistence, both-provider migrations and default health-only Aspire hosts, strict xUnit v3/MTP/coverage/supply-chain gates, AI instructions, durable issuer credentials/encrypted Data Protection, and an explicitly enabled **Development-only first-party login/code-S256 pilot**. Explicitly enabled administrator enrollment, password/TOTP account login, restricted recovery, factor/code replacement and local logout are also implemented. Full pilot/MFA milestone acceptance remains open; production administration, Admin BFF/React and management APIs are not implemented.

**Current increment:** M02-A increment 2. The T016 design checkpoint merged in [PR #24](https://github.com/mosesparadise/OpenIdentityAdmin/pull/24); [T022 account composition](../specs/008-administrator-foundation/account-composition.md) was squash-merged in [PR #25](https://github.com/mosesparadise/OpenIdentityAdmin/pull/25): independent activation/protection, explicit route/scheme isolation and 31 new regression cases. [T023 protected factor storage](../specs/008-administrator-foundation/factor-storage.md) was squash-merged in [PR #27](https://github.com/mosesparadise/OpenIdentityAdmin/pull/27) with both-provider migrations and 51 Factors cases per provider. Administrator account MFA/login is accepted through PR #30 and recovery/security through PR #31; broader administration remains unavailable. Engineering standards/proportional CI/code-quality enforcement merged in [PR #22](https://github.com/mosesparadise/OpenIdentityAdmin/pull/22); appsettings-backed OpenAPI metadata/viewer regression work merged in [PR #23](https://github.com/mosesparadise/OpenIdentityAdmin/pull/23) as `35873ee`. See [feature 009 tasks](../specs/009-engineering-standards/tasks.md) for separate legacy cleanup.

M02-A increment 1 was squash-merged in [PR #21](https://github.com/mosesparadise/OpenIdentityAdmin/pull/21) as `38535bd`: protected first-administrator bootstrap, permission/governance storage and both-provider migrations. Final revision passed 31 required checks and 1,301 ordinary .NET cases; one restore-only setup retry is recorded on the PR. Bootstrap initially leaves the account disabled pending explicit MFA enrollment.

**Current authentication work:** T025 protected enrollment/MFA login/authoritative account sessions was squash-merged in [PR #30](https://github.com/mosesparadise/OpenIdentityAdmin/pull/30) (`78d77c3`), with all 48 CI jobs, 1,666 required .NET matrix cases and 35 coverage inputs passing. T026 recovery/security/logout was squash-merged in [PR #31](https://github.com/mosesparadise/OpenIdentityAdmin/pull/31) (`c10f9d2`); see the [execution packet](../specs/008-administrator-foundation/recovery-security.md). T027 evidence gates are accepted in PR #33 (`ab8b62d`); T028 automated evidence is accepted in PR #34 (`fafc98d`); T029 scoped operational handoff was squash-merged in [PR #35](https://github.com/mosesparadise/OpenIdentityAdmin/pull/35) as `8792f85`, after final head `eb2cd0a` passed all 62 checks in run `38354470938`. [A-003](amendments/A-003-startup-investigation-sequencing.md) expired at that merge. ADM-OPS-01 remains unresolved and acceptance/public-release-blocking. The subsequent main run `38355735749` failed a separate Firefox synthetic history-readiness control (ADM-EVID-01); it is not clean acceptance. The [investigation](../specs/008-administrator-foundation/startup-investigation.md) records the retained failures, three corrected PostgreSQL diagnostic controls and next evidence requirements. Neither finding has a demonstrated causal correction. T031 was squash-merged in [PR #37](https://github.com/mosesparadise/OpenIdentityAdmin/pull/37) (`1a043c0`) after all 62 exact-head checks passed, adding bounded sanitized startup-transition evidence and 20 diagnostic controls. [T032 browser diagnostics](../specs/008-administrator-foundation/history-diagnostics.md) was squash-merged in [PR #38](https://github.com/mosesparadise/OpenIdentityAdmin/pull/38) (`fbd8ca9`) after all 62 exact-head checks passed. [T019 governance design](../specs/008-administrator-foundation/governance-design.md) is independently reviewed and supplies the usable-administrator, current-permission and supported-recovery contracts plus planned GOV-01–12 scenarios. It supplies no runtime authority; T020/T021 remain gated. Subsequent runtime feature implementation remains gated. Human observations keep whole-increment acceptance open; increment3 supplies current-permission/last-administrator enforcement, but its implementation cannot use this exception. Administrator API/BFF remains unavailable until M02-B–D.

M01-F contract tooling was squash-merged in [PR #20](https://github.com/mosesparadise/OpenIdentityAdmin/pull/20) as `6c64d34`. Production documentation remains unavailable until M02/M04 security/resource acceptance and the separate [vendor activation gate](../eng/contracts/README.md) are complete. [Feature 007 T015](../specs/007-api-contract-foundation/tasks.md) remains partial and activation/release-blocking. Pilot T021 still needs named human assessors, an interactive HTTPS client and recorded manual results; its handoff was delivered in PR #16. M01-E restart/distribution/key lifecycle was delivered in PRs #17–#19; prior evidence remains in [feature 006](../specs/006-issuer-lifecycle/verification.md).

M01-F, Admin BFF/React and the wider R1 product remain open. The five-minute administrator application-creation target remains NFR-09/M09-A, not a pilot-login target. A-001 retains actual certification as a public-release gate; all 345 ASVS controls remain pending/not_assessed.

Milestone numbers below come from the approved product roadmap. Local Spec Kit feature numbers are increments: `001-platform-foundation` and `002-issuer-foundation` both contribute to **milestone 001**. They do not complete milestones 001 and 002.

**Approved engineering batch:** feature 009 T017–T019 cover configuration-test names, the Core permission catalogue and shared lifecycle evidence, each in a separate reviewed squash PR. T017 was squash-merged in [PR #40](https://github.com/mosesparadise/OpenIdentityAdmin/pull/40), retiring27 findings after all62 CI jobs passed. T018 is the next catalogue cleanup. These are bounded portions of the still-open T013–T015 retrofit. This work does not close ADM-OPS-01/ADM-EVID-01 or enable gated feature 008 T020/T021. See the [task packet](../specs/009-engineering-standards/tasks.md#approved-focused-retrofit-batch--13-october-2026).

## Status, ownership and evidence

- **Done:** the bounded task has actual acceptance evidence. PR merge state is recorded separately.
- **Partial:** some acceptance exists; the remaining scope is explicitly listed.
- **Planned:** implementation/required acceptance is outstanding. A planned task is not a verified capability.
- **In progress:** an authorized task is being implemented or verified; its acceptance remains pending.
- **Externally blocked:** an identified dependency prevents its acceptance; independent authorized development can continue.

The implementation lead owns code/tasks/evidence; architecture, test and relevant specialist reviewers independently inspect each increment. Operator/security-assurance owners must supply deployment decisions, independent assessment and external release evidence. AI reviewers do not stand in for these owners. Every implementation feature expands the tasks below into a Spec Kit specification, plan, dependency-ordered file-level tasks, control/test links and verification record before coding.

| Feature / change | Status | Detailed tasks and evidence | Merge state |
| --- | --- | --- | --- |
| Platform foundation, grouped layout, xUnit v3/MTP | Done for bounded scope | [Tasks](../specs/001-platform-foundation/tasks.md), [foundation evidence](../specs/001-platform-foundation/verification.md), [MTP/layout evidence](../specs/001-platform-foundation/verification-mtp-layout.md) | PR #1 merged |
| Durable issuer credentials and Data Protection | Done for bounded scope | [Tasks](../specs/002-issuer-foundation/tasks.md), [evidence](../specs/002-issuer-foundation/verification.md), [operations](operations/issuer-protection.md) | [PR #2](https://github.com/mosesparadise/OpenIdentityAdmin/pull/2) squash-merged as `8079b5e` |
| Configuration references, static analysis and this roadmap | Done | [T014–T018](../specs/002-issuer-foundation/tasks.md), [quality policy](engineering/code-quality.md) | Squash-merged in PR #2 as `8079b5e` |
| Assurance register baseline | Done for bounded scope | [Feature 003 tasks](../specs/003-assurance-register/tasks.md), [register](assurance/README.md) | [PR #3](https://github.com/mosesparadise/OpenIdentityAdmin/pull/3) squash-merged as `df828d6` |
| First-party login and S256 code flow | Partial; bounded implementation and T023–T028/T030 merged; T032 and T021 handoff merged; human execution pending | [Feature 004 spec](../specs/004-first-party-login/spec.md), [32 tasks and acceptance gaps](../specs/004-first-party-login/tasks.md), [current evidence matrix](../specs/004-first-party-login/acceptance-matrix.md), [full issuer backlog](engineering/issuer-flow-backlog.md) | Pilot/operations through PRs #4–#9; corrected browser/mutation acceptance in PR #11 (`bdd45e5`); atomic capacity in PR #12 (`fca82fb`). T029 merged in PR #13. T031/T018 merged in PR #14. T032 squash-merged in PR #15 as `f1b1a4f`; [manual assessment instructions](operations/pilot-manual-assessment.md) and a results template now support the remaining T021 obligation. No human results are recorded; manual/full-feature acceptance remains open. |
| Explicit host and feature composition | Complete; merged | [Feature 005 spec](../specs/005-host-composition/spec.md), [plan](../specs/005-host-composition/plan.md), [14 completed tasks](../specs/005-host-composition/tasks.md), [evidence](../specs/005-host-composition/verification.md), [convention](engineering/host-composition.md) | [PR #8](https://github.com/mosesparadise/OpenIdentityAdmin/pull/8) squash-merged as `64d8ceb`; final head `3241d15` passed all seven gates in [run38090792266](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38090792266), including603 cases with zero skips and all nine coverage floors |
| Management contract foundation | Done for bounded tooling; production documentation/vendor acceptance remain partial | [Feature 007 tasks](../specs/007-api-contract-foundation/tasks.md), [evidence](../specs/007-api-contract-foundation/verification.md), [commands](../specs/007-api-contract-foundation/quickstart.md) | [PR #20](https://github.com/mosesparadise/OpenIdentityAdmin/pull/20) squash-merged as `6c64d34`; T015 remains open |

| Protected administrator bootstrap and storage | Done for bounded implementation acceptance; MFA and governance enforcement remain open | [Feature 008 tasks](../specs/008-administrator-foundation/tasks.md), [evidence](../specs/008-administrator-foundation/verification.md), [operator instructions](../specs/008-administrator-foundation/quickstart.md) | [PR #21](https://github.com/mosesparadise/OpenIdentityAdmin/pull/21) records final documentation checks and normal squash; bootstrap remains independently accepted; usable local administrator MFA/recovery followed in PRs #30–#31; API/BFF authority and governance enforcement remain pending |

## Milestone 001 — Platform and issuer foundation (partial)

| Task | Status | Deliverable and acceptance |
| --- | --- | --- |
| M01-A | Done | Grouped solution/Features, native Identity/OpenIddict stores, both provider migrations, isolated Aspire, health/telemetry, test/report/coverage infrastructure and evaluated AI instructions. See feature 001 evidence. |
| M01-B | Done | External distinct RSA credentials, encrypted issuer-specific DP, safe startup, reload/wrapper rollover and operator runbook. See feature 002. This does not prove cookie/code/token restart or public-key overlap. |
| M01-C | Partial; baseline verified | [Feature 003](../specs/003-assurance-register/spec.md) imports the complete official catalogue and adds register integrity checks. Final named ownership, applicability, normative crosswalks and product evidence remain pending. Determine applicability across L1–L3, create threat/control/requirement/evidence registers with owners and exceptions; maintain throughout delivery. Do not invent control identifiers or defer mapping until release. |
| M01-D | Partial, feature 004 | One real non-administrator first-party login/S256 code flow: safe explicit synthetic bootstrap, protected themed Razor form, bound browser interactions, named authentication schemes, OpenIddict validation/crypto, Minimal API application decisions, current eligibility and deliberate claims. These implemented paths have both-provider evidence. T031 closes the specified transport matrix; T032 proves representative committed current-authorization denials. [T021 assessment preparation](../specs/004-first-party-login/manual-acceptance.md) tracks the handoff; named human assessors, an interactive HTTPS client, qualified manual UI evidence and full convergence remain open. |
| M01-E | Controlled restart, distribution and key lifecycle delivered through PR #19 | [Feature 006 spec/plan/tasks](../specs/006-issuer-lifecycle/spec.md). Real session/code/token restart and two-instance behavior; single-use code concurrency/replay; staged public JWKS and signing/encryption overlap/retirement. Retain every unbuilt ISS-001–012 acceptance item explicitly. |
| M01-F | Partial; bounded tooling verified in PR #20 | [Feature 007](../specs/007-api-contract-foundation/spec.md) implements versioning/generation/client compatibility tooling with test-only representative contracts. Management API/BFF hosts do not yet implement administrator authentication: shipped docs remain unavailable. Protected same-origin BFF docs/proxy and real management-contract acceptance require M02/M04. Complete renderer inventory/advisory/notice evidence is a separate activation/public-release gate ([boundaries](../eng/contracts/README.md)); T015 and FR-16 remain partial after tooling alone. Protocol routes remain unversioned. |
| M01-G | Done | Adapt reference configuration, enforce static analysis/formatting, document the full roadmap, and adopt squash merges. Seeded rule failures and both-provider regression passed; see [quality evidence](../specs/002-issuer-foundation/verification-quality.md). |

Dependencies: M01-A/B enable M01-D/E. M01-C starts with feature 003 before the next login slice and continues throughout. M01-F tooling can proceed independently but remains required before accepted management endpoints; actual authenticated docs/proxy acceptance depends on M02-A–D and real resource contracts continue in M04-D. Milestone 001 remains partial until its whole approved acceptance passes. PR #19 final SQL issuer time was 653.141s against the unchanged 660s bound; retain that performance follow-up and isolate new contract work from that invocation.

## Milestone 002 — Secure administrator session (planned)

Prerequisites: issuer flow and durable session/key behavior; minimum administrator MFA, enrollment/recovery and current-permission controls must exist before exposing administrator access. This pulls the necessary subset of milestone 005 forward.

| Task | Deliverable and acceptance |
| --- | --- |
| M02-A | In progress: [feature 008](../specs/008-administrator-foundation/spec.md), three approved increments with separately reviewed task PRs. First increment accepted in PR #21: protected bootstrap, inert pending administrator and explicit permission/governance storage, verified on both providers. T024–T026 ceremony protection, TOTP enrollment/login and restricted recovery/security are accepted in PRs #29–#31. T027 accepted in PR #33; T028 automated scope and T029 handoff merged in PRs #34–#35. Next: ADM-OPS-01/ADM-EVID-01 investigation and T028/T029 overall acceptance, with human observations still pending. Third: T019 governance design, then gated T020/T021 current authority and last-administrator enforcement. Second-administrator onboarding remains an M05 lifecycle dependency; role assignment alone does not supply it. Admin API/BFF access remains unavailable until M02-B–D. |
| M02-B | Actual BFF code/S256/PAR login/callback/logout, same-origin secure cookies, separate protected BFF key ring, server-held tickets/tokens, idle/absolute expiry and fresh session identifiers. |
| M02-C | Two-node refresh serialization, rotation/replay response, fail-closed eligibility, CSRF/fixation/origin/proxy controls and concurrency/response-loss tests. |
| M02-D | Admin API issuer/audience/token-type/store validation and current authorization; deny bypass via direct HTTP calls. Prove specified five-second enforcement across instances. |
| M02-E | React/TypeScript shell, BFF browser contract/client, same-origin Aspire composition and shared slate/indigo light/dark tokens. Prove no browser token storage with real browser E2E. |

## Milestone 003 — Scope and audience catalogue (planned)

Prerequisite: milestone 002. Real UserInfo needed for this milestone's approved claim-policy exit must be implemented here; milestone 007 later completes the surface.

| Task | Deliverable and acceptance |
| --- | --- |
| M03-A | Scope/audience search/create/edit/delete, current permissions, safe references, concurrency and atomic audit intent on both databases. |
| M03-B | Explicit claim catalogue/reserved claims, requested/allowed/consented scope intersection and one approved resource audience. No arbitrary credential/internal claims. |
| M03-C | Prove scope policy with real tokens and scope-filtered UserInfo, exact subject, machine restrictions and current-state changes. |

## Milestone 004 — Application lifecycle (planned)

Prerequisites: milestones 002–003 and the actual protocol capabilities being offered. Do not accept an unimplemented grant merely because it is shown in a client profile.

| Task | Deliverable and acceptance |
| --- | --- |
| M04-A | Application catalogue/details/create/edit/clone/disable/delete; immutable unique client IDs, reference safety, concurrency, permission checks and atomic audit. |
| M04-B | Validated web/browser/native/machine/device profiles, exact redirects and compatible grants/endpoints/authentication. Pull required machine/device capabilities forward or mark acceptance pending. |
| M04-C | Manager-hashed one-time secrets, immediate replacement/expiry and asymmetric public-key registration. Prove secret reads, logs and lost-response handling are safe. |
| M04-D | Versioned Admin API/BFF contracts, generated TypeScript, Scalar and Swagger from authoritative specifications, compatibility gates, permission-aware docs and runnable integration examples. |
| M04-E | Real React→BFF→API→STS registration-to-token journeys, current client-disable behavior and both-provider positive/negative/concurrency evidence. |

## Milestone 005 — Accounts and strong authentication (planned)

Prerequisites: milestone 001 and secure administration from 002; administrator protection needed by 002 is delivered earlier, not postponed here.

| Task | Deliverable and acceptance |
| --- | --- |
| M05-A | Users, roles, claims, permissions, disable/unlock/delete/recovery initiation; current authorization, recent MFA and transactional last-administrator protection. |
| M05-B | Themed login/logout, confirmation/reset, optional registration disabled by default, generic recovery responses, protected mail delivery and rate/abuse controls. |
| M05-C | Full TOTP, recovery-code and passkey enrollment/use/change/recovery lifecycle, replay resistance, origin/RP-ID/user-verification checks and credential-loss paths. |
| M05-D | Fixed operator-configured OIDC federation, issuer/subject binding, proof before linking existing accounts, collision/SSRF defenses and controlled Aspire mail/provider fixtures. |

## Milestone 006 — Consent and access lifecycle (planned)

Prerequisites: milestones 003–005. Distinguish cookie, refresh, token-entry, authorization and offline access-token effects explicitly.

| Task | Deliverable and acceptance |
| --- | --- |
| M06-A | Consent policies/UI, remembered grants, scope changes, prompt/max_age/fresh authentication and correct noninteractive errors. |
| M06-B | Ordinary-client refresh eligibility, current claims/permissions, absolute lifetimes, scope narrowing, rotation/replay and concurrency; extend BFF-specific refresh behavior from 002. |
| M06-C | User-owned grants and administrator authorization/token-metadata/local-session visibility; grant/subject revocation and cross-instance five-second enforcement with measured limits. |

## Milestone 007 — Device and protocol completion (planned)

Prerequisites: milestones 003–006. Implement earlier subsets when required by accepted client/scope/BFF journeys; complete every advertised capability here.

| Task | Deliverable and acceptance |
| --- | --- |
| M07-A | Complete client-credentials behavior without user impersonation; include the required subset with milestone 004 machine-client acceptance. |
| M07-B | Device authorization/confirmation/polling, denial, expiry/replay/slow_down; complete PAR policy and its negative matrix beyond BFF use. |
| M07-C | Full introspection, revocation, UserInfo and RP-initiated logout with ownership, authentication, hints/redirects and CSRF rules. |
| M07-D | Endpoint/grant/method/content-type/error/claims/interoperability matrix, reverse-proxy/rate-limit evidence, accurate discovery and applicable official conformance automation. Password, implicit and hybrid stay disabled. |

## Milestone 008 — Audit and configuration health (planned)

Prerequisites: milestones 002–007. Every earlier security mutation already includes atomic audit intent; this milestone adds investigations, dashboards and operational rules.

| Task | Deliverable and acceptance |
| --- | --- |
| M08-A | Restricted audit search/investigation/export and diagnostics, redaction, delivery/outbox behavior, retention and tamper evidence. |
| M08-B | Unsafe-redirect/PKCE/scope/expiry/lifetime rules, deduplicated findings/resolution, dashboards, bounded jobs, leases/concurrent workers and cleanup. |
| M08-C | Public certificate/key inventory, active/retiring state and expiry alerts; no private-key UI/export. |

## Milestone 009 — Production readiness (planned; certification dependency tracked)

Prerequisites: all R1 milestones above. These tasks verify completeness; testing, security, accessibility and operability accompany each earlier feature.

| Task | Deliverable and acceptance |
| --- | --- |
| M09-A | Complete React/Razor states, externalized English strings, responsive light/dark themes, reviewed visual snapshots, WCAG 2.2 AA/manual checks and timed primary journeys. |
| M09-B | Published containers/assets and production configuration, trusted proxies, least privilege, protected bootstrap, fresh/upgrade/rolling-restart tests, key rotation and both-provider backup/restore drills meeting RPO/RTO. |
| M09-C | Published reference dataset/hardware, 30-minute load tests, latency/error targets, five-second revocation under load, availability monitoring and capacity evidence. Harness timings are not product benchmarks. |
| M09-D | Complete applicable ASVS evidence and independent assessment; security regressions; actual conformance plans and version/configuration-bound Basic OP/Config OP submission package. Basic OP suite compatibility is externally blocked under A-001; Config OP is not blocked by that specific mismatch. |
| M09-E | Complete deployment SBOM, signed provenance, notices/license decisions, vulnerability/incident procedures, retention/privacy/deletion decisions and protected evidence retention. |
| M09-F | Complete requirement/browser/provider/operations matrix, permanent regressions, critical-decision mutation coverage, unresolved-finding disposition and release convergence. |

## Milestone 010 — Extended parity (later release, outside R1)

M10-A: separately specify dynamic federation, arbitrary relying-party session propagation, overlapping shared secrets, distributed automatic signing-key management, reusable project templates and financial-grade profiles. Each needs its own acceptance and any material scope/security approval. Do not apply conventional R1 refresh assumptions to FAPI without profile-specific analysis.

## Complete PRD allocation

This table assigns every FR/NFR to planned work; it does not mark a whole product requirement complete merely because infrastructure exists.

| Requirement | Owning tasks | Current product status |
| --- | --- | --- |
| FR-01 applications | M04-A/E | Planned |
| FR-02 client profiles | M01-D, M02-B, M04-B/E, M07-A/B/D | Partial: one fixed confidential S256 pilot client; management and remaining profiles planned |
| FR-03 credentials | M04-C/E, M07-A | Planned |
| FR-04 scopes/audiences/claims | M01-D, M03-A/B/C, M06-A/B | Partial: one pilot resource/scope and deliberate claims; management planned |
| FR-05 users | M05-A/B, M06-B/C | Planned |
| FR-06 roles/permissions | M02-A/D, M05-A | Planned |
| FR-07 account/authenticator journeys | M01-D, M02-A, M05-B/C, M07-C | Partial: pilot login/lockout and administrator TOTP enrollment/login/restricted recovery accepted; full account/passkey lifecycle remains planned |
| FR-08 consent/grants | M06-A/C | Planned |
| FR-09 protocol | M01-D/E, M02-B/C, M03-C, M06-B, M07-A/B/C/D | Partial: genuine pilot code/S256 flow and denials; M01-E and broader protocols remain |
| FR-10 access visibility/revocation | M06-C | Planned |
| FR-11 BFF/API protection | M02-A/B/C/D/E | Planned |
| FR-12 audit/logging | Every mutation, M08-A, M09-E | Partial: pilot security transitions have atomic audit evidence; administration audit remains planned |
| FR-13 rules/dashboard | M08-B | Planned |
| FR-14 federation | M05-D | Planned |
| FR-15 keys/inventory | M01-B/E, M08-C, M09-B | Partial: credential/protection adapter |
| FR-16 API/docs/contracts | M01-F, M04-D | Partial: verified contract foundation in PR #20; authenticated docs, real resource contracts and vendor activation evidence remain open |
| FR-17 provider equivalence | M01-A, every affected feature, M09-B/F | Partial: implemented scope tested |
| FR-18 installation/recovery | M01-A/B/E, M02-A, M09-B | Partial: local schemas/provisioning and DP; production operations unfinished |
| FR-19 UI/theme | M01-D, M02-E, every UI, M09-A | Partial: shared tokens/themed Razor pilot; React and full manual acceptance remain planned |
| FR-20 later parity | M10-A | Outside R1 |
| FR-21 comprehensive tests | Every task, M09-F | Partial: 1,017-case accepted pilot checkpoint; full product/manual matrix remains |
| FR-22 engineering | Every task, M01-G | Ongoing |
| FR-23 AI instructions | M01-A; evaluation per feature | Initial install/evaluation done; ongoing use |
| FR-24 Aspire | M01-A, M02-E, M05-D, M09-F | Partial: hosts/providers and pilot browser journeys tested; mail/federation pending |
| FR-25 assurance | M01-C, M07-D, M09-D/E/F | Register baseline verified; no assessed controls or certification |
| NFR-01 security findings | M01-C, feature controls/reviews, M09-D | Scoped checks exist; independent assessment pending |
| NFR-02 equal providers | Every persistence/E2E task, M09-F | Partial |
| NFR-03 reference workload | M09-C | Planned |
| NFR-04 latency/error targets | M09-C | Planned |
| NFR-05 five-second enforcement | M02-D, M06-C, M09-C | Planned |
| NFR-06 RPO/RTO | M01-B/E, M09-B | Building blocks only; timed recovery pending |
| NFR-07 availability | M09-B/C/E | Deployment/operating evidence required |
| NFR-08 no secret leakage | Every feature, M02, M04-C/E, M09-D/E/F | Scoped evidence exists |
| NFR-09 usability/accessibility | Every UI, M09-A | Partial: pilot keyboard/reflow/automated accessibility; qualified manual assessment and timed journeys pending |
| NFR-10 retention | M08-A/B, M09-E | Planned |
| NFR-11 traceability/coverage | Every task, M01-C, M09-F | Partial |
| NFR-12 visual/browser regression | Feature/nightly gates, M09-A/F | T027 pilot browser work is merged with its reproduced WebKit defect corrected; PR #12 reverified all six browser cells. Wider product/manual acceptance remains planned |
| NFR-13 independent assurance | M01-C, M07-D, M09-D | Planned; Basic OP external dependency |
| NFR-14 reproducibility/contracts | M01-A/F, M02-E, M04-D, M09-F | Foundation evidence exists |

## Acceptance applied to every future feature

Use Features inside each affected project; preserve library-owned protocol/Identity behavior. Each Spec Kit task list must cover positive/deny/boundary/expiry/replay/concurrency/cancellation/dependency-failure cases at the meaningful boundary, relevant real browsers and both databases, exact contract changes, atomic audit, shared accessible theme, telemetry/runbooks, permanent defect regressions and independent reviewers. Keep the per-module coverage floors and strict no-skips evidence gate. React→BFF→API→STS E2E may not substitute a fake authentication shortcut.

Follow the [quality policy](engineering/code-quality.md), static-analysis checks and squash-merge convention. Update this register and the feature verification record when acceptance changes. A checked task requires recorded evidence; an open PR is not a merged change.

## External decisions and release gates

[A-001](amendments/A-001-certification-sequencing.md) permits implementation while Basic OP/mandatory-PKCE suite compatibility remains unresolved. It does not permit PKCE exemptions or skipped-as-passed results, and Basic OP/Config OP certification remains a public-release gate. Independent assessment, organizational ISO/SOC evidence and deployment-specific privacy/legal decisions require their qualified owners. Production deployment, Foundation communication/submission, fees and external declarations require separate authorization. No Duende migration is planned.

Engineering update (12 October 2026): [PR #23](https://github.com/mosesparadise/OpenIdentityAdmin/pull/23) squash-merged validated appsettings-backed OpenAPI metadata/viewer labels and encoding regressions. All 34 final CI jobs passed; 1,327 ordinary cases, three contract browsers and all nine coverage gates passed, with the restore-only retry recorded in feature 009 verification. Feature 009 T010–T012 are complete; legacy retrofit T013–T016 remains separate. The merged T016 design checkpoint adds no working MFA journeys.

T022 verification (12 October 2026): implementation head `4ea212d` passed all 34 CI jobs, 1,358 ordinary .NET cases and all nine coverage gates. PR #25 was normally squash-merged as `f9a5156`; this documentation-only follow-up records its acceptance. Shared protection is independent of the pilot; administrator tickets/issuance remain rejected until authoritative sessions are implemented. No working MFA journey or management access is claimed.

T023 verification (12 October 2026): PR #27 was normally squash-merged as `824220b`. Exact head `e356090` passed [all 36 CI jobs](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38291036878), 1,460 ordinary .NET cases, 25 required coverage inputs and all nine module floors. Both providers executed 51 Factors and 45 unchanged Bootstrap cases in isolated invocations. [Storage verification](../specs/008-administrator-foundation/verification.md#t023-factor-storage--12-october-2026) records accepted reviews, corrections and limits. T024 is next; administrator login, MFA pages, recovery journeys and sessions remain unavailable until later packet acceptance.

T026 verification (13 October 2026): [PR #31](https://github.com/mosesparadise/OpenIdentityAdmin/pull/31) was normally squash-merged as `c10f9d2` after head `14cd9e8` passed [full CI](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38325799594): all 58 jobs, 1,802 required nonmutation .NET cases with no skips, 45 mandatory coverage inputs and every module floor. Both providers passed 18 Recovery and 21 Security cases, all three browser engines executed 7 Accounts and 5 Security cases per provider, and all seven issuer plus six administrator behavioral mutants were killed per provider with original/restored controls. [Recovery/security verification](../specs/008-administrator-foundation/verification.md#t026--restricted-recovery-and-account-security--13-october-2026-bst) records reviews, fixes and limits. Next: complete feature 008 T027–T029 acceptance reconciliation; T017/T018 remain open until that evidence closes. Then T019–T021 implement current-permission/last-administrator governance. M02-B–D, React/BFF, human assessments and external release gates remain separate.

T027 verification (13 October 2026 UTC): [PR #33](https://github.com/mosesparadise/OpenIdentityAdmin/pull/33) squash `ab8b62d` passed all 58 exact-head CI jobs, 1,802 required nonmutation .NET cases, 45 coverage inputs and every module floor. Shared catalogue preflight and TRX bindings now reject the demonstrated malformed evidence. [T028 evidence work](../specs/008-administrator-foundation/mfa-evidence.md) adds native concurrency/clock edges, actual process failure/restart, cleanup/state races and native clipboard acceptance. It remains in progress; human password-manager and screen-reader results are not claimed.

T028 automated verification (13 October 2026): PR #34 was normally squash-merged as `fafc98d`. Exact head `d4e2a1a` passed all 58 jobs in run `38334770309`, 1,868 ordinary .NET cases, both-provider/all-engine evidence, eight administrator mutations per provider, all 45 coverage inputs and nine floors. The preceding failed/cancelled attempt remains recorded in [MFA evidence](../specs/008-administrator-foundation/mfa-evidence.md). T029's [operations runbook](operations/administrator-accounts.md), [manual assessment](operations/administrator-manual-assessment.md) and [unexecuted results template](operations/administrator-manual-results-template.md) make the remaining handoff visible. Human observations, T019–T021 governance enforcement, M02-B–D API/BFF/UI, legacy engineering debt, M01-F vendor activation and external assurance/certification are still pending.
