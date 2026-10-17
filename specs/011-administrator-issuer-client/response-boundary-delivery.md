# Accepted dormant HTTP response and native client scope prerequisite

17 October 2026 (Europe/London). [PR #64](https://github.com/mosesparadise/OpenIdentityAdmin/pull/64) delivers a bounded feature 011 T005 prerequisite: finite response staging and admission, original-deadline transport abort, actual native response completion, and fresh owned scopes created by the existing OpenIddict client service. Production composition remains dormant. Provider extensibility, existing databases and migration histories remain preserved.

## Exact accepted candidate

[Run 39072758180](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39072758180), attempt 1, passed all **63 jobs** at source `33543844018d3c21c6e3638dbd1e14501a8826f5`. Tested merge `578d611d5061fc56a8ae63a481ae510e472d7a05` has parents main `4aabebf1b6277c6fbf9135e9660fbe9137c5dc6e` and that source; both share tree `fd7d2a87880dd0dba89989e40943e54a8c423157`. Normal squash `4e0e5c44172ea4696a480d74006f3ec512e7dcd8` has that exact tree and sole parent `4aabebf1b6277c6fbf9135e9660fbe9137c5dc6e`; remote main was verified at the squash. Fresh pre-merge reads confirmed unchanged base/head, all 63 successful exact-head checks, a clean merge state, no unresolved threads or blocking review, and no restriction to bypass. Copilot posted a quota-exhaustion comment after ready-for-review; it supplied no review result and was not counted as approval. The final acceptance log explicitly accepted full scope at the tested merge.

## Runtime evidence

All 644 unit cases pass (Host 549, Core 25, Architecture 5, AppHost 65). Independent strict raw-report review preserves all 599 prior identities plus 45 additions. Six real Kestrel cases cover blocked operation, blocked rejection publication, blocked native completion and failed completion. All unique result/definition/entry/execution bindings reconcile, with zero nonpass counters or RunInfo errors. Unit artifact `12115288770` (232,095 bytes) has ZIP SHA-256 `a54b39889e03fc9f2d49f4494e898dcce272d391b8982ca23117845079fc4175`; artifact metadata and checkout logs bind the exact final source and tested merge.

PostgreSQL raw review confirms 22/22 native foundation cases (Persistence 7, Foundation 8, BffSessions 7), all 22 prior identities retained with zero nonpass counters. Artifact `12116500290` (103,209 bytes) has ZIP SHA-256 `b2a8480f1f04b90cb1b26aa9b64895bba4367330ddc7e02c6efc57d9ed507715`. Sessions and Platform ownership cases pass in 7.693 and 2.152 seconds; these durations cover the whole existing fixture, not only its new response phase. SQL Server also passes all 22 cases, preserving all 44 native identities across both providers with exact unique raw-report bindings and no errors/skips. SQL artifact `12116645178` (102,533 bytes) has ZIP SHA-256 `5d4ba41eb26a8a4eae3b205c24ea25c3c9a0570bbd8ac3fea9da1b8220cd61da`. SQL Platform and Sessions ownership cases pass in 9.573 and 18.636 seconds. All 688 reviewed unit/native cases pass. Existing four provider/store ownership cells now prove native manager commit-versus-rollback response admission. These combine real database/native-manager work with separate Host native client-scope controls; they do not establish a complete real-database BFF client backchannel exchange.

All **48 required coverage inputs** are present, and all **15 production modules** pass the unchanged 90% line/80% branch floors. Independent review recomputed every module rate from merged XML SHA-256 `0326faee7c823f48eaa7b69d5aaaaf7f955b67f64cb0461cafb41164eac7d529`. All 17 changed executable production files appear; the remaining two contain only enum/constants declarations, with no new exclusion. Input paths and ZIP digests reconcile against hosted merge/download logs and exact-run artifact metadata; individual XML reports for all 48 inputs were not separately downloaded. Coverage policy, exclusions and workflow are unchanged. Chromium and WebKit jobs pass; Firefox remains explicitly deferred under A-007.

Independent security review reconciles all six existing mutation suites: 42 kills with 42 passing baseline and 42 passing restored controls, zero skips. Exact artifact digests, run/head/tested-merge bindings, manifests, current source and recomputed mutant hashes, provider/method identities and source restoration agree. Native exit log projections show baseline/restored exit 0 and mutant exit 2. The strict verifier remains unchanged (SHA-256 `ce162465e9dc336c7dcd90373cf91654e5d18b7f0e80634eb538ba5ae175736a`). Sanitized mutation receipts are not raw mutation TRX or opaque native UID proof, and existing mutations do not establish new B2 mutation coverage.

| Module | Line coverage | Branch coverage |
| --- | ---: | ---: |
| IdentityPlatform.Admin.Api | 100.00% | 100.00% |
| IdentityPlatform.Admin.Bff | 93.91% | 92.78% |
| IdentityPlatform.AppHost | 99.35% | 100.00% |
| IdentityPlatform.AuthServer | 97.34% | 100.00% |
| IdentityPlatform.Core | 98.91% | 91.67% |
| IdentityPlatform.Database.Abstractions | 99.11% | 100.00% |
| IdentityPlatform.Database.Hosting | 95.45% | 100.00% |
| IdentityPlatform.Database.PostgreSql | 100.00% | 100.00% |
| IdentityPlatform.Database.PostgreSql.Hosting | 100.00% | 100.00% |
| IdentityPlatform.Database.SqlServer | 100.00% | 100.00% |
| IdentityPlatform.Database.SqlServer.Hosting | 100.00% | 100.00% |
| IdentityPlatform.Infrastructure | 91.65% | 95.23% |
| IdentityPlatform.Migrator | 98.80% | 100.00% |
| IdentityPlatform.ServiceDefaults | 100.00% | 100.00% |
| IdentityPlatform.Worker | 100.00% | 100.00% |

| Artifact | ID | ZIP SHA-256 |
| --- | --- | --- |
| administrator-mutations-PostgreSql | 12116157072 | `0d3ffbe6ce59c173990cbbf34b49cc321983f6bcd32a2c594ed552ea087593ad` |
| administrator-mutations-SqlServer | 12116590242 | `41d3b24abd081c15934793aa29d6ce6cf884315a1673a7dbe61f95afce5f2fc8` |
| critical-mutations-PostgreSql | 12115298980 | `990d8ce90a1b29dd38b1dff630dd845a4eb55ced7133bf4be4db078be7e781a6` |
| critical-mutations-SqlServer | 12115931761 | `85a2ced377e7680ae51df53bcd46b249be3d43e72d00d6eb97b0fb4038b67e6f` |
| foundation-coverage | 12115953454 | `c2ddb2818f02a1dd73625498167af91009304f1235fead21b9692e90f5baad05` |
| foundation-PostgreSql | 12116500290 | `b2a8480f1f04b90cb1b26aa9b64895bba4367330ddc7e02c6efc57d9ed507715` |
| foundation-SqlServer | 12116645178 | `5d4ba41eb26a8a4eae3b205c24ea25c3c9a0570bbd8ac3fea9da1b8220cd61da` |
| foundation-unit | 12115288770 | `a54b39889e03fc9f2d49f4494e898dcce272d391b8982ca23117845079fc4175` |
| governance-mutations-PostgreSql | 12115921380 | `3785e94efc5632522a8b61c323c6d3465258b2389176289f8708dc32c8a5aea8` |
| governance-mutations-SqlServer | 12115299218 | `adcafdb010de01c9e61e987abb0e36f483150769ab1cf5a21ee43126ce9b633b` |

## Review and retained corrections

Independent architecture/test and security reviews resolved native error publication outliving the watchdog, cached-cookie escape, header array aliasing, unverified error-label publication, concurrent bounded mutations, empty-header-array growth, swallowed cancellation, callback admission races, unsupported features, native child admission/cleanup and actual terminal response completion. The exact test and integration limits are in [the implementation record](response-boundary-implementation.md).

Initial run `39071115865` at `f7515f12472615b28bc2b007d657c446ebd577dd` failed the 9,999 ms boundary fixture's mixed real-timer/synthetic-clock behavior and an informational unused-import diagnostic. The fixture now uses one coherent controlled timeline for that exact boundary theory and deliberately delays wall time while its clock remains unchanged. Production deadlines and expected boundary results were preserved. Run `39071671333` at `b731de91026d2167c86d7984e30d93ab0a068ed5` passed 640 units and static analysis but was superseded before acceptance by the native response-completion correction; its final disposition is 60 successful jobs, two cancelled jobs (SQL Server and aggregate coverage), and failed final acceptance after supersession. It is not accepting evidence.

Pinned ASP.NET Core 10.0.12 source confirms Kestrel normally validates content length and sends final framing after the application delegate returns. The boundary now awaits the captured native body's CompleteAsync under the original watchdog on success and fixed rejection. A forwarding native feature is required for this completion guarantee; arbitrary middleware may not forward terminal completion. This is local completion, not proof of remote receipt or atomic delivery. Owned child OnCompleted callbacks remain unsupported. The intermediate 547/549 local run reproduced test-only access to a returned/disposed HttpContext; the final test captures references while blocked and reads only independent counters after return.

Final local instrumented Host evidence passes 549/549 with zero skips, including 119 protocol-storage/transport controls and all six Kestrel cases. Raw report SHA-256 `17e4a55252c15cc7a791929f6fc4d67cf32baf1f07e5b927c59629361fbd4133`. Independent review reconciles all prior 545 local identities plus four completion cases. Locked builds, code quality, informational unused-import checks and all 199 Python controls pass without expanded allowances or weakened production deadlines. No local Docker/native SQL experiment occurred.

## Continuation

T005/B2 remain incomplete. Next prove early durable native redemption and assertion consumption survive later grant/issuance rollback, with one original request across fresh owned scopes, then quota reservation and actual issuance commit linearization on both providers. Native cryptographic/profile/backchannel interoperability and verified native error-response permits remain required. Do not repeat accepted provider or ownership prerequisites or treat these dormant components as administrator login activation.

All six historical findings, 48 human rows and activation/public-release gates remain open. A-007 defers Firefox only through internal B2 and requires restoration before B3 implementation/activation/release. No production activation, schema migration, expired diagnostic renewal or external release occurred. The delivery-record PR uses proportional documentation checks and normal squash; it does not replace executable acceptance.
