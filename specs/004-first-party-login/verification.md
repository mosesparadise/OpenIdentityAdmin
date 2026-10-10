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
