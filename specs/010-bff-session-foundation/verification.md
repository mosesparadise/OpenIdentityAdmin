# Planning and implementation evidence

15 October 2026. This record distinguishes a reviewed plan from running code.

## Planning baseline and scope

Remote main was verified at `5f4fce7fe0c7761e6eab7308f2cd38908937af51`; PR #50 remains draft/unmerged at `6fcb96216eca0974041d3e8346adc4ce8458789d`. A fresh worktree preserves the existing PR and diagnostic work. No runtime source, workflow, dependency, test selector, allowance or approval policy changes belong to this planning packet.

The installed Spec Kit 1.1.3 workflow was used to specify, clarify, plan, generate requirement checklists/tasks and analyze consistency. The feature directory was selected independently of the Git branch. No extension hook file is present. There were no unresolved product questions; approved documents and source inspection supplied the design inputs. Local Spec Kit selection state is not a published change.

## Independent reviews

Architecture/test and security source-dependency reviews agree that a complete internal protected ticket store is a useful first M02-B slice. They identified the native BFF state tables already present, the pilot-only issuer restrictions, absent PAR/refresh/end-session integration and the expired shared allowance. Their review is read-only and supplies no runtime acceptance.

Both exact-packet reviews passed all 12 requirement-quality items after three corrections: persist/authenticate the original shorter idle duration; validate native ticket scheme/identity/bindings and lifetime ownership; and explicitly require atomic audit intent as partial FR-12 coverage. Actor attribution and audit export/retention remain named integration gaps. These are resolved planning findings, not demonstrated runtime defects.

Main's cross-artifact analysis found all eleven feature requirements covered by dependency-ordered tasks, no unassigned success criterion and no unresolved critical/high inconsistency after review. At that planning checkpoint T001/T002 were complete; T003 awaited delivery and T004–T022 were unchecked. Later completion is recorded below.

Local checks passed: `scripts/test_ci_scope.py` (7 controls), `scripts/check-ai-instructions.py` (eight skills/eight reviewers), `scripts/check_assurance.py` (345 unchanged not-assessed controls), `scripts/test_assurance.py` (27 controls), relative Markdown-link checks, unique sequential task IDs and `git diff --check`. The complete 14-file diff selects `docs` under the unchanged selector. No policy, executable or hidden Spec Kit state is included.

At planning publication, hosted exact-head checks and squash were pending. The subsequent PR #51 receipt below supplies T003 completion evidence.

## Runtime acceptance status

The initial planning-only statements below are historical. The implementation evidence at the end supersedes their execution status, without claiming acceptance.

## Implementation continuation — 15 October 2026

The user instructed proceeding with the recommended B1 implementation after the
planning delivery. PR #51 squash-merged as
`ab281729fc1a44c188359faac620105d50376aec` (tree
`cf662b23ceed009da44c0d1f10eac1b016bc4ea0`). Reviewed planning head
`bee35bf55af80960f07772b4c39874f90b341aa1` passed run `38729606426`, attempt 1,
against merge `e0e3580c6c465e8a4a8234c3322d2967c5ffe70a`: four required prose
checks passed and eleven product suites were intentionally unselected. This is
T003 planning completion only.

At implementation start remote main still equals that squash; PR #50 remains
draft/unmerged at `6fcb96216eca0974041d3e8346adc4ce8458789d`, with the failed
required CI and expired witness allowance still blocking executable acceptance.
T004 is complete. Fresh branch `050-bff-ticket-store` preserves all existing
worktrees, PR #50 work and the original handoff checkpoint. Neither diagnostic
execution nor an allowance extension is part of this continuation.

Spec Kit prerequisites resolve feature 010 explicitly. Requirements checklist
8/8 and security checklist 12/12 remain checked; no extension hooks exist.
These are requirements-quality results, not runtime evidence.

At planning publication, all BFF-STORE-01–08 evidence families were **not run**. No ticket store, migration, BFF protection configuration, issuer/client integration, browser login, API or React capability was implemented by this planning packet. Product coverage, native provider/browser evidence and full CI were not executed for this prose-only change.

The next source target at that planning checkpoint was tasks T004–T021, with B1's complete storage/protection/expiry/revocation acceptance. T022 prepares B2. The expired allowance and PR #50's failed required CI remain executable-delivery blockers; this planning delivery does not waive them.

T005 pre-code architecture/test and security reviews passed after specifying fresh
clock checks following lock and audit waits, conservative native time projection,
and provider-before-wrapper disposal. Source reviews were pending at that pre-code checkpoint; their final dispositions are recorded below.

## Implemented candidate — 15 October 2026

Branch `050-bff-ticket-store` starts from main `ab281729fc1a44c188359faac620105d50376aec`, reverified before publication. It implements the internal store, dedicated encrypted BFF protection, additive PostgreSQL/SQL Server migrations, immutable lifetime policy, conditional renewal, permanent local revocation, atomic audit intent and bounded cleanup. Production composition never activates the store and adds no HTTP route. [Operations](../../docs/operations/bff-session-store.md) describes deployment prerequisites and remaining integration gaps.

The shared encrypted-ring/private-storage/certificate mechanisms are extracted into Infrastructure; issuer ownership, purpose and configuration remain separate. Existing issuer protection regressions pass. No production dependency declaration was upgraded. The new BFF reference in AppHost tests resolves their existing shared OpenTelemetry dependencies from 1.17.0 to the already pinned BFF/ServiceDefaults 1.19.1; the test lock file records that graph.

| Executed local verification | Result | Scope and limit |
| --- | --- | --- |
| Final corrected `python3 scripts/verify.py unit` | 484/484 pass, no skips | Core 25, Host 393, Architecture 5, AppHost 61; SDK 10.0.401. Subsequent C# edit only documents the tripwire getter. |
| Issuer protection regression selection | 123/123 pass | Shared-mechanism extraction controls, before final test-only additions. |
| Initial PostgreSQL BFF selection | 6/6 pass, no skips; 2m39.584s | Earlier candidate: independent providers/restart, protected persistence, lifetime/concurrency/failure/cleanup. This is not the final seven-case inventory or exact-head acceptance. |
| Final BFF native discovery | 14 cases | Seven per provider, with existing IDs retained; discovery is not execution. AppHost foundation totals become 15 per provider. |
| Lifecycle selection regression controls | 7 pass | Strict expected standard/provider totals 311/797/902; no exclusions, floors or deadlines relaxed. |
| AI instruction consistency / assurance | Pass | Eight skills/eight reviewers; all 345 assurance controls remain not assessed. |
| Mandatory code-quality check | Fail | Seeded controls and non-growth pass; unchanged expired witness allowance fails validation. No renewal or new exception. |
| SQL Server foundation and final focused BFF attempts | Blocked/failed | Docker VFS container creation reports no space left on device before usable SQL evidence; latest owned invocation stopped. |
| Final PostgreSQL foundation attempt | Blocked/failed | Same exhausted Docker storage; does not supersede the earlier focused pass. |
| Exact implementation-head full CI, aggregate coverage and merge | Pending | Required for acceptance; never inferred from local units or source review. |

The first AppHost locked restore rejected the changed project graph; the lock file was regenerated and locked restore then passed. Intermediate builds exposed documentation/formatting and bounded-wait analyzer errors, which were corrected. One Host route-isolation test initially lacked the real API-documentation configuration; loading the checked-in BFF configuration corrected that fixture, and the final full unit profile passed. One unit invocation could not create its artifact directory when storage filled. These attempts are retained, not counted as successful acceptance.

Docker uses VFS and requires a full writable SQL image copy for a new container. Current storage exhaustion supersedes the old registry-host access failure as this attempt's observed blocker; no network/SQL defect is inferred. No additional native attempt is justified until capacity is repaired. Local source, native reports and failed logs are retained. Download cache was disposable; generated binaries moved to temporary storage or an archive to preserve existing work. No historical source, SQL image or prior evidence was deleted.

Independent architecture/test and security source reviews passed after corrections: bounded native serialization must stop before later fields; exact ordinal claim binding rejects aliases and actor chains; scalar protection settings are validated; post-lock and post-audit clock checks precede commit; test task drains have independent bounded cleanup; the lock-wait fixture gates after authentication and before acquiring the database blocker. That last change corrects test design across provider isolation levels, not an observed SQL failure. The version-corruption control now first proves a valid envelope and changes only the version, with a separate expiry change. No unresolved source-review finding remains in those scopes. Review does not establish provider/full-CI acceptance or human approval.

Thirty-two obsolete legacy findings are retired (1,826 to 1,794), with no added or rebound allowance. The one existing witness exception remains unchanged and expired. PR #50's existing refactor must be accepted separately; this candidate does not copy it. All six historical findings, 48 human rows and activation/public-release gates remain open; A-004 is expired and A-005/A-006 are concluded.

BFF-STORE-01–08 now have implemented tests with partial local evidence. Final native PostgreSQL/SQL Server runs, migration/grant verification, logs/privacy and outage controls, exact-head full CI/coverage and normal squash remain outstanding. Restart tests recreate independent in-process services rather than proving process-crash durability. Lost-acknowledgement tests simulate the commit boundary with native transaction interceptors. No remote logout, current administrator authorization, browser flow, API access or React functionality is claimed.

The append-only task convergence records T023 for capacity and existing allowance dependencies. T011/T014–T016/T020–T021 remain open. Next: finish that bounded acceptance work once prerequisites are available; T022 can independently prepare the distinct administrator issuer/client integration packet.

### Retained native report receipts

These SHA-256 values identify retained local raw reports; the reports are not published with synthetic private material. The earlier provider/issuer reports are not attributed to the final commit. All listed reports have equal total/executed/passed counts and zero failure, skip, aborted or incomplete counters.

| Report | Passed | SHA-256 |
| --- | ---: | --- |
| `artifacts/tests/unit/AppHost.trx` | 61 | `1be1f0346d53d99427858a16f277bb4039872473350c92cf8d796a066808527b` |
| `artifacts/tests/unit/Architecture.trx` | 5 | `2081615074ca1828f279ccbb4b9bfae5a73bb645f08e6bb58745348011084521` |
| `artifacts/tests/unit/Core.trx` | 25 | `d3bcefd91ebc0b08f22511a728a2e61a31a7be6f8583336fe733e9e3af63067b` |
| `artifacts/tests/unit/Host.trx` | 393 | `f28ec11a7b0ff217c081e83d7013d3230db7f0f1ab5478ba18b7c6352a9d05f9` |
| `postgres-first/IdentityPlatform.AppHost.Tests_net10.0_x64.trx` | 6 | `fdc52e360357d9afb45f45b67b578c58b7190bbe8dc83d7f9b11868f23f254a7` |
| `issuer-extraction/IdentityPlatform.Host.Tests_net10.0_x64.trx` | 123 | `85abc9d4982e48d13dda9f2a0b2a1f2aff41f7b01ebc3fd45193cfc0f4bd8875` |

Final syntax-aware inventory contains 1,795 findings: the 1,794 retained exact legacy findings plus the one unchanged expired exception, with no new finding. The required command uses `IDENTITY_QUALITY_BASE=ab281729fc1a44c188359faac620105d50376aec python3 scripts/check_code_quality.py` because this isolated checkout has no `origin/main`; an initial invocation without that explicit trusted base failed before comparing allowances. Supplying the verified base passes non-growth and reaches the expected expired-allowance rejection. This is not a quality-gate pass.

## First hosted candidate and integration correction — 15 October 2026

[Run 38740538365](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38740538365), attempt 1, tested PR #52 head `16044a2a1409107e7a09e1f9a13cea9810b29399`. It failed. All 51 ordinary unit/provider/browser jobs stopped at the same pure issuer-selection prerequisite: it expected eight AppHost foundation cases instead of the reviewed fifteen. Missing test/coverage uploads were consequences of that early stop, not additional native test failures. No ordinary provider/browser acceptance was obtained. The six mutation jobs passed; those statuses alone are not artifact reconciliation or final acceptance. Aggregate coverage was skipped and final acceptance failed.

The static-analysis job built successfully with zero warnings/errors, then
rejected four unused namespace imports (IDE0005). The correction removes exactly
those imports and updates the independent foundation-count assertion to fifteen.
No runtime behavior, test identity, selector boundary, coverage floor, deadline,
workflow or allowance is changed. Hosted failure is the before-control; all 114
local prerequisite controls now pass (lifecycle 7, issuer 9, administrator 17,
browser stories 14, evidence 8, mutations 56, coverage 3). The exact hosted
IDE0005 verification command also passes locally, and the complete solution builds with zero warnings/errors. The mandatory expired-witness
allowance dependency remains open. New exact-candidate checks and native
provider results remain required; these corrections do not close historical
findings or make the implementation merge-ready.

## Corrected-selection CI and bounded observer correction — 15 October 2026

[Run 38745940575](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38745940575), attempt 1, tested head `4e0efb88d465bc1c0e5b40fcd9c090e6a8467615` through merge `93393d1281caaf7454508901eeace25423da8494`. Its tree `067876a4093a907a7a4cdb681657185ca22d78cd` equals the source head tree. Final result: **65 successful jobs, four failed jobs, one skipped job**. The selector/import corrections passed. Required acceptance is still failed.

- Hosted unit verification passed 484/484, zero skips.
- PostgreSQL foundation passed Persistence 4/4 and AppHost 15/15, zero skips; AppHost took 503.298 seconds. This supplies the first complete current-candidate PostgreSQL BFF evidence.
- SQL Server persistence passed 4/4, including migration/repeated initialization and runtime permissions. AppHost reached its unchanged 660-second invocation limit without a completed report. Artifact `11980221874`, SHA-256 `d00850a0d3c43412dcdbfb857e3a58ddfa4b35c46b353b165b34fa9068a109f4`, contains Persistence TRX, partial coverage and a native unfinished stream, not an AppHost TRX.
- Independent parsing against pinned Microsoft.Testing.Platform 2.5.1 writer source (`0bf77e3aedbf86f1fa16220bc951d3b52ba56763`) found nine complete terminal SQL records, all passing, without exceptions. Three are BFF cleanup/concurrency cases and six are existing cases. The last completion was 13:59:20.641943 UTC; 193.44 seconds elapsed without another terminal result before the supervisor stopped execution. The stream does not retain started/in-progress records, so it cannot identify the unfinished case or prove full completion.
- PostgreSQL lifecycle key-rotation activation failed 1/3 cases during initial Aspire startup. After migrations completed, DCP creation of admin-bff and pilot-bootstrap resources hit the native 20-second RPC timeout; the test failed before its replacement/rotation oracle. The unchanged lifecycle fixture/topology and initial credential loading do not establish an extraction defect. Artifact `11980141682`, SHA-256 `b799a641a4a7e315269a392a053d288859e83c5116cba14eaff6e8bb8bf67d77`, retains this failure. No timeout adjustment or causal closure is claimed.
- Static checks passed build/style and then failed the unchanged expired witness allowance. Final acceptance failed and aggregate module coverage was skipped.
- All six existing issuer/administrator/governance mutation suites passed. Independent review reconciled the actual ZIP digests, exact source/tested-merge binding, manifest/method/provider sets, source and computed mutant hashes: 42 kills with 42 baseline and 42 restored controls, zero skips. These are sanitized native-verifier receipts; raw mutant TRX is not present and new BFF mutation coverage is not claimed.

The final local SQL attempt after reclaiming 3.1 GiB still exhausted Docker VFS
storage during container creation. Console capture also became incomplete
(exit 120); no completed acceptance report resulted. No further local native
run followed. Archived generated outputs and relocated SDK/caches preserve
source, prior evidence and binary bytes; enough space was recovered for ordinary
editing and standard artifact downloads, not another SQL invocation.

Source review identified a bounded observer-format weakness: the lock-test
parameter used `marker + '%'` against full `sys.dm_exec_sql_text` batch text.
A parameter declaration can precede the command's unique comment. The correction
uses `'%' + marker + '%'`, retaining parameterization, the per-instance GUID and
the actual provider lock-wait predicate. The observer's own query contains only
the parameter placeholder and does not acquire this tagged ticket-update lock.
The existing repository DatabaseWaiters uses the same contains-style approach.
Independent test review approves this narrow correction; production behavior,
case identities, selector boundaries, coverage floors and all deadlines remain
unchanged. The affected AppHost test project builds locally with zero warnings
and zero errors after the correction.

This is a precise source concern, **not a proven cause of the SQL timeout**: the
failed run retained neither the active-case identity nor the relevant DMV batch
text. Existing mandatory native lock-wait/provider tests must verify the new
candidate. No partition is added solely from this incomplete trace. PR #50's
new current-base CI also has required startup failures; its retirement remains
unaccepted and cannot be borrowed by this PR. All historical/human/release gates
remain open.
