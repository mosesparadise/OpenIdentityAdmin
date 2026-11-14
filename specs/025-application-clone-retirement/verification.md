# Feature025 verification

Status: clone implementation and verification in progress; retirement implementation pending. This is not acceptance or a release claim.

## Environment and baseline
14 November 2026, isolated branch 148-application-clone-delete at base
23a47a69cdac22edc2f87770d8175ef1429f7119. Installed official .NET SDK 10.0.404 in task
workspace. Docker server 28.4.0 is available. Baseline preceded the focused results recorded below.

- dotnet restore --locked-mode: exit 0. NuGet reported transient 503 downloads and recovered
  during the same restore; no package/version or lock-file change.
- dotnet build --no-restore --nologo: exit 0, zero warnings/errors, 1m27s.
- Existing .gitignore covers .NET output, node_modules, secrets, scratch and test artifacts.
- Spec Kit path/setup-plan/setup-tasks/prerequisite scripts succeeded with explicit feature
  directory; pinned integration files unchanged. No extension hooks are installed.

Implementation, focused tests, migrations, browser/contract checks, full exact-head CI,
coverage/mutations, original evidence reconciliation and implementation reviews remain open.
All Windows/manual, Firefox/A-009, historical findings and external release gates stay open.

## Clone implementation checkpoint — 14 November 2026

Clone runtime/API/BFF/React and additive provider migrations are implemented locally;
retirement remains design-only. Neither increment is accepted or merged yet.

- Locked tool restore and npm installs used existing pinned dependencies; no updates.
- Initial build errors (missing namespace, formatting and xUnit analyzer diagnostics)
  were corrected. The full build4 completed with zero warnings/errors. Later tests and
  browser/upgrade coverage are still being compiled; that earlier build is not final-head evidence.
- The first focused provider attempt overlapped a build briefly and failed startup.
  A separate creation control confirmed HTTPS worked while readiness returned 503.
  Diagnosis: generated migrations were missing from the providers' exact migration lists.
  The lists were corrected without changing TLS, readiness rules or deadlines. Both
  investigative runs were explicitly cancelled and are not acceptance evidence.
- Fresh sequential build4 then `dotnet test --project
  tests/aspire/IdentityPlatform.Administrator.Tests --no-build --no-restore --filter
  'FullyQualifiedName~ApplicationClone' --report-trx`: **16 passed, 0 failed, 0 skipped**,
  PostgreSQL and SQL Server, 1m54s. Includes nonempty source grants/scopes and ten
  real save/audit rollback cases. Local TRX retained at work/clone-tests/provider4.
- UI `npm run build`, `npm run check`, `npm test`: **139 passed, 0 failed, 0 skipped**
  across shell 11, list 9, editor 49, grants 21, catalogue 28, scopes 21.
  TypeScript and six deterministic assets verified. Existing per-module coverage
  floors unchanged; editor controller 97.66% lines / 97.63% branches / 100% functions.
- The initial contract run found an outdated expected path count (15 versus 16).
  Counter corrected; final contract rerun remains pending. Production dormant-host
  exports and browser contract generation completed without opening hosted listeners.
- Source review found no clone runtime blocker. Test review requested nonempty source
  evidence, persisted corruption controls, true HTTP boundaries and genuine native
  sign-in; these are being implemented and verified. Full exact-head CI, migration
  compatibility, browser matrix, causal mutations and final independent reconciliation
  remain pending. No deadline, assertion, inventory baseline or coverage floor was relaxed.

### Expanded checks and review corrections

- Full build6 passed, then focused execution exposed test-only issues: the host parser
  fixture omitted logging/problem services; clone-first expected the source display
  name despite explicitly requesting a new one; the invalid browser-CSRF test used the
  helper's direct-API default; HTTP expiry expected fractional seconds omitted from its
  UTC-second request. All were corrected without changing production behavior. The
  expanded provider6 result was 40 passed / 6 failed / 0 skipped; retained as a failed run.
- Upgrade6: 22 passed / 2 failed / 0 skipped. The two frozen-implementation compatibility
  cells lacked required producer environment variables. Prepared the pinned immutable
  38eb41e producer with checksum manifest. Preparing it changes its restore context;
  a later ordinary no-restore solution build emitted six baseline-project reference
  warnings. Re-running the normal locked restore then build8 cleared those warnings:
  zero warnings/errors, 42.82 seconds. No dependency lock was changed.
- C# non-browser contracts: 44 passed / 0 failed / 0 skipped. Host clone rerun pending.
- Node contracts: 46 passed / 0 failed / 0 skipped after installing the pinned oasdiff
  archive with SHA256 verification. An initial attempt without that executable is a
  setup failure, not a contract pass. Independent operation inventory is now 30.
- Independent contract review found clone POST lacked a mandatory source precondition
  in the JavaScript inventory checker. A targeted negative test first failed with
  “Missing expected exception”; explicit versioned-clone recognition fixes it. The
  existing generated-contract test now rejects missing/optional If-Match, weakened
  authentication/CSRF and binds current clone permission/freshness/201 response.
- Independent migration/race review required exact PK/FK child keys plus negative
  key-removal/substitution controls, and fresh persisted absence checks after denied
  races. Corrections are source-review accepted; runtime rerun remains pending.
- All 2,977 existing administrator native identities/metadata remain unchanged, with
  48 new clone cases. All 80 existing persistence identities remain, with two populated
  upgrade cases added. Provider ordinary totals: administrator 1,384 PostgreSQL / 1,489
  SQL Server; Applications 139 each; Persistence 41 each; AppHost 25 each including
  four new real clone browser cells. Host adds 12 cases (2,088 total). Native discovery
  supplied every new identity; no path-dependent UID was invented or pinned.
- Application mutation inventory grows from eight to ten with disabled-by-default and
  post-save source verification witnesses. Existing lifecycle reason mutation follows
  the cohesive audit-helper extraction; its original witness and meaning are retained.
  Mutation harness controls: 64 passed. Real mutation executions remain pending.
- API/BFF/contract/UI and migration/race source reviews pass after corrections. Browser
  review confirms the actual end-to-end flow; added error accessibility/success focus
  and cross-client native secret rejection. Used-source grant isolation is attributed
  to the component/provider cases, not to the browser's newly created source. Human
  accessibility assessment remains open.

### Corrected focused results

- Build8 after normal locked restore: pass, zero warnings/errors (42.82s).
- Host clone: **12 passed, 0 failed, 0 skipped** (1.95s).
- Populated upgrade chain: **24 passed, 0 failed, 0 skipped** (1m24s), both real
  providers, including immediate-predecessor clone additions, calibrated PK/FK controls,
  prior migrations and immutable old-binary compatibility with the original manifest.
- Selection controls: administrator 30, persistence 6, AppHost 8; coverage identity 36;
  contract evidence 4; contract supply controls 4, all passing. Assurance inventory remains
  all 345 controls not_assessed; this change makes no compliance claim.
- Provider8's 48 clone cases, final browser refinements, exact-head CI/coverage/mutations
  and final acceptance are still pending at this checkpoint.

Provider8 completed: **48 passed, 0 failed, 0 skipped** (3m41s), covering both engines,
nonempty source grant/scope isolation, exact provenance/credential dates, protected and
unsupported sources, current permissions/MFA/session expiry, actual routed HTTPS/CSRF,
strict preconditions/input, duplicate destination, both source-edit commit orderings,
pre-snapshot catalogue eligibility and post-save source/catalogue/audit corruption.
Local original TRX is retained at work/clone-tests/provider8/clone-provider.trx.
A subsequent success-case assertion additionally requires exactly one new audit row;
that narrow strengthening and final browser refinements await final build/CI.

Final local source build9 passes with zero warnings/errors (52.56s), including the browser
focus/error/foreign-secret refinements. Generated TypeScript regeneration/check has no drift;
76 locked npm entries and exact renderer archive supply checks pass. Staged gitleaks scan
finds no secrets. Full ordinary unit verification and the strengthened two-provider source
isolation case are running; required hosted browser/coverage/mutation acceptance remains open.
