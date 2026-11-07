# Pilot replay correction evidence

## T008 acceptance candidate — 7 November 2026

The user selected **T008, SQL Server validation, and T009** for this batch. Branch
`132-pilot-replay-acceptance` preserves the prior handoff `2b5990d79fa89b152bf6b2c382bf70e089efb705`.
Source `71ab8e319ab183b8af21b0405f4a2ac565a93894` adds five behavioral mutation controls,
their real-provider witnesses, portable native inventory and mandatory hosted jobs.
Production behavior and migrations remain the reviewed T003–T007 implementation;
subsequent style cleanup only orders imports and removes unnecessary directives.

The controls independently omit gate acquisition, roll back native consumption,
release the gate through a savepoint while generation remains active, omit generation
commit, and omit actual scope cleanup. Each kill marker requires an observed protocol/
native-storage violation. Setup failures, timeouts and unrelated errors cannot count
as kills. The cleanup witness explicitly drains all actual scopes, including its
fresh-flow positive control. Architecture/test review accepts these source boundaries
conditionally; actual provider execution remains required.

Native discovery is now **2,771 administrator cases**, adding exactly five per provider.
All 2,761 previous identities, metadata and relative order remain. Each provider's
ProtocolAdmission partition has 162 cases; browser cases remain 74/provider. No
production gate, native validation, response rule, browser deferral or coverage floor
was relaxed. All 74 selector controls and 62 mutation-verifier controls pass; the
five PostgreSQL baseline witnesses pass. Initial formatting-only build errors were
corrected; the rebuilt project and code-quality gate pass without allowances.

Both provider mutation jobs are mandatory in the fail-closed final acceptance gate.
Source-derived full-run obligations are 126 jobs, 103 native reports/5,875 executions,
120 mutation kills, 120 artifacts, 100 original coverage inputs, 15 unchanged module
floors, 83 UI cases and 44 Node contract identities. These are required counts, not
completed hosted evidence. [Isolated PostgreSQL mutation evidence](evidence/mutation-candidate.json)
passes all five baseline cases, demonstrates all five intended behavioral kills,
then passes all five restored cases with source hashes restored. Independent review
recomputes all mutation hashes and verifies exact identities, traits and clean
restoration. Raw mutation TRXs are intentionally ephemeral; this is receipt plus
reviewed strict-runner validation, not independent raw-report reparsing. The savepoint
mutation demonstrably releases exclusion on PostgreSQL; its SQL behavior remains
unverified until the hosted run. Exact-head hosted acceptance is pending.

CI551 at `0159cc26e0005284534e9fc98133b9bf433f06ce` caught a missing expected-job
entry in `test_prepare_native_image.py`: the new pilot mutation job already prepares
the correct pinned image, but the independent job inventory did not include it.
The failure was reproduced locally. Adding the exact new entry preserves set,
ordering and timeout assertions; all 22 related controls pass. The hosted run is
retained as a failed attempt, and its other provider results do not establish full
acceptance. The next candidate requires its own complete clean run.

Provider/Aspire review found no verified local PAL configuration correction. Both
retained HOME/workdir probes already reached the pinned sqlservr binary and failed
creating `/.system`; neither is repeated. The unchanged pinned image and required
SQL cells will run in the authorized hosted matrix. Local startup failure remains
an environment limitation; it is not SQL or schema acceptance. Both-provider schema/
transaction review is source-only until the actual required tests succeed.

T008/T009 and outstanding both-provider acceptance remain open. Preserve all previous
failed attempts, source receipts and frozen producer files. Historical findings,
refresh activation and manual/Firefox/A-009/vendor/production/public-release gates
remain open.


## T005–T007 implementation checkpoint — 7 November 2026

Source `6860a6b7b155d931b8d7e7757992ddceb1b428a4` is pushed on
`131-pilot-replay-coordination`, based on branch130 handoff
`f9da6414ac187bbfc1c0e0eb7f918edfd54da2cb`. Remote main remains PR125's planning squash
`8f840f5a22836efd4b74ee5883ec98d04d3c6cec`; no PR, full CI, merge or production action
was performed. Earlier sections below are historical checkpoints.

Implemented and independently reviewed boundaries:

- **T005:** standalone and mixed native token requests retain the original bounded
  response capture. Initial lifecycle/replay validation commits in a fresh gate
  phase. Generation reacquires that gate and owns every generated native entry in
  the same context/transaction. Only a freshly bound Valid code enters a separately
  committed native-redemption child; that child never runs replay cleanup. A
  conditional Valid-row lock prevents native bulk revocation, which does not change
  its concurrency token, from racing the tracked redemption update. Fresh authority
  and original expiry are rechecked after consumption and before commit. Command,
  connection and commit fences deny replacement contexts, reconnect/autocommit and
  late continuation; unsafe storage attempts latch request failure. Native output
  remains captured through callbacks, confirmed commit and actual scope disposal.
  Pilot remains LegacyPilot; `ProtocolStorageOwner.BeginChild()` was not relaxed.
- **T006:** real HTTPS tests cover actual INSERT rollback, commit acknowledgment loss,
  disposal failure, real native backend termination, both competing-code and native
  bulk-revocation continuations, caller cancellation, exact five-second generation
  and original ten-second request deadlines, missing gate without repair, post-wait
  account/session/application/scope/authorization restrictions, original code expiry,
  and committed replay revocation followed by formatting/OnStarting failure. Tests
  identify the reached native boundary and persisted result; arbitrary HTTP failure
  is insufficient. The old permissive ordering fixture is replaced by explicit
  historical revoked-code/valid-access state, preserving its 12 binding negatives,
  cleanup-failure and unrelated-success controls.
- **T007:** a disposable database-only INSERT barrier targets the exact native
  access-token type and authorization. Each direction proves distinct live issuer
  process generations and pinned transports, durable consumption, the real
  holder→winner→replay database wait chain, exactly one independently verified
  success, and every winning entry Revoked before any further replay. All actual
  exchanges and database sessions are drained before fixture objects are dropped.

The native inventory is 2,761 administrator cases: 1,328 PostgreSQL and 1,433 SQL
Server, including the unchanged 74 browser cases per provider. HTTP counts are
1,254/1,359 and ProtocolAdmission is 157/provider. Exactly 2,677 prior identities
retain their metadata and relative order; two historical ordering names are
explicitly replaced with four seeded-history identities, and 80 other cases are
added. Host discovery is 1,852; Lifecycle is 43/provider, including Distribution 25.
All 74 Python selection tests pass after correcting stale count expectations. No
coverage floor, browser deferral or legacy quality allowance was expanded.

### Final-source scoped verification

All four clean runs below use source `6860a6b7b155d931b8d7e7757992ddceb1b428a4`;
AuthServer and each test assembly carry that informational version. The
[coordination receipt](evidence/coordination.json) records the complete source archive,
DLL/PDB/report/log hashes, native discovery, actual execution IDs and case outcomes.
Discovery and executed display-name multisets match, retaining native truncated-name
collisions as distinct test IDs. These are scoped local results, not full CI.

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| PostgreSQL administrator ProtocolAdmission | 157 | 0 / 0 |
| Host regression suite | 1,852 | 0 / 0 |
| PostgreSQL lifecycle Distribution | 25 | 0 / 0 |
| PostgreSQL persistence | 29 | 0 / 0 |

The 157-case run includes all 47 focused pilot cases in one clean final-source run;
the 25-case run includes both new real two-process directions and existing distribution
regressions. The final persistence run verifies fresh/upgrade compatibility using the
preserved producer from `38eb41e06d40b6addbe57e3af839f912645e6dea`; all 77 files match
its original manifest. The historical producer was not rebuilt or replaced.

Two failed final-source persistence attempts remain in the receipt: concurrent test
collections exhausted local Docker vfs storage (5 passed / 24 failed), then serial
collections passed 28 with one failure because the frozen producer environment paths
were unset. The final 29-case run supplies the verified paths and disables collection
parallelism with `--parallel none --max-threads 1`; competing operations inside the
concurrency tests remain intact. No product source changed between these runs.

Exact-source serial builds pass with zero warnings/errors. The code-quality check,
74 selection tests, instruction integrity, 345-control register integrity and source
secret scan pass. All 345 controls remain `not_assessed`. New mutation controls,
coverage acceptance, SQL cells and full exact-head CI remain outstanding.

Independent final evidence review verifies all 2,063 passing executions, exact native
name multisets and unique linked TRX identities, all 43 receipt file hashes, all
2,882 source-archive blobs against the committed tree, assembly source versions and
the 77 frozen-producer files. It accepts this bounded evidence checkpoint only;
SQL Server, T008/T009 and broader acceptance remain open.

### Evidence and review limits

Initial focused PostgreSQL evidence is **45 passed / 2 failed**; the corrected
native-bulk observer run passes exactly those two failed identities. The combined
observations cover all 47 focused cases but are not one clean final-source run.
The ordered two-process run passes 2/2. Retain these reports and their actual failed
attempts; final-source broader results are recorded separately below.

Initial test implementation failures were corrected without weakening acceptance:
EF original-value metadata was replaced with a same-transaction persisted Valid
read; bulk fixture execution now has the explicit transaction its observer requires;
the standalone recovery observer no longer requires mixed-host-only registration;
and the cross-process trigger now uses the pinned native full token-type URN rather
than the short JSON response field name. The initial replay old-source failure in
`evidence/t002-baseline.json` remains unchanged.

Architecture/security review identified and resolved the Valid-read/update race and
the accidental non-token watchdog scope. Four host controls prove ordinary routes
keep their existing lifetime. Further independent source/test review at 6860a6b finds
no blocking implementation/oracle issue, while explicitly retaining SQL,
mutation/coverage and CI obligations. The final-source scoped runs above subsequently
passed; they do not discharge those broader obligations. AI review is not external security acceptance.

SQL Server remains blocked **before readiness** by `/.system` access denial,
`LinuxDirectory.cpp:420`, errno13, exit1. The pinned image's actual non-root UID/GID
is 10001 and `/var/opt/mssql` is writable. One workdir-only and one HOME-only startup
probe each retained the same failure, then removed its container. No SQL queries,
migrations, ports, image changes, root-user workaround or production operations
were used. Neither override is justified as a repository fix. No identical startup
attempt is reclassified as provider/schema/replay acceptance; a changed,
evidence-based runtime condition is needed before executing the pending SQL cells.

T003–T007 therefore remain unchecked for required both-provider verification.
T008 still requires new gate/publication mutation controls, original coverage inputs,
all unchanged regressions and exact-head full CI. T009 still requires final integrated
review and delivery. The correction and all six historical findings remain open;
refresh activation and Windows/manual/Firefox/A-009/vendor/production/public-release
gates remain unchanged. Do not redo the implemented phases at the next handoff.


## Verified starting checkpoint — 7 November 2026

Remote PR124 is merged with squash `e44667c45d103cdc3d70427577f89b77539f6f22`.
Remote main matches it. Post-merge workflow runs42326203442 and42329191762 report
success. Prior feature021 acceptance remains in its original receipts and PR124;
it is not rerun or relabeled as this correction's evidence.

The previous feature worktree remains clean and preserved. This work uses the new
`129-pilot-replay-serialization` branch/worktree based on current main. No production
source, schema, native test or workflow has changed at this planning checkpoint.

## Review dispositions

Initial architecture/security source review identifies the existing initial replay
window and confirms a preserving-contract correction fits current approval.
It rejects a status-check-only fix and warns against relaxing owned parent/child
transaction invariants. Root review identified manifest-lock self-blocking; follow-up
review confirmed the actual manifest readers and the absence of a supported RCSI
guarantee. The plan now uses isolated coordination state.

Further root/architecture review rejects independent gate and generation commits:
connection loss would leave a late-generation commit race. They now share one
transaction. Native initial replay validation has its own gate transaction; the only
child without reacquisition performs freshly bound early redemption, never replay
cleanup, preventing a late unfenced revoker. Test review required a common old/new
safety oracle, independently seeded historical cleanup state, post-wait freshness/
code-expiry evidence and durable replay followed by response failure. These are now
explicit in C01/C08/C11/C12. Independent architecture/security and test reviewers accept T001 planning only.
Root applied the final wording corrections distinguishing actual transaction release
from later cleanup and independent validation from consumption. No runtime pass is claimed.

Test review also notes that native bulk revocation can change status without changing
its concurrency token. The plan must not infer every intervening revocation is caught
by optimistic concurrency alone: C12 requires actual status, replay denial and absent
credential output. Exact terminal status and usability are separate observations.

## Open acceptance

Integrated C01–C12 acceptance, both-provider correction proof, mutation controls
and final-head implementation CI remain open. The foundation evidence below is
partial and does not close those cells. The initial pilot window remains open. Refresh stays disabled. All six
historical findings and Windows/manual/Firefox/A-009/vendor/production/public-release
gates remain open.

## Planning checks actually run

- `python3 scripts/test_ci_scope.py`: 7/7 pass. Changed prose selects the existing
  docs scope; no policy/workflow/source change is included.
- `python3 scripts/check-ai-instructions.py`: eight skill/reviewer configurations pass
  integrity checks; this is not runtime reviewer effectiveness evidence.
- `python3 scripts/check_assurance.py`:345-control integrity passes, all345 remain
  not_assessed; no compliance claim. `python3 scripts/test_assurance.py`:27/27 pass.
- Local structural check:9 unique tasks,12 acceptance cells, no unresolved product
  clarification markers and existing local file-link targets.
- Gitleaks directory scan with redacted output: no leaks. An initial invocation
  referenced an absent optional config and did not scan; the corrected invocation
  used the same default configuration as repository CI.
- Final indexed `git diff --check`: pass after removing three Markdown hard-break
  trailing spaces detected during staging. Spec Kit setup-plan/setup-tasks/prerequisite resolution
  succeeds for feature022; no extension hooks file exists.

At the planning checkpoint T001 alone was complete; the later foundation checkpoint
below supersedes that task status. Every integrated runtime acceptance cell remains open. The planning PR's exact-head documentation CI and merge state are its delivery
record, not implementation verification. Next: retain and run T002's baseline safety
regression, then implement the reviewed persistence/runtime correction.

## T002–T004 foundation — 7 November 2026

Main is now PR125's planning squash `8f840f5a22836efd4b74ee5883ec98d04d3c6cec`.
Implementation branch `130-pilot-replay-foundation` starts there, preserving the
prior worktrees, feature021 evidence and frozen compatibility producer. T002 is
complete as a regression checkpoint. T003–T004 code is implemented, but their
both-provider verification remains open. This branch must not merge as a completed
security correction: T005 still has to bind generation to the gate transaction.

Implemented boundaries:

- **T002 / C01, C08:** `InitialPilotReplayTests` pauses a genuine winning HTTPS
  exchange after native redemption, then requires the *initial* completed replay
  to cover all winning entries before any additional replay. The winner's public
  token cryptography and an unrelated successful authorization are checked. Actual
  database contention is required when a live generation gate exists; an absent
  or already committed validation gate never counts as contention. Existing
  `PilotReplayOrderingTests`, its 12 binding guards and cleanup-fault control remain.
- **T003 / C09:** dedicated bounded `PilotProtocolGate`, seeded by PostgreSQL
  `20261107135812_PilotProtocolGate` and SQL Server
  `20261107140026_PilotProtocolGate`. No existing migration is edited. Upgrade
  witnesses preserve every old physical value/history, including populated grant
  jobs, approvals, progress and audit provenance. Independent catalog checks cover
  required bounded ID, key and absence of foreign keys. Fresh least-privilege
  acquisition works without bootstrapping a pilot; missing coordination fails
  without runtime repair.
- **T004 / C07, C10 foundation:** one original native validation slot in standalone
  and mixed hosts, fresh LegacyPilot selection before manager resolution, exact
  native/manifest binding, native token and authorization lifecycle validation,
  existing replay cleanup and commit inside one gate transaction. Actual scope,
  transaction and connection cleanup finishes before returning. Request
  cancellation is retained and the original five-second phase lifetime is checked
  after native work/commit/cleanup. `ProtocolStorageOwner.BeginChild()` is unchanged.
  Native early redemption and generated-token storage remain unchanged until T005.

### Retained baseline and local limits

The complete baseline source is commit `8a724a6833b7f49ed7b8bdb38603e0f515727f0c`.
[SHA-256 receipts](evidence/t002-baseline.json) bind its full source archive, exact
patch, logs and native TRX reports. PostgreSQL fails **1/1 at the required safety
oracle**, after the unrelated positive control succeeds. The fixed diagnostic is:
“Initial pilot replay completed but a winning native token entry remains usable
before any additional replay.” Later observer-lifecycle/deadline and safe diagnostic
refinements preserve this oracle; the T004 foundation still demonstrates it.

The first PostgreSQL attempt failed during migration because the workspace filled;
it supplies no defect evidence. Historical artifacts and reproducible build outputs
were moved to the separate temporary volume after byte verification, retaining their
original paths. The corrected run above reached the actual assertion. SDK10.0.403 /
runtime10.0.14 was restored from verified official distribution metadata; package
locks and the repository SDK policy were unchanged. Development HTTPS trust was
restored without disabling TLS verification.

The current pinned SQL Server image downloads successfully. Both actual Aspire and
plain Testcontainers attempts then exit1 before readiness, with native error
`LinuxDirectory.cpp:420`, `0xC0000022`, errno13, system directory `/.system` access
denied. This is **not** a migration, grant or replay behavior result. No root-user
container workaround, altered image, production correction or expired diagnostic
approval was used. SQL runtime acceptance remains unverified; repeated identical
runs are not reported as progress.

### Independent review dispositions

Architecture/security review found and root fixed standalone service registration:
OpenIddict removes a handler's service when removing its descriptor, so native
registration must follow removal. Standalone's adapter is registered only once.
Review also required fresh native authorization status validation in the same child.
The old outer native check remains. Test review found and root fixed stale/disposed
observer ownership, stop/timeout ordering, unsafe diagnostic comparisons, standalone
fixture route composition and insufficient populated-grant/schema witnesses. The
narrow source/test review accepts the corrected foundation; it does not establish
SQL Server or integrated security acceptance. No production, refresh, historical
finding, human/manual, Firefox, A-009, vendor or public-release gate is closed.

The next implementation task is T005, retaining durable early consumption while
putting generation and gate ownership in the same transaction and withholding
publication through actual cleanup. SQL Server startup recovery and its outstanding
foundation tests remain prerequisites to both-provider acceptance. Full CI,
coverage/mutation and cross-process acceptance belong to the integrated correction;
no full CI or merge is claimed for this intentionally incomplete branch.

### Final local foundation checks

Code checkpoint: `bcb3bd52180286021582444c6ba5ed4bba1fd0ee`.
[Native report/source/log SHA-256 receipt](evidence/foundation.json) retains exact
selected test identities, discovery and the complete source archive.

| Check | Actual result | Scope/limit |
| --- | --- | --- |
| PostgreSQL persistence | **10/10 passed** | Gate fresh/populated upgrade, runtime privilege/missing row, actual contention, cancellation/expiry rollback, existing grant/lifecycle/browser-audit upgrade witnesses |
| PostgreSQL HTTPS | **5 passed, 1 failed** | Standalone+mixed native lifecycle and revoked-authorization cases pass; old cleanup/guards/fault pass; mandatory initial race oracle remains red pending T005 |
| Native registration | **8/8 passed** | Existing server/client consumption and owner-lock guards plus both new pilot composition cases |
| SQL Server | **1 setup failure per harness** | Both baseline Aspire and foundation Testcontainers fail before readiness; no runtime acceptance |
| Administrator discovery | **2,669 retained + 10 added** | Exact existing metadata unchanged; 1,287 PostgreSQL / 1,392 SQL Server identities, including browser cases |
| Persistence discovery | **48 retained + 10 added** | 29 identities per provider; no identity replaced or omitted |
| Host discovery | **1,848 identities** | Prior 1,846 plus two composition cases |
| Selection controls | **30 administrator + 6 persistence passed** | Portable metadata/inventory checks also pass |
| Build and source quality | **Passed** | Three changed native test projects build with zero warnings/errors; unchanged size/literal/XML/naming allowances; eight skill/reviewer integrity checks |
| Assurance integrity | **345 controls unchanged, all not_assessed** | No compliance or release claim |

Intermediate local compilation/style failures and the initially incorrect standalone
fixture/duplicate service-registration assertion were corrected before these results.
Earlier attempts remain in local logs. Full CI is intentionally deferred until T005
and the remaining integrated tasks remove the known security regression; the branch
is pushed for continuation without opening a PR that would launch full CI now.
