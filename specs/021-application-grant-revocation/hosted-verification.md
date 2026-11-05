# Grant-revocation hosted verification

## Inventory gate correction — 5 November 2026

[Run529](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42107559474)
at `fab66f16a49b1937c2fb5cf0da8c80bd31812b7b` finishes25successful,98failed
and one skipped jobs. The UI correction passes its hosted job. Of the failures,96
stop at the lifecycle selection control before native execution, one fails locked NuGet
restore with a connection reset, and final acceptance fails. No new provider result
is inferred from these jobs.

The three related selection-control suites retained pre-expansion counts despite the
updated native inventory. Full local selection discovery reproduced four failures out
of74 controls. Corrected expectations now match the independently reviewed eight additions:
2,573administrator cases;1,162/1,267ordinary administrator cases and1,541/1,646complete
provider cases for PostgreSQL/SQL Server. All2,565prior portable records and their order
are unchanged. No selector, partition, identity assertion or execution gate is removed.

All74selection controls now pass, as do16browser-story,8evidence and3coverage controls
and instruction integrity. Independent test review reproduced the74passes and verified
the exact count changes and preservation. The earlier local check omitted these related
selection suites; future inventory updates must run the complete selection-control family.
The corrected hosted provider execution and remaining feature acceptance are still required.

## Worker correction and expanded acceptance — 5 November 2026

[Run528](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42095693143)
at head `a2f159580ae9ca9752b43912e597ea534484fae9` finishes119successful/four failed/one
skipped jobs. It remains rejected. Downloaded ZIP digests and native reports were
independently reconciled in [run528-evidence.json](run528-evidence.json):

- Both real-provider grant suites:44passed each.
- Complete persistence suites:24passed each, including fresh/populated migration and
  frozen-producer preservation; feature021 T004 is complete.
- Ordinary Aspire local-demo:3passed each, including actual browser grant submission,
  real Worker completion, re-enable/fresh sign-in and native lifecycle cleanup.
- All four retained Chromium/WebKit protection cells:32passed each, including the new
  genuine grant browser journey and the corrected prior login-focus case.
- Six new grant behavioral mutants killed on each provider, with clean baseline/restored
  receipts and source hashes matching this head. This is12actual kills, not compile failures.

Remaining CI failures: one UI polling test observes `loading` before native WebCrypto
hash validation completes; two unrelated SQL WebKit jobs fail locked NuGet restore with
connection resets; final acceptance fails and aggregate coverage is skipped. The UI test
previously assumed20event-loop turns completed the operation. It now awaits the actual
published terminal observation with a bounded test deadline. A deterministic digest
entry/release barrier proves pending work remains loading and cannot send a mutation.
Independent review caught an initially unarmed hash barrier; the corrected test now
awaits entry into the native digest mock before releasing it. Both polling tests retain
their original read-only/single-send assertions. All83UI cases
pass locally after correction, without product timing or security changes.

Eight additional provider cases compile: an independently enrolled and genuinely
password/MFA-authenticated approver continues while the original account remains disabled,
then loses its own persisted role authority; a missing actor-lock row; a mismatched current
factor generation; and a retained key exactly at24hours with genuinely renewed password/MFA
authority. Original approval/native-state oracles are preserved. The shared fixture still
creates native API entries at a documented component seam; this is not signed bearer or
browser acceptance. All2,565prior portable discovery records/order remain unchanged, with
eight additions (2,573total;48ordinary grant cases/provider).

Independent security/test review also corrected the replay case to assert the coordinator’s translated ApplicationHttpRejected401/409 rather than its internal exception types.
The initial expanded build lacked an import and was corrected. Final builds and quality
checks pass. A focused PostgreSQL attempt could not start its database when the shared
filesystem filled; all three selected cases failed fixture setup and none counts as a
behavioral result. Docker cleanup restored the prior small free space. No additional local
container retry is claimed; hosted execution is required for these new cases. The later
replay-expiry pair has not run locally. No new task completion is inferred from compilation.

Ordered issuance/re-enable/admission races, genuine cross-instance native entry validation,
remaining API/BFF failure boundaries and complete exact-head coverage/acceptance remain open.
PR124 stays draft/unmerged. Existing historical/manual/Firefox/A-009 and release gates persist.

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
