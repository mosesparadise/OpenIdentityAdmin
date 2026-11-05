# Application lifecycle implementation verification

## Candidate and scope

**5 November 2026 — implementation in progress, not accepted or merged.**
The user selected issuance-only disable/re-enable, with bulk grant revocation separate.
PR122 is the implementation PR on `126-application-lifecycle`; remote main was reverified as
`b27669f04ad5992ae829c1250bd13f90e8c72446`. The previous planning checkpoint is
`554376bde37ab492dbf66e6eefa890b1c3224e3b`. Planning-only CI is not evidence for this implementation.

The candidate adds lifecycle state, numeric linked audit reasons, both additive provider migrations,
explicit permission/MFA/action dispatch, genuine issuer enforcement, API/BFF contracts and React
controls. Both transitions advance the aggregate version while preserving native configuration,
credential hash/revision/expiry and existing grants. A disabled application can still be edited or
have its credential explicitly replaced; neither action enables it. Re-enable requires new issuer
proof and cannot revive earlier cached requests, human ceremonies or authorization codes.

No production changes, refresh activation, bulk revocation, expired diagnostic approval,
Firefox execution or historical-release closure is included. Ordinary Aspire remains the local
entry point; see [quickstart](quickstart.md). Deployment requires the documented issuer-first and
coordinated/quiesced strict-JSON API/BFF/UI switch in [data model](data-model.md).

## Local evidence

Environment: .NET SDK10.0.403/runtime10.0.14, Docker28.4, Node24.19/npm11.9; disposable real
PostgreSQL and SQL Server under the existing pinned provider images. Builds and provider runtime
suites are serialized. No operation timeout, MFA window, retry policy or coverage floor was widened.

| Executed check | Result and limits |
| --- | --- |
| Persistence `ManagedSchemaTests` and `LifecycleSchemaTests`, each provider | 6 passed/provider, no skips. Fresh/populated upgrade, exact historical data/history, idempotence, physical default and finite reason/FK constraints. |
| Administrator lifecycle namespaces, PostgreSQL | Final33 application and11 managed cases passed (44 total), no skips. Includes native expiry, finite reasons, permission/MFA/waits/failures, independent peer, old artifacts, issuance races and delayed committed publication. |
| Administrator lifecycle namespaces, SQL Server | Final33 application and11 managed cases passed (44 total), no skips, including accepted finite audit reasons, reason drift and expired-credential enable denial. |
| Corrective AppHost Harness | 104 passed, zero skips, including deterministic MFA exhaustion, private-safe managed callback diagnostics and source-backed clock controls. |
| Full corrected Persistence | PostgreSQL21/21; SQL21/21 with serial execution. Earlier SQL20/21 container-startup failure is retained in the hosted correction record. |
| Focused managed admission | One genuine BFF/issuer case per provider passed; full journey body remains unchanged. |
| Host Applications tests | 235 passed, no skips, before final schema metadata refinement. |
| Core/native contracts | Final core partition44 passed, no skips, including exact finite reason and true-only confirmation schemas. |
| Native API/BFF export, generated browser types, UI build/check | Final native export, core browser-contract generation, deterministic regeneration, TypeScript and six UI assets pass. |
| Frontend tests | All suites passed, including42 editor cases; critical editor modules retain their existing coverage floors. Generated contract/tooling controls44/44 pass. |
| Selection tooling | 74 passed after regeneration from actual native discovery. |
| Mutation tooling controls | 60 passed. Original and focused four lifecycle witnesses demonstrated kills on both providers; exact receipt source bindings are recorded in [evidence](evidence/README.md). |
| Source quality | Build, staged-source secret scan, whitespace and final source rule checks pass without expanding legacy allowances. |

Current actual discovery contains2,475 portable cases and preserves every2,385 pre-feature identity.
The original comprehensive journey remains required; its two CriticalDecision traits move to the new focused admission cases.
Non-browser Administrator counts are1,114 PostgreSQL/1,219 SQL Server: Applications92/provider,
ManagedApplications118/provider (Admission54, Issuance64). Persistence21/provider. The native
inventory validator checks complete names, type metadata and traits; no path-dependent UID is pinned.

An initial broad contract invocation included seven viewer cases before the local generated consumer
bundle existed; those seven failed at fixture setup, while all44 core cases passed. The subsequent
exact core partition passes independently. Viewer acceptance remains open; neither that broad run
nor the unavailable local browser binaries count as a browser pass. Earlier journey test fixes kept
exact authorization/token cardinality and waited for a real TOTP counter rollover after exhausting
the native window with multiple ceremonies; no native validity window was widened.

The feature's four new mutation controls target disabled admission, exact lifecycle permission,
aggregate version advance and independently verified persisted reason. The existing isolated runner
requires one exact tagged case/provider, unchanged baseline/restored passes, the expected marked
behavioral failure, identical discovery and source restoration. Both provider workflow lanes include
the new suite without raising existing time budgets. Original and focused controls have both-provider receipts; the focused executions bind to a87a96d, before the later live-clock browser fixture correction.

## Hosted correction checkpoint

[Run517](hosted-verification.md#second-candidate--integrated-checks-remain-rejected) is rejected:113 successful/eight failed/one skipped jobs. Narrow upgrade-witness corrections pass21 persistence tests/provider locally; focused admission passes once/provider. Demo proof preparation and finite WebKit completion diagnostics address the remaining evidence gaps without changing product policy. Full corrected hosted acceptance remains required.


[Run516 and its corrections](hosted-verification.md) retain the first implementation candidate's
missing persistence catalogue entries and four unused imports. Full actual persistence discovery
now preserves38prior identities and appends4new cases,21/provider; all74 selection controls and
the complete IDE0005 style verification pass. These are verification bookkeeping/style corrections,
not relaxed gates. Corrected exact-head hosted acceptance remains required.

The live-clock correction builds, passes104 Harness cases, full source quality/IDE0005 checks and complete unchanged2475-case Administrator discovery. Both focused mutation suites passed at a87a96d; run518 also passes both persistence21/provider and both native demos3/provider. Required corrected WebKit and final exact-head acceptance remain open.

## Reviews and remaining acceptance

Independent architecture and security source reviews found no production bypass or atomicity defect.
Test review found the four new mutation witnesses decisive in design. Their acceptance remains
conditional on execution. Reviewer findings prompted these regressions/refinements:

- Accepted security/configuration requests must link numeric2/3 to the correct actual audit.
- Enable at expired credential must preserve expiry/hash and still deny real issuer admission.
- Authoritative input schemas must publish exact reasons and true-only confirmation.
- Lifecycle confirmation, validation and success require both themes and explicit success focus.
- Lifecycle browser recovery must prove stale-screen rejection and lost committed response followed
  by deliberate reload without resubmission.

The final refined candidate still needs both-provider full affected stories, required
unit/contract/quality/coverage checks, behavioral mutation execution, retained Chromium/WebKit and
both native Aspire demos, independent final reviews and exact-head hosted reconciliation before
ready/squash. PR122 remains draft during this verification.

Local pinned browser executables are unavailable. No CDN retry, alternate binary or policy bypass
was attempted. Browser/demo code is implemented, not locally passed. Old-artifact rejection is
proved by native HTTP tests; browser/demo journeys add fresh denial and fresh success and do not
independently claim old-code replay coverage. Raw hosted artifact downloading is unavailable here;
any eventual hosted receipt must state the actual evidence source and its limits.

Actual Windows Docker Desktop, qualified manual accessibility, precise A-009 certificate deferrals,
Firefox deferral, six historical findings and human/vendor/production/public-release gates remain open.
