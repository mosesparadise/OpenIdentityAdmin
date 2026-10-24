# T005 browser coordination implementation

The user selected T005, T007 and T008, including required T003/T004 prerequisites, as the next implementation batch. This record covers the first item's dormant persistence boundary. Nothing here activates product routes or marks the three-item batch complete.

## Owned transitions

A new migration-seeded Sessions budget row serializes quota admission and browser writers through a portable EF UPDATE. A bounded connection uses the existing provider connection/lock policy; one five-second operation budget covers acquisition, writes and commit. No lock spans issuer HTTP. Every operation uses a fresh context and owns rollback/disposal. Missing seed, dirty context, overflow, cancellation, failed/unknown commit and unavailable storage fail closed without retries.

Browser contexts retain a canonical proof digest, monotonic generation, current logical ticket reference, observation and retention bounds. Initial creation requires generation zero; a missing later generation cannot be recreated. Explicit replacement advances generation, cancels old pending work and revokes a former ticket atomically. A browser proof alone supplies no authenticated actor. The optional authenticated ticket reference must come from the BFF's validated cookie; under ownership, the exact current protected row and its B1 lifetime are authenticated again before attributing actor provenance. An anonymous replacement can invalidate an orphan without attributing its stored subject to the caller.

Login intent follows pending → reserved → established or terminal denial/cancellation. Reservation occurs once before native HTTP, preserves the original 300-second deadline, and retains a separate server-generated reservation digest. That digest is correlation, **not proof that native state exists**. T009–T011 must carry it in protected native state and compare it after native validation/consumption. No automatic challenge or callback retry follows a reserved/ambiguous result.

Establishment receives a private snapshot of a prepared protected canonical B1 ticket. The codec authenticates the exact snapshot and its lifetime; the native callback owner remains responsible for actual cryptographic protocol validation and consumption. The coordinator stages it through the accepted `SessionTicketTransaction` in the same context/transaction as generation, intent and actor audit. Immutable intent/ticket/actor deadlines are rechecked after all writes. The existing participant performs the sole commit; transaction/connection disposal completes before success is returned. Production orchestration must release a raw handle/cookie only after that result. Existing B1 CAS and independent ticket cleanup remain unchanged; ticket references are logical rather than restrictive foreign keys.

Local logout atomically advances generation, revokes the ticket, cancels pending establishment and appends audit. A remote correlation requires validated current ticket provenance. Full remote capacity or an unavailable correlation leaves the local revoke committed and returns `RemoteReserved=false`; optional issuer assistance cannot block local sign-out. Remote-pending and returned-unverified are separate one-use states; neither proves issuer revocation or creates authentication. Later endpoint work must apply native antiforgery/exact-origin/generation validation before invoking this internal persistence boundary.

Audit stores a separate generated GUID correlation and finite operation, with an authenticated subject/source session only when validated. It never stores the browser proof digest. This local correlation is not yet linked to issuer T006 audit: the later authenticated native transport must establish that safe cross-service link without deriving it from secret proof material. No complete cross-service audit claim is made.

## Bounds and retention

Contexts and each operation category retain at most 10,000 rows; login/logout live operations each cap at 1,024. Replacement cancels the same browser's former live intent under the same lock, preserving at most one live login per context. Tombstones and unknown reservations count until safe cleanup. Expiry equality denies; effective observations are monotonic and clock rollback beyond 30 seconds denies. Operation expiry never slides. A reserved intent retains through its original expiry plus the possible 300-second native-state dependency and 30-second tolerance. T009–T011 must bound actual native creation to the original intent deadline; reservation does not prove native creation. Establishment extends retention through the immutable ticket absolute/source bound plus tolerance.

Cleanup shares writer ordering and deletes at most 100 children per category, then at most 100 unreferenced contexts. It retains live tickets and all later dependencies, keeps audit independently, and rolls back the batch on failure. Neither cleanup nor a restored database grants authority. Operational restore invalidation remains required before enablement.

## Independent design review

Architecture/test and security/API reviewers accept the existing transaction participant, singleton-first portable ordering, logical ticket reference, explicit anonymous versus authenticated actor, immutable copied ticket material, and separate native reservation obligation. Required controls include both provider race orders, real protected actor provenance, quota equality, cleanup retention, failed audit, cancellation and committed-but-unacknowledged outcomes. Final source review found no remaining production architecture/security defect. Test review corrections witness caller mutation, final actor expiry, exact provider/injected failures, both cleanup race orders, lost acknowledgement, duplicate winner effects and preservation of every retained quota row. Both-provider runtime acceptance is pending.

## Evidence

At the 24 October source checkpoint, Administrator, Persistence and Host builds pass with zero warnings/errors; Host passes 1,306/1,306 cases with zero skips, including 15 new policy controls. Four pure upgrade-witness controls pass. All 219 Python tooling controls and source-quality checks pass. Native discovery preserves every prior name/type/trait and contains 1,483 Administrator cases (121 BrowserSessions/provider) and 20 Persistence cases (10/provider). Coverage remains 52 inputs and 15 unchanged module floors.

The first local PostgreSQL attempt timed out during Aspire DCP HostStart, before database tests executed. The task-owned stalled process was stopped; this is not a provider pass. Hosted exact-source provider/migration and full-workflow acceptance remain pending. No T005 completion, merge or product browser claim is recorded yet. Preserve all existing B1/B2/T006 tests, historical findings, deferred browser issues and human/activation/public-release gates. T007 and T008 remain pending in the authorized batch.

## First hosted correction

PR #78 source `342a29e9d5281569b97df3952dd79511598d413d`, run `40044579611`, reached all 121 BrowserSessions cases on each provider: PostgreSQL and SQL Server each passed 120 with one corruption-fixture failure and zero skipped. The attempted byte-array element mutation did not independently establish durable corruption; the test now assigns copied damaged bytes, explicitly marks the property modified, and proves codec rejection from an independent fresh persisted read before testing actor denial. The denial expectations remain unchanged. Static formatting also identified an import-order defect, corrected without behavior changes. Corrected exact-head execution remains pending; the failed run is retained.

The same run passed all 42 contract-core C# cases but failed its current npm advisory gate: `fast-uri` 3.1.8 received GHSA-w74x-v6f9-j3xg, GHSA-xhq7-xmqj-769h, GHSA-wjr9-j795-jxcr and GHSA-333x-w4r6-6crp. The compatible development-tooling patch is pinned to 3.1.9; only that lock entry and reviewed lock hash change. Both tarball SHA-512 values match their lock integrity; BSD-3-Clause LICENSE bytes are identical (SHA-256 `b010b0dfdfdb23d7396e03b82cd4621fc9bb8f95d6b0aea70b9c24e12074c786`). Independent review checked scheme, host and relative-path normalization fixes, unchanged public types/CommonJS API/scripts, no new runtime dependencies or install lifecycle. Both AJV consumers already permit `^3.0.1`.

Patched local npm audit reports zero vulnerabilities; 34 compatibility tests, all four committed-document schema validations and TypeScript checking pass. The first local `npm run check` lacked freshly exported actual contract artifacts in the isolated correction worktree, so actual-host regeneration remains for hosted verification rather than being claimed locally. The viewer Zod advisory, incomplete vendor inventory and all release limitations remain open. No audit gate, case, floor or deadline is weakened.

## Accepted T005 — 24 October 2026

[PR #78](https://github.com/mosesparadise/OpenIdentityAdmin/pull/78) was normally squash-merged as `e6447e35fc0ec49edfe1acb897818924fd58105f` after all 69 exact-head jobs and independent architecture/test/security/raw-evidence reviews passed. [Run 40045654668](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40045654668), attempt 1, verifies source `17dd440d83cc4f1e1e61dce94eb7d12725c73d84`, base `af93dd54aa4b60ed07272e555e6a49fdcd671110`, tested merge `06d55e4508f813f2b8461660809a35342a69addd`. Source, tested merge and actual squash share tree `7aae37ab016084b0bba8d58eb0d8a835b30f7c41`. PR head, base, mergeability and review state were rechecked immediately before merge; no review restriction was bypassed.

Independent review downloaded 16 focal artifact ZIPs, matched every API SHA-256 and recounted 14 raw TRX reports. Both BrowserSessions lanes pass 121/121; Persistence passes 10/provider including the frozen upgrade; AppHost Foundation/BffSessions passes 8/10 per provider; historical Governance failure lanes pass 61 PostgreSQL and 166 SQL Server; unit Host/Core/Architecture/AppHost pass 1,306/25/5/65. All recounted cases pass without skips/failures and retain strict native bindings. All prior Administrator/Persistence names and metadata are preserved.

All 48 required behavioral mutation kills have passing baseline/restored controls, exact inventories and independently recomputed source/mutant hashes. All 52 required coverage artifact inputs belong to the current head and pass the hosted nonempty-input guard; the raw merged report independently satisfies all 15 unchanged module floors (90% lines/80% branches). Coverage job `120732597740` and acceptance job `120733670110` pass.

Review limits remain explicit: this is a focal 16-artifact/14-report recount, not raw unpacking of all 52 coverage inputs. Mutations retain sanitized JSON receipts, not raw mutant TRX; frozen producer `38eb41e` receipts bind 77 binary hashes but those binaries were not independently rehashed. One pair of truncated Host display names is disambiguated by unique native result/definition/execution IDs. This accepts T005 for internal development only. T007/T008 continue in PR #79; complete B3 integration, all historical findings, deferred browser work and human/activation/public-release gates remain open.

| Retained artifact | ID | SHA-256 |
| --- | --- | --- |
| foundation-administrator-SqlServer-browsersessions | `12541975002` | `95c856a3eaa80f11c2f8cfa3d26576bd5ac0115769805890bb2615c87c2ccfde` |
| critical-mutations-SqlServer | `12541791925` | `bbcb7722bcb269d8ca2f9dd016ca08c4db1b5a999dedc26b81d426024afe45f0` |
| governance-mutations-PostgreSql | `12541786142` | `4a5a1ca002ca0c59228329934b8ca8eb276011092157385f280bc46f76b52f00` |
| foundation-administrator-SqlServer-governance-failures | `12541786084` | `1da85935af76c5b76228001af1490cdf63f0a597432c3cc33eb309c8c5d9eaa9` |
| administrator-mutations-PostgreSql | `12541652256` | `93f3cd06510f9c918e8cb593e202e1143f4d3f84b340a9a060ccc46c37b7faab` |
| governance-mutations-SqlServer | `12541627171` | `c7ccad897c7e29c984fe0a28a40e926fd34e02e7b2146885accf5d0b3b6e345a` |
| administrator-protocol-mutations-PostgreSql | `12541621271` | `cda380504cd8f989b3fe06399717f7a73f72c122854b6ae2600c4317c2178644` |
| foundation-administrator-PostgreSql-browsersessions | `12541556698` | `0bb85d4479b41dff95229fb43d5998d4b4914776ef058ffed9b7f59b827d0e7b` |
| critical-mutations-PostgreSql | `12541547300` | `5191eaecf9e041559960dda7475609fb4c707374cd4724b1c5222bc91912a0ac` |
| foundation-unit | `12541336762` | `c9e557c7d6362c507d206ce8a25195db5626c4f09439ff99e5a3acaa72b4159d` |
| foundation-administrator-PostgreSql-governance-failures | `12541327509` | `19b7cc53aceca3422b1d4e0438be4146d78660fe8cbbd23121b71deb883d5ad2` |
| administrator-mutations-SqlServer | `12541162737` | `b7fc4f184887ba24eec3e71509cf1e55815d5f3eaaee6eaac77fa9f79e9906bf` |
| foundation-PostgreSql | `12540769235` | `bc5859166d35d26bd46aa13121f6969ec5e43427c8e0cc336778c56f3b5bb7dc` |
| administrator-protocol-mutations-SqlServer | `12540629189` | `718058bbff67ca1d472a059772a0788a458adb9c85f43553773197817d2df402` |
| foundation-coverage | `12541981743` | `32119f1f5a7de321847db68250210c3c3a377a5ca2ab9180ebfa166b81ab99c7` |
| foundation-SqlServer | `12541946390` | `dfe8e2762daffb8292bbabdb533ae667f8eea753258648588d01776f2d5175a7` |
