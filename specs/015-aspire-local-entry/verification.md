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
