# Hosted lifecycle verification

## First implementation candidate — 5 November 2026

Source `b7886ff6cf87d0cab541ecdc2e8412b8f4b8ea80` was submitted as tested merge
`1d7b2436ba0ae157b36e79f44eca927ddd2a5f77`, tree
`4c9e8b975e246b46e43979a114236c9d6d3b78ef`, against main
`b27669f04ad5992ae829c1250bd13f90e8c72446` in
[run516](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41946316577).
This candidate is **not accepted**.

The ordinary PostgreSQL/SQL Server foundation jobs failed at full native discovery, before database
execution. `eng/test-matrix.json` correctly expected21 persistence cases/provider, but
`eng/persistence-test-matrix.json` still contained19/provider and omitted the four new
`LifecycleSchemaTests` identities. The shared portable-metadata validator failed closed. Its
administrator-named module is also used for persistence; the error did not prove an administrator
runtime failure. Jobs126979358906 and126979358940 independently reported this same omission.

The correction appends exact actual native discovery metadata for the four lifecycle schema cases.
All38 prior names and complete type/trait metadata remain unchanged. Full discovery and exact
provider selections now verify42 total/21 per provider, matching the matrix. All74 selection controls
pass. No identity checker, count, namespace, provider or skip rule is weakened.

Static analysis job126979357979 independently found four unnecessary using directives in
`ApplicationLifecycleTests`, `ApplicationLifecycleRaceTests`, `ManagedLifecycleRaceTests` and
`ManagedSchemaTests`. Only those directives are removed. The full local
`dotnet format style --no-restore --severity info --diagnostics IDE0005 --verify-no-changes`
then passes. No product behavior or native test assertion changes in these corrections.

An independent hosted reviewer read complete paginated job inventories and exact source/merge
metadata through the approved GitHub connector. Direct shell API access remains forbidden; raw
artifact downloads are unavailable and were not retried. Partial successes or locally passing
feature suites do not replace corrected exact-head hosted acceptance. Later candidate results must
record their own source/tested merge, complete jobs, summaries and evidence-access limits.

## Second candidate — integrated checks remain rejected

[Run517](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41947123873), source
`2f852e2dff9230d93a94f0df00f654542097929f`, tested merge
`a9f61e2411f038a5faf12dd6e78368e2db6b43d8`, tree
`f4ea34b7da5ddc759e5834dd3c4e8be94ce7d3f5`, completed with 122 jobs: 113 successful,
eight failed and one skipped. The independent paginated API review retained 114 nonexpired artifact
metadata records. Combined SQL application mutation evidence and merged coverage are absent.
Raw artifact ZIPs were not downloaded or reparsed. Final acceptance failed and coverage was skipped.

Four failure categories remain explicit:

1. Both foundation jobs ran 21 persistence cases: 19 passed and two failed. Historical integrated
   upgrade witnesses omitted the new lifecycle migration/table/query from exact latest-schema
   expectations. The correction appends only those exact additions, requires the default Enabled
   column and empty new audit-detail table, and preserves historical row fingerprints and all
   duplicate-sensitive catalogue checks. The immutable producer and ManagedApplicationUpgradeWitness
   are unchanged. Local full PostgreSQL now passes 21/21. The first local SQL run had 20/21 passes:
   one container startup could not locate sqlcmd, before schema assertions. A changed-condition
   execution with `--parallel none` passes all 21/21 without skips; the earlier startup failure's
   cause is not established and remains recorded. Hosted default execution is still required.
2. The SQL application mutation job passed all eight existing controls and four lifecycle baselines,
   then the lifecycle enforcement mutant failed outside its expected behavioral marker. The strict
   runner rejected it (`stack_match=true`, `marker_match=false`); no kill is claimed and the original
   cause is unknown. PostgreSQL passed both mutation suites. A new genuine BFF-disable/first-issuer-
   request witness isolates disabled admission, verifies committed state/audit/reason and unchanged
   credentials, then checks exact local denial and no state change. Only the CriticalDecision trait
   and manifest target move; the comprehensive old-cache/code/peer/re-enable journey stays required
   unchanged. New focused ordinary executions pass once per real provider. New mutation receipts
   remain required; [original local receipts](evidence/README.md) are not substituted for them.
3. Both native Aspire demo jobs passed two of three cases; their complete runtime journey failed
   during fresh-login code selection, before factor submission. Adding the second managed journey
   makes six accepted MFA ceremonies possible within a five-counter native window. Test-only
   preparation now checks for unused material before opening a new administrator sign-in, permits
   one cancellable wait to the next actual counter boundary, then requires fresh material. It neither
   submits authentication nor caches a code, clears replay history, widens the native window or changes
   authentication/navigation budgets. A deterministic Harness regression covers exhaustion, original
   priority, unchanged replay set and independent expected rollover digits. The final sign-in now has
   a distinct finite phase label. Successful hosted demos remain required.
4. Both WebKit BrowserProtection jobs passed 30/31 cases, failing the second managed ceremony after
   re-enable at the completion URL wait after consent. Both Chromium cells passed. The source trace
   establishes the wait location, not whether consent, correlation, exchange or validation failed.
   No speculative security/browser change is made. Finite per-flow diagnostics now distinguish
   callback arrival/rejected correlation, exchange status, token-shape/identity validation and
   completion; output contains only closed categories, bounded counters and known route categories.
   They emit on first and repeated journeys, preserve original failures and deadlines, and never
   retain URLs, token bodies or exception messages. A private-canary Harness regression protects
   the diagnostic output. Corrected exact-head browser acceptance remains open.

The corrective inventory preserves every earlier test identity, adds two focused provider cases,
and moves only the original journey's two mutation-selection traits. Administrator portable discovery
is 2,475; ordinary provider totals are 1,114/1,219, ManagedApplications 118/provider (Admission 54,
Issuance 64). AppHost Harness now expects 102 cases. All coverage floors, browser engines retained
for this phase, strict selection/skip rules and production policies remain unchanged. PR122 stays draft.

## Third candidate and live-clock fixture correction

[Run518](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41951407103), source
`a87a96d3f7669fc6322e90827028c81dd2dc7508`, tested merge
`43d4b681d21c3c6c1fa5f563d4284e898675e55a`, tree
`443ad619b8063ec70cbe043076b76d2f0db184b5`, retains the same verified main parent.
At the bounded review checkpoint, both full persistence lanes pass21/21 and both native Aspire
local-demo lanes pass3/3 with zero failures or skips. PostgreSQL's combined mutation job passes
all eight existing application controls and the four focused lifecycle controls. The run is not yet
accepted; later job results must be reconciled separately. Independent source obligations are
122 jobs,116 artifacts,101 ordinary reports/5,523 cases,98 coverage inputs/15 floors and98
behavioral kills across16 suites/14 jobs.

Both new focused mutation suites also completed locally at this exact source:four baseline passes,
four demonstrated kills and four restored passes/provider, no skips, with exact source restoration.
[Receipts](evidence/README.md#focused-admission-witness--a87a96d) retain their precise source binding.

The timeout investigation independently found a fixture clock-domain inconsistency: JourneyScenario
froze FactorClock at fixture creation and supplied that same time to native OpenIddict. The native
60-second authorization-code expiry clamps managed ID/access tokens, while the independent relying
party validates against actual UTC with zero skew. A later browser ceremony can therefore receive
an already-expired token even though its issuer fixture still observes the earlier time. This is a
source-established risk, not proof of the historical WebKit failures' cause.

The correction selects a source-backed `FactorClock(TimeProvider.System)` only when constructing
ApplicationManagedBrowserEvidence, before enrollment or the first native cookie. That exact instance
passes through all existing fixture constructors to JourneyScenario, issuer/API/BFF service roots
and native server/client options; the actual browser journey asserts each binding. Every other test
keeps its original frozen default. The live setter rejects manual resets; inherited real timers,
production code, native lifetimes, expiry clamps, strict RP validation and browser deadlines remain
unchanged. Two deterministic Harness controls prove forwarding, preserved frozen defaults, expired
original absolute deadlines and new observations without renewing old authority. They are fixture
controls, not a claim of native issuance or historical-cause reproduction. Required actual hosted
browser journeys and existing native lifetime oracles remain the integration evidence.
