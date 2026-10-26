# B3 native form policy — rejected browser run and bounded reproduction

**26 October 2026.** The rejected run below remains retained. The user subsequently approved [A-010](../../docs/amendments/A-010-b3-native-form-policy.md); its scoped implementation and regression tests are now in [PR #97](https://github.com/mosesparadise/OpenIdentityAdmin/pull/97), pending corrected-source acceptance. T019 remains delivered and T020 incomplete.

## Exact rejected source and hosted result

| Binding | Value |
| --- | --- |
| Main/base | `dcb3cb36b8a013781a3608d5c51d559623edee73` |
| Branch | `099-b3-https-browser-logout` |
| Source | `9c6564937753326aff4d04f7cc9feb88503b504c` |
| Source/tested tree | `e02d9bb8d73552b00f48e3f751024e81098f7e20` |
| Tested merge | `43a466ec1e8f7f3a84e5117e87b6e5e65fd5a39a`, exact base/source parents |
| Run | [40250167955, attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40250167955) |
| Final jobs | 71 success, 5 failure (four new browser cells and final acceptance), 1 skipped coverage aggregation |
| Raw reports | 67 TRX; 4,389 executions: all prior 4,377 identities preserved and passing, 12 new failures, zero skipped tests |
| Mutations | 48 passing baseline controls, expected kills and restored controls in eight sanitized receipts; raw mutant TRX were absent from those ZIPs |
| Coverage | All 60 raw inputs retained; no merged report/floor acceptance because aggregation was skipped |

Each new cell has three failures and no passes: local logout/fresh sign-in, confirmed issuer-assisted logout, and abandoned issuer confirmation. Each times out while filling `#username` at `BrowserHttpsJourney.cs:46`, after the BFF Start sign-in button. Actual per-cell coverage shows `BrowserLoginService.StartAsync` returning BadRequest at its first form guard. `BrowserLoginRequest.FormAsync` reaches the initial predicate, but not body reading, parsed-form validation or antiforgery. Issuer `Browser/LoginModel.OnGet` is unexecuted.

The rejecting expression is `!BrowserRequestPolicy.Mutation(...) || IFormFeature != null`. Aggregate coverage cannot identify its exact failing operand. No captured hosted request header proves the original `Origin` value. Later database teardown messages do not establish the cause. These failures are not the known A-009 Firefox or WebKit certificate-observation defects and are not bypassed.

| Browser artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| PostgreSQL/WebKit | 12621310327 | `cce4cba6b1dbac1c9e223eaafc811d3918042fec67f51dca908a1eb196b8f013` |
| PostgreSQL/Chromium | 12620811314 | `c29953b401f568cb0a764944fe60bdbe0f7ce1efacc2aeb1560f4d3d80c82b39` |
| SQL Server/WebKit | 12620730898 | `c11c4179971d46921699da57c64074c4111ebe3fb83462cc4759a6dc66608a43` |
| SQL Server/Chromium | 12619634492 | `6fc8209ed8647d45cd28ebec6c67f25a220b02f4420a089aa845817387b49fe6` |

Independent raw review retained `/workspace/pr97-evidence/rejected-browser-receipt.json` SHA-256 `3bf3494755b529709a6b0ff4508bc8ddade97cee1252df4692a20826a44baa78`, `all-report-preservation.json` SHA-256 `34a15c31734527bb3c74e216025f72cea97abc3114ecceb44e1f1f01e2ecabbb`, and `all-browser-coverage-localization.json` SHA-256 `c82e005c93e666c77aa128a2443ef496259c4208fe69f3e3c05826d8c961d1e0`. Workspace paths are session-local; the run/artifact identities above are the remote evidence reference.

## Actual isolated browser controls

Commands actually executed: `python /tmp/b3-origin-probe.py` and `python /tmp/b3-form-policy-probe.py`. Both use Python's `HTTPServer`, the installed Python Playwright package and `/usr/bin/chromium` **151.0.7922.173**. They serve synthetic native forms on loopback HTTP, click their real submit buttons, and inspect only generated transport metadata. No database, account, authentication cookie, factor, token, real protocol selector or TLS bypass is involved. This is a browser semantics control, not trusted-HTTPS/provider acceptance. Architecture and test reviewers independently reran both controls and reproduced the outcomes. This installed system Chromium is not the pinned hosted Playwright browser; WebKit has not run this isolated control.

The first control serves a same-origin POST form under each response policy:

| Document policy | Actual native POST `Origin` | Actual `Referer` |
| --- | --- | --- |
| `no-referrer` | literal `null` | absent |
| `strict-origin` | exact loopback origin | origin plus `/` only |
| `same-origin` (comparison, not proposed) | exact loopback origin | full same-origin document path |

Each POST uses native `application/x-www-form-urlencoded` framing. Script SHA-256: `08845ee4e6ac56925006cfd641a644412493d82d5d6d0c1609d6250e2f5f31ba`.

The second control uses two loopback origins. The initial document sets `strict-origin`; its native POST receives a 302 to the second origin, with an explicit `no-referrer` response policy. With `form-action 'self'`, the destination is not reached. With `form-action 'self'` plus the exact second origin, it is reached. Both native POSTs have the exact source Origin and origin-only Referer; the permitted redirected GET has no Referer. Script SHA-256: `bf789eeba637a22f6519676037e4b92a0c22ad78dc1b00fb77284c8cbc5465ab`. This confirms the prospective CSP obstacle in the synthetic control; hosted tests have not reached issuer MFA or logout yet.

## Disposition and continuation

The contract requires both exact Origin and `no-referrer`; the native-form control demonstrates their incompatibility. Preserve exact-origin/antiforgery validation. A-010 proposes `strict-origin` only for actual B3 form documents, and only a trusted fixed BFF origin in the two issuer forms' CSP. Redirects, non-form results and inert errors retain `no-referrer`. At that rejected-source checkpoint no product correction had been made or acceptance claimed. The current HTTPS harness uses `localhost` on different ports: different origins, but the same host/site. It does not prove separate-host cookie isolation or cross-site SameSite behavior.

The subsequent A-010 approval authorizes the scoped implementation, permanent request/header/redirect regressions, exact-head provider/browser/full acceptance and normal squash after checks and review. PR97 remains draft/unmerged. T020's remaining negative-flow/theme/accessibility matrix, T012/T016 browser evidence and T021–T024 full B3 work remain open, along with all historical/human/release gates.

## Rejected first A-010 implementation run

The user-approved correction source `be46d3958d4b6b4797d0de3f12de8c81e268ff5c`, tree `2fed97ce8d5a248759af1f8ef94307ad5c364c51`, was tested as merge `25b5ab9c192cf4d63654d6652a1c4e624af67220` with exact base/source parents in [run 40255156414 attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40255156414). It is **rejected**: 70 successful jobs, five failed execution jobs plus failed acceptance, and skipped coverage aggregation. Actual raw evidence is 65 TRX, 4,384 executions, 4,378 passed, six failed and zero skipped tests. Eighteen SQL Foundation/BffSessions cases were not executed after the earlier Persistence failure, so the expected 4,402 total was not satisfied. All 60 raw coverage inputs were inspected but no merged-floor acceptance exists. All 48 baseline/kill/restored mutation receipts pass; contracts 43/7/7 and Node 37 pass. Unit/BrowserSessions server reports independently pass 2,224 cases, including all 1,614 unit identities and all 305 server cases/provider.

| Required failure | Actual evidence and limit |
| --- | --- |
| SQL Persistence native client state creation | `NativeOwnershipTests.Given_NativeClientState_When_CreationAndQuotaBoundariesAreExercised_Then_OnlyVerifiedBoundedStateCommits`, 28.231 seconds. The first positive generation reaches `ProtocolStorageRequest.RequireCurrent:37`: its original ten-second request budget has elapsed before entering the durable child. It is not external-token cancellation, a shared request across iterations, or a quota/race result. What consumed the time was not measured. ZIP 12622376423 SHA-256 `0de12273a7ab71a045b72d3ac3e8a6f8e1102bd2449d3d72f3b9ceeae38f72be`. |
| PostgreSQL governance consistency setup | `ConsistencyTests.Given_SerializedCurrentAuthority_When_AnotherWriterContendsBetweenReads_Then_NoMixedSnapshotEscapes`, `before-claim-read`, 28.232 seconds. Finite stages are JourneyLease → HostBuilder → HostBuild → HostStart → AuthReadiness → HostCleanup. No HostReady, lease or governance assertion was reached. Failure could occur in health waiting, lifecycle capture, connection retrieval or parameter lookup; the sanitized original exception does not identify which. The three-minute startup limit is not shown to have expired. ZIP 12622536216 SHA-256 `440309e3eacbc3b3308e5e54f7b8f87e494770a834bce81a042011bc379b2189`. |
| PostgreSQL/Chromium local logout/fresh sign-in | Two of three browser cases pass. The failing case times out at the second post-MFA `WaitForURLAsync`, after its first genuine login, committed local logout, issuer-separate checks and authentic old-cookie denial. ZIP 12622950218 SHA-256 `a77c15d06984683db7af0eaa769585296818004ddb1f80728b5ffc632b47ac39`. |
| SQL Server/WebKit confirmed issuer logout | Two of three browser cases pass. The confirmed case times out at the post-confirmation `WaitForURLAsync`. Actual unique-path coverage reaches native issuer revocation/sign-out/302 formatting and BFF return consumption/result publication, with the relevant denial/catch branches unexecuted. Thus this is not evidence that CSP or the Origin guard blocked that request. It still does not establish the final browser URL/document state. ZIP 12622606663 SHA-256 `d614a2a1cbc7e6b197876894ad69bd1704f10bea94e0a83e532fc6f14574c053`. |
| PostgreSQL/WebKit initial sign-in | Local logout/fresh sign-in passes; both issuer-choice journeys time out at their initial post-MFA URL wait. Coverage is aggregated, so it cannot assign every successful callback branch to each failed attempt. ZIP 12622074459; exact digest is retained in the independent receipt. |

SQL Server/Chromium passes all three new journeys: ZIP 12622531811 SHA-256 `1bdff44309c69702a8d7ff57205d1e8ce86930008935c863f8a5df62bf34ecef`. Overall new-browser evidence is **8 passed / 4 failed**, not complete acceptance. Independent final receipt `/workspace/pr97-a010-evidence/final-receipt.json` SHA-256 `e003aa1689ba4dafba1a35a20f8d2add23fa823f024651d8ecad07add43190fe` retains every artifact binding and the coverage-localization limits. No infrastructure-load or Playwright-defect cause is inferred from these failures.

## Bounded verification corrections after that rejection

The native client fixture now finalizes its actual OpenIddict client options and ephemeral credentials during owned fixture setup. Previously first native factory resolution could initialize options and prepare two 4096-bit ephemeral RSA keys inside the first measured request. This is a supported initialization concern, not proven historical timing. No state-generation request is primed or retried, and each real operation keeps its fresh original ten-second budget and the unchanged outer test deadline. Finite preparation-phase/elapsed observations locate factory completion and child entry if failure recurs.

The bootstrap harness now reuses the already tested `ProfileStartupEvidence` observer before cleanup, retaining only its six fixed resource roles, allowed states, exit codes and PIDs. Three finite checkpoints distinguish healthy-resource completion, lifecycle capture and connection retrieval. A closed exception category and cancellation Boolean never retain exception messages, inner exceptions or connection values. Existing seeded diagnostic tests cover the new labels, hostile exception messages and failing sinks. No startup retry, readiness relaxation, lifecycle relaxation or deadline increase is introduced; the original startup cause remains unresolved.

Browser completion now awaits the required visible BFF sign-in heading or unique unverified-logout result, then privately checks the exact fixed URL/path and preserves all real status, state, audit, cookie, source-revocation and replay assertions. These assertions target the committed product document rather than requiring a separate browser load-event condition. Browser defaults and all native deadlines remain unchanged; no timeout extension, retry, header override or manufactured navigation is used. The exact earlier wait-timeout causes remain unproven. After joined host shutdown, the fixture retains only finite request/policy counters and a route-index/status encoding, including on failure; no raw URL/header/HTML or credential is logged.

These corrections require a new exact-source full workflow and raw evidence review. They do not convert any rejected result to a pass or close T020's remaining scope.

Local verification of this follow-up: the complete solution builds with zero warnings/errors (41.55 seconds); all 226 Python regressions pass (9.394 seconds); actual compiled startup-label/canary controls pass 8/8, and existing resource-observer controls pass 8/8. Actual native Administrator discovery remains 1,857, and all six browser-story filters reconcile at 8/5/3 per provider. Formatting, explicit-base code quality and diff checks pass. Architecture, security and test source review support the bounded changes while retaining the causal limits above. These local controls do not replace required hosted provider/browser execution.
