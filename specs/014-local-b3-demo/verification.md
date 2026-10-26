# Local demo candidate verification

Starting main was remotely verified as `eb739386f6117047edb70bce62cd7e73ba311d94`; local branch `108-local-b3-demo` preserves that accepted B3 delivery. No prior implementation or diagnostic approval was repeated.

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
