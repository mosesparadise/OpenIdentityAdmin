# Durable issuer continuation and native PAR attachment

23 October 2026. Feature 013 T006 and dependent T003/T004 groundwork; FR-001–003/011–013, B3-AUTH-01–04 and B3-STATE-03/04. Base is accepted PR #75 squash `9f062444d23cbf817905aff5d214ef51c48c294f`. This is a dormant infrastructure slice; product routes, browser generation, source-session authorization and B3 claims remain later dependencies.

**Accepted internal delivery (23 October):** PR [#76](https://github.com/mosesparadise/OpenIdentityAdmin/pull/76) is squash-merged as `600874938afea432044c3731397ca390453b61ff`. T006 is complete after all 69 exact-head jobs and independent source/raw-evidence review pass. [Acceptance and limits](#accepted-t006--23-october-2026) supersede the pending checkpoints below. Product routes and complete B3 activation remain separate work.

## Persistence and ownership

The Platform continuation retains canonical decoded-byte intent/browser digests, independent audit correlation, original expiry, monotonic observation, revision and exact interaction/account/new-session snapshots. Pending entry becomes completed only inside the private real `JourneyCompletion` path after password and unused native MFA verification. The raw completion selector is returned only through the existing post-commit/post-disposal grant. Password admission alone cannot issue it; an existing session cannot substitute for a fresh ceremony.

Anonymous entry uses a distinct transaction mode, the existing installation admission lock and a restricted fresh-pending commit. The commit constructs its own finite anonymous audit and rejects extra tracked entities or prepopulated authority. Bounds are 128 live, 10,000 retained, one live per issuer browser and 300 seconds from original admission. Duplicate/capacity denial preserves existing records. Cleanup removes at most 100 strictly eligible records and retains equality/future-watermark rows. Late password binding clamps the interaction expiry to the original continuation expiry; completed state retains through the actual new session expiry plus 30 seconds.

Additive native EF migrations append to each existing Platform history and provider catalog. They add `AdministratorIssuerContinuation` and the nullable interaction marker, with bounded ordinal fields, revision/time constraints and unique nullable interaction/completion/request references. PostgreSQL native nullable uniqueness and SQL Server filtered unique indexes permit independent unbound entries. No database/provider implementation, existing migration identity or history table is replaced.

The frozen-provider compatibility witness now permits exactly the additional table, known migration IDs and one appended nullable 64-character interaction marker. It binds exact columns-query provenance and both owner copies, rejects altered existing metadata, requires every old interaction's new marker to be null and compares every preceding field. The reusable conformance runner receives independently captured old history IDs instead of assuming a fixed number of missing migrations. The frozen producer is unchanged. Two standalone seeded controls pass without database startup; both native compatibility runs remain required.

## Native attachment

Explicit `AddBrowserCompletionPar` composition is dormant and absent from product startup. Canonical raw/native pair agreement is required when present. Existing profile ownership precedes the admission lock; attachment creates no nested transaction or HTTP-held ownership. The completion stays detached during native capacity checks so denial/cleanup cannot accidentally save it.

The adapter freezes immutable browser/interaction/account/source-session provenance, original deadlines, state and revision. It adds private binding to the actual native request principal, limits expiry to the earlier request/continuation/session deadline, and attaches exactly once to the actual token ID only after existing native issuance verification. Attachment and finite client audit share the original issuance commit. Original binding and permitted state/revision transition are checked again before commit. Source-session authorization is a separate T009 obligation: the dormant adapter rejects bound authorization after native parameter restoration until that policy is implemented.

## Review corrections and regressions

Independent architecture and security review identified and corrected:

- Missing interaction marker could fall back to ordinary completion despite an inverse continuation reference. The unchanged completion overload now checks that reference and fails closed.
- Completion staging accepted an arbitrary caller-staged session. Staging is now private to the actual MFA owner and receives its exact locally created session.
- Final clock checks dirtied tracked state after persistence. Final checks are nonmutating; observations happen before writes. Detached PAR attachment explicitly marks the updated watermark for persistence.
- Late-created interaction expiry could exceed continuation retention. Both now share the earlier original expiry; completed retention follows the actual source session.
- PAR final checks lacked immutable provenance and exact transition checks. The original snapshot and expected state/revision/request identity are retained across native persistence.
- The schema needed unique binding indexes and completion needed an explicit account-derived audit with the same independent correlation. Both are included.

Provider controls exercise real fresh password/native MFA, consumed-factor replay, missing marker/row, wrong browser/purpose and strict original expiry; actual denied audit INSERT rollback; inclusive capacity/cleanup and provider-observed admission contention. Additional clock and schema controls use actual write boundaries and native constraint errors. Synthetic occupancy/corruption fixtures never stand in for an authentication proof. Native PAR controls additionally require actual saved native-token observation before an owned commit race or denied audit INSERT, independently spent fresh assertions, a fresh persisted watermark read and exact original live response-start loss after commit. The response-loss case is in-process publication evidence, not new TLS or provider commit-acknowledgment-loss evidence. Permission-denial fixtures always restore through an independent bounded cleanup token, including ambiguous denial/cancellation, and preserve both primary and restoration failures. No automatic security retry, relaxed timeout, provider omission or inherited test removal is permitted.

## Initial verification checkpoint and remaining work

Local solution build passes with zero warnings/errors; all 1,291 Host controls pass with zero failures/skips. Native discovery preserves every previous 1,321 name/type/trait record and adds 76 cases (1,397 total). Required BrowserSessions counts rise from 40 to 78 per provider; ordinary Administrator counts are 633 PostgreSQL / 738 SQL Server, with browser-inclusive inventories 646 / 751. Persistence discovery preserves all 16 prior identities and adds the two standalone controls, requiring nine per provider. Quality, instruction and assurance checks pass without expanded allowances; all 345 assurance controls remain not assessed.

The local PostgreSQL attempt failed during `BootstrapHost.CreateAsync` at HostStart, before database test execution. It did not create a running database container; after the first failure, repeated harness startup was stopped and only this task's test/DCP processes were terminated. This is failed local harness evidence, not a passed provider test or a product SQL failure. No deadline or test oracle was relaxed. Full exact-head hosted provider/coverage/mutation acceptance remains pending. Generated migrations have not been applied to a production database; source review is not runtime evidence.

At this initial checkpoint, T006 remained unchecked pending its complete bounded runtime evidence. T005 browser generation and the T008/T009 product entry/source-session/claim policies remain dependent work; no cookie, endpoint, authorization or public claim is activated by this slice. A-009 keeps known Playwright/Firefox issues documented and deferred for internal work. PR #69, issue #70, six historical findings, 48 human rows, 345 not-assessed controls and activation/production/public-release gates remain open. A-004 stays expired; A-005/A-006 stay concluded.

## Initial hosted correction checkpoint

PR #76 head `0504a1f56a020be462cc5926274f75758fef26ac`, run `40036846174`, failed pre-execution Python controls: the precommit-cookie mutation still targeted the old commit call without its clock argument, and partition controls retained the previous Administrator/Persistence counts. The correction updates the exact mutation anchor while preserving its seeded missing-commit defect, marker and witness; independent partition expectations become 320 standard, 633/738 Administrator and 994/1,099 total ordinary cases. The foundation split requires nine Persistence cases per provider. No test identity, mutation, required provider, assertion strictness or deadline is removed. These failed jobs provide no native provider acceptance; the corrected head requires a fresh complete run.

The same run rejected two unnecessary test imports and a newly reported viewer candidate advisory. The imports were removed. The current advisory query adds Zod `GHSA-mrhx-c7xc-m3w5` (high; bundled candidate 4.4.3, upstream patched 4.6.6). Independent review confirmed affected parsing code in the exact pinned Scalar asset and unchanged, unmapped production documentation routes. The bounded [23 October source review](../../eng/contracts/vendor/configuration-review-2026-10-23.json) preserves this as an unresolved affected-component/activation blocker, not vulnerability clearance. Strict new/changed-advisory rejection, asset/configuration hashes and all prior findings remain intact. The failed hosted response bytes were not retained; fresh query evidence is explicitly separate.

Corrected local validation: all 219 Python controls pass; the complete static-analysis command (build, whitespace, unused imports, quality and seeded enforcement) passes. Exact renderer archives/configuration and the live advisory comparison pass with `activationBlocked=true`. Independent test review confirms every previous Administrator/Persistence identity and metadata record is preserved and no mutation oracle changed. These local results do not replace the required corrected-head hosted provider, mutation and coverage run.

## Historical upgrade fixture correction

Corrected head `6c326fa986ae6746f44f17337e492ac04efe03ad`, run `40037677959`, passes all 78 SQL Server BrowserSessions cases. The existing governance upgrade witness failed on both providers (PostgreSQL 60/61, SQL Server 165/166) because its deliberate downgrade to the Recovery-era schema still used the current whole-entity interaction query. PostgreSQL rejected absent `BrowserIntentDigest` with 42703; SQL Server rejected the same absent column. This occurs in historical snapshot acquisition, before the forward migration.

The historical witness now privately captures the same nine security surfaces and explicitly projects all 21 Recovery-era interaction fields. Independent comparison with the historical model confirms no old field is omitted. The current-schema `SecuritySnapshot` remains unchanged. A nonempty interaction precondition prevents vacuous preservation, and both current-schema endpoints require empty continuation storage and null markers so the fixture cannot silently discard B3 authority during DOWN. Existing data/hash, idempotent UP, full migration history and readiness assertions remain. Architecture review accepts the source correction; fresh both-provider runtime verification is required.

## Accepted T006 — 23 October 2026

PR [#76](https://github.com/mosesparadise/OpenIdentityAdmin/pull/76) was normally squash-merged after architecture, test, security and independent evidence review. The recorded initial failures above are retained; they are superseded by the corrected final run, not treated as passing attempts.

| Identity | Verified value |
| --- | --- |
| Reviewed source | `c430b200bf3bf307590f5e6f5ee4d014b45c9a72` |
| Source / tested merge / squash tree | `6ad49a1c8efb84671c3436185007bfab9391dc91` |
| Tested merge | `37ee3fa17271cf9624d60182bcf886c29d78e1ad` |
| Base | `9f062444d23cbf817905aff5d214ef51c48c294f` |
| Verified squash | `600874938afea432044c3731397ca390453b61ff` |
| Full CI | [40038741666](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40038741666), attempt 1; 69/69 jobs successful |
| Coverage / acceptance jobs | `120710001715` / `120710178685` |

The final source supplies the durable issuer continuation, actual fresh password/MFA completion and one-use native PAR attachment required by T006. Original deadlines, atomic audit, profile/account/admission ordering and both existing provider histories remain intact. The adapter remains explicit and dormant; source-session authorization/claims and product routes are not activated.

Independent review downloaded 16 retained ZIPs, matched every computed SHA256 to GitHub artifact metadata and recounted 14 raw TRX reports with strict counters and native result/definition/execution bindings:

| Raw evidence | PostgreSQL | SQL Server |
| --- | ---: | ---: |
| BrowserSessions | 78 passed | 78 passed |
| Historical governance failures suite | 61 passed | 166 passed |
| Persistence, including frozen upgrade | 9 passed | 9 passed |
| AppHost foundation / BFF sessions | 8 / 10 passed | 8 / 10 passed |

The unit artifact independently contains Host 1,291, AppHost 65, Architecture 5 and Core 25 passing cases. Required results contain no failures or skips. Both historical-governance upgrade controls and both frozen `38eb41e` provider upgrade controls passed. Every previous Administrator/Persistence discovery identity and metadata record is preserved.

All eight retained mutation records match the exact source and mutant hashes and expected witness inventories: per provider, seven critical, eight administrator, six governance and three administrator-protocol kills, totalling 48. Baseline/restored test sets match and pass; all records confirm source restoration. The raw merged coverage report was independently recomputed against all 15 unchanged floors (minimum 90% lines / 80% branches). All 52 expected collector inputs are present at the current head, and the strict hosted nonempty-input guard passed.

Evidence limits: the raw recount covers the 16 focal ZIPs, not every matrix artifact or all 52 input bundles. Mutation `evidence.json` files are retained sanitized records; raw mutant TRX files are not uploaded. Frozen producer receipts identify `38eb41e` and 77 binary hashes per provider; those binaries were not independently rehashed. Host result identities are unique, although two long parameterized display names truncate identically. These limits do not claim external certification or human acceptance.

The Zod viewer finding remains affected/unresolved with activation blocked, as recorded in the [bounded source review](../../eng/contracts/vendor/configuration-review-2026-10-23.json). A-009 browser deferrals, PR #69, issue #70, six historical findings, 48 human rows, 345 not-assessed controls and activation/production/public-release gates remain open. No expired diagnostic authority is revived.

Next: T005 durable browser coordination, login-intent generations and atomic ticket/audit ordering. T003/T004/T007 remain partial; T008/T009 and the remaining browser-session routes, cookie/CSRF, callback and logout work remain open. Do not repeat T006 or the accepted PR #71–#75 prerequisites.
