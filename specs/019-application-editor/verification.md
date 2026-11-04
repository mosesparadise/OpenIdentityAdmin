# Application editor verification

## Accepted internal delivery — 4 November 2026

[PR #120](https://github.com/mosesparadise/OpenIdentityAdmin/pull/120) is squash-merged as `74e186078c8b98aec3fff0e8e6fb7300dcf8eafa`. The native Aspire workspace now
supports application creation, sanitized details, concurrency-safe editing and explicit
one-time secret replacement. The same UI-created and edited registration completes
fresh password/MFA, explicit OpenID consent and real server-side code/S256 exchange.
Use the [local quickstart](quickstart.md) from main; no custom PowerShell or separate
frontend server/build is required.

Source `5f1413e8c616be4a6bfc21abd88534d073b3f3ab`, tested merge `0c826c0e7003e8087784bece74f109b7ce0e43e8` and the squash share tree `111687129f2ad27ddba90fc9a2a8e30f8a4d1478`.
Base was `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa`. [Run 41896318614](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41896318614),
attempt1, satisfies all122 exact-head job obligations. Every job's initial checkout,
head and attempt is bound to the tested merge. The reconciliation binds all45 reviewed
source/configuration inputs; remote main and the squash tree were
verified after merge. Twelve earlier failed tested candidates remain rejected in
[hosted verification](hosted-verification.md); this acceptance uses no retained result
from those runs and claims no earlier failed run became successful. Intermediate
`c0322ebcd0ed568f73c1c105bbb22170badd9106` was superseded to remove masked negative
controls; it is not accepted evidence and is not counted as another rejected tested candidate.

| Verified boundary | Accepted evidence |
| --- | --- |
| Ordinary execution | 101 strict-verifier report obligations /5,427 required native passes, zero failures/skips |
| Identity preservation | 2,385 Administrator native identities; prior2,375 retained with documented bootstrap rename mapping and ten additive browser records |
| Host/provider behavior | 1,846 Host cases; genuine PostgreSQL and SQL Server mutation, audit rollback, concurrency, current authority, expiry and native issuer cases |
| Native Aspire | Both ordinary local demos pass3/3, including the actual UI-created registration and browser sign-in ceremony |
| Retained browsers | All40 provider/engine cells pass; Administrator's nine stories total71 cases per provider per engine |
| Browser journey and recovery | Same-registration fresh password/MFA/consent/code flow; denial; real permission removal/expiry; bounded error presentation; committed lost-output recovery and one-send checks |
| Native lifecycle | Eight lifecycle executions across both providers/engines include headed history/native visibility and headless pending logout/original expiry, with joined pending work and no late secret restoration |
| Contracts and UI | 44 native core and seven cases per retained browser; separate44 exact Node contract controls and55 exact UI cases; deterministic generated/shipped bytes |
| Coverage | All98 required coverage inputs and15 unchanged module floors pass |
| Mutations | 90 behavioral kills in14 lanes, with passing baseline/restored controls and exact source restoration |
| Artifact bindings | All116 accepted upload-log/API identities and SHA-256 values match; [manifest](accepted-artifacts.md) |

Lifecycle execution uses the explicitly instrumented Playwright1.63.0 driver copies
documented in [browser corrections](browser-corrections.md). All40 browser jobs run
the12 mandatory instrumentation controls; each of the four actual lifecycle wrapper
steps separately proves its selected engine/version/original/copy hashes. Ordinary
browser lanes use the unmodified driver. These controls are separate from ordinary
native totals. Actual window-manager/DOM transitions and trusted main-document events
are required; no synthetic event satisfies them. Only the two lifecycle helpers seed
one exact unpredictable test-owned counter key. Its bounded integer record is
validated, privately scanned and atomically removed before unchanged empty-storage
assertions; unrelated browser journeys carry no such instrumentation. Finite progress
checkpoints are diagnostic only and cannot replace acceptance evidence. History tests record bfcache use
when observed; a zero count does not establish bfcache restoration or browser-wide
compatibility. No cache/security header is weakened. Pending Navigation/Visibility
retain headed contexts and genuine window transitions. Session/OriginalDeadline use
headless contexts with the same explicitly instrumented pinned driver; their earlier
failed headed observations remain rejected and cause-unproven. No headed logout/expiry
acceptance is inferred from this new topology.

Only pending Navigation on Chromium explicitly adds `--enable-automation`, enabling
the native command-line observation prerequisite while retaining the actual
BFCache-disabled launch-policy assertion. Other launches receive no added argument,
and non-Chromium opt-in is rejected. This flag can affect browser chrome; results
apply to this explicitly configured fixture, not an ordinary unmodified launch.
Earlier unflagged failures remain rejected. Pinned source establishes the prerequisite;
it does not recover the suppressed hosted exception text.

Navigation-only Chromium proves original UI ownership loss rather than network
termination. It verifies the running BFCache-disabled configuration, explicit loss
of the original captured context epoch, a different live default-context identity,
real history and the returned main-frame identity with a new loader before response
release. This is not generic physical realm destruction, preserved-BFCache acceptance,
browser receipt, network completion or remote cancellation. Initiation-only network
sealing is permitted only with this independently validated navigation witness.
WebKit and same-document boundaries retain actual native request-terminal observation;
original Playwright request identity and one-send guards remain mandatory.

The exact admitted BFF request separately requires both pipeline return and the real
server Response.OnCompleted callback, joined with API work under the original
three-second cleanup budget and sealed after native-session cleanup. Completed server
response writes and observed request cancellation are recorded as different outcomes.
A cancellation-only path does not prove a successful late response was delivered to
the browser. Returned-UI/no-late-output checks remain required; no browser receipt or
immediate remote cancellation claim is inferred from server completion. Synthetic
ledger/callback controls calibrate missing, invalid, duplicate and late evidence;
they are not native browser execution. Separate genuine transport-disconnect controls
remain required and pass.

Creation-body observation captures and parses the same exact201 response once before
DOM/focus/input waits, preserving the original body/display equality and application-ID
checks. It adds no retry, refetch, interception or background body task. Permanent
sanitizer controls restrict failure diagnostics to a bounded closed category and six
booleans, preserving the original exception without exposing body, URL, headers or
failure text. This observation change and its accepted execution do not establish the
cause of either earlier demo failure; those failed runs remain rejected.

Private-output observation covers bounded actual URLs/current history state,
console/errors, DOM/storage, successful post-cleanup xUnit output and sanitizer
controls. It does not enumerate arbitrary browser memory, complete private history,
later process/TRX output or uploaded ZIP contents. Raw artifact transfer remains
blocked by enforced network policy. Reconciliation uses exact committed verifiers,
decoded finite logs and upload/API metadata; no local ZIP hashing or raw report reparse
is claimed. The hosted coverage checker enforces exact floors; the table below retains
its rounded log percentages.

| Module | Lines | Branches |
| --- | ---: | ---: |
| IdentityPlatform.Core | 99.7% | 96.4% |
| IdentityPlatform.Infrastructure | 93.3% | 96.4% |
| IdentityPlatform.ServiceDefaults | 100.0% | 100.0% |
| IdentityPlatform.Migrator | 98.7% | 100.0% |
| IdentityPlatform.AuthServer | 94.2% | 98.6% |
| IdentityPlatform.Admin.Api | 93.4% | 96.8% |
| IdentityPlatform.Admin.Bff | 94.4% | 96.4% |
| IdentityPlatform.Worker | 100.0% | 100.0% |
| IdentityPlatform.AppHost | 97.1% | 99.0% |
| IdentityPlatform.Database.Abstractions | 99.1% | 100.0% |
| IdentityPlatform.Database.PostgreSql | 100.0% | 100.0% |
| IdentityPlatform.Database.SqlServer | 100.0% | 100.0% |
| IdentityPlatform.Database.Hosting | 95.5% | 100.0% |
| IdentityPlatform.Database.PostgreSql.Hosting | 100.0% | 100.0% |
| IdentityPlatform.Database.SqlServer.Hosting | 100.0% | 100.0% |

Independent architecture/security, test/CI and UX source reviews pass within their
recorded scope. The final requirement review finds no remaining source/design gap for
T007/T014/T019/T022; hosted evidence review separately reconciles the accepted results.
T001–T022 are complete for this internal slice. The coherent documentation followup
records T023's reviewed squash and local instructions. Earlier pending/missing-scenario
wording below and in linked checkpoints is historical and superseded by this acceptance.

The selected scope remains one confidential web code/S256 profile, a generated
expiring shared secret and OpenID-only identity from existing administrator accounts.
It grants no administrator role or management authority. Other profiles, ordinary-user
onboarding, scope/audience/claim catalogues, clone/disable/delete and asymmetric
credentials remain roadmap work. Refresh remains disabled. Actual Windows Docker Desktop, qualified manual accessibility, Firefox and the precise A-009 certificate deferrals, all six historical findings, human/vendor/production/public-release gates remain open.

## Historical implementation and verification record

The following checkpoints retain their original results and source bindings.


Starting main: `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa`. Planning checkpoint `cbe39a2f116702c85edca1879203c05c5955f41f` preserved. User's “all recommended” response resolves the three initial-slice choices; selected scope is recorded in spec/research/approval.

Independent architecture, credential-journey, provider/ownership and test research identifies separate native managed-client admission/publication, no administrator completion grant, atomic manager/policy/audit writes, exact aggregate concurrency, cross-family CSRF/replay and real issuer tests. Initial T004 implementation is in progress; no end-to-end feature delivery is claimed.

Pinned native research at OpenIddict7.7.2: initial authorization-request caching bypasses passthrough and dispatches an anonymous ProcessSignIn; native PrepareRequestTokenPrincipal stores the exact parameters and cached_authorization_request subtype; native GenerateRequestToken creates the reference row; ProcessSelfRedirection performs the local native redirect. Resumed validation restores parameters from the native token before the managed immutable demand is captured. The initial cache has no endpoint passthrough receipt. Native cached-request and code redemption are durable before later issuance; do not move either into a transaction that later rollback could resurrect.

These are source observations, not execution acceptance. T003 must prove the actual native HTTP/manager path. All existing feature018 results remain historical accepted baseline, not evidence for new code. Required providers, retained browsers, unchanged coverage floors and all historical/human/Windows/Firefox/A-009/vendor/public-release limits stay open as applicable.

Independent T002 test review passes the bounded input/DTO/version task T004. Contract precision now fixes strong ETag grammar, exact UTC seconds and ill-formed-Unicode rejection. Architecture review identified native cached-request redemption durability as well as code redemption; the plan/tasks now preserve both in earlier committed child epochs and require later-failure replay regressions. Native integration and concrete interaction-capacity/lifetime proof remain T003/T005 prerequisites; T004 is independent.

T002 final scoped architecture acknowledgment passes the corrected two-artifact durability design. Concrete interaction limits are now recorded in plan/data-model. Source/test/UX acceptance and actual native boundary evidence remain required. Cross-artifact analysis: all12 FRs are mapped to23 tasks; no unresolved product choice or conflicting selected profile remains. Source observations are not runtime proofs.

Initial local T004 diagnostic: SDK10.0.403, Host build succeeded after correcting CA1861 test-array diagnostics. Applications namespace test run:96 passed,0 failed,0 skipped. Additional safe-projection test is being added; final T004 count and source review remain pending. No database/browser/full-CI pass is claimed from this unit run. Remote main and branch were rechecked at4c9fb24 andcbe39a2 respectively; existing work remains preserved.

## Tested foundation — 30 October 2026

T003 native characterization and T004 bounded contracts are implemented. T005–T023 remain unfinished; no create/edit UI, managed human login or production publication capability is delivered by this checkpoint.

- Host build:0 warnings/errors; complete native Host suite1697 passed,0 failed,0 skipped (53 added cases). Input/version/details/one-time-output checks include an independently specified SHA-256 vector, exact finite JSON projection fields and no credential-bearing ToString.
- Native Administrator build:0 warnings/errors. ManagedApplications characterization:6 PostgreSQL and6 SQL Server passed,0 skipped. Real pinned databases, native OpenIddict7.7.2 managers, secret hashing, Basic/S256, HTTP extraction and crypto are used. This is an HTTP-component harness with an explicitly synthetic human/consent seam, not socket/TLS or product managed-login acceptance.
- Initial SQL attempt:5 cases,2 passes/3 failures. Failures were test-oracle errors (assuming every native rejection is JSON400, including injected server_error500 and plaintext authorization errors). Retain that failed receipt. Corrected tests assert exact native errors/status/callback provenance, add a separately registered-client positive control, and query actual persisted status before injecting rollback. The final six-case runs are new-source verification, not a claim that the failed attempt passed.
- Native observations: initial anonymous caching creates one request-token and no authorization; native restoration preserves original callback/state/nonce/scope/S256 despite conflicting outer values. A second registered client's own cache works, but substitution into the first request is denied. Successful code/exchange consume their native artifacts. Late rejection after actual persisted redemption followed by commit blocks replay. An exception followed by enclosing transaction rollback restores the artifact, and genuine replay succeeds. Therefore T012 must use a separately committed consumption epoch; early native handler order alone is insufficient. Production replay-failure controls remain required.
- Independent architecture and test reviewers pass the scoped design/T004 source. Independent native-boundary reviewer passes corrected T003 source after closing exact-error, registered-foreign-client and persisted-redemption-witness findings. Reviewers did not claim to execute these tests.
- Exact native discovery2065 preserves every prior2053 metadata record unchanged and adds12. Required ManagedApplications lanes are added for both providers; Host count1697; coverage-input obligation becomes80 with all15 floors unchanged. All selection controls67/67 and browser/coverage-input controls7/7 pass; stale independently pinned totals were updated by the exact12-case delta. Native checks used direct scoped filters; subsequent native filtered discovery and strict story/metadata/execution-name reconciliation confirm the exact six cases per provider. Full ordinary exact-head CI remains required before feature delivery.
- Syntax-aware code-quality, scoped unused-import/order verification, instruction and assurance snapshot checks pass without new allowances. Existing production source is unchanged except additive Core contracts; full browser/provider regression and production endpoint acceptance are not inferred from these passes.

Commands executed with SDK10.0.403, existing locked packages and existing certificate trust: `dotnet build tests/backend/IdentityPlatform.Host.Tests/IdentityPlatform.Host.Tests.csproj --no-restore -m:1`; analogous Administrator build; `dotnet run --project tests/backend/IdentityPlatform.Host.Tests --no-build --no-restore -- --report-trx ...`; Administrator native executable filtered by `FullyQualifiedName~IdentityPlatform.Administrator.Tests.Features.ManagedApplications.NativeCacheTests&Provider=PostgreSql` and separately `SqlServer`; `python3 scripts/check_code_quality.py`; selection/browser-matrix unittest discovery. Native database preparation uses the unchanged pinned image script. Reproducible unused images were exchanged sequentially to fit local storage; no source/evidence deletion, image/duration weakening or network bypass.

Local receipt digests (retained under `/workspace`; not hosted acceptance):

| Receipt | SHA-256 |
| --- | --- |
| `feature019-host-full/IdentityPlatform.Host.Tests_net10.0_x64.trx` | `758b6d43d7fae7d067b66207da327cebbe163ac46cf244603f5d83eff97da306` |
| `feature019-cache-sql/IdentityPlatform.Administrator.Tests_net10.0_x64.trx` | `0d06d0708044671603dd8f93ec0938a2c7511a9910e73f500e16eb16371ba112` |
| `feature019-cache-pg/IdentityPlatform.Administrator.Tests_net10.0_x64.trx` | `2445102957544ac45be660a7944e7966befc4a24e95f27b5e77cf2abffe88d6c` |
| `feature019-cache-sql-final/IdentityPlatform.Administrator.Tests_net10.0_x64.trx` | `a36465bf4c36b8cf8a11203f9ad04ab3e30aacb7fb0302654640d5bf01048992` |
| `feature019-administrator-discovery.json` | `8e63670b4721df0b6c43fd4cf40bef9930003ea24d7ad8d16b9d0ba85d4ad8de` |

## T005 additive storage checkpoint — 30 October 2026

Implemented ApplicationPolicy and ManagedLoginInteraction, with native-generated Platform migrations `20261030100418_ManagedApplications` (PostgreSQL) and `20261030100520_ManagedApplications` (SQL Server). Only these two tables and their constraints/indexes are added. Existing native/Identity tables, migration files and Sessions history remain unchanged. No native registration is automatically adopted. [Data model](data-model.md) distinguishes static constraints from the runtime attachment, authority, retention and publication obligations still assigned to T006/T010–T012.

Local execution uses SDK10.0.403, EF10.0.14, the unchanged frozen producer at `38eb41e06d40b6addbe57e3af839f912645e6dea`, and the repository-pinned PostgreSQL18/SQL Server2022 images. Locked restore and the persistence project build pass; the final build has zero warnings/errors. Both complete persistence inventories pass: **19 PostgreSQL +19 SQL Server, zero failed/skipped**. Exact native discovery/result names, run-local identities, strict completion and counters reconcile using the existing inventory/selection/TRX validators. All30 prior portable metadata records remain byte-equivalent; eight new records add four scenarios per provider. Required matrix counts rise15→19/provider, with independent partition totals updated and all67 selection controls passing. Coverage floors and input obligations remain unchanged.

New scenarios prove populated immediate-prior upgrade and repeat preserve every old physical row/catalog object and exact history; native manager credential validation remains valid after upgrade; no managed policy or interaction is seeded; all seven static phases and transient nullable reservations persist; malformed revisions/times/native bindings/proofs, premature proofs and duplicate native identities fail in the actual engines; full native account widths remain supported; stale aggregate and interaction revision/watermark writes fail without renewing the deadline or aggregate version. Independently enumerated physical columns/types/nullability/widths and seeded preservation-witness defects complement existing frozen-baseline, fresh migration, model parity and provider-conformance cases.

Commands actually run: `dotnet build tests/persistence/IdentityPlatform.Persistence.Tests --no-restore -m:1`; `python3 scripts/prepare_provider_baseline.py`; native persistence executable discovery with `--pre-enumerate-theories on --list-tests json`; then separate native executable runs using `--filter-trait Provider=PostgreSql` / `Provider=SqlServer`, `--parallel none`, `--pre-enumerate-theories on` and TRX output. Both runs set the existing frozen producer/manifest variables. `python3 -m unittest discover -s scripts -p 'test_*selection.py'`, code-quality seeded/baseline checks, scoped IDE0005/IDE0065 verification, instruction checks, assurance snapshot integrity and `git diff --check` pass. Assurance remains345 not-assessed controls.

Independent architecture/migration review passes the two-table delta, explicit nullable-proof guards, filtered SQL uniqueness and documented runtime limits. Test review identified missing interaction concurrency and early-phase proof controls; both were added and the corrected source passes review. No reviewer claims a product browser or full-CI pass. The next-step ownership review confirms reuse of ordered governance/profile/account locks and bounded current grant/source observations. Native owned stores are non-reentrant: direct EF observations need an execution lease, while native manager calls must use their own store leases. ApiRead remains rollback-only; no mutation authority is implemented by this checkpoint.

Failure provenance is retained. The first build failed formatting in new fixtures, then passed after correction; the first quality check exposed unnamed nullable markers, corrected without allowances. Selection checks initially exposed old fixed counts; the explicit reviewed totals were updated. An initial focused SQL run passed three earlier cases but is not the final acceptance source. The first full SQL invocation used default class parallelism, exhausted Docker VFS disk space and produced no complete acceptable report. The subsequent PostgreSQL image pull also failed on space. Read-only Docker layer/reference inspection identified four unreferenced init layers created during that exact SQL attempt and one unreferenced partial layer from the failed pull; only those five newly generated layers were removed after reference and timestamp checks. Existing source, evidence and older layers were preserved. Final provider runs execute the complete unchanged selections sequentially, with no longer test deadlines or skipped cases.

Independent final evidence review parsed both reports, confirmed38 passing exact identities, preservation of all30 prior records plus8 additions, all29 source-manifest hashes and the67 passing selection controls. It explicitly excludes the incomplete earlier SQL attempt from acceptance.

[Evidence hashes](schema-evidence.json) bind final reports, discovery, changed-source hashes and retained failure/recovery receipts under `/workspace/`. T005 is complete as this local implementation/verification checkpoint. T006–T023 remain open; this is not full feature acceptance, hosted CI, a new UI or a merge. Remote main was reverified at `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa`, with the branch initially at `944419d8e647240d7c93e8afab6d48ab639690ae`. All historical/human/Windows/Firefox/A-009/vendor/production/public-release gates remain unchanged. Next: T006 current owned create/edit/replacement authority with native-manager/policy/audit atomicity, then JSON antiforgery and the real issuer/UI integration.

## T006 owned application writes — 30 October 2026

The internal create/edit/secret-replacement coordinator now uses a fresh Owned native-storage child and one transaction for current action authority, OpenIddict manager changes, ApplicationPolicy and a sanitized audit intent. It acquires governance → administrator profile → actor-account locks before observing current source/grants, binds the immutable action and original source/native deadlines, rechecks actual persisted rows after native/internal saves, and consumes authority once immediately before commit. Original API budgets cover real child cleanup; a successful result and one-time secret are constructed only after disposal completes. Unknown commit/cleanup outcomes never trigger a retry. Existing ApiRead remains rollback-only.

Creation requires current applications.write. Edits require that same grant and the exact aggregate ETag; presentation/callback changes preserve the native hash and credential lifecycle. Replacement requires current applications.credentials, explicit confirmation, exact ETag, and actual persisted MFA strictly under300 seconds. Create/replacement recheck the explicit future UTC expiry under locks and immediately before commit. Native managers hash fresh32-byte random secrets. Unsupported/native system registrations are not adopted; both reserved names and persisted system IDs are protected. Exact target IDs reject provider aliasing, and frozen expected policy/native/audit values detect internal-save drift. The API observation path uses metadata only; no issuer private keys or factor decryption are introduced.

The new real-provider component scenarios cover create/edit/replacement, native old/new-secret validation, independently enumerated persisted presentation/profile/audit fields, concurrent duplicates and same-version races, current grant removal, MFA boundary, persisted pilot binding, exact target aliases, audit failure/deletion, token/source revocation and rebinding, tracked/detached policy drift, credential expiry, actual post-save cancellation, and actual child-disposal failure after a confirmed commit. Cleanup failure demonstrates one durable application/policy/audit and one commit while withholding the output. The existing seven Applications cases per provider retain read-only ownership and reader regression coverage.

These tests use actual databases, native managers, account enrollment/source state and transaction/cleanup machinery. They construct ApiIdentity from seeded native authorization/access rows at the component seam; they do not prove signed bearer HTTP authentication, JSON antiforgery, BFF transport, managed issuer login, browser rendering or complete feature delivery. Those remain in T007–T023. T008/T015 have substantial component evidence but remain unchecked for their complete integrated acceptance.

Independent architecture/provider review found and resolved stale tracked native observations, missing immutable source binding, SQL target aliasing, and expected-state capture after internal saves. Independent test review required fresh persisted profile/audit assertions, replacement rollback, edit lifecycle-drift detection, real cancellation and actual disposal-failure oracles; all were added. Corrected scoped source/tests pass both reviews. Reviewers distinguish code review from execution evidence.

Failure provenance: the first SQL smoke failed during Aspire DCP startup because .NET did not bypass the proxy for bracketed IPv6 loopback with only ::1 in NO_PROXY. A direct proxy-bypass probe reproduced False; appending [::1] to NO_PROXY/no_proxy returned True. The restored SDK's per-user HTTPS development certificate was trusted, with SSL_CERT_DIR pointing to that local trust directory. No remote proxy/TLS policy was disabled. The next smoke passed. The first complete SQL partition passed23/24; the remaining setup failure was a synthetic PilotManifest.Id longer than the existing32-character column. The fixture identifier was shortened without changing schemas, production behavior or assertions. Initial compiler/analyzer/quality findings were corrected without expanding allowances; one final quality invocation omitted the SDK PATH and failed before executing, then passed with the documented SDK environment. All failed receipts remain retained separately.

Traceability: FR-005/006/008 are implemented at this owned component boundary; FR-007/010 receive version/credential controls, and FR-012 receives exact native inventory/failure controls. T007/T009/T016 still provide authenticated HTTP/CSRF and protocol projections; T010–T014 provide the genuinely usable issuer/browser journey.

Commands actually executed with SDK10.0.403 and locked packages: `dotnet build tests/aspire/IdentityPlatform.Administrator.Tests --no-restore -m:1`; Host build and native Host TRX run; `python3 scripts/prepare_native_image.py PostgreSql` (SQL image was already prepared); `python3 scripts/verify.py SqlServer --partition administrator --story Applications` and the same command for PostgreSql; `python3 -m unittest discover -s scripts -p 'test_*selection.py'`; `python3 scripts/check_code_quality.py`; scoped IDE0005/IDE0065 `dotnet format style --verify-no-changes`; instruction/assurance checks and `git diff --check`. Provider runs use the unchanged pinned PostgreSQL18 and SQL Server2022 images sequentially; unused images were exchanged to fit local storage. No test/startup deadline, coverage floor, skipped-case rule or browser deferral was changed.

Final local results: **24 PostgreSQL +24 SQL Server Applications cases, 1,697 Host cases and67 selection controls pass, zero required failures/skips.** Both ordinary provider invocations strictly reconcile native discovery, portable metadata, exact executed identities, counters and completion. All2,065 prior administrator metadata records remain exact;34 additions produce2,099 records,17 new cases per provider. Applications grows7→24/provider; overall Administrator totals become934 PostgreSQL/1,039 SQL Server. All80 coverage-input obligations and 15 module floors remain unchanged. Final builds have zero warnings/errors; code quality, scoped style, instructions and assurance snapshot checks pass (345 controls remain not assessed).

[Mutation evidence hashes](mutation-evidence.json) bind the21 changed source/inventory files and retained local receipts under `/workspace`, including the failed smoke and23/24 SQL report separately from the two final24/24 reports. This completes T006 locally. T007–T023 remain open. Next: T007 genuine JSON antiforgery acquisition/binding and bounded fixed mutation transport, followed by API endpoints and the managed issuer/UI integration. Main remains `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa`; the implementation branch began this item at `e7395c6c9bbe8ae98fc8acb91f694a9587da139f`. No full CI, merge, product create/edit screen, refresh activation or human/public-release gate closure is claimed.

Independent final evidence review passes this bounded local checkpoint: both exact24-case provider reports,1,697 Host results,67 selection controls, all24 artifact hashes and all21 changed source hashes were independently verified. All2,065 prior metadata records are preserved, with34 additions. The earlier SQL setup failure is explicitly excluded from acceptance.

## T007 JSON protection and mutation transport — 30 October 2026

[Boundary implementation and verification limits](json-transport.md) describe the genuine native token GET, separate application proof family/browser-generation binding, exact fixed mutation transport, strict bounded JSON and action-specific response validation. The original five-second budget covers real HTTPS cancellation and final output; fresh durable state and post-serialization deadlines suppress unsafe publication. Native OpenAPI and generated browser types include the token GET. Actual API writes and production mutation-route mapping remain T008/T009/T016 dependencies.

Independent architecture/security and test reviews resolved response substitution, post-serialization expiry, UTF-8 expansion and contract parameter admission, and strengthened actual TLS/browser oracles. A final narrow review accepts the upstream cancellation assertion. Review is source evidence; it does not establish browser execution. [Receipt hashes](json-transport-evidence.json) preserve final local results separately from failed fixture/setup attempts and pre-deadline-assertion provider reports.

All 2,099 prior administrator metadata records remain exact, with eight additions for 2,107. Both provider Applications selections now require 26 cases; browser counts rise to 65/provider (30 BrowserProtection), Host to 1,734 and Node contracts to 38. The 80 coverage-input obligations and 15 floors are unchanged; this checkpoint does not claim a new coverage measurement or full CI.

**T007 remains unchecked:** the locked Playwright 1.63.0 CLI receives `403 Domain forbidden` downloading Chromium from `cdn.playwright.dev`; installation stops before WebKit. The new retained-engine scenarios are compiled and inventoried but unexecuted. Preserve this explicit pending requirement while continuing T008/T009 integrated create/details API work. No completed create/edit UI, merge, refresh activation, production or historical/human/public-release gate closure is claimed.

Final local results: **26 PostgreSQL +26 SQL Server Applications cases, 1,734 Host cases, 44 native contract cases, 38 exact Node contract cases and67 selection controls pass, with no required failures or skips in these final selections.** Both final provider reports include the tightened actual upstream cancellation join. The native contract run verifies repeatable production entry-point export with zero operations/network effects and contract compatibility against main `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa`. Final builds, syntax-aware quality, formatting, generated-type checks, instruction and assurance-snapshot checks pass; all345 assurance controls remain not assessed. The final source manifest binds36 changed source/inventory files against `df494ec5cfb5fa596c8f7a844385794cdb57000c`; local receipts and earlier failed/nonfinal runs are hashed separately.

Independent final evidence review passes this bounded checkpoint: both latest exact26-case provider reports, Host/native/Node/selection results, all36 source hashes and76 local artifact hashes were verified. All2,099 prior administrator metadata records remain exact. The browser installation failure and earlier failed/nonfinal reports remain excluded from acceptance. T007 stays unchecked for retained-engine execution.

## T008/T009 authenticated create and details — 30 October 2026

[Implementation, exact HTTP semantics and verification scope](http-endpoints.md) document genuine native API authentication, independently current permissions, atomic create/one-time output and rollback-only safe details. Actual three-host TLS tests drive password/MFA/native code exchange and signed-bearer validation without authentication or upstream-response substitutes. They are HttpClient-driven integration evidence, not browser-engine evidence.

Final full affected selections pass: **35 Applications cases per provider,1,735 Host,44 native contract,38 exact Node contract and67 selection controls**, zero required failures/skips. Native discovery preserves all2,107 previous metadata records and adds18; current inventory is2,125. All80 coverage inputs and 15 module floors remain unchanged, without claiming a new coverage measurement. Generated native BFF contracts/types include create/details, preserving status/list/token. Comparison against main `4c9fb24f003ecfdeb4b4b4d438ce7c847f3780fa` passes. Final builds, formatting, code-quality, instruction and assurance-snapshot checks pass;345 controls remain not assessed.

Independent architecture review corrected pre-projection native-ID filtering. Test review added direct API privacy, current-token and mapped-CSRF controls, exact-limit valid stored JSON, and fresh native absence after late expiry. Corrected scoped source reviews pass. The initial seven-case PostgreSQL run failed an existing fixture's fixed-host MFA CSP assertion; explicitly passing the real configured origin preserves exact CSP checks and resolves setup. Earlier focused/full PostgreSQL passes before final oracle strengthening remain separately classified. [Evidence manifest](http-endpoints-evidence.json) binds38 changed current source/inventory files, one moved/deleted source path and local receipt hashes against base `151d4cbfd52e97d172a6ba7ab3dcd399ad9d5caf`.

Commands actually executed with SDK10.0.403 and locked packages include Administrator/Host/contract builds with --no-restore -m:1; full Host native execution; `python3 scripts/verify.py PostgreSql --partition administrator --story Applications` and the same selection for SqlServer; `python3 scripts/verify_contracts.py core`; exact native discovery; npm generation/type checks;67 selection controls; syntax-aware quality, formatting/import, instruction/assurance and diff checks. Unused unchanged pinned database images were exchanged sequentially to fit local VFS storage; TLS, budgets and acceptance rules were preserved.

T008/T009 are complete locally. T007 remains unchecked for retained Chromium/WebKit execution after the prior download403; no new browser waiver or pass is inferred. Next: T010 managed-client native profile/admission/cache/decision, then genuine managed authentication/consent/code-token and UI. No create/edit screen, full feature acceptance, full CI, merge, refresh activation or historical/human/public-release gate closure is claimed.

Independent final evidence review passes: both exact35-case provider reports, all Host/native/Node/selection results,38 current source hashes, the moved source deletion and47 local artifact hashes were independently verified. All2,107 prior native metadata records and their existing ordering are preserved;18 additions are appended without duplicates. The initial fixture failure and earlier nonfinal PostgreSQL reports remain excluded from final acceptance.


## T010 managed native cache and decision — 30 October 2026

[Implementation and limits](managed-cache.md) document the separate Development managed profile, real native request-token caching/self-redirect, managed browser binding, locked current policy and finite native/interaction capacity. Exact native creation, complete persisted attachment, original deadline, commit and actual disposal precede publication. Restored native intent and the real request/browser/revision are checked in a fresh locked decision child; a valid resumed request deliberately returns an empty local 503 until T011 supplies fresh managed-purpose password/MFA and explicit consent. No administrator session, BFF grant, consent, native redemption or code/token is produced.

Final scoped runtime results: **31 ManagedApplications, 110 ProtocolAdmission and 35 Applications cases on each real provider; 1,761 Host and 67 selection controls pass**, with zero required failures/skips in these final selections. The 25 new provider cases use actual Kestrel HTTPS and authenticated creation, real native persistence, actual commit/disposal callbacks and two-root provider wait graphs. Twenty-six new Host cases cover composition, initial demand isolation/correlation, framing and nonextendable publication. Existing native characterization remains separately scoped. All 2,125 prior administrator metadata records and existing order are preserved with 50 additions for 2,175; ManagedApplications 31/provider, ordinary totals 970 PostgreSQL/1,075 SQL Server, browsers 65/provider. Existing 80 coverage inputs / 15 floors remain unchanged; no new coverage measurement is claimed.

Independent architecture/security review resolved immutable reservations, current watermark/native proof, original expiry propagation, cached-only passthrough observation and race-task cleanup. Independent test review added full cookie/body and actual native-FK assertions, foreign genuine browser, actual native dependency retention, mandatory completed mutation injection and initial managed-demand isolation with exact correlation boundaries. Corrected scoped source/inventory reviews pass. [Evidence manifest](managed-cache-evidence.json) binds current source/inventory files and retained receipt hashes against base `763987326b8fa22927278daa569e9e8f49006b82`.

Earlier SQL attempts failed during initial native passthrough and mixed native/policy identifier comparison; the final implementation delegates initial passthrough unchanged and applies the registered provider's ordinal collation. Exact historical SQL error number remains unproven. The intermediate 19-case SQL run's two failures were new fixture defects (missing Watermark mutation arm and Expired completion flag); successful actual injection is now required. Temporary diagnostic output was removed. Initial compiler/format/import/quality findings and stale aggregate selection totals were corrected without weaker gates or allowance growth; failed/nonfinal receipts are excluded from acceptance.

Commands actually executed with SDK 10.0.403 and locked packages include Administrator/Host builds with --no-restore; full Host native execution; `python3 scripts/verify.py <provider> --partition administrator --story <story>` for both PostgreSql/SqlServer and ManagedApplications/ProtocolAdmission/Applications; exact native JSON discovery; 67 selection controls; syntax-aware quality, whitespace/import verification, instruction/assurance and diff/link checks. Pinned images were exchanged sequentially after active containers exited to fit local Docker VFS storage. Clearing the task's NuGet HTTP download cache preserved installed packages. TLS, original deadlines and provider/no-skip obligations remain unchanged. All 345 assurance controls remain not assessed.

T010 is complete locally; next is T011 managed-purpose fresh password/MFA and explicit consent, then T012 code/token, T013 full managed-client journeys and T014 UI. T007 retained Chromium/WebKit execution remains pending after the documented Playwright CDN 403. No new browser waiver, browser pass, full feature acceptance, full CI, merge, refresh activation, production correction or historical/human/public-release gate closure is claimed.

Independent final evidence review passes this bounded checkpoint: all 51 current source hashes and 80 local artifact hashes match; both providers’ exact 31/110/35 selections match the native inventory and passing raw TRX results, with zero failures/skips. Host has 1,761 distinct passing test IDs and selection controls: 67 passes. All 2,125 prior metadata records and ordering remain intact with 50 additions. SQL’s pre-formatting binary limitation and the pending managed human/browser/full-CI/merge boundaries remain explicit.


## T011 fresh managed authentication and consent — 30 October 2026

[Implementation and traceability](human-authentication.md) describe genuine restored-native request entry, separate managed browser/protected form, fresh password and unused native MFA, explicit consent and terminal approval/cancellation. Shared native credential primitives run under a distinct managed purpose in one owned transaction; generic administrator completion and commit paths cannot issue authority. Exact immutable/account/ceremony snapshots, current application/native state, atomic audit and original-deadline publication surround internal saves and actual cleanup. Genuine native old-hash upgrades accept only their successfully persisted native hash/stamp successor. No administrator session/cookie, completion grant, BFF ticket or native code/token is created.

Final scoped execution passes **259 PostgreSQL and259 SQL Server cases (ManagedApplications50, Ceremonies41, Accounts58, ProtocolAdmission110 per provider), plus1,773 Host cases**, with zero failures/skips in all nine final reports. Strict reconciliation confirms exact native story identities, unique IDs, completed summaries, executed counts and every non-pass counter zero. Final independent evidence review passes this bounded local checkpoint. [Source and receipt hashes](human-authentication-evidence.json) bind50 current source/inventory files and114 local artifacts against base `b6154cf7f3c48ff74347584d2a805f2d47e8de64`. The initial SQL Accounts process aborted after an Aspire health failure and SQL file-expansion OS error31; failed/incomplete receipts remain separate. Docker inspection found23.76GiB of unreferenced VFS directories; reference/mount-checked cleanup reclaimed only disposable orphan layers before rerunning Accounts. No exact deeper Aspire failure cause is claimed.

Native discovery preserves all2,175 previous metadata records and existing order, appending38 new provider records for2,213. The affected managed selection is50/provider; ordinary totals are989 PostgreSQL and1,094 SQL Server. Browser inventory stays65/provider, and80 coverage inputs/15 floors remain unchanged. This checkpoint makes no new coverage measurement or browser-engine claim. Selection controls pass67; code quality, instruction validation and assurance snapshot integrity pass. All345 assurance controls remain not assessed.

Commands actually executed with SDK10.0.403/runtime10.0.14 and locked packages include Administrator/Host builds with `--no-restore`; full Host native tests; `dotnet test --project tests/aspire/IdentityPlatform.Administrator.Tests --no-build --no-restore --no-progress --filter "FullyQualifiedName~.Features.<story>.&Provider=<provider>" --report-trx` for ManagedApplications/Ceremonies/Accounts/ProtocolAdmission on each provider; exact native JSON discovery;67 selection controls; syntax-aware quality, formatting/import, instruction, assurance and diff checks. Strict receipt reconciliation requires exact native story identities and every final case passed, not just aggregate counts. PostgreSQL/Host used build17 and SQL uses build18 after unused-import cleanup in six files. Independent review confirms all2,213 portable metadata records agree, but cannot reproduce the operator-recorded source-line equality because before-source copies/hashes were not retained.

Independent architecture/security and test source reviews pass after immutable state, real managed correlation, exact redirect/body, full session/grant/ticket evidence, second-browser CSRF, completed fault injection, actual lock waits with same-code recovery, ownership cleanup and native rehash/failure controls. Final raw paired-provider evidence review verifies all50 source hashes and114 artifact hashes, the exact259 passing identities on each provider,1,773 distinct Host identities and67 selection controls. All2,175 prior metadata records/order are exact with38 additions. Failure provenance and the qualified build17/build18 source-comparison record remain explicit. Earlier failed/nonfinal fixture/build/socket/storage receipts remain excluded from acceptance, as detailed in the implementation record.

**T011 is locally complete.** Next implementation is T012 native request/code consumption and code/token issuance, then T013 whole-created-client tests and T014 create/details UI. T007 retained Chromium/WebKit execution remains pending after the documented Playwright CDN403; Firefox initial-phase exclusion persists. No full feature acceptance, full CI, merge, refresh activation, production correction or historical/human/Windows/A-009/vendor/public-release gate closure is claimed.


## T012 managed native code/token issuance — 1 November 2026

[Implementation and requirement map](native-issuance.md) describe actual native authorization-code and Basic/S256 token issuance after fresh managed password/MFA and explicit consent. Separate durable native validation/redemption commits and disposes before later issuance. Current locked application, credential, account, factor, ceremony and original-expiry checks bind the exact native input, prepared claims and original response parent. Native creation, interaction consumption, retention and sanitized audit commit atomically; sealed output publishes only after actual acknowledgement and cleanup. Identity-only tokens cannot authorize the real Admin API. Native replay revokes linked tokens while an unrelated grant's issued tokens remain valid and unchanged.

Final-source local execution passes **101 ManagedApplications, 110 ProtocolAdmission, 58 Accounts and 41 Ceremonies cases on each real provider**, plus **1,820 Host cases**: 2,440 passing executions, zero final failures/skips. Raw reports have completed summaries, exact non-pass-zero counters and one-to-one result/definition/entry/execution linkage. Provider identities exactly match their native inventory selections. Host preserves all 1,773 prior portable definitions/multiplicities and adds 47; its two pre-existing truncated theory-display collision groups retain distinct executions without dropping any case. Native discovery preserves all 2,213 prior administrator metadata records and provider order, adding 102 for 2,315. Selection controls pass 67. Existing browser inventory, 80 coverage inputs and 15 floors remain unchanged; no new coverage measurement or dedicated mutation-testing run is claimed.

Administrator build18 and Host build5 use SDK 10.0.403/runtime 10.0.14 with locked OpenIddict 7.7.2 packages, one MSBuild node and no node reuse; both have zero warnings/errors. All nine accepted runtime reports use the final source, including the response lifetime ceiling and corrected native cached-input error expectation. The retained 63-file C# snapshot matches every changed C# file without later edits. [Evidence manifest](native-issuance-evidence.json) binds 67 source/inventory files and local artifact hashes against `2103478da0974409005d06bd4f0420288852ce34`. Earlier failed/nonfinal builds, quality attempts and runtime receipts remain separate, including the environment interruption and unproven parallel MSBuild termination cause. Workspace hashes are not a claim of hosted artifact upload.

Independent architecture/security and test reviews resolved original parent/input ownership, exact native destination metadata, prepared-principal restrictions, protected original nonce/private claims, native error phases and response lifetime bounds. Tests prove independently observed durable redemption, actual provider audit denial, completed internal/final mutations, current valid-versus-redeemed drift, exact race token/audit deltas, native cryptographic/row bindings and original expiry after actual disposal. Independent final raw-evidence review passes the scoped source, inventories and receipt reconciliation. Quality, instruction validation, assurance snapshot integrity and diff checks pass; all 345 assurance controls remain not assessed.

**T012 is complete locally.** T013 already has its core created-client success, invalid-proof denial, drift, successful-code replay and late-save/audit-failure replay evidence. Finish only the remaining explicit consent-cancellation continuation, successful cached-request replay and two-real-client substitution checks, reconcile that evidence, then proceed to T014's visible create/details/one-time-output UI. T007 retained Chromium/WebKit execution remains pending after the previously documented CDN 403; no blocked download was retried. Full feature CI/review/squash remain T022/T023. No refresh activation, full feature acceptance, production correction or historical/human/Windows/Firefox/A-009/vendor/public-release gate closure is claimed.


## T013 genuine created-client journey closure — 1 November 2026

[Journey evidence and requirement map](created-client-journeys.md) close three managed-product gaps after T012 without changing production code. Real cancellation cannot reopen its original authorize request; successful cached-request replay cannot issue again or alter existing credentials; two independently API-created clients cannot substitute cached requests or authenticated codes in either direction. Rightful exchanges retain their original verifier/callback and independently validate actual JWKS signatures, identity audience/authorized party, nonce, access hash, native row bindings and expiry. Full rejection digests retain all native token/authorization fields, product policy, human/audit state and administrator authority without normalizing replay effects. Native application descriptors are outside that digest's stated scope.

Final build4 uses SDK 10.0.403/runtime 10.0.14 and existing locked packages, one MSBuild node and no node reuse, with zero warnings/errors. **104 ManagedApplications and 35 Applications cases pass on each provider: 278 exact executions**, zero failures/skips and completed result summaries. Native discovery preserves all 2,315 previous metadata records and provider order, adding six for 2,321. Each raw report exactly matches its provider/story inventory with distinct IDs and one-to-one result/definition/entry/execution linkage. The ordinary totals become 1,043 PostgreSQL and 1,148 SQL Server; browser inventory stays 65/provider. Selection controls pass 67. No new Host, browser-engine, module coverage or dedicated mutation-testing run is claimed; prior T012 evidence remains explicitly separate.

Independent architecture/security review required retaining the pre-existing global interaction/audit defaults and checking returned API client IDs/callbacks against the submitted values before using them as expectations. Both corrections are implemented; only new multi-client success checks explicitly opt into application-scoped reads. Independent source and final raw-evidence reviews pass. [Evidence manifest](created-client-journeys-evidence.json) binds 12 current test/inventory files and local receipts against `ccfe3c3159a878fc12dfb11e0c1fc136bd9f5e23`. The initial build-format failure and focused PostgreSQL error-name mismatch are retained separately. The latter expected invalid_request but the earlier existing managed binding guard rejects invalid_token; the corrected test requires that exact native local 400 while retaining state-equality and rightful-exchange controls. Quality, instruction/assurance integrity and diff/link checks pass, with no new legacy allowances. All 345 assurance controls remain not assessed.

**T013 is complete locally; T014 visible create/details/one-time-secret UI is next.** The generated create/details and antiforgery BFF contracts already exist. T007 retained Chromium/WebKit execution remains pending after the documented CDN 403; no blocked download or Firefox test is repeated. Full-feature review/CI and normal squash remain T022/T023. No new screen, full feature acceptance, refresh activation, merge, production correction or historical/human/Windows/Firefox/A-009/vendor/public-release gate closure is claimed.

## T014 visible create/details UI — 1 November 2026

[Implementation and retained acceptance work](create-details-ui.md) and [exact evidence](create-details-ui-evidence.json) cover the new registration form, generated fixed CSRF/create/details transport, resource-bound response validation, original authority/deadline ownership, one-time output dismissal and ambiguous-result guidance. FR-001 maps to form/details and hosted create; FR-009 to native proof/fixed transport/one-send; FR-010 to exact input/ETag/deadline/current authority; FR-011 to generated contract consumption and independent embedded-asset comparison; FR-012 to preserved identities, six per-module frontend floors, both real providers and independent reviews. SC-005 browser/keyboard/theme/reflow acceptance remains pending.

The scoped implementation preserves the original 11 session and 9 list Node identities/runners and adds 26 exact editor cases. Real hosted verification adds one Applications case per provider while preserving all 2,321 prior administrator records/order. Final scoped results are 36 Applications/provider, 1,820 Host, 46 Node and 67 selection controls passing, with zero final failures/skips. The manifest binds complete TRX linkage, unchanged Host portable identities/multiplicities, deterministic assets, raw failures and reviewed source. No C# coverage/mutation gate is reduced or newly claimed as executed.

Source architecture/security/test/UX reviews pass after correcting callback ordering, managed metadata and pre-dispatch expiry, stale response fences, independent deadline tests, failed-validation markup and list focus/expiry guidance. The full native AppHost container demo and Chromium/WebKit DOM journeys remain not run following the documented download-policy blocker. **T014 remains unchecked**; no full CI, PR or merge is claimed. Continue T015 tests and T016/T017 implementation while retaining T007/T014/T019 acceptance obligations and every historical/external gate.


## T015 edit/replacement atomicity — 4 November 2026

[Scenario mapping and limits](update-atomicity.md) and [exact evidence](update-atomicity-evidence.json) complete the bounded provider-test requirement for FR-005–008/010. Four independent-root same-version races and four native-save/audit-failure cases per provider use real lock-wait observations, distinct commands, complete native/hash/policy/audit snapshots and exact original-version recovery. Both complete Applications selections pass 44 cases, with zero failures/skips and full result/definition/entry/execution linkage. All 2,323 prior native records/order remain exact;16 additions bring discovery to 2,339. Selection controls pass 67. No product source or prior test identity changes.

Source architecture/security/test reviews pass after exact effect/exception attribution and cancellation-safe cleanup corrections. The evidence records final runtime review separately. T015 is locally complete as component/provider verification; the global quality check **fails** because the unchanged BootstrapTests CQ008 allowance expired 2 November 2026. All 1,695 finding identities remain unchanged and no new C# finding is introduced. The allowance is not renewed, no failed check is counted as a pass, and T022/full verification/merge remains blocked. T016 endpoints and T017 UI are next; prior browser/native-demo and all historical/external gates remain open.


## T016 edit and secret-replacement HTTP — 4 November 2026

[Endpoint acceptance](update-http.md) and [exact local receipts](update-http-evidence.json) cover genuine API/BFF If-Match PUT and explicit replacement using the existing T006/T015 coordinator. Current write/credential permissions remain independent, with persisted fresh MFA for replacement, strict native JSON/CSRF/bearer admission, one-time secret output and no retry. Expired existing credentials remain editable/recoverable. Native OpenAPI and generated TypeScript expose the actual operations and required preconditions.

Final PostgreSQL and SQL Server Applications suites each pass 59; Host passes 1,830; native contracts 44, Node compatibility 40 and selection controls 67 pass. All prior 2,339 native records/order remain exact with 30 additions; prior 1,820 Host, 44 native contract and 38 Node identities are preserved. Actual native token/continuation expiry is observed independently; nearest-second expires_in rounding is bounded to ±500ms, and API/BFF deny at actual native expiry. Isolated MFA 403/edit 200 proof remains explicitly attributed to the T006 component test. Earlier failed fixture/session/expiry assertions remain recorded and excluded from final acceptance.

Independent source/security/test reviews pass after complete response, protected-target, replay and native deadline oracle corrections; final raw review is recorded in the manifest. T016 is locally complete; T017 visible forms are next. Global quality still **fails** on the unchanged expired BootstrapTests exception, with all 1,695 finding identities unchanged. Full contract verification also **fails** at npm audit on GHSA-g3hv-75wg-m249 in the unchanged generator chain. Separately passing functional checks do not override either failure. T022/full delivery, browser/native-demo, all historical/external gates and refresh-disabled status remain unchanged; no full CI, PR or merge.

## T017/T018 editor and recovery — 4 November 2026

[Implementation, traceability and limits](editor-recovery.md) and [exact evidence](editor-recovery-evidence.json) bind the new UI, genuine edited/replaced issuer journeys and retained browser/demo test source. Clean provider reports each pass 107 cases (106 ManagedApplications plus 1 shipped-asset HTTPS witness), with 55 UI tests and 83 selection controls. Architecture/security, test and UX source reviews accept the corrected behavior. A later diagnostic-only test assertion correction removes protected-reference formatting on failure; three focused cases per provider pass on the final DLL, without double-counting unique coverage or claiming the 107-case reports used that final DLL. All 2,369 previous native metadata records/order and 26 previous editor identities are preserved, with 6 native records and 9 editor cases added; module floors remain 95/90/100.

T017/T018 complete the bounded local implementation. T019 remains unchecked: browser/demo code compiles but the required engines are unavailable and blocked downloads are not retried. Actual browser consent/history/retained-artifact acceptance is still required; Firefox remains excluded. Global quality still fails on the unchanged expired feature009 exception, and the unchanged contract-generator advisory remains unresolved. No full CI, PR/merge, new Host/contract/coverage/mutation campaign, refresh activation or external release acceptance is claimed.

## T020–T022 contracts and final verification — 4 November 2026

[Current remediation and limits](delivery-checks.md) and [mutation receipts](mutation-controls-evidence.json) supersede the earlier open generator-advisory/expired-exception status without rewriting historical failures. T020 passes the complete local core contract gate: 44 native and 44 exact Node cases, real zero-network-effect native exports, deterministic generated output, TypeScript consumers, five-document compatibility with main and the clean 76-entry audited toolchain. T021 passes 26 behavioral kills and 52 baseline/restored cases on real PostgreSQL/SQL Server. Existing cases, UI identities, coverage inputs and floors remain preserved; the bootstrap refactor has an explicit 46-case rename ledger.

T022 remains open for full exact-head hosted verification, aggregate coverage and retained browser/native-demo acceptance. Source reviews and scoped local gates do not close those obligations. T007/T014/T019 and all six historical/human/Windows/A-009/vendor/production/public-release gates stay open; refresh remains disabled. The final local checks and delivery candidate are recorded in the linked remediation record.
