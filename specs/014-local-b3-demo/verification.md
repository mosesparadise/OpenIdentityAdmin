# Local demo verification

Starting main was remotely verified as `eb739386f6117047edb70bce62cd7e73ba311d94`; local branch `108-local-b3-demo` preserves that accepted B3 delivery. No prior implementation or diagnostic approval was repeated.

## Accepted implementation — 26 October 2026

[PR #105](https://github.com/mosesparadise/OpenIdentityAdmin/pull/105) is squash-merged as `6e711d5dea45d66742213fd3e9d35e43912b459c`. Source `8b18ed04496794a4f3b41a49009534f73c108e74`, tested merge `0e0c0bdd0681ee2cb2929bd27b2fe94f04a7392e` and squash have identical tree `88197286cda778d6c406b3ea32c34fa125c6a07b`; base was `eb739386f6117047edb70bce62cd7e73ba311d94`.

[Run 40390054252](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40390054252), attempt 1, passes **all 93 jobs**. Main review reconciles finite native summaries from 74 ordinary jobs: **77 report groups, 4,606 successes, zero failures/skips**. Every initial job checkout matches the tested merge; the two foundation jobs additionally build the deliberately frozen `38eb41e` provider-compatibility producer in an isolated worktree. These are execution counts, not a claim of 4,606 distinct test identities. Strict CI native discovery/selection/report verifiers remain mandatory. AppHost discovery contains 105 cases (67 Harness plus 19 per provider); Foundation8, BffSessions10 and LocalDemo1 are disjoint and mandatory. All accepted pre-feature cases remain required; only the two new demo identities moved namespace during candidate partitioning.

| Required demo | Job | Result |
| --- | --- | --- |
| PostgreSQL | [121808466300](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40390054252/job/121808466300) | 1 pass, zero skips; 2m17.484s |
| SQL Server | [121808466068](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40390054252/job/121808466068) | 1 pass, zero skips; 3m11.007s |

These cases use the real normal-host Linux container graph, native stdin bootstrap, genuine Chromium enrollment and fresh password/MFA ceremonies, local and confirmed issuer logout, isolated stale-cookie replay, actual loopback endpoint inspection, owned database inclusion and native removal of captured containers/anonymous volumes. A cookie observed live immediately before teardown is rejected after a new run. Scenario, invocation and job deadlines remain unchanged.

Independent evidence review confirms all **74 mandatory coverage inputs and 15 unchanged module floors**, plus **64 baseline passes, mutation kills and restored passes**. Coverage job `121818282525` and final acceptance `121818539111` check out the same tested merge. Architecture, security, test and Aspire reviews resolved the source findings recorded below. The ordinary summary reconciliation is the main review; the separate evidence reviewer reconciled mutation logs, coverage and terminal gates.

The final API inventory contains 87 retained artifacts, including the 85 required report artifacts (74 inputs, merged coverage and ten mutation reports). Selected API metadata:

| Artifact | ID | GitHub-reported SHA256 |
| --- | --- | --- |
| PostgreSQL demo | 12683046354 | `3a701100fb4f2f4f403b4e1ddca2acf4907510af099f67f4007e0c2ca79ff55f` |
| SQL Server demo | 12684130118 | `71f919f55327329a658516af59906a3a96837cffd5d29f641c4a202248c14c91` |
| Merged coverage | 12685525856 | `7953d86cd0375710fa7ecff4c08d24298eba2aa16ba07914a4a904b597878bd6` |

Evidence uses exact-source CI verifiers, finite successful logs and retained artifact API metadata. The executor's enforced network policy rejects File Service artifact transfer with CONNECT 403. **Raw ZIP bytes were not independently downloaded/hashed or XML reparsed.** API digests above are not local byte-verification claims. Workspace receipts under `/tmp/pr105-delivery-evidence/` are supplementary and not portable repository artifacts: `job-log-review.json` SHA256 `c0dca767f09eef1885e16deeed3d761ec651dd6cfa18804a59f5cd7d74def19d`; independent `final-receipt.json` SHA256 `85c1f5f271c482e154a3162d80bfec1e6da354ffbe6d5802946d9b47eff5b40f`; `mutation-review.json` SHA256 `fdfea8da5552a490ee58ed1203c3201a02a181cb11c5b0f3ee67d8f89a466c38`.

**Windows acceptance remains open:** Linux CI and PowerShell 7.5.3 on Linux do not establish SC-001 on Windows Docker Desktop. [T014](tasks.md) retains the complete documented both-provider Windows walkthrough, including TLS, interruption/disposal and fresh-run behavior. T001–T013 implementation/Linux verification are delivered; full platform acceptance is not claimed. No production, public-release, human, six historical findings or A-009 restoration gate closes. Next roadmap work is reviewed M02-C server-held refresh planning.

## Historical candidate evidence

The dated candidate statements below preserve their then-current scope and failures. Their pending hosted/draft wording is superseded by the exact-source delivery above; the Windows limitation is still current.

## Executed local evidence

- .NET SDK 10.0.401/runtime 10.0.12; the initial host/graph implementation compiled with zero warnings/errors.
- Focused normal-host/configuration/material checks: **29 passed, zero failures/skips**. Raw report: `/tmp/local-demo-tests/IdentityPlatform.Host.Tests_net10.0_x64.trx` (workspace evidence, not a portable repository link).
- Real Aspire graph callbacks, both provider registrations, seven distinct generated key roles, secret recipients, endpoints and public registration: **2 passed, zero failures/skips**. Report: `/tmp/local-demo-graph-tests/IdentityPlatform.AppHost.Tests_net10.0_x64.trx`.
- PowerShell **7.5.3 on Linux**, verified against its release checksum, parses all three helpers. Four native-process test methods pass, including success/UTF-8/EOF, zero/multiple/invalid ownership, bootstrap/registration failures and timeout. Docker is a controlled fake for these transport tests; no Windows execution is claimed.
- Unit matrix executed: Core 25, Host 1,561, Architecture 5 and AppHost harness 67 passed, zero failures/skips (1,658 total). The final solution build succeeds with zero warnings/errors; actual hosted container execution remains pending.
- Code-quality seeded controls and no-new-finding check passed, including the runtime-browser test addition. Eight stale allowances removed for changed declarations; none expanded.
- AppHost selector controls pass with eight Foundation, ten BffSessions and one LocalDemo executions required per provider. Existing cases remain mandatory.

One early focused test expected a successful login page against an intentionally unreachable database; the real login correctly returns 503 because its browser coordination requires storage. The test now independently expects that denial while requiring /alive, inert 404s and anonymous status behavior. Earlier failed build/style iterations were corrected; they are not acceptance evidence.

## Added required hosted evidence — pending

The normal container graph test starts existing schema/role provisioning, supplies genuine bootstrap through Docker stdin, completes real Chromium password/TOTP enrollment, performs fresh B3 ceremonies, tests local logout/replay denial and confirmed issuer logout with independent old issuer-cookie replay denial, disposes the graph, then proves cookies independently observed live immediately before teardown fail in a new run. It runs once per PostgreSQL/SQL Server LocalDemo cell with the existing 660-second invocation and 20-minute job deadlines; the scenario retains its nine-minute limit and all existing tests remain required. No result is claimed yet.

Required full CI, source-linked native discovery/inventory, retained browser tests, coverage floors/mutations, actual Docker endpoint bindings, certificate selection/trust and volume cleanup remain pending. Windows Docker Desktop end-to-end remains unexecuted. No task or release gate is closed by an unexecuted test.

## Hosted candidate iterations

[PR105](https://github.com/mosesparadise/OpenIdentityAdmin/pull/105) remains draft. Initial candidate control failures exposed three stale selector/provisioning expectations; those controls were updated for the additional provider scenario without removing existing cases. Run `40383435486` at `3a972ad5f35eb0537a2eb26b805f642c392b9f4e` passed the hosted unit cell. Each Foundation cell passed its eight existing cases and failed the new demo during the combined second-ceremony/issuer-logout phase (PostgreSQL 2m03s, SQL Server 2m22s for that scenario). Prior phases exercised actual startup, bindings, bootstrap, enrollment, first login and local logout/replay, but this is failed-run evidence, not scenario acceptance.

Independent source review found that replay restored stale cookies into the main browser before the next sign-in, replacing the valid post-logout generation. The corrected test uses an isolated replay context, preserving the product's stale-generation denial. Finite phases now separate second login, positive issuer-cookie control, confirmed logout/return, issuer replay denial and disposal. Runtime verification remains required on the corrected head.

The same run found import-order/unused-import failures and the expected strict viewer configuration-hash gate after 43 contract cases and native entrypoint exports passed. Imports were corrected. The [bounded viewer configuration review](../../eng/contracts/vendor/configuration-review-2026-10-26.json) verifies the identical extracted options registration and unmapped viewers; existing advisory payloads, findings and activation gates remain. A subsequent current advisory query matched the 12 reviewed entries, with `activationBlocked=true` and `inventoryComplete=false`.

## Independent review dispositions

- Architecture: selected a generic BFF host with owned B3 routes and no global authentication dispatch; inspected source has no remaining concrete finding in that scope.
- Security: found cross-loader encryption/wrapping reuse, partial-cleanup short circuit and interruption cleanup outside finally. Candidate fixes and permanent tests address these; independent security rereview passed the three corrections.
- Aspire/Windows: found CRLF shell packaging and insufficient PowerShell minimum. Targeted `.gitattributes` LF enforcement and PowerShell 7.4 minimum address these. Actual served certificate, loopback bindings and anonymous-volume cleanup require runtime evidence.
- Test: required actual normal-host tests, private staging faults, graph/role separation, genuine container/browser execution and process-helper controls. Candidate tests are added. Review strengthened live-cookie restart and independent issuer-authority oracles and joined initializer process/read lifetimes; hosted evidence review is pending.

All historical/human/production/public-release gates stay open. The next roadmap item after this bounded local-demo delivery remains reviewed M02-C server-held refresh planning.

The expanded local build initially exhausted workspace space while copying immutable Playwright binaries. Exact-content duplicate build copies were deduplicated against the pinned package cache; source and prior evidence were preserved. This is an environment/build interruption, not a test pass or SQL diagnostic.

## Required demo partition after observed aggregate deadline

Run `40386187387` at `088c01cd5b90602adef7c5a52c5770dbb0bc32a1` passed all nine PostgreSQL Foundation cases in 7m12s, including the corrected actual demo. The SQL Server combined group exceeded the unchanged 660-second native invocation deadline and emitted no complete passing report; it is not accepted evidence. The demo now has one mandatory `LocalDemo` job per provider, while the original Foundation eight and BffSessions ten remain required. Full native discovery still proves the disjoint 8+10+1 union on every selected invocation. Neither the nine-minute scenario nor 660-second invocation nor 20-minute job budget is widened. Both new coverage artifacts are mandatory: 74 inputs and the unchanged 15 floors, with 77 expected TRX reports and 4,606 ordinary executions. This scheduling change requires fresh exact-head full CI.

The current executor cannot download connector File Service artifact URLs because their host is outside its enforced allowlist (CONNECT 403). GitHub job logs and artifact API metadata remain accessible. Reviews distinguish CI strict parsing of retained raw reports from independent local byte hashing/XML reparse, which has not been performed for these runs. No proxy bypass or inappropriate Sediment download is used.
