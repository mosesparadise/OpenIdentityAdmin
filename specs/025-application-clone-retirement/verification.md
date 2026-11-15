# Feature025 verification

Status: clone accepted for internal development and squash-merged in PR138; retirement implementation in progress. See clone-acceptance.md for exact evidence and limits. This does not complete feature025 or authorize release.

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

### PR 138 first CI feedback and corrections

The final ordinary local unit verifier completed successfully: Core 94, Host 2,088,
Architecture 5 and AppHost 118; no failed or skipped required cases. The strengthened
source-isolation case also passed on both providers (2/2, 1m45s).

CI 608 (`43655695701`, head `c30cb4e`) exposed stale independent selection totals in
the shared pre-test checks. The first failure was standard partition 356 versus 359;
the new cases account for +1 persistence and +2 AppHost cases per provider. Further
inspection found old administrator and combined totals in five selection-control files.
Independent review confirms all 2,977 previous administrator and 80 persistence identities
and metadata are unchanged; the additions are 48 and 2 respectively. The corrected totals
are standard 359, administrator 1,384/1,489, combined 1,786/1,893, and full administrator
catalogue 3,025. Existing group obligations and fail-closed controls remain intact.
The first local run of all 28 workflow Python control scripts caught the remaining issuer
partition assertions; after their correction its nine controls pass, and the other 27
scripts had passed. These failures occurred before the affected native/browser tests and
must not be interpreted as executed test results.

CI's exact static gate also rejected three unused imports; these were removed. An
independent browser review caught a contradictory test helper requiring heading focus
after Cancel while the next assertion correctly required restored Clone-button focus.
Cancel alone now checks visible details and the initiating button; entry, success,
post-hide and reloaded details retain heading-focus assertions. A local build rejected
the initial correction's missing braces; the correction now follows the enforced style.
The reviewer confirms the final focus semantics and finds no further concrete UI/assertion
contradiction in the inspected journey. The exact static gate and new hosted CI remain
required; no merge or full acceptance is claimed for CI 608.

Corrected exact local static gate (`scripts/check-static-analysis.py --self-test`) passes:
zero-warning/error build, whitespace and IDE0005 verification, code-quality checks with
unchanged legacy baseline, and all three seeded analyzer/style negative controls. The
cancel-focus correction is independently source-reviewed. Full hosted acceptance remains open.

### Source-time regression added during final review

The coverage reviewer identified an unexercised clone-specific denial when the persisted
source observation is ahead of the current clock. A new real-provider case sets that
watermark one second ahead while identity/MFA and requested credential expiry stay current.
It requires status 400, no credential/projection/version, no destination or clone provenance,
unchanged full source/policy/audit snapshots, and continued validity of the source secret.
Both provider cases pass (2/2, no skips, 1m30s), and independent source review confirms the
intended branch. Build12 has no warnings/errors, formatting and quality checks pass.

Native discovery now retains 3,027 administrator identities, adding exactly these two
cases to the previously reviewed inventory. The clone suite now contains 50 cases;
Applications has 140 cases per provider, administrator ordinary totals are 1,385/1,490,
and complete provider totals 1,787/1,894. All 28 workflow Python control scripts pass
against the corrected inventories. CI 609 (`43656525088`, head `7c57b05`) began before
this addition and cannot establish acceptance of the new head. Full exact-head hosted
browser/coverage/mutation and final artifact reviews remain required.

### CI 610 evidence review and test corrections

CI 609's SQL persistence job completed 40/41 cases; the remaining existing catalogue-runtime
case could not start its SQL Server container (`CoInitializeSecurity`, 0x800706b5, exit 255).
Its original log is retained. The run was superseded by the clock-regression source update;
no retry or deadline relaxation converts that failed case into acceptance.

CI 610 (`43657036888`, head `7c080a9`, merge `5cab4e`, tree
`81d6172f113b91ad56560984ca239b6db369c340`) passed all four sample browser cells.
Independent review reconciled original archive hashes/sizes/CRC, source/build/report bindings,
and exact TRX identities/execution links: four retained ordinary journeys and four clone
journeys, eight passes, no skips. This is preliminary browser evidence, not full acceptance.

Its Host project exceeded the unchanged 180-second covered invocation limit. The prior
accepted CI 603 Host TRX took 176.492s. Diagnostic reconstruction of CI 610's incomplete
native stream found all 12 clone cases (0.056s combined), with the parallel phase faster
than CI 603; five existing exclusive sample/telemetry cases remained without final results.
The partial stream is not a passing Host report. The reviewed correction bounds Host-only
conservative scheduling at eight collections and retains both exclusive collections.
MaintenanceTests now joins the existing telemetry isolation because its process-global
listener otherwise allowed unrelated measurements to satisfy its completion source.
No test identity/assertion, request timer, invocation bound or coverage policy was removed.

Mutation review found that the new source-verification witness could emit its bypass marker
for an unrelated exception. It now emits that marker only after a successful 201 result and
independent persisted proof of the changed source policy, unchanged other source fields,
distinct destination with valid new credential, and exact linked audit/provenance. The
interceptor marks itself fired only after exactly one persisted row changes. This stricter
check exposed two previously vacuous catalogue-fault cases: they queried the unmapped
CatalogueScope projection. The corrected query targets mapped ScopePolicy by ScopeId.
Atomicity13 recorded 10 passes/2 failures; corrected Atomicity14 passes all 12 cases on both
providers (1m52s), with actual catalogue corruption and rollback. Both corrections pass
independent source review; previous affected claims are not accepted as causal evidence.

Build14 and quality checks pass with zero build warnings/errors and unchanged legacy debt.
The local covered Host run completes all 2,088 cases in 1m03s, inside the original 180s bound.
A private post-validator initially rejected 11 theory display-name collisions (ten historical
and one new clone JSON display truncated by the native formatter);
correct validation of the same original report confirms 2,088 unique native IDs and exact
result/definition/execution/entry links, all passing, no skipped cases or run-level errors.
The original command's post-validation failure is retained; no rerun was used for this
analysis correction. Fresh exact-head CI remains mandatory for the scheduling/fault changes.
Independent review of that original Host report confirms peak concurrency eight, preserved
logical bindings for all 2,076 prior cases plus 12 clone cases, and zero overlap between
the 15 sample-exclusive / nine telemetry-exclusive cases and any other case.
The exact local static gate with self-tests also passes after these corrections, including
unused-import/format verification and all seeded enforcement checks.

### CI611 final clone acceptance and normal squash

All 141 jobs pass at head `2a8f4d2`, tested merge `8198056`, tree `3c8c42e`.
Independent source, native, browser, contracts/UI/supply, coverage, mutation and holistic
reviews pass after reconciliation of all 135 original artifacts. Native evidence contains
117 ordinary reports / 6,466 passes; coverage uses all 114 inputs and 17 unchanged floors;
mutation evidence contains 147 kills with passing baseline/restored controls. The original
local source-verification calibration additionally passes baseline 2/2, one demonstrated
kill per provider and restored 2/2, with clean restored isolated source.

PR138 was normally squash-merged on 15 November as
`0366f5f1ebdca7797edd6dfd6954715b0545dd63`; its tree exactly matches the tested source.
[Clone acceptance](clone-acceptance.md) records detailed counts, receipt hashes and limits.
T013/T015 are complete. Retirement T016–T029 remains separate and unfinished.

### Retirement implementation start

Worktree branch `149-application-retirement` starts at the accepted clone squash.
Pinned Spec Kit prerequisites resolve to feature025 using explicit feature-directory
state; the first name-only resolution failed without modifying source and was corrected.
The requirements-quality checklist remains 10/10; no extension hooks exist. Existing
approval authorizes this increment. Independent preparation confirms terminal details
retain their application ETag and exact linked grant-job recovery; ordinary Submit on a
retired target conflicts, while exact approved continuation remains possible.

Retirement baseline restore/build passes with zero warnings/errors. The first real HTTPS
retirement test executes both providers and fails specifically at expected 202 versus
actual 404 (2 failed, no skips/errors/timeouts, native exit 2, 1m30s). This is the intended
behavioral red before retirement implementation, not a setup failure or passing test.


### Retirement implementation checkpoints (15 November 2026, not acceptance)

The selected retirement behavior remains irreversible, with history and client identity retained.
The working branch now stages terminal policy, a separate immutable retirement receipt and a
new durable Submit approval/job inside one owned transaction. It requires both lifecycle and
grant permissions under one fresh-MFA receipt. Normal lists filter terminal policies before
paging, while `/retired-applications` and terminal details expose the exact linked recovery job.
The UI confirms permanence, retained history/client ID, offline-token and remote-session limits;
retired recovery uses the receipt's job identity and cannot submit another ordinary job.

Executed locally on the pinned environment:
- Build 4 failed on nullable/format diagnostics in new BFF code; corrected before build 5.
- Builds 5 and 6 passed with zero warnings/errors (76.27s and 55.35s).
- Genuine HTTPS API/BFF initial retirement and exact replay: **2/2 passed**, one per provider,
  zero skipped (`retirement-tests/green1/retirement.trx`, 93.660s). This follows the recorded 404 red test.
- Admission/current dual authority/MFA/rollback/conflicting replay/terminal mutation denial:
  **20/20 passed**, both providers, zero skipped (`retirement-tests/admission1/retirement.trx`, 123.938s).
- Contract core: **44/44 passed**, zero skipped (`retirement-tests/contracts1/contracts.trx`, 2.307s).
  Native production export remains deterministic with zero management operations/network effects;
  the separate browser contract and generated types include the two implemented retirement paths.
- Editor behavior initially passed all assertions but missed the unchanged 100% function coverage
  floor because opaque-ID percent encoding was unexercised. Added a historical punctuation case;
  the second run passed. New retirement validator measured 100% lines/branches/functions.
- Expanded grant UI behavior passed unchanged floors; all six UI suites then passed together.
- UI typecheck passed; asset drift check correctly failed before rebuilding changed assets.
- Code-quality check initially reported new unnamed literals; named feature/test constants fixed
  those without changing allowances. The second check passed all controls and source rules.

Independent ownership review found two defensive persisted-corruption gaps: Continue replay
could follow a substituted older approval job ID, and retired effective-approval binding could
accept a later approval relabeled Submit. Exact replay job matching and original-Submit versus
subsequent-Continue/audit checks now reject those cases. Dedicated fault tests and re-review
remain pending; no ordinary-request exploit was established by that read-only review.

Remaining work includes populated-upgrade witnesses, corruption/atomicity and additional race
cases, genuine browser reload/re-login/lost-response recovery, exact inventories/mutations,
full exact-head checks and independent original-artifact acceptance. These checkpoints do not
complete T016–T029 or authorize an acceptance claim.

### Retirement focused verification expansion — 15 November 2026

These are local checkpoints, not exact-head acceptance. Retirement remains unmerged.

- Build7 passed with zero warnings/errors (54.94s). Recovery1: **10 passed, zero skipped**
  (115.919s), including real hosted Worker restart, current-permission stop/explicit Continue,
  immutable original receipt and genuine database-observed independent admission races.
- Issuer1: **8 passed, zero skipped** (132.166s), PostgreSQL/SQL Server, covering both
  observed retirement/authorization-code and token-issuance commit orders.
- Build8 passed (53.62s). Boundary1: **9 passed, 1 failed, zero skipped** (114.913s).
  The four persisted binding-substitution and four final-save corruption cases passed.
  SQL Server list failed with 503: the new policy/native ID join needed explicit ordinal
  collation. PostgreSQL list passed. The comparison now uses the selected provider's
  ordinal collation before filtering/order/offset/lookahead; subsequent browser1 selection
  passed **both list cases**.
- Immediate populated upgrades initially passed **2/2** (32.414s). Independent review
  strengthened the physical schema oracle with index enable/filter/PK checks, ordinal
  SQL text collation and an exact receipt-check inventory plus seeded negative controls.
  Upgrade2 then passed 11 and failed five SQL cases because the new test assumed an
  incorrect sys.indexes wildcard offset. An isolated pinned SQL Server metadata query
  demonstrated its undocumented is_ignored_in_optimization column. Corrected offsets
  are has_filter=19 and filter_definition=20 in the existing full projection.
  Upgrade3: **16 passed, zero skipped** (73.959s), retaining every affected older
  populated upgrade and both immediate retirement predecessors.
- Build9 passed (51.87s). Browser1 selected two PostgreSQL/Chromium retirement journeys
  plus both list cases: **2 passed, 2 failed**, zero skipped (125.695s). Both browser
  failures occurred at BrowserSession.CreateAsync before any retirement browser flow:
  pinned binaries are absent locally. The installer could not acquire root for native
  dependencies, and a separate binary-only install received HTTP403 Domain forbidden
  from the official download host. No browser pass or TLS bypass is claimed. Required
  retained-browser execution remains pending hosted CI.
- Build10 failed only on new test use of Assert.Empty(StringValues); changed to exact
  header Count. Build11 passed with zero warnings/errors (8.61s). Unit1: **35 passed,
  zero skipped** (1.733s), strict retirement API/BFF request and single-send response
  bindings, original/successor version separation, evolving-job replay and hostile framing.
- UI second check verified TypeScript and six deterministic assets; all six UI suites
  passed after the validated retirement-reference copy fix. Existing floors are unchanged.
- New source-mutation races, retained scope/clone-history, physical terminal/concurrency
  tests, fresh-login replay-window tests, replacement-approver tests and seven separate
  retirement causal mutation witnesses are under validation. Build11's first reference
  test compared a snapshot containing all application audits; the new retirement audit
  correctly changed that aggregate. The test now checks the clone's exact native/policy
  state and provenance separately and clears the clone's scope assignment so the retired
  source is the sole reference blocking catalogue deletion. That correction is pending rerun.
- Independent ownership follow-up found both original replay/effective-approval findings
  resolved and no new source blocker. Its review of seven proposed causal witnesses
  requires successful operations plus fresh persisted violation evidence; actual baseline,
  mutant and restored executions remain pending. No generic exception is an accepted kill.

Original logs/TRXs and scoped review receipts remain under task work storage. Full native
inventories, final coverage, browser cells, mutation execution, original hosted artifacts,
final independent review and normal squash are still required. No existing identity,
assertion, coverage floor, deadline, legacy allowance or external/manual gate is relaxed.

### Retirement reviewable implementation checkpoint

- Build12 passed (40.58s), then Build13 passed (49.56s), both zero warnings/errors.
- Race1 completed **20/22 passing**, zero skipped (174.362s): all sixteen actual
  retirement-versus-edit/clone/replacement/scope commit-order cases and four physical
  terminal/concurrency cases passed. The two reference snapshot errors described above
  were corrected. Subsequent baseline1 independently passed **11/12 per provider**
  (PostgreSQL88.153s, SQL Server111.892s): all seven retirement mutation witness baselines,
  both physical constraint/concurrency tests, fresh native-login replay deadline and actual
  replacement-approver/current-authority tests passed. The remaining reference test expected
  catalogue deletion admission202, but its retained native reference correctly causes409
  before a job is admitted. It now requires409, null job, no deletion row and unchanged
  complete retirement state; rerun is tracked separately.
- Host applications2: **194 passed, zero skipped** (1.974s), preserving all older application
  cases alongside35 retirement request/transport tests. All2,088 previous Host discovery
  records retain their exact portable type/traits and display-name multiplicity;35 were added.
- Portable Administrator inventory preserves all3,027 previous identities and metadata,
  adding80:34 ordinary application cases and4 managed issuer cases per provider, plus2
  browser cases per provider. Non-browser totals1,423 PostgreSQL/1,528 SQL Server;
  Applications174/provider, ManagedApplications149/provider, browser78/provider.
  Persistence preserves82 old identities plus2 upgrades (42/provider).
- All346 Python tooling controls pass after exact new count/job obligations were added.
  The separate seven-decision retirement mutation job is mandatory for both providers in
  the final acceptance dependency set and source-scope gate; old jobs/deadlines remain.
- Pinned oasdiff1.33.0 archive was reused and SHA256-verified by the unmodified installer.
  Allfive documents are compatible against0366f5f1ebdca7797edd6dfd6954715b0545dd63.
  The initial Node contract run caught the old30-operation count; updated to32 with the
  exact two added paths, preserving all old assertions. Added a dual-authority/header-drift
  control; all47 Node cases now pass with zero skips. TypeScript generation check passed.
- Assurance register check passes its345 controls, all still not_assessed; this is snapshot
  integrity, not a compliance assessment. Current source-quality check passes without new
  allowances. Independent upgrade final review corroborates16 original upgrade3 TRX cases
  and all corrected physical-oracle controls. Ownership follow-up passes source findings.

T016–T021 andT024–T025 record implemented and component-verified work. Browser-dependent
T022/T026, remaining reference proofT023 and final mutation/evidence/mergeT027–T029 stay open.

Reference3 now passes **2/2**, zero skipped (93.577s), on both providers. The retired
source is the sole remaining native scope reference, deletion admission returns409 without
a new job/audit, and clone native/policy/provenance remains unchanged. T023 is complete.
API/BFF/contract source review passes with no confirmed blocker; full runtime/export/browser
and exact-head acceptance remain separate obligations.

### Retirement PR139 review and convergence

PR139 opened at `e61c0f280a11b103fd0c2a799cd506a0fc060f8e`; CI614
(`43668901781`, attempt1) is retained as the initial implementation run, not accepted
delivery evidence. Its source secret scan flagged the parser test's public, synthetic
idempotency UUID because its constant was named `Key`. Renaming it `RequestIdentifier`
preserves exactly the same UUID, requests and assertions. The pinned scanner then found
zero leaks against the corrected source snapshot; no suppression or scanner change was added.

The independent current-source convergence review checked14 functional requirements,
5 success criteria,11 acceptance scenarios and all29 original tasks. It found two medium
verification gaps and no confirmed runtime defect: direct current-authority UserInfo denial
after retirement, and actual retirement-versus-enable commit orders. The installed
convergence prerequisites passed with no extension hooks. T030/T031 were appended without
rewriting the existing tasks; source review accepts the resulting test additions.

Convergence1 now passes **22/22**, zero skipped (164.262s): all20 observed
retirement-versus-mutation cases, including both enable orders/provider, plus a genuine
pre-retirement UserInfo positive control followed by retirement, cryptographic validity of
the already-issued token, UserInfo and new-authorization denial before cleanup, and unchanged
authority/native state on each provider. The enable-first preserved-native/history assertions
were then strengthened and have a separately recorded rerun. Host retirement3 passes35/35
(1.745s). All346 tooling controls pass again (28.000s), and source quality passes without
new allowances. New native discovery preserves all3,107 prior current-branch identities and
metadata, adding six cases:3,113 total, ordinary1,426 PostgreSQL/1,531 SQL Server;
Applications176/provider, ManagedApplications150/provider, Issuance group71/provider.

Build15 passed with zero warnings/errors (18.84s). The final strengthened enable assertions,
hosted browsers, original mutation receipts, coverage and exact-head acceptance remain
separate obligations; these local results do not close T022/T026–T029.

Build16 passed (24.91s), and full whitespace/style verification and the staged-source secret
scan pass. An attempted `FullyQualifiedName` argument filter selected zero cases (exit8),
so it provides no pass evidence; the complete20-case mutation-race class was selected for
the strengthened assertion rerun instead. Independent local PostgreSQL causal calibration
at `e61c0f2` passed all7 baselines (46.449s),7 demonstrated behavioral kills and7 restored
controls (81.392s), zero skips, with original source hashes restored. SQL Server and final
hosted exact-head mutation evidence remain required.

Enable3 passes **20/20**, zero skipped (173.188s), including both strengthened enable
orders on both providers. T030/T031 are complete at the component boundary; full hosted
verification remains pending.

### CI615 deadline findings and bounded Applications groups

CI615 run `43669851039`, attempt 1, tests head `e5af2662e3ff51868c6db257e657a8530dab1653`
through merge `045d1814c03fc8c698c0894d2805322fa37a3079`, tree
`a1d706b7c7c70fb0f3e9f01126ff0753ee926e83`, against base `0366f5f1ebdca7797edd6dfd6954715b0545dd63`.
It is **not accepted**: Host exceeded its unchanged 180-second bound, and the expanded SQL
Applications story exceeded its unchanged 660-second bound. Preserve original partial
streams, coverage, logs and authenticated archive metadata; they are diagnostic evidence,
not complete passing native reports. Remaining cells are recorded separately.

Independent Host analysis found 2,110 completion records, all with the passing outcome byte,
but no final Host TRX. All 35 new cases completed in 0.225s combined. Existing parallel work
took 142.764s versus 97.952s in CI611; 13 old exclusive cases lacked completion records.
The observations establish budget exhaustion, not a specific resource cause or stalled new
test. One local current-source comparison ran the unmodified full unit verifier under the
same coverage configuration/deadlines: 94 Core, 2,123 Host, 5 Architecture and 118 AppHost passed.
Host's original TRX spans 77.027479s, peak concurrency 8, with no overlaps among 15 sample and 9
telemetry exclusive cases. This local result does not repair CI615, establish root cause,
attest immutable CI binaries or prove stability. No Host deadline, concurrency or isolation
setting was changed, and no historical diagnostic approval was renewed.

Applications batch growth has separate direct evidence. CI611's SQL 140-case story took
590.232s, leaving 69.768s under 660. CI615's partial SQL stream records all 36 new retirement
completions totaling 121.421s, plus 132 old completions totaling 533.383s; eight old cases remain
uncompleted. Observed execution is serialized. PostgreSQL's complete 176-case report passes
in 347.376s (old 140 recorded sum 278.479s; retirement 36 sum 61.740s). The extra bounded work is
material against the old SQL margin, so T032 records a verification correction.

The existing strict administrator-group mechanism now declares Registration 34 classes / 140
cases and Retirement 12 classes / 36 cases per provider. Four mandatory workflow cells replace
the two enlarged cells. Both default and explicit story execution require every group;
selected execution still discovers and validates all group identities, metadata, counts and
their disjoint union. Each invocation retains 660 seconds. Coverage requires 116 originals
instead of 114; 17 module floors/exclusions, all assertions, inventories and browser obligations
remain unchanged. Expected ordinary reports increase 117→119; total native executions remain
6,593. Source-derived workflow totals become 145 jobs / 139 artifacts, subject to actual evidence.

All 352 Python controls pass (21.824s), including six new seeded omission/overlap/substitution/
provider/UID/tuple/redistribution/CLI/workflow controls. Actual native discovery independently
confirms all 3,113 identities and each 140+36=176 provider union. Independent source/discovery
review passes without a new deadline or verification exception. Fresh complete exact-head
CI and original artifact reconciliation are still required.

CI615's scoped contract/UI/supply review passes: 149 UI, 47 Node, 44 native contract and 7 cases
per retained contract browser; five matching exports, six zero-socket receipts, 32 browser
operations, unchanged prior security metadata and all UI floors. The pinned source scanner
reports zero leaks without added exceptions. Both hosted retirement mutation jobs pass their
seven baseline/kill/restored sequences. Local e61c0f2 calibration separately passes SQL Server
baseline 7/7 (86.229s), 7 causal kills and restored 7/7 (78.552s); both local providers restore
original source hashes. Normal harness cleanup removed temporary raw mutant TRXs; retained
sanitized receipts are producer-validated evidence, not independently replayable raw reports.
None of these scoped passes closes the two failed CI615 jobs or the final delivery gate.

### CI615 final browser findings and verification corrections

The completed run has 133 successful jobs, eight failed execution jobs, a failed acceptance
gate and skipped coverage aggregation. All 136 available original archives have authenticated
size/SHA256 and ZIP-integrity reconciliation. CI615 remains failed; it has no complete merged
coverage or passing full-run acceptance evidence.

Two retained browser assertions ran without waiting for their required response phase. The
security-focus check now waits for DOMContentLoaded after the new error becomes visible, which
includes the page's deferred login script. The fresh replacement-login audit check now waits
for the new factor form before reading the unchanged exact password/factor counts. The shared
password helper remains a submission helper; only its documentation changes. Independent
source review accepts these barriers without retries or deadline changes. The old failures
do not independently prove a historical timing cause; fresh hosted verification is required.

The three complete Applications browser reports each contain 21 retained passes and two new
retirement failures: expected 10 native rows, actual 15. SQL/WebKit has both matching failures
in its authenticated log and 23 partial-stream completion records, but no final TRX. The new
journeys deliberately perform a real administrator logout/relogin; the old shared grant oracle
incorrectly expected no new native issuance anywhere. T034 keeps that shared final oracle
unchanged. Retirement-specific phase snapshots require exact preservation through logout and
sign-in, and admit exactly five new rows only for the persisted administrator registration:
one valid authorization, valid access/identity tokens and redeemed request/code tokens. Only
those validated rows are appended to the original pre-retirement baseline. The final oracle
still requires exact cardinality, original identities/bindings, target revocations, untouched
peer statuses and no extra target rows. Hosted execution must validate these expectations.

The SQL/WebKit deadline has separate measured workload evidence: all 23 partial completion
records span 653.242s of case time (old21 532.869s, new2 120.373s), before process overhead and
final reporting; the unchanged 660s bound expired after the last completion. CI611's old21
wall time was 538.127s. SQL/Chromium's expanded complete report took619.102s. T035 therefore
retains Sessions15 and Applications21 and adds mandatory Retirement2 cells for every active
provider/engine. This grouping does not fix the assertion failures. With both grouping changes,
the source-derived obligations are 149 jobs, 143 artifacts, 123 ordinary native reports and
120 coverage inputs; native executions remain6,593 and all17 coverage floors remain unchanged.

Build17 passes with zero warnings/errors (83.56s). T032–T035 and final acceptance remain pending
fresh exact-head execution and independent original-artifact reconciliation.

The correction snapshot passes Build18 with zero warnings/errors (46.80s), all352 tooling
controls (34.436s), whitespace/style verification, source quality and the pinned staged-source
secret scan without exceptions. Actual native discovery preserves all3,113 identities and
independently verifies each15+21+2 browser union. Grouping review accepts all48 mandatory browser
cells and120 coverage obligations. The retirement oracle source review accepts preserved
original target/peer checks and explicit administrator-only additions, including ordinal client
identity after database lookup. It proves row population, not an additional token-family linkage
claim. These local/source checks do not replace corrected hosted browser or full-run evidence.
