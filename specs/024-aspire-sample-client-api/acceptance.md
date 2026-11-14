# Interactive Aspire sample delivery

Feature024 T001–T013 is delivered for internal development through
[PR136](https://github.com/mosesparadise/OpenIdentityAdmin/pull/136).
This reviewed documentation follow-up completes T014.
This delivers the selected M04-D integration example, not the whole application
roadmap or production readiness. The documentation follow-up records acceptance;
its proportional checks do not represent another runtime execution.

## Accepted source and delivery

- Reviewed source: `7bde3a6a77ed4ebddfd060055af1389c079d26da`.
- Tested PR merge: `6e11d1c6618d1c5cb496b439b2411c486ecf5882`.
- Source/tested/squash tree: `ddf5e4a8aa9ae988ff9673c097ba567dd4b17c83`.
- Normal reviewed squash: `f2cccffcf79c0dc40ff2adefd2a778d0ef6db032`.
- [CI603](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43620484227),
  attempt 1: all 141 jobs successful, with no failed or skipped required job.

## Available behavior

Ordinary Aspire `local-demo` now starts a separate Development-only relying-party
application and synthetic orders API. Register the client manually in the existing
administrator UI, then use the authenticated **Configure sample client** dashboard
command. Bounded secret input travels over protected stdin to a private per-run file.
The [walkthrough](quickstart.md) documents the exact callbacks, scopes and origins.

Native OpenIddict owns authorization code/S256, client-secret-basic authentication,
cryptographic validation and protected reference state. Bounded server-held tickets,
browser-bound login attempts and terminal callback/logout admission keep tokens out
of browser storage and prevent late response publication from restoring authority.
Sign-in uses a fresh password/MFA ceremony and explicit consent. Profile retrieval
displays only matching subject and currently permitted consented email/email_verified.

The API independently validates exact issuer, audience, access-token type, RS256,
lifetime and `orders.read`. It validates JWTs offline: an already-issued token can
remain usable until expiry after grant or policy changes. Current UserInfo demonstrates
current-authority denial separately. Local sample logout destroys that browser's
retained session/tokens; it does not revoke grants, erase issuer sessions or sign out
another application/browser. State is bounded, process-local and disposable per run.
No database access, refresh/machine activation, general client SDK or production
configuration surface is added.

## Reconciled evidence

| Evidence | Current-run result |
| --- | --- |
| Original archives | 135 unique originals / 47,719,176 compressed bytes |
| Ordinary native reports | 117 reports / 6,398 passing executions, no failures/skips |
| Separate synthetic contrast experiment | One report / one passing execution, outside the ordinary total |
| Unit native reports | Core 94; Host 2,076; Architecture 5; Aspire Harness 118 |
| Real sample journeys | PostgreSQL and SQL Server × Chromium and WebKit, one complete case per cell |
| Behavioral mutations | 23 suite/provider receipts / 143 kills, with 143 baseline and 143 restored passes |
| UI / Node contracts | 132 UI cases / 46 Node cases |
| Coverage | 114 mandatory original inputs / all 17 module floors |

Execution totals include repeated provider/browser cases. Host includes 86 sample
cases; the Harness includes eight dashboard cases. All prior required matrix cells
remain. Firefox and the exact WebKit certificate-negative cases retain their visible
A-009 deferrals and are not credited as passed.

All ZIP hashes, byte sizes, unique IDs/names, CRCs, member paths and run/source
ownership reconcile. Source-derived job/upload inventory and all 141 decoded logs
bind the 135 uploads to the exact tested merge. The five formerly missing archives
are now included; earlier partial totals are superseded, not added to this run.
The current environment reports current observations and an enforced policy covering
the actual File Service hosts while preserving the registry allowances. Fresh normal
proxy downloads succeeded without changing proxy variables or TLS validation.

Native result counters, outcomes, definitions, test entries and execution links were
independently reparsed. Exact committed cases and native metadata match all 84
administrator/persistence reports. Remaining full native discovery identities also
rely on the source-bound hosted validators; this review did not compile new discovery.
The four sample originals have their exact committed scenario identities.

Coverage originals bind report/identity hashes, before/after build copies, tested
merge/tree, run/attempt and collector configuration. Independent conservative branch
calculations and line union reproduce the hosted result. DLL/PDB hashes bind CI build
copies, not loaded runtime modules. Lossy merged branch summaries are not authoritative.
The two sample modules are added without lowering any existing floor or changing
collector exclusions. Every required module retains 90% line and 80% conservative branch floors;
two provider-hosting modules have zero branch populations (0/0).

| Module | Lines covered/total | Conservative branches covered/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 452/455 | 379/397 |
| IdentityPlatform.Infrastructure | 9481/10154 | 4652/5514 |
| IdentityPlatform.ServiceDefaults | 70/70 | 43/44 |
| IdentityPlatform.Migrator | 608/616 | 319/366 |
| IdentityPlatform.AuthServer | 7294/7786 | 5753/7178 |
| IdentityPlatform.Admin.Api | 1825/1958 | 1089/1331 |
| IdentityPlatform.Admin.Bff | 4626/4888 | 3480/4137 |
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

Mutation review verifies original/transformed source hashes, exact transforms and
witness identities, causal verdicts, passing baseline/restored controls and restoration
receipts. Underlying mutant TRXs/discovery are intentionally not uploaded; runtime
failure markers and source restoration rely on the strict source-bound producer and
sanitized receipts, not independent replay of those executions.

UI XML/TAP and Node identities reconcile with source. Contract exports match committed
generated documents. Locked dependency inventories and advisory receipts reconcile;
they are not a full deployment SBOM or legal clearance. Candidate viewer evidence
still records incomplete inventory and blocked activation. Retained renderer PNG/HTML
inventory is not qualified manual visual/accessibility acceptance.

## Reviews, receipt identities and limits

The [implementation record](verification.md) preserves the earlier independent
architecture, security, tests, UX and Aspire source reviews and their corrections.
This continuation adds independent native, coverage, mutation, artifact/job ownership,
contract/supply-chain and final acceptance reviews. Final review checks the bounded
source/spec contracts and evidence together; it does not pretend to repeat every
earlier specialist source review or constitute a human security assessment.

| Local receipt | SHA-256 |
| --- | --- |
| artifacts.json | `444162f1c7565ebf51ba3589085a98bc112d6c43a0890943ec575eefbb350282` |
| archive-reconciliation.json | `61ad1603f6c17a5ce632a1465668ca0f0da1329e14a0844265f7d1beb8fc3672` |
| run.json | `fa427263d5a459823867663354fe7bbd5bbbb663ad7c6d2f03d160a8f405ae48` |
| jobs.json | `de431f4b6d8cc7592955116cc5d41429f33259a421ad2d1b95cb3b8a30274185` |
| review-native/native-review.json | `3cfa36532b573cb539df5adb2219279bfd269885f5d521a7f49feaf758a84ecb` |
| review-coverage/review.json | `829eea184e4431da8cb5af5798a95290edb104e3586cac703cd4095d0c71efa1` |
| review-mutations/mutation-review.json | `11a0b16c6d4cb8f7e3f3b4b2e5e487c7cb37af72ee8b25a5b2b9e1ef44a01225` |
| review-ownership/reconciliation.json | `946d9c4bdcbd15a3239f4995dc92b907a0731b89f44b45843921c599bc8c864d` |
| review-contracts/result.json | `2d2f16ff0ff208e0ef44789d683907537482a3ed097e663ddcbf98e8dd299bf2` |
| review-final/review.json | `5de01e74640ff362274ee87b61e252ec309dc0a2f5bc38619ab7bf1f05e86b1c` |
| premerge136.json | `100ea7d05c7791bede52c8f2f096c52c799af38c6793de8153391baddb5af376` |

The originals and local review receipts are retained in the task evidence bundle.
Hosted artifacts begin expiring on 21 November 2026; workspace/chat copies are not a
permanent externally managed evidence archive. No signed download URL or credential
is included in these records.

Host process runtime is **177.315 seconds against the unchanged 180-second bound**.
The TRX interval measures a different period. The narrow margin remains a reliability
qualification; neither this pass nor scheduling isolation establishes stability or
the cause of CI601. Preserve failed CI599–CI602, as detailed in verification.md,
and feature023's unexplained CI590 restart and CI592 contrast failures.

Configured new-host tests reject old cookies and replay a consumed callback. They do
not directly replay an unconsumed pending callback with every original cookie across
restart; the real Aspire restart separately proves fresh waiting/setup state.
Maximum-length profile reflow uses explicitly synthetic presentation-only text.

## Preserved obligations and next work

All six historical findings remain open: ADM-OPS-01, ADM-EVID-01, the SQL/WebKit
certificate-evidence finding, mutation-verifier timeout, GOV-EVID-01 and GOV-PRIV-01.
Keep draft Firefox-restoration PR69 and the A-009 deferrals. Windows Docker Desktop,
pilot and administrator manual assessment (including the 48 proposed rows), qualified
accessibility/visual assessment, the 345 not-assessed assurance controls, refresh
activation, vendor clearance, independent security assessment, certification,
production operations and public-release authorization remain separate open gates.
Historical diagnostic approvals are not renewed.

The next practical activity is the actual Windows Docker Desktop walkthrough. No
post-feature024 implementation batch has been selected. Application clone/delete
(M04-A, with deletion/grant semantics clarified) and general user administration
(M05-A) are candidate future slices; dormant refresh is not silently activated.
