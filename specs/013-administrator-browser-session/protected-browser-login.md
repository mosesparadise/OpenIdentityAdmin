# T010 protected BFF login and bounded resume

Status: implemented for internal development; exact-head hosted acceptance is pending. T010 remains unchecked until acceptance. This continues accepted T009 from PR #81 and documentation main `2bb371100d591d64637ff424b228590a424ea51d`; it does not repeat T005–T009. T011 canonical B1 callback and later production composition remain pending.

## Product behavior and ownership

The explicitly composed `/bff/login` GET is inert: it renders a finite sign-in form with native antiforgery and a purpose-isolated protected initial-generation cookie, without creating a durable login intent. Its protected POST requires HTTPS, exact configured Origin, exact form media, actual bounded bytes, one antiforgery field, unambiguous cookies and the current durable browser generation. It then uses the existing T005 transaction to replace the generation, revoke any former ticket and atomically record the authenticated actor when a live canonical B1 cookie actually proves that actor. A stale/revoked canonical cookie permits deliberate anonymous replacement; a live authentic cookie associated with another browser denies. Failed replacement retains the previous ticket and all durable state.

Only acknowledged committed work can stage the new generation/prelogin cookies and a 303 to the fixed issuer browser-entry route. The new intent has its own original five-minute deadline; publication must also satisfy the original proof that admitted the POST. Neither client return URLs nor supplied intent/generation fields are accepted.

The exact `/bff/login/resume?Completion=...` GET accepts one canonical completion selector, the independently protected pending envelope, the matching browser generation and the original pending durable row. It reserves that intent once through T005. The local transaction and DI scope are fully disposed before the actual named OpenIddict client request/challenge handlers run inside the existing native HTTP owner. No Sessions lock spans native HTTP. A private one-use receipt binds that exact HTTP context to the intent digest, browser digest, generation and reservation digest. Native state protects that tuple and the original intent expiry; raw selectors are not projected as private state claims. Missing capture, receipt substitution or unreserved private-claim injection cannot silently downgrade the request to ordinary B2.

Native state/PAR continues to use the existing library cryptography, managers and transaction ownership. A native failure leaves the reservation spent; no automatic retry occurs. Lost acknowledgement or cleanup failure can leave committed state, but publishes no redirect or cookie. The original ten-second request budget includes body parsing and local work. An immutable product publication guard additionally checks all original protected proof bounds after native scope disposal/drain and throughout output admission. A thrown guard permanently fails the request. Native/local denial responses retain no-store and no-referrer.

## Explicit composition limit

`AddBrowserLogin` and terminal `UseBrowserLogin` remain dormant in production Programs. The integration uses an explicit Generic Host/IApplicationBuilder pipeline and resolves the named native handler only within its request owner. Unrelated health/404 routes do not trigger native authentication. `UseBrowserLogin` deliberately rejects automatic `WebApplication` composition: its automatically inserted global authentication would initialize the OpenIddict request handler outside ownership. The later T023 production adapter must explicitly solve and verify that integration before activation. It must not use undocumented framework middleware marker keys or weaken native ownership. Startup also requires a browser registration, identical canonical issuer/client destinations, and a native callback distinct from login/resume.

The minimal HTML form is an internal functional surface; T013/shared UI and T028 browser/theme/accessibility restoration remain required. In-process TestServer/issuer bridging is not trusted TLS or browser-engine evidence.

## Scenarios and evidence

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
