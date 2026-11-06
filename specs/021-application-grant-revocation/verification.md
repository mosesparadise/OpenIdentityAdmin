# Grant revocation implementation verification

## Current checkpoint — 6 November 2026

Feature021 remains in draft PR124 on `128-application-grant-revocation`; main remains
`dce682f9812d228b41ae7fad1dcb1f6636f40d38`. Run534 at `2a18e68` passes all124jobs.
[Focused raw receipts](run534-evidence.json) verify51grant and131issuer-flow cases per
provider (364selected passes), including the new HTTP target/version/profile and persisted
pilot-ID negatives. Ten tasks remain complete: T001–T004/T007/T009/T011/T013/T014/T017.
Administrator-ID protection and the other specific [task gaps](tasks.md) remain open.

The earlier native replay failure is now reproduced by controlled genuine PostgreSQL
HTTP ordering: the loser revokes the redeemed code before the winner inserts its access
token; subsequent replay rejects the code without cleaning up that later token. A scoped
pilot-only correction repeats native cleanup after exact persisted bindings, leaving
native rejection in place. Final controlled PostgreSQL and SQL Server cases pass, including
twelve component binding negatives, actual cleanup UPDATE failure and a different genuine
authorization control. The original standalone issuer case is also verified separately.
See [replay receipts](replay-correction-evidence.json) and [hosted verification](hosted-verification.md).

This correction does not close the initial replay/late-issuance window or constitute
complete race prevention. Full corrected-head CI, further boundary review and feature
acceptance remain required. Native discovery preserves2,579prior portable identities and
adds exactly two controlled replay cases. Local quality, formatting at informational
severity,74selection controls and both affected project builds pass without allowance growth.

Accepted grant jobs continue after logout/MFA expiry under recorded approval and current
account/permission checks; disabled targets remain disabled until successful reconciliation
and a separate re-enable. Actual stopped-job browser recovery and all historical/manual/
public-release gates remain open. No PR has been marked ready or merged by this checkpoint.

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

The exact remaining cases are listed by task in [tasks.md](tasks.md). Current issuer
ordering/cross-instance additions await both-provider execution. Target and current-authority
negatives, API/BFF deadline/cleanup/downstream/generation boundaries, actual ordered late
failure capture and browser recovery remain open. Full CI success does not replace these
unimplemented acceptance cases. All 100 coverage inputs and 15 unchanged floors remain
mandatory for final delivery. Historical findings, manual Windows/accessibility work,
Firefox/A-009, vendor, production and public-release gates remain open. Refresh stays disabled.
