# T012–T014 product journeys, session status and browser contract

Status: T013/T014 and the server/provider portion of T012 accepted internally through PR #87 squash `c1d699bb0f23c1cf1b7011fc471117303e266392`. T012 browser/TLS acceptance and production activation remain open. Source checkpoints below retain development evidence and failures; final acceptance is recorded at the end.

25 October 2026, Europe/London. The user authorized the next three items after accepted T011: T012, T013 and T014, with their necessary tests and dependencies. Base is `685efc7e3197180c87a406f00eaf09f0f6f76e4d`. This source checkpoint is not hosted acceptance or production activation.

## Implemented boundary

T012 extends the existing real-provider product fixture with staged challenge/authorization helpers. New cases cover two complete logins with genuine existing BFF/issuer cookies, different-account native authority in the same issuer database/key ring, a genuine password-only pilot cookie, actual restricted recovery, expired PAR and native state, and supported audited permission loss before exchange. The pilot's initial bounded interaction reference is explicitly fixture-owned; actual password verification, session persistence and cookie issuance run through production components. This is not additional full pilot OAuth evidence. Existing B2 and T011 cases remain unchanged.

T013 adds `GET /bff/session`: a detached joined read of browser context and ticket, authenticated protected B1 envelope and exact generation association, then original lifetime checks after scope disposal. It returns only authenticated state and, when valid, subject and effective local expiry. Reads never renew, write audit/state or set cookies. Persistence/cleanup failure returns sanitized 503; invalid authority returns unauthenticated 200. Strict framing rejects bearer headers, bodies, queries and unsupported methods. The fixed `/bff/login/error` page is inert, query-free, no-store and contains only an explicit return-to-sign-in action. Existing host-only cookie and native antiforgery rotation mechanisms are reused. Production composition stays dormant.

T014 supplies native `bff-v1` metadata using the actual status handler/DTO. `/bff/session` is version-neutral as specified; the document remains version 1. Protocol callbacks/resume and HTML routes are excluded. `generated/bff-v1.browser.json` and its TypeScript declarations represent the explicitly composed dormant browser surface. The two production exports remain empty, and the synthetic management fixtures remain separate. The new narrowly classified status operation does not weaken management permission, version, concurrency or antiforgery checks. No React, API proxy or documentation viewer activation is included.

## Traceability and inventory

| Requirement | New evidence families |
| --- | --- |
| FR-001–005/013, B3-AUTH-01–04 | BrowserProductJourneyTests, BrowserProductSubstitutionTests, BrowserProductPilotTests, BrowserProductRecoveryTests |
| FR-006–008/011, B3-WEB-01–04 | BrowserSessionStatusTests, BrowserSessionBindingTests, BrowserSessionFailureTests, BrowserStatusFramingTests |
| FR-006/007/013/015 | BrowserSessionDocumentTests, generated dormant browser contract, three Node validation controls |

Actual native discovery preserves all 1,645 prior Administrator identities and portable metadata, adds 36 cases (18/provider), and totals 1,681. BrowserSessions requires 220/provider; ordinary Administrator totals 775 PostgreSQL and 880 SQL Server. Host inventory is 1,440; Contracts core 43 and Node 37. No coverage input/floor, mutation target, deadline or existing provider identity is removed.

## Local development evidence and retained failures

- Pinned SDK 10.0.401 installed on the temporary volume after the workspace filesystem filled during the first SDK copy. Existing worktrees/evidence were preserved; the new registered worktree moved to `/tmp/b3-session-use`.
- Locked restore and Administrator/Host/Contracts builds pass with zero warnings/errors at their recorded development checkpoints.
- Contracts core 43/43 and Node 37/37 pass; deterministic JSON/TypeScript regeneration and TypeScript compilation pass. Actual production contract export remains repeatable, empty and has zero network effects. Selection-tool regressions 53/53 pass.
- First contract fixture execution exposed missing service annotation, then missing API-version-set metadata, then missing `WithDocumentPerVersion` mapping. Corrected native generation passes; no fallback handwritten schema was substituted.
- First focused Host run passed 18/19; the encoded callback-alias case failed earlier in native canonicalization than its expected composition boundary. Separate permanent assertions now identify those two rejection boundaries. Corrected full Host execution passes 1,439/1,439 with zero failures/skips.
- The focused PostgreSQL status attempt failed in Aspire startup before reaching test assertions (`JourneyLease → HostBuilder → HostBuild → HostStart → HostCleanup`). The run was cancelled; it supplies no provider acceptance. No local SQL diagnostic experiment was run. Hosted provider execution is required.
- Architecture/API and security read-only reviews find no confirmed status authentication, renewal or disclosure defect. Test review required stronger recovery identity assertions, stale/cross-paired/duplicate-cookie cases, independent foreign-account authority proof and explicit pilot fixture scope; these were incorporated. Final exact-source reviews and hosted evidence remain pending. Syntax-aware code-quality controls pass without allowance changes; contract baseline compatibility passes for all five documents.

First hosted source `a631803d479fb8a6eb29a451419b836d1a71b2e9`, run [40208882824](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40208882824), passes both BrowserSessions lanes, unit, Contracts core and static analysis. This is supporting development evidence; final acceptance belongs to the subsequent exact source. Final source review also adds an actual pilot completion-page request using the issued cookie as a positive control before B3 denial, and gives the dormant contract a product-owned tag instead of the fixture assembly name. The strengthened pilot fixture builds, native contract regeneration/focused execution and TypeScript compilation pass, and independent test/API review accepts those changes. All 219 Python regressions pass. The final focused Host run passes 20/20, including the additional inert safe-error-page control; current Host inventory is 1,440. No earlier failed result is replaced by these passes.

## Remaining acceptance

### Retained deadline failure and bounded execution partition

Run [40209842008](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40209842008), source `9c8adeaa70264526048d4e152c394c5c88610b7b`, tested merge `75ce96bc938a2e052223082a901a1bc15e4b8331`, is rejected development evidence: 66 successful jobs, failed SQL BrowserSessions and aggregate acceptance, skipped coverage. SQL job `121218665074` hit the unchanged 660-second child-process deadline. Artifact `12605372105` (216,123 bytes, SHA-256 `ae57d8430d9d09e0f55395e6133dc4c55d6da9c5c1baa1ab0dc69d92485f0960`) contains an unfinished TRX stream and partial coverage, no completed report. Neither an assertion failure nor the timeout's cause is established. The first source run was automatically cancelled for its unfinished jobs when the reviewed correction was pushed; its successful provider jobs are supporting evidence only.

Independent review of the rejected run's successful artifacts verifies PostgreSQL 220/220, unit 1,535/1,535 (Host 1,440), Contracts core 43/43 and Node 37/37, preserving every prior identity. Eight actual mutation ZIPs verify 48 kills with passing original/restored controls and unchanged source manifests. These do not substitute for the missing SQL report or aggregate coverage. Raw mutant TRX files are not retained by the workflow; the inspected mutation artifacts contain sanitized receipts.

The bounded correction partitions ordinary BrowserSessions into explicit **Foundations 158** and **Product 62** cases per provider, without changing any test identity, metadata or body. The completed PostgreSQL TRX sums individual durations to 278.74 and 270.50 seconds respectively, supporting this workload split without explaining the SQL termination. All 59 classes are explicitly enumerated. Both actual native filters are validated before either group executes: full Administrator discovery first, then exact group identities, counts, disjointness and union. Ungrouped/default execution expands both groups; incompatible CLI arguments fail before execution. Each of the four hosted cells retains the existing 20-minute job and 660-second process deadlines.

The two former BrowserSessions coverage inputs are replaced by four mandatory distinct group inputs, increasing the required total from 52 to 54 while leaving all 15 module floors and every other input unchanged. Seven new seeded selection controls plus the expanded missing/empty-coverage controls pass; the full Python suite passes 226/226. Fresh native discovery independently verifies all 1,681 identities and both 158/62 unions on each provider. Architecture and test reviews cover the selection correction; new exact-source hosted acceptance remains required. No retry, sleep, timeout increase, test omission or browser deferral is introduced by this partition.

T012 remains open for actual trusted-TLS Chromium/WebKit product journeys and deferred Firefox. The provider HTTP tests do not establish browser-engine/theme/accessibility acceptance. A-009 covers only its named deferrals, not a blanket browser/TLS waiver. T013/T014 source delivery requires the full exact-head workflow, independent source/evidence review, unchanged coverage floors and mutation verification before normal squash.

All six historical findings, 48 human rows, 345 not-assessed controls, production/public-release gates, PR #69/issue #70, A-009/T028 restoration and the viewer advisory remain open. T015 onward, logout, refresh, current API authorization and React remain separate roadmap work. No expired diagnostic approval is revived.

## Accepted internal delivery — 25 October 2026

[PR #87](https://github.com/mosesparadise/OpenIdentityAdmin/pull/87) was normally squash-merged as `c1d699bb0f23c1cf1b7011fc471117303e266392` after all 71 exact-head checks and independent architecture/API, test, security, mutation and coverage reviews passed. Exact head, base, mergeability and review state were checked; no restriction was bypassed. Remote main and the actual squash tree match the reviewed and tested source.

| Boundary | Verified identity |
| --- | --- |
| Reviewed source | `6d44e7438ab04bd6de18cbceb5464b53539ecfb9` |
| Base | `685efc7e3197180c87a406f00eaf09f0f6f76e4d` |
| Tested merge | `e660090b945e542b52e27dd7623b13056b3e893d` |
| Source, tested merge and squash tree | `308ab39969d45ab717d4550822c5adeadd1d5e95` |
| Actual squash | `c1d699bb0f23c1cf1b7011fc471117303e266392` |
| Workflow / attempt | [40211611241](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40211611241) / 1 |
| Coverage / acceptance jobs | `121228732006` / `121228869336` |

Independent raw inspection of six corrected ZIP artifacts verifies nine TRX reports with **2,018 passing native executions** plus **37 passing Node cases**, with zero failures, skips, errors or aborts. Native totals are BrowserSessions 440, unit 1,535 (Host 1,440, Core 25, Architecture 5, AppHost 65), and Contracts core 43. Every raw result reconciles to its definition, class/method, execution, entry and list identity. Both providers' 158 Foundations and 62 Product cases form exact disjoint unions of 220; all 202 previously accepted BrowserSessions cases per provider remain, with 18 additions. Host retains all 1,427 prior cases plus 13; Node retains all 34 prior cases plus three. All 1,645 base Administrator identities and complete portable metadata remain, with 36 additions producing 1,681. Actual checkout logs bind the four provider groups, unit and contract execution to the tested merge.

All five generated JSON contracts match final source. Native production export receipts remain repeatable and empty, with zero socket effects; three socket allocations per export are recorded separately. Hosted deterministic generation, TypeScript compilation, baseline comparison and exact contract identities pass. This does not enable production documentation or infer a network effect from allocation alone.

All **54 mandatory coverage inputs** match the final run/head and the hosted download/merge manifest's artifact IDs, sizes and SHA-256 values. The nonempty-input guard and 54 unique matching merge paths pass. Independent raw merged-Cobertura recomputation passes all **15 unchanged 90% line / 80% branch floors**. Admin.Bff measures **2,618/2,790 lines (93.8351%)** and **584/594 quantified branches (98.3165%)**. Merged XML SHA-256 is `fa7e4c80c3d887f99f88626f05f5cee3396ee3289ab11ce407e4c26956b1a7a4`. All eight actual mutation ZIPs independently verify their API hashes/sizes, exact source and suite/provider identities, unchanged manifests/verifier, reconstructed original/mutant source hashes, **48 kills, 48 passing baseline controls and 48 passing restored controls**, zero skips.

Review boundaries remain explicit: the nine native TRX reports, Node XML, contract exports/receipts, merged coverage XML and eight sanitized mutation receipt bundles were inspected. Individual coverage-input XML files and raw mutant TRX files were not independently unpacked; input nonemptiness is hosted evidence, and mutation ZIPs contain sanitized receipts only. Unquantified branch markers are not additional measured branches. The earlier SQL deadline failure remains unexplained and rejected; successful partitioned execution is accepted for this source without making a historical causal claim. No deadline, module floor, test assertion or browser deferral changed.

| Retained final artifact | ID | SHA-256 |
| --- | --- | --- |
| administrator-mutations-PostgreSql | `12606251868` | `6ecebf745a3efa75c0361980d01363bc0520fbbfc0a8df1c12ecac4ee0ae291e` |
| administrator-mutations-SqlServer | `12605663175` | `4700f19d8b699fe0c0cac3d06fc2cc0ec042ccbdade8e0a5d315df5e202f323c` |
| administrator-protocol-mutations-PostgreSql | `12605618684` | `3d4523d65956ff2d028df292d5e8d1ea9ca77993e894c98441f775a8d268910e` |
| administrator-protocol-mutations-SqlServer | `12605767432` | `202e9841b220eed652f9ec9415d6898eca19496fee51c32b5449b36378db71d5` |
| critical-mutations-PostgreSql | `12606226827` | `8e30b66fb56227487d0b000985a8cf54370dcab456cba609c46b747120a5f722` |
| critical-mutations-SqlServer | `12606441054` | `3a4e3207b8361f7a7fee901ad317c27b32833b78d87fc00ece7eab8bced728bd` |
| foundation-administrator-PostgreSql-browsersessions-foundations | `12606440378` | `9edb7479d6b576e0fd1a180500b6b838415e760b220a170457bb863c9ececaf3` |
| foundation-administrator-PostgreSql-browsersessions-product | `12605324783` | `d7f652667a9dd9bdcecf9aefd81972979e7cff9c61134606c3cc0b34728c7310` |
| foundation-administrator-SqlServer-browsersessions-foundations | `12606007060` | `4da9e63bef3fed0f86735d50d4604e29e13d0954121bbb615b545e42425e6cbb` |
| foundation-administrator-SqlServer-browsersessions-product | `12605543169` | `a692adaa02c3dd695207dd4e8fecc91fcf36f739ce51a2577fd5203f98afea13` |
| foundation-contracts-core | `12605514331` | `0fbcbf4155596059a60c859b1417a0e80ba3f33c2c2def03fc798c39206b87ab` |
| foundation-coverage | `12605948605` | `2b6caaa83208b323d74394ca42492371b6186a0840d01db4de523bc724f7d844` |
| foundation-unit | `12605558396` | `286a2cc6539c9507308f0afeb87bb5bcf2d91963c0c4030b7c244e93f5e218dc` |
| governance-mutations-PostgreSql | `12605822435` | `e4aef27d3bfd6c1a81d551b908349e53da59550cdbd54d2ee0aa92891a387886` |
| governance-mutations-SqlServer | `12606332081` | `05345e8cbd16fd90f8b14aa443891edd4fca17ae863a3efd9af964b4a5d9031f` |

**T013 and T014 are complete for internal development.** T012's server/provider product-journey controls are accepted, but T012 remains unchecked for actual trusted-TLS Chromium/WebKit product journeys and deferred Firefox evidence. In-process HTTP over real stores does not establish browser engines, theme or accessibility acceptance. A-009 is not a blanket TLS/browser waiver. Production composition and full B3 remain pending.

Next is T015's remaining lifecycle/failure verification; its callback-versus-logout cases require T017's protected local logout implementation. Reuse the accepted callback/replacement and rollback controls rather than repeating them. T016 browser evidence and T018/T019 native issuer-assisted logout remain open. No M02-C refresh, M02-D current API authority, React or product activation is complete.

All six historical findings, 48 human rows, 345 not-assessed controls, production/public-release gates, A-009/T028 browser restoration, PR #69/issue #70 and the viewer advisory remain open. No expired diagnostic approval is revived.
