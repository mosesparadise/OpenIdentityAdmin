# ADM-OPS-01 investigation — 13 October 2026

Status: **open; no causal correction established.** The user authorized investigation after PR #35's normal squash (`8792f85`). A-003 expired at that merge. This record does not renew it, close whole-increment acceptance, or authorize release. Trace: ADMIN-011, FR-21–23, NFR-11.

## Historical failure and confirmed boundaries

The retained [incident record](operations-evidence.md#adm-ops-01-unresolved-required-profile-incident) identifies the original SQL Server Development failure, artifact and digest. Its immutable test result failed after 41.958 seconds in the explicit Worker health wait, after `StartAsync` returned. The test checks AuthServer, Admin API, Admin BFF and Worker in that order. Reaching Worker means the first three already passed actual HTTPS readiness and route assertions. The intentional database stop occurs later.

Worker starts after successful Migrator completion and uses the same platform runtime credentials/schema as AuthServer and Admin API. AppHost test assembly parallelism is disabled and each case owns its resources. These facts do not prove that a later process, connection or runtime failure was impossible; they rule out describing migration omission, the test's database stop, or established same-assembly overlap as the observed cause.

Pinned Aspire 13.6.1 (`3751e615ad660621f2a7aa4c390ef48e08a0eed8`) narrows the failure mechanism:

- `ResourceNotificationService.ShouldYieldHealthyWait` returns an unhealthy resource for `Finished`, `Exited`, `FailedToStart` or `RuntimeUnhealthy` when using `StopOnResourceUnavailable`.
- An HTTP readiness failure while the resource remains `Running` does not alone enter that branch. `RuntimeUnhealthy` describes runtime unavailability, not an ordinary application `/health` 503.
- `ResourceHealthCheckService` starts health monitoring upon `Running`, cancels it for terminal states and updates health reports without converting ordinary HTTP failures into terminal process states.

The launch request follows Migrator completion by approximately 14 milliseconds. The cancelled Worker health probe lasted approximately 2.898 seconds. Log output was buffered: outer log timestamps cannot establish the precise ordering of all underlying events. Cancellation could follow a terminal transition or test disposal. It does not demonstrate that a probe timeout caused the incident.

The original artifact has no decisive state, exit code or process generation. Other child hosts also lack application stdout beyond their launch messages, so absent Worker stdout does not prove it never ran. Earlier SQL pre-login probe failures precede successful database/Migrator readiness and do not establish the later Worker cause. OOM, port collision, certificate failure, profiler failure and an infrastructure transient remain unsupported hypotheses.

## Discriminating local controls

Three fixed disposable PostgreSQL controls exercise the actual Worker and Aspire health wait, using the existing AppHost project temporarily. They preserve TLS and three-minute case deadlines, use no retry, and assert graph cleanup only after the experiment completes successfully. Their explicit two-second cancelled wait is an experimental discriminator, not a change to acceptance deadlines. SQL Server is not available in the local managed Docker cache; previous registry-policy constraints remain. PostgreSQL results cannot substitute for the original SQL profile.

| Control | Required distinction |
| --- | --- |
| Worker-only invalid provider | Worker exits with code 2, becomes terminal and the readiness wait fails closed |
| Stop database after Worker readiness | Worker remains `Running`; `/alive` is 200, `/health` is 503 and a fresh readiness wait ends through explicit cancellation |
| Stop Worker after readiness | Worker becomes unavailable and a fresh readiness wait fails closed |

The first execution passed invalid configuration (33.445 seconds) and database loss (68.547 seconds), but failed Worker stop (187.372 seconds including teardown): the probe incorrectly expected `Exited`, while the project reached `Finished` with exit `-1`. This was an experimental assumption error, not a product defect or reproduction. Disposal ran while unwinding the failed case, but its later `AssertRemovedAsync` was not reached; that attempt does not independently prove cleanup. The other two cases reached and passed cleanup assertions.

The correction awaits `Finished` for a project and `Exited` for a container, and asserts the pinned generic exception message as well as its type. The corrected execution passed all three cases, with zero skips, in 154.898 seconds overall:

| Control | Case duration | Observed result |
| --- | ---: | --- |
| Invalid configuration | 34.068 s | `Finished`, exit `2`; exact generic unavailable exception |
| Database loss | 73.318 s | `Running`, no exit code; `/alive` 200, `/health` 503; explicit two-second wait cancellation while the case token remained active |
| Worker stop | 46.231 s | `Finished`, exit `-1`; exact generic unavailable exception |

All three corrected cases reached and passed post-disposal resource-removal assertions. No case deadline, production behavior or accepted test was changed. These controls validate diagnostic distinctions; none proves which condition caused the historical failure. PostgreSQL is the only exercised provider. No local Firefox experiment was run because browser binaries are unavailable under the managed environment's existing network policy.

### Reproduction and retained execution identities

Environment: Linux, .NET SDK 10.0.401/runtime 10.0.12, Aspire 13.6.1, Docker 28.4.0, cached `postgres:18`; source baseline `8792f8599f0e66766250c22870b5f717cedffde3`. The [experimental source](worker-experiment.md) contains the corrected executable statements. Its one subsequent XML-summary correction is disclosed there. Commands were run from the repository root after loading the installed SDK environment:

```sh
dotnet build tests/aspire/IdentityPlatform.AppHost.Tests --no-restore -m:1 -nr:false
dotnet test --project tests/aspire/IdentityPlatform.AppHost.Tests --no-build --no-restore --no-progress --filter 'FullyQualifiedName~IdentityPlatform.AppHost.Tests.Features.WorkerIncidentProbe.' --report-trx --report-trx-filename worker-controls.trx --results-directory /workspace/work/adm-ops-01-controls-corrected
```

The first execution used the same command with results directory `/workspace/work/adm-ops-01-controls`. The initial build rejected missing braces/formatting; those were corrected before that execution. Build/experiment logs and TRX remain local diagnostic artifacts, not uploaded acceptance inputs or secrets-cleared public logs. SHA-256 identifiers:

| Artifact | SHA-256 |
| --- | --- |
| Initial executed source | `e41686f19f657babdb3851e0384304411a017bc8b8c38fc0bef8f3ac78f1d5b2` |
| Corrected executed source | `1aec0e2186f9616948fe75a18c6a3ba987be4fc7e325839a0a149dc5fa57d30d` |
| Appendix C# with corrected XML summary | `38a902fd343e84505317e0661bd255f38e3d302be4825598ace9f99d3dda556e` |
| Initial TRX, two passes/one failure | `faa2592022547ba80060f31a2f794e8cde02aa3553350a7625e589eb76580566` |
| Corrected TRX, three passes | `451e6be31c79124ed120794b20a0cd091762c22e5157b7217084393d5560b42d` |

The temporary test source was removed afterward. The restored AppHost project built with zero warnings/errors. Native MTP discovery (`dotnet run --project tests/aspire/IdentityPlatform.AppHost.Tests --no-build --no-restore -- --pre-enumerate-theories on --list-tests json`) contains exactly the original 51 cases: 39 Harness, six PostgreSQL and six SQL Server; no `WorkerIncidentProbe` remains. The experiment contributes no ordinary acceptance cases.

## Separate post-merge finding: ADM-EVID-01

[Main run 38355735749](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38355735749), at `8792f85`, failed `Browser administrator Accounts / SqlServer / firefox`. The required eight-case job passed seven cases and failed the synthetic history-readiness control. Overall, 59 jobs passed, that job and final acceptance failed, and combined coverage was skipped. This run is **not clean acceptance**. All startup/provider foundation checks passed, so it does not reproduce ADM-OPS-01. It does not invalidate the separately recorded pre-merge exact-head run, but current source reliability remains unresolved.

Artifact `11793083103`, `foundation-administrator-SqlServer-firefox-accounts`, SHA-256 `5a43813dd6927ef6f3846ee2c366d7107b12d546b1f7c6c605a035b17e373648`, contains only Administrator TRX and that job's coverage XML. The failed result has ErrorInfo only, without DOM/readiness, route-completion or trace evidence.

The failed identity is `HistoryObservationTests.Given_DisabledPageScripts_When_HistoryDocumentBecomesReady_Then_SecretsFailImmediatelyAndOnlySafeDocumentsPass`. `RequireReadinessAsync` reached its first positive oracle call after releasing a held image and timed out at the unchanged five-second limit. The prior expected 250-millisecond rejection of an incomplete document had passed.

`release.TrySetResult()` permits route continuation; it does not observe successful `route.FulfillAsync` completion. No recorded state distinguishes slow fulfillment, browser load completion, driver scheduling or predicate execution. Pinned Playwright 1.63 locator polling runs from its driver, so disabled page JavaScript stopping animation-frame polling is not a supported explanation.

Administrator tests allow collection parallelism, and this synthetic browser control is outside the database fixture collection. Resource waits began at 07:21:49.714 BST while the history test ran approximately 07:21:52.438–07:22:06.150 BST. This confirms overlapping SQL graph startup, **not resource starvation or causality**. The control itself does not query a database; its provider argument is a matrix label. Browser profiles are uniquely allocated.

## Next diagnostic work and closure criteria

The implementation lead owns both findings, with independent Aspire/architecture and test review. Planned work stays within existing test projects and Features conventions; no new production host or security behavior is proposed.

1. For ADM-OPS-01, subscribe before startup to a bounded, sanitized transition recorder. Retain fixed resource roles, monotonic ordering, allowlisted state/health, numeric process/exit identifiers and fixed test phase. Preserve the first unavailable transition even if a later snapshot replaces it. Indicate truncation or missing evidence explicitly. Never serialize configuration, URLs, arbitrary snapshot values or exception text; observer failures must preserve the original failure/cancellation.
2. Validate that recorder with terminal-process, running-unhealthy and stopped-process controls, including saturation/redaction/failure behavior. Run the unchanged original SQL Development profile with the recorder in controlled CI. Retain every result. A green run is only nonreproduction; it does not close the incident.
3. For ADM-EVID-01, add exception-propagating fulfillment completion and fixed safe phase/counter evidence. Keep one absolute five-second positive-observation budget across the completion handshake and oracle; do not allocate five seconds to each. Preserve the one-second immediate secret denial and both secret-marker controls. A timeout must never become accepted native history refusal.
4. Compare fixed isolated and overlapping browser executions. Seed incomplete delivery and completed secret-bearing documents so diagnostics cannot classify an unobserved page as safe. Do not serialize suites or extend deadlines and call that a causal fix without before/after evidence.
5. Close either finding only through a substantiated causal disposition, correction where needed, a matching permanent regression and independent review. No retry, subsequent green run or speculative explanation substitutes for this evidence. Further feature acceptance and production/public release remain blocked by outstanding requirements.

Independent architecture/Aspire and test reviewers examined the source and original logs. Both returned insufficient evidence for causal closure, identified the distinctions above, and rejected treating hypothetical causes as established defects. No production change is justified by the evidence in this packet.

## T031 diagnostic implementation — 13 October 2026

The user authorized processing T031, T032 and then the T019 design document as three sequential reviewed PRs. This authorizes diagnostic work and design only; it does not close either incident, renew A-003 or accept subsequent runtime feature implementation through them.

T031 keeps all six original provider/profile identities, route assertions and three-minute/ten-second deadlines. `ProfileTransitionObserver` starts consuming the pinned public notification stream before `StartAsync`. `ProfileTransitionLog` retains at most 128 chronological sanitized changes, six latest observations and six independently retained first-unavailable observations. The existing final-snapshot evidence remains. Deduplication considers safe state, PID, exit, health and phase; unknown roles are ignored. Output contains only fixed role/phase/state/health labels and numeric metadata, never resource IDs, arbitrary property formatting, configuration, URLs or exception text.

Sequence, elapsed milliseconds and phase describe **consumer observations**, not original publication times. A terminal Migrator can be normal; the first-unavailable marker is not a causal verdict. The header explicitly reports truncation, observer failure, join completion and `publication-drain=not-confirmed`. Missing role observations stay explicit. Pinned Aspire's upstream channel remains unbounded; only our retained history/output is bounded. No completeness claim is inferred from the absence of an observed transition.

Observer cancellation and joining share a separate one-second diagnostic shutdown budget, without extending application readiness limits. If shutdown times out, output marks the join incomplete and a cleanup task retains ownership of the cancellation source and sole enumerator until they finish. No concurrent iterator disposal occurs. Reader, sanitization, disposal, callback and output faults cannot replace the original test exception/cancellation. Observer failures on otherwise successful graph checks produce an explicit diagnostic-gap record; normal success stays silent. A stalled upstream observer remains a reported limitation, not claimed completed cleanup.

Twenty additional native Harness cases cover all four unavailable states, running-unhealthy distinction, safe-field deduplication, redaction, saturation with two independent roles, missing evidence, subscription ordering, original exception/token preservation, reader/output/disposal/cancellation faults, blocked callbacks, cancellation-ignoring readers, and real notification-service seeded/live/immediate-failure paths. Fault controls release their own gates and observe teardown. No temporary Worker probe was restored. The manifest raises Harness 39 to 59; the six PostgreSQL and six SQL Server profile obligations remain unchanged.

Local execution: restored project build passed; all 59 Harness cases passed with zero skips in 3.799 seconds. Required static/provider/CI results and independent final review are recorded in the task PR before normal squash. ADM-OPS-01 remains open: a clean profile run with this recorder demonstrates nonreproduction, not historical causality or closure.
