# Pilot acceptance and remaining work

Status date: **11 October 2026**. This is the current evidence index for feature 004, not a production-release or certification statement. The [task list](tasks.md) and [product roadmap](../../docs/roadmap.md) track the remaining work; [verification.md](verification.md) preserves the detailed chronology, including failed and superseded runs.

## What is implemented

The explicitly enabled **Development-only non-administrator pilot** has transactional synthetic bootstrap, a protected themed Razor login, browser-bound interactions, revocable local sessions, OpenIddict-owned S256 code issuance/redemption, persisted eligibility checks, deliberate claims, sanitized application audit intent and bounded fault behavior. T030 adds atomic eight-live-interaction admission on both databases. Cookie continuity defines the admission cohort; it does not identify a physical device after every cookie is cleared.

The default Aspire profile remains health-only. The test relying party keeps tokens server-side and exists inside the automated fixture; it is not a shipped demo application or Admin BFF. Usable administration, React, administrator MFA and management API contracts remain unbuilt. Passing the current suite does not establish complete coverage of every specified edge: T031/T032 below retain the gaps found during this reconciliation.

## Accepted merged checkpoint

[PR #12](https://github.com/mosesparadise/OpenIdentityAdmin/pull/12) squash-merged T030 as **`fca82fb363ae90287633f720920a1daeaaa331da`**. Its final source head **`4cdb0437d7e26c93cdbe276ca3078505df783e14`** passed all **17 checks** in [run 38148540658](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38148540658). GitHub tested merge checkout `29bbc45068bfc52a44be0edeea8f4fdf5d269399`, whose tree matches that source head. T029 changes documentation/register references and their supporting assurance fixtures, without runtime product changes; its own exact-head verification and squash delivery are recorded on its PR.

| Ordinary partition | Passing cases | Scope |
| --- | ---: | --- |
| Unit/architecture | 243 | Core 25, Host 204, Architecture 5, AppHost 9 |
| PostgreSQL | 214 | Persistence 4, real Aspire 4, HTTP issuer 206 |
| SQL Server | 214 | Persistence 4, real Aspire 4, HTTP issuer 206 |
| Chromium, Firefox, WebKit × both providers | 174 | 29 cases in each of six cells |
| **Total** | **845** | **Zero failed or skipped required cases** |

Separate evidence, excluded from that ordinary count and its coverage input: 60 focused WebKit observations with zero findings; seven baseline passes, seven demonstrated behavioral mutation kills and seven restored passes **per provider**. Source hashes and mutation artifacts were independently checked. The failed/superseded cancellation diagnostics are not accepted runs; the temporary diagnostic workflow was removed before this final checkpoint.

All nine handwritten modules passed their unchanged **90% line / 80% branch** floors. AuthServer measured 95.8% / 97.5%; Infrastructure 99.1% / 100%; Migrator 98.2% / 100%; AppHost 98.8% / 100%; the other five 100% / 100%. Coverage is evidence for exercised code, not proof of requirement completeness. Build/formatting enforcement, seeded analyzer failures, advisory/license/secret checks and assurance integrity also passed. Independent architecture, security, test, performance and documentation reviews had no remaining findings in T030's scope; no GitHub self-approval or independent penetration test is claimed.

Execution scope: Ubuntu 24.04/Linux x64 CI; SDK 10.0.401, .NET/EF 10.0.12, OpenIddict 7.7.1, Aspire 13.6.1, xUnit v3 with native MTP, Playwright 1.63.0, pinned PostgreSQL 18 and SQL Server 2022 images. Version/image locks remain authoritative. Issuer durations were PostgreSQL 366.893s and SQL Server 510.201s; the slowest browser cell was PostgreSQL WebKit at 355.929s, within the unchanged 660s invocation deadline. These are test-run durations, not production latency or availability measurements. No Windows/WSL2 manual execution is asserted.

## Requirement-to-test evidence

Names below identify existing tests/groups under `IdentityPlatform.Issuer.Tests.Features.Protocol` or `.Browser`; links locate their definitions. Provider theories run on both actual databases; browser groups run in the six cells above. Original FR/NFR/ISS allocation is retained in [the specification](spec.md#functional-requirements) and [roadmap](../../docs/roadmap.md#complete-prd-allocation).

| Requirement / tasks | Concrete evidence at the accepted checkpoint | Remaining limit |
| --- | --- | --- |
| LOGIN-001; T004/005/007/008/010/023 | [InitializationTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InitializationTests.cs) `Repeating_explicit_bootstrap_is_audited_without_changing_credentials_or_owned_ids`; [BootstrapAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/BootstrapAcceptanceTests.cs) conflict matrix, simultaneous initialization and audit-failure rollback; the BootstrapAcceptanceTests process case in `BootstrapProcessTests.cs` and Host PilotPolicyTests | Accepted for the explicit synthetic Development profile; not production provisioning or administrator enrollment. |
| LOGIN-002; T009/010/014/017/018/025 | [MetadataTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/MetadataTests.cs) exact discovery/public JWKS; [DenialTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/DenialTests.cs) invalid authorization and proof/client-binding denials; [CodeFlowTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/CodeFlowTests.cs) independent issuer/audience/type/signature/nonce controls | T031 retains missing client-authentication/unsupported-grant/transport cases. No Basic OP/Config OP certification; A-001 remains active. |
| LOGIN-003; T009/011/012/015/024/026 | [PasswordEntryTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/PasswordEntryTests.cs) current eligibility and five-failure lockout; [BrowserBoundaryTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/BrowserBoundaryTests.cs) forged forms, foreign browser and cross-site POST; InteractionTests and OperationsTests session/audit/request bounds | Accepted tested password/form boundaries; no administrator MFA/AAL claim. T031 retains untested transport edges. |
| LOGIN-004; T011/012/014/015/024/030 | [RequestBindingTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/RequestBindingTests.cs) overlays, delayed max_age, tabs and one-code resume; [InteractionRaceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InteractionRaceTests.cs) exact completed-session and conditional winner; [InteractionCapacityTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/InteractionCapacityTests.cs) `Frozen_seven_cookie_starts_admit_exactly_one_and_reject_the_stale_snapshot`, plus Boundary/Fault/Cancellation partial files and InteractionBudgetMigrationTests | T030 accepted including rollback, expiry/consumption recovery and legacy migration. Independent DbContexts prove database coordination, not deployed two-node M01-E behavior. |
| LOGIN-005; T011/014/016/018/024/025 | [CurrentStateTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/CurrentStateTests.cs) `Exchange_rechecks_current_state_even_without_scope_parameter`; [ExchangeAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/ExchangeAcceptanceTests.cs) deletion/roles/revocation/unrelated-cookie subject; PasswordEntryTests at credential entry | **Partial:** T032 must directly prove committed account/session and application/scope changes at a new/resumed authorization boundary. Shared-policy source and redemption tests do not prove that HTTP boundary. No instantaneous offline JWT revocation claim. |
| LOGIN-006; T016/017/018/025/028 | CodeFlowTests `Real_form_callback_and_independent_token_validation_succeed`, actual 60s expiry, `Concurrent_valid_redemption_has_exactly_one_winner_and_replay_fails`; ExchangeAcceptanceTests lost-response recovery; DenialTests and native-replay mutation | One-winner/replay/token-binding evidence accepted; T031 retains omitted/conflicting client credentials and unsupported grant denials. Replay verifies revocation in native token storage, not a deployed resource server. |
| LOGIN-007; T013/015/027 | [LoginBrowserTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/LoginBrowserTests.cs) genuine keyboard/CSP/storage journey, theme/reflow/automated accessibility; [BrowserAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/BrowserAcceptanceTests.cs) system theme, generic errors, expiry and no-JavaScript continuation; [reviewed visual baselines](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/Baselines/README.md), actual-Razor theme and focused regression | Automated and reviewed visual evidence accepted. Qualified screen-reader/password-manager review, complete WCAG assessment and timed usability remain open; React theme integration remains later work. |
| LOGIN-008; T009/012/019/023/026/030 | [OperationsTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/OperationsTests.cs) stopped database and audit rollback; [OperationsAcceptanceTests](../../tests/aspire/IdentityPlatform.Issuer.Tests/Features/Protocol/OperationsAcceptanceTests.cs) native contention/cancellation/issuance failure/log secrecy and consume rollback; Host Pilot diagnostic reload/rate/deadline controls; capacity cancellation/fault cases | Scoped application atomicity and tested bounds accepted. Native lifecycle remains library-owned; production sinks, full audit pipeline and retention need later evidence. |
| LOGIN-009; T005/006/010/015/017–022/028–032 | Exact partition identities/counts in `eng/test-matrix.json` and CI TRX; real-provider upgrades, browser TLS positive/negative controls; [mutation manifest](../../eng/critical-mutations.json); all nine coverage gates and specialist reviews | **Partial:** T031/T032 and human/manual/full-feature acceptance remain. No skipped, selected-out or unexecuted requirement becomes a pass. |

## Story and success-criterion reconciliation

| Spec scenario | Evidence disposition |
| --- | --- |
| US1 AC1–3 | Bootstrap/rollback/no-op/startup/metadata evidence above covers the bounded pilot. |
| US2 AC1 | Password-entry eligibility/lockout/generic-error and session/audit tests pass. |
| US2 AC2 | Browser antiforgery/correlation and HTTP expiry/replay/overlay denials pass. |
| US2 AC3 | Browser tabs, replacement-session rejection, freshness/silent/consent decisions and HTTP races pass; T032 supplements committed current-state authorization denials. |
| US2 AC4 | Theme/keyboard/errors/reflow/storage automation and visual review pass; **password-manager and qualified manual accessibility remain open**. |
| US3 AC1 | Genuine callback/exchange plus independent public-key claim/token validation pass. |
| US3 AC2 | Existing proof/client/redirect/scope denials pass; **T031 retains missing client-authentication and unsupported-grant transport cases**. |
| US3 AC3 | Actual code expiry, single-winner concurrent redemption, replay and lost-response/new-flow recovery pass. |
| US3 AC4 | Committed changes before redemption and unrelated-browser subject isolation pass; LOGIN-005 additionally requires T032 authorization-time evidence. |
| US3 AC5 | Bounded outage/cancellation, readiness/liveness, diagnostic secrecy and audit-failure cases pass for tested sinks/configuration. |

| Criterion | Current status |
| --- | --- |
| SC-001 | **Partial:** both databases pass the selected journeys; T031/T032 retain specified denial gaps. |
| SC-002 | Accepted tested tamper/expiry/replay and exactly-one-winner behavior on both providers; M01-E cross-node testing is separate. |
| SC-003 | Mapping is reconciled here and coverage/static floors remain enforced; T029 PR records its final documentation-head checks. Mapping a gap does not close it. |
| SC-004 | Automated six-cell browser/visual criterion has evidence; full US2 AC4 and qualified manual accessibility remain open. |
| SC-005 | T026's stopped-database bound (10s plus 1s scheduling tolerance), other bounded fault observations and metadata restriction remain covered. See [operations evidence](operations-acceptance.md); no universal native-transaction SLA is inferred. |

## Next work and release gates

| Work | Required result / owner role |
| --- | --- |
| **T031 — next implementation task** | Both-provider transport acceptance: invalid client authentication, unsupported grants and wrong methods/content types must produce contract-appropriate denials without issuance. Spoofed forwarding headers must not influence canonical issuer/callback/TLS decisions; otherwise valid requests may succeed with headers ignored. Include genuine eligible controls, recovery and safe diagnostics. ENG/QA with security review. |
| **T032** | Establish a genuine session, commit representative account/session and application/scope restrictions, then test new/resumed authorization including silent requests; prove no code/subject substitution and genuine recovery on both providers. ENG/QA with security review. |
| Full M01-D / T021 | Complete T031/T032 and qualified manual password-manager/screen-reader/accessibility work; preserve timed usability under NFR-09/M09-A. QA/UX; named human assessors still pending. |
| M01-E | Actual session/code/token restart and two deployed instances, cross-node one-use behavior, staged public signing/encryption overlap/retirement. ENG/OPS; use a new Spec Kit increment before code. |
| M01-F | Versioned Admin API/BFF contracts, compatible current OpenAPI tooling, Scalar and Swagger, generated-client/compatibility gates; keep protocol routes unversioned. ENG/API. |
| M02–M08 | Administrator enrollment/MFA/BFF/React, management, remaining protocol/account/federation journeys, operational UI, audit pipeline and retention. [Full roadmap](../../docs/roadmap.md). |
| M09 / assurance | Named owners, applicability and independent ASVS assessment, workload/recovery/operations evidence, privacy/accessibility and actual Basic OP/Config OP certification. All 345 ASVS rows remain pending/not_assessed. A-001 preserves certification as a public-release gate; ISO/SOC/NIST claims require their distinct scope/evidence. |

A-002 expired on recurrence; its theme finding was reproduced, corrected and regression-tested in PR #11. It is not an active waiver. T029 does not grant a new exception, authorize deployment or approve an external submission. No Duende migration is in scope.
