# T018 protected native issuer end-session

**Historical implementation candidate — 25 October 2026; superseded by [accepted delivery](#accepted-internal-delivery--25-october-2026) below.** Base `411245d48ffb93efe4c9de62edd6077b61d18f15`; branch `095-b3-issuer-logout`. T018 remains unchecked until exact-source hosted verification and independent evidence review pass. This continues accepted T015/T017 and reuses the native end-session validation and audited fixed-registration upgrades delivered in PRs #72–74. Those prerequisites are not implemented again.

## Scope and implementation

FR-010/011/013; B3-END-02/03. Explicit `AddIssuerLogout` composition exposes the fixed `/connect/end-session` route only when deliberately installed. Production Programs are unchanged. The request contains only the fixed client, exact registered return URI, noncredential source selector and opaque state. It accepts no ID/access token or client credential. Exact same-origin protected POST requires both genuine native antiforgery and a purpose-separated Data Protection confirmation bound to the current administrator subject/session and every original native field. Confirmation expires at the earlier of five minutes and original source expiry; rollback and equality deny. GET displays confirmation without revocation or logout audit. Named-cookie validation may update ordinary activity metadata.

The actual OpenIddict validation context and request identity are retained across native dispatch. The separately validated return URI and request fields remain immutable. Actual `JourneyLogout` rereads current named administrator authority under existing account serialization and atomically commits only that source's revocation/revision with the actor-bound logout audit. Its new final persistence guard rechecks original source and confirmation time after SaveChanges and before commit. A private request-bound receipt can be created only by this real operation after transaction disposal. It retains pure time bounds, not a live context. The owned response child waits for service/context cleanup before publication.

Cookie clearing and actual native `ProcessSignOut` follow committed revocation. The installed native query formatter creates the redirect; a private observer permits only the original validated return plus exact state, no other response fields, no fragment and no body. It freezes status, headers and output across late native callbacks and scope cleanup. Publication uses a dedicated verified-logout outcome with the genuine receipt and zero protocol write phases; it does not invent a dummy native transaction or treat the mutation as a read-only response. Lost commit acknowledgement, failed cleanup, expiry and mutated native output suppress all success material and return finite unavailability without falsely claiming rollback.

The exact B3 manifest is observed again before revocation. This read-only observation rejects completed disablement/drift but **does not serialize concurrent external profile changes**. Source revocation itself remains serialized by the existing account ceremony. Other issuer sessions, tokens, grants and BFF sessions are not revoked by this route.

## Decisive controls

| Test class | Boundary and evidence |
| --- | --- |
| `IssuerLogoutTests` | Genuine fresh password/MFA, native BFF callback and named issuer cookie; GET has no revocation/audit; explicit protected POST revokes exactly the fresh source, emits one exact actor/target logout audit and preserves native token/grant hashes. Replay denies. Another genuinely current same-account source can load its own confirmation but cannot submit the original source's form. Cross-origin, changed state and invalid native antiforgery preserve the original positive control. |
| `IssuerLogoutBoundaryTests` | Wrong client/return/source, credential-bearing hint, duplicate field and missing cookie deny without mutation. Completed profile disablement and independently committed source logout deny. Competing protected submissions produce one return and one audit. This last test does not claim a witnessed database lock order. |
| `IssuerLogoutFailureTests` | Actual accepted-logout save interception crosses original confirmation expiry/rollback; zero commits and zero audit/revocation precede a restored same-form positive. Actual provider audit INSERT denial rolls back; restored permission allows success. Actual commit acknowledgement loss, disposed-context cleanup failure and expiry during cleanup retain exactly one durable logout while publishing no success. Probes exclude earlier cookie-validator metadata commits. |
| `IssuerLogoutDeadlineTests` | Genuine enrolled source and actual `JourneyLogout`, independently of confirmation: original idle/absolute expiry during SaveChanges preserves original revision/activity with zero revocation/audit/commit; removing the fault lets the same source revoke once. The absolute case deliberately seeds recent activity to isolate absolute expiry. These are mechanism-level provider tests, not HTTP-cookie acceptance. |
| `IssuerLogoutNativeTests` | Mutation of the separately native-validated destination, an extra native response credential field and late OnStarting output each have observed reach and actual-commit witnesses, exact 503/empty output/no cookies/no return, and independently inspected durable logout. |
| `IssuerLogoutPolicyTests` / `IssuerLogoutTransportTests` | Pure Data Protection field/subject/session/key-purpose/time binding, retained source rollback/idle/absolute limits, exact field multiplicity, actual bounded body length, contradictory framing, transfer encoding, pre-parsed form, origin, cookie duplication, HTTPS and actual GET-body denial. These do not simulate authenticated acceptance. |

Session selectors, cookies, confirmation tokens and redirect state remain in memory. Assertions on private collections/responses use scalar or Boolean comparisons, so failures cannot dump selector dictionaries or response objects. Synthetic native secret/output faults use fixed markers; no production credentials are involved.

## Historical selection and local evidence

SDK 10.0.401; OpenIddict 7.7.1. Host and Administrator builds pass with zero warnings/errors. Focused Host controls pass **24/24, zero skips**, 1.236 seconds (`/tmp/b3-issuer-host-final/IssuerLogoutPolicy.trx`). Native discovery preserves all 1,761 prior Administrator identities and complete portable metadata, adding 36 cases, for **1,797**. Both actual provider traits and all six actual filtered discoveries are checked. BrowserSessions is **278/provider**: unchanged Foundations 158 and Product 96, Lifecycle 24. Ordinary Administrator counts are PostgreSQL **833** and SQL Server **938**. Host is **1,481** and unit total **1,576**. These are discovery counts, not provider pass claims.

The first focused Host attempt retained 21/23 passes and two failures: raw authority comparison incorrectly rejected an explicit default HTTPS port. Normalizing the host's missing/default port corrected this product guard; the corrected 23/23 run and final expanded 24/24 run are retained separately. One local PostgreSQL positive attempt failed during Aspire host start/cleanup before any logout assertion (one failed, zero passed/skipped, 93.374 seconds). It was not repeated or credited as product evidence. Local storage remains constrained; actual both-provider acceptance is assigned to required hosted jobs.

Source quality and assurance checks pass without allowance growth. All 226 Python regressions pass after updating their independently asserted expected counts to the new reviewed inventory. Complete discovery and independent source review are recorded with the candidate. Architecture, security and test reviewers required original deadline enforcement, immutable separately validated URI, actual body bounds, complete Provider traits, strict postcommit output assertions and private failure diagnostics; these corrections are included. Source review is not runtime acceptance. Required workflow selection remains 73 jobs, 56 coverage inputs and 15 unchanged module floors; no timeout, legacy allowance, coverage exclusion or prior test was removed.

## Candidate-stage remaining work and gates

Exact-source hosted tests, coverage/raw-report and independent evidence review are pending. T018 stays unchecked. T019 must implement the BFF continuation/return and accurate local/remote status; this issuer route alone does not supply it. No refresh capability, global logout, grant revocation or production activation is introduced. Actual trusted TLS, browser/theme/accessibility and broader logout acceptance remain T020 and the existing deferred acceptance tasks. The TestServer HTTP fixtures are not socket/TLS/browser-engine evidence.

T003/T004 remaining integration, T012/T016 browser evidence, T019–T024, A-009/T028, PR #69/issue #70, all six findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open. Expired diagnostic approvals are not revived.

## Rejected first hosted candidate and correction — 25 October 2026

PR #93 source `0fda3c2e7d636b409ad4cdeb47d9819f37847c87`, tested merge `f402abd356d74a009cc3e1a18115f8f1c940b461`, identical tree `c8c2e0ba8fcc07575fe8b9cf4417ed90b14b919e`: [run 40230383276](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40230383276), attempt 1. Static formatting and both Lifecycle jobs failed. The independently inspected raw Lifecycle reports each have 24 executed, eight passed, sixteen failed and zero skipped on PostgreSQL and SQL Server. The four unit reports pass all 1,576 cases, including Host 1,481. All sixteen HTTP cases fail the first genuine confirmation GET with 503 instead of 200, before their intended controls. Six prior Lifecycle cases and both new source-deadline mechanism cases pass. This does not establish later HTTP revocation or publication behavior.

The executed coverage reaches `IssuerLogoutFlow.HandleAsync`'s `Select(Owned)` and no subsequent line. The actual `ProtocolStorageScope.Create` already selects Owned for that child. Repeating selection correctly trips the existing fail-closed owner latch. The narrow correction replaces the redundant selection with `Require(Owned)`; it does not relax ownership or any test expectation. Architecture review independently confirmed source and raw executed-line evidence. Existing positive HTTP controls remain permanent regressions for this defect.

The static job reports import ordering in six files plus one unused test import. Apply the actual formatter and retain its unchanged verification rules. Corrected source requires a new hosted run; the failed run and its raw reports remain rejected evidence, with no acceptance or coverage-floor claim.

## Rejected second hosted candidate and native token correction — 25 October 2026

Corrected source `8b33f983f306ab9279e0b5f27f2c276a23dae00c`, tested merge `973775fe1b2a24f27608d3d376b50a277f88ef53`, identical tree `5fd8fcd2fdc3d54b273e9e7fa380ee258de3fde8`: [run 40231202195](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40231202195), attempt 1. Static analysis and all 1,576 unit tests pass. Each independently recounted Lifecycle report has 24 executed, sixteen passed, eight failed and zero skipped. Initial confirmation now loads. Seven positive/restored-control cases expect native 302 but receive 503; competing submissions have zero successful returns. The three native mutation cases, publication-failure cases, direct deadlines and earlier lifecycle cases pass, but positive return failure prevents acceptance.

Raw executed lines reach actual native formatting, successful output verification and post-child-cleanup checks; frozen-parent rejection and native dispatch token-substitution throw sites are unhit. The pinned OpenIddict 7.7.1 ASP.NET handler constructs `ProcessSignOutContext` and sets its cancellation token from `HttpContext.RequestAborted` **before** entering the dispatcher. Native contexts share `Transaction.CancellationToken`. Inside the owned response child this initializer replaces the suspended parent's token with the child's token. The dispatcher correctly restores its own entry token, but child cleanup subsequently cancels it. Copying even an empty redirect body then fails at response StartAsync before outer publication.

The correction encloses the actual native ASP.NET sign-out API: retain the exact prior transaction token, keep the real child request token during native work, verify that native completion returned with that child binding, and restore the original token in `finally`. No cancellation, owner-failure, native identity, deadline or frozen-output guard is disabled. Architecture review independently confirmed the pinned native source and this trace. The existing real positive POST remains the integration regression; an additional both-provider native event deliberately substitutes `CancellationToken.None`, requiring observed reach, one actual commit and exact empty 503 without cookies/return. This negative control ensures restoration cannot excuse foreign token substitution.

The two rejected runs retain their reports and source bindings. Corrected token restoration still requires new exact-source provider/full-workflow evidence; no failed or partial run closes T018.

The token-corrected candidate preserves all 1,797 prior identities/metadata and adds the two native cancellation cases, totaling **1,799**. All six actual native group discoveries match. BrowserSessions is now **279/provider** (158 Foundations + 96 Product + 25 Lifecycle); ordinary Administrator is **834 PostgreSQL / 939 SQL Server**. Host/unit counts remain 1,481/1,576. The cancellation substitution is an end-to-end integrity control and may be rejected by the existing dispatcher before the adapter's final check; it is not an isolated mutation proof of that new comparison. Independent architecture, security and test source reviews accept this bounded correction, with runtime acceptance still pending.


## Accepted internal delivery — 25 October 2026

[PR #93](https://github.com/mosesparadise/OpenIdentityAdmin/pull/93) was normally squash-merged as `12c2d2e6e866a407b46c8c1af7525aeb95a123e1` after independent architecture, security, test and raw-evidence review and all 73 required exact-head checks passed. There were no GitHub review submissions or inline review comments and no requested reviewers; the independent source/evidence dispositions above are agent reviews, not human approvals. GitHub reported a clean mergeable head; no restriction was bypassed. T018 is complete for internal development. The earlier pending wording records historical checkpoints.

| Binding | Verified value |
| --- | --- |
| Source | `8351389f8a1ea80d82835707ac2c0cd86f2b184e` |
| Base | `411245d48ffb93efe4c9de62edd6077b61d18f15` |
| Tested merge | `bd3856d832af5f3d7c4d3028a89883bafbbb4baf` |
| Source/tested-merge/squash tree | `1547b32ee75e594978e949d62cab801124c1cd5d` |
| Workflow | [40232412700](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40232412700), attempt 1, 73 success / 0 failure / 0 skipped / 0 cancelled |
| Coverage / final acceptance jobs | `121291250322` / `121291353047` |
| Independent local receipt SHA-256 | `61987c573fc0ca9130d9f831b5d880eeb5e2da2b36ec7f1ba7ea8d36facf3b5f` |

The fetched squash tree equals the source and actual tested merge tree. Native discovery preserves all 1,761 base Administrator identities and metadata plus 38 new cases (1,799 total). Actual six provider/group discoveries reconcile to Foundations 158, Product 96 and Lifecycle 25 per provider. Both raw Lifecycle reports pass 25/25 including genuine positive native return and all four native fault variants. Unit reports pass 1,576 including 1,481 Host; scoped unit plus BrowserSessions is 2,134 passing executions across ten reports. All prior 1,552 unit identities/multiplicities are preserved plus 24.

Independent review directly inspected all 63 retained ordinary TRX reports: **4,300 executed/passed, zero failed**, preserving all 4,238 prior report name/class/method multiplicities plus 62 (24 Host and 19 Lifecycle per provider). This counts executions, not unique source test identities. The selection reconciles 4,073 native-profile executions + 172 browser executions + 57 contract TRX executions minus two exact A-009 WebKit certificate deferrals. The actual two WebKit deferral receipts declare `deferred_not_run`, `cause_established:false`, `acceptance:false`; each corresponding 29-case raw selection equals the 30-case Chromium selection minus only that named case. Neither deferred case is credited as passed. Contract reports pass 43 core, seven Chromium and seven WebKit; 37 Node cases pass separately. Firefox stays deferred.

All 56 required raw coverage XML inputs were directly inspected from 57 foundation ZIPs. Recomputed floors pass for all 15 modules with unchanged 90% line / 80% branch policy, no allowance or exclusion growth. The table below records exact counts; modules with no branch sites retain the established no-branch policy.

| Module | Covered / valid lines | Covered / valid branches |
| --- | --- | --- |
| `IdentityPlatform.Core` | 91 / 92 | 7 / 8 |
| `IdentityPlatform.Infrastructure` | 6529 / 6996 | 605 / 624 |
| `IdentityPlatform.ServiceDefaults` | 61 / 61 | 18 / 18 |
| `IdentityPlatform.Migrator` | 608 / 616 | 72 / 72 |
| `IdentityPlatform.AuthServer` | 4721 / 4985 | 674 / 682 |
| `IdentityPlatform.Admin.Api` | 71 / 71 | 10 / 10 |
| `IdentityPlatform.Admin.Bff` | 2698 / 2866 | 598 / 608 |
| `IdentityPlatform.Worker` | 26 / 26 | 0 / 0 |
| `IdentityPlatform.AppHost` | 152 / 153 | 14 / 14 |
| `IdentityPlatform.Database.Abstractions` | 111 / 112 | 4 / 4 |
| `IdentityPlatform.Database.PostgreSql` | 96 / 96 | 2 / 2 |
| `IdentityPlatform.Database.SqlServer` | 93 / 93 | 6 / 6 |
| `IdentityPlatform.Database.Hosting` | 42 / 44 | 4 / 4 |
| `IdentityPlatform.Database.PostgreSql.Hosting` | 13 / 13 | 0 / 0 |
| `IdentityPlatform.Database.SqlServer.Hosting` | 13 / 13 | 0 / 0 |

All eight actual mutation ZIPs were inspected: 48 decisive kills, 48 passing baseline controls and 48 passing restored controls, with source/restored/mutant hashes and 24 oracle bindings checked. These ZIPs retain sanitized receipts, not raw mutant TRX; direct raw-mutant-report review is not claimed. Actual production exports, dormant session contract and six exporter control receipts match committed documents. Syscall traces/intermediate control documents were not retained; screenshots were not visually re-reviewed. Native selection metadata comes from actual local discovery; hosted ZIPs retain raw TRX/coverage, and checkout logs bind the exact tested merge.

### Retained hosted artifact bindings

Every artifact below belongs to run `40232412700` and source `8351389f8a1ea80d82835707ac2c0cd86f2b184e`. IDs locate the hosted ZIPs; hashes bind the actual downloaded bytes. Local audit receipts are workspace evidence, not a permanent repository archive.

| Artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| `administrator-mutations-PostgreSql` | `12613788883` | `43aa08ff2bc54550b1dcd18936087a7d7366e096d32ca7509a04b2b95ce2dceb` |
| `administrator-mutations-SqlServer` | `12614175778` | `fac3fce3a7155b569046b1987f9d86d1ecc546bebf563fffdfc7c01bf760dbae` |
| `administrator-protocol-mutations-PostgreSql` | `12613479194` | `d9029505152bca521224513e9af71a6e812ad86234683dcdf846c53b06ef9a08` |
| `administrator-protocol-mutations-SqlServer` | `12613669073` | `5f9d72c116775dcbcbc097ddb09e622abce6030f9fd89e20c90528e5dbcc2635` |
| `critical-mutations-PostgreSql` | `12613173274` | `7a88b10ae8bf1854df5144a0d9d5f5c8db34f5611e38ce01554a4bade6831a42` |
| `critical-mutations-SqlServer` | `12613093539` | `54b1db07ff150e04dab87a618561d96dcdcfa3030b7cdef209ee65a754965da2` |
| `foundation-administrator-PostgreSql-browsersessions-foundations` | `12614140044` | `2f9902a041302632d66b51b81974a14bed6a8287c71c33ce993307030ba51a4f` |
| `foundation-administrator-PostgreSql-browsersessions-lifecycle` | `12613383976` | `16001a58a3746127c4e039575ced0ad58dde8baba80fa6ac889439353834b234` |
| `foundation-administrator-PostgreSql-browsersessions-product` | `12613554099` | `106718c09698e5286d7ce735033cb88b1e98a3e3a394c6a9f5fef18a1e206e27` |
| `foundation-administrator-SqlServer-browsersessions-foundations` | `12613479934` | `09a5ce6cea1aa54bf6d110d11aa673dc9d2dbbedbdddc2676621085ba90bea21` |
| `foundation-administrator-SqlServer-browsersessions-lifecycle` | `12613463098` | `2bdf60b7f03c06cd16063d4dee39e3ea7218b9116acddaea416aed1bf54f4825` |
| `foundation-administrator-SqlServer-browsersessions-product` | `12614036146` | `39579bd21bb8cc4abe70fa91c53c7a2659466e4e27b58dd392bbe16e414d9aea` |
| `foundation-contracts-chromium` | `12613624669` | `4dc2d8915963b7212d07d18563932826eeff8303ff4ad83afaf6a4644c7e2fb0` |
| `foundation-contracts-core` | `12613243514` | `0c829fa121c5c51469b7a26d093978e0e6ffc889e810ebde107a219fc542d482` |
| `foundation-contracts-webkit` | `12613963095` | `4dbf26d2379381c3b55a4ca68e562c991d44d4646492112f3711ba459a7ed19d` |
| `foundation-coverage` | `12613398987` | `e47b86769a305c272e781a91e584f777a0b2e9dd2d5d5d73c8ca2854549a0037` |
| `foundation-unit` | `12612899695` | `11a1b1f585e78f7843c5c6dc8491fc5e26cfeee1f6790e5461e2450c7f95bb3c` |
| `governance-mutations-PostgreSql` | `12612918614` | `302a1414d3aa6cc5c9b19c122904688905c64eb63e9ef2491cc088ec9760b0c9` |
| `governance-mutations-SqlServer` | `12613529667` | `38dac176b0a30a42804f4c4eb94e2f5f461a7ae9c7326dd0ca6f155db66985a3` |

### Rejected evidence and remaining limits

The first run `40230383276` concluded cancelled with 42 successful, four failed and 27 cancelled jobs. Both Lifecycle reports retain eight passed / sixteen failed. The second run `40231202195` concluded cancelled with 65 successful, three failed and five cancelled jobs; both Lifecycle reports retain sixteen passed / eight failed. Neither run supplies acceptance or aggregate coverage evidence. The mode-selection and shared native cancellation-token corrections above have preserved positive and negative controls in the accepted source. The earlier local Aspire startup failure remains distinct and was not repeated.

The genuine native HTTP provider fixture is in-process TestServer, not socket/TLS/browser-engine evidence. Competing submissions prove duplicate-submission outcome, not a witnessed database lock order. Registration observation does not serialize concurrent external profile disablement. Cancellation substitution is end-to-end integrity evidence, not isolated mutation proof of the adapter comparison. These limits remain unchanged.

T003/T004 remaining integration, T012/T016/T020 browser/TLS evidence, T019–T024, A-009/T028 restoration, PR #69/issue #70, all six historical findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open. No refresh, global logout, grant revocation or production composition is enabled, and no expired diagnostic approval is revived.

### Next bounded implementation: T019

Reuse the delivered durable local logout and `BrowserLogoutContinuation`/`RemoteAsync` state machine, existing logout-cookie purpose and fixed return destination; no new provider migration is needed. Add the protected explicit local-only/issuer-assisted choice, purpose-separated browser proof and exact fixed issuer link. Only after known local commit and scope disposal may a separate owned operation advance the reserved remote continuation. Remote quota exhaustion or failure must preserve confirmed local success. T017 acknowledgement reconciliation remains local-only and must not reconstruct a remote reservation.

The return boundary must require exact HTTPS GET framing, one canonical state, authentic protected logout cookie and current persisted browser/generation/original-time binding. A one-use return records only unverified issuer status; it must never authenticate, renew or infer remote commit from query/redirect. Add both-provider positive journeys and forged/cross-browser/stale/replayed/concurrent returns, quota, audit/commit/cleanup failure and lost-navigation controls. Keep composition dormant and leave actual browser/TLS acceptance to the remaining tasks. This is a reviewed next-step design, not completed T019 implementation.
