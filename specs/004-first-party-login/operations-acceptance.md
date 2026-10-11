# T026 — Operations and failure acceptance

Current delivery: T026 is squash-merged in [PR #9](https://github.com/mosesparadise/OpenIdentityAdmin/pull/9) and reverified at the PR #12 checkpoint. The plan/checkpoint statements below are historical; use the [current acceptance matrix](acceptance-matrix.md) for remaining obligations.

Authorized by the user's request to complete T026–T028 sequentially, each through a separate reviewed/fixed/squash-merged PR. PR #8 merged as `64d8ceb` after all seven checks on `3241d15`; this branch completes only T026. Existing v0.2, LOGIN-008/009, SC-005 and the feature's contracts govern the work. T027–T030 remain separate acceptance obligations.

## Design and evidence boundaries

| Work | Boundary and oracle |
| --- | --- |
| Actual diagnostic reload | Extend the existing Host.Tests logging case with IConfigurationRoot.Reload, category/provider-specific overrides, an observed options change and reused ILogger instances. Sensitive events remain suppressed and a safe positive event is captured before/after reload. |
| Exact rate and concurrency admission | Fresh TestServer instances use the actual AuthServer StartupContext/composition/feature pipeline. Test-owned `/account` probe handlers exercise the unchanged 600/minute and 16 concurrent limits, immediate 429, operational bypass, release/re-admission and fully drained teardown. No production test endpoint or lowered policy. |
| Cooperative timeout/client cancellation | The same real middleware component host observes RequestAborted at the downstream handler, completes its cleanup and verifies the ten-second policy or caller cancellation. These are middleware tests, not TLS/database evidence. |
| Consumption audit failure | Both providers: genuine Pending→Consumed using an existing eligible session, and Completed→Consumed using its completed cookie. Deny only runtime audit INSERT. Require fixed503, no code/cookie, unchanged application interaction/session/audit snapshots, independent permission restoration and fresh-flow recovery. Inspect native request-token state separately. |
| Delayed native command / HTTP abort | Both providers: obtain a genuine code, hold an independently owned conflicting database lock, observe the actual issuer runtime command blocked by that transaction, then await command timeout or cancel the HTTP caller. Drain blocked database work before release/state inspection. Record exact native code/issuance state; no assumed rollback or exchange retry. |
| Native consumption followed by failure | After a genuine code, deny runtime INSERT on native tokens while preserving UPDATE. Verify native redemption can survive a later issuance failure, no token response/new access or ID entry, independent restoration and fresh-flow recovery. |
| Actual HTTP log canaries | Capture the AuthServer resource's real process output in memory, with aggressive category/provider logging overrides. Exercise genuine form/exchange success/denial and a storage fault. The response's safe incident event is a positive capture barrier. Assert secret/cookie/state/code/verifier/token absence with fixed diagnostics. Never persist raw logs. The boundary is the preceding successful/rejected form/exchange and correlated fault event with the configured default Simple console formatter (scopes precede its message), not arbitrary future events or third-party providers. |

Reuse executed bootstrap creation/no-op audit rollback (T023), password/session/replacement rollback and stopped-database readiness/liveness (existing OperationsTests), and response-loss/replay semantics (T025). Component interceptor cancellation in T024 is not represented as child-process evidence. Native state remains owned by OpenIddict; application audit atomicity is not extended to native redemption. T030's concurrent browser-interaction capacity gap is outside this task.

## Implementation structure and cleanup

Preserve Features and the confirmed Composition/Setup pattern. Host component helpers remain under Host.Tests `Features/Pilot`; provider acceptance stays under Issuer.Tests `Features/Protocol`, with cohesive reusable fault/log helpers under its existing Fixtures directory. No new projects, packages, schema or runtime configuration switches are planned.

Use provider-native admin transactions solely for test fault injection. Parameterize identifiers/values; do not recreate redemption logic. Runtime requests retain actual least-privilege connections and five-second command bounds. Cleanup uses independent bounded cancellation; a restoration failure invalidates the shared environment. Every started request is awaited/drained, including failed assertions, before releasing fixture ownership. Captured logs, credentials, token snapshots and exception objects must never appear in failure output.

## Execution budget

The existing issuer runner deadline remains660s. Feature005 SQL issuer observations range8m33s–10m32s; the slower observation leaves about28s. Add no new Aspire environment for ordinary fault cases. Reuse the existing serialized issuer fixture and put database-independent policy tests in Host.Tests. Measure the revised full profiles; do not hide failures with retries, omissions or deadline increases. Process runtime is distinct from the ten-second application cancellation budget, five-second database bounds and SC-005's specific stopped-database <11s assertion.

## Execution checklist

- [x] OP-01 Inspect approved scope, existing evidence and fixture budget; independent architecture/test/security design review before code.
- [x] OP-02 Add actual configuration reload and exact production middleware boundary regressions.
- [x] OP-03 Add both-provider consumption-audit and native post-redemption failure tests with safe log capture.
- [x] OP-04 Add observed real database contention/HTTP cancellation tests and bounded drain/recovery evidence.
- [x] OP-05 Update strict test counts; run local static/unit/targeted provider tests and all required CI gates with zero skips.
- [x] OP-06 Review source/evidence independently; resolve GitHub findings; record bounded convergence and actual results.
- [x] OP-07 Publish a Conventional Commit PR, verify its final head/checks/reviews and squash-merge before T027.

Read-only planning analysis: T026's obligations map to OP-02–04 and retained named regressions; all implementation/evidence gates map to OP-05–07. No accepted security contract or test deadline is relaxed. The requirements-quality checklist remains7/7 checked. Original feature tasks remain partial until their complete acceptance is demonstrated; T026 is checked only after its own evidence passes.

Completion evidence: [verification](verification.md#t026-completed-ci-checkpoint),617 cases/zero skips/all seven gates on `d6735cf`. Source reviewers closed every bounded finding. The documentation correction receives final-head CI; OP-07 remains the final authorized merge step. Observed SQL issuer duration10m54.846s leaves about5s; T027 must budget its browser work without dropping cases or extending deadlines.

Final merge: PR #9 squash-merged as `7d1a9bc61a57d4f8f03deb4b789307bb02f7fa18` after all seven checks on `2839a26` in [run38094527063](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38094527063),617 tests and zero skips. This supersedes the pending merge checkpoint above.
