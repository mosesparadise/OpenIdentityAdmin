# Protected application list verification

## Accepted internal delivery — 29 October 2026

[PR #118](https://github.com/mosesparadise/OpenIdentityAdmin/pull/118) is squash-merged as `27ccdf033db5aa7bf13eab26fafd0eda5ffe7a9d`. Native Aspire now serves a real Applications catalogue with literal client-ID prefix search and paging through React, the protected BFF and an independently authorized Admin API. Use the [quickstart](quickstart.md); no custom PowerShell or separate frontend build is required.

Source `a689f53f3f8ca19df78b5ee49808c0b345931367`, tested merge `a9508340ff98fe88ce7ef9081e7bfacca6411d1b` and the squash share tree `9e4389b9e43b4233b9c06923a8ff68e678a4df2e`. Base was `0f71ceabee7d232f920895f615c97e96a95631be`; remote main and the squash tree were verified after merge. [Run 40960329950](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40960329950), accepted attempt 2, satisfies all 100 job obligations.

Acceptance combines 97 retained successful attempt-1 executions with the bounded SQL Chromium Security rerun and dependent coverage/final gates. Independent review matches the retained executions by exact name, start/end timestamps and conclusion despite GitHub assigning new job identifiers. The rerun passes all five cases with zero skips. The earlier startup failure remains recorded below with unproven cause; this is neither a clean first attempt nor a root-cause correction.

| Verified boundary | Accepted evidence |
| --- | --- |
| Ordinary execution | Hosted strict verifiers account for 81 report obligations and 4,869 passes, with no failed/skipped case in the accepted execution set |
| Preservation | All 2,013 previous administrator native names/type/traits retained; 40 additions produce 2,053; prior Node identities retained |
| Host and provider reads | 1,644 Host passes; seven application read/ownership cases per real provider |
| API authority | Genuine signed-token substitutions, current account/source/profile/token/authorization/grant changes across two instances, original signed/persisted expiry, native lock contention and rollback-only cleanup |
| Browser protection | 28/28 in each PostgreSQL/SQL Server × pinned Chromium/WebKit cell, including real paging, permission/failure/expiry clearing, proxy faults and distinct browser/transport cancellation controls |
| Native Aspire | Both provider demos pass three cases each, including genuine initialization/enrollment/sign-in, the real Applications list, logout and disposable restart |
| Contracts | 44 native core, seven Chromium and seven WebKit cases; 37 separate Node controls, exact routes, deterministic generated clients and compatibility checks |
| Frontend | Eleven retained plus nine new exact cases; 104-entry locked dependency review and deterministic embedded assets; scoped critical controller/observation coverage passes |
| Coverage | All 78 required inputs and 15 unchanged module floors pass; Admin API 92.4% lines/97.7% branches, BFF 94.4%/97.2% |
| Mutation controls | 68 behavioral kills with passing baseline/restored controls and source restoration, including two new application controls per provider |
| Artifact bindings | 94 accepted upload-log/API SHA-256 matches; the separately retained failed artifact is excluded |

The [artifact manifest](accepted-artifacts.md) records every accepted identity, execution attempt and digest. Final independent receipt SHA-256 is `01d53def5522a0d593e0f6cd548c498682906f7a61bc5a40d95f74a147f13b24`. Accepted manifest SHA-256 is `73752ea442084e306d2225b8abe9fb1aa52510fcb13a7bb3c3493f10e98aa46b`. Coverage artifact `12937416519` has upload/API digest `2bcd739df0359f7a4f727214fd67bae7c3bcc442e60843d62eba5e6b50517c72`. Coverage download logs select successful SQL Security artifact `12936489215`, not failed artifact `12936447506`.

Independent architecture/security, test/CI, UX and hosted-evidence reviews pass within their recorded scope. Evidence review inspected exact committed verifier logic, finite hosted results, source/tree bindings and upload/API digest agreement. The enforced runtime network policy blocked raw artifact transfer; no local ZIP-byte hashing or raw XML reparse is claimed. The ordinary count excludes separate frontend/Node checks, mutation executions and the focused WebKit experiment. Successful hosted verifiers enforce exact native identities, complete counters and mutation markers; these claims do not imply local raw-report inspection.

Feature 018 T001–T013 is complete for internal read-only delivery, with the normal squash of the coherent evidence/roadmap followup completing the record. Next is application creation/editing with its required permissions, concurrency, atomic audit and provider/browser tests. No schema or provider migration history changes. Refresh stays disabled. Actual Windows Docker Desktop, qualified screen-reader/manual acceptance, actual BFCache restoration, all six historical findings, Firefox/A-009 restoration, vendor activation and production/public-release gates remain open. Synthetic pagehide/pageshow tests do not establish real BFCache behavior, and source/automated UX review does not replace qualified manual acceptance.

## Historical implementation and verification record

The following checkpoints retain their then-current status. The accepted delivery above supersedes their pending wording; rejected observations and evidence limits remain applicable.

## Starting checkpoint and authority

Remote main verified29October2026 as `0f71ceabee7d232f920895f615c97e96a95631be`, preserving PR116 UI and PR117 acceptance. Current work is `121-protected-application-list`. The user's next-item request continues the approved protected-list → creation/editing sequence. The protected-list implementation is under verification; no hosted acceptance or merge is claimed.

Managed environment observations are current, network policy enforced, both MCR layer hosts allowed; no configured credential bindings or VPN. Existing worktrees and evidence are preserved.

## Independent planning review

Architecture review confirms approved validation choice and identifies missing protected access-token retention and current API authority. It rejects reusing issuance proofs or independent reads described as a snapshot. Test review independently confirms real API composition and browser-fixture prerequisites. Design dispositions appear in plan/research. Final source review dispositions are recorded below; hosted evidence review remains pending.

## Evidence limits

Local verification is in progress; retained-engine CI and both-provider final acceptance remain pending. Feature017 evidence is historical baseline only. All six findings, human rows, actual Windows, Firefox/A-009, vendor-documentation and production/public-release gates remain open; refresh stays disabled.

Architecture final design PASS: fixed query contract, original five-second budget, protected credentials and rollback-only authority fit approved scope. Test review caught native unbounded DisplayName/Id fields; contract now specifies finite per-field limits, SQL limit+1 projection, whole-response overflow denial and512KiB response cap. Boundary and overflow tests are mandatory for both providers.

Final test design PASS after bounded-projection correction. SQL substring semantics differ for supplementary Unicode but remain finite; final .NET UTF-16 limits enforce the wire contract. Cross-artifact analysis maps FR001–008 to T003–T013 and their named host/persistence/browser/contract boundaries; no unresolved design or authority question remains. Spec Kit prerequisite checks resolve all required documents; no extension hooks are installed. Requirements checklist passes7/7.

## Local implementation checkpoint — 29 October 2026

Native API/BFF composition, protected access credentials, current rollback-only authority, bounded provider projection, generated contracts, Aspire API runtime and React Applications screen are implemented. Refresh remains disabled.

The first corrected PostgreSQL run passed18of19 cases with zero skips: query/UTF16/ownership, seven two-instance authority revocations, current permission, actual list/search/expiry, real HTTPS response faults and visible paging/theme/focus/lifecycle/logout controls. The failed issuer-signed expiry substitution was retained and corrected for further verification. Host41 focused cases passed. Contract44 nonbrowser cases passed;7 browser cases failed because the pinned browser binary is absent in this environment. These are incomplete local results, not acceptance.

Browser diagnostics use systemChromium151.0.7922.173 with the synthetic dev certificate trusted, through Playwright1.63.0. They do not substitute for pinned Chromium153.0.8010.12/revision1243 or WebKit CI. Synthetic pagehide/pageshow events test lifecycle handlers, not actual BFCache navigation. No authenticated screenshots or token payloads are retained.

Independent source review found and approved corrections for public `client_id` (native private-presenter precedence), original signed `exp` (persisted-expiry restoration), and minimum BFF/API/access/session expiry. Upstream7.7.2 references: [server rounding](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Server/OpenIddictServerHandlers.cs#L5663-L5667), [client expiry](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Client/OpenIddictClientHandlers.cs#L3214-L3219), [presenter precedence](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Validation/OpenIddictValidationHandlers.Protection.cs#L491-L498), [native lifetime configuration](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Validation/OpenIddictValidationOptions.cs#L212-L214), [stored expiry restoration](https://github.com/openiddict/openiddict-core/blob/7.7.2/src/OpenIddict.Validation/OpenIddictValidationHandlers.Protection.cs#L589-L595). Signed public expiry is checked only after genuine native cryptographic/lifecycle validation, and the earlier signed/stored deadline is retained.

UX source review passes after stable error filters, requested-read focus restoration, page/count announcements and input-border contrast corrections. Test review requires observed native database contention/cleanup, caller abort and deterministic paging identities; these controls are being added before final acceptance. Temporary application diagnostic logging has been removed. Historical evidence and the six findings remain unchanged.

Final scoped architecture/security and test/CI source reviews pass. All 2,013 prior administrator native metadata records are unchanged; 40 additions yield 2,053. Ordinary Applications adds seven cases per provider, and BrowserProtection adds thirteen per provider. New required mutation lanes exercise rollback-only commit exclusion and whole-page lookahead overflow; their exact native identities, source boundaries and assertion markers use the existing fail-closed mutation verifier. Coverage obligations increase from 76 to 78 without changing the 15 module floors. The unchanged 58 mutation-verifier controls pass locally; actual mutant execution remains pending hosted evidence.

The corrected PostgreSQL diagnostic run passes 20/20, including original signed expiry, caller-abort propagation, observed native governance-lock deadline, joined cleanup, hashed authority rollback and subsequent reuse. Full Host tests pass 1,644/1,644 with zero skips. UI verification passes the retained eleven plus nine new cases, deterministic shipped assets, critical observation/controller coverage and the 104-entry locked dependency inventory. Code quality passes with only two obsolete legacy allowances retired. SQL Server and final hosted evidence remain pending.

SQL Server diagnostic run also passes 20/20 with zero skips. The AppHost test project builds with zero warnings/errors; full native container demos remain hosted obligations. The source candidate has no known local provider test failure; pinned retained-engine, mutation, coverage and exact-head acceptance are still pending.

## Hosted candidate history

[PR #118](https://github.com/mosesparadise/OpenIdentityAdmin/pull/118) opens the candidate at `88743b74445f1d49f3727a9e438d66f2a3e696b5`. Its [first run](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40955296631) is not accepted: native-image job enumeration and lifecycle/browser-session partition control tests retained stale counts after the explicit additions. The corrected expectations retain every prior identity and require the new jobs; all 239 Python verification-control tests pass locally. Import ordering and two unused imports are also corrected. This history is not replaced by successful local product tests; exact corrected-head hosted acceptance remains required.

The [second run](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40955937943), source `8b65fe5e6134ff5e7f0f8ca41a8f14894ccc52a5`, is also rejected: 93 successful jobs, six failures (three contract catalogue guards, two WebKit caller-abort cases and final acceptance), and one skipped coverage aggregate. Both native Aspire demos pass three cases each; both pinned Chromium BrowserProtection cells pass 28 cases; all 68 mutation controls pass with baseline/restored controls. Independent review reconciles 93 upload-log/API artifact digests and the tested/source tree, without local raw-ZIP reinspection. Neither partial successes nor digest reconciliation substitute for corrected-head acceptance.

Native discovery confirms 44 ordinary contract cases; the separate catalogue is corrected from 43. The retained Node control now requires both exact browser paths, cookie-protected `applications.read` for the management route, and anonymous access only for session status. All 37 Node identities remain unchanged. Complete local contract verification passes 44 native cases, 37 Node controls, production entry-point exports, deterministic regeneration, native schemas, compatibility and supply-chain checks.

Both WebKit failures occur after upstream start and a browser Fetch `AbortError`, while waiting three seconds for downstream cancellation. The missing propagation cause remains unproven; this is not an established upstream regression. Architecture and test review distinguish JavaScript cancellation from a confirmed transport disconnect. Corrected controls retain actual browser abort/no returned data and join its downstream work against the original eight-second request/cleanup ceiling, while the production server budget stays five seconds. A separate real HTTPS client copies only the genuine host-bound session/generation cookies, proves an actual 200 baseline, then requires caller callback, canceled client task and downstream cancellation within one original three-second envelope and before the original five-second server deadline. It verifies one send and unchanged hashed Sessions state. No timeout is renewed, no product behavior changes, no case is skipped and no browser deferral is added.

Runtime artifact-download limitation: the GitHub connector provides jobs/logs/artifact metadata and a file-service ZIP reference, but the current enforced network policy denies the signed artifact-download host. Independent review uses exact-source strict verifier execution, finite logs and upload/API digest agreement; it does not claim local ZIP-byte or raw XML reinspection. Historical raw artifacts are not reused for this head.

The corrected cancellation test builds without warnings/errors and passes the focused SQL Server diagnostic case, including one shared three-second cancellation envelope. Final narrow source review passes; code-quality verification remains unchanged. Corrected-source pinned-browser and full hosted acceptance are still required.

## Corrected-head startup failure and bounded verification rerun

[Run 40960329950](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40960329950), attempt 1, tests source `a689f53f3f8ca19df78b5ee49808c0b345931367` through merge `a9508340ff98fe88ce7ef9081e7bfacca6411d1b`; both have tree `9e4389b9e43b4233b9c06923a8ff68e678a4df2e`. All four BrowserProtection provider/engine cells pass 28/28, including the corrected browser and transport cancellation controls. All three contract lanes, both native Aspire demos and all 68 mutation controls pass.

Attempt 1 is not full acceptance: 97 jobs succeed, SQL Server Chromium Security and final acceptance fail, and coverage aggregation is skipped. The five-case security lane passes four and fails the dark/320 case before the journey, at `BootstrapHost.CreateAsync`. The original startup deadline expires with the database resource `Exited`/255 and dependent resources waiting. Exit 255 does not establish a cause; later SQL handshake diagnostics do not establish why the original container exited. Failed artifact `12936447506` has upload/API SHA-256 `8d807f970391e748fae77efe7bafb784749c160ddded4bd3325a6d71315181b1`. The independently frozen attempt-1 rejected receipt has SHA-256 `99fbbfe80531ba8f4b78c4caa2f852ae9c8640f654edbef1ba2f65acf86fd6f2`.

Independent test and hosted-evidence reviewers accept one bounded rerun of the unchanged failed job and dependent gates to supply the missing execution evidence. No source, assertion, deadline, inventory, coverage floor or browser obligation changes. The failed attempt remains evidence; any later acceptance must explicitly reconcile retained successes with the rerun, without claiming a clean first attempt or a root-cause fix. Repeated failure requires investigation rather than another automatic retry.
