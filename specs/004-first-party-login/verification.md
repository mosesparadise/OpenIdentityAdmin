# Feature 004 verification and review record

Status: implementation in progress; full acceptance pending. Partial executed evidence below does not establish release readiness or a control pass.

## Handoff

PR #3 was squash-merged to main as `df828d6f7a38ca142f44e4e7b4caac3a2be9df19` after checking head `a76789c1503e811b9704d7df46cbbe264e867e0b`, all seven successful checks and resolved review threads. GitHub rejected self-approval (422); normal squash merge succeeded, without bypass. Baseline:165 .NET /46 Python regressions and nine covered modules; these are PR #3 results, not feature004 results.

## Spec Kit and design review

Pinned Spec Kit1.1.3 specify template resolution, feature pointer, setup-plan, setup-tasks and check-prerequisites ran on branch004. No extension hook file exists. Requirements checklist reviewed before planning. Clarification used existing approval and explicit conservative pilot assumptions, without a new scope gate.

Independent architecture, security, test and UX reviewers inspected the design/native source. Lead dispositions:

| Finding | Disposition |
| --- | --- |
| Hard10s promise conflicted with native cancellation | Spec/plan distinguish cooperative application budget, per-operation bounds and measured stopped-DB case with1s scheduling tolerance. No abandoned task or native rollback claim. |
| Audit ownership/fields incomplete | Enumerate application atomic mutations including consume; add safe actor/target/outcome/independent correlation fields. Native lifecycle remains native-owned. |
| Existing-session transition absent | Pending→Consumed explicitly allowed only for current eligible existing session without freshness requirement; credential completion remains distinct. |
| Bootstrap matching vague | Versioned singleton binds native identities and immutable manifest; partial/drifted/ineligible records fail without repair. |
| Shared test state unsafe | Serialized collection, fresh contexts, exact manager-based restore, abort/recreate on cleanup failure; outage separate environment. |
| RP fixture ordered after browser tests | T015 now depends explicitly on T016/T017; fixture implemented before real callback acceptance. |
| Two-tab promise ambiguous | Delayed replaced-session completion denies; independent completed flows succeed, never subject switch. |
| Client authentication unspecified | Only client_secret_basic, explicit discovery and missing/wrong/body/conflicting-credential tests. |
| Long expiry tests unexecutable | Persisted expiry mutations plus deterministic application-clock tests; native code lifetime has one bounded real60s wait/provider. |
| Generated migration paths inaccurate | Corrected to existing provider Platform directories and Features/Health/SchemaCatalog. |

Read-only analysis after corrections:9 requirements,22 tasks,100% task coverage; no unmapped implementation task, unresolved clarification, duplicate requirement or remaining critical design inconsistency identified by lead. All18 constitution principles reviewed; no exception. This analysis is design evidence, not a substitute for actual independent code review or tests. Manual screen-reader/password-manager, official certification and M01-E restart/overlap remain explicit later evidence.

## Executed implementation checks

Local Linux/.NET10.0.401, actual PostgreSQL through Aspire13.6.1, OpenIddict7.7.1:

- Full solution build passed with zero warnings/errors at the first 53-case HTTP checkpoint.
- `dotnet test --project tests/backend/IdentityPlatform.Host.Tests --no-build --no-restore --no-progress`:159 passed, zero skipped at that checkpoint. Eleven newer proof-key syntax cases still require the next run.
- `dotnet test --project tests/aspire/IdentityPlatform.Issuer.Tests --no-build --no-restore --filter-trait Provider=PostgreSql --filter-not-trait Category=Browser --no-progress`:53 passed, zero skipped in41.6s. This explicitly excludes browser acceptance. Covers bootstrap/no-op/drift input, discovery/public JWKS, genuine protected form/callback, independent JWT validation, exact concurrent winner/invalid_grant loser, replay/linked native token revocation, after-code account/session/client/scope changes, malformed request/proof/client binding and silent-login denials.
- Earlier runs exposed a builder-lifetime fixture defect, broad native-claim rejection and missing short-challenge rejection. These were corrected; the failures are not represented as passes. The source-reviewed one-character matching-verifier case now has a passing permanent HTTP regression.
- Both provider migrations were generated using EF tooling. SQL Server execution is pending CI; its image download has been locally blocked by network policy.
- Pinned Playwright1.63.0 Chromium download was denied by this workspace network policy (`cdn.playwright.dev`, HTTP403). No browser execution is claimed. CI browser provisioning/TLS trust remains mandatory; no certificate bypass or substitute browser was used.
- Static full-gate scripts, updated exact-count matrix, combined per-module90/80 coverage, expanded form/fault/browser tests, visual review, mutation evidence and final independent reviews remain pending.

Source review corrections: current lockout-enabled/application-type/settings checks; bounded correlation-cookie querying and concurrency; cancellation propagation; positive max_age recheck after completion; anonymous denied actor versus victim target; request-rejection headers/status; separate native/public claims catalogue; RFC7636 syntax guard; test-RP backchannel redirect refusal/correlation consumption/terminal failures; cleanup attempts for all resources and fixture invalidation on failed restoration. Revised paths still need the final complete suite/re-review. No ASVS row is promoted from not_assessed by this partial record.

### Second local checkpoint (still incomplete)

- Strict `python3 scripts/verify.py unit`:208 required cases passed (Core25, Host170, Architecture5, AppHost8), zero skipped; fresh exact-count TRX validation passed.
- Expanded PostgreSQL HTTP suite:60 passed, zero skipped in44.2s before adding the noncanonical challenge HTTP case. Includes forged/stale forms, generic credential errors, password non-echo and replaced-tab denial. Browser cases remained explicitly excluded.
- All46 Python assurance/evidence/supply-chain/coverage-gate regressions passed; all345 ASVS rows remain not_assessed. License inventory now covers221 locked package/version pairs with the test-only MPL exception documented.
- Canonical challenge negative control exposed .NET's `TryDecodeFromChars` throwing for invalid source encoding; switched to its OperationStatus overload. Its permanent unit regression now passes.
- New CI matrix counts include pending Chromium cases; they are expected counts, not local passes. Draft delivery retains all unfinished task checkboxes and review gates.

- Full static-analysis/formatting gate and its clean/CA5350/IDE0055/IDE1006 enforcement controls passed after removing unused imports. Current source snapshot Gitleaks scan passed. The first outage/body-bound test additions compile cleanly and await execution.

### Third local checkpoint and first draft CI

- Latest full build: zero warnings/errors. PostgreSQL HTTP suite:67/67 passed, zero skipped. Added real audit INSERT-permission denial/transaction rollback, known-length/chunked body bounds, stopped-database503/readiness/liveness, 128-character valid proof, anonymous two-tab antiforgery recovery and A-login/B-login/A-silent-reuse authentication-time regression.
- Draft PR#4 first head `4eed7bb`: assurance, supply-chain and208 unit cases passed in GitHub Actions run38059292010. PostgreSQL HTTP cases passed but all five Chromium cases failed strict TLS verification at the test RP. Static gate caught one unused import, removed locally. SQL Server run still pending at this checkpoint.
- Chromium's documented NSS trust flag for a self-signed server leaf is `P,,`; the original workflow incorrectly used CA trust `C,,`. Corrected to peer trust without disabling TLS checks; rerun required. Source: <https://chromium.googlesource.com/chromium/src/+/main/docs/linux/cert_management.md>.
- Combined first-run unit/PostgreSQL coverage meets AuthServer91.0% lines/87.5% branches and eight module floors; Migrator89.7% lines still needs SQL evidence. This partial merge is diagnostic, not the final combined gate.
- Architecture review identified a globally newest-session auth_time oracle; expected identity/time now comes from each flow's completed interaction or that browser's previous session, with the passing two-browser regression above. Remaining acceptance includes persisted bootstrap drift/concurrency, expanded freshness/expiry, negative RP validation controls, browser baselines/TLS negative control, Firefox/WebKit cadence, fault/cancellation/log tests and mutations. Existing unchecked tasks remain authoritative.

### Expanded HTTP, mutation and rendered-UI checkpoint

- Latest local solution build and full static-analysis/formatting gate passed. Expanded actual PostgreSQL HTTP run:75/75 passed, zero skipped,3m18s; fresh TRX includes real61s native-code expiration, freshness/session-reuse modes, eight-interaction capacity/recovery, concurrent form completion, independent wrong issuer/audience/type/signature/nonce controls. This excludes six browser cases; their expected matrix count is not a local pass.
- Two temporary source mutations were killed by assertions in the52-case `PilotPolicyTests` cohort: lowering verifier minimum43→1 failed `Verifier_requires_RFC7636_length(42,false)`; replacing the browser digest comparison with a nonempty check failed `Browser_secrets_are_256_bit_and_checked_against_independent_digest`. Each mutant had exactly one assertion failure, not a build/discovery failure. Both original files were restored byte-for-byte, rebuilt, and all52 cases passed. These do not complete the pending eligibility/session/replay mutation obligation.
- CI38060004485 at `692fe98` proves the `P,,` browser trust correction: PostgreSQL67 HTTP plus all four light/dark ×320/1280 Chromium reflow/axe cases passed. The remaining browser-login case stopped at an overstrict header string assertion: ASP.NET antiforgery correctly strengthens `no-store` to `no-cache, no-store`. Replaced equality with parsed `CacheControlHeaderValue.NoStore`; next run must prove the complete login. No production policy was relaxed.
- Four empty-form Chromium screenshots are retained in that run's `foundation-PostgreSql` artifact for review. They contain no credentials/tokens. Initial rendered review found a coherent slate/indigo card, visible labels/controls and no clipping at320px; final independent UX disposition and deterministic baseline comparison remain pending.
- Combined first-draft unit/PostgreSQL/SQL coverage met every module floor: Core100/100, Infrastructure99.0/100, ServiceDefaults100/100, Migrator98.1/97.8, AuthServer91.0/97.3, Admin API/BFF/Worker100/100, AppHost98.4/100 (line/branch%). Because that run failed browser acceptance, these are diagnostic coverage results, not a clean final-head gate.
- Independent narrow security source re-review found no new runtime defect in response protection/status pages, cookie clearing on failure, cancellation-before-cookie or current-state/native ownership. It executed no tests. Independent test review accepted the flow-specific expected-session correction and identified the documented peer-trust fix. Final feature acceptance still requires T023–029; no control/certification claim is made.

Independent rendered UX review inspected all four Chromium captures and accepted their initial empty-login layout/readability as candidate baselines. They are now versioned with capture provenance and an explicit review policy under `tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/Baselines/`; exact comparison execution remains pending. This scope does not include error/focus/system states or manual accessibility acceptance.

SQL Server CI38060004485 also passed67 HTTP plus all four Chromium theme/axe/reflow cases; only the same corrected cache-header assertion failed. Comparing its independently captured PNGs against PostgreSQL found three byte-identical captures. Light1280 differs at30 pixels around the native Theme selector border by only one8-bit RGB unit, with identical dimensions. The screenshot gate therefore decodes pixels with pinned Chromium and allows only maxchannel delta1 and100 changed pixels, with no masks and exact dimensions. This measured rendering-noise allowance replaces the unexecuted byte-hash comparison; it does not change the reviewed images. Baseline comparison and full login still await the next CI head.

At `10b7eb7`, CI38060923071 PostgreSQL passed75 HTTP cases plus the genuine Chromium keyboard/form/callback/token-storage/CSP journey and the trusted/unrelated-untrusted TLS control. All four visual cases reached their comparison step but failed because Playwright did not deserialize the comparison result into the requested typed dictionary. Changed that test helper to a fixed three-integer result array with an explicit length assertion; no application behavior, image baseline or tolerance changed. Full build passes; the corrected comparison still requires CI execution.

### Completed CI checkpoint at 2920ae1

[Run 38061570437](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38061570437) completed successfully for source head `2920ae1d96f80fce4328703545fc0895dbbd3f72`, using GitHub's merge checkout `f59c3352d2a5726de784921c0cb53153b25186e5`. All seven jobs passed: assurance, static analysis/formatting, dependency/license/secret checks, unit, PostgreSQL, SQL Server and combined coverage. The earlier pending statements above are historical checkpoints, superseded for that head by this result.

All384 .NET cases passed with zero skips:208 unit/architecture plus88 per real database (Persistence3, AppHost4, Issuer81). Each issuer cell includes75 HTTP cases and6 Chromium cases: complete keyboard/form/callback, trusted/untrusted TLS, and four reviewed light/dark ×320/1280 visual/reflow/axe cases. All nine unchanged90% line/80% branch module floors passed; AuthServer91.8/97.4, Migrator98.1/97.8, Infrastructure99/100, AppHost98.4/100, others100/100. This proves the corrected pixel-comparison serialization; it does not close T023–T029, manual accessibility or certification.

### PR #4 review follow-up

The user requested fixes, approval and squash merge of this Development-only increment, followed by the next planned item. The PR was already marked ready for review when this request began. Merging this bounded checkpoint does not accept M01-D or erase T023–T029; bootstrap acceptance T023 remains the next implementation item.

| Review finding | Correction / permanent regression |
| --- | --- |
| Replacement lacked a distinct audit intent | Conditional unrevoked→revoked update adds `session-replacement/revoked`, account actor and SHA-256 session target in the existing transaction. Initial login adds no replacement; real-provider test independently derives the digest. Audit denial must roll back completion/new session/old revocation, and the original browser must still complete silent login. |
| Wordmark implied nonexistent home navigation | Noninteractive wordmark, preserved skip link; Chromium keyboard checks move from skip link directly to Theme. Existing reviewed visual baselines remain unchanged. |
| Unexpected faults were silent | Source-generated Warning1001 in the dedicated `PilotRequestProtection` category, fixed template and independent random incident ID only; matching `X-Identity-Incident` response header. No exception/request data supplied. Unit test checks exact structured fields and distinct IDs despite repeated inbound trace IDs. Ordinary invalid requests retain their status without an incident. |
| Three secrecy assertions exposed detected material | Shared boolean assertion with fixed diagnostic for captured HTML/storage/body. Negative controls intentionally detect token/password/client-secret/code sentinels and verify the resulting exception contains neither sentinel nor captured cookie/content. Case names use integer IDs. Nearby raw body, code and sensitive collection comparisons also use fixed boolean failures. |
| Durable CI evidence was stale | The exact2920ae1 checkpoint above records both provider/browser cells and coverage, while retaining all remaining acceptance gaps. |

Before product changes, the compiled middleware regression failed for its missing independent incident ID (one other client-rejection case passed), and the actual PostgreSQL replacement regression failed for its missing audit intent. These were assertion failures, not build/discovery failures. Local build of corrected source passes with zero warnings/errors; the final unit/provider/CI results are recorded with the review handoff.

Independent security source review accepted the bounded conditional audit and fixed incident payload; no new confirmed defect in that diff. Independent test review identified failure-output hazards in collection assertions and test case names; those were corrected. Neither reviewer claims test execution. Actual logging-provider enrichment/scopes remain part of T026's integration evidence, beyond the exact application payload proven by the unit test. No assurance row is promoted.

Corrected-source strict unit matrix:216/216 passed, zero skipped (Core25, Host178, Architecture5, AppHost8), including all eight new incident/assertion cases. Assurance integrity and AI-instruction structure checks passed. Both-provider/Chromium/final combined coverage are mandatory for the review-fix commit and remain pending at this local checkpoint; their exact head/run is recorded in the PR handoff and carried into the next delivery record.


### PR #4 merged checkpoint and T023 handoff

[Run38066104676](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38066104676) passed all seven jobs on source `7d7205843db186bc95ae22dd5e5d88c086c22c33`:392 .NET tests, zero skips (216 unit/architecture, plus88 per provider including81 issuer/Chromium). All nine coverage floors passed; AuthServer92.4% lines/98.2% branches. Local corrected-source PostgreSQL75 HTTP cases also passed. Both independent narrow security/test source reviews closed their findings; all seven GitHub threads were resolved.

GitHub rejected the formal approval attempt with422 because the connected account authored PR #4. Normal squash merge succeeded as `8a70b029b8930e330fa1d881bc7839627a39b07d` after verifying exact head, checks and threads, without bypass. This is a Development-only checkpoint, not full M01-D acceptance. The next authorized work is T023, whose detailed [bootstrap acceptance plan](bootstrap-acceptance.md) was documented and architecture-reviewed before implementation. T024–T029 and release gates remain open.


### T023 bootstrap acceptance implementation

The [execution plan](bootstrap-acceptance.md) was recorded before code. A separate health-only Aspire fixture applies real migrations to a fresh disposable database per case and invokes the actual PilotBootstrap through least-privileged runtime scopes. Native managers seed account/application/scope conflicts; administrator access is reserved for provisioning, intentional corruption, permission fault injection, inspection and cleanup. No production code or policy changed.

The new BootstrapAcceptanceTests adds49 cases per provider:44 persisted conflicts, valid no-op with existing password-failure count, deterministic two-context first-creation race, single-caller cancellation/drain, and audit INSERT denial during first creation and verification. Comparisons cover all persisted Identity/OpenIddict/pilot records, retain the complete original creation audit on successful no-op/retry, and never put captured values into assertion diagnostics. Successful/empty state checks reject unexpected related records. Test matrix expects130 Issuer cases per provider (existing81 plus49); expected complete matrix is490 .NET cases, not a claimed pass until CI finishes.

Local commands: solution build with --no-restore/--disable-build-servers/-m:1 passed with zero warnings/errors; whitespace and IDE0005 verification passed; scripts/verify.py unit passed216 required cases with zero skips. Initial new-case PostgreSQL run passed49/49 before strengthening the snapshots. Final full PostgreSQL HTTP, SQL Server, browser and combined coverage evidence remains pending at this checkpoint. Assurance integrity and AI-instruction checks pass; all345 ASVS rows remain not_assessed.

Independent architecture and test source review accepted the bounded design after correcting the explicit cancellation deadline, matching pilot Identity policy, and adding complete snapshots/original-audit preservation. Reviewers ran no test suites. T023 remains unchecked until both real providers and CI prove the final assertions. T024–T029, full M01-D acceptance and external release gates remain open.


The strengthened-source local PostgreSQL HTTP suite passed124/124 with zero skips, including all49 new bootstrap cases and the previous75 HTTP cases. Browser cases were explicitly excluded locally; CI supplies both-provider Chromium evidence. The build and unit counts above apply to source `7341d16b6fed3ef782827a9cc891ce4e9eebcf2a`.


### T023 completed CI checkpoint

[Run38067841270](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38067841270) passed all seven jobs for source `7341d16b6fed3ef782827a9cc891ce4e9eebcf2a`. All490 .NET cases passed with zero skips:216 unit/architecture and137 per real provider (Persistence3, AppHost4, Issuer130). Each issuer cell includes124 HTTP/database cases and6 Chromium cases; all49 new bootstrap cases passed on each provider. SQL Server's issuer cell completed in6m20s, below the existing660s outer deadline. No retry, skip or timeout increase was needed.

All nine unchanged90% line/80% branch module floors passed: Core100/100; Infrastructure99.0/100; ServiceDefaults100/100; Migrator98.1/90.0; AuthServer92.6/97.7; Admin API/BFF/Worker100/100; AppHost98.4/100. Static analysis/formatting and all three enforcement negative controls, assurance integrity, dependency/license/secret gates and strict fresh-report test counts passed. The initial pending statements above are historical checkpoints superseded for this source by this run.

T023 is complete with bounded independent architecture/test source-review dispositions recorded in the execution plan. [PR #5](https://github.com/mosesparadise/OpenIdentityAdmin/pull/5) contains this increment, uses the Conventional Commit title `test(auth): verify bootstrap conflicts and rollback`, and remains unmerged for review. This documentation-only completion commit receives its own final-head CI run; its result is linked in the PR to avoid a self-referential evidence commit loop. No product/test source changes follow the tested source checkpoint. T024 is next; T024–T029 and full M01-D/manual/external acceptance remain open, and no ASVS row is promoted.
