# Application lifecycle implementation verification

## Accepted internal delivery — 5 November 2026

[PR #122](https://github.com/mosesparadise/OpenIdentityAdmin/pull/122) is squash-merged as
`0ce00a22f4534847bb7ae5731ae20bf6b6c3064f`. Managed application details now offer Disable and Re-enable through ordinary
Aspire. Disable stops new sign-ins and token issuance; re-enable requires fresh issuer proof
and cannot revive old requests/codes or extend credential expiry. Existing grants remain
intact; bulk grant revocation is separate, as selected by the user.

Source `966d93f16ccfb0a2f8097ce208b965a621c2df04`, tested merge `700991071391a1964d57aee1feab77656ee4951d` and the squash share tree `23a62b9dea82ad3ccd9d3635fc5ee193211c0c5d`.
Base was `b27669f04ad5992ae829c1250bd13f90e8c72446`.
[Run 522](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41962624583), attempt 1,
passes all 122 exact-head jobs. The finite reconciliation binds every job's checkout,
strict report summaries, mutation steps and artifact upload/API metadata to this run.
No result from rejected/superseded runs516–519 or521 supplies acceptance. Their failures and unknown
causes remain recorded in [hosted verification](hosted-verification.md).

| Verified boundary | Accepted evidence |
| --- | --- |
| Ordinary execution | 101 strict report obligations; 5,531 required native passes, no failures/skips |
| Identity preservation | 2,475 Administrator identities; all 2,385 pre-feature identities preserved |
| Application and issuer | Applications 92/provider and ManagedApplications 118/provider, including genuine enforcement, peer isolation, old-artifact rejection, races and failure persistence |
| Upgrade | Persistence 21/provider with real PostgreSQL and SQL Server, additive schema and historical preservation |
| Native Aspire | Both ordinary local demos 3/3, including create → sign in → disable → denial → enable → fresh success |
| Retained browsers | All40 provider/engine cells pass; Chromium/WebKit protection 31 per cell and all existing lifecycle/consent stories |
| Harness | 110 cases, including clock, credential binding and private diagnostic controls |
| Contracts/UI | Native core 44, viewer 7 per retained engine; separate UI 62 and Node 44 exact controls |
| Coverage | All98 required inputs and 15 unchanged module floors pass |
| Mutations | 98 exact behavioral kills in 16 suites across 14 jobs, with baseline/restored controls and source restoration |
| Artifacts | All116 upload-log/API identities and SHA-256 values match; [manifest](accepted-artifacts.md) |

Actual UI completion, one mutation request, independent resource/version binding and native
authentication of the original displayed credential replace Playwright inspector-body/DOM
equality. No fetch interception, response cloning, automatic mutation retry or security-timeout
extension is used. The invalid-code credential pair proves authentication before replacement;
it does not itself prove PKCE or mint a code/token/grant. The later real relying-party journey
proves the replacement credential's complete code/S256 exchange. The managed browser fixture
selects a shared live clock before enrollment; frozen boundary tests remain frozen and native
lifetimes/expiry clamps remain unchanged. The OriginalDeadline fixture now reads initial expiry
from actual UI and verifies the final BFF's longer timestamps through an opt-in, bounded,
single-write observer bound to the same sealed request and both completion signals. Its
unchanged expired-display/no-secret assertions prove rejection behavior; final server
publication does not claim browser consumption of intentionally suppressed JSON.

The inherited [native visibility evidence limits](../019-application-editor/verification.md#accepted-internal-delivery--4-november-2026)
still apply. All40 browser jobs pass the12 separate pinned-driver instrumentation controls;
all four actual lifecycle wrapper steps bind the selected copy/version/hashes. These controls
are additional to the ordinary native total. Configured headed/headless topologies and explicit
Chromium automation prerequisites do not imply an ordinary unmodified-browser result.

Independent architecture/security/API/UX and test reviews pass the implemented boundaries;
the final scoped architecture/test review also passes the passive observation correction.
The [sanitized acceptance receipt](evidence/hosted-acceptance.md) records reconciliation
hashes and limits. Review used decoded verifier logs and upload/API metadata. Raw uploaded
ZIPs were not independently downloaded/reparsed, and local browser binaries were unavailable.
Rounded coverage log percentages are not independently recomputed coverage; the hosted checker
enforced exact thresholds. No unsupported local browser or actual Windows pass is claimed.

T001–T017 are complete for this bounded internal slice. Use the [local walkthrough](quickstart.md)
from main. Strict API/BFF JSON requires a coordinated/quiesced compatible switch; see
[deployment and recovery](data-model.md). Actual Windows Docker Desktop, qualified manual
accessibility, Firefox and precise A-009 deferrals, all six historical findings, human/vendor,
production and public-release gates remain open. Refresh remains disabled. The dated candidate
records below retain their historical state and are superseded by this delivery.

## Candidate and scope

**Historical checkpoint, 5 November 2026 — implementation was in progress, not yet accepted or merged.**
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
| Corrective AppHost Harness | 110 passed, zero skips, including deterministic MFA exhaustion, private-safe diagnostics, clock/binding controls and four final-publication observation controls. |
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

Run519 passes both live-clock WebKit protection suites31/provider and all98 mutation controls, but remains rejected for two inspector-body observation failures. The reviewed passive correction passes106 Harness cases and the exact paired native credential characterization on both providers. It replaces inspector body/DOM equality with actual UI consumption, independent request/version binding and original-credential native authentication; no fetch interception or mutation retry is used. See [bounded evidence](hosted-verification.md#passive-uinative-observation-correction).

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

The OriginalDeadline correction builds both test projects with zero warnings/errors and passes110 Harness cases,74 selection controls, unchanged complete2475-case Administrator metadata and source quality without allowance growth. A preceding multi-node Administrator build lost three MSBuild workers without retained diagnostics; subsequent serial builds pass. The exact hosted lifecycle failure and correction limits remain in [hosted verification](hosted-verification.md#original-deadline-publication-observation-correction). Corrected hosted acceptance remains open.
