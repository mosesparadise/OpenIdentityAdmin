# Local demo candidate verification

Starting main was remotely verified as `eb739386f6117047edb70bce62cd7e73ba311d94`; local branch `108-local-b3-demo` preserves that accepted B3 delivery. No prior implementation or diagnostic approval was repeated.

## Executed local evidence

- .NET SDK 10.0.401/runtime 10.0.12; the initial host/graph implementation compiled with zero warnings/errors.
- Focused normal-host/configuration/material checks: **29 passed, zero failures/skips**. Raw report: `/tmp/local-demo-tests/IdentityPlatform.Host.Tests_net10.0_x64.trx` (workspace evidence, not a portable repository link).
- Real Aspire graph callbacks, both provider registrations, seven distinct generated key roles, secret recipients, endpoints and public registration: **2 passed, zero failures/skips**. Report: `/tmp/local-demo-graph-tests/IdentityPlatform.AppHost.Tests_net10.0_x64.trx`.
- PowerShell **7.5.3 on Linux**, verified against its release checksum, parses all three helpers. Four native-process test methods pass, including success/UTF-8/EOF, zero/multiple/invalid ownership, bootstrap/registration failures and timeout. Docker is a controlled fake for these transport tests; no Windows execution is claimed.
- Unit matrix executed: Core 25, Host 1,561, Architecture 5 and AppHost harness 67 passed, zero failures/skips (1,658 total). The final solution build succeeds with zero warnings/errors; actual hosted container execution remains pending.
- Code-quality seeded controls and no-new-finding check passed, including the runtime-browser test addition. Eight stale allowances removed for changed declarations; none expanded.
- AppHost selector controls pass with nine Foundation plus ten BffSessions executions required per provider. Existing cases remain mandatory.

One early focused test expected a successful login page against an intentionally unreachable database; the real login correctly returns 503 because its browser coordination requires storage. The test now independently expects that denial while requiring /alive, inert 404s and anonymous status behavior. Earlier failed build/style iterations were corrected; they are not acceptance evidence.

## Added required hosted evidence — pending

The normal container graph test starts existing schema/role provisioning, supplies genuine bootstrap through Docker stdin, completes real Chromium password/TOTP enrollment, performs fresh B3 ceremonies, tests local logout/replay denial and confirmed issuer logout with independent old issuer-cookie replay denial, disposes the graph, then proves cookies independently observed live immediately before teardown fail in a new run. It runs once per PostgreSQL/SQL Server Foundation cell with the existing overall execution deadline and all existing tests retained. No result is claimed yet.

Required full CI, source-linked native discovery/inventory, retained browser tests, coverage floors/mutations, actual Docker endpoint bindings, certificate selection/trust and volume cleanup remain pending. Windows Docker Desktop end-to-end remains unexecuted. No task or release gate is closed by an unexecuted test.

## Independent review dispositions

- Architecture: selected a generic BFF host with owned B3 routes and no global authentication dispatch; inspected source has no remaining concrete finding in that scope.
- Security: found cross-loader encryption/wrapping reuse, partial-cleanup short circuit and interruption cleanup outside finally. Candidate fixes and permanent tests address these; independent security rereview passed the three corrections.
- Aspire/Windows: found CRLF shell packaging and insufficient PowerShell minimum. Targeted `.gitattributes` LF enforcement and PowerShell 7.4 minimum address these. Actual served certificate, loopback bindings and anonymous-volume cleanup require runtime evidence.
- Test: required actual normal-host tests, private staging faults, graph/role separation, genuine container/browser execution and process-helper controls. Candidate tests are added. Review strengthened live-cookie restart and independent issuer-authority oracles and joined initializer process/read lifetimes; hosted evidence review is pending.

All historical/human/production/public-release gates stay open. The next roadmap item after this bounded local-demo delivery remains reviewed M02-C server-held refresh planning.

The expanded local build initially exhausted workspace space while copying immutable Playwright binaries. Exact-content duplicate build copies were deduplicated against the pinned package cache; source and prior evidence were preserved. This is an environment/build interruption, not a test pass or SQL diagnostic.
