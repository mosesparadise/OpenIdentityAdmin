# T019 BFF logout continuation and return

**Historical implementation candidate — 25 October 2026; superseded by [accepted delivery](#accepted-internal-delivery) below.** Base `03c072cea6cccab395a19018673e5285e8925578`; branch `097-b3-logout-continuation`. Continues accepted T017/T018 without repeating issuer validation, migration or registration implementation. T019 remains unchecked until required exact-source hosted tests and independent raw evidence review pass.

## Behavior and ownership

FR-009/010/015; B3-END-01–03, with FR-007/008/011 original proof, generation, audit and failure protections. The dormant BFF logout form offers explicit local-only and local-plus-issuer choices. An omitted choice preserves the delivered local-only contract. Only exact `Mode=local` or `Mode=issuer` is accepted alongside native antiforgery; no client, source or destination is accepted from the form. Anonymous pending cancellation and T017 acknowledgement reconciliation stay local-only even if the issuer choice is submitted. No new database schema/provider migration is required.

The real local transaction reserves optional correlation only from the authenticated current ticket. Its detached result captures actual stored source provenance and original browser/generation/correlation/time bounds. Local commit, transaction disposal, whole local scope disposal and original publication checks must finish before a separate owned scope can advance `LocalCommitted` to `RemotePending`. No database lock spans issuer navigation. Remote quota exhaustion, rollback, lost acknowledgement or cleanup failure preserves known local success; no remote proof/link escapes uncertain preparation, and there is no automatic retry. An uncertain local commit/cleanup never starts remote preparation.

The optional response contains only a fixed configured issuer link with exact client, registered return URI, noncredential source selector and random state. No ID/access/refresh token or client credential enters browser transport. The logout cookie uses a distinct host-only protection purpose and the existing strict canonical pending-envelope codec. It binds random state, browser digest, generation and original creation/expiry; no lifetime restarts at remote preparation. Issuer confirmation remains the real native protected T018 flow. The BFF does not make an issuer HTTP request itself.

`GET /bff/logout/return` requires exact path/case, configured HTTPS host/port, no path base/body/content type/Authorization/transfer framing, exactly one canonical state and both independent authentic generation/logout cookies. Persistence compares exact original bounds, browser/generation/correlation and state under the existing writer order before consuming `RemotePending → ReturnedUnverified`. Only after transaction and scope disposal and final original bounds may it clear the logout cookie. It never authenticates, renews, clears or rotates current session/generation/antiforgery cookies. The page always says the issuer result is unverified, including a genuine successful issuer logout. A valid return without any issuer visit deliberately demonstrates this distinction.

## Publication correction and reviews

Independent architecture/security review identified a staged-cookie flaw before hosted verification: restoring an earlier raw generation-cookie header after remote delay could publish beyond an original proof boundary and overstate remaining Max-Age. The correction retains only detached ciphertext/immutable expiry, removes the staged positive generation header before the optional wait and recalculates lifetime at final publication. All remote outcomes check original publication bounds. Expired proof suppresses positive generation/remote cookie/link while retaining deletion cookies and truthful local-success HTML. The same original request deadline is rechecked before fallback; expiry aborts rather than extending the budget. Exact return-path comparison and scalar-only cookie/HTTP failure assertions address the other review findings.

The real expiry fixture retains the genuine short pre-callback generation proof alongside its same-generation actual callback ticket. This approaches the original five-minute bound while the B1 ticket remains inside its thirty-minute idle lifetime; the actual targeted remote commit and disposed-context probes prove reach. No authentication/proof is fabricated or reseeded. Pure cookie tests independently isolate earlier incoming proof expiry and reduced remaining Max-Age. Architecture, security and test review dispositions are source reviews, not human or runtime acceptance.

## Scenario and local evidence

| Tests | Decisive boundary |
| --- | --- |
| `BrowserRemoteLogoutTests` | Genuine product start/password/MFA/native callback, BFF-produced issuer link, actual native issuer confirmation, independently observed exact source revocation/audit and still-unverified BFF return. Authentic return without issuer visit, concurrent duplicate submissions and replay; abandoned/absent-cookie issuer confirmation preserves local revocation. Concurrent returns establish duplicate outcome, not a witnessed database lock order. |
| `BrowserRemoteLogoutBoundaryTests` | Missing/wrong-purpose proof, forged/duplicate state, old proof after new genuine protected sign-in, original expiry equality/rollback, untouched durable-state hashes and retained positive return. |
| `BrowserRemoteLogoutPairingTests` | Two actual fresh password/MFA/native callback journeys in separate jars over the same keys/store; cross-paired authentic cookies deny and both originals remain usable. |
| `BrowserRemoteLogoutCapacityTests` | Both exact live/retained quota ceilings preserve all rows and local success; anonymous assisted choice cannot create remote provenance; genuine local acknowledgement/disposal loss cannot reconstruct remote work through explicit reconciliation. Only inert quota rows are seeded. |
| `BrowserRemoteLogoutFailureTests` | Exact RemotePending/Return audit-targeted actual save, acknowledgement, real context disposal and save/cleanup expiry faults. Actual save/commit/cleanup witnesses distinguish rolled-back state from committed-but-unpublished outcomes; no first-unrelated-commit shortcut. |
| `BrowserRemoteLogoutAuditTests` | Actual provider audit INSERT privilege denial for each remote phase, provider denial classification, independent permission restoration and restored positive return. |
| `BrowserRemoteLogoutLifetimeTests` | Earlier genuine incoming proof expires during actual remote cleanup; genuine abandoned continuation is retained at RetainUntil equality and reclaimed at +1 tick, with replay denied and local audit retained. |
| Host proof/framing/choice/publication tests | Purpose isolation, key loss/corruption, original canonical bounds, closed optional choice, exact return wire framing and final cookie lifetime. These pure controls do not claim authenticated browser acceptance. |

SDK 10.0.401 / OpenIddict 7.7.1. Host and Administrator builds pass with zero warnings/errors. Focused Host tests pass **25/25, zero skips**, 1.315 seconds (`/tmp/b3-remote-host/RemoteLogoutPolicy.trx`). An initial command used the wrong SDK test invocation shape and ran zero tests (exit 5); it is retained separately and supplies no pass evidence. The corrected invocation uses the repository's native `dotnet test --project ... --pre-enumerate-theories on --report-trx` shape. Compilation/analyzer feedback was corrected without suppressions.

Complete actual native discovery preserves every prior **1,799** Administrator identity and portable type/trait metadata and adds **52**, total **1,851**. All six actual group-filter discoveries are reconciled: BrowserSessions **305/provider** = Foundations 158 + Product 96 + Lifecycle 51. Ordinary Administrator counts are **860 PostgreSQL / 965 SQL Server**. Host is **1,506**, unit total **1,601**. These are discovery counts until required hosted execution completes. Every previous selector, timeout, coverage input/floor and mutation oracle is retained. The selector tooling's independently asserted counts are updated from actual discovery. Two intermediate tooling runs exposed stale expected Administrator and aggregate profile totals; those expectations were corrected to the actual discovered 860/965 and 1,222/1,327 counts. The final 226 Python regressions pass (9.577 seconds), as do explicit-base code quality, actual formatter verification, instruction/assurance integrity and diff checks. No baseline, exclusion or deadline was expanded.

## Candidate acceptance and limits (historical)

Required exact-source hosted both-provider/full-workflow, actual raw reports, all 56 coverage inputs/15 unchanged floors and mutation evidence remain pending. No local SQL/Aspire retry is credited; the prior environment's startup/storage limits remain documented. Native integration fixtures run in-process HTTP TestServer over actual stores and real protocol handlers, not trusted socket TLS or browser engines. This does not close T020's actual browser/theme/accessibility acceptance or T021's dedicated B3 mutation work.

T003/T004 remaining integrations, T012/T016/T020, T021–T024 full B3, A-009/T028 restoration, PR #69/issue #70, all six historical findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open. No refresh capability, API proxy, React, global/grant revocation or production composition is enabled. Expired diagnostic approvals remain expired/concluded.

## Accepted internal delivery

PR [#95](https://github.com/mosesparadise/OpenIdentityAdmin/pull/95) was normally squash-merged on 26 October 2026 as `eba29159c32f0b09e28bb6d0e4d65e7dcb980c4e`. The fetched squash tree equals the accepted source/tested-merge tree; no shared history or review restriction was bypassed. Exact head, base, all check runs and empty pending review/comment/requested-reviewer state were verified before readiness and squash. Independent architecture, security, test and raw-evidence reviews passed after the pre-CI corrections recorded above.

| Binding | Accepted value |
| --- | --- |
| Source | `0c26af6b38e84fedae58a6f457e6e4d768b4ab2b` |
| Base | `03c072cea6cccab395a19018673e5285e8925578` |
| Tested merge | `740a1ae883833f444922e9af0a7763db204c789b` |
| Source / tested merge / squash tree | `c2982604ec2bf707146a66b779e1384585fb9cbc` |
| Full workflow | [40246138578, attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40246138578), 73/73 successful jobs; zero failed, skipped or cancelled jobs |
| PostgreSQL / SQL Server Lifecycle jobs | `121328024059` / `121328024094`, 51/51 passing each |
| Coverage / acceptance jobs | `121333152232` / `121333280465`, successful |
| Independent final receipt SHA-256 | `01f28d79393121c3efbb13667ca5f199a013d07652f98b455ad02d4441c5e6e4` |

The independent reviewer directly inspected all 63 retained ordinary TRX reports: **4,377 passing executions**, zero failures, preserving every prior 4,300 name/class/method multiplicity and adding 77 (25 Host + 26 Lifecycle per provider). Root separately reconciled both actual Lifecycle reports. The scoped unit/BrowserSessions evidence contains 10 raw reports and 2,211 passes: 1,601 unit (Host 1,506; Core 25; Architecture 5; AppHost 65) plus 305/provider (158 Foundations + 96 Product + 51 Lifecycle). Actual native Administrator discovery retains all 1,799 prior identities and portable metadata plus 52 new, total 1,851; all six actual group selectors reconcile disjointly and exhaustively.

Ordinary native profile executions are 4,150. Adding 172 browser and 57 contract TRX executions and subtracting the two exact A-009 deferred WebKit certificate cases gives 4,377. Those two cases are unrun/deferred, **not passed**; Firefox remains deferred. The separate 37 Node cases are not added to the TRX total. Actual contract reports pass core 43, Chromium 7 and WebKit 7.

All 56 required raw coverage XML inputs were directly inspected from 57 foundation ZIPs. Independently recomputed coverage passes all 15 unchanged 90% line / 80% branch floors; no timeout, exclusion or allowance grew. Modules with no branch sites retain the established no-branch policy.

| Module | Covered / valid lines | Line % | Covered / valid branches | Branch % |
| --- | --- | --- | --- | --- |
| `IdentityPlatform.Core` | 91/92 | 98.9130 | 7/8 | 87.5000 |
| `IdentityPlatform.Infrastructure` | 6546/7013 | 93.3409 | 659/678 | 97.1976 |
| `IdentityPlatform.ServiceDefaults` | 61/61 | 100.0000 | 18/18 | 100.0000 |
| `IdentityPlatform.Migrator` | 608/616 | 98.7013 | 98/98 | 100.0000 |
| `IdentityPlatform.AuthServer` | 4721/4985 | 94.7041 | 768/776 | 98.9691 |
| `IdentityPlatform.Admin.Api` | 71/71 | 100.0000 | 10/10 | 100.0000 |
| `IdentityPlatform.Admin.Bff` | 2877/3050 | 94.3279 | 658/668 | 98.5030 |
| `IdentityPlatform.Worker` | 26/26 | 100.0000 | 0/0 | 100.0000 |
| `IdentityPlatform.AppHost` | 152/153 | 99.3464 | 14/14 | 100.0000 |
| `IdentityPlatform.Database.Abstractions` | 111/112 | 99.1071 | 4/4 | 100.0000 |
| `IdentityPlatform.Database.PostgreSql` | 96/96 | 100.0000 | 2/2 | 100.0000 |
| `IdentityPlatform.Database.SqlServer` | 93/93 | 100.0000 | 6/6 | 100.0000 |
| `IdentityPlatform.Database.Hosting` | 42/44 | 95.4545 | 4/4 | 100.0000 |
| `IdentityPlatform.Database.PostgreSql.Hosting` | 13/13 | 100.0000 | 0/0 | 100.0000 |
| `IdentityPlatform.Database.SqlServer.Hosting` | 13/13 | 100.0000 | 0/0 | 100.0000 |

All eight actual mutation ZIPs were inspected: 48 decisive kills, 48 passing baseline controls and 48 passing restored controls, preserving 24 oracle bindings across 19 files. These ZIPs retain sanitized receipts, not raw mutant TRX; direct raw-mutant-report review is not claimed. The production exports, dormant session contract and six exporter control receipts match source. Export syscall traces/intermediate documents were not retained; screenshots were not visually re-reviewed. Native selection metadata comes from actual local discovery; hosted ZIPs retain raw TRX/coverage and exact checkout logs bind the tested merge. There was no raw-artifact transfer limitation for this run.

### Retained artifact bindings

Each artifact below belongs to accepted run `40246138578`, attempt 1, source `0c26af6b38e84fedae58a6f457e6e4d768b4ab2b`. The independent final receipt binds these nineteen actual ZIPs and the detailed raw review receipts.

| Artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| `administrator-mutations-PostgreSql` | `12619185651` | `235bd73f9a929544d33c2b6c04951e80e48fba389cd26dbe58db170907001651` |
| `administrator-mutations-SqlServer` | `12619220681` | `1361da6f9d377fabe173e47a9974a782a171936ef47c4bcd22640a1541afa15f` |
| `administrator-protocol-mutations-PostgreSql` | `12619130426` | `2cba4a19da8b6d72356a3f6b0882525117e3f69b1856a3e33e6113372f53e562` |
| `administrator-protocol-mutations-SqlServer` | `12618214618` | `021261e4f6da67810a060e7eb3d0065b75085562cd0e31862d4be2ad459e1b8c` |
| `critical-mutations-PostgreSql` | `12618873424` | `befcde6afacb28c737e5a41930235f3780cc2c25b24278b4d0baf3a487215343` |
| `critical-mutations-SqlServer` | `12618743988` | `3a07c4ad76d7c84b4a4951eb12a3d804220a77dac9c5aecd4b37ceb01998e5a4` |
| `foundation-administrator-PostgreSql-browsersessions-foundations` | `12618882715` | `f5abb02ea8261b7df2c0d308e67bca5168d1790f37e3197be479bd3f748ec083` |
| `foundation-administrator-PostgreSql-browsersessions-lifecycle` | `12618468937` | `0731ffbbb33c603ddb1b50115357242f0f6203385999f7f99632ebc16bc01648` |
| `foundation-administrator-PostgreSql-browsersessions-product` | `12619250265` | `ec60cce2ad2da1ac195eac3e1815ace394bbe3e82b44c71b5746c08702655e22` |
| `foundation-administrator-SqlServer-browsersessions-foundations` | `12618973157` | `88818a982f532dfdba758fe930854b3415e63b284bb79f40b9e3911b18e009da` |
| `foundation-administrator-SqlServer-browsersessions-lifecycle` | `12618349314` | `f0730c33066c576a2781bb118c627d6ec237659ef0baba8f74503f85a9a0133c` |
| `foundation-administrator-SqlServer-browsersessions-product` | `12619275766` | `8b2cfb3119601b3aad297b079e240ed65147570932c2499a04bde5bfa3e2eac9` |
| `foundation-contracts-chromium` | `12618404612` | `3d0040a83a6d8017563f93e24b64798b2d7e7d01674a45de4b2ee91ec81b61fc` |
| `foundation-contracts-core` | `12619102105` | `2b6c439374de16fbb504590bd21cdcc5314d8844d06ef4a4b268863a08230089` |
| `foundation-contracts-webkit` | `12619330253` | `565b14416e6f8541805f7be65319a779b76fec1ce60a2b946bbc09757f1f6b09` |
| `foundation-coverage` | `12619406213` | `efd8449b821f888288f55e0c05a593006d5d7764eae77001f48b785ac4d3bd88` |
| `foundation-unit` | `12618848450` | `6bbff8ff651f9def8aefceb12d48f3f793dbdf5f2ee47923cdda444245b37611` |
| `governance-mutations-PostgreSql` | `12618519034` | `d8c5a5440f4a3d7d4ea728cb4218ff5e5f57f5077964c799c63cebe08a1ecd2a` |
| `governance-mutations-SqlServer` | `12619047705` | `61590160a277196046c194d98c16857f4649bc240a7c7a504bf19206dc7e474c` |

### Disposition and remaining gates

T019 is complete for internal development. The local zero-test invocation, intermediate compilation/tooling corrections and pre-CI review findings remain recorded above; none counts as a successful execution. The accepted source had one full hosted attempt with all 73 jobs successful. Earlier T018 rejected/cancelled runs remain in their original evidence record; no local SQL/Aspire retry or renewed diagnostic authority is claimed.

Native integration fixtures use TestServer over actual stores and native protocol handlers; they do not prove socket TLS, browser cookie policy, theme or accessibility. T003/T004 remaining integration, T012/T016/T020 browser/TLS evidence, T021–T024 full B3, A-009/T028 restoration, PR #69/issue #70, all six historical findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open. M02C/D/E acceptance remains absent. No refresh, API proxy, React, global/grant revocation or production composition is enabled. Expired diagnostic approvals remain expired/concluded.

### Next bounded implementation: T020

Build a test-only dual-HTTPS harness for the complete issuer accounts/protocol flow and explicit generic-host BFF middleware. Existing BrowserLoginHost/BrowserIssuerHost use TestServer; the B2 NativeIssuerHttpsHost supplies useful trusted-Kestrel/request-count/drain patterns but not the complete B3 composition. Supply both immutable endpoint identities before credentials, account/profile and registration construction; reuse existing database leases, retained keys and audited registration upgrade. Use actual native HTTP backchannel without an issuer bridge, normal TLS validation, original deadlines and joined shutdown before database/key disposal. Preserve the WebApplication composition guard and dormant production Programs.

First prove genuine password/MFA → native callback → status, local-only logout and issuer-assisted logout on both providers with active Chromium/WebKit selection. Inspect local ticket/generation and committed issuer source revocation separately; a return remains explicitly unverified. Include inert issuer GET, protected POST, abandoned confirmation and denial of the old local session, then extend the remaining T020 matrix. Different localhost ports provide different origins, not different cookie hosts/sites; use correctly trusted distinct names for any SameSite/host-isolation claim and record topology limits.

A-009 defers the named Firefox/history/WebKit certificate-negative issues only, not all missing HTTPS or new browser work. T021's dedicated B3 mutation work remains separately required and must preserve the existing 48 controls with decisive baseline/mutant/restored evidence. This is the reviewed next-step design, not completed T020 or T021 implementation.
