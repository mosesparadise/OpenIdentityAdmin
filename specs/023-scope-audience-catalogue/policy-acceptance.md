# Managed catalogue policy binding delivery

Feature023 T009 completes the internal-development policy-binding prerequisite.
T010–T016, M03-B/C completion and all historical/manual/public-release gates remain open.

## Accepted source

- Reviewed source: `4ae6c03e56ab9ca6377b6dea2537f7f405e6b146`.
- Tested PR merge: `7f20699f6c7451bf2efe3f9750da283c1362b523`.
- Source/tested tree: `91aea115d22aa653ff217e9aa07e70d9e78cbd1f`.
- [CI581](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43542395165): all 130 required jobs successful on attempt 1; no failed or skipped required job.
- [PR132](https://github.com/mosesparadise/OpenIdentityAdmin/pull/132): normal reviewed squash `7d4fe56e163f4a6c6133842aa7d4e7c69bdd093f`; its tree equals the reviewed/tested tree above.

## Behavior and boundary

Managed interactions now retain bounded canonical policy snapshots, digests and
scope/audience references. Current native permissions/resources and managed catalogue
metadata determine each decision under existing governance serialization. Changes to
policy, consent metadata, mappings or authority invalidate the captured decision.

New OpenID-only reservations carry canonical bindings. Legacy interactions preserve
both nullable fields and remain usable only under the existing exact OpenID profile.
Malformed or substituted policy cannot fall back to legacy. Immutable save checks and
persisted reference checks protect every current public managed flow.

Shared catalogue reference decisions protect retained scope/audience identities in
read responses and deletion. Cleanup removes references only with an eligible terminal
parent in the owning transaction. Both provider migrations preserve existing data and
histories; runtime grants and generic provider registration remain intact.

**Nonempty application scope assignments still block sign-in and token issuance.**
T010 introduces consent integration and T011 extends native issuance. T012–T014 cover
UserInfo and complete journeys. No additional public claim, scope or audience is enabled
by this increment, and no approval is inferred from a migrated legacy row.

Aspire SDK/hosting moves from 13.6.4 to 13.6.5 as a required advisory-fixing restore
prerequisite, with compatible locked transitive patches and unchanged audit enforcement.
Playwright remains 1.63.0 under the existing compatibility exception.

## Verification

| Evidence | Result |
| --- | --- |
| Ordinary native reports | 107 reports / 6,181 passing executions; none failed or skipped |
| Behavioral mutation controls | 134 demonstrated kills with successful baseline/restored controls |
| UI / Node contracts | 132 UI cases / 46 Node contract cases |
| Coverage | 104 original inputs / all 15 unchanged module floors |
| Full run receipts | 130 decoded logs / 124 original artifact archives |

Both real PostgreSQL and SQL Server pass, including 40 persistence cases per provider.
Retained Chromium/WebKit journeys pass; all four BrowserLogin cells pass nine cases.
Execution totals include repeated provider/browser runs. Administrator discovery
preserves all 2,923 earlier identities and adds ten; Persistence preserves 76 and
adds four; Host preserves 1,905 and adds ten. Native metadata/order remains intact.
Two new policy mutations are killed on each provider, adding four to the earlier 130.

All 124 original ZIPs were downloaded; archive SHA-256, size, unique identity, owner,
run and source bindings were checked. Native TRX payloads were reparsed for exact
report ownership, member path, counters, unique execution IDs and passing outcomes.
Exact test-selection identities additionally rely on the source-bound hosted validators.
The 104 original coverage XMLs and identities were reparsed; their line union matches
the merged report, and independently recomputed conservative branch bounds match the
hosted result. Each report binds the tested merge/tree, run/attempt, collector settings
and build DLL/PDB copies. This is CI build-copy provenance, not runtime loaded-module
attestation. Merged lossy branch summaries are not authoritative.

All 16 mutation archives were reparsed into 22 sanitized suite/provider receipts:
source and transformed-source hashes, exact administrator critical witnesses,
demonstrated bypass kills, baseline/restored controls and source restoration reconcile.
Issuer witness selection additionally relies on the source-bound hosted validator. Underlying mutant
TRXs are not retained; this is receipt validation, not reparsing those mutant TRXs.
UI/Node counts and identity checks are reconciled from source-bound hosted validators
and decoded logs. Seven-day hosted retention and local workspace copies are not a
permanent external evidence archive.

This Markdown-only follow-up records accepted runtime evidence; its proportional
checks are not another full runtime execution.

| Controls / requirements | Permanent test groups |
| --- | --- |
| C05/C07/C08, FR007/008/011/013 | ManagedScopeCodecTests and ManagedScopePolicyTests: canonical bounded input, exact current native policy and metadata drift |
| C07/C08 | ManagedScopeBindingTests, ManagedScopeLegacyTests and existing ManagedHumanAtomicityTests: actual public rejection, genuine legacy journey and save-time rollback |
| C05/C08 | ManagedScopeCleanupTests and CatalogueManagedReferenceTests: retained identity, eligible cleanup and explicit deletion refusal |
| C05/C14 | ManagedScopeSchemaTests, ManagedScopeUpgradeTests, CatalogueRuntimeTests and existing provider compatibility suites: real restrictive schema, populated upgrades and least-privilege runtime access |
| C14 | Preserved discovery identities, both providers, retained browsers, two additional causal policy mutations and unchanged coverage floors |


## Coverage and receipt bindings

All unchanged floors require 90% lines and 80% conservative branch coverage.

| Module | Lines covered/total | Branch lower bound/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 452/455 | 379/397 |
| IdentityPlatform.Infrastructure | 9389/10041 | 4555/5392 |
| IdentityPlatform.ServiceDefaults | 70/70 | 43/44 |
| IdentityPlatform.Migrator | 608/616 | 319/366 |
| IdentityPlatform.AuthServer | 6897/7365 | 5362/6700 |
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
| run-final.json | `363eb7acee21c7d65fe3bc561408f558249e40491da00905030ede6ab140b921` |
| jobs-final.json | `b7a78daed21cf334cd3f3974fbfafe0e1daeece51ac6d73235539e325029cd5f` |
| artifacts-final.json | `0306b1e17f046a60eb47e57d897e48c84c9b07121e916523c5e2e0e4ae9be2b3` |
| independent-log-review.json | `341e99949d4ee925c874afc2e8fcb450e59103e0543c2f5cfc9e6a68de8a0c63` |
| raw-archive-review.json | `8b4d3452cb0615bf0046b1fbc27d438ed12fe580aed035d7d74e5077835eec46` |
| mutation-archive-review.json | `c78017d08044d111638b8dcdd032ed897a2c6b6f1bdb119fd2db231f6146f452` |

## Corrections and preserved evidence

CI578 exposed missing provider migration-history entries, an old fixture seeding the
new EF shape against the predecessor schema, and SDK import-format errors. CI579
confirmed the migration catalog correction and exposed unsupported `DBNull` parameter
mapping in the historical fixture. The fixture now parameterizes all 29 original fields
and preserves CLR nulls through EF's supported interpolated SQL path.

CI580 passed all 40 persistence cases on each provider. PostgreSQL/WebKit timed out in
foreign-account preparation before the account-substitution check. The corrected helper
requires DOM readiness, exact protected response/page URLs, HTTP 200 and the fixed
security heading. It preserves one reload, real password/MFA, native session/cookie
proofs, all substitution/rejection assertions, test identities and deadlines. The
original timeout does not establish an upstream browser defect. No retry, skip or new
browser deferral was added; all four nine-case BrowserLogin cells remain required.

All rejected or superseded candidate receipts remain distinct from final acceptance.
Architecture/security and test reviews found no remaining blocking source issue in the
corrected candidate. AI source review does not replace independent human assessment.

Original connector file references download through the native file tool. Earlier
direct-URL HTTP403 observations remain recorded; no environment network change is needed.
Local provider startup/full-solution copying failures remain failed disk-capacity
receipts. Hosted evidence does not retroactively convert those attempts into passes.

## Remaining work

Next: T010 requested/granted decisions and explicit consent disclosure, with current
policy checks and real cache/race evidence. Extended issuance remains T011. All six
historical findings and human, Windows, Firefox, A-009, refresh, vendor, production and
public-release gates remain open. This is internal-development acceptance only.
