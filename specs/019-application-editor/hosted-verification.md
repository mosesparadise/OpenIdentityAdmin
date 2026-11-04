# Hosted verification

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
