# Pilot acceptance and remaining work

Status date: **11 October 2026**. This is the current evidence index for feature 004, not a production-release or certification statement. The [task list](tasks.md) and [product roadmap](../../docs/roadmap.md) track the remaining work; [verification.md](verification.md) preserves the detailed chronology, including failed and superseded runs.

## What is implemented

The explicitly enabled **Development-only non-administrator pilot** has transactional synthetic bootstrap, a protected themed Razor login, browser-bound interactions, revocable local sessions, OpenIddict-owned S256 code issuance/redemption, persisted eligibility checks, deliberate claims, sanitized application audit intent and bounded fault behavior. T030 adds atomic eight-live-interaction admission on both databases. Cookie continuity defines the admission cohort; it does not identify a physical device after every cookie is cleared.

The default Aspire profile remains health-only. The test relying party keeps tokens server-side and exists inside the automated fixture; it is not a shipped demo application or Admin BFF. Usable administration, React, administrator MFA and management API contracts remain unbuilt. Passing the current suite does not establish complete coverage of every specified edge: T032 below proves its representative committed authorization-time matrix; manual and full-feature obligations remain.

## Current next item: T021 manual assessment

T032 final head `423725281a0a674b0f4916fd2eb4e1363ba8f9d6` passed all 17 checks and 1,017 ordinary cases in [run 38163339126](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38163339126), then squash-merged in PR #15 as `f1b1a4fe040a7e425557ed51ce835572264fda78`. Tested merge `918ff03` and squash share tree `e3d2ee235c54433d03362dca26571c7b1d9b37df`. Final issuer durations were PostgreSQL 452.210s and SQL Server 570.363s. The earlier source checkpoint below remains historical evidence.

The [manual handoff plan](manual-acceptance.md), [assessment instructions](../../docs/operations/pilot-manual-assessment.md) and [unexecuted result template](manual-assessment-results.md) prepare the next obligation. Named assessors and a separately configured interactive HTTPS client are still needed. No Windows/WSL2, screen-reader, password-manager or qualified WCAG results are claimed. T021/full M01-D remain open. The NFR-09 five-minute **administrator application-creation** criterion remains in M09-A; it is not a pilot-login target.

## Accepted merged checkpoint

T030 squash-merged in [PR #12](https://github.com/mosesparadise/OpenIdentityAdmin/pull/12) as `fca82fb`; T029 evidence reconciliation followed in [PR #13](https://github.com/mosesparadise/OpenIdentityAdmin/pull/13) as `237afa5`. T029 source `87492cd` passed all 17 checks and 845 ordinary cases, zero failures/skips, in [run 38151249880](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38151249880). These are prior checkpoints; the expanded T031 acceptance below supersedes their test scope.

## T032 verified source checkpoint

T032 source `8b8f12ac6095593b392738eb5592a3255ffe3a6d` passed all 17 checks and 1,017 ordinary cases with zero failures/skips in [run 38162030027](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38162030027). Final documentation-head verification and normal squash delivery are recorded on [PR #15](https://github.com/mosesparadise/OpenIdentityAdmin/pull/15); this source checkpoint does not claim its own future merge.

Source `8b8f12ac6095593b392738eb5592a3255ffe3a6d` and tested merge `e90740b124b1418313a16cb44a2a1f51c445f878` share tree `e9f148e7f780e3d198cba34f98e4a52807f01e31`.

The 37 new cases per provider cross eight representative committed restrictions with new ordinary, new silent, pending cached silent and completed fresh-login authorization. Four unchanged-state controls and a distinct current eligible subject's genuine cookie prove success and completed-subject isolation. Denials preserve existing issuance/session/interaction/audit state; ordinary reauthentication renders the actual protected form. Every restriction case restores shared metadata and completes a fresh independently validated login/exchange. Case-owned revoked/expired sessions remain terminal. No production code changed.

| Ordinary partition | Passing cases | Scope |
| --- | ---: | --- |
| Unit/architecture | 243 | Core25, Host204, Architecture5, AppHost9 |
| PostgreSQL | 300 | Persistence4, Aspire4, HTTP issuer292, including37 T032 |
| SQL Server | 300 | Same required partition |
| Chromium, Firefox, WebKit × both providers | 174 | 29 in each of six cells |
| **Total** | **1,017** | **Zero failures/skips/incomplete cases** |

Separate from that count: 60 focused WebKit observations; seven passing baseline, seven demonstrated behavioral mutation kills and seven restored passes per provider. All nine unchanged 90% line/80% branch coverage floors passed. AuthServer 96.0%/97.6%, Infrastructure 99.1%/100%, Migrator 98.2%/100%, AppHost 98.8%/100% line/branch; the other five modules 100%/100%. Issuer durations: PostgreSQL 445.764s; SQL Server 577.695s, both within the unchanged 660s deadline. Existing pinned versions and Ubuntu/Linux execution scope below remain unchanged; no Windows/WSL2 manual execution or performance SLA is claimed.

Architecture, security and test source/evidence reviews resolved all bounded findings; documentation reconciliation retains the manual/product gaps. AI review is not an independent security assessment or GitHub self-approval. The initial local fixture rate-budget failure and corrected execution are preserved in [the execution plan](authorization-state-acceptance.md). T021/full M01-D, qualified manual password-manager/screen-reader/accessibility and timed usability, M01-E/M01-F, Admin BFF/React and the wider R1 product remain open. A-001 retains actual certification as a public-release gate; all 345 ASVS controls remain pending/not_assessed.

## T031 verified source checkpoint

The following is the prior transport checkpoint, delivered in PR #14 as squash `44a0e90`; the T032 checkpoint above is current.

Corrected source `b7e6332b45a26713be168519d1ce12be48f39efa` passed all 17 checks and 943 ordinary cases with zero failures/skips in [run 38158500106](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38158500106). Final documentation-head checks and normal squash delivery are recorded on [PR #14](https://github.com/mosesparadise/OpenIdentityAdmin/pull/14); this source checkpoint does not claim its own future merge. GitHub tested merge `49ec8cad8e4162c0f792bd2beaba66ec362e4b23` has the same tree `050d964f5d81f3d94b26cbbf7584836a93946b9b` as the accepted source. T031 adds 49 HTTP cases per provider and closes the reproduced conflicting-identity and malformed-media defects. Full pilot acceptance remains open.

| Ordinary partition | Passing cases | Scope |
| --- | ---: | --- |
| Unit/architecture | 243 | Core 25, Host 204, Architecture 5, AppHost 9 |
| PostgreSQL | 263 | Persistence 4, real Aspire 4, HTTP issuer 255 |
| SQL Server | 263 | Persistence 4, real Aspire 4, HTTP issuer 255 |
| Chromium, Firefox, WebKit × both providers | 174 | 29 cases in each of six cells |
| **Total** | **943** | **Zero failed or skipped required cases** |

Separate evidence, excluded from that ordinary count and its coverage input: 60 focused WebKit observations with zero findings; seven baseline passes, seven demonstrated behavioral mutation kills and seven restored passes **per provider**. The five pre-fix transport failures on each provider are defect evidence, not acceptance; see the [before/after record](transport-acceptance.md).

All nine handwritten modules passed their unchanged **90% line / 80% branch** floors. AuthServer measured 96.0% / 97.6% line/branch; Infrastructure 99.1% / 100%; Migrator 98.2% / 100%; AppHost 98.8% / 100%; the other five 100% / 100%. Coverage is evidence for exercised code, not proof of requirement completeness. Build/formatting enforcement, seeded analyzer failures, advisory/license/secret checks and assurance integrity passed. Independent architecture, security, test and documentation reviews resolved all bounded findings; no GitHub self-approval or independent penetration test is claimed.

Execution scope: Ubuntu 24.04/Linux x64 CI; SDK 10.0.401, .NET/EF 10.0.12, OpenIddict 7.7.1, Aspire 13.6.1, xUnit v3/native MTP, Playwright 1.63.0, pinned PostgreSQL 18 and SQL Server 2022 images. Version/image locks remain authoritative. Issuer durations were PostgreSQL 421.916s and SQL Server 547.952s, within the unchanged 660s invocation deadline. These are test-run durations, not production latency or availability measurements. No Windows/WSL2 manual execution is asserted.

## Requirement-to-test evidence

Names below identify existing tests/groups under `IdentityPlatform.Issuer.Tests.Features.Protocol` or `.Browser`; links locate their definitions. Provider theories run on both actual databases; browser groups run in the six cells above. Original FR/NFR/ISS allocation is retained in [the specification](spec.md#functional-requirements) and [roadmap](../../docs/roadmap.md#complete-prd-allocation).

| Requirement / tasks | Concrete evidence at the accepted checkpoint | Remaining limit |
| --- | --- | --- |
| LOGIN-001; T004/005/007/008/010/023 | [InitializationTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InitializationTests.cs) `Repeating_explicit_bootstrap_is_audited_without_changing_credentials_or_owned_ids`; [BootstrapAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/BootstrapAcceptanceTests.cs) conflict matrix, simultaneous initialization and audit-failure rollback; the BootstrapAcceptanceTests process case in `BootstrapProcessTests.cs` and Host PilotPolicyTests | Accepted for the explicit synthetic Development profile; not production provisioning or administrator enrollment. |
| LOGIN-002; T009/010/014/017/018/025 | [MetadataTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/MetadataTests.cs) exact discovery/public JWKS; [DenialTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/DenialTests.cs) invalid authorization and proof/client-binding denials; [CodeFlowTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/CodeFlowTests.cs) independent issuer/audience/type/signature/nonce controls | T031 transport coverage below completes those named cases. No Basic OP/Config OP certification; A-001 remains active. |
| LOGIN-003; T009/011/012/015/024/026 | [PasswordEntryTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/PasswordEntryTests.cs) current eligibility and five-failure lockout; [BrowserBoundaryTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/BrowserBoundaryTests.cs) forged forms, foreign browser and cross-site POST; InteractionTests and OperationsTests session/audit/request bounds | Accepted tested password/form boundaries; no administrator MFA/AAL claim. T031 adds direct method/media/forwarding acceptance. |
| LOGIN-004; T011/012/014/015/024/030 | [RequestBindingTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/RequestBindingTests.cs) overlays, delayed max_age, tabs and one-code resume; [InteractionRaceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InteractionRaceTests.cs) exact completed-session and conditional winner; [InteractionCapacityTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InteractionCapacityTests.cs) `Frozen_seven_cookie_starts_admit_exactly_one_and_reject_the_stale_snapshot`, plus Boundary/Fault/Cancellation partial files and InteractionBudgetMigrationTests | T030 accepted including rollback, expiry/consumption recovery and legacy migration. Independent DbContexts prove database coordination, not deployed two-node M01-E behavior. |
| LOGIN-005; T011/014/016/018/024/025/032 | [CurrentStateTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/CurrentStateTests.cs) `Exchange_rechecks_current_state_even_without_scope_parameter`; [ExchangeAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/ExchangeAcceptanceTests.cs) deletion/roles/revocation/unrelated-cookie subject; PasswordEntryTests at credential entry; [AuthorizationStateTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/AuthorizationStateTests.cs) at new/cached/silent/completed authorization | T032 proves eight representative committed account/session/application/scope restrictions, protected reauthentication, exact errors, no issuance/substitution and genuine recovery. This is decision-boundary evidence, not exhaustive property combinations, concurrent administrative atomicity or instantaneous offline JWT revocation. |
| LOGIN-006; T016/017/018/025/028 | CodeFlowTests `Real_form_callback_and_independent_token_validation_succeed`, actual 60s expiry, `Concurrent_valid_redemption_has_exactly_one_winner_and_replay_fails`; ExchangeAcceptanceTests lost-response recovery; DenialTests and native-replay mutation | One-winner/replay/token-binding evidence accepted; T031 proves omitted/conflicting client credentials and unsupported grant denials. Replay verifies revocation in native token storage, not a deployed resource server. |
| LOGIN-007; T013/015/027 | [LoginBrowserTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/LoginBrowserTests.cs) genuine keyboard/CSP/storage journey, theme/reflow/automated accessibility; [BrowserAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/BrowserAcceptanceTests.cs) system theme, generic errors, expiry and no-JavaScript continuation; [reviewed visual baselines](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/Baselines/README.md), actual-Razor theme and focused regression | Automated and reviewed visual evidence accepted. Qualified screen-reader/password-manager review, complete WCAG assessment and timed usability remain open; React theme integration remains later work. |
| LOGIN-008; T009/012/019/023/026/030 | [OperationsTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/OperationsTests.cs) stopped database and audit rollback; [OperationsAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/OperationsAcceptanceTests.cs) native contention/cancellation/issuance failure/log secrecy and consume rollback; Host Pilot diagnostic reload/rate/deadline controls; capacity cancellation/fault cases | Scoped application atomicity and tested bounds accepted. Native lifecycle remains library-owned; production sinks, full audit pipeline and retention need later evidence. |
| LOGIN-009; T005/006/010/015/017–022/028–032 | Exact partition identities/counts in `eng/test-matrix.json` and CI TRX; real-provider upgrades, browser TLS positive/negative controls; [mutation manifest](../../eng/critical-mutations.json); all nine coverage gates and specialist reviews | **Partial:** human/manual/full-feature acceptance remains. No skipped, selected-out or unexecuted requirement becomes a pass. |

T031's concrete HTTP evidence is [TransportAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/TransportAcceptanceTests.cs) (client authentication and grants), [TransportRequestTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/TransportRequestTests.cs) (methods/media, persisted-state snapshots and valid forms), and [TransportForwardingTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/TransportForwardingTests.cs) (discovery, redirect, cookie and actual-HTTP TLS decisions). Together these supply LOGIN-002/003/006/009, US3 AC2 and T018 transport evidence. T018's after-code current-state matrix is complete; T032 separately supplies the representative authorization-time matrix above.

## Story and success-criterion reconciliation

| Spec scenario | Evidence disposition |
| --- | --- |
| US1 AC1–3 | Bootstrap/rollback/no-op/startup/metadata evidence above covers the bounded pilot. |
| US2 AC1 | Password-entry eligibility/lockout/generic-error and session/audit tests pass. |
| US2 AC2 | Browser antiforgery/correlation and HTTP expiry/replay/overlay denials pass. |
| US2 AC3 | Browser tabs, replacement-session rejection, freshness/silent/consent decisions and HTTP races pass; T032 supplements committed current-state authorization denials. |
| US2 AC4 | Theme/keyboard/errors/reflow/storage automation and visual review pass; **password-manager and qualified manual accessibility remain open**. |
| US3 AC1 | Genuine callback/exchange plus independent public-key claim/token validation pass. |
| US3 AC2 | Existing proof/client/redirect/scope denials and T031 client-authentication/unsupported-grant/transport cases pass on both providers. |
| US3 AC3 | Actual code expiry, single-winner concurrent redemption, replay and lost-response/new-flow recovery pass. |
| US3 AC4 | Committed changes before redemption and unrelated-browser subject isolation pass; T032 adds representative authorization-time restrictions and current eligible distinct-subject isolation. |
| US3 AC5 | Bounded outage/cancellation, readiness/liveness, diagnostic secrecy and audit-failure cases pass for tested sinks/configuration. |

| Criterion | Current status |
| --- | --- |
| SC-001 | Both databases pass the automated pilot journeys, including T032; full feature/manual acceptance remains open. |
| SC-002 | Accepted tested tamper/expiry/replay and exactly-one-winner behavior on both providers; M01-E cross-node testing is separate. |
| SC-003 | Mapping is reconciled here and coverage/static floors remain enforced; T032 PR records its final documentation-head checks. Manual/product gaps remain explicit; mapping a gap does not close it. |
| SC-004 | Automated six-cell browser/visual criterion has evidence; full US2 AC4 and qualified manual accessibility remain open. |
| SC-005 | T026's stopped-database bound (10s plus 1s scheduling tolerance), other bounded fault observations and metadata restriction remain covered. See [operations evidence](operations-acceptance.md); no universal native-transaction SLA is inferred. |

## Next work and release gates

| Work | Required result / owner role |
| --- | --- |
| Full M01-D / T021 | Execute the [manual assessment pack](../../docs/operations/pilot-manual-assessment.md) with qualified password-manager/screen-reader/accessibility review, findings/retests and full-feature convergence. QA/UX; named human assessors and configured interactive HTTPS relying party still pending. Timed administrator application-creation usability remains NFR-09/M09-A, not a pilot-login threshold. |
| M01-E | Actual session/code/token restart and two deployed instances, cross-node one-use behavior, staged public signing/encryption overlap/retirement. ENG/OPS; use a new Spec Kit increment before code. |
| M01-F | Versioned Admin API/BFF contracts, compatible current OpenAPI tooling, Scalar and Swagger, generated-client/compatibility gates; keep protocol routes unversioned. ENG/API. |
| M02–M08 | Administrator enrollment/MFA/BFF/React, management, remaining protocol/account/federation journeys, operational UI, audit pipeline and retention. [Full roadmap](../../docs/roadmap.md). |
| M09 / assurance | Named owners, applicability and independent ASVS assessment, workload/recovery/operations evidence, privacy/accessibility and actual Basic OP/Config OP certification. All 345 ASVS rows remain pending/not_assessed. A-001 preserves certification as a public-release gate; ISO/SOC/NIST claims require their distinct scope/evidence. |

A-002 expired on recurrence; its theme finding was reproduced, corrected and regression-tested in PR #11. It is not an active waiver. T029 does not grant a new exception, authorize deployment or approve an external submission. No Duende migration is in scope.
