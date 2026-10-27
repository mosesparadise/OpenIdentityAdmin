# Native Aspire and dependency verification

Starting remote main: `1d1ce53597d96abe063caf51a0d037f817589a8d`. Branch `110-aspire-local-entry`; original workspace/branches/evidence preserved. User authorized native Aspire entry, stable10.x SDKs and current packages.

## Current evidence

- Reviewed native command/interaction APIs; corrected native resource-ID alias handling before runtime verification.
- Latest package restore passes. SDK10.0.403/runtime10.0.14 builds AppHost/tests with zero warnings/errors. Required unused interface stubs have narrow experimental-API pragmas; production interaction uses stable APIs.
- Focused suite: 35 passes, zero failures/skips. Native discovery: 138 AppHost cases = 100 Harness + 19 PostgreSQL + 19 SQL Server, adding 33 while preserving both real LocalDemo identities. Full unit verification passes 1,691 cases (Core25, Host1,561, Architecture5, AppHost100), zero failures/skips. The final AppHost rerun passes all 100 cases including the strengthened native snapshot-publication assertion.
- Full solution build passes with zero warnings/errors. Core contract verification passes 43 native cases and 37 Node identities, exact five contract exports and regeneration with no drift. Python control suites pass 232 cases.
- Independent architecture, security, Aspire, vendor/dependency and narrow final test source reviews found no remaining blocker within their scopes. Review corrected native resource aliases, actual child/descendant cleanup oracles and independent dashboard snapshot-publication assertions. Windows/runtime and hosted acceptance were not inferred from source review.
- Independent vendor/npm byte-provenance review and strict supply-chain gate pass for75locked npm entries. Current viewer query6, retained union13findings: all12historical plusnewqs1243452 remain unclosed. Viewers remain unmapped; inventory/notice/release gaps remain.
- Code-quality no-new-finding and seeded controls pass; no allowance expansion.

## Required pending evidence

Genuine both-provider dashboard-command enrollment/login/logout/disposal, contract/browser regressions, unchanged coverage/mutation gates and final independent reviews. Hosted acceptance/merge are not claimed. Actual Windows Docker Desktop execution remains unperformed; feature014SC001/T014 stays open.

## Decisions and limits

[Dependency decisions](dependencies.md) distinguish current stable from latest compatible OpenApi/TypeScript. Stable10.x SDK roll-forward replaces exactSDKpinning; locks remain exact and runs record the actualSDK. Frozen provider-compatibility history/lock is preserved. A-009 deferrals, all six historical findings and human/production/public-release gates remain open. M02-C refresh planning remains next after this correction.

## Rejected first hosted candidate and corrections

[Run 40556099508](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40556099508) tested source `f1ac42e10689ccb0d6c341a8c35a715c08e27a08` through merge `d2973a6bce45ffa44568f117b25bb0a016c35d74`; it is not accepted evidence. Required gates exposed:

- Gitleaks interpreted an `openapi-core-candidate.json` filename/hash dictionary entry as a generic API key. The exact recorded SHA-256 was verified as provenance, then represented as separate `file`/`sha256` fields. All values remain unchanged; the same checksum-verified scanner passes without an exclusion.
- Exact license records were missing for Deque.AxeCore.Commons4.14.0 and Microsoft.Data.SqlClient.SNI.runtime6.0.3. Independent review verified their actual license bytes are identical to the prior reviewed versions; new exact-version records preserve the original redistribution obligations and historical entries. The full285package/version inventory now passes. See [license review](evidence/license-review.json).
- The frozen producer wrapper inherited upgraded central package declarations, causing locked-restore NU1004. A first attempted global frozen-props override also failed because the historical wrapper lock contains EF.Relational10.0.12 absent from the older source's props. The corrected project-local props import the frozen source's definitions plus that exact historical wrapper declaration, while referenced old projects retain their own original definitions. Both frozen locked restore/build and ordinary current locked restore pass; old source and frozen lock remain unchanged. Runtime `.deps.json` is checked against frozen locked versions, with positive subset and negative upgraded/unknown/empty-package controls.
- The native process-tree test assumed descendant disappearance was synchronous with parent exit. .NET tree kill requests termination, while WaitForExitAsync joins only the represented parent. The corrected oracle observes both actual PIDs within the original timeout-plus-five-second-cleanup budget (or cancellation-plus-five seconds), before emergency test cleanup. It does not extend any production deadline, retry mutation, or accept a surviving descendant.

Corrected local verification: all 100 AppHost Harness cases pass under child-process coverage; ten supply-chain controls pass; strict vendor/npm review and 285-entry license inventory pass. A bounded intentional parent-only-kill mutation fails both observed-descendant tests; restoring the exact original source passes both. This confirms that the asynchronous exit oracle still rejects survivors. Normal current locked restore and frozen locked restore/build pass separately, with zero build warnings/errors. The corrected source requires fresh full hosted verification.

## Rejected second hosted candidate

[Run 40557715712](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40557715712) at `2230187accb736700495ffa71d2946edec6d6140` resolves the prior secret/license/static gates and builds the frozen producer. Both real database compatibility cells then reject stale test expectations for newly applied history rows: the approved EF runtime now writes10.0.14, while those witnesses still expected10.0.12. The correction changes only new-row expected versions; full old history rows, original generated migrations and frozen lock/source remain untouched. Independent controls require old10.0.12 plus new10.0.14 and reject both rewritten old versions and stale new versions through the same comparison used by the actual audit migration path. Existing scenario counts are unchanged.

The PostgreSQL demo cell also fails before test execution when official Ubuntu archive `apt-get update` exceeds its existing120-second acquisition budget. No passing demo evidence is claimed for that cell, no timeout is widened and no browser obligation is removed. The next corrected source requires fresh full CI.

## Requirement-to-evidence map

| Requirement / task | Permanent scenario evidence |
| --- | --- |
| FR-001,006 / T002,T004 | `DashboardInitializationTests.Given_LaunchProfiles_When_DeveloperSelectsDemo_Then_OnlyNamedProfileEnablesLocalActivation`, ordinary-profile command absence, and the current IDE/CLI quickstart. |
| FR-002 / T003 | `DemoCommandTests.Given_InvalidReturnedInput_When_Submitted_Then_NoMutationOrSecretDisclosureOccurs`, masked native interaction, cancellation and input-byte boundary controls. |
| FR-003 / T003 | `DemoCommandTests.Given_StaleOrForeignInitializer_When_Invoked_Then_MutationIsDenied`, concurrent admission and terminal-repeat controls; real native alias and dashboard publication controls. |
| FR-004 / T003 | `DemoProcessTests.Given_BlockedNativeTree_When_ExecutionStops_Then_ObservedProcessesHaveExited`, independent stdout/stderr bounds, exact UTF-8/EOF, and cleanup after command cancellation. Parent-only-kill mutation is rejected. |
| FR-005 / T005 | `LocalDemoRuntimeTests.Given_DisposableContainerDemo_When_RealBrowserJourneysAndRestartRun_Then_AuthorityAndLifecycleRemainBounded` on both providers; unchanged migration/data, browser, coverage and mutation obligations. |
| FR-007 / T007 | Locked restore/build and contract regeneration; SDK selectors and registry/byte provenance; supply-chain/secret gates; `FrozenProducerEvidenceTests` and actual frozen build; provider history-version substitution controls. |
| All / T006 | Exact-source hosted acceptance and independent source/evidence reviews; Windows execution remains separately open. |

The final history correction builds with zero warnings/errors; both provider-tagged witness scenarios pass with explicit wrong-new-version and rewritten-old-version controls. Formatting and no-new-code-quality checks pass. Actual database acceptance still requires the fresh hosted run.
