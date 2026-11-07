# Grant revocation implementation verification

## Accepted source — T018, 7 November 2026

Feature021 T001–T018 are complete for internal development at `2343f2bb10740444eb55d231f6861879dd033495`.
[CI545](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42319410601) passes
all 124 jobs on its first attempt. The tested merge and reviewed source share the exact tree recorded in the receipts.
[Exact receipts](t018-evidence.json) and [requirement bindings](t018-requirements.json)
record the accepted result. Final-head checks and normal reviewed squash are
recorded on [PR124](https://github.com/mosesparadise/OpenIdentityAdmin/pull/124); it remains
draft until those pass. This section supersedes pending implementation/evidence wording below.

| Required scope | Verified result |
| --- | --- |
| Source/jobs/artifacts | Exact 124 jobs, 118 artifact names/digests and 124 timestamped checkout log bindings; no job failures/skips/reruns. |
| Native cases | 103 reports, 5,753 passes, zero required failures/skips; exact source-derived name multiplicities, assembly/class/method and unique result/definition/execution/entry bindings. Host has 10 truncated-display collisions; discovery UID-to-TRX-ID equality is explicitly not attested. |
| Grant scope | 93 cases on each real provider, including actor/application/action retained-key isolation and every earlier boundary/race/recovery obligation. All 2,663 prior administrator identities/metadata/order retained; 2,669 total. |
| Browser/UI/contracts | Both-provider Chromium native Aspire demos; all retained Chromium/WebKit matrices, 136 BrowserProtection passes and the corrected SQL Server WebKit logout case; 83 UI and 44 Node contract cases. |
| Mutation controls | 18 suite executions/110 demonstrated behavioral kills, passing baseline/restored controls and exact original/mutant/restored source hashes; 14 anti-vacuity control steps. |
| Instrumentation | All 40 browser control steps and four actual native visibility wrappers independently bound to pinned bundle hashes and their own step timestamps. |
| Coverage | All 100 source-derived inputs, all 15 unchanged 90% line/80% branch floors; independent original-report arithmetic verifies conservative method branch bounds, exact input/build manifests and merged line-union equality. Upstream merged branch metadata is non-authoritative. UI critical-module 95% line/90% branch/100% function floors pass. |
| Additional controls | Separate bounded WebKit contrast cohort 20 samples × 3 arms/60 contexts has zero findings. Advisory 31-project inventory reports no known vulnerabilities; 288 locked NuGet license pairs and 104 frontend package versions/integrities/licenses match source. |

CI543 remains rejected:121 jobs passed, the SQL Server WebKit navigation assertion and
final gate failed, and coverage skipped. The test had observed the denied response before
the navigation committed. Waiting for commit with the same destination predicate corrects
the observation without another POST or weakened security assertions. CI544 and CI545 verify the
correction; no blanket retry or required-test waiver was introduced. Local new scope tests
pass 3/3 on each provider, full solution build has zero warnings/errors, all 74 selectors,
quality, assurance snapshot and source secret scans pass. All 345 assurance controls remain
not_assessed; snapshot checks are not compliance acceptance.

Independent architecture/security/API, test and UX/Aspire source reviews pass after the
recorded corrections. Complete-spec review added direct key-scope evidence, preserved
orphan-audit detection across multiple jobs, documented contextual `jobVersion` validation
and corrected stale implementation/topology descriptions. The native workspace reconciler rejects
46 seeded malformed-evidence controls; the coverage gate has 65 independent controls and its independent artifact reconciler
rejects 14 seeded defects after a complete synthetic positive baseline; these are parser controls, not product test passes.

Final record review corrected historical Core UUID argument names in the requirement map
using fresh discovery, unchanged source methods/subcase cardinality and the exact CI545
results. A test-review correction now changes every synthetic manifest coherently and
asserts the specific malformed-module/hash error, so a cross-cell disagreement cannot
mask those validators. All 36 provenance controls pass locally; these strengthened
controls are part of the final head and require its CI before squash.

Ordinary native Aspire demo acceptance is Chromium on both providers. Chromium/WebKit
recovery composes shipped UI, real Kestrel and the production Worker host in fixtures;
it is not four full-Aspire recovery executions. Host-lifecycle restart is not an OS-process
kill claim. Firefox and two exact WebKit certificate cases remain deferred under A-009,
not passed. Actual Windows/manual accessibility, six historical findings, vendor, production
and public-release gates remain open. Refresh stays disabled. The earlier bounded repeated
pilot replay cleanup does not close the initial replay/late-issuance window.

**Grant coverage correction — 7 November 2026:** CI544 at `c18569e` passes all124 jobs;
reconciliation verifies5,753 native executions, both native demos and110 mutation kills.
Independent coverage review nevertheless proves that the upstream merge loses branch
metadata. T018 stays open. The correction enforces conservative original-method bounds,
unchanged15-module floors and per-cell before/after DLL/PDB/source/collector bindings.
Saved-report feasibility passes; new exact-head CI provenance and acceptance remain required.
PR124 stays draft/unmerged. The initial pilot replay window and all historical/manual/
public-release gates remain open. See [coverage correction](coverage-correction.md).

## T018 acceptance candidate — 6 November 2026

Complete-spec review found missing direct actor/application/action request-key isolation
cases. Three new cases pass on each real provider with unchanged prior job history, exact
actor/action/key/target binding, no duplicate acceptance and native/configuration isolation.
The audit helper proves that all application grant audits are the union of the requested
job details before splitting histories; orphan audit detection is preserved. All2,663 prior
portable administrator identities remain; six are added, making2,669 total and93grant
cases/provider. Build, quality and all74selection controls pass.

CI543 at `09654e2` failed the SQL Server WebKit cross-site logout case: the response was
correctly denied, but the test read the page URL before navigation commit. The correction
waits for commit using the same exact destination predicate, without repeating the native
POST or weakening denial/nonmutation/original-form assertions. The rejected run has121
successful jobs, two failed and one skipped; it is not accepted. Corrected browser execution
and full final-head artifact/coverage reconciliation remain pending. [Local receipts](t018-local-evidence.json)
record six focused passes and source hashes. Architecture/security/API, test and UX/Aspire
source reviews pass after corrections; this is not hosted runtime acceptance.

The first broad no-restore solution build found missing/stale local assets; locked restore
and a new serial build are required before fresh non-administrator discovery. The initial
selector check found stale total expectations; corrected counts pass all74controls with no
identity removal. T018 remains open and PR124 stays draft. Initial pilot replay/late-issuance,
historical/manual/Windows/Firefox/A-009/vendor/production/public-release gates remain open;
refresh stays disabled.

## Current checkpoint — T016 accepted, 6 November 2026

Feature021 is **17/18 complete**. The two genuine browser recovery journeys pass on both
providers and retained engines: **136/136 BrowserProtection cases, including eight new
recovery executions**, at `b2464ce` in CI542. Strict artifact/native identity/execution
reconciliation is recorded in [T016 receipts](t016-evidence.json) and the
[hosted verification](hosted-verification.md#t016-genuine-browser-recovery-acceptance--6-november-2026).
No production behavior changed. All prior2,659 test identities/metadata/order remain and
four browser identities are added (2,663 total). Local build,90 selection/browser-story
controls, quality, assurance and staged secret scan pass.

T018 remains open for complete-spec final-head acceptance. The initial CI542 attempt has
an unrelated runner shutdown during governance build. The isolated rerun passed; all124
latest jobs now succeed at the same tested head. This is status reconciliation; T018 final
artifact/coverage acceptance and new-head checks remain required. PR124 stays draft/unmerged,
main is unchanged, refresh remains disabled, and all historical/manual/public-release
gates and the initial pilot replay window remain open.

## T015 checkpoint — 6 November 2026

Feature021 remains in draft PR124 on `128-application-grant-revocation`; main is unchanged
at `dce682f9812d228b41ae7fad1dcb1f6636f40d38`. Prior CI540 at `9d68c04` passed all 124 jobs
(workflow/job status reconciliation only). New-head full CI and final feature acceptance
remain required.

**16 of 18 tasks are complete.** [T015 receipts](t015-evidence.json) reconcile 13 passes on
each real provider using identical final source and strict native discovery/definition/
execution bindings, with zero failures/skips. Five new cases per provider close the remaining
T015 gaps; eight related authority/effective-approver cases provide targeted regressions.
Existing production behavior passes without modification.

The transaction deadline test waits on the actual product token after a native token save,
observing phase cancellation while request/caller remain live. The request deadline test
forwards the original scoped native resolver and token, delaying real context activation
before any transaction can start. It observes request cancellation with a live caller, no
transaction, unchanged durable job/native/history state and completed context disposal;
a fresh ordinary request subsequently completes the queued work. UTC remains frozen in
both cases, so wall-clock advancement cannot masquerade as real elapsed enforcement.
Measured timings are retained as diagnostics; exact minimum timing assertions are not used.

Three real failed-batch races cover later progress, fresh explicit continuation and terminal
success. A persisted native revocation is observed before the deliberate save failure;
actual rollback/disposal releases its locks before a test gate delays cleanup. After the
peer reaches commit, original cleanup drains and the fresh failure recorder is observed
waiting on that exact peer session. Same-approval incomplete progress gains one zero-delta
stop; newer approval and success preserve the peer's exact job snapshot without extra audit.
Target/control native rows, configuration, counts and immutable approvals/audits are checked.
No manually constructed GrantAttempt is supplied to these races.

Build, informational style, quality, all 74 selection controls and assurance pass without new
allowances. All 345 assurance controls remain not_assessed. Discovery preserves all 2,649
prior identities/metadata/order and adds ten (2,659 total; 90 grant cases/provider).
Independent test/security review required a marker proving the intended native fault was
reached after persisted verification; this is implemented and required by both-provider
results. Architecture review passes scoped ownership and helper-compatibility checks.
See [hosted verification](hosted-verification.md) for the retained initial quality failure,
review dispositions and execution limits.

T016/T018 remain open in [tasks](tasks.md). Next is genuine browser stopped-job continuation
and unknown-acceptance recovery, followed by complete-spec final-head acceptance. Accepted
work retains recorded approval plus current account/permission checks; re-enable stays
separate after successful reconciliation. The initial pilot replay/late-issuance window and
all historical/manual/Firefox/A-009/production/public-release gates remain open. Refresh
stays disabled. No PR was marked ready or merged by this checkpoint.

## Executed local evidence

| Scope | Actual result | Limits |
| --- | --- | --- |
| Core finite contracts | 41 passed, including16 new grant cases | Final local build and full Core suite pass after synthetic UUID fixture correction. |
| Real provider grant schema/upgrade | 3 passed on PostgreSQL;3 on SQL Server | Additive migration, max identifiers, stale concurrency, real constraints and populated immediate-predecessor preservation; changed older upgrade fixtures still need full suites. |
| Complete grant namespace | 44 passed on PostgreSQL;44 on SQL Server | Native selection/state boundaries, current authority, actual HTTPS acceptance/continuation, provider-observed admission/worker contention, restart, audit denial/cancellation, lost acknowledgements and delayed failure reconciliation. Retained-browser execution remains separate. |
| Actual hosted Worker | 2 passed/provider in focused runs | Production Worker registrations, partial shutdown/restart, and two independent hosts with provider-observed governance lock waits. TestServer host, not ordinary Aspire browser acceptance. |
| Native API/BFF contract suite | 44 passed, including4 generation cases | Both actual mapped endpoint sets, finite permissions, key/precondition/CSRF metadata,202+Location, lossless counters and deterministic browser export. |
| UI behavior | 21 new cases passed;83 total UI cases | Strict outputs/counts, confirmation, exact key recovery including denied recovery, generation/expiry/hide/disposal and generated transport. New modules meet95%line/90%branch/100%function floors; actual lines/functions100%, minimum branch95.70%. Browser rendering/a11y remains pending. |
| TypeScript, shipped assets and contracts | Passed | Six deterministic local assets, all existing session/list/editor floors,104 locked dependencies and44 Node contract controls. |
| Code quality | Passed after corrections | No new allowance; obsolete Worker documentation allowances removed. New fixture literals have independently named values and all method/type limits pass. |
| Native discovery and selectors | 2,565 administrator cases;74 selector controls passed | All2,475 prior portable identities/metadata and order retained;88 ordinary and2 retained-browser grant cases added. Persistence48/Core41 inventories retained. |
| Mutation tooling | 60 control tests passed | Six new grant decisions registered for both providers, preserving49 prior decisions/provider. Actual new mutant execution remains pending. |
| Ordinary Aspire browser journey | Compiles | Shared actual UI confirmation/progress/completion step added to both native demos, with the real background Worker. Runtime remains pending hosted retained browsers. |

Commands used the selected SDK environment (`source /workspace/managed-env.sh`),
serial `dotnet build ... -m:1` and .NET10 MTP `dotnet test --project ... --no-build`
with explicit ApplicationGrants class/provider filters and TRX reports. SQL/PG runtimes
were sequential. `npm test`, native contract generation, pinned contract/type generation
and `python3 scripts/check_code_quality.py` were executed. [Hashed local receipts](local-evidence.json) retain actual native test names and counters. Raw reports are retained in
`/workspace/grant-*-results` and `/workspace/grant-*.log`, outside committed source.

Retained failed attempts: malformed `dotnet test -m:1` invocation executed zero tests;
initial native token identifier was corrected before the schema suites; initial EF
second-provider generation required serial build then `--no-build`; initial integration
builds failed formatting and cached-array diagnostics before their corrected passes.
A stale quality allowance failed the gate after its documentation defect was fixed;
the stale entries were removed, not renewed. Contract generation initially lacked its
locked local npm tree; `npm ci --ignore-scripts` restored it before successful generation.
An initial UI test inventory command used the wrong working directory; corrected execution
passed its then-current inventory. A later malformed OR class filter executed zero tests and was replaced with a proper native filter. Stale Node operation/discovery/coverage-input expectations were corrected without dropping identities. The full PostgreSQL persistence run passed23/24; the frozen-producer test failed before execution because its required fixture environment paths were unset. Its corrected run remains pending. The SQL full-persistence attempt and a retry after releasing regenerable build outputs both aborted when Docker reported no disk space; no pass or complete report is claimed. Source and retained evidence were preserved. Docker reports no remaining containers/volumes, but the shared filesystem again exhausted its capacity, so further broad container checks are deferred to a fresh hosted runner. AppHost initially lacked restored assets; locked restore then build passed. Folder-only formatting inputs missed two files; explicit file inputs fixed formatting, and the build passed. New quality findings were corrected with named fixture constants, without new allowances. The first staged export failed for lack of disk; that scan is invalid. A corrected staged-only scan identified two synthetic UUID fixtures as generic-key patterns; low-entropy canonical synthetic UUIDs retain the alias checks, and the final staged scan passes with no findings. No failed attempt is counted as a pass.

## Independent source-review dispositions

- Security: frozen pre-native baseline, target checks, final clock and local+persisted
  output comparison resolve earlier findings. Narrow API/BFF/source review passes;
  actual boundary regression evidence is still required.
- Architecture: found missing Worker `ProtocolStorageSelection` registration. Explicit
  scoped registration fixes ordinary owned-only composition; the actual hosted restart
  test passes on both providers. Shared linked internal OpenAPI helpers avoid duplicated
  contracts without coupling host assemblies. The data model explicitly distinguishes runtime-generated exact64 lowercase SHA256 digests from relational maximum64 storage; database alphabet/exact-length rejection is not claimed.
- UX: preserve original unknown acceptance across failed/denied recovery; refresh current
  protected status after recovered acceptance; restore focus after explicit actions;
  show job reference and accurate permission/MFA denial wording. Final narrow source review confirms these findings resolved. Browser proof remains pending.
- Test review: strengthened delayed-failure oracles with exact native ID/association/status comparisons, reused independent audit provenance checks for the browser fixture, and replaced immediate focus reads with bounded native Playwright focus assertions. The late-failure cases remain component reconciliation evidence; real failure capture/contention have separate tests.

## Required remaining evidence

T016 is complete with the four reconciled retained browser cells described above.
T018 requires complete-spec review and full exact-head artifact/coverage reconciliation before
reviewed squash delivery. All100coverage inputs and15unchanged floors remain mandatory.
The initial pilot replay/late-issuance window, historical findings, manual Windows/accessibility,
Firefox/A-009, vendor, production and public-release gates remain open. Refresh stays disabled.
