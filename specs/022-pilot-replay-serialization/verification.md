# Pilot replay correction evidence

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
