# Hosted verification

## Seventh candidate — protocol observation remained insufficient

[Run41858361973](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41858361973)
tests `d5a1e5aac6e5060819512204b5e2d99e1b3cc6a2` through merge
`3fd6772eacada5da4d3a5e617bd93e8bcb4d01ac`, sharing tree
`28b7cceb5a677223276c4c63d68de2e3197e039a`. It is rejected: both Chromium
lifecycle cells still fail history observation. The new finite counters show hides0
against baseline0, shows3 against baseline1 and hidden0. The raw CDP correction did
not resolve capture; document destruction remains a hypothesis, not an established
cause. Required native events and output-erasure assertions are not weakened.

Both WebKit lifecycle jobs reach the unchanged660-second command deadline without a
usable completed case result. Their exact point of suspension is unproven. Completed
job-step/log evidence confirms these failures even while the run/jobs/check overview
still reports stale in-progress states. No complete success count or acceptance is
inferred from that inconsistent overview. Failed jobs are126673258723/126673258910
(Chromium PostgreSQL/SQLServer) and126673258899/126673259196 (WebKit PostgreSQL/SQLServer).

The next source replaces protocol-delivered lifecycle counters with synchronous
trusted-event counters under one random test-owned sessionStorage key, restricted
to the two lifecycle scenarios. Every event rereads the exact bounded record;
missing/corrupt/overflow evidence fails and never resets. Validation and private
secret scanning precede atomic removal of only that unchanged key/listeners, followed
by the unchanged empty-session-storage assertion. Shared privacy observers for
managed sign-in/recovery remain free of this instrumentation. A dispatched untrusted
visibility event is a negative control and cannot increment native acceptance counts.

A separate finite checkpoint records only scenario/boundary/operation/disposal-entry
enums in a wrapper-owned temporary file. On failure or success the wrapper decodes
at most64 bytes and prints fixed labels only. Filesystem/output failure cannot prevent
original cleanup or replace its primary error. This records the last successful
checkpoint write, not completion or proof of a timeout cause. The next run retains
all original deadlines, inventories, floors, one-send and no-late-output requirements.

## Sixth candidate — native lifecycle observation

[Run 41852108038](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41852108038)
tests `7a8d724d1e618661d6fc68f6f8cf4e80722c78cd` through merge
`1acf6a5274e90567410a1b18af6590f598724480`, sharing source tree
`b63d5a183822f7b8f330b4ed22897c4e9299d47c`. All122 jobs are accounted for:
116success, five failures and one skipped coverage aggregate. This run is rejected.
Both native demos, all four standard browser cells, all four protection cells and
all12 new consent/recovery/authority cells pass. Every mutation lane passes.

The four ApplicationLifecycle cells and final acceptance fail. Chromium on both
providers reaches real navigation but fails the required history-event counter
join. The retained stack does not distinguish which counter failed. Pinned driver
source confirms that its exposed-function handler resolves the execution context
after an await and drops the callback if navigation destroyed that context. That
is a concrete observation vulnerability and a plausible explanation, not a proven
attribution of the exact lost event. Subsequent source uses an owned raw Chromium
Runtime binding, restricted to the main document and bounded finite event data;
it requires the same trusted native transitions and adds finite failure counters.

WebKit passes the complete output/history/visibility case on both providers, then
fails the pending navigation case while waiting three seconds for upstream
RequestAborted before releasing its response gate. Browser document teardown does
not establish an immediate remote transport disconnect. The correction retains
the original five-second UI boundary, native browser completion, one send and no
late output, releases the gate and joins actual upstream middleware completion
within the existing three-second cleanup cap. Existing genuine transport-disconnect
tests are unchanged. This lifecycle case does not claim cancellation propagation.

Chromium's pending-case cleanup timeout masked its primary failure. Cleanup now
preserves the original exception and aggregates a cleanup failure without a second
join during disposal. Safe diagnostics inspect both inner source stacks without
retaining private messages. Neither old failing result is relabeled as passed.
Corrected-head execution remains required; no product behavior, deadline, inventory,
coverage floor, retry rule or browser obligation changes.

Failed lifecycle jobs: PostgreSQL/Chromium126651056736, PostgreSQL/WebKit126651056876,
SQLServer/Chromium126651056920, SQLServer/WebKit126651057004.

## First candidate

Run [41824740733](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41824740733)
verified head `04a850a008f271e01a866044baea3585df08ff4a` on 4 November 2026.
The complete two-page jobs inventory contains 104 jobs: 96 successful, seven failed
and one skipped. The failures are retained as failures; aggregate coverage was skipped.

| Failed job | Observation |
| --- | --- |
| 126556584646 / SQL Server native demo | Safe failure diagnostic reported the broad fresh-browser-login phase. |
| 126556584695 / PostgreSQL native demo | Same broad phase; it also covered application journeys. |
| 126556665019 / PostgreSQL Chromium protection | Four failures, 27 passes. |
| 126556665269 / PostgreSQL WebKit protection | Four failures, 27 passes. |
| 126556665338 / SQL Server Chromium protection | Four failures, 27 passes. |
| 126556665519 / SQL Server WebKit protection | Four failures, 27 passes. |
| 126567816227 / Verification acceptance | Required failing jobs prevented acceptance. |

All four protection lanes report the same failing methods:

- `ApplicationListBrowserTests.Given_RegisteredApplications_When_AdministratorSignsInAndSearches_Then_RealCollectionExpiresWithoutRenewal`
- `ApplicationPresentationBrowserTests.Given_RealPagedCollection_When_NavigatedAndPermissionRemoved_Then_AccessibleStatesClearProtectedData`
- `ApplicationMutationBrowserTests.Given_AuthenticatedBrowser_When_ApplicationTokensCrossPurposeOrGeneration_Then_OnlyCurrentBoundNativeTokenDispatches`
- `ApplicationMutationBrowserTests.Given_RealJsonTransport_When_UpstreamSubstitutesOrGenerationChanges_Then_NoSecretOrRetryEscapes`

Source inspection identified duplicate `Registered applications` headings on the
outer workspace and loaded collection, permitting a strict-locator collision and an
early match before loading. The retained list failure is sanitized as unclassified;
the presentation check expected 25 rows and observed zero. The outer action heading now
reads `Application actions`; the loaded heading and original assertions remain.
The two mutation tests expected `no-store` from native antiforgery token acquisition,
but ASP.NET returns `no-cache, no-store`. The exact assertion now requires that native
value; application mutation responses still require `no-store`. Production cache
behavior, test deadlines, retry rules and selection inventories are unchanged.

The demo now identifies application list/editor failures separately from sign-in.
The initial broad phase alone does not establish the demo's underlying failure cause.
Corrected hosted execution is required before claiming a resolved demo.

Local correction checks passed: all 55 UI identities and existing module floors,
deterministic generated UI, AppHost and Administrator test builds with zero warnings
or errors, code quality and scoped SDK unused-import verification. Independent UX
and security reviews accepted the heading/diagnostic and exact cache-header changes.
These local checks do not execute retained browser engines.

Evidence was read through GitHub's full jobs inventory and decoded job logs. The
separate artifact download was denied by the current proxy; raw uploaded ZIP/TRX
contents were not independently reparsed. No failed job or unavailable artifact is
represented as a pass. The [remaining browser acceptance](browser-acceptance-gaps.md)
also contains missing test scenarios, so a green existing matrix alone cannot close
T019/T022. All existing human and external release boundaries remain open.

## Corrected browser candidate

Run [41828715345](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41828715345)
used head `227a929a531b75435e838b2617221b116067d752`. Its 104-job inventory finished
with 97 successes, six failures and one skipped coverage job. Both native Aspire demos
passed all three required cases, and the earlier application list/presentation
failures did not recur. Each protection lane completed 31 cases with 29 passes, two
failures and no skips. The two mutation cases now reached the exact token-body check,
which observed Playwright object-reference metadata (`$id`) in addition to
`requestToken`. Failed lane IDs are 126570192254, 126570192426, 126570192488 and
126570192501. Final acceptance (126582479121) correctly failed.

The next correction reads the response body as text and parses that wire JSON in
.NET, matching the existing application-list test pattern. It still requires exactly
one property named `requestToken`; it does not allow or strip extra wire fields. A
boolean property-name assertion avoids printing the token body on failure. Independent
security review accepts this bounded correction; production serialization is unchanged.

The SQL Server ManagedApplications job (126570103709) separately exceeded the existing
660-second process deadline before producing a completed report. It is not a pass or
an identified product failure. The earlier same suite passed all 106 cases in
8 minutes 10 seconds (job126556586118); that earlier pass does not replace this timeout.
No completed-case count is inferred from the partial coverage upload.

## Bounded managed-application execution

The next candidate splits each provider's unchanged 106 managed cases into Admission
(18 classes/50 cases: cache, password and human authentication) and Issuance
(11 classes/56 cases: token and created/updated-client journeys). A shared explicit
class-group selector retains the prior BrowserSessions invariants. Default story
execution runs both groups. Every requested group first validates full native
discovery and every sibling filter, both-provider metadata/counts, exact tuples and
the disjoint union. Incompatible CLI combinations reject before execution.

The two old combined CI/coverage obligations become four required group reports:
`administrator-{PostgreSql,SqlServer}-managedapplications-{admission,issuance}`.
Required coverage inputs rise from 80 to 82; all existing cases and all 15 module
floors remain unchanged. Missing either new report fails even if an old combined
report exists. Native inventory remains 2,375 records. The 660-second process deadline,
native lifecycle/TRX/identity checks and no-retry rules remain unchanged.

Local validation passes 74 selection controls and seven browser-matrix controls.
Actual native discovery independently matches all 2,375 records and all ten filtered
groups across BrowserSessions/ManagedApplications and both providers. Administrator
tests build with zero warnings/errors. Architecture review accepts the exhaustive
partition and coverage wiring. Split-lane execution still needs current hosted
evidence; grouping does not establish the timeout's underlying cause or guarantee
runtime duration. No local browser download or SQL diagnostic experiment is repeated.

## Grouped candidate — 4 November 2026

Run [41833225401](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41833225401)
used `3c433e8190958d2f3766d0e751a5fc0ee799b03f`. The complete 106-job inventory
finished with 100 successes, five failures and one skipped coverage job. Both
managed groups on both providers and both native demo jobs succeeded. Each of the
four protection jobs completed 31 cases: 30 passed and the cross-purpose/generation
mutation case failed. Failed job IDs are 126585569846, 126585569914, 126585569974 and
126585570114; acceptance 126597288097 failed. No aggregate coverage pass is claimed.

The safe failure category was unclassified and source lines were absent because the
mutation test file was missing from the diagnostic allowlist. Source inspection found
that the foreign-origin fetch ran from the issuer product page, whose CSP disallows
that connection. The correction uses the existing explicitly test-owned attacker
page with the exact allowed peer, as the existing origin tests do. Product CSP,
origin validation and request assertions remain unchanged. The correction is a
source-supported hypothesis until the corrected test actually executes. The fixed
source filename allowlist now preserves useful line numbers without private messages.

This run predates the new same-registration browser ceremony and lifecycle/recovery
acceptance additions. Its successes cannot substitute for their execution. Earlier
failures remain separate; the raw artifact transfer limitation remains unchanged.


## Fourth candidate: complete browser scenario source

Run [41840008939](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41840008939)
verified head `0d03974e6969b979d0b169e022deb37d4370a74c`. The final two-page inventory
contains122 jobs:104 successful,17 failed and one skipped aggregate-coverage job.
All four new permission/expiry lanes and both Chromium consent-cancellation lanes
passed. None of these successes accepts the complete feature or a later source tree.

| Failed jobs | Retained observation |
| --- | --- |
| 126608883228 /126608883274 | Both native demos fail during the generic application/editor phase. |
| 126608959915 /126608960048 | Chromium protection: positive managed callback ERR_ABORTED and cross-purpose token fetch failure at line88. |
| 126608959926 /126608960087 | WebKit protection: consent Cancel contrast failure and the same cross-purpose fetch failure. |
| 126608959852 /126608959921 /126608959962 /126608960244 | Editor recovery reaches the lost-committed-output seam; its private failure flag is true. |
| 126608959871 /126608960086 /126608960224 /126608960014 | Lifecycle preflight times out waiting for actual hidden visibility. |
| 126608960085 /126608960207 | WebKit managed-consent cancellation finds white-on-silver contrast1.81 on the unstyled Cancel button. |
| 126622048969 | Required failures prevent verification acceptance. |

The [correction candidate](browser-corrections.md) separates source-supported product
issues from fixture limitations. Broad native-demo and response-seam failures do
not establish a more specific cause. Retain the original failures; no timeout,
retry, browser obligation, coverage floor or private-output rule is weakened.


## Fifth candidate: consent and native desktop corrections

Run [41846849133](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41846849133)
verified head`1efd6a78a6baafd2bb91af46dd8c6e0c8ace8bfe`. Its final122 jobs contain108
successes,13 failures and skipped aggregate coverage. Both native demos pass3/3;
all12 consent-cancellation, editor-recovery and authority lanes pass. Required
acceptance remains failed; these receipts do not accept a later source tree.

| Failed jobs | Retained observation |
| --- | --- |
| 126632731512 /126632731693 /126632732079 /126632731889 | The existing computed-style diagnostic control fails at its stable selector index; two new selectors had been prepended. |
| 126632731549 /126632731705 /126632731932 /126632732084 | Only the cross-purpose/generation method fails: genuine foreign text/plain POST returns415, while the test incorrectly expected400. Each lane reports30 passes/one failure. |
| 126632731671 /126632731722 | Chromium preflight observes an iconic native window, then times out waiting for hidden DOM state; the original driver session still owns visible capture. |
| 126632731912 /126632731919 | WebKit cannot find the anchored temporary window title because MiniBrowser adds its fixed prefix; both selected cases fail before native-cycle acceptance. |
| 126646899120 | Required failures prevent verification acceptance. |

[The targeted follow-up](browser-corrections.md#fifth-attempt-fixture-follow-up)
preserves diagnostic order, exact media admission, genuine valid-JSON Origin guards,
engine-specific native activity, and every existing timing/visibility/secret oracle.
The earlier claimed public-CDP reset is explicitly superseded. No old execution is
relabelled as a corrected pass, and raw artifact transfer remains unavailable.


## Eighth candidate — native history observed, pending completion still open

Candidate `51e76c64217e4fc7e0c5bf49debbde36bd3a66a3`, tested merge
`b4aaebd25038ad1e475aebf89143b9f5cf688a61`, share tree
`d3d8065103e7b7e54525aca7896f3f29189c3b06` over main `4c9fb24`.
[Run503](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41866449915),
attempt1, is rejected by actual failed jobs; it is not accepted through prior results.

Both Chromium lifecycle jobs (`126701941357` PostgreSQL and `126701941586` SQL Server)
pass the output/history/native-visibility method and fail the pending-write method:
one pass/one failure per provider, zero skips. The finite checkpoint identifies
Pending/Navigation/Release, followed by environment disposal. The native history
transition assertions complete; the independent cleanup join then expires under
its unchanged three-second budget. Exactly one browser request was observed,
upstream started/completed are true, browser completion is false and no response
was observed. This distinguishes the new failure from the earlier missing pagehide
counter, without proving why the native page-level terminal event is absent.

PostgreSQL's ordinary Aspire demo job `126701863155` passes two cases and fails the
runtime journey in the fixed phase “application creation, edit and replacement.”
Its outer sanitizer retained no more precise source location or error category.
SQL Server's ordinary demo passes3/3. The failure cause is unproven; teardown health
messages and an immediate validation-focus assertion are not established causes.
No retry, focus assertion change, deadline extension or test waiver follows.

Both WebKit lifecycle commands reach the unchanged660s command deadline, with no
completed case report accepted. Their last finite checkpoint is identically
Pending/OriginalDeadline/ResultChecked/disposalNone. This establishes the last
recorded operation, not a completed privacy/storage/presentation check or timeout
cause. SQL job `126701941793` reports it at16:49:14UTC; PostgreSQL job `126701941687`
at16:52:09UTC. The next source appends milestones for secret absence, observation,
storage and final presentation without changing prior stage ordinals or assertions.

Terminal metadata is now verified: all122 jobs are present;115 pass, six fail and
aggregate coverage is skipped. Failures are the PostgreSQL demo, all four lifecycle
cells and final acceptance. All other ordinary jobs, including SQL demo3/3 and all
consent/editor-recovery/authority cells, pass on this candidate. These results do not
accept the next source. PR120 stays draft/unmerged; no rejected run is rewritten.


## Ninth candidate — native completion and presentation remain open

Candidate `77b6799f66d219e1414dfb9ca381155f4d1e9260`, tested merge
`7aa9d431926073fb795fdd59273e9be44bfa9a34`, share tree
`babc4c23882fdba6d42fe24df05a1f609387c89a` over unchanged main `4c9fb24`.
[Run504](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/41872632765),
attempt1, is rejected by four actual lifecycle failures. Both ordinary native demos
pass3/3; the prior PostgreSQL demo failure remains cause-unproven.

Chromium PostgreSQL job `126723778435` and SQL job `126723778657` each pass the
output/history/native-visibility case and fail pending navigation, with zero skips.
The exact original request is seen once; both page and context completion are false,
upstream started/completed are true and no response is present. The finite checkpoint
is Pending/Navigation/Release/disposalEnvironment. Context-level observation did not
resolve the missing native terminal evidence. The original three-second join fails;
upstream completion is not accepted as browser completion.

SQL WebKit job `126723778510` reaches the unchanged660s command deadline at17:23:41UTC;
PostgreSQL job `126723778897` does so at17:28:20UTC. Both last record
Pending/OriginalDeadline/PresentationStarted, followed by disposalObservation and
disposalBrowser respectively. Privacy/storage milestones completed and presentation
began before unwinding. This does not identify the failed or stalled presentation
operation or establish the cleanup cause. No completed lifecycle report is accepted.

Normal and attempt-specific paginated job APIs independently retain stale parent
metadata:115 successes, two Chromium failures, three in-progress jobs, queued acceptance
and skipped coverage. All three in-progress jobs have completed steps: both WebKit
lifecycle commands failed, while the standard SQL WebKit job has no failed step.
Do not infer terminal totals from these inconsistent views. The independent safe
snapshot SHA256 is `401b9f8fdd14db41df33ee201072ec4aab117553803f12e49c8facca34216f1a`.
The next source requires fresh exact-head acceptance; none of these failed observations
is relabelled. PR120 remains draft/unmerged.
