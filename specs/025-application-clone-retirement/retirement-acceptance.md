# Permanent retirement acceptance — 15 November 2026

Accepted for internal development. [PR139](https://github.com/mosesparadise/OpenIdentityAdmin/pull/139)
was normally squash-merged as `92849d2bb7ec37c13d9e7f6be8f7abf3f23b1466` at 2026-11-15T06:08:06Z after
independent review and all required checks. Together with [clone PR138](clone-acceptance.md),
this completes the selected M04-A clone and controlled-delete journeys for the existing
managed confidential web profile. Delete means the user-selected permanent retirement with
history retained. This documentation follow-up completes the delivery records; its proportional
checks do not represent another runtime execution or external release acceptance.

## Delivered behavior

Retirement immediately prevents new issuance and current-authority UserInfo, removes the
application from ordinary listings before pagination, reserves its client ID and retains
native identity, consent, audit and clone history. Durable grant cleanup remains observable
through the separate Retired applications view after reload, re-login or a lost admission
response. Stopped cleanup requires explicit fresh approval and cannot undo retirement.

Admission requires current application-disable and grant-revocation permissions under one
authority epoch, fresh MFA, the current application version and an exact idempotency key.
Terminal policy, version, immutable receipt, original approval/job and audit commit atomically.
Exact replay remains bound to the original actor, intent and version with current authority.
Every incomplete prior grant job conflicts. Current worker authority and explicit continuation
preserve the original job and immutable approval history. Additive migrations preserve populated
existing data on PostgreSQL and SQL Server.

## Exact source and evidence

- Feature head: `7608a12b8632d4894540fc927591ea8c3cb7f691`.
- Base: `0366f5f1ebdca7797edd6dfd6954715b0545dd63`.
- Tested merge: `1e6a35fa6d20d42a08e6d1d7881724f45745cbf6`.
- Source, tested merge and squash tree: `a27273b247fba10a842de959929ec4c4e5093eb0`.
- [CI618](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43680949577),
  run `43680949577`, attempt 1: all 149 jobs pass.

All 143 original archives / 50,664,240 compressed bytes were retained
and independently checked against authenticated IDs, sizes and SHA-256 digests, ZIP integrity,
safe member paths and source/run bindings. The exact job/upload sets derive from the pinned
workflow. All 123 ordinary native reports reconcile to 6,599 passing executions, with no failed,
missing or skipped required case. The separate synthetic contrast control passes outside that
total. All prior identities remain; the administrator inventory has 3,119 identities. Applications
has 140 registration plus 39 retirement cases per provider. T032 originally split 140 + 36;
T036 subsequently added three publication cases per provider, yielding the final 140 + 39.
BrowserProtection retains the mandatory 15 Sessions + 21 Applications + 2 Retirement groups.

Host passes 2,123 cases in 153.551567 seconds against its unchanged 180-second bound, with
peak concurrency eight and zero overlap for all 15 sample-exclusive and nine telemetry-exclusive
cases. All 16 affected populated-upgrade cases pass; six reviewed upgrade-oracle hashes remain
unchanged. All eight retirement recovery and eight sample browser executions pass across both
providers and Chromium/WebKit. The independent scoped browser review covers 14 original archives
and 114 passing executions, including retained Applications and regression cells.

Six real held-response publication cases prove successful positive controls and exact sanitized
401 denial after browser-generation loss or original useful-lifetime expiry following genuine
API retirement commit. The original receipt/job/audit/native snapshot survives, one downstream
send remains, and an explicit authorized read recovers the same job. Both provider originals
execute the post-upstream denial body. This closes the CI616 verification gap on fresh evidence.

UI source/manifests/XML/TAP reconcile to 149 cases and unchanged critical-module floors; Node
contracts reconcile to 47 cases. Native contracts pass 44 core and seven cases per retained
browser. Five exports match committed contracts; six zero-socket receipts and 32 browser
operations retain exact contract/security bindings. Dependency evidence reconciles 104 UI and
76 contract npm entries plus 265 NuGet package records across 33 projects, with no reported
vulnerabilities in those active inventories. Candidate viewer and legal limits remain open.

Nineteen mutation archives contain 25 receipts and 161 demonstrated causal kills, with 161
baseline and 161 restored passes, zero skips, exact witness/source/transformed hashes and
successful owning jobs. Local retirement calibration remains separate corroboration.

## Coverage and review receipts

All 120 original coverage inputs and before/after snapshots bind to the exact merge/tree/run
and collector configuration. Independent conservative branch calculations reproduce the hosted
receipt. The merged full line union matches: 27,947 covered of 29,864 observed line points in
19 observed modules. All 17 required modules retain 90% line / 80% conservative branch floors.
Zero-branch populations do not establish exercised branches. No exclusion or floor was relaxed.

| Required module | Lines covered/total | Conservative branches covered/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 468/471 | 385/403 |
| IdentityPlatform.Infrastructure | 9854/10543 | 4858/5783 |
| IdentityPlatform.ServiceDefaults | 70/70 | 43/44 |
| IdentityPlatform.Migrator | 608/616 | 319/366 |
| IdentityPlatform.AuthServer | 7294/7786 | 5757/7178 |
| IdentityPlatform.Admin.Api | 1994/2150 | 1189/1470 |
| IdentityPlatform.Admin.Bff | 4816/5086 | 3655/4347 |
| IdentityPlatform.Worker | 94/96 | 8/8 |
| IdentityPlatform.AppHost | 670/695 | 313/368 |
| IdentityPlatform.Database.Abstractions | 111/112 | 71/78 |
| IdentityPlatform.Database.PostgreSql | 96/96 | 12/12 |
| IdentityPlatform.Database.SqlServer | 93/93 | 16/16 |
| IdentityPlatform.Database.Hosting | 42/44 | 14/16 |
| IdentityPlatform.Database.PostgreSql.Hosting | 13/13 | 0/0 |
| IdentityPlatform.Database.SqlServer.Hosting | 13/13 | 0/0 |
| IdentityPlatform.Sample.Client | 965/1046 | 536/616 |
| IdentityPlatform.Sample.Api | 119/124 | 58/68 |

The retained evidence package contains source, original archives, authenticated metadata,
reconciliation scripts, review receipts and actual premerge/delivery bindings. Final receipt
SHA-256 values:

| Review | SHA-256 |
| --- | --- |
| Source | `eeaf4f0802cb55a5cb1d71b64d7d4a1f13c7036b06ea2fae92d58e432ee9d991` |
| Ownership and authority | `c44b8f041b1c774cf224cde6669ef72e6c25851bb80310d6956f15ca2bd5ddb6` |
| Native identities and results | `8baa17cb093ecfe3b301a28f25cc4b08722c37d9c244f424d2a07c442ffecdb1` |
| Contracts/UI/supply | `0c8d688b996d05f9982efb509cb1d6cfbda678842eb6fd5be26a2f3bc7f6e368` |
| Browser originals | `235876774454e71cad1ac4f9e8cf8408a522576a2f6eb1ef21cb729ef681a9e0` |
| Publication boundary | `df537bf72a116bd518b6ee1aa611203bb0cd6a20ea1788698fdd5f386a4f38b4` |
| Coverage | `7c0c740fda441613a2bad2559f16defb361db9480f923f5a318790c7f5084740` |
| Mutations | `b47f7e46469945c2aa9c86e6f4e299e55f8f05ef1e42c93f352b978bf8c3d108` |
| Technical convergence | `c623e51f9aa5e3f81b659ce413df043ac624eb02f1b2925af9795ef16e667fbf` |
| Holistic technical acceptance | `989d945b9e9ddf7baac0985bd23479879b159b3cdb1efdc25effed5ab172c235` |

The final premerge check verified all 149 exact-head successful checks, no review threads or
review submissions, the unchanged base, clean merge state and active Protect Main rules.
The normal expected-head squash has the reviewed tree and exactly one parent, the recorded
base. No branch restriction was bypassed. Technical convergence covers 14 functional
requirements, five success criteria, 11 acceptance scenarios, 23 constitution principles and
all 36 tasks; this documentation follow-up closes the actual delivery records.

## Qualifications and remaining work

CI614 and CI615 remain failed/superseded evidence, with their source-scanner, batch-growth,
browser-readiness and retirement history-oracle findings retained in [verification.md](verification.md).
CI616 passed execution but holistic acceptance was withheld for missing post-upstream
publication negatives. T036 closes that specific gap; no production defect was demonstrated.

CI617 remains failed: one unchanged PostgreSQL/WebKit sample clone journey timed out waiting
for the exact sample destination after consent. Its complete original report has one pass and
one failure. No retained timeline establishes the cause. A single unchanged local comparison
never started because pinned WebKit download was blocked by HTTP 403. Original failure,
terminal metadata, all 142 produced archives and independent failed-run reviews are retained.
CI618 passes both cases on the same executable source and unchanged deadlines; this does not
repair CI617, demonstrate a fix, explain its cause or establish general suite stability.
Preserve earlier clone, Host and pre-feature025 reliability history and diagnostic limits.

Hosted mutant raw TRXs/discovery are not retained by the normal harness; causal interpretation
relies on the reviewed source-bound producer and sanitized receipts. Local retirement
calibration likewise retained receipts and source restoration hashes rather than raw temporary
mutant TRXs. Build-copy provenance does not attest runtime-loaded modules. The browser wrapper
retires an already-disabled target; genuine active-retirement UserInfo/issuance enforcement is
proved separately by provider tests. The re-login oracle proves exact administrator-owned row
population/status and original target/peer preservation, not a single linked grant family.
Automated axe/reflow/focus evidence does not replace qualified human accessibility assessment.
Workspace copies and finite hosted retention are not a permanent external archive.

Already-issued offline JWTs can remain valid until expiry. Retirement does not erase remote
relying-party sessions. Physical erasure and a new retention policy are outside the selected
scope. Windows/manual walkthroughs, Firefox/A-009, six historical findings, dormant refresh,
viewer/vendor/legal clearance, independent security assessment, certification and production/
public-release authorization remain open. No diagnostic approval is renewed. M04-B/C's broader
client profiles and credentials, protected documentation and M05-A general user administration
remain separate roadmap work.
