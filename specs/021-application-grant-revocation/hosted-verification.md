# Grant-revocation hosted verification

## First implementation candidate — 5 November 2026

[Run527](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42090760689)
tests published feature head `a1b809cb4e0c075dd1475d6a048560fc7481f8d4` through PR merge
ref `1591301d4f753eac22dcb5c9385a1aa3343ef029`. Both ordinary grant lanes and the full
PostgreSQL persistence lane report success. Final job enumeration is109successful,14failed and one skipped. The run is rejected;
aggregate coverage was skipped. Three retained protection cells pass; PostgreSQL Chromium
passes31of32 with an existing login-focus assertion failure. Complete artifact reconciliation
and exact-head acceptance remain pending.

Confirmed failures and bounded corrections:

| Boundary | Observed failure | Treatment |
| --- | --- | --- |
| Static analysis | Import ordering and unused imports in changed files | Apply only the named import corrections; declarations and method bodies remain unchanged. |
| Native-image workflow control | Application mutation job is45minutes; its test still expected30 | Match the existing45-minute workflow budget. Product/request/startup deadlines remain unchanged. Seven controls pass locally. |
| SQL application mutations | Third mutant, `application-write-permission`, failed compilation after two earlier behavioral kills | Authority refactoring moved permission observation into a method without a command parameter. The obsolete mutant references that instance field from a static method. Replace its condition with `permission == CredentialPermission`, retaining the former credential-only enforcement bypass. |
| Package restore | Several jobs fail locked restore with NuGet `Connection reset by peer` | Preserve failed attempts. No dependency, lockfile, TLS or product retry changes; rerun through the corrected candidate. |
| Both ordinary Aspire demos | Browser-category failure at `LocalDemoBrowser.cs:73`, the new grant journey | The separate local-demo container graph omitted Worker entirely. Add its locked publish/pinned non-root runtime stage, Platform-only connection and migrator completion dependency. Require its startup and native cleanup. Retain safe source-location diagnostics for further failures. Actual corrected demo completion remains required. |
| PostgreSQL Chromium protection | Existing login-presentation dark case fails `BrowserLoginPresentation.ErrorAsync:52`, an immediate focus read after summary visibility | Use native bounded `ToBeFocusedAsync`; retain visible focus outline, error text, ARIA and cleared-input checks. No forced focus, sleep or retry of the journey. |

The mutation build failure is reproduced locally as CS0120. The corrected mutant and
restored API each compile successfully with `dotnet build ... --no-restore -m:1`.
This is compilation evidence only: both-provider behavioral kills remain required.
Independent test review confirms the replacement's equivalence and the unchanged
witness's credential-denial control and exact unauthorized-write failure marker.
All60 mutation-tool controls pass. Changed-file import verification and code quality
pass without allowance growth. An initial quality command omitted the SDK environment
and could not find dotnet; the corrected command passes.

The existing graph test now requires the actual Worker resource, exact Platform runtime
reference, no session/provisioning access or issuer keys, no published endpoint and one
migrator-completion dependency. Both provider graph cases fail before the correction
(missing Worker) and pass afterward. All110AppHost Harness cases pass on the corrected
source; AppHost and Administrator projects build with zero warnings/errors. Architecture review confirms the host needs no
issuer material. The runtime fixture waits for Running and includes Worker in owned
container/volume cleanup; only actual demo completion proves processing.

A temporary rendering-only diagnostic compiles the actual GrantPanel and exercises
system Chromium151 with synthetic HTTP responses. Open, missing confirmation, cancel,
action focus, reopen, keyboard submission and completed rendering pass with one POST.
The first diagnostic omitted required response security headers and was rejected; the
corrected fixture supplies those headers. This does not establish native authority,
Worker execution, retained Playwright-browser compatibility or Aspire acceptance.
No synthetic transport is introduced into the required real browser journeys.

Local broad container reruns remain blocked by shared disk exhaustion recorded in
[verification](verification.md). Source and retained evidence are preserved. PR124
remains draft/unmerged; remote main was reverified as
`dce682f9812d228b41ae7fad1dcb1f6636f40d38`. Remaining feature requirements, Firefox/A-009,
historical findings and human/production/public-release gates are unchanged.
