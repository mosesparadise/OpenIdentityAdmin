# Local demo navigation-budget correction

## Verified starting point and retained failure

PR112 source `132dadd8d653dc6068fb9f4178b33625b9b8902e` passed all95 jobs in [run40700541564 attempt1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40700541564/attempts/1). Independent review verified89 artifact ZIP hashes,79 raw TRX reports/4,693 passes with zero failures/skips,76 coverage inputs/all15 unchanged floors and64 source-bound mutation kills with baseline/restored controls. Source and tested merge `6cdf5737dfb9b1768259015e1745ee274af43fe4` share tree `06581b5e509ca0863d80cc64ee7d5450b1bb7086`.

The earlier [run40694955525](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40694955525) failed the unchanged PostgreSQL demo during its second fresh ceremony. Its sanitized report cannot identify the failing browser operation or recover its duration. [Original failure evidence](native-cleanup.md#rejected-full-run-and-bounded-demo-diagnostics) remains retained. Three predetermined, sequential focused executions at132dadd (attempts2/3/4, jobs122872543660/122877013593/122881304067) each passed the sole exact demo identity with zero skips; their hashes are recorded on [PR112](https://github.com/mosesparadise/OpenIdentityAdmin/pull/112). Those passes establish nonreproduction, not a cause or correction.

## Separately reproduced harness defect

The demo configures a15-second action timeout and a20-second navigation timeout. Its original MFA sequence awaited `Locator.ClickAsync()` and only then called `WaitForURLAsync()` for the exact BFF login destination. Installed Playwright1.63 includes implicit navigation in the click's15-second progress deadline. A navigation taking between15 and20 seconds therefore fails before the explicit navigation wait can apply its configured budget.

A local actual-browser probe using installed Playwright1.63 and explicitly selected system Chromium151.0.7922.173 reproduced this: a synthetic17-second redirect timed out after15,008ms with the original click, while explicit navigation ownership completed in17,130ms. Each fresh context sent exactly one POST. These are synthetic loopback HTTP observations, not authentication, TLS, database, Windows or pinned-CI-browser acceptance.

A second .NET10 probe compiles the repository's actual `LocalDemoNavigation` and `LocalDemoNavigationFixture` sources. Four nominal4.25-second redirect delays reproduce the same failure without making one synthetic handler wait17 seconds. The complete [source-linked local receipt](evidence/demo-navigation-local.json) records the original click timing out at15,022ms and the corrected helper completing at17,306ms, each with one POST. A disabled action retained its native15-second action timeout after joining the outstanding navigation wait at20,002ms, with zero POSTs. An unfinished navigation failed at20,005ms after one POST. The delayed-action control failed at20,000ms after one POST, proving the navigation budget was not restarted after the click. These are supplemental local observations; required hosted results remain pending.

This establishes a real defect in the exact harness sequence. It does **not** establish that the earlier sanitized second-ceremony incident had this cause. No TOTP-selection, stale-cookie or product-transaction cause is claimed.

## Correction and unchanged product boundaries

`LocalDemoNavigation.SubmitAsync` arms one exact-URL, normal-load-state navigation wait before the single native click. The click retains normal actionability checks and its15-second limit, with `NoWaitAfter=true` because navigation is explicitly owned and awaited. The20-second navigation deadline starts before the click and is never restarted. Both tasks are observed; if action and navigation fail, the original action exception remains primary. No form resubmission, authentication retry or timeout recovery is added.

The **effective post-submit navigation allowance changes from15 to the already configured20 seconds**. Numeric browser defaults, issuer/BFF10-second requests, five-second owned operations, nine-minute runtime scenario,660-second invocation and20-minute job limits are preserved. This is a reviewed test-harness wiring correction within authorized verification work, not a new authentication policy or A-009 exception.

Playwright1.63 marks `LocatorClickOptions.NoWaitAfter` obsolete because it will default totrue in a future release; the installed documentation explicitly states its current default isfalse. One CS0612 pragma surrounds only that assignment, with this rationale. No project-wide warning or code-quality baseline is changed. The explicit destination/load-state checks remain mandatory even after a future library default changes.

## Permanent required inventory

Feature014 FR-003/004/006, SC-002 and NFR-001 are exercised by the existing real-container demo and these additional harness controls. Feature016 T003 cleanup evidence remains separate. Both new methods are in `IdentityPlatform.AppHost.Tests.Features.LocalDemo.LocalDemoNavigationTests`, with exact PostgreSql/SqlServer provider traits:

| Scenario suffix | Required observations |
| --- | --- |
| RedirectChainBeyondActionBudget / ExactDestinationCompletesWithoutResubmission | Original click demonstrably fails at15 seconds; the shared corrected helper reaches the exact destination and completion DOM through the same17-second chain, one POST in each fresh context |
| UnavailableActionOrUnfinishedNavigation / OriginalDeadlinesDenyAndBothWaitersAreObserved | Disabled button produces the original15-second action error and zero POSTs; unfinished navigation fails at20 seconds after one POST; a seven-second delayed action plus17-second chain still fails at the already-armed20-second deadline rather than receiving a new budget |

The browser controls use synthetic public HTTP only and do not claim provider persistence coverage. They run in both existing browser-provisioned LocalDemo jobs beside the genuine provider-specific runtime journey. LocalDemo grows1→3 cases/provider; AppHost provider totals19→21. All prior identities and groups remain required. The administrator catalogue remains2,005, including27 SessionRefresh cases/provider. Expected full ordinary executions become4,697 in79 reports; all95 jobs,89 artifacts,76 coverage inputs,15 floors,64 mutation controls and precise browser deferrals remain required. Discovery and actual hosted evidence must confirm these counts.

Local validation uses SDK10.0.403/runtime10.0.14: AppHost test build succeeds with zero warnings/errors;100 existing Harness cases pass with zero skips;38 AppHost/issuer/lifecycle/browser selection controls pass. Native discovery contains142 AppHost identities, preserving all138 prior executed names from the independently verified reports and adding exactly four. Its disjoint groups verify8 Foundation +10 BffSessions +3 LocalDemo per provider. Formatting/imports, code-quality and assurance-register integrity pass without allowance expansion. Independent architecture and test source reviews pass; supported-browser and real-provider hosted acceptance remains mandatory.

## Acceptance disposition

Architecture and test review classify the observed deadline overlap as a routine authorized harness correction. A new corrected-source acceptance decision requires the permanent before/after and timeout controls on the supported CI browser, both genuine provider demo journeys, unchanged full exact-head gates and independent review. The historical failure stays recorded with unknown cause; accepting a reviewed correction does not reconstruct missing evidence or turn the three previous repetitions into causal proof. Any recurrence is a failing required case and must be investigated.

PR112 remains draft until that corrected-source acceptance. Product refresh, T003–T022, actual Windows014 T014, all historical/human/assurance/viewer findings and production/public-release gates remain open. No production code, registration, database schema, credential handling or authentication requirement is changed by this correction.

## Accepted corrected source — 28 October 2026

[PR112 delivery](native-cleanup.md#accepted-internal-delivery--28-october-2026) supersedes the pending candidate disposition above. Source `b3e8cf3a3720149cac54bf2dbf18dc2903471041` passed all95 jobs/4,697 raw executions in run40716859486 attempt1; both LocalDemo lanes passed3/3 including the permanent before/after and deadline controls and genuine runtime journey. Independent final source and artifact reviews pass. PR112 is squash-merged as `5e8719040a9beaddb6e15c1e9197b5a79ec44048`. The earlier5d failure's unknown cause and original rejected evidence remain unchanged; this is acceptance of the independently reproduced correction, not retrospective causation. No runtime task or external gate closes.
