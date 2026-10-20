# Accepted internal administrator issuer/client boundary

**Delivered coherent B2 (20 October 2026):** [PR #66](https://github.com/mosesparadise/OpenIdentityAdmin/pull/66) is squash-merged as `42214382f5b6246332fe9b1ecfe00463a5ed9965` after all 67 exact-head jobs pass. Independent review verifies 3,378/3,378 raw executions, zero skips, all 50 coverage inputs/15 unchanged module floors and 48 mutation kills with passing baseline/restored controls. Feature 011 T005–T026, including the requested T005–T009 batch and its required coherent dependencies, are complete for internal B2. Production BFF login, product cookies, API enforcement, refresh and React remain unactivated.

## Exact accepted source and delivery

[Run 39502898784](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39502898784) verifies source `2a672d25a86501c2c0535c28b8be82884d785aca`, tested merge `ae1592b9cd99b776acffc0ba395e31273fcd16a3`,
and tree `6fc003289adc25fa5d41cb0eeaa54e1081fc4732`. Normal PR #66 squash `42214382f5b6246332fe9b1ecfe00463a5ed9965` has the same tree.
Fresh pre-merge reads confirmed unchanged source/base, all 67 successful commit
checks, a clean merge state, no blocking GitHub review and no unresolved thread.
The normal squash used the exact expected head without a bypass. Its sole parent
is `1ffe5ed1481d065f487f12aeef80657cc955003d`; remote main and the identical
accepted tree were independently read back after merge.
This documentation followup records the completed implementation separately;
its own proportional checks and merge do not replace executable acceptance.

The [acceptance map](b2-acceptance-map.md) links FR-001–013, SC-001–006 and
T005–T026 to actual production ownership and concrete tests. The delivered
boundary combines US1 current-authority admission, US2 fixed registration and
durable replay-safe exchanges, and US3 the persistent native client with isolated
operation. Prior provider/ownership deliveries remain preserved.

## Independently reviewed execution

All 50 raw execution artifacts and 57 TRX
reports reconcile to **3,378/3,378 passing executions**, zero failures/skips.
All 3,285 unique case names reconcile with native identities,
class/method/traits and current source inventories. The final corrected fixture
does not add, remove or rename cases. All **1,241 Administrator source cases**
are represented, including 110 protocol-admission cases/provider. Both foundation
suites pass 23 cases/provider, including PAR/code/state strict-expiry controls.
All 172 Chromium/WebKit executions and 56 contract executions pass.

| Ordinary profile | Executions |
| --- | ---: |
| unit | 1,221 |
| SqlServer | 1,017 |
| PostgreSql | 912 |

Both restore cases now reach their complete oracles: ordinary replay denial,
exact private code/state image restoration, intentional resurrection with real
authentication and token issuance, then persisted scoped invalidation and
callback denial without another token send. Both actual PAR races, all eight
wire quota cases and both separate-process restart cases pass. Existing
governance process controls remain represented.

All 50 coverage inputs were parsed; all 15 modules pass unchanged 90% line/80%
branch floors. Merged XML SHA-256 is `ea95da3837d05acef050a8b3564f17210ebf152a19fcc8eef124dd5f5596a798`;
coverage policy SHA-256 is `0e26c85af81345cfa6f339a6bb6342a5697ec8c413c53f57ab1c8e5aa6587020`.

| Module | Line | Branch |
| --- | ---: | ---: |
| IdentityPlatform.Admin.Api | 100.000% | 100.000% |
| IdentityPlatform.Admin.Bff | 93.315% | 95.312% |
| IdentityPlatform.AppHost | 99.346% | 100.000% |
| IdentityPlatform.AuthServer | 94.835% | 98.837% |
| IdentityPlatform.Core | 98.913% | 95.000% |
| IdentityPlatform.Database.Abstractions | 99.107% | 100.000% |
| IdentityPlatform.Database.Hosting | 95.455% | 100.000% |
| IdentityPlatform.Database.PostgreSql | 100.000% | 100.000% |
| IdentityPlatform.Database.PostgreSql.Hosting | 100.000% | 100.000% |
| IdentityPlatform.Database.SqlServer | 100.000% | 100.000% |
| IdentityPlatform.Database.SqlServer.Hosting | 100.000% | 100.000% |
| IdentityPlatform.Infrastructure | 92.937% | 97.328% |
| IdentityPlatform.Migrator | 98.326% | 100.000% |
| IdentityPlatform.ServiceDefaults | 100.000% | 100.000% |
| IdentityPlatform.Worker | 100.000% | 100.000% |

All eight mutation receipts bind the exact source, recomputed mutant hashes and
named provider controls: **48 kills, 48 passing baseline controls and 48 passing
restored controls**. Raw mutant TRX is not retained; this is receipt/source/hash
verification, not a claim to have inspected unavailable raw mutation reports.
The coverage merge itself was not rerun by the independent reviewer.
[Receipt inventory](b2-evidence-receipt.md) preserves report and coverage hashes.

## Reviews, corrected failures and limits

Independent architecture/test/API and security source reviews approve the coherent
boundary. Final independent raw-evidence review accepts the exact tested source,
required partitions, preserved identities, coverage and mutation receipts.
Local Administrator/Persistence builds, discovery preservation, quality/import
checks, 207 Python controls, instruction integrity and staged secret scans pass;
the final fixture correction additionally preserves discovery byte-for-byte.
No local SQL or Docker experiment was performed.

The preceding run [39500242311](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39500242311)
at `2fe195316cb68c51e8675ef727ff4c7bf666d7db` remains failed: 62 successful jobs,
four failed jobs and skipped aggregate coverage; 3,375/3,378 raw executions pass
with zero skips. The two restore fixtures fail at initial state capture because
they require the server-only storage selector from the owned-only client root.
The narrow optional-selector correction preserves required ownership and every
assertion. The third raw failure is the unchanged-source PostgreSQL WebKit
certificate-negative evidence-wait recurrence. The final pass establishes
current acceptance, not a cause or closure for that historical finding.
See [exact failure/correction record](native-budget-replay-and-restore.md).

The prior green process-restart run 39497238379 passed all 67 jobs and 3,366
executions; it was not substituted for final-source evidence. Earlier failed
and superseded runs remain in the linked implementation records.

Restore tests exercise isolated exact row resurrection and scoped invalidation,
not provider backup restoration, cross-database atomic recovery or production
restore automation. Restart tests prove graceful OS-process replacement, not
forced-crash recovery. Client diagnostic capture is bounded and does not certify
all host logs or arbitrary nested objects. Local response completion is not
atomic remote delivery. Native linked-token revocation does not establish live
Admin API enforcement. The operator runbooks retain missing production
restore/rotation/cleanup automation.

All six historical findings, 48 human rows, 345 not-assessed assurance controls and production/public-release gates remain open. A-004 remains expired and A-005/A-006 concluded. Firefox was deferred only for internal B2 under A-007; T028 must restore it before B3 implementation, activation or release.

## Continuation

**Next bounded step:** T027 prepares B3 request-bound fresh login, product callback/B1 ticket establishment, cookie/CSRF/fixation and accurately bounded logout specifications with three-engine acceptance and issuer-policy dependencies. T027/T028 remain unchecked; this record does not implement or activate B3. Preserve the delivered provider plugins, existing databases and migration histories; do not repeat the accepted ownership or B2 work.
