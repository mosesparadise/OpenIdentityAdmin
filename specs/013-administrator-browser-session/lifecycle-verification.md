# T015 browser session lifecycle verification

**Historical implementation candidate; superseded by [accepted internal delivery](#accepted-internal-delivery--25-october-2026) below.**

Base: PR #90 squash `2a7f2d617e2faf7de443c356565826272c5f8b0f`. Implementation candidate on `092-b3-lifecycle-verification`, continuing the [accepted local logout](local-logout.md#accepted-internal-delivery--25-october-2026). T015 remains unchecked until exact-source hosted verification and independent raw-evidence review pass. Production Programs, persistence models, migrations and product behavior are unchanged.

## Scope and decisive controls

FR-005/008/009/011/012; B3-STATE-01–04 and B3-OPS-01–03. These controls complete remaining lifecycle evidence around genuine password/MFA, native callback and canonical B1 sessions. They reuse the existing native driver, real Sessions runtime credentials, provider lock observer and bounded cleanup owners.

| Boundary | Actual control |
| --- | --- |
| Separate process and retained-key continuity | `BrowserProcessTests` establishes a genuine product session, then uses six distinct fully exited child processes for positive status, independently valid wrong-ring denial, actual native logout form, wrong-ring protected POST denial, retained-ring protected logout and old-cookie denial. Every child reimports retained files and builds the real `AddBrowserLogin`/`UseBrowserLogin` root; no live provider/key object crosses the process boundary. The parent requires complete process-group/stream termination before crediting a result. |
| Wrong-key mutation isolation | The incapable child receives the genuine native antiforgery token/cookies but returns 400, no cookies and zero outbound sends. A whole-store snapshot remains identical. The retained-key child then commits genuine actor-bound local logout. |
| Private driver and evidence hygiene | `BrowserProcessFramingTests` proves an accepted closed frame reaches the absent-key request control, while mixed URI, unknown/default operation, irrelevant/missing token, invalid time and unknown/missing protection reject at Input before readiness. Seeded cookie/redirect/unexpected-form markers prove failed output assertions omit private contents. |
| Selective restored-state resurrection | `BrowserRestoreTests` captures every mapped scalar/blob field of three genuine callback-established rows (context, established intent and canonical ticket). Actual protected logout first denies old cookies. Exact selective row restoration under writer ordering demonstrably makes those authentic cookies usable again. A second protected logout, explicit revocation of the actual fresh B3 source session, fixed-profile disablement, Platform token/grant revocation and Sessions native-state revocation are each independently read back. Old cookies and callback remain denied without another exchange. Historical audit rows remain. |
| Genuine callback orphan reclamation | `BrowserOrphanCleanupTests` delivers the genuine callback to a detached cookie jar, verifies the actual authenticated result, then independently executes B1 ticket cleanup and browser coordination cleanup. The B1 ticket survives one tick before idle expiry and is removed at equality. Coordination remains through the later actual ticket/native dependency at equality, then removes its child/context one tick afterward. Audit/native records remain byte-equivalent through nonsecret hashes; fresh reads prove owner tables empty and old cookie/callback denial. |
| Product cleanup/quota contention | `BrowserCleanupAdmissionTests` invokes a genuine native-CSRF protected start against eligible cleanup with 9,999 retained inert contexts plus one expired row. Both actual database lock orders are provider-observed. Cleanup-first admits one start. Start-first denies without cookie/intent/audit creation or eviction; only a separate explicit retry after cleanup succeeds. Every retained row's full-field hash remains unchanged. There is no synthetic authenticated session in the capacity seed. |

Existing accepted controls remain required and unchanged: `BrowserCallbackReplacementTests` and `BrowserLogoutRaceTests` cover both actual commit orders; `BrowserCallbackRaceTests` covers duplicate native consumption; `BrowserCallbackAuditTests` covers actual B1/browser audit INSERT denial after state consumption; `BrowserCallbackFailureTests`, `BrowserLogoutFailureTests` and recovery failure cases cover acknowledgement/disposal uncertainty and original expiry; `BrowserLogoutLifecycleTests` covers callback response nondelivery/late cookies and retained-file root reload. Coordination foundation tests retain all live/retained quotas, bounded child-first cleanup, rollback and acknowledgement-loss controls. This candidate does not present those already delivered implementations as new work.

## Harness boundaries and limitations

The child adds only a closed typed local-operation branch to the existing private native client protocol. Common native framing remains strict. B3 operations accept no arbitrary path, SQL, request body or destination; they require the fixed synthetic issuer/client and exact four protection settings. UTC ticks are preserved with a consistent millisecond field so a new process cannot accidentally observe before the original proof timestamp. Actual local response writes are capped at 8,192 bytes, cookie output at eight/16,384 bytes and the outer private frame retains its 65,536-byte bound. Any attempted outbound HTTP is counted and rejected before transport. Request completion, root and imported key disposal precede result publication and successful exit.

This is **local product middleware in actual child processes**, not socket/TLS/browser-engine evidence. Detached callback delivery is a controlled nondelivery/late-arrival oracle, not an observed transport disconnect. Restoration covers **three exact test rows**, not database backup restore, and deliberately retains audit history; existing native restoration controls cover consumed native state separately. Explicit test invalidation is not a production operator command. Runtime worker scheduling, operational restore instructions, actual browser/TLS, issuer-assisted logout and full B3 acceptance remain their separate tasks.

## Selection and verification

Actual native discovery contains 1,761 unique Administrator identities, preserving every prior 1,749 name and complete portable metadata, plus twelve cases (six per provider). BrowserSessions now contains 260/provider: unchanged Foundations 158 and Product 96 plus required Lifecycle 6 (five classes). All six actual filtered discoveries match complete discovery exactly and form disjoint exhaustive unions. Administrator ordinary totals become PostgreSQL 815 / SQL Server 920; Host remains 1,457 and unit remains 1,552.

Lifecycle gets one required job per provider so the expensive process/quota controls do not consume the existing Product group's finite budget (the first T017 SQL Product run took 536.763 seconds). The same 660-second invocation and 20-minute job limits remain. Required coverage inputs increase from 54 to 56; all 15 module floors remain 90% lines / 80% branches, with no exclusion or prior coverage input removed. Strict selector regressions include all three groups and reject missing/overlapping/substituted identities. The eight mutation suites and all prior oracles remain unchanged. Full expected workflow has 73 jobs.

Local checks: Administrator/driver build passes with zero warnings/errors; source quality passes without allowance growth. Actual child-process framing and seeded private-output controls pass 2/2, zero skips (3.921 seconds). All 226 Python regression checks pass. Native full/six-group discovery and complete metadata preservation pass. Full hosted PostgreSQL/SQL Server runtime, all exact-head checks and independent raw evidence are still required. The earlier Aspire startup timeout was not retried or credited as product evidence.

Independent architecture/test/security source reviews accepted the real process, selective restore and both cleanup-owner design. Review required a genuine wrong-ring logout POST and failure-output sanitization; both corrections are included with positive controls. No source-only review is represented as provider execution. API contracts and product production composition are unchanged.

T015 stays open pending delivery. T003/T004 remaining integration, T012/T016 browser/TLS, T018/T019 issuer-assisted logout, T020–T024 overall acceptance, A-009/T028, PR #69/issue #70, all six findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open.

## Rejected first full run and exact SDK correction — 25 October 2026

PR #91 source `1451d9f23372005fbfb9afc8f843f2c172fc8179`, tested merge `2e0b8910d3f1ceb1e6167f29f686736e5edc7d7f`, identical tree `35babe7ea28801bc924f1e33318e15b28b2f8087`: [run 40222380487](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40222380487), attempt 1, finished with 71 successful jobs and two failed jobs. All runtime, browser, contract, static and mutation jobs passed. Coverage setup (`121261265755`) failed before input validation, merge or floor measurement; final acceptance (`121261559859`) consequently failed. No merged coverage artifact exists and no coverage-floor verdict is claimed for this run.

The pinned `actions/setup-dotnet` at `a98b56852c35b8e3190ac28c8c2271da59106c68` converts `global.json`'s `latestPatch` policy into channel `10.0.4xx`. At the coverage job's later installation, the channel resolved to SDK `10.0.403`; that archive and preliminary LTS runtime `10.0.14` returned HTTP 409 after the action's built-in attempts. The installer's “Could not find” summary does not establish that those versions do not exist. This is provisioning failure, not a failed application test or coverage floor.

The bounded correction changes only SDK `rollForward` to `disable`, retaining version `10.0.401`, `allowPrerelease=false`, the MTP runner and all eleven existing `global-json-file` setup sites. Independent architecture/host reviews inspected the pinned action's actual source: `disable` leaves the exact SDK version intact and the installer uses `--version 10.0.401`. Local SDK resolution returns `10.0.401`; a bounded TLS-verified HEAD of that version's official archive returned 200. This does not prove a future complete download. The action still attempts a floating LTS runtime first; failure there is warning-only, while SDK failure remains fatal. No SDK version, package, runtime-policy override, endpoint, test, deadline, floor or exclusion is substituted.

Independent raw review of the rejected run verifies ten native TRX reports with 2,072 passes (1,552 unit + 260 BrowserSessions per provider), all prior identities preserved and zero skips. The new six-case Lifecycle groups took 71.944221 seconds on PostgreSQL and 88.336917 seconds on SQL Server. All 56 actual coverage input XML files were inspected, but cannot supply the absent aggregate floor verdict. Raw contracts pass 43 core + seven Chromium + seven WebKit and 37 Node cases. All eight actual mutation receipt ZIPs verify 48 kills, 48 baseline and 48 restored passes, unchanged oracle files and matching hashes; raw mutant TRX is not retained. These successful bounded results do not accept the failed whole run. A fresh complete corrected-head workflow and independent raw coverage review remain required.

## Accepted internal delivery — 25 October 2026

[PR #91](https://github.com/mosesparadise/OpenIdentityAdmin/pull/91) was normally squash-merged as `ae5c1187b44bb6df137479154ae004c5e043284d` after all 73 exact-head jobs and independent architecture, security, test, host and raw-evidence reviews passed. Exact head/base/check/review state and mergeability were reverified; no restriction was bypassed. The fetched squash tree matches both source and tested merge.

| Boundary | Verified identity |
| --- | --- |
| Reviewed source | `c14245e7f92e0757ad5f906acc3066165a907fdc` |
| Base | `2a7f2d617e2faf7de443c356565826272c5f8b0f` |
| Tested merge | `6acac777af0cce67d69faf92e32b26572008dee6` |
| Source, tested merge and squash tree | `0566c15ad40db809a8a38dd1b1ed32b491ccf557` |
| Actual squash | `ae5c1187b44bb6df137479154ae004c5e043284d` |
| Workflow / attempt | [40224436758](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40224436758) / 1 |
| Coverage / acceptance jobs | `121267229940` / `121267457936` |

Independent inspection of seven actual unit/provider ZIPs verifies ten raw TRX reports with **2,072 passing executions**: 1,552 unit (Host 1,457, Core 25, Architecture 5, AppHost 65), plus 260 BrowserSessions cases per provider (158 Foundations + 96 Product + six Lifecycle). Results, definitions, classes/methods, executions, entries and list bindings reconcile exactly, with zero failures, errors, aborts or skips. Every prior unit identity/multiplicity is preserved. The full Administrator catalogue preserves all 1,749 prior identities and complete portable metadata plus twelve, total 1,761. The six actual native filters form exact disjoint exhaustive groups; discovery JSON was inspected locally, while hosted ZIPs retain reports/coverage and checkout logs bind the tested merge. The corrected Lifecycle groups took 65.589248 seconds on PostgreSQL and 88.110702 seconds on SQL Server, below unchanged bounds.

Independent raw contract review passes 43 core, seven Chromium and seven WebKit cases plus 37 exact Node identities/counters. Both actual production exports, the dormant browser-session document and six export-control receipts match committed contracts. Original export syscall traces/intermediate control documents are not retained, and viewer screenshots were not independently visually re-reviewed.

All **56 required coverage XML inputs** were directly inspected with actual ZIP sizes/SHA-256 values bound to this final head/run. Independent recomputation of raw merged line hits and quantified branch counts passes all **15 unchanged 90% line / 80% branch floors**. Admin.Bff measures 2,698/2,866 lines (94.1382%) and 582/592 branches (98.3108%). No input, floor or exclusion was removed. All eight actual mutation ZIPs independently verify API bytes/hashes, source/tested-merge/provider bindings, unchanged manifests/verifier and all 24 oracle files, original and reconstructed mutant hashes, **48 kills, 48 baseline passes and 48 restored passes**, zero skips. These ZIPs retain sanitized receipts rather than raw mutant TRX; no raw mutant-report review is claimed.

The coverage job itself invoked the installer with `--version 10.0.401`, reported the SDK already installed, then validated all 56 inputs, merged them and passed all module floors and final acceptance. No hosted `dotnet --version` command is invented. The optional preliminary LTS runtime download still returned HTTP 409 as a warning; deterministic SDK selection resolves the observed blocking installation choice without claiming that remote download failure is fixed. The initial failed run remains rejected and preserved above. Local SDK resolution and corrected build pass with zero warnings/errors; source/test logic and all deadlines remain unchanged by the one-field SDK correction.

| Retained corrected artifact | ID | SHA-256 |
| --- | --- | --- |
| administrator-mutations-PostgreSql | `12611336125` | `0d27a5c5cbddcd5836f97e23b0d069fba1b6f8ba5b1e52b40cd9151120f001bd` |
| administrator-mutations-SqlServer | `12610688220` | `d1c7eac5328f86e806e3774628fbb5ca80016b99d2ce16b6a4180330af4622b8` |
| administrator-protocol-mutations-PostgreSql | `12611310471` | `c9a245009ef6c0ee13dd217178509eed085d01e274341d2dc7db3e51c4a32c97` |
| administrator-protocol-mutations-SqlServer | `12610408367` | `1aaff583577c5d291477e899b836090b5dfd58d0908deab4441d792ed6cc8c9e` |
| critical-mutations-PostgreSql | `12611480124` | `21515b194d77014201f40b81b7c50ee05612d8fdd6a16c0db32fcaa9e27008c1` |
| critical-mutations-SqlServer | `12611471497` | `fb9de8e5accf35fc5122f63623a23c4e2a2f967e7d97819a226708c1e9f2bc67` |
| foundation-administrator-PostgreSql-browsersessions-foundations | `12610473713` | `36cbf6ea23ffd07dc5533d436d092191c4493e954bbedf730590c80328cae0b2` |
| foundation-administrator-PostgreSql-browsersessions-lifecycle | `12610457827` | `2299edd435033efe6985ed8df65503e3be135096f24c239fcb61ac4e1c64c15f` |
| foundation-administrator-PostgreSql-browsersessions-product | `12611106398` | `bc911ad436cf915180804f75eba614aff9d1cfe90c7d81194516c46658430807` |
| foundation-administrator-SqlServer-browsersessions-foundations | `12609914319` | `85c483621958aecc97a7bba19354bf03eb380cb457b6a05cbf3eac4d5cf3b0a4` |
| foundation-administrator-SqlServer-browsersessions-lifecycle | `12611240486` | `e4a2cb2227b86cf15c2176a9f3c28cb267db296250eeee9ff8a521c72f4f40a0` |
| foundation-administrator-SqlServer-browsersessions-product | `12611425934` | `6b8df83a5e0ab71dd465377120c3947f4f4e1177f13859b2810a6df5d0606f7b` |
| foundation-contracts-chromium | `12611041588` | `e35c70e3bc3f8e8ab29bff27841faf3505b6b17bbc37d12cd4f92c0d7202ec05` |
| foundation-contracts-core | `12611011644` | `0fdd0a6e00d2fd57730ef31e98b2abcc8e636ed05065c2e15749b9549ecabb4c` |
| foundation-contracts-webkit | `12611380961` | `c16a10a7d9762ab6cf541dd311de6a9470cb9ad267dfa982aea6be651685b490` |
| foundation-coverage | `12611461867` | `74e5e8c8f2a4555dd16630e17eb33f5673b98f2dbfbda8af1448d5fda8748250` |
| foundation-unit | `12611335467` | `3af6847d32e391281f04a2c5b002e32699522bc38ea16c8ddef6c829f85fe098` |
| governance-mutations-PostgreSql | `12611375668` | `89cf8b1916130b1c64d7a127c506079e1abf692d703ab3b551c48dc819dedbd9` |
| governance-mutations-SqlServer | `12610667950` | `3a2aa23188849d07bf4a463e3c1809d51d103c67d534e9720be0163d7d113bb2` |

**T015 is complete for internal development.** T017's protected local logout was already delivered in PR #89. These accepted controls use genuine product authority in actual exited processes, selective exact-row restoration and both real cleanup owners; they do not claim database backup restoration, transport-disconnect testing, Kestrel/TLS/browser acceptance or production scheduling.

Next is **T018 protected issuer end-session composition and exact current source-session confirmation**, followed by T019 BFF continuation/return. Reuse the accepted native feasibility (PR #72), fixed B3 registration contract (PR #73) and audited upgrade (PR #74); do not repeat those delivered prerequisites. T003/T004 remain partial for later integrations. T012/T016/T020 actual browser/TLS and T021–T024 overall B3 acceptance remain open. No refresh, current API authority, React or product activation is complete.

A-009/T028 restoration, PR #69/issue #70, all six historical findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open. No expired diagnostic approval is revived.
