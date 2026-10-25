# T012–T014 product journeys, session status and browser contract

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

T012 remains open for actual trusted-TLS Chromium/WebKit product journeys and deferred Firefox. The provider HTTP tests do not establish browser-engine/theme/accessibility acceptance. A-009 covers only its named deferrals, not a blanket browser/TLS waiver. T013/T014 source delivery requires the full exact-head workflow, independent source/evidence review, unchanged coverage floors and mutation verification before normal squash.

All six historical findings, 48 human rows, 345 not-assessed controls, production/public-release gates, PR #69/issue #70, A-009/T028 restoration and the viewer advisory remain open. T015 onward, logout, refresh, current API authorization and React remain separate roadmap work. No expired diagnostic approval is revived.
