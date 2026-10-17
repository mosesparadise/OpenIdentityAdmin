# Native consumption integration checkpoint

18 October 2026, Europe/London. The user selected **T005–T009, including their required tests and dependencies** as the next five items. This branch continues that batch from verified remote main `1ffe5ed1481d065f487f12aeef80657cc955003d` (PR #65). None of the five tasks is complete at this checkpoint. PR #58/#60/#62/#64 and their accepted evidence remain preserved.

## Implemented boundary

The opt-in server and client adapters retain the installed OpenIddict 7.7.1 descriptors, orders, filters, transactions and native handlers. A fresh child scope validates authoritative persisted bindings and strict expiry under an owning-store coordination lock, invokes actual native validation/redemption, commits durable consumption and drains actual cleanup before the original dispatcher continues. Server code replay retains native linked-token revocation. Native client state consumption precedes correlation/error handling and remote transport. Other native client registrations retain their original behavior. No production composition invokes the adapters yet.

The shared child bridge retains the original request deadline and parent exclusivity through partial service acquisition, actual disposal and quarantine. Constructor failure in native client service-created scopes also retains the parent lease until partially acquired resources finish disposing. These paths cannot reopen a completed owner or renew the original request budget.

Additive provider-native migrations introduce the Platform profile/replay tables and the Sessions state-budget singleton. No enabled profile, native application or native scope is automatically seeded. The Sessions singleton is inert coordination state. Both existing provider implementations append their migration catalog entries; all historical migrations remain unchanged. The frozen pre-refactor database witness preserves old data, history and complete catalog rows as multisets, admitting added metadata only through its actual owning schema/table. Forged constraint/index names, wrong schemas, unresolved ownership and missing duplicate metadata are rejected by independent seeded controls.

## Verification scope

FR-005–011/013 and SC-002/003/005/006 govern this prerequisite. Pure tests exercise descriptor registration, child ownership and blocked/failing partial acquisition. Native fixtures extend the four existing PostgreSQL/SQL Server × Platform/Sessions ownership cases without changing their identities or three-minute deadlines. They use installed native dispatch/creation/validation/redemption handlers with synthetic **already validated** principals; they do not prove cryptographic or complete HTTP interoperability.

The native controls cover durable consumption despite later rollback, replay/missing rows, native PAR-versus-cache identity, error-callback consumption, persisted/principal expiry before/at/after the boundary, expiry during commit, ambiguous commit without retry, profile availability and binding, persisted type/application/authorization/subject mismatch and revoked authorization. A separate redemption/revocation barrier pauses after native row materialization and observes the exact competing session blocked by that redemption session. It requires the final committed row to remain revoked, independently of the lock observation. All actors remain joined through cancellation and cleanup failures.

The full Host run passes 567/567 with zero skips, including both additional native-client acquisition cases and the original generic acquisition control. All 199 Python verification controls pass; these use mocked image preparation and perform no native container operation. Informational unused-import verification passes for Host and Persistence. Both projects build with zero warnings/errors. All four provider/context models report no pending changes after the additive migrations. Code-quality checks pass without allowance changes. Hosted real-database execution, coverage, mutation controls and exact-head acceptance are still pending; source review alone is not database evidence.

All previous test identities, 48 coverage inputs, 15 module floors, 42 security mutation controls and Chromium/WebKit obligations remain mandatory. T006 must add the new protocol mutation/partition obligations before batch acceptance. Firefox remains deferred only through internal B2 under A-007 and returns before B3 implementation, activation or release.

## Remaining batch work

T005 still requires complete native pipeline ordering/interoperability, assertion consumption, quota reservation and issuance-specific commit/publication proof. T006 requires the final partition/discovery/coverage and mutation plan with decisive seeded controls. T007–T009 require coherent current administrator authority, explicit opt-in server composition and fixed-profile authorize/token decisions. Necessary registration/assertion/client dependencies are pulled forward under the user's selected scope; this schema and consumption checkpoint does not accept those features or B2.

All six historical findings, 48 human assessment rows and production activation/public-release blockers remain open. No expired diagnostic approval is renewed, no local SQL/container experiment is run, and no production change or merge is implied by this checkpoint.
