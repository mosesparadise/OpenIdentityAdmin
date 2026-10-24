# T010 protected BFF login and bounded resume

Status: accepted for internal development on 24 October 2026; [exact-source acceptance](#accepted-t010--24-october-2026) is recorded below. T010 is complete. This continues accepted T009 from PR #81 and documentation main `2bb371100d591d64637ff424b228590a424ea51d`; it does not repeat T005–T009. T011 canonical B1 callback and later production composition remain pending.

## Product behavior and ownership

The explicitly composed `/bff/login` GET is inert: it renders a finite sign-in form with native antiforgery and a purpose-isolated protected initial-generation cookie, without creating a durable login intent. Its protected POST requires HTTPS, exact configured Origin, exact form media, actual bounded bytes, one antiforgery field, unambiguous cookies and the current durable browser generation. It then uses the existing T005 transaction to replace the generation, revoke any former ticket and atomically record the authenticated actor when a live canonical B1 cookie actually proves that actor. A stale/revoked canonical cookie permits deliberate anonymous replacement; a live authentic cookie associated with another browser denies. Failed replacement retains the previous ticket and all durable state.

Only acknowledged committed work can stage the new generation/prelogin cookies and a 303 to the fixed issuer browser-entry route. The new intent has its own original five-minute deadline; publication must also satisfy the original proof that admitted the POST. Neither client return URLs nor supplied intent/generation fields are accepted.

The exact `/bff/login/resume?Completion=...` GET accepts one canonical completion selector, the independently protected pending envelope, the matching browser generation and the original pending durable row. It reserves that intent once through T005. The local transaction and DI scope are fully disposed before the actual named OpenIddict client request/challenge handlers run inside the existing native HTTP owner. No Sessions lock spans native HTTP. A private one-use receipt binds that exact HTTP context to the intent digest, browser digest, generation and reservation digest. Native state protects that tuple and the original intent expiry; raw selectors are not projected as private state claims. Missing capture, receipt substitution or unreserved private-claim injection cannot silently downgrade the request to ordinary B2.

Native state/PAR continues to use the existing library cryptography, managers and transaction ownership. A native failure leaves the reservation spent; no automatic retry occurs. Lost acknowledgement or cleanup failure can leave committed state, but publishes no redirect or cookie. The original ten-second request budget includes body parsing and local work. An immutable product publication guard additionally checks all original protected proof bounds after native scope disposal/drain and throughout output admission. A thrown guard permanently fails the request. Native/local denial responses retain no-store and no-referrer.

## Explicit composition limit

`AddBrowserLogin` and terminal `UseBrowserLogin` remain dormant in production Programs. The integration uses an explicit Generic Host/IApplicationBuilder pipeline and resolves the named native handler only within its request owner. Unrelated health/404 routes do not trigger native authentication. `UseBrowserLogin` deliberately rejects automatic `WebApplication` composition: its automatically inserted global authentication would initialize the OpenIddict request handler outside ownership. The later T023 production adapter must explicitly solve and verify that integration before activation. It must not use undocumented framework middleware marker keys or weaken native ownership. Startup also requires a browser registration, identical canonical issuer/client destinations, and a native callback distinct from login/resume.

The minimal HTML form is an internal functional surface; T013/shared UI and T028 browser/theme/accessibility restoration remain required. In-process TestServer/issuer bridging is not trusted TLS or browser-engine evidence.

## Implementation checkpoint and scenarios

The pending statements in this checkpoint describe the pre-publication state; final acceptance below supersedes them.

Requirements: FR-001/002/005/007/008; B3-AUTH-01/02, B3-WEB-02, B3-STATE-01/02.

| Test family | Required boundary |
| --- | --- |
| `BrowserLoginTests` | Inert GET, protected POST, stale antiforgery, exact Origin/form, original expiry, equal-generation cross-browser substitution, old-generation replay |
| `BrowserLoginNativeTests` | Actual password and unused MFA completion, one native PAR/state, independent native state validation of the reserved tuple, cross-root replay denial |
| `BrowserLoginRaceTests` | Real provider-observed blocked peer on the browser budget; one reserve/audit/PAR/state across independent hosts |
| `BrowserLoginAuditTests` | Actual audit INSERT denial for initial start, reservation and authenticated replacement; exact provider denial, complete snapshot rollback and positive control after restoration |
| `BrowserLoginFailureTests` | Actual committed-but-unacknowledged reservation, missing product capture, native state child commit/disposal failure, actual issuer PAR rejection, spent-state no retry |
| `BrowserLoginDisposalTests` | Exact disposed DbContext witness, local failure after commit, original proof expiry during replacement disposal, original intent expiry after successful PAR during native root disposal |
| `BrowserLoginReplacementTests` | Authentic prior B1 revocation/actor audit, stale revoked-cookie recovery, foreign live-cookie denial |
| Host proof/request/composition/state/publication tests | Real protection purpose/time bounds, raw framing/quote/UTF-8 rejection, startup identity/callback constraints, private-claim injection, original publication bounds and terminal guard failure |

Actual native discovery preserves all 1,557 prior Administrator test identities and their full portable metadata. It adds 48 cases (24/provider), total 1,605: 750 PostgreSQL and 855 SQL Server including unchanged browser cases. BrowserSessions requires 182/provider; ordinary Administrator totals are 737/842. Both providers remain mandatory. No schema, provider implementation, migration history, package version, coverage floor or required mutation target changes are introduced.

Local evidence is supporting evidence only. The initial focused Host execution passed 51/52 and exposed native cookie parsing retaining a quoted value; the explicit quote rejection corrects it and the regression remains. Initial compile/format errors were corrected without weakening checks. The first Python invocation lacked the scripts import path and was corrected to `PYTHONPATH=scripts`; 53 focused tooling controls pass after updating both exact partition totals from actual discovery. The final full local Host run passes 1,410/1,410, zero skips, including all 29 added cases. Both Administrator and Host projects build with zero warnings/errors; the source-quality gate passes without new allowances. Hosted provider execution, aggregate coverage and final source/evidence review remain required. The previous local Aspire startup failure is retained in T009 evidence; unchanged local startup loops were not repeated.

Independent architecture, security and test reviews identified and corrected global authentication dispatch, missing final receipt enforcement, stale cookie recovery, original admission-proof replacement, final native-disposal expiry, missing no-referrer on native failures, masking cross-browser test setup, incomplete snapshot payloads and fixture startup cleanup. Final scoped security source review passes; no agent review substitutes for executed provider evidence or independent security assessment.

All six historical findings, 48 human rows, 345 not-assessed assurance controls, production/public-release gates, PR #69/issue #70 and the viewer advisory remain open. A-009 permits internal progress with documented Playwright/Firefox deferrals; T028 restoration remains open. No expired diagnostic approval is revived.

## Accepted T010 — 24 October 2026

[PR #83](https://github.com/mosesparadise/OpenIdentityAdmin/pull/83) was normally squash-merged after all 69 exact-head jobs and independent architecture/API, security, test and evidence reviews passed. Head, base, mergeability, required checks and review state were verified before merge; no review restriction was bypassed. Remote main and the actual squash tree were then verified.

| Boundary | Verified identity |
| --- | --- |
| Reviewed source | `6eb407367d8205f1688dda8f57d7326ba99b9caf` |
| Base | `2bb371100d591d64637ff424b228590a424ea51d` |
| Tested merge | `59e91a240b3c796e10570bc22a28ea626bd8693f` |
| Source, tested merge and squash tree | `f73f35aaba68142f9c9c3245adc725dd56c7465c` |
| Actual squash | `b5147d0e28f633e9f42becf8231f437a500afc2d` |
| Workflow / attempt | [40098076752](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40098076752) / 1 |
| Coverage / acceptance jobs | `120889041555` / `120889147322` |

Independent raw review verifies six TRX reports and 1,869 passing executions, with zero failed, skipped or aborted cases: BrowserSessions 182/provider, Host 1,410, Core 25, Architecture 5 and AppHost 65. Exact names, result/definition/execution identifiers and source bindings match. All 48 added provider cases and 29 added Host cases pass. Every prior Administrator identity and portable metadata is preserved: 1,557 existing plus 48 new equals 1,605. Both PostgreSQL and SQL Server remain mandatory.

All 52 required coverage inputs match this run/head and their API digests appear in the hosted download log; the unchanged nonempty-input guard passes. Independent recomputation of raw merged Cobertura passes all 15 unchanged 90% line/80% branch floors. Admin.Bff measures 2,314/2,474 lines (93.53%) and 861/870 quantified branches (98.97%). All eight mutation ZIPs match their API SHA-256; sanitized receipts independently verify all 48 kills, 48 passing baseline controls and 48 passing restored controls, with exact manifests and recomputed original/mutant source hashes. Local supporting evidence includes 219 Python controls, builds, formatting and unchanged source-quality allowances.

Review scope is explicit: six raw TRX reports, merged coverage XML and eight sanitized mutation receipt bundles were inspected. The 52 individual input XML files and raw mutant TRX files were not independently unpacked. Coverage branch rates follow the report's quantified condition fractions; unquantified branch markers are not additional measured branches. In-process controls do not establish trusted TLS, browser-engine, accessibility or full B3 acceptance. The initial local quoted-cookie regression and corrected compile/tool invocation failures above remain retained; the accepted hosted run passed on its first attempt without reruns or skipped required cases.

| Retained final artifact | ID | SHA-256 |
| --- | --- | --- |
| foundation-unit | `12561467476` | `cc5eb2a65a758590f1eb725027785e36e5d066298eca9f8241b0c58f1d89eb7e` |
| foundation-administrator-PostgreSql-browsersessions | `12562345385` | `dfac014224331834909045f8b20012c09b70b47abe9730e59ff050806f00eff8` |
| foundation-administrator-SqlServer-browsersessions | `12561908435` | `6ea25180b4b1bc2b2c6ef978bd9468371b6456e571199385ce20daa45deb64a1` |
| governance-mutations-PostgreSql | `12562320766` | `0cbf959f31f4de9fb3b30a933d48aec8ac840e66f72ba0c8e8abb6c924fb372c` |
| critical-mutations-PostgreSql | `12561717716` | `2e01448aa36391cc763ea48bb1db40002fb6604976bd1e8f094cd14b28906585` |
| administrator-protocol-mutations-SqlServer | `12561534041` | `bc94506ff75e5c2934cdd42baa4e34d30652c6edada196e768e973514824f07e` |
| administrator-protocol-mutations-PostgreSql | `12561069031` | `db95baa99495b792f0acfd0fc63093d5cc2e96cb182c46227fa64e2d083f8092` |
| administrator-mutations-PostgreSql | `12562011345` | `775df60fb8a9636af0031141c3c1241018233fc18835693a692611a0fbc435c5` |
| governance-mutations-SqlServer | `12561632303` | `8e0b11efe277392e17a65c470c0ba2395724b7c754c2eec8711a3c3f6fa35b4e` |
| administrator-mutations-SqlServer | `12561683591` | `3903737dbf37341578cf4428e667220fe066d0204b19d41859a6112dcd144d62` |
| critical-mutations-SqlServer | `12561069806` | `685f23225dca7de963ba3baf53b8b469de41d63460c6f40c4ea9d6bcb8cf1f3e` |
| foundation-coverage | `12562560668` | `e821f37c9411457c9b40edbceadd2007f4e986a9f634d25a13d2352d51a0e041` |

T010 is complete for internal development. Next is T011: own the native product callback and establish a canonical B1 ticket with original source expiry, atomic browser/ticket/audit ordering, commit-before-cookie publication and uncertain-commit controls. T023 must still provide the reviewed production adapter; T013 supplies the remaining browser/session UI and cookie integration. Full B3, production/public release, all six findings, 48 human rows, 345 not-assessed controls, A-009/T028 browser restoration, PR #69/issue #70 and the viewer advisory remain open.
