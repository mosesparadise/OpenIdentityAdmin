# BFF ticket store: accepted internal B1 delivery

15 October 2026 (Europe/London). [PR #52](https://github.com/mosesparadise/OpenIdentityAdmin/pull/52) was normally squash-merged as `ca410c01a161032c1f09e2bd92439cb48b046476`. Feature 010's internal B1 store is delivered. B2 issuer/client integration, B3 browser journeys and M02-C/D/E remain open; production composition is dormant.

## Exact accepted candidate

[Run 38769486233](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38769486233), attempt 1, passed **all 70 jobs** at head `bc84ec7846afeaf8b4cc104ac1928efb2a34bd92`. Tested merge `2121db07b1897827d0ff16f8ad64879387889137` has parents `bb37751077d9e4084b6466f4c2f8a1f5a2230bef` and that head. Head, tested merge and delivered squash share tree `b3a4d15fdf40b7992b9ea0f25b25034712e5c8cb`. The squash parent is accepted main `bb377510`. Final acceptance explicitly passed full scope; fresh remote checks found a clean merge, no outstanding submitted review or repository restriction to bypass.

The store supplies protected native tickets behind fresh opaque handles, dedicated encrypted BFF key ownership, both-provider additive migrations/session-only grants, bounded immutable lifetimes, revision-based renewal, permanent local revocation, bounded cleanup and atomic minimal audit intent. Shared protection mechanics preserve separate issuer ownership. No browser login/callback/proxy, management route, refresh lifecycle or remote logout is activated. A stored ticket is not current administrator authorization. Operations and restore invalidation remain in the [runbook](../../docs/operations/bff-session-store.md).

PR #50's accepted image preparation and actual witness-exception retirement are integrated. BFF extraction retires 32 obsolete legacy entries; 1,794 legacy records and zero exceptions remain. No allowance is renewed.

## Native and coverage reconciliation

Independent architecture/test review checked the downloaded raw TRX counters, unique result/definition/execution-entry bindings and exact discovered case names. Units passed **484/484** (Core 25, Host 393, Architecture 5, AppHost 61). Each real provider passed **19/19**: Persistence 4, AppHost Foundation 8 and BffSessions 7, with zero skips/errors. All seven BFF cases passed on both PostgreSQL and SQL Server. The three previously unreported SQL cases now passed: durability/restart 37.690 seconds, atomic write failures 37.617 seconds and cancellation/database loss 81.729 seconds.

Mandatory disjoint/exhaustive eight-plus-seven grouping retains every native identity, each 660-second invocation bound, the shared coverage collector and unchanged 20-minute job. Actual PostgreSQL and SQL jobs took 636 and 888 seconds respectively. This proves this run completed; two maximum invocations are not guaranteed to fit the aggregate budget. Hosted capacity supplied acceptance; local Docker storage remains insufficient.

| Artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| foundation-coverage | 11992101480 | `27085e35719352b27d84b57a5502fab2aa5a1e4b576b5b2cf1a169308e2e3f21` |
| foundation-unit | 11991620036 | `e4b30894492fb25bdba1cbb816aa9fa533daca43407a3b0a21c2cdfff80afbc4` |
| foundation-PostgreSql | 11992250511 | `bf3b1c27975b12716e3f139563da0bd2cdefb3e91f7f2bc214064d281c71ff60` |
| foundation-SqlServer | 11990999128 | `7123103ccb0a536c1652ff4624e90b6c80c11bceb436a42854a7be75ea0affdf` |

All **55 required coverage inputs** are present. Aggregate XML SHA-256 is `19bcdc6d7d57f2cf45f0fcead31888da636328c0d7d2c31965d1f842169a4a0c`. All 20 changed surviving handwritten C# source files appear in coverage. All nine modules pass the unchanged **90% line / 80% branch** floors; exclusions are unchanged.

| Module | Lines covered/total | Branches covered/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 93/94 | 14/14 |
| IdentityPlatform.Infrastructure | 3158/3339 | 377/387 |
| IdentityPlatform.ServiceDefaults | 61/61 | 18/18 |
| IdentityPlatform.Migrator | 386/390 | 30/30 |
| IdentityPlatform.AuthServer | 1749/1795 | 188/188 |
| IdentityPlatform.Admin.Api | 70/70 | 10/10 |
| IdentityPlatform.Admin.Bff | 470/495 | 166/174 |
| IdentityPlatform.Worker | 25/25 | 0/0 |
| IdentityPlatform.AppHost | 187/188 | 14/14 |

## Security and existing mutation evidence

Independent security review reconciled all six downloaded current mutation ZIPs: **42 kills, 42 baseline passes and 42 restored passes**, zero skips. Run/head/tested-merge bindings, manifests, source hashes, recomputed mutant hashes and fixed native methods/provider arguments agree. All 41 verifier/manifest/target/test files remain byte-identical to accepted PR #50. These are existing issuer/administrator/governance mutations, **not BFF-specific mutation coverage**.

The ZIPs contain sanitized verifier receipts, not raw mutation TRX. The unchanged verifier enforces native exit/marker/whole-run and discovery-restoration checks; opaque native UIDs are not retained in receipts. BFF native behavior and coverage are separately established above.

| Suite | Provider | Kills | Artifact | ZIP SHA-256 |
| --- | --- | ---: | --- | --- |
| governance | SqlServer | 6 | 11992190528 | `71c10731d40f17cdaa643734383d8f03a157fd2a345b4a9dff8ce1e7e7178bfe` |
| administrator | SqlServer | 8 | 11992180666 | `076a647e587feded3bba68d09eac5a12429d656439d9422923db5af48e0ca9a3` |
| governance | PostgreSql | 6 | 11991615693 | `255673f69bdb944b47de5853f59493a5c245b997874f586d34325a84c4003464` |
| issuer | SqlServer | 7 | 11991551269 | `4ddfab6cc2488fba1145de3bab279ca79a8c212db54de5deb71525072867a4d1` |
| issuer | PostgreSql | 7 | 11990923429 | `a13309d4977ff1ea936939d8bef971c3449e0958b060da2fdc42a1ddde733c8c` |
| administrator | PostgreSql | 8 | 11990593699 | `6cf0784c6eb931ebac3ad6c75fa85a9dded90beeb39b2d578f9392cf707b3362` |

Independent architecture/test and security source, integration and final evidence reviews passed. No source changed between those final reviews and squash. Prior failed runs `38740538365`, `38745940575` and `38750400030`, local storage failures and their actual limits remain in [verification](verification.md); current success supplies no sole-cause or historical incident closure.

## Task disposition and next product step

Feature 010 T011, T014–T016, T020 and T023 now have both-provider/full-CI acceptance. T021's implementation squash is complete and this delivery packet updates the roadmap, handoff and task register. T022 remains open: prepare the distinct administrator issuer/client integration specification, fixed manager-backed client/scope/audience, current-MFA admission, pinned native code/S256/PAR/`private_key_jwt` support and both-provider denial/replay matrix. Do not broaden pilot eligibility or infer authority from ticket storage.

B2/B3/C/D/E, authenticated actor attribution, audit retention/export, remote OAuth revocation, refresh serialization, five-second live API enforcement and React remain separate work. All six historical findings, 48 human assessment rows and activation/public-release gates remain open. A-004 stays expired; A-005/A-006 concluded. Preserve diagnostic branch `046-governance-diagnostic-proposal` at `3699718603d90665117e384128803aa5f75ad12e`; the retired WebKit diagnostic branch must not merge or relaunch.

This prose delivery packet requires its own exact-head proportional checks and normal squash; its PR records those results. It does not rerun or replace the accepted executable evidence above.
