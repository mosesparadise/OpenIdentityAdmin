# Grant revocation implementation verification

## Current checkpoint — 5 November 2026

Feature021 is in progress on `128-application-grant-revocation`; draft PR124 is not
accepted or merged. Remote main was reverified as
`dce682f9812d228b41ae7fad1dcb1f6636f40d38`. Implementation is published at
`a1b809cb4e0c075dd1475d6a048560fc7481f8d4`, superseding the requirements-only
`a0665f7309a69d314556f1db15bd5a91ce0980d2` checkpoint. [Hosted run527 and its bounded
corrections](hosted-verification.md) remain incomplete acceptance. This document records partial evidence,
not a final exact-head acceptance receipt.

The user selected disabled-until-success. Accepted work continues after logout/MFA
expiry under recorded approval, with current effective-account and persisted permission
checks for each batch. Neither decision remains pending. The UI adds explicit
confirmation, durable reference/status/counts and same-key recovery for uncertain
acceptance. Browser interaction and integrated acceptance remain unverified.

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

Alternate effective approver, retained-key expiry under fresh current authority and missing
actor-lock/factor-generation cases now compile and are in the exact native inventory;
their provider execution remains pending. Ordered issuance/re-enable/admission races, broader
BFF downstream/generation failures, genuine cross-instance native entry-validation
proof, complete module coverage and full applicable exact-head CI remain open. Run528
provides verified both-provider native-demo/retained-engine grant execution and12new
behavioral mutation kills, recorded in [hosted evidence](hosted-verification.md); it is
rejected for a UI polling test and two unrelated package-restore failures. The current
HTTPS and provider component passes do not close these broader obligations. See [tasks](tasks.md).
The six new mutation controls and shared browser journeys are scheduled in ordinary full CI. All100 coverage-report inputs and15 module floors remain mandatory. No task is complete merely because its implementation exists. All historical findings,
manual Windows/accessibility work, Firefox/A-009, vendor, production and public-release
gates remain open. Refresh stays disabled.
