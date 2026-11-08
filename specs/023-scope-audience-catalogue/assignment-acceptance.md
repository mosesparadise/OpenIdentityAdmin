# Application scope assignment delivery

Feature023 T008 is complete for internal development. T001–T008 are delivered;
T009–T016, M03-B/C completion and all historical/manual/public-release gates remain
open. This increment adds explicit assignment management, not extended issuance.

## Accepted source

- Reviewed source: `7eb152a963bf780a7e1ffd428a69af5ccabdb43c`.
- Tested PR merge: `965965211b7ff66ffa326a755566bd3879df974c`.
- Source, tested merge and squash tree: `046e2ebf3bee27f4648b5932a6458f89e4daed30`.
- [CI568, attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42436022464): all 130 jobs successful, no failed or skipped required job.
- [PR130](https://github.com/mosesparadise/OpenIdentityAdmin/pull/130) normal squash: `41a93fd0e795c195670f4bb27299050e42b50ae2`.
- This later Markdown-only record documents accepted runtime evidence; its proportional checks do not claim another full runtime execution.

## Available behavior and boundary

The existing application editor now shows **Application scopes**: current assignment,
bounded catalogue search/paging, retained selections, explicit replacement confirmation
and deliberate recovery. Empty selection removes additional scopes. Every accepted
save, including an unchanged set, advances the application version once and commits
one ApplicationScopesAssigned audit. Credentials, callbacks, lifecycle, native fixed
permissions and existing grants remain unchanged. No migration is introduced.

The additive versioned Admin API and fixed BFF GET/PUT scopes subresource preserve
current read/write permissions, fresh MFA, native CSRF, exact If-Match, governance
ordering and original browser authority. Current native scope/audience policy and exact
persisted permissions are verified before commit; stale or invalid changes cannot
publish success. Catalogue deletion and assignment serialize on both providers.

**A nonempty assignment currently blocks that application's sign-in and token
issuance, including OpenID-only requests.** The editor explains this before save and
in persisted status. Removing every additional scope restores eligibility for a fresh
sign-in, subject to current application/credential/lifecycle checks. Previous requests
and codes remain invalid. Untouched applications retain their existing behavior.
T009–T011 must deliver policy bindings, consent and native issuance before extended
scope sign-in becomes available. UserInfo remains T012–T014; refresh stays separate.

## Reconciled verification

| Evidence | Result |
| --- | --- |
| Ordinary native reports | 107 reports / 6,157 passing executions; none failed or skipped |
| Behavioral mutation controls | 130 demonstrated kills with successful baseline/restored controls |
| UI / Node contracts | 132 UI cases / 46 Node contract cases |
| Coverage | 104 original inputs / all 15 unchanged module floors |
| Full run receipts | 130 decoded logs / 124 artifact metadata records |

Both real PostgreSQL and SQL Server and retained Chromium/WebKit journeys pass.
Native execution totals include repeated provider/browser runs. All 2,871 prior
administrator discovery records retain their metadata/order, with 52 additions for
2,923 unique cases. Host discovery preserves 1,892 identities and adds 13 for 1,905.
The prior 111 UI cases remain and 21 scope cases are added. The new exact-permission
mutation is killed once per provider only after independently observed persisted
substitution, version advancement and audit; unrelated failures cannot count as kills.

The focused [run42434975233](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42434975233)
at isolated source `6687319` passes 26 cases/provider on Chromium and one case/provider
on WebKit, plus five catalogue/assignment mutation controls/provider. All six artifact
archives were downloaded; original discovery/TRX selections and mutation receipts
were independently reparsed locally. This focused evidence supplements full CI568.

| Controls / requirements | Permanent test groups |
| --- | --- |
| C02/C06, FR007/011 | ApplicationScopeTests, ApplicationScopeDenialTests and genuine direct/BFF ApplicationScopeHttpTests |
| C03/C04, FR007/013 | ApplicationScopeAtomicityTests, ApplicationScopeRaceTests and ApplicationScopeDeletionWaitTests with actual independent native waiters |
| C06/C07, FR007/013 | ManagedScopeAssignmentTests (old request/code closure and unrelated/fresh success), GrantAssignedScopeTests and ordinary editor preservation |
| C06/C13, FR011/012 | ApplicationScopeBoundaryTests, ApplicationScopesTransportTests, ApplicationScopePublicationTests, ApplicationScopeBrowserTests and 21 exact UI cases |
| C14, FR013 | Immutable discovery, both-provider full CI, native exact-permission mutation and 104 original coverage inputs |

These groups establish the bounded assignment increment. They do not complete C07–C12
extended consent/issuance/UserInfo obligations, which remain in later tasks.

Coverage keeps 90% line and 80% branch floors. Branch values are conservative lower
bounds from original method conditions; merged lossy branch summaries are not
authoritative. Hosted validators bind reports, source/tree/run/attempt, collector
settings and build DLL/PDB copies. This is CI build-copy provenance, not runtime
loaded-module attestation.

| Module | Lines covered/total | Branch lower bound/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 452/455 | 379/397 |
| IdentityPlatform.Infrastructure | 9176/9820 | 4427/5232 |
| IdentityPlatform.ServiceDefaults | 70/70 | 43/44 |
| IdentityPlatform.Migrator | 608/616 | 319/366 |
| IdentityPlatform.AuthServer | 6895/7363 | 5362/6700 |
| IdentityPlatform.Admin.Api | 1825/1958 | 1089/1331 |
| IdentityPlatform.Admin.Bff | 4625/4887 | 3479/4135 |
| IdentityPlatform.Worker | 94/96 | 8/8 |
| IdentityPlatform.AppHost | 531/547 | 220/250 |
| IdentityPlatform.Database.Abstractions | 111/112 | 71/78 |
| IdentityPlatform.Database.PostgreSql | 96/96 | 12/12 |
| IdentityPlatform.Database.SqlServer | 93/93 | 16/16 |
| IdentityPlatform.Database.Hosting | 42/44 | 14/16 |
| IdentityPlatform.Database.PostgreSql.Hosting | 13/13 | 0/0 |
| IdentityPlatform.Database.SqlServer.Hosting | 13/13 | 0/0 |

Coverage-policy SHA-256: `0e26c85af81345cfa6f339a6bb6342a5697ec8c413c53f57ab1c8e5aa6587020`.

| Local receipt | SHA-256 |
| --- | --- |
| Full decoded-log reconciliation | `4d9e07d58abb753f9dffaf5f913ef3b804bc40ba397be803fafb42d49dc087f6` |
| Terminal job metadata | `11fdbc2e72744bc0ad86871e15031ac20cac541dc821cc4684ce346492fde36c` |
| Artifact identity/digest metadata | `6d2e17f83c96a47535c813c03817b1c93e51ba5dc110ad945bd222bc3e1c7aea` |
| Focused raw-report reconciliation | `d322eef054b017e644ca1cac9aa1ce99395868261426fa99dbb39895d6b8940c` |
| Full mutation archive receipt review | `37ff6012110c7f38dd66141805814faba049c72786860b9ecc944425333d7f79` |

Full-run original TRX/coverage inputs were validated by source-bound hosted
validators; uploaded ZIP identities/digests were reconciled from metadata. These
full-run payloads were not all downloaded and reparsed locally. Nonmutation artifact
digests are reconciled metadata, not locally recomputed archive hashes. Focused raw
artifacts were separately reparsed as stated above. All 16 small full-run mutation
archives were downloaded and their ZIP hashes
matched upload metadata. Their 22 sanitized suite/provider receipts reconcile all 130
kills, baseline/restored identities and original/transformed source hashes. Underlying
mutant TRXs are not retained, so this is receipt validation rather than independent
reparsing of those mutant TRXs. Seven-day hosted retention and local
workspace receipts are not a permanent external archive.

## Reviews and preserved failures

Independent architecture/API/security, UX and test/evidence reviews pass the bounded
increment. Findings corrected bounded detail reads, asynchronous expiry checks,
unknown-commit wording, focus ownership, actual native contention observation,
persisted race oracles and mutation attribution. Runtime tests verify the corrections.

Focused run42434182462 preserves all four failed browser receipts. A reused
secret-erasure helper deliberately cleared the page session; the corrected journey
uses fresh normal navigation and also proves stale-save recovery. The product guard
was preserved. CI567 at c65d43d failed test-import formatting; its superseded terminal
receipt is 28 success/2 failure/100 cancelled. CI568 runs the entire matrix anew after
the import-only correction; earlier successes are not mixed into final acceptance.
Earlier local failed build, transport-expectation and missing-tool attempts remain
recorded in [assignment.md](assignment.md). Branch139's isolated validation workflow
is excluded from the product and must not be merged. Preserve prior frozen producers,
branches and failed evidence.

Firefox and the two exact WebKit issuer-certificate evidence cases remain deferred
under existing A-009. All six historical findings, human, Windows, refresh, vendor,
production and public-release gates remain open. No new waiver is introduced.
Use the [ordinary Aspire walkthrough](quickstart.md); no custom PowerShell is needed.
