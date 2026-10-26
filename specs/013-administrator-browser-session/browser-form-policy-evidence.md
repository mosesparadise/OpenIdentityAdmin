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
