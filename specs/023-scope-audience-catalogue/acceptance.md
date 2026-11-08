# Scope and audience catalogue delivery

Feature023 T001–T007 are complete for internal development. This delivers M03-A
catalogue management through the existing Aspire workspace. Application assignment,
consent integration, token claims and UserInfo remain T008–T014; M03-B/C and the
feature-wide T015/T016 integration obligations remain open.

## Accepted source and delivery

- Reviewed source: `932a71d1e071c3b07abb81f03a9d91f2303c0750`.
- Tested PR merge: `4d25cde45f5ee6c845ddb1f357125f702a50f388`; tree `1ac01d3c16bcb46e4836fedfe8ee9b8fb333ab2a` equals the reviewed source and resulting squash tree.
- [CI563, attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42427424538): all 130 jobs successful, no failed or skipped required job.
- [PR128](https://github.com/mosesparadise/OpenIdentityAdmin/pull/128) normal squash: `57c11e395d92a3e7d62e64c48a5345fa5c5c0cd2`.
- This later Markdown record documents that accepted runtime source. Its proportional documentation checks do not claim another full runtime execution.

## Available behavior

The **Scopes** and **Audiences** screens provide bounded search, list/details,
creation/editing and explicit deletion with durable status/recovery. The versioned
Admin API and fixed BFF preserve current permissions, native CSRF, fresh-MFA writes,
strong versions, original approval deadlines, current and retained references, atomic
audits and cleanup before publication. OAuth tokens remain server-side. Existing
application management and ordinary Aspire startup are preserved.

PostgreSQL migration `20261107191539_ScopeAudienceCatalogue` and SQL Server migration
`20261107191542_ScopeAudienceCatalogue` are additive. Both providers pass fresh and
populated upgrade, history/data preservation, concurrency and restricted-runtime
checks. Use the normal Migrator lifecycle; this record authorizes no production migration.

## Reconciled verification

| Evidence | Actual result |
| --- | --- |
| Ordinary native reports | 107 reports; 6,090 passing executions; no failed or skipped selected case |
| Behavioral mutation controls | 128 kills, with matching baseline/restored controls and restored source hashes |
| UI / Node contracts | 111 UI cases / 46 Node contract cases |
| Coverage | 104 original report inputs; all 15 unchanged module floors pass |
| Retained run evidence | 130 decoded job logs; metadata for 124 uploaded artifacts |

Native executions include provider/browser repetitions, not unique test counts.
The focused contrast experiment adds one passing execution outside the ordinary total.
All seven native browser contracts execute per retained engine. The two exact WebKit
issuer certificate evidence cases and Firefox remain deferred under the existing A-009
policy; no new deferral or skipped-case acceptance is introduced.

The four new catalogue mutation controls demonstrate deletion after original expiry,
deletion despite a retained revoked grant, false completion without transaction commit,
and publication without current browser-authority reread. Both providers kill all four
with successful baseline/restored controls; unrelated failures cannot earn a kill.

## Coverage and evidence limits

Every module retains 90% line and 80% branch floors. Branch figures below are conservative
lower bounds from original method conditions; lossy merged branch summaries are not
authoritative. Hosted validators bind original report hashes, source/tree/run/attempt,
collector settings and build DLL/PDB copies. This is trusted CI build-copy provenance,
not runtime loaded-module attestation.

| Module | Lines covered/total | Branch lower bound/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 444/448 | 373/391 |
| IdentityPlatform.Infrastructure | 9092/9742 | 4380/5191 |
| IdentityPlatform.ServiceDefaults | 70/70 | 43/44 |
| IdentityPlatform.Migrator | 608/616 | 319/366 |
| IdentityPlatform.AuthServer | 6895/7363 | 5362/6700 |
| IdentityPlatform.Admin.Api | 1746/1876 | 1040/1266 |
| IdentityPlatform.Admin.Bff | 4515/4767 | 3363/3992 |
| IdentityPlatform.Worker | 95/96 | 8/8 |
| IdentityPlatform.AppHost | 531/547 | 220/250 |
| IdentityPlatform.Database.Abstractions | 111/112 | 71/78 |
| IdentityPlatform.Database.PostgreSql | 96/96 | 12/12 |
| IdentityPlatform.Database.SqlServer | 93/93 | 16/16 |
| IdentityPlatform.Database.Hosting | 42/44 | 14/16 |
| IdentityPlatform.Database.PostgreSql.Hosting | 13/13 | 0/0 |
| IdentityPlatform.Database.SqlServer.Hosting | 13/13 | 0/0 |

Coverage-policy SHA-256: `0e26c85af81345cfa6f339a6bb6342a5697ec8c413c53f57ab1c8e5aa6587020`.
Collector-settings SHA-256: `6f7e7be9c12ecf74a7c7fa196f5aeaf15eb86975fb66cad29e025c3e255ce1ae`.

| Retained local receipt | SHA-256 |
| --- | --- |
| Independent decoded-log reconciliation | `e7fca07507ca2e79d7acaeaf33fbed3a7d71eb8647964f841a76346f84782638` |
| Terminal job metadata | `8b661dd9936363851ab343fb5bbaebe03bd7f2aed72e22b2d682d881d2b6cb98` |
| Artifact identity/digest metadata | `e1ce5682d4e7bc3ad991be67d3c92e928f23243dc6bcc10ea34ca942da4e4dfc` |
| Focused retained-browser review | `2fde904d933c98d5ec637fae7c92cb1516a87380d5eab8d205f5d2d7356b86b3` |
| Corrected cross-site logout review | `8d671abdfd47deca492f83e2a8cd27fbc8fb740f33ca23cc0f53359de25e2012` |

Independent architecture/API/security, UX and test/evidence reviews pass this increment.
The test review independently reconciles decoded logs and API metadata with the exact
source-bound hosted discovery/TRX and original-coverage validators. Raw ZIP/TRX/coverage
payloads were not downloaded and independently reparsed locally: their file-URL retrieval
route was blocked. Artifact digests are recorded metadata, not locally recomputed archive
digests. Seven-day hosted retention is temporary; local receipts are not a permanent
external archive. No broader assurance or public-release acceptance is implied.

## Browser correction and preserved failures

The final focused [four-cell run 42420821014](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42420821014)
passes Chromium/WebKit on PostgreSQL/SQL Server with exact hosted discovery/TRX matching.
The real native select retains labels, ARIA and keyboard behavior while bounded overflow,
custom appearance and a decorative theme-aware arrow resolve measured WebKit overflow.
All original reflow, touch, theme, keyboard, focus and axe assertions remain.

Cross-site logout observes an exact native main-frame POST and the same request's clean
completion, with one submission and no redirect. Empty denial responses do not require
an error document to render. Failed, missing, premature, duplicate or unrelated terminal
evidence is rejected; every original denial, durable-state, cookie and subsequent genuine
logout assertion remains. The diagnostic completed all original assertions in all four
cells but did not reproduce CI562's timeout or establish a WebKit cause. Local exact-source
controls reject independently seeded failure-handling and destination-matching defects.
Corrected [run42427434039](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42427434039)
passes the complete original scenario on both providers and retained browsers, with
exact hosted discovery/TRX matching. Its src/tests/eng trees match the accepted source.

Failed native attempts, CI557's import error, superseded CI558/559, CI560's protocol-suite timeout, CI561’s rejected pilot witness failure, CI562’s cross-site logout timeout, failed WebKit runs
and intentionally failing diagnostic measurements remain in [verification](verification.md).
Branch136's isolated workflow and temporary probes are excluded from the product.

Use the [ordinary Aspire walkthrough](quickstart.md). Catalogue entries alone do not
grant application scopes or change tokens. All six historical findings and human,
Windows, Firefox, A-009, refresh, vendor, production and public-release gates remain open.
