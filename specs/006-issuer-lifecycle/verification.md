# Issuer lifecycle verification — restart delivered; distribution source accepted

The user confirmed the dedicated lifecycle test project and three sequential PRs on 11 October 2026. PR #17 delivered restart as squash `a6b4dfb`. Distribution source acceptance is recorded in [run 108 below](#distribution-source-acceptance--run-108): all 21 checks and 1,097 ordinary cases passed. Final documentation-head checks and normal squash delivery remain the [PR #18](https://github.com/mosesparadise/OpenIdentityAdmin/pull/18) delivery gate. Key overlap/retirement, full M01-E, T021 human assessment, M01-F and public-release gates remain open.

Earlier sections retain historical experiments and checkpoints; their counts and pending statements describe those earlier stages. The latest disposition is the run 108 section.

Spec Kit template resolution, setup-plan/setup-tasks and prerequisites ran. No extension hooks exist. The requirements checklist and independent design review are complete. The prior accepted checkpoint is PR #16 squash `559380e952059383741a7bc75c259b5bb39ed1e1`; its 13 checks/901 ordinary cases do not establish lifecycle behavior.

## Implementation and review

The new native xUnit v3/MTP project reuses explicit fixture source links, with 13 required restart cases per provider. Existing AppHost harness tests add process-generation and accumulated-cleanup controls. Each provider receives its own <=660-second lifecycle invocation with child-process coverage. The initial combined SQL job exceeded 20 minutes; the corrected design uses separate standard/lifecycle jobs, each retaining that same limit. Both new lifecycle coverage artifacts are mandatory alongside the original inputs (eleven for the full browser matrix). Corrected scheduling passed in run 38177093949 below.

Independent architecture/security/test source reviews identified and corrected: cancellation that could abandon stopped callbacks; replay snapshots taken too late; missing session-inventory and completed one-shot generation checks; unauthenticated rather than authenticated wrong-client controls; generic denials masking native binding failures; broad continuation/wrapper failure oracles; incomplete JWKS private-field guards; contradictory TRX counters; and missing lifecycle catalogue/manifest checks. Architecture/security/test source reviews passed after corrections. Independent security review accepted both-provider restart evidence; the complete source/evidence disposition is recorded below.

## Experiments

- Initial compilation exposed missing Aspire testing namespace and formatting/static-analysis violations; fixed without disabling rules.
- First PostgreSQL run failed: Aspire reports a deliberately stopped .NET project as `Finished`, while the harness waited only for `Exited`. The fixture was invalidated, remaining cases failed closed, and teardown attempted all resources. MTP also refused duplicate test/cleanup result identities. This is failed evidence, not a pass. Terminal handling now accepts explicit `Finished`/`Exited` while independently requiring OS process exit.
- Corrected PostgreSQL experiment at the initial implementation checkpoint: 13/13 passed, zero skipped, 309.106s module duration (309.545s outer run). This precedes the final descendant/health-freshness/accumulation changes and does not accept them.
- Additional review/inspection identified Aspire's `dotnet` launcher versus the actual AuthServer child. The adapter now tracks both using Linux PID/start-tick identities, rejects a surviving orphan child, recognizes zombie/dead states, and waits within bounded termination budgets. A private locked recorder prevents concurrent observers from losing captured generations; the unit control retains all 64 concurrent additions.
- A malformed synthetic `/proc` fixture was rejected by the new parser unit test; its missing field was corrected. The strengthened current source subsequently built with zero warnings/errors and passed all 245 unit-profile cases (Core 25, Host 204, Architecture 5, AppHost 11); provider reruns were pending at that experiment; the corrected checkpoint below supersedes it.
- Initial full CI source checkpoint passed all six browser cells, unit, dependency/license/secret, assurance and focused contrast checks. Static analysis rejected import ordering in the new session test; ordering was corrected. This initial run did not establish provider/mutation/coverage acceptance; see the corrected checkpoint below.
- Local Python checks: lifecycle evidence selection (4 tests), existing evidence parser (8), browser matrix (8) passed. These do not replace provider execution.

- Final strengthened local PostgreSQL experiment: all 13 exact lifecycle identities passed, zero skipped, 365.308s module / 366.071s outer duration. Recorded process replacement observations were 15.215–21.628s; these include the deliberately rejected wrapper startup where applicable and are not production recovery objectives.
- Security follow-up added a successful `IssuerCredentials.Load` and matching wrapping-certificate thumbprint assertion before the unrelated-wrapper child launch. The temporary loaded bundle is disposed before startup. This distinguishes valid replacement material from inability to decrypt the old populated ring. The focused original-cookie/wrapper PostgreSQL case passed (1/1, zero skips, 56.384s module / 56.845s outer); build had zero warnings/errors. The corrected CI checkpoint below includes this additional assertion on both providers.

## Restart traceability

All names below are in `IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart`; each row executes separately for PostgreSQL and SQL Server. The strict discovery gate requires 13 distinct identities per provider, all executed with passing rows, complete counters, matching definitions/entries and no run errors.

| Plan / requirements | Test class and method | Cases per provider / evidence |
| --- | --- | --- |
| RST-01; LIFE-001/002/004 | `SessionContinuityTests.Original_cookie_survives_process_replacement_without_login_or_ticket_refresh` | 1; original identity/authentication time, unchanged complete session inventory, no replacement ticket or credential fallback; absent/tampered cookie denies. |
| RST-02; LIFE-002/008 | `SessionContinuityTests.Original_pending_form_keeps_antiforgery_and_browser_binding_after_restart` | 1; original form/cookies succeed; invalid antiforgery and foreign browser with valid own antiforgery fail before owner consumption with unchanged persisted state. |
| RST-03; LIFE-002/003 | `SessionContinuityTests.Completed_continuation_retains_owner_binding_and_single_consumption_after_restart` | 1; exact foreign-browser callback/error/state denial, original completed owner identity, single consumption, replay denial. |
| RST-04; LIFE-003 | `CodeContinuityTests.Exact_live_code_retains_bindings_and_redeems_after_restart` | 3; proof/client/redirect native errors ID2052/ID2069/ID2072 followed by exact-code valid redemption and replay denial. Wrong client is independently authenticated. |
| RST-05; LIFE-003/005 | `CodeContinuityTests.Still_unexpired_redeemed_code_remains_replayed_and_revokes_linked_tokens` | 1; native ID2010 while original code remains unexpired, linked token status revoked, no new issuance. |
| RST-08; LIFE-003/005 | `CodeContinuityTests.Original_code_reaches_unchanged_real_expiry_and_fresh_flow_recovers` | 1; original native lifetime <=60s, real expiry ID2016, fresh genuine recovery. |
| RST-06; LIFE-004/008 | `ProtectionContinuityTests.Original_tokens_validate_with_fresh_public_keys_and_new_tokens_with_retained_keys` | 1; original public tokens and fresh JWKS, new tokens and retained public keys, tamper/unrelated-key denials, all RSA private fields including `oth` rejected. |
| RST-09; LIFE-001/008 | `ProtectionContinuityTests.Populated_ring_rejects_unrelated_wrapper_then_original_cookie_recovers` | 1; valid unrelated wrapper, fixed safe exit 2 without readiness, restored original wrapper/ring and original cookie recovery. |
| RST-07; LIFE-005 | `EligibilityContinuityTests.Restriction_committed_while_issuer_stopped_denies_original_cookie_then_fresh_login_recovers` | 3; revoked/expired session or disabled account committed while stopped; denial without issuance; bounded restoration only for reversible account restriction and explicit fresh-session recovery. |

Every case uses `RestartIssuerAsync`: original launcher/actual AuthServer child termination, distinct new generations, fresh health report and HTTPS 200, unchanged canonical issuer/callback/database IDs, completed bootstrap/migrator generations and original credential/ring hashes. Captured generations accumulate under a lock; teardown attempts all resources and verifies containers removed and child generations no longer executing before accepting the fixture. Linux zombie/dead records are non-executing; unreadable/malformed evidence fails. Shared AppHost harness controls reject no-op/stale/reused-PID observations and surviving orphan children, and retain all 64 concurrent generation additions.

Live-code success and replay assertions require more than 10 seconds of original validity at their decision boundaries; restart cannot replace the code or extend its 60-second native lifetime. The helper's 45-second restart and nested 20-second negative-start limits remain unchanged. No new production endpoint, persistence model, credential policy or coverage exclusion was added.

The corrected checkpoint below supplies provider timing, exact source/merge trees, complete regression/coverage and review evidence; final PR delivery remains a separate gate.


## CI scheduling failure and correction

[Run 38175469170](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38175469170), source `d5af709133f6391a324bf6309a31d02de1cb57c6`, tested merge `e1a9daeaa5151184f220e48c834328377451b757`, tree `34669d9fbfd59d2cfb808073b11be020e37430a7` (identical source/merge trees), is **not accepted full-matrix evidence**. It passed unit 245, all six browser cells (174), PostgreSQL 313 including lifecycle 13, both seven-mutation jobs, static analysis, dependency/license/secret, assurance and focused WebKit contrast (60 observations, zero findings).

SQL Server passed persistence 4 (41.702s), AppHost 4 (216.283s), and issuer 292 (620.761s) before beginning lifecycle. The whole job reached its 20-minute budget during lifecycle execution; cancellation is not a lifecycle pass and coverage was skipped. GitHub's terminal cleanup killed remaining processes; this interrupted run is not evidence of successful fixture teardown. PostgreSQL completed in 13m52s; its lifecycle MTP module took 288.539s (TRX start-to-finish 288.362s), and restart observations were 12.909–17.780s. All live-code assertions retained their strict original validity margin.

The correction adds two provider lifecycle rows to the existing verify matrix. `--partition standard` retains all 300 existing provider cases and their artifact names; `--partition lifecycle` runs all 13 lifecycle cases under distinct provider-lifecycle names; the default remains all 313. A regression proves the two selections are disjoint and their union preserves the complete profile, and rejects unit/browser misuse. Coverage now requires both added artifacts; missing or empty inputs cannot hide behind passing standard coverage. All five verify jobs are coverage prerequisites. No test/job deadline, skip rule, security policy, module floor or exclusion changed. No new production source or dependency was added.

Independent architecture review accepted this scheduling correction as ordinary implementation work under the approved scope. Local selection tests now pass 5/5, browser/coverage-input tests 8/8, evidence tests 8/8 and coverage-floor tests 3/3. Both lifecycle artifacts and the complete corrected-source regression/coverage subsequently passed as recorded below. The final documentation-head delivery checks remain mandatory.

## Corrected restart evidence

[Run 38177093949](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38177093949) tests source `dcddc926a368fcb9817333dd996d7b30175cfa1d` through merge `8f325b882374373e411e48d561ccd69ecd7ee4b6`, both tree `c31a0560e875ad6c22783a02c2b226f8e083e02c`, based on main `559380e952059383741a7bc75c259b5bb39ed1e1`. Both lifecycle jobs passed, including the replacement-certificate positive control. All 19 checks subsequently passed, including complete regression and combined coverage, with no required case skipped or retried.

| Provider | Exact lifecycle cases | MTP module duration | TRX start→finish | Whole lifecycle CI job | Observed replacements |
| --- | --- | --- | --- | --- | --- |
| PostgreSQL | 13 passed, 0 failed/skipped | 312.055s | 311.867s | 6m18s | 13.483–17.328s |
| SQL Server | 13 passed, 0 failed/skipped | 357.319s | 357.045s | 7m00s | 14.614–20.106s |

Whole-job durations use the first and last timestamped job log entries, including setup/post-job cleanup. MTP module, outer runner and TRX intervals differ and are not interchangeable. The final local PostgreSQL `python3 scripts/verify.py PostgreSql --partition lifecycle` run also passed the exact 13 cases with zero skips, 333.249s module / 333.694s outer. It exercised the corrected partition, native discovery and strict evidence gate, not just a focused test filter.

Evidence paths in the retained `foundation-<provider>-lifecycle` artifacts are `tests/<provider>-lifecycle/Issuer.Lifecycle.trx` and `coverage/<provider>-lifecycle.cobertura.xml`. Independent security review parsed both reports, matched unique identities/definitions/entries/counters and all 13 process replacement observations, verified the source/merge tree identity, and accepted the bounded restart evidence and runbook. Independent architecture and test reviews accepted the partition correction; the test reviewer also identified and corrected the quickstart's partition-specific report path. No GitHub self-approval is claimed.

Execution uses Ubuntu 24.04 CI and a Linux local harness; no Windows/WSL2 manual run is asserted. Pinned versions remain SDK 10.0.401, .NET/EF 10.0.12, OpenIddict 7.7.1, Aspire 13.6.1, xUnit v3 MTP-v2 package 4.0.2 and MTP TRX 2.5.1. Database images remain PostgreSQL 18 digest `74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336` and SQL Server 2022 digest `4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`. All data and credentials are synthetic and disposable.


## Complete corrected-source acceptance and delivery gate

The retained ordinary TRX reports were independently counted against the committed manifest: **1,045 passed, zero failures/skips/incomplete cases** across 18 reports. Exact lifecycle discovery/execution checks passed for both providers. CI's 19 successful checks include static analysis/formatting, dependency/license/secret checks, assurance integrity, all selected browser/provider cells, focused WebKit contrast, both mutation jobs and combined coverage.

| Partition | Required/passed cases |
| --- | --- |
| Unit / architecture / harness | 245 (Core 25, Host 204, Architecture 5, AppHost 11) |
| PostgreSQL standard | 300 (Persistence 4, AppHost 4, Issuer 292) |
| SQL Server standard | 300 (same partition) |
| PostgreSQL lifecycle | 13 |
| SQL Server lifecycle | 13 |
| Chromium / Firefox / WebKit on both providers | 174 (29 × 6) |

Separate from the ordinary count: the focused contrast experiment passed 60 observations with zero findings; each provider's mutation evidence passed seven baselines, killed seven deliberately weakened decisions through demonstrated behavioral bypass, then passed all seven restored cases. Production source hashes and the tested merge identity match; sources were restored. Those deliberate mutant failures are not ordinary test failures.

All **eleven** required child-process coverage inputs were present and nonempty before merging. All nine unchanged 90% line/80% branch module floors passed:

| Module | Lines | Branches |
| --- | --- | --- |
| Core | 100.0% | 100.0% |
| Infrastructure | 99.1% | 100.0% |
| ServiceDefaults | 100.0% | 100.0% |
| Migrator | 98.2% | 100.0% |
| AuthServer | 96.0% | 98.7% |
| Admin.Api | 100.0% | 100.0% |
| Admin.Bff | 100.0% | 100.0% |
| Worker | 100.0% | 100.0% |
| AppHost | 98.8% | 100.0% |

The standard PostgreSQL job took 11m01s; issuer module 453.597s. The standard SQL job took 16m07s; issuer module 636.720s (637.363s outer summary). The latter remains within the unchanged 660-second invocation deadline; this single observation does not establish a performance guarantee. Separate lifecycle jobs remove the demonstrated aggregate-budget failure without increasing any bound. All successful fixture reports completed with their cleanup assertions; the final local run also left no test containers or executing IdentityPlatform applications.

Architecture, security and test reviews corrected the source/evidence issues listed above and accepted the resulting bounded restart implementation. Final documentation-head checks, review-thread state, expected head and normal squash delivery must still be verified on PR #17. No branch protection bypass, GitHub self-approval, production deployment, formal accessibility acceptance or certification is implied.

**Disposition:** US1 / SC-L01 and the restart portions of SC-L02/05/06 are accepted at the corrected source checkpoint. LIFE-006 / US2 / SC-L03 (two actual nodes) and LIFE-007 / US3 / SC-L04 (staged signing/encryption overlap) remain unbuilt. M01-E / ISS-003 stays partial. T021 human assessment, M01-F, wider R1, backup recovery and all external/public-release obligations remain open. The next authorized increment begins only after PR #17 is normally squash-merged and its concrete topology/structure is confirmed and reviewed.


## Distribution design checkpoint (T013)

Restart PR #17 is squash-merged as `a6b4dfbde660162197e188715a4ba4056cd14772`; final head `3870ffaba5bb8f202fa1cff7ded89d7a78af0903` passed all 19 checks in run `38178327657`, with 1,045 ordinary passes and no failures/skips. The tested merge and delivered squash share tree `97df4e105983271cd6f10644596824d277417860`.

The user confirmed test-only routing, a second real issuer in the test Aspire graph and the existing lifecycle Fixtures structure. Architecture review accepted direct process-owned listener proof, immutable per-generation transports and an explicit environment allowlist. Security findings added per-request/redirect guards, explicit token/JWKS transport, authorization as well as exchange after committed restrictions, valid incompatible encryption-key recovery, TLS rejection and accurate concurrent/replay semantics. Test/Aspire design review passes the updated contract; exact catalogue union and mandatory story coverage are implementation obligations. These are design dispositions, not executed distribution results.

Spec Kit prerequisites resolved feature006; all eight requirements-quality checklist items are checked and no extension hooks exist. Read-only consistency analysis: all 11 LIFE requirements map to T001–T018; US2 cases DST-01–09 cover LIFE-002/003/004/005/006/008 and T014, with T015 carrying LIFE-009/010 execution. US1 is delivered and US3 remains explicitly deferred until US2 merge. No constitution conflict, unmapped task or unresolved critical design finding was found. Implementation proceeds only within the reviewed contract; any experiment contradicting it must be investigated, not accepted by widening an oracle.


## Distribution implementation and evidence

The implementation adds no production source or dependency. Six helper cases raise the unit inventory from 245 to 251. The lifecycle project contains the unchanged 13 Restart and 23 Distribution cases per provider; the complete ordinary matrix is 1,097 cases, pending full CI acceptance. Separate story invocations retain the 660-second deadline and 20-minute CI jobs. The full coverage matrix now requires 13 inputs.

| Cases | Distribution test methods | Cases/provider |
| --- | --- | --- |
| DST-01; LIFE-002/006/008 | `Original_form_crosses_nodes_with_antiforgery_and_browser_binding` | 2 directions |
| DST-02; LIFE-002/003/006 | `Original_completed_continuation_crosses_nodes_once_for_its_owner` | 2 directions |
| DST-03; LIFE-002/004/006 | `Original_cookie_silently_crosses_nodes_without_reauthentication` | 2 directions |
| DST-04; LIFE-003/006 | `Exact_live_code_crosses_nodes_with_original_proof_and_single_use` | 2 directions |
| DST-05; LIFE-003/005/006 | `Concurrent_node_redemptions_have_one_native_winner_and_replay_revokes_issued_tokens` | 1 |
| DST-06; LIFE-005/006 | `Other_warm_node_enforces_committed_restrictions_at_authorization_and_exchange` | 8: account/session/client/scope, both directions |
| DST-07; LIFE-001/002/006/009 | `Surviving_node_uses_original_state_while_opposite_process_is_stopped` | 2 directions |
| DST-08; LIFE-006/008 | `Unsuitable_secondary_material_fails_safely_while_original_cookie_survives` | 2: wrong wrapper/missing credential |
| DST-09; LIFE-003/006/008 | `Healthy_but_incompatible_encryption_denies_then_exact_original_code_recovers` | 1 |
| LIFE-008/009 transport control | `Selected_socket_preserves_certificate_validation_and_canonical_authority` | 1 |

Initial source experiments: compilation exposed cancellation-parameter ordering, nonconstant Aspire state patterns, required static arrays and formatting violations; corrected without disabling analysis. The first actual PostgreSQL topology experiment passed both cookie directions (2/2, zero skipped, 45.340s). The first complete distribution experiment passed 23/23, zero skipped, 171.858s. These precede the final source-review corrections and do not accept them.

Independent architecture/security/test source findings tightened: per-request process-generation binding, Running/current-PID-before-fresh-health ordering, guarded credential-file moves and all-path restoration, invalidation after ambiguous concurrent client failure, authorization-denial snapshots including native authorizations and excluding only request-token bookkeeping, exact winner-only token inventory, current-launch diagnostic attribution, bounded concurrent readiness observation, and safe failure messages instead of formatting unexpected HTTP response objects. Corrected source and both-provider regression acceptance remain pending below.

The race rendezvous proves deliberately concurrent requests to distinct processes, not a forced native database interleaving. The loser must reach native ID2010. After both finish, persisted code status determines the explicit replay oracle: redeemed → ID2010; already revoked → ID2016, with more than ten seconds of original code validity. OpenIddict's `RevokeByAuthorizationIdAsync` updates the redeemed code as well as linked issued tokens; requiring ID2010 unconditionally after revocation would contradict the native library. The final linked access/ID token entries must be revoked; no claim of offline JWT invalidation follows.


Source-review checkpoint: architecture and test reviewers passed the corrected implementation; security follow-up passed after readiness-probe faults were made to invalidate the fixture, still attempt final lifecycle capture and preserve primary exceptions. The local reviewed suite passed 251 unit/harness cases and all 23 PostgreSQL distribution cases (zero failures/skips; distribution outer summary 180.895s). These executions precede the final expiry-at-response assertion and probe-fault cleanup correction. The corrected full solution builds with zero warnings/errors and passes whitespace plus IDE0005 checks at info severity.

CI run `38181285056` at `f0ce52453e6eb62abeaa3f59ec38d972c4152224` passed both distribution providers and both restart providers, but static analysis rejected an unused import. That import was removed; this run is not full-matrix acceptance. The subsequent exact-head required matrix must pass before T015 delivery. No provider requirement, diagnostic severity, coverage floor or test deadline was relaxed.


## Distribution source acceptance — run 108

Run [38181828768](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38181828768) passed **all 21 checks** at source `12ae1108097cdf90db3319007effd3cec70b6194`, tested merge `ff2ab4cfb1532ee640f21dff13e260138eac6216`. Both commits have tree `ddd54a4b683faec77925452a12cdeb348ae3ec32`. Artifact association comes from GitHub run/head metadata, not a SHA embedded in TRX. Downloaded ZIP digests matched GitHub SHA-256 metadata.

The retained 20 ordinary reports reconcile to **1,097 passes, zero failures/skips/incomplete cases**: 251 unit/architecture/harness, 300 standard per provider, 13 Restart plus 23 Distribution per provider, and 29 browser cases in each of six provider/browser cells. Strict lifecycle checks match discovered identities, definitions, entries, execution IDs and all counters; no run/fixture errors were present. Independent test and security reviewers accepted both corrected distribution artifacts. Architecture source review also passed.

Distribution TRX intervals were **190.146s PostgreSQL / 211.281s SQL Server**; Restart intervals were **326.702s / 354.503s**. Standard issuer TRX intervals were **392.538s / 583.755s**. These are report intervals, not whole-job timings or SLA guarantees; every invocation kept its 660-second outer deadline. No provider, case, coverage floor or timeout was relaxed.

All **13** required child-process coverage inputs were nonempty, including every provider/story and all six browser cells. Nine production-module floors (90% line / 80% branch) passed:

| Module | Lines | Branches |
| --- | --- | --- |
| Core | 100.0% | 100.0% |
| ServiceDefaults | 100.0% | 100.0% |
| AuthServer | 96.0% | 98.5% |
| Infrastructure | 99.1% | 100.0% |
| Admin.Api | 100.0% | 100.0% |
| Admin.Bff | 100.0% | 100.0% |
| Worker | 100.0% | 100.0% |
| Migrator | 98.2% | 100.0% |
| AppHost | 98.8% | 100.0% |

Separately, both providers passed seven mutation baselines, demonstrated seven behavioral kills and passed seven restored cases. Exact original/mutant source hashes and the tested merge identity match, and sources were restored. The focused WebKit experiment completed 60 observations with zero findings. These are separate assurance checks, not extra ordinary cases or formal accessibility acceptance.

All successful fixture runs completed mandatory capture/disposal/container/process-removal assertions; distribution TRXs have no standalone cleanup ledger or captured stdout/stderr. The final local inspection found no remaining test containers or executing IdentityPlatform applications. Source review establishes the exceptional probe-fault correction; the normal passing run does not claim to have deliberately injected every cleanup exception. The race proves concurrent attempts and native outcomes, not a forced internal database interleaving. Offline JWT invalidation, production load balancing/shared rate limits, throughput, backup recovery and certification are not established.

**Disposition:** LIFE-006 / US2 / SC-L03 is accepted at this corrected source checkpoint, together with its cross-node LIFE-002/003/005/008/009 obligations. All source findings are resolved. Copilot reported an exhausted requester quota and supplied no review; no GitHub approval is claimed. Final documentation-head checks, review threads, expected head and normal squash delivery remain the [PR #18](https://github.com/mosesparadise/OpenIdentityAdmin/pull/18) delivery gate. T016 begins only after that merge. LIFE-007 / US3 / SC-L04, full M01-E, T021 human assessment, M01-F, wider R1 and external/public-release obligations remain open.
