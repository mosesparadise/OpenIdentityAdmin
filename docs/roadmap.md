# Delivery roadmap and task register

This is the living view of **all approved work**, as of 11 October 2026. It supplements the immutable [v0.2 plan](approved-v0.2/06-spec-kit-plan.md), [PRD](approved-v0.2/00-prd.md) and [approval record](approval.md). Dates are status dates, not delivery estimates. No estimate is invented before a feature's dependencies and acceptance work are understood.

**Current merged capability (`main`):** native persistence, both-provider migrations and default health-only Aspire hosts, strict xUnit v3/MTP/coverage/supply-chain gates, AI instructions, durable issuer credentials/encrypted Data Protection, and an explicitly enabled **Development-only first-party login/code-S256 pilot**. Full pilot acceptance is still in progress; production administration, Admin BFF/React and management APIs are not implemented.

**Current delivery:** T032 authorization-state acceptance squash-merged in [PR #15](https://github.com/mosesparadise/OpenIdentityAdmin/pull/15) as `f1b1a4f`, with final head `4237252` passing all 17 checks / 1,017 cases in [run 38163339126](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38163339126). Its [execution plan](../specs/004-first-party-login/authorization-state-acceptance.md) retains the initial fixture correction and review dispositions. T031/PR #14 squash `44a0e90` remains covered.

**Current next work:** the T021 manual handoff squash-merged in [PR #16](https://github.com/mosesparadise/OpenIdentityAdmin/pull/16) as `559380e` after 13 checks / 901 ordinary cases passed. Named human assessors, an interactive HTTPS relying party and actual observations are still needed; T021/full M01-D remain open. M01-E now has a [feature 006 specification](../specs/006-issuer-lifecycle/spec.md), [confirmed plan](../specs/006-issuer-lifecycle/plan.md) and [tasks](../specs/006-issuer-lifecycle/tasks.md). The user confirmed a dedicated Aspire lifecycle test project and three sequential PRs. Restart source acceptance passed all 19 checks / 1,045 ordinary cases in [run 38177093949](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38177093949); exact final-documentation-head checks and squash delivery are recorded on [PR #17](https://github.com/mosesparadise/OpenIdentityAdmin/pull/17). [Evidence](../specs/006-issuer-lifecycle/verification.md) includes the corrected SQL scheduling failure, 13 lifecycle cases/provider, unchanged deadlines and eleven mandatory coverage inputs. Two-instance behavior is next, followed by key overlap/retirement; M01-E remains partial.

M01-F, Admin BFF/React and the wider R1 product remain open. The five-minute administrator application-creation target remains NFR-09/M09-A, not a pilot-login target. A-001 retains actual certification as a public-release gate; all 345 ASVS controls remain pending/not_assessed.

Milestone numbers below come from the approved product roadmap. Local Spec Kit feature numbers are increments: `001-platform-foundation` and `002-issuer-foundation` both contribute to **milestone 001**. They do not complete milestones 001 and 002.

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

## Milestone 001 — Platform and issuer foundation (partial)

| Task | Status | Deliverable and acceptance |
| --- | --- | --- |
| M01-A | Done | Grouped solution/Features, native Identity/OpenIddict stores, both provider migrations, isolated Aspire, health/telemetry, test/report/coverage infrastructure and evaluated AI instructions. See feature 001 evidence. |
| M01-B | Done | External distinct RSA credentials, encrypted issuer-specific DP, safe startup, reload/wrapper rollover and operator runbook. See feature 002. This does not prove cookie/code/token restart or public-key overlap. |
| M01-C | Partial; baseline verified | [Feature 003](../specs/003-assurance-register/spec.md) imports the complete official catalogue and adds register integrity checks. Final named ownership, applicability, normative crosswalks and product evidence remain pending. Determine applicability across L1–L3, create threat/control/requirement/evidence registers with owners and exceptions; maintain throughout delivery. Do not invent control identifiers or defer mapping until release. |
| M01-D | Partial, feature 004 | One real non-administrator first-party login/S256 code flow: safe explicit synthetic bootstrap, protected themed Razor form, bound browser interactions, named authentication schemes, OpenIddict validation/crypto, Minimal API application decisions, current eligibility and deliberate claims. These implemented paths have both-provider evidence. T031 closes the specified transport matrix; T032 proves representative committed current-authorization denials. [T021 assessment preparation](../specs/004-first-party-login/manual-acceptance.md) tracks the handoff; named human assessors, an interactive HTTPS client, qualified manual UI evidence and full convergence remain open. |
| M01-E | Partial; restart source accepted; distribution and rotation pending | [Feature 006 spec/plan/tasks](../specs/006-issuer-lifecycle/spec.md). Real session/code/token restart and two-instance behavior; single-use code concurrency/replay; staged public JWKS and signing/encryption overlap/retirement. Retain every unbuilt ISS-001–012 acceptance item explicitly. |
| M01-F | Planned | Pin compatible API-versioning/OpenAPI tooling; establish authoritative versioned Admin API and BFF contracts, Scalar/Swagger and generated-client/compatibility gates. Protocol routes remain unversioned. |
| M01-G | Done | Adapt reference configuration, enforce static analysis/formatting, document the full roadmap, and adopt squash merges. Seeded rule failures and both-provider regression passed; see [quality evidence](../specs/002-issuer-foundation/verification-quality.md). |

Dependencies: M01-A/B enable M01-D/E. M01-C starts with feature 003 before the next login slice and continues throughout. M01-F can proceed independently but remains required before accepted management endpoints. Milestone 001 remains partial until its whole approved acceptance passes.

## Milestone 002 — Secure administrator session (planned)

Prerequisites: issuer flow and durable session/key behavior; minimum administrator MFA, enrollment/recovery and current-permission controls must exist before exposing administrator access. This pulls the necessary subset of milestone 005 forward.

| Task | Deliverable and acceptance |
| --- | --- |
| M02-A | Protected operator-supplied initial administrator bootstrap, minimum MFA/recovery and permission model. No default password or public bootstrap endpoint. Audit bootstrap/security transitions. |
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
| FR-07 account/authenticator journeys | M01-D, M02-A, M05-B/C, M07-C | Partial: synthetic password login/lockout; administrator MFA, enrollment and recovery planned |
| FR-08 consent/grants | M06-A/C | Planned |
| FR-09 protocol | M01-D/E, M02-B/C, M03-C, M06-B, M07-A/B/C/D | Partial: genuine pilot code/S256 flow and denials; M01-E and broader protocols remain |
| FR-10 access visibility/revocation | M06-C | Planned |
| FR-11 BFF/API protection | M02-A/B/C/D/E | Planned |
| FR-12 audit/logging | Every mutation, M08-A, M09-E | Partial: pilot security transitions have atomic audit evidence; administration audit remains planned |
| FR-13 rules/dashboard | M08-B | Planned |
| FR-14 federation | M05-D | Planned |
| FR-15 keys/inventory | M01-B/E, M08-C, M09-B | Partial: credential/protection adapter |
| FR-16 API/docs/contracts | M01-F, M04-D | Planned |
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
