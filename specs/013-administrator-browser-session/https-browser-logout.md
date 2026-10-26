# T020 real HTTPS browser logout — initial implementation slice

**Historical implementation candidate — 26 October 2026; superseded by accepted delivery below.** Base `dcb3cb36b8a013781a3608d5c51d559623edee73`; branch `099-b3-https-browser-logout`. Continues delivered T019 without repeating persistence, registration or logout implementation. T020 remains unchecked: this slice establishes three real-browser journeys, not the full adverse-flow/theme/accessibility matrix. Hosted provider/browser acceptance is pending.

## Boundary and design

FR-001–010/012–015; B3-AUTH-01, B3-END-01–03 and SC-001/002/004/005. The test-only `BrowserHttpsFixture` reserves two managed loopback sockets before constructing immutable issuer/client/callback identities, credentials, account profiles and the exact fixed registration. Kestrel receives each already-bound socket once through the supported `SocketTransportOptions.CreateBoundListenSocket` factory; there is no release/rebind port race or raw descriptor ownership. The existing development certificate and normal TLS validation apply to both browser navigation and native BFF backchannel requests.

The BFF uses its explicit generic-host `UseBrowserLogin` composition with Sessions-only runtime credentials. No `NativeClientIssuerBridge`, fabricated authentication, replaced native HTTP handler, ignored certificate failure or production Program change is introduced. The issuer reuses native account services, real Razor pages, ownership middleware and `MapSharedProtocol` authorization/token decisions; discovery/JWKS/PAR/end-session remain native-handler-owned. Existing TestServer factories retain their original mode.

Fixture setup performs genuine enrollment and the delivered audited B3 registration upgrade. It never imports the setup scenario's cookie into the positive browser. Each sign-in uses actual password/MFA forms, browser-generated origin/antiforgery, fresh native PAR/code/callback and the real committed local ticket. Independent real-store reads inspect local revocation, browser generation, continuation state and exact issuer source revocation/audit. Browser return remains unverified issuer status.

The BFF listener stops and joins requests while the issuer remains available; the issuer then stops/joins before dependent keys and database leases are released. Review found that the shared issuer startup catch bypassed its new HTTPS join path and could skip credential disposal after cleanup failure. Initialization now guards credential ownership throughout configuration and reuses the same host-owner disposal path after construction, preserving original and cleanup exceptions. The narrow architecture correction passed source review.

## Actual scenario inventory

`Features.Browser.SessionLogoutBrowserTests` has three provider theories, each carrying `Category=Browser`, `BrowserStory=BrowserSessions` and an exact Provider trait:

| Scenario | Independent observations |
| --- | --- |
| `Given_RealHttpsSession_When_LocalLogoutAndFreshSignIn_Then_OldCookiesDenyAndIssuerAuthorityIsSeparate` | Actual local form/commit; prior authentic cookies denied in another real browser; issuer source remains live and its cookie is retained; another explicit sign-in still traverses password/MFA and creates a different source; exact two PAR/two token requests and zero issuer end-session requests. |
| `Given_RealHttpsSession_When_IssuerConfirmationCompletes_Then_BothCommitsAreObservedAndReturnRemainsUnverified` | Local revocation and `RemotePending` before navigation; issuer GET inert; actual protected confirmation POST revokes only the intended source with audit; fixed state-only return records `ReturnedUnverified`, deletes correlation/issuer cookies and cannot authenticate; replay without deleted proof denies. Exact one PAR/token and one end-session GET/POST. |
| `Given_RealHttpsSession_When_IssuerConfirmationIsAbandoned_Then_LocalRevocationPersistsAndIssuerRemainsLive` | Genuine assisted local commit and issuer GET, then navigation away; local status remains unauthenticated, continuation remains pending and issuer source remains live. Exact one PAR/token and one end-session GET, zero POST. |

Cookie assertions inspect actual Secure/HttpOnly/path/SameSite flags, session nonpersistence, issuer retention/deletion and logout-correlation presence/deletion. In-memory storage checks keep the captured local handle/generation proof out of script-readable state. No raw callback URL, cookie, token, HTML, screenshot, HAR or trace is retained as browser failure diagnostics; the existing finite-category/source-line wrapper covers the new journeys. These bounded checks do not claim complete SC-002 network/storage inspection.

Review strengthened actual end-session request counters, cookie effects and audit correlation/generation checks. An initial added assertion incorrectly expected subject/source on remote transition audits. Source inspection confirmed `BrowserLogoutService.TransitionAsync` intentionally records those audits without actor fields; the corrected oracle requires null remote actor fields, exact correlation/generation and the original subject/source on the durable continuation and authenticated local logout audit. Production audit behavior is unchanged.

## Discovery, local verification and required CI

Actual native discovery contains **1,857** Administrator identities, preserving every prior **1,851** name and portable type/trait record and adding six. Browser inventory is **16/provider**: Accounts 8, Security 5, BrowserSessions 3. All ordinary provider counts, 305 BrowserSessions server cases/provider, 1,601 unit cases, mutation selectors and 15 coverage floors remain unchanged.

The new explicit browser story requires PostgreSQL/SQL Server × Chromium/WebKit: four additional jobs and twelve browser executions. Required actual coverage inputs increase from 56 to **60**; no existing input is removed. Selection remains exhaustive and fail-closed for missing, duplicated, unknown, substituted or miscategorized identities and missing/empty coverage. Existing child/browser/job deadlines are unchanged. A-009's two exact WebKit certificate cases and Firefox deferral are unchanged; neither is counted as a pass.

Local SDK remains 10.0.401. Initial compile feedback corrected an incorrect test enum member, formatting and analyzer-incompatible assertions; no suppression or baseline growth was used. The final Administrator build passes with zero warnings/errors (21.93 seconds). Actual formatter execution and explicit-base code-quality checks pass without allowance growth; instruction/assurance integrity and diff checks pass. An initial local reconciliation command passed the full versioned metadata document to the provider-name selector and correctly failed; it supplies no acceptance evidence. The corrected invocation matches the existing verifier API by supplying its two-provider name map. All six actual provider/browser-story discovery filters then reconcile: 8 Accounts, 5 Security and 3 BrowserSessions per provider. All **226 Python tooling regressions** pass (9.451 seconds), including new-story selection and all 60 coverage obligations.

A temporary standalone control compiled the actual `BrowserHttpsEndpoint` source and started two real Kestrel listeners with the retained sockets. Both actual HTTPS requests passed normal certificate validation using the exported public development certificate. A gated in-flight request prevented shutdown completion until released; stop then joined successfully. This is a local socket/TLS/ownership control, not a provider/browser acceptance result, and is not added to native test counts. No local SQL/Aspire retry or diagnostic image acquisition ran.

Required exact-source hosted execution, raw reports, coverage, mutation and independent evidence reconciliation remain pending. Source security review found no authentication shortcut or TLS bypass; architecture and test reviews required the concrete corrections recorded above. Source review is not runtime acceptance.

## Remaining work and limits

Both identities use `localhost` on different ports. They are different origins but the same cookie host/site; browsers share host-only cookies across ports. This topology proves actual TLS/native/browser transport and explicit origin behavior, not separate-host isolation or cross-site SameSite behavior. Those claims need correctly trusted distinct hostnames and dedicated tests.

T020 remains open for its remaining cross-site/framing/mismatch/expired-correlation/absent-session/failure/lost-return matrix and theme/accessibility work. T012/T016 remaining browser evidence, T003/T004 full integration/inventory, T021 dedicated B3 mutations, T022–T024 coherent B3 acceptance and A-009/T028 restoration stay open. All six historical findings, 48 human rows, 345 not-assessed controls, PR #69/issue #70 and production/public-release gates remain open. M02-C/D/E refresh/API/React acceptance is absent. No expired diagnostic authority is revived and no production composition is enabled.

## Historical approved A-010 correction candidate

The initial source/run and all twelve failed new browser executions are retained in [browser form policy evidence](browser-form-policy-evidence.md). The user approved [A-010](../../docs/amendments/A-010-b3-native-form-policy.md) on 26 October 2026. Its correction applies strict-origin only at actual B3 form rendering, including valid password/MFA retry forms. Shared defaults, redirects, status, returns and inert errors retain no-referrer. MFA CSP reuses the existing script nonce and validated fixed BFF authority; issuer logout confirmation receives the return URI from the observed fixed registration policy. No request-supplied origin or return destination controls CSP. Exact-Origin, native antiforgery, password/MFA and durable authority checks are unchanged.

Permanent controls add six Host response-policy cases, five actual account-Origin framing cases and two issuer-logout Origin cases. Existing provider journeys now assert final form, retry, redirect and denial headers, the real rendered script nonce and exact CSP. The actual HTTPS listeners retain only scalar native POST Origin/Referer observations and protocol-return/redirect policy observations; each browser journey requires exact operation counts and zero violations. No raw headers, URLs, page content, nonces or credential material are retained by these observations. Corrected-source hosted acceptance is still required; the initial browser failures are not deferred under A-009.

Source review also required two concrete corrections: the actual account guard now restores default headers after a BadHttpRequestException during form rendering, covered alongside generic failure by actual guard controls; final HTTPS response policy is observed through the outer OnStarting callback, with exact successful-form and redirect counts to prevent a missing observer passing. Duplicate CSP headers fail on scalar cardinality before any value is extracted for assertions. Actual Host discovery increases from 1,506 to 1,519 (13 additions); unit total becomes 1,614. Administrator identities remain 1,857; expected complete ordinary TRX executions become 4,402, with 60 required coverage inputs and unchanged floors.

The first approved-source full run was rejected despite 8/12 new browser successes and 2,224/2,224 scoped unit/server passes. [The retained failures and bounded verification corrections](browser-form-policy-evidence.md#rejected-first-a-010-implementation-run) record native client setup preparation, startup observations and visible-document completion assertions. These do not waive any failed case or establish an unmeasured historical cause. The corrected exact source requires its own full acceptance.

## Accepted internal delivery — 26 October 2026

[PR #97](https://github.com/mosesparadise/OpenIdentityAdmin/pull/97) was normally squash-merged after the user-approved A-010 correction, all required checks and independent architecture/security/test/raw-evidence review. This accepts the initial three HTTPS journeys and scoped native form correction; it does not complete T020 or B3.

| Binding | Value |
| --- | --- |
| Base/main before merge | `dcb3cb36b8a013781a3608d5c51d559623edee73` |
| Accepted source | `45c2ce6ea0b9d02e962dd7037db300d0efe4b50f` |
| Tested merge, exact base/source parents | `2a5250da8ae4e282554ed1e6ab31c8f312f52b17` |
| Source/tested/squash tree | `e444de93e7f1e93aca447172d32eebae25f176fe` |
| PR #97 squash | `b3ad00a6d61e19ccbcbfecc3326d3f6083aaa002` |
| Accepted run | [40257327073, attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40257327073), all 77 jobs successful |
| Coverage / acceptance jobs | `121368006060` / `121368139561`, successful |
| Independent final receipt SHA-256 | `53d54216aff4912dc34981aa26dedda450e3cf96125dbc841030887a81f6575d` |
| Raw coverage/contract review SHA-256 | `eb263124ea8c9df9360243572c7823dacf7033ea930181600e4d496c11b68464` |

Root rechecked the exact head, all 77 successful check-runs, unchanged main, empty GitHub review submissions and clean mergeability before the normal squash. Independent agent reviews are source/evidence reviews, not human GitHub approvals. Fetch after merge verified the actual squash parent and identical tested tree. Workspace receipts are session-local; the run and artifact identities below bind remotely retained evidence.

### Actual tests and policy observations

All **67 actual ordinary TRX reports** were inspected: **4,402/4,402 passed**, zero failed or skipped. Every prior 4,377 test identity/multiplicity is preserved; additions are 13 Host controls and twelve real browser executions. Host passes 1,519 and total unit passes 1,614. BrowserSessions server groups pass 305/provider (158 Foundations, 96 Product, 51 Lifecycle). Scoped unit/server totals are 2,224. Actual Administrator discovery preserves all 1,851 prior portable identities and adds six, total 1,857; browser inventory is Accounts 8, Security 5 and BrowserSessions 3 per provider. Current ordinary profiles total 4,163; 184 browser executions minus two exact A-009 deferrals plus 57 contract executions reconcile to 4,402. The two unrun WebKit certificate cases and Firefox are deferred, not passed. Separate Node controls pass 37/37 and are not added to TRX totals; contracts pass core 43, Chromium 7 and WebKit 7.

Each of the three genuine password/MFA/native callback/logout journeys passes on PostgreSQL and SQL Server in Chromium and WebKit: **12/12**. Actual raw output contains **24** issuer/BFF policy records with exact operation counts and zero Origin, form-referrer, return-referrer or response-policy violations. The observed count tuples `(POSTs, returns, forms, redirects)` are:

| Journey | BFF | Issuer |
| --- | --- | --- |
| Local logout and fresh sign-in | `(3, 4, 5, 6)` | `(4, 2, 4, 6)` |
| Confirmed issuer logout | `(2, 4, 3, 3)` | `(3, 1, 3, 4)` |
| Abandoned issuer confirmation | `(2, 2, 3, 3)` | `(2, 1, 3, 3)` |

Those finite records accompany independent committed-store/audit, exact-source revocation, cookie, stale-session and replay assertions. They prove current browser behavior without logging raw headers, URLs, form contents, cookies, factors or tokens. They do not retrospectively prove the exact cause of the original rejected run.

All **60 actual raw coverage inputs** were directly parsed and hashed from 61 foundation ZIPs. Independently recomputed coverage passes all **15 unchanged 90% line / 80% branch floors**. No deadlines, exclusions, floors or allowances were relaxed. Modules with no branch sites retain the established no-branch policy.

| Module | Covered / valid lines | Line % | Covered / valid branches | Branch % |
| --- | --- | --- | --- | --- |
| `IdentityPlatform.Core` | 91/92 | 98.9130 | 11/12 | 91.6667 |
| `IdentityPlatform.Infrastructure` | 6546/7013 | 93.3409 | 579/598 | 96.8227 |
| `IdentityPlatform.ServiceDefaults` | 61/61 | 100.0000 | 18/18 | 100.0000 |
| `IdentityPlatform.Migrator` | 608/616 | 98.7013 | 98/98 | 100.0000 |
| `IdentityPlatform.AuthServer` | 4743/5007 | 94.7274 | 666/674 | 98.8131 |
| `IdentityPlatform.Admin.Api` | 71/71 | 100.0000 | 10/10 | 100.0000 |
| `IdentityPlatform.Admin.Bff` | 2878/3051 | 94.3297 | 456/466 | 97.8541 |
| `IdentityPlatform.Worker` | 26/26 | 100.0000 | 0/0 | 100.0000 |
| `IdentityPlatform.AppHost` | 152/153 | 99.3464 | 14/14 | 100.0000 |
| `IdentityPlatform.Database.Abstractions` | 111/112 | 99.1071 | 4/4 | 100.0000 |
| `IdentityPlatform.Database.PostgreSql` | 96/96 | 100.0000 | 2/2 | 100.0000 |
| `IdentityPlatform.Database.SqlServer` | 93/93 | 100.0000 | 6/6 | 100.0000 |
| `IdentityPlatform.Database.Hosting` | 42/44 | 95.4545 | 4/4 | 100.0000 |
| `IdentityPlatform.Database.PostgreSql.Hosting` | 13/13 | 100.0000 | 0/0 | 100.0000 |
| `IdentityPlatform.Database.SqlServer.Hosting` | 13/13 | 100.0000 | 0/0 | 100.0000 |

All eight actual mutation ZIPs were independently inspected: **48 passing baselines, 48 decisive expected kills and 48 passing restored controls**, with exact source/mutant hashes and oracle checks. The verifier's only change adds three finite setup-stage labels; its verdict/hash/oracle logic is unchanged. The sanitized receipts do not include original raw mutant TRX, so direct raw-mutant-report inspection is not claimed. Production exports, dormant session contract and exporter controls were reviewed; original export syscall traces are absent. No screenshot visual re-review or complete network/storage secrecy inspection is claimed. All required raw ZIP/XML/JSON artifacts were accessible; there is no transfer limitation for this accepted run.

### Retained accepted artifact bindings

These actual ZIPs belong to run `40257327073`, attempt 1 and source `45c2ce6ea0b9d02e962dd7037db300d0efe4b50f`. The final receipt binds all 61 foundation and eight mutation archive reviews; the 23 focused delivery bindings are listed here.

| Artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| `foundation-unit` | `12623152857` | `94a40608b73975f507280f64a7be25fe01cb92b27003784c38ed43f31a62313b` |
| `foundation-administrator-PostgreSql-browsersessions-product` | `12622474325` | `474d3b9fa9f6e93662023ea9148c360e691e63c5e6eccd67a15c08332449e28f` |
| `foundation-administrator-PostgreSql-browsersessions-lifecycle` | `12622279839` | `f019d2da4ba5332be3b8e2171830e08c745061b75d3e32c6a68b3562c7867cda` |
| `foundation-administrator-PostgreSql-browsersessions-foundations` | `12622913623` | `4a8cf03846d07881133593599747131b744bcb0cd570aa62b3f791753ff1446f` |
| `foundation-contracts-chromium` | `12623520094` | `2fe3a8a639da43e953b3e43dbb4218b6501df3c354d1191091d10500642c7582` |
| `foundation-administrator-PostgreSql-chromium-browsersessions` | `12622839272` | `ec0f323af418673b918302bc97a8d37d018eb157870c165d17bc605852b8a595` |
| `foundation-contracts-core` | `12622464823` | `8b404177d7d04cfe874809fbb3ffee1cbd4e5ef23c78aaa5c150a73bd0ce8e74` |
| `foundation-contracts-webkit` | `12622369897` | `cb65a997088d5e0551cf22d40da0c750c3e263375a4908eccc1589230b99bcbe` |
| `foundation-administrator-SqlServer-browsersessions-lifecycle` | `12623418046` | `390341866cad2f6a6d3321b0531b49d4c218e9bf5065831e36d2862bb2ae27dc` |
| `foundation-administrator-PostgreSql-webkit-browsersessions` | `12623268510` | `c4394a7d12aaaf3ff617faa78ccff3218fb905a3a783b7c86e45f4941f146a80` |
| `foundation-administrator-SqlServer-browsersessions-foundations` | `12623119002` | `9fd248e422e292d2d775bb12ebc0849e67d7eb062741a89af4f1f2bd0b34994a` |
| `foundation-administrator-SqlServer-chromium-browsersessions` | `12623108850` | `f6f8f13a737a548032d997143457836cd25ef193b91ca7f81da92cde6e384555` |
| `foundation-administrator-SqlServer-webkit-browsersessions` | `12622969056` | `519daf143f54b019c66f089d0f1148f89f4bd8c1e9ff1bd2036e02b3d6dd3b23` |
| `foundation-administrator-SqlServer-browsersessions-product` | `12622859443` | `902b7b297015f69af3626d508bc55d4780267480fbcec82f64c6217daccf3db0` |
| `foundation-coverage` | `12623503346` | `8af0e8743ad2c4518bc8f4333e819c831911c7a5d664caf81c91213f94174baf` |
| `administrator-protocol-mutations-PostgreSql` | `12623495462` | `27f5577a4e6d9f078f8744834e0c7ab14f6b1a27f48ac51b7012f9009371dbc2` |
| `administrator-protocol-mutations-SqlServer` | `12623307211` | `08e1336f8b897e578758e1684b57c59e58b78724604f2c0e45a8a5549d0bd47f` |
| `governance-mutations-PostgreSql` | `12623480525` | `da8b10405acf695750932b9627bfe476d5e7b987267433d5e39165c3c67c4f31` |
| `critical-mutations-SqlServer` | `12623178636` | `e6df267259d06161c5de9039d6b2efddadcd4f28b7e10edc0d0dd343684041a1` |
| `governance-mutations-SqlServer` | `12622919136` | `cc2485087bacfbf75ead219bba9cf48e2a2af729b04a8e1ddf1a7491e5a6fce6` |
| `critical-mutations-PostgreSql` | `12622344944` | `30cbba8a344de4bc0d09e104597fd45ccfbe079dab322731d5931edac5011c26` |
| `administrator-mutations-PostgreSql` | `12623014073` | `62f1c7681f873d945d5af255c48139f8dd43cb7f55cea86ba20c1000b41dc4f1` |
| `administrator-mutations-SqlServer` | `12623273807` | `c8e62e8a3805064635aa35d4853e3f2d67b77ee051b3aba28e995d7bb13d13d2` |

### Failures, review dispositions and remaining work

Both rejected runs remain rejected and preserved in [browser form policy evidence](browser-form-policy-evidence.md): initial source/run `9c65649`/`40250167955` and first approved correction `be46d39`/`40255156414`. A-010 was explicitly approved before product correction. Source review required inert-error header restoration, exact final-response counts, original nonce checks and safe fixture ownership; those corrections and permanent controls are included. Final architecture, security and test reviews found no scoped blocker. Local final solution build/format/analyzers, 226 Python regressions, eight setup diagnostics and eight profile diagnostics also pass; earlier command/compile failures remain recorded.

Current native-state cases pass on both providers. The SQL case runs 34.953 seconds overall, with finite options-preparation observations of 7,990/4,537/1,911 ms, factory resolution 0–1 ms and child entry 0–4 ms. Fresh request budgets remain unchanged; preparation generates no authority. These measurements do not establish the prior unmeasured timeout cause. The previously failing PostgreSQL governance-policy lane passes all 74 cases. Startup diagnostics preserve original readiness/lifecycle requirements and deadlines; no behavioral retry was introduced and the historical startup cause remains unknown. Visible browser outcome synchronization retains fixed URL checks and real effects; green execution does not prove a Playwright defect caused historical timeouts.

T020 remains unchecked. Different localhost ports are distinct origins on the same cookie host/site; these results do not establish distinct-host isolation or cross-site SameSite behavior. Remaining negative flows, theme/accessibility, T012/T016 browser acceptance, T003/T004 remaining integrations, T021 dedicated B3 mutations and T022–T024 coherent B3 acceptance remain open. A-009 restoration/T028, PR #69/issue #70, all six historical findings, 48 human rows, 345 not-assessed controls and production/public-release gates remain open. M02-C/D/E refresh/API/React acceptance is absent. No expired diagnostic approval is revived or production composition activated.

### Next bounded implementation

Extend the retained HTTPS harness with T020 negative logout/return journeys: absent or mismatched issuer session, expired/mismatched return correlation, remote failure and lost return. Assert local revocation independently, no unrelated issuer revocation, no local resurrection and truthful unverified remote status; reuse delivered server failure controls where browser transport adds no new invariant. Preserve native authentication, original deadlines, exact request counts and PostgreSQL/SQL Server × Chromium/WebKit execution. Add dedicated correctly trusted distinct-host/cross-site fixtures before claiming cookie isolation or cross-site denial, and complete the outstanding both-theme/accessibility matrix separately. Do not repeat the accepted harness, A-010 implementation or completed T017–T019 work.
