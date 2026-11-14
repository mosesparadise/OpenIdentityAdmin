# Consented scopes and current UserInfo delivery

Feature023 T010–T012 and required T013–T016 are delivered for internal development.
M03-A–M03-C are complete within the approved managed confidential web profile.
Acceptance is qualified by the retained, unexplained CI590 restart and CI592 contrast failures below;
it is not a claim of suite stability, production readiness or external certification.

## Accepted source

- Reviewed source: `a0a96a0aca5c18055f9e13c007a1c27a0bf53d30`.
- Tested PR merge: `fc711e33986452339639ad40cc0139f623dec934`.
- Source/tested/squash tree: `ac00422a1dae481dd936fdbf6d0a4f16d54ab605`.
- [CI594](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43588408738): all 136 required jobs successful on attempt 1, with no failed or skipped required job.
- [PR134](https://github.com/mosesparadise/OpenIdentityAdmin/pull/134): normal reviewed squash `eb2458ad71a53e949ea8d7e715e66e0a8d5c84dd`.

## Delivered behavior

T010 binds bounded ordinal requested scopes and current policy through the native
cached request, protected displayed consent, code and exchange. **Allow sign-in**
approves the entire eligible displayed request; **Cancel** approves none. Changed
policy invalidates stale decisions. Existing OpenID-only applications remain supported.

T011 issues native code/S256 tokens with exact granted scopes and at most one mapped
API audience. Only explicitly consented current Identity email and email_verified may
appear in the ID token and UserInfo. Missing email omits both fields. API access tokens
contain neither email nor administrator authority. Existing replay, consumption,
governance locking, transaction ownership and publication deadlines remain enforced.

T012 provides native authenticated HTTPS GET/POST UserInfo with one bounded bearer
transport and current token, authorization, consent, account, application and policy
checks. It preserves the earlier signed public expiry even when native validation
restores a later database expiry. Projection commits and cleans up before one-use
publication. T013 accepts only the known optional discovery endpoint in the fixed
BFF while preserving both independent no-UserInfo-send guards.

Assignment no longer imposes the former temporary sign-in hold. It permits requested
scopes; it does not infer consent or revoke grants. Current UserInfo can reject a
previously issued token after authority changes. Earlier ID tokens, tokens validated
offline and sessions in other applications are not rewritten or erased. Refresh and
machine grants remain inactive. No schema or package change is introduced by this batch.

## Verification and independent review

| Evidence | Current-run result |
| --- | --- |
| Ordinary native reports | 113 reports / 6,300 passing executions; no failed or skipped case |
| Behavioral mutations | 140 demonstrated kills with passing baseline/restored controls |
| UI / Node contracts | 132 UI cases / 46 Node contract cases |
| Coverage | 110 original inputs / all 15 unchanged module floors |
| Original receipts | 136 decoded job logs / 130 original artifact archives |

Both real PostgreSQL and SQL Server pass. Each provider passes managed Admission 63,
Issuance 66 and Disclosure 16, including genuine native cryptographic validation and
current UserInfo. All eight BrowserProtection provider/Chromium-WebKit group cells pass: Sessions 15 and
Applications 21 preserve the original 36 cases per provider/engine. ApplicationScopeBrowserTests
in each Applications group covers UI assignment → actual Allow → native token validation
→ UserInfo, Cancel and stale/removal/accessibility recovery. These journeys belong to
BrowserProtection/Applications, not the separate ApplicationEditor group.
Both ordinary Aspire demo cells pass all three original scenarios, including the
fresh-run stale-cookie check. Execution totals include repeated provider/browser runs.
All 2,933 prior administrator identities and full metadata/order remain; 44 additions
bring the inventory to 2,977. Coverage policy is byte-identical to accepted main.

Independent architecture, protocol/security, API/transaction, test/provider and UX
reviews examined their affected boundaries. Source reviews admitted runtime evidence
limits; final evidence review separately reconciled the current run. AI review is not
independent human security assessment or certification. Local Host 1,990/1,990 and
AppHost 110/110 controls, UI 132 cases, clean builds and unchanged quality checks support
the source changes; they do not substitute for hosted provider/browser acceptance.

Original archive SHA-256, byte size, unique name/identity, owner, run and source binding
were reconciled against GitHub API metadata. Independent review additionally binds
all 130 artifact upload IDs/digests to their source-derived owning job logs. Native TRXs were reparsed for report
ownership, paths, passing counters/outcomes, all other counters zero and unique
execution IDs. Independent review also reconciles native result/definition/entry
linkage across all 113 reports and exact committed case sets for 84 administrator/
persistence reports. Exact selection identities additionally rely on source-bound hosted
validators. The 110 original coverage XMLs and build identities were reparsed; their
line union and conservative branch bounds match hosted aggregation. DLL/PDB hashes
attest CI build copies, not loaded runtime modules. Lossy merged branch summaries
are not authoritative.

All 16 mutation archives yield 22 sanitized suite/provider receipts with source and
transformed-source hashes, exact critical witnesses, causal kills, successful
baseline/restored controls and source restoration. Underlying mutant TRXs are not
retained; this is receipt validation, not reparsing those TRXs. UI/Node identity
checks additionally rely on source-bound hosted validators and decoded logs.
Seven-day hosted retention and local workspace copies are not a permanent external
evidence archive. This Markdown-only follow-up records accepted runtime evidence;
its proportional checks are not another full runtime execution.

| Controls / requirements | Permanent scenario families |
| --- | --- |
| C07/C08; T010 | ManagedRequestedScopeTests, ManagedConsentDriftTests, ManagedConsentBindingTests, ManagedScopeProcessTests: exact displayed consent, policy drift, cache/process contention |
| C07–09; T011 | ManagedConsentProjectionTests, ManagedConsentHttpTests and retained native replay/publication controls: exact token scope/audience/current claims |
| C10/C11; T012 | ManagedUserInfoTransportTests, ManagedUserInfoDenialTests, ManagedUserInfoPublicationTests and UserInfo lifetime/writer controls: native current authority, bounded projection, expiry and publication failures |
| C12/C13; T013/T014 | Fixed BFF discovery/login/logout controls and ApplicationScopeBrowserTests: real browser assignment/consent/token/UserInfo/Cancel and accessibility |
| C14; T015/T016 | Preserved native inventories, both providers/retained browsers, causal mutations, unchanged floors and independent reviews |

## Coverage and receipt bindings

Unchanged floors require 90% lines and 80% conservative branches.

| Module | Lines covered/total | Branch lower bound/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 452/455 | 379/397 |
| IdentityPlatform.Infrastructure | 9481/10154 | 4652/5514 |
| IdentityPlatform.ServiceDefaults | 70/70 | 43/44 |
| IdentityPlatform.Migrator | 608/616 | 319/366 |
| IdentityPlatform.AuthServer | 7294/7786 | 5753/7178 |
| IdentityPlatform.Admin.Api | 1825/1958 | 1089/1331 |
| IdentityPlatform.Admin.Bff | 4626/4888 | 3480/4137 |
| IdentityPlatform.Worker | 95/96 | 8/8 |
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
| run-final.json | `ec50a1dcd53999ecea35ed25bf9662526c1a85d1d3499b0a45fef10bd110d789` |
| jobs-final.json | `1542e368ba005e2bbd215d10d6ec305f8b67a4d25e6a5af14681bec694cc068e` |
| artifacts-final.json | `1a4fa3268a2386227a8ddd8392576c79ecae311b2606c5add3616c71b7aa7b88` |
| api-ownership-review.json | `b9657b5bb2388116de9b4270030d9df916e6ff231784a438cfdb18020cb88dc7` |
| independent-log-review.json | `9417909f3ae9e54398f2a78571c39c4889f3f4d1243379a8b248b0fb31a8821f` |
| raw-archive-review.json | `c2b8d37dc21b46a183e60f82eefdbf554d7be80dbf0f980ca400a274881a0d1c` |
| mutation-archive-review.json | `5ec2146d9204daac528fbe16561f502239bbcd50e60cdcf3ea4f07332a6dbaa0` |
| source-preservation.json | `fa85ba2a022e790cff7f17b191efccc0629f3f4838582f190e540e263ddf235b` |
| demo-independent-review.json | `1fd3488315b1e3943ce2ff128c37f87adb9bf5aa63547955b3d10d7ff7759949` |
| architecture-review.json | `95a7adfc172d1968f49f219282d11ee372c69cc8e13fecdf157c98bf68de033f` |
| browser-partition-original-review.json | `62ede49df99f97ff40c0b1edaf06cfe5868af9f10d91016edb99ef8177d7cd75` |
| all-original-trx-independent-review.json | `bb848038b27446195d2956064ece1bed21b4d62dea91ec35ecbb38426a514898` |
| artifact-job-ownership-independent-review.json | `89eca84ab766ad1262c2f7b9139c80ae80c9439081a4b14b572a4bd324bee376` |
| final-evidence-review.json | `860a4882ad7be0ffacfb56d0b07de9bbb84a98359731682cf7600469aa436b18` |

## Rejected evidence and reliability qualification

CI586–CI592 remain rejected or superseded runtime evidence, distinct from this run.
CI593 is a separate inconclusive synthetic diagnostic. The
[implementation record](consented-issuance.md) preserves each failure and correction.
Corrections include real .NET JSON writer pending-byte accounting, exact expected
lock metadata, signed-token fixture fidelity, original signed expiry versus restored
native expiry, the process-exit observer race and exact PostgreSQL lock-timeout
classification. The original SQL Server startup exit 18/tempdb permission failure
remains unexplained; later passes do not establish its cause.

CI590's PostgreSQL disposable-restart final navigation failed before its HTTP/status
and stale-cookie assertions. Its cause remains unknown. The added finite network-error
classification improves future diagnostics; it does not prove a product fix or an
infrastructure cause. Current exact-head original demo scenarios pass on both providers,
but acceptance remains qualified by that unresolved reliability evidence. Earlier
passes are not combined with current totals. Recurrence requires investigation of the
recorded finite category; do not weaken assertions or accumulate reruns to hide it.

CI591's only actual test failure was the SQL Server/Chromium scope browser peer save,
after Allow/token/UserInfo and Cancel completed. Source inspection found that a saved
notice can precede catalogue-search completion while Save remains disabled; keyboard
PressAsync does not wait for enablement. The helper now explicitly asserts enabled
inside its original response waiter, then performs exactly one activation. An actual
compiled-helper local reproduction changed from timeout/zero PUT to success/one PUT
with the same five-second budget and system Chromium 151/Playwright 1.63. This establishes
the source gap, not unavailable original CI591 DOM state. No retry, timeout extension,
test identity, count or final assertion was changed. All four current Applications groups pass their original scope case.

CI592 later exposed a separate PostgreSQL/WebKit dark/320 recovery-page contrast
finding. Immediate diagnostics show missing inherited theme-control tokens/font/scheme
while root/body/siblings and loaded stylesheets remain healthy. No product cause,
scanner false positive or painted-pixel state is established. CI593's isolated
40-context original/candidate ownership comparison passed every sample, so it is
inconclusive; no CSS change was adopted. Preserve its original archive and independent
review. Never merge diagnostic branch 144 or treat its synthetic success as actual
browser-journey acceptance. CI594's original Security case passes, but the earlier
failure remains unresolved reliability evidence. Recurrence requires investigation,
not assertion weakening or reruns to accumulate passing results.

CI592 also exhausted the original 660-second BrowserProtection budget. Its partial
native stream retained 31 complete Passed cases spanning 642.403 seconds, including
the scope journey, while five cases lacked terminal outcomes. Source-compatible MTP
parsing and comparison with the preceding run support distributed aggregate slowdown;
they do not identify an unfinished case, exclude a hang or establish infrastructure
causation. The reviewed scheduling correction requires Sessions 15/Applications 21
invocations on every provider/engine, with original full native metadata, disjoint
complete selection, assertions and each 660-second deadline unchanged. All eight
current group reports pass. Coverage separately requires all 44 browser cells and 110
inputs; old unsplit reports cannot substitute. Actual compiled discovery preserves
all 2,977 identities and verifies both exact groups/provider; 125 local Python controls
cover selection, metadata, CLI, coverage omission/substitution and unchanged other
execution groups. This correction does not resolve the separate contrast finding.

## Local use and remaining work

Follow the [Aspire walkthrough](quickstart.md); no custom PowerShell is required:

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Create/assign scopes in the existing administrator UI. An integrating confidential
client can exercise the real consent/token/UserInfo flow. This increment does not add
a sample external application or API server. Recommended next visible increment:
an Aspire sample relying-party application and API using these existing contracts
(M04-D); establish its bounded specification before implementation.

All six historical findings and human/Windows/Firefox/A-009/refresh/vendor/production/public-release gates remain open.
