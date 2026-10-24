# Selected T005/T007/T008 batch delivery

24 October 2026. The three items explicitly selected by the user, with their required prerequisites, are delivered for internal development. This completes these bounded tasks; complete B3, product activation and external release remain open.

| Task | Delivered behavior | PR / squash | Accepted workflow |
| --- | --- | --- | --- |
| T005 | Portable durable browser generations, one-use intent, atomic B1 ticket/audit transitions, bounded logout correlation and cleanup; additive migrations on both providers | [#78](https://github.com/mosesparadise/OpenIdentityAdmin/pull/78), `e6447e35fc0ec49edfe1acb897818924fd58105f` | [40045654668](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40045654668), 69/69 jobs |
| T007 | Fixed destinations, host-only bounded cookie policy, native antiforgery/browser-generation binding and current authentic B1 ticket acceptance | [#79](https://github.com/mosesparadise/OpenIdentityAdmin/pull/79), `42c9eda2f83866b3289b99d22b809fad6332cc8c` | [40048482317](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40048482317), 69/69 jobs |
| T008 | Dedicated issuer entry and MFA pages requiring new password/MFA, original intent expiry, strict cross-page isolation and committed fixed completion return | Same #79 squash | Same accepted run |

T005's separate [acceptance record](browser-coordination.md#accepted-t005--24-october-2026) retains its initial failures, correction and migration evidence. [T007 policy](browser-web-policy.md) and [T008 issuer journey](issuer-browser-journey.md) retain their scoped requirements, source reviews, controls and limits. T007 adds 54 Host controls to the prior signed identity/state/projection controls; T008 adds eight proof controls and 44 provider cases. Neither production BFF nor issuer browser composition is activated by this delivery.

## Exact T007/T008 source

| Boundary | Verified identity |
| --- | --- |
| Reviewed head | `40f49e67ead6df22093fda30ac3e4a338972819d` |
| Base | `e6447e35fc0ec49edfe1acb897818924fd58105f` |
| Tested merge | `0e085a80622a0efb7aa39681d70a898dd790f498` |
| Source, tested merge and actual squash tree | `a9bdfa5866bf03461bae7cfb52bbb7f4f2b7ae59` |
| Actual squash | `42c9eda2f83866b3289b99d22b809fad6332cc8c` |
| Workflow / attempt | `40048482317` / 1 |
| Coverage / acceptance jobs | `120742151035` / `120742267326` |

Exact head/base, mergeability and review state were checked immediately before the normal squash. No review restriction was bypassed. Reconciling the earlier T005 squash into the feature branch preserved the entire reviewed implementation tree. Every prior Administrator name/type/trait remains: 1,483 preserved plus 44 new equals 1,527 native identities (711 PostgreSQL, 816 SQL Server, including existing browser cases). Existing provider interfaces, migration histories, CI workflow, collectors, module floors and deadlines remain unchanged.

## Verification and independent review

All 69 exact-head jobs pass. Independent evidence review matched 16 focal artifact ZIP SHA-256 values and recounted 14 raw TRX reports with strict native bindings and no failed/skipped cases: BrowserSessions 143/provider, Host 1,368, Persistence 10/provider, AppHost Foundation/BffSessions 8/10 per provider, historical Governance failure controls 61 PostgreSQL/166 SQL Server, and unit Core/Architecture/AppHost 25/5/65. The four corrected audit-denial and completed-MFA lost-acknowledgement cases are individually Passed on both providers. Frozen and historical migration upgrade controls remain passing.

All 48 required security mutation kills have passing baseline/restored controls, exact inventories and independently checked source/mutant hashes. All 52 expected coverage inputs belong to this source and pass the hosted nonempty-input guard; independent recomputation of the raw merged report passes all 15 unchanged floors (90% lines/80% branches). Local builds, 1,368 Host cases, 219 tooling controls, source quality and import/whitespace checks also pass. Interrupted local execution during a workspace restart is excluded from these pass claims.

Architecture/API, security and test source reviews pass after the bounded cookie-lifetime, exact replay, background-worker isolation and precise commit/save-failure observations recorded in the implementation notes. Razor source review confirms shared labelled fields, headings, error focus target and existing layout/theme consistency. TestServer HTTP over actual providers does not establish trusted TLS or browser-engine/accessibility acceptance.

The initial PR #79 run `40047493717` at `2cf968b` passed PostgreSQL 143/143 and SQL Server 142/143. Its SQL failure was a missing command-level audit-error receipt; the HTTP denial checks passed. The replacement save-failure observer still requires exact native PostgreSQL 42501/SQL Server 229, rollback and genuine success after permission restoration. Corrected execution passes both providers. The old exception phase was not retained; a precise historical causal claim is not inferred from the green replacement run.

Evidence limits: raw inspection covers the 16 focal bundles and 14 reports, not independent unpacking of every coverage input. Mutation evidence is sanitized JSON rather than raw mutant TRX. Frozen producer receipts bind `38eb41e` and 77 binary hashes; those binaries were not independently rehashed. One pair of truncated Host display names is bound through unique result/definition/execution IDs.

| Retained artifact | ID | SHA-256 |
| --- | --- | --- |
| administrator-protocol-mutations-PostgreSql | `12543006186` | `dd90c67cbb26868dd5c57a5c5c5f960401db2ca8ca010872646164f9460a7d93` |
| critical-mutations-PostgreSql | `12542882067` | `c1504be8a650a210a5d04753ff8fe78b43376f43e4d1152bae36b4ab7a543da9` |
| administrator-protocol-mutations-SqlServer | `12542408443` | `46c64db7411d65bdb301e994ba4c03c8bf94bfa8458c01ed3a0b6ff5a56c9e61` |
| foundation-unit | `12542279098` | `c515208b5d70d24e4cd8269ea6b4714b66973ecd6a7fc42f03fa6e68891980ef` |
| administrator-mutations-PostgreSql | `12542692659` | `b3707a68a9e83d6348243d0d1889db85d69b4fa1c99b297b4ffd373924bf0488` |
| foundation-administrator-PostgreSql-governance-failures | `12542638017` | `5b977ea96493fb97d467e56968177c9ee7b844272be3682bf6d9c0f027a762cd` |
| governance-mutations-SqlServer | `12542483422` | `ebe0fe17356e3d3dc1ba293ca681f1919f3a0f4d109c057d6b6854957d150284` |
| foundation-administrator-SqlServer-governance-failures | `12543190475` | `06a8c216c811607f83dcb30920329ca570da2c7873adc4c5fd624467c4a07a65` |
| foundation-administrator-PostgreSql-browsersessions | `12542372845` | `6370835d2d9413fd63b5a964b6df32f017814003da3272e61bbd65706858df08` |
| critical-mutations-SqlServer | `12542783081` | `638570c2d4df89ac7a74b8271aef52cd10bfe19a5aaeb5c790dabff2fb3a1645` |
| administrator-mutations-SqlServer | `12542173530` | `4f3fbb484f806fe08a77610aff67a590829db006f434e1389803b8ba4d4d09a0` |
| foundation-administrator-SqlServer-browsersessions | `12543032212` | `b08c18a91144fa9593c794b662bb1526b03458f151cd01bc677adb407886be77` |
| governance-mutations-PostgreSql | `12543141756` | `fd218ecb76fdf581339741bb05073776c67824b261cf8d755bd1ab8dc0169663` |
| foundation-PostgreSql | `12542687330` | `8c9c2a5299c015e64aee651b5071a74c3c88903318a05b283e8be503dc295e26` |
| foundation-coverage | `12543207123` | `af35cbea76402f3a7278a99b5ea2f45bd116a31c5b27ed6c29971b97a9b04749` |
| foundation-SqlServer | `12543108055` | `f6a236df4aedaf038f7b3d90198d415dce1b0b8a5e1a1603ea7089e34647012a` |

## Next bounded implementation and open gates

Next is T009: compose the already-proved native intent/completion binding and strict B3 ID-only source-deadline/intent claims under current-authority ownership, preserving ordinary B2 behavior. Then T010/T011 connect protected BFF start/resume, a fresh native challenge and the canonical committed B1 callback. Existing T003/T004 prerequisites remain partial for those dependent integrations, end-session/composition and their new inventories; this delivery does not mark the whole prerequisite program complete.

Product cookie/status/logout adapters, complete callback/browser journeys, issuer-assisted logout, browser/theme/accessibility acceptance, operations and full B3 delivery remain later tasks. A-009 keeps Firefox and documented Playwright issues deferred for internal implementation, without closing browser-support or release obligations. PR #69 and issue #70 remain open. The embedded viewer Zod 4.4.3 advisory GHSA-mrhx-c7xc-m3w5 remains affected with reachability unresolved and activation blocking.

All six historical findings, 48 human rows, 345 not-assessed assurance controls and human/activation/production/public-release gates remain open. No expired diagnostic approval is revived, production deployment performed or external certification claimed.
