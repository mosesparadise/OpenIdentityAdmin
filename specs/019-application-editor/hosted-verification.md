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

The outer workspace and loaded collection both used the heading `Registered
applications`. This caused a strict locator collision and allowed the presentation
check to observe zero rows before loading completed. The outer action heading now
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
