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


### PR #5 final-head squash checkpoint

[Run38068877907](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38068877907) passed all seven gates on exact PR #5 head `6d5e310f54f845199db9e9660d8922c7174af865`:490 .NET tests, zero skips, including130 issuer cases on each real provider. All nine coverage floors passed; AuthServer92.6/98.2 and Migrator98.1/97.8 (line/branch%). GitHub review recommended approval with zero findings and no open review threads. The user's squash-and-proceed instruction was executed through normal expected-head squash merge as `d4664a3f0222ffda2b0d4ae01d8e6aaedb551b92`, using the Conventional Commit PR title. No review restriction was bypassed.

### T024 interaction acceptance implementation

The [execution plan](interaction-acceptance.md) preceded implementation. This increment adds44 cases per real provider:7 password eligibility/lockout,31 cached-request/browser binding and6 database-transition cases. All use genuine Aspire login journeys or explicitly bounded component calls through the actual production service registration and least-privileged runtime role. No production source or policy changed. The strict expected matrix is174 issuer cases per provider and578 .NET cases overall; acceptance remains pending until both providers pass.

Review corrections include independent pre-resume authentication-time snapshots, valid identity-specific antiforgery setup, the separate cancellation deadline exercised against an uncancellable caller, deterministic completed-session mismatch denial/recovery, full pre-existing audit preservation and bounded cleanup. Architecture/test/security reviews are source reviews and do not claim execution.

Initial PostgreSQL cohorts passed password7/7 and transition5/5 before final assertion strengthening. The first binding run passed26/31 and identified two incorrect test assumptions (native error format and absent code on denied callbacks); both were corrected. Full final-source PostgreSQL HTTP, unit/static gates and both-provider Chromium/coverage CI remain pending at this checkpoint. T024 remains unchecked. T025–T030, full pilot acceptance and external release gates remain open. T030 specifically tracks the source-identified concurrent browser-capacity gap; the existing capacity proof is sequential only. All345 ASVS rows remain not_assessed.


At this intermediate checkpoint, local source built with zero warnings/errors and the strict unit matrix passed216/216, zero skips. Bounded independent architecture/test/security source reviews had closed their findings; reviewers ran no tests. The full PostgreSQL HTTP suite and final static/CI gates were still pending. This checkpoint is superseded by the fixture-isolation and native-replay corrections and the passing `32d5903` evidence below.


The first full local168-case PostgreSQL run at `1833ec8` passed162 and failed6. One new assertion incorrectly expected a callback after native request-token redemption; five later cases reached the shared issuer's600/minute rate limit (including an explicit429), causing setup failures/timeouts. The new44-case cohort now owns a separate, nonparallel disposable Aspire fixture; production limits and security-operation behavior remain unchanged. The HTTP race allows only the two legitimate denial stages: exact access_denied callback when validation preceded the winner, or native400 invalid_token after redemption. Explicit replay requires the latter. This correction is test isolation/response evidence, not a product policy change. Initial CI source1833ec8 passed static, unit, assurance and supply-chain jobs; provider results are superseded by the corrected-source run.


Corrected source `32d590340fc9b5924b072912428455613f5bde5e` passes the full local PostgreSQL HTTP/database suite168/168, zero skips, in4m18s. Fresh TRX confirms all44 new cases (PasswordEntry7, RequestBinding31, InteractionRace6). Browser cases were explicitly excluded locally. The final local build has zero warnings/errors; static enforcement checks passed, and CI reruns every gate for this exact source. The earlier162/168 failure checkpoint is superseded for local HTTP evidence by this run.

| T024 requirement / scenario | Permanent acceptance coverage | Cases per provider |
| --- | --- | --- |
| LOGIN-003, US2/AC1–3: current account eligibility and native lockout | `PasswordEntryTests`: six committed restrictions after form rendering; five-attempt lockout, correct-password denial and genuine expiry recovery | 7 |
| LOGIN-004/005: cached request, correlation, freshness and tabs | `RequestBindingTests`: GET/POST overlays and client binding, delayed positive max_age, valid-antiforgery cross-browser denial, authenticated tabs, replaced-session direct resume, concurrent HTTP issuance/native replay | 31 |
| LOGIN-004/005 and bounded cancellation: single winner and coherent persisted state | `InteractionRaceTests`: completion, pending/completed consumption, mixed transition, exact-session denial/recovery, independent deadline cancellation/drain; immutable bindings and full prior audit preservation | 6 |

Existing both-provider `InteractionTests` retain freshness, sequential capacity/expiry, simultaneous form completion, anonymous-tab restart, session replacement/rollback and independent-browser reuse evidence. Atomic concurrent capacity remains T030; broad fault/logging and mutation acceptance remain T026/T028.


### T024 completed CI checkpoint

[Run38073502300](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38073502300) passed all seven gates for source `32d590340fc9b5924b072912428455613f5bde5e`, tested through GitHub merge checkout `8bebc97`. All578 .NET cases passed, zero skips:216 unit/architecture plus181 per real provider (Persistence3, AppHost4, Issuer174). Each issuer cell includes168 HTTP/database and6 Chromium cases, including all44 new T024 cases. PostgreSQL issuer duration5m50s and SQL Server6m40s remain below the unchanged660s deadline. No required retry, skip or timeout increase was introduced.

All nine unchanged90% line/80% branch floors passed: Core100/100; Infrastructure99.0/100; ServiceDefaults100/100; Migrator98.1/97.8; AuthServer93.6/98.7; Admin API/BFF/Worker100/100; AppHost98.4/100. Static analysis/formatting and enforcement negative controls, assurance integrity, dependencies/licenses/secrets and strict fresh-report counts all passed. The earlier partial/failed checkpoints are historical and superseded for this corrected source.

T024 is complete. [PR #6](https://github.com/mosesparadise/OpenIdentityAdmin/pull/6), `test(auth): verify login and interaction boundaries`, contains this bounded test increment and remains unmerged for review. The final documentation-only completion commit receives its own CI run; its status is linked from the PR without a self-referential evidence-commit loop. No product or test source changed after the passing source checkpoint. Final exact-head checks remain mandatory before any future merge.

T025 is next. T025–T030 and full M01-D/manual/external acceptance remain open; T030's concurrent browser-capacity gap is not covered by the sequential-capacity result. Independent architecture/test/security reviewers closed their bounded source findings and executed no tests themselves. All345 ASVS rows remain not_assessed; no certification claim is made.

### PR #6 squash checkpoint

The chronology finding was corrected at `bd4992c88154aa2ff9c98af71a75f107245dc658`; its review thread is resolved. [Run38082495623](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38082495623) passed all seven gates on that exact head, including578 .NET cases with zero skips and all nine unchanged coverage floors. The user-authorized normal expected-head squash merge completed as `5aa330cdff0b94f5854ff2ff0bee47fe7480bd2a`, using Conventional Commit title `test(auth): verify login and interaction boundaries`. No review restriction was bypassed.

### T025 exchange-acceptance source checkpoint

The [execution plan](exchange-acceptance.md) preceded source changes. The increment adds seven real-provider cases in `Features/Protocol/ExchangeAcceptanceTests.cs`; all changes are tests, fixtures, matrix or documentation. Runtime-role Identity/OpenIddict managers own security mutations. Each code is identified from the fresh token-ID set and linked to its original subject/client/authorization/session, with unchanged authentication time and stamp and sufficient remaining lifetime. Denials compare exact post-mutation token IDs and the complete audit history. Native authorization deletion legitimately removes linked tokens; its case proves denial after that removal, not isolated execution of the Minimal endpoint's authorization check.

| T025 requirement / scenario | Permanent test method | Cases per provider |
| --- | --- | --- |
| LOGIN-005, US3 current account deletion | `Deleted_code_subject_cannot_exchange_in_owned_disposable_environment` | 1 |
| LOGIN-005, role added after code | `Committed_role_addition_invalidates_code_without_changing_security_stamp` | 1 |
| LOGIN-005/006, native authorization invalidation | `Native_authorization_revocation_or_deletion_prevents_exchange` | 2 |
| LOGIN-005/006, named code-principal isolation | `Unrelated_genuine_cookie_cannot_replace_or_rescue_the_code_subject` | 2 |
| LOGIN-006/009, lost response/replay/recovery | `Lost_successful_response_is_not_retried_and_recovery_requires_new_authorization` | 1 |

Cookie cases obtain a genuine B login through temporary test-database manifest ownership; they do not introduce a multi-user product mode. Positive exchange restores A's eligibility and verifies A's tokens despite B's genuine cookie; negative exchange keeps B eligible and proves that its cookie cannot rescue A. The actual token POST header is observed without printing it. Temporary manifest/account cleanup uses fresh bounded scopes and invalidates failed restoration. The disposable fixture retains synthetic history; cleanup does not claim to erase all B session/audit/native records.

The lost-response transport drains and disposes a real HTTP200 then throws a fixed injected exception before delivery to application code. It asserts one attempt, exact native redemption and exactly one valid access/ID entry, then explicitly tests adversarial replay and linked-entry revocation. Normal recovery uses a distinct authorization/code. This is deterministic application-delivery loss, not a claim of physical TCP loss or immediate invalidation at offline JWT validators.

Architecture/test/security source reviews passed after closing the captured-response diagnostic assertions and narrowing completion wording to the T025 gaps. Reviewers ran no tests. Local strict build passed with zero warnings/errors; static formatting/style and seeded CA5350/IDE0055/IDE1006 rejection controls passed. The strict unit matrix passed216/216, zero skips. Initial new PostgreSQL cases passed7/7 in1m21s, zero skips; final full HTTP/database regression and both-provider Chromium/coverage CI are pending. Existing real61-second expiry, concurrent redemption/replay and independent wrong issuer/audience/type/signature/nonce controls remain mandatory in the full suite. Expected matrix:181 issuer cases/provider and592 total. T025 remains unchecked at this source checkpoint; T026–T030, full pilot acceptance and external release gates remain open. All345 ASVS rows remain not_assessed.


### T025 completed CI checkpoint

[Run38083498265](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38083498265) passed all seven gates for source `fc990e712e80a97c920d9178e0a7d1ed1e9c3c59`, tested through GitHub merge checkout `83772bf`. All 592 .NET cases passed with zero skips: 216 unit/architecture plus 188 per provider (Persistence 3, AppHost 4, Issuer 181). Each issuer cell includes 175 HTTP/database and 6 Chromium cases. All seven new cases passed on each provider, together with the existing real 61-second expiry, concurrent redemption/replay and independent wrong issuer/audience/type/signature/nonce controls. Fresh local corrected-source PostgreSQL TRX independently confirms 175/175 in 5m27s, including all seven new cases; browser cases were explicitly excluded locally.

All nine unchanged 90% line/80% branch floors passed: Core100/100; Infrastructure99.0/100; ServiceDefaults100/100; Migrator98.1/97.8; AuthServer 93.6/98.7; Admin API/BFF/Worker100/100; AppHost98.4/100. Static analysis/formatting/enforcement controls, assurance integrity, dependency/license/secret checks and exact fresh-report counts passed. No required retry, skipped case or timeout increase was introduced. PostgreSQL issuer CI took 5m19s and SQL Server 10m05s under the unchanged 660s deadline. The latter leaves limited execution headroom; the T026 plan must budget its fault fixtures without dropping coverage.

Architecture/test/security source findings are closed. GitHub's automated review reported zero open findings and no blocking source defect, with its sole concern being the then-pending SQL Server check; that check is now green. No review thread is open. These completed results supersede the earlier pending T025 checkpoints above.

T025 is complete in [PR #7](https://github.com/mosesparadise/OpenIdentityAdmin/pull/7), `test(auth): verify code exchange and lost-response recovery`, which remains unmerged for review. No product or test source changed after the passing source checkpoint. This documentation-only completion commit receives its own CI; its status is linked from the PR to avoid a self-referential evidence-commit loop. Exact-head checks remain mandatory before any future merge. T026 is next; T026–T030 and full M01-D/manual/external acceptance remain open. All345 ASVS rows remain not_assessed.


### PR #7 squash checkpoint

PR [#7](https://github.com/mosesparadise/OpenIdentityAdmin/pull/7) was subsequently squash-merged as `9c52c2d` after all seven final-head checks passed and its review findings were resolved. The preceding T025 paragraphs describing that PR as unmerged are historical checkpoints, superseded by this merge record. Feature005 then completed in PR #8, squash-merged as `64d8ceb`, before T026 began.

### T026 operations-acceptance source checkpoint

The [operations plan](operations-acceptance.md) preceded implementation under the user-authorized T026–T028 sequence. The change is limited to tests, fixtures, strict counts and documentation; production policy, packages and database schemas remain unchanged.

| Requirement / boundary | Permanent acceptance evidence | Added cases |
| --- | --- | --- |
| LOGIN-008/009 diagnostic reload | `PilotRegistrationTests.Diagnostic_overrides_and_options_reload_cannot_expose_protocol_secrets`: actual configuration reload, observed options changes, reused sensitive and safe loggers, None→Information positive control | Strengthened existing case |
| LOGIN-008/009 request admission and cancellation | `PilotPipelineTests`: actual600/minute,16-concurrent, release/re-admission, operational bypass, real10-second deadline and downstream cancellation/drain through production composition | 4 Host cases |
| LOGIN-008 application audit atomicity | `OperationsAcceptanceTests.Consumption_audit_failure_preserves_pending_or_completed_application_state`: runtime audit INSERT denial, both interaction paths, exact full application/audit and native request state, independent restore/recovery | 2/provider |
| LOGIN-008/009 native consumption and logs | `Native_issuance_failure_keeps_code_redemption_and_real_process_logs_secret_free`: true form/exchange success and denial, later native token INSERT denial, persisted redemption, no issued token entries, actual child console capture | 1/provider |
| LOGIN-008/009 delayed command/client abort | `Actual_database_contention_is_bounded_and_client_cancellation_drains_native_work`: independently held real code-row lock, observed child runtime read/UPDATE, command bound or caller abort, independent DB-session drain, fresh-context state and new-flow recovery | 2/provider |

The database drain oracle covers the identified native session's executing command/open transaction before lock release. The HTTP caller is awaited separately; this does not claim instrumentation of the entire server request after a client disconnect. PostgreSQL may expose the native UPDATE timeout as400/invalid_grant because OpenIddict TryRedeemAsync catches update failures. SQL Server may block at a preceding read and produce the application's fixed503 instead. Each case observes the actual blocked stage and checks the corresponding response; neither path invents rollback guarantees. The post-redemption INSERT-failure case separately proves that redemption persists when later issuance fails.

Log canaries include actual password/rejected password, client secret/Basic credentials, anti-forgery and cookies, state/nonce/verifier/code/tokens and cached request URI, with raw/URL/HTML encodings. Capture resolves the actual Aspire project resource and uses the correlated safe incident event as a positive barrier. Evidence covers the preceding exercised paths and that event under the configured Simple console formatter (scopes precede the message), not arbitrary future events or third-party providers. Raw logs and sensitive state remain in memory; failure assertions use fixed messages.

Bootstrap creation/no-op audit rollback remains covered by T023's real both-provider regressions. Existing OperationsTests retain password/session replacement audit rollback and stopped-database readiness/liveness with the SC-005 <11-second assertion; T025 retains native lost-response/replay semantics. No prior case is removed or deadline increased. Expected full matrix is617 cases:229 unit/architecture,194 per provider (Persistence3, AppHost4, Issuer187 including6 Chromium cases).

Initial local compilation exposed two test-only API/analyzer issues, corrected before execution. Host190/190 passed with zero skips. The first targeted PostgreSQL run passed4/5 and exposed a log-helper lookup error: the logical name did not resolve the actual DCP instance. The helper now uses the IResource overload, and the corrected run passed5/5 with zero skips in51.397s. Subsequent source review added request-URI and explicit-denial canaries and strengthened cleanup; that revised run and full static/unit/both-provider CI are pending at this checkpoint. Independent reviewers found no production architecture/security regression and requested fixed-message assertions, transaction-aware drain, failure-safe cleanup, all-request concurrency completion and complete canary paths; those bounded findings are being resolved in source. T026 remains unchecked until full evidence and review are complete; T027–T030 remain open.


The revised canary run passed5/5 on PostgreSQL in47.380s, zero skips; dispatch-to-observed-database-session-drain was5029ms for the command-timeout case and5044ms for the client-abort case. These are disposable-harness measurements, not production performance benchmarks. After the final component-cleanup and import corrections, local static build/format/import verification and seeded CA5350/IDE0055/IDE1006 enforcement all passed. The strict unit matrix passed229/229, zero skips (Core25, Host190, Architecture5, AppHost9). Assurance and AI-instruction integrity plus diff-whitespace checks passed. Architecture, test and security reviewers closed every bounded source finding; they performed source review only. PR [#9](https://github.com/mosesparadise/OpenIdentityAdmin/pull/9) is open; full exact-head provider/browser/coverage CI and GitHub review remain pre-merge gates. Earlier local failure checkpoints are historical, not claimed as clean passes.


### T026 completed CI checkpoint

[Run38093331327](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38093331327) passed all seven gates for source `d6735cf317c1c3c488ba6b77d6f17171d18a9593` (GitHub merge checkout `9bf64b8`). All617 required .NET cases passed, zero skips:229 unit/architecture and194 per provider (Persistence3, AppHost4, Issuer187 including6 Chromium cases). Fresh provider TRX artifacts explicitly confirm all five OperationsAcceptance cases on each provider. No case, retry or timeout relaxation was introduced.

| Provider | Observed native blocked stage | Dispatch to observed DB-session drain: command timeout / client abort | Full issuer duration |
| --- | --- | --- | --- |
| PostgreSQL | UPDATE; native invalid_grant after failed redemption | 5032ms / 5027ms | 7m46.397s |
| SQL Server | Read; fixed503 on command failure | 5011ms / 5024ms | 10m54.846s |

These are disposable CI harness measurements. Both response/persistence paths and permission/transaction cleanup passed. SQL Server leaves only about5s under the unchanged660s issuer deadline; T027 must plan browser isolation and execution cost before adding cases, retaining every existing requirement and deadline. A passing observation is not evidence of stable capacity or production performance.

All nine unchanged90% line/80% branch floors passed: Core100/100, Infrastructure99.0/100, ServiceDefaults100/100, Migrator98.2/90.0, AuthServer95.2/98.4, Admin API/BFF/Worker100/100, AppHost98.8/100. Static build/format/import and seeded enforcement, assurance integrity, dependency/license/secret checks and strict fresh-report counts also passed.

Architecture, test, security and performance/Aspire source reviews are complete. Findings were corrected: safe diagnostics, all-request completion, independent failure-safe drain, actual resource-log lookup, complete denial/request-URI canaries and precise timing language. GitHub review's sole finding was the stale T025 unmerged statement; the explicit PR #7 squash checkpoint above fixes it. Reviewers ran no tests themselves. Bounded T026 acceptance maps LOGIN-008/009 and SC-005 to the new and retained named cases; no additional unallocated T026 work was found. T027–T030 and full-feature/manual/external acceptance remain open; all345 ASVS controls remain not_assessed.

T026 is complete in [PR #9](https://github.com/mosesparadise/OpenIdentityAdmin/pull/9), `test(auth): verify operational failure boundaries`, awaiting its authorized squash merge. This documentation-only completion/review-correction commit changes no tested code and receives its own exact-head CI before merge. That final run and merge status are tracked on the PR to avoid a self-referential evidence-commit loop. Earlier pending statements above are historical checkpoints, superseded for this source by the completed results here. T027 begins only after PR #9 is reviewed, corrected and squash-merged.

### T026 final merge and T027 start

PR #9 squash-merged as `7d1a9bc61a57d4f8f03deb4b789307bb02f7fa18` after final documentation head `2839a26` passed all seven checks in [run38094527063](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38094527063):617 tests, zero skips and all nine unchanged coverage floors. Final issuer durations were PostgreSQL7m40.311s and SQL Server10m27.662s; planning retains the slower654.846s source observation. The preceding pending merge statements are historical. T027 now follows its [browser acceptance plan](browser-acceptance.md); T028–T030 and full-feature/manual/external gates remain open.

### T027 initial source checkpoint

The browser plan preceded implementation and received independent architecture, test, security and UX/Aspire design reviews. The source retains181 HTTP issuer cases/provider and expands the browser partition from6 to17 cases/provider/engine: the original TLS/keyboard/four theme layouts, four theme behavior modes, two freshness modes, two terminal layouts, credential-error recovery, no-JavaScript completion, and a safe-diagnostic/engine-selection regression. Genuine issuer/RP HTTPS, form protection, code exchange and public-key validation remain active.

Local build/static analysis and229 unit cases passed with zero skips. Actual native executable JSON discovery proves181 HTTP +17 browser cases/provider with disjoint exhaustive identities; the SDK discovery transport did not forward JSON and returned zero, so discovery explicitly invokes the native test executable. Execution remains native MTP. Six selector/partition regression cases initially passed, with a seventh added to protect NUL-delimited Git paths. One parallel MSBuild invocation terminated child nodes before compilation; the subsequent single-node build passed without warnings/errors. No browser result is inferred from local checks: managed-network browser downloads remain blocked.

Review corrections include both Playwright and timeout diagnostic sanitization, safe negative-control assertions, private Firefox profiles, credential-empty capture guards, actual callback-navigation counts, complete keyboard traversal, constrained theme storage values, exact matrix pairs, NUL path parsing and complete dependency triggers. Updated run instructions include the isolated browser profiles. CI must still prove all six cells, new screenshots require individual review, and combined coverage remains pending. T027 stays unchecked. Missing new baselines deliberately fail the first capture run; they are never automatically accepted.
