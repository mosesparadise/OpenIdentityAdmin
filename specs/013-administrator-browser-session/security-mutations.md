# Dedicated B3 security mutation controls

**Accepted internal delivery:** PR [#100](https://github.com/mosesparadise/OpenIdentityAdmin/pull/100) is squash-merged as `e228461750842987e6a9aa2be1122f9743ac94c0`. T021 is complete internally. The final record below supersedes dated candidate statements; rejected and pre-reconciliation evidence remains retained. No production or external gate closes.

## Candidate scope — 26 October 2026

T021 adds eight exact-source security mutants per provider through the existing isolated mutation verifier. PostgreSQL and SQL Server must each pass the same-source baseline, fail each seeded defect at its specific demonstrated-bypass marker, and pass the restored-source controls. Candidate source and compilation alone do not establish a mutation kill. T021 remains unchecked pending exact-head hosted evidence and independent review.

The existing issuer, administrator, governance and B2 protocol manifests/oracles remain unchanged: their 48 required kills and same-source controls are preserved. The new suite adds 16 required kills, for a proposed total of 64. No exception, timeout, unrelated assertion, empty selection, changed identity, skip, missing report or generic process failure can count as a kill.

## Exact decision witnesses

| ID / oracle class | Seeded defect | Required unsafe observation before marker | Mapping |
| --- | --- | --- | --- |
| `browser-intent-binding` / `BrowserIdentityMutationTests` | Omit expected signed login-intent equality | Genuine native callback authenticates an independently re-signed wrong intent after real code exchange/state consumption. Signature, nonce, audience and other claims stay unchanged; replay still denies. | FR-001–004/013; B3-AUTH-03 |
| `browser-source-deadline` / `BrowserIdentityMutationTests` | Omit original source-expiry `end > now` | Genuine native callback authenticates signed source-expiry equality while ordinary token expiry is still future and authentication time is earlier. This is a B3 native identity-boundary observation, not B1 session resurrection. | FR-003/004/013; B3-AUTH-03 |
| `browser-generation-binding` / `BrowserBindingMutationTests` | Omit persisted/protected generation equality | An authentic older proof authenticates with a genuinely current replacement ticket. The response must carry the exact current subject and minimum original proof/browser/ticket deadline, with no writes or cookies; restored current proof remains usable. | FR-005–008; B3-STATE-01/02, B3-WEB-01 |
| `browser-antiforgery-binding` / `BrowserBindingMutationTests` | Bypass native antiforgery additional-data equality | Original token plus actual newer browser cookie commits a second intent, generation and Start audit. A merely unexpected status is insufficient. | FR-001/007/008; B3-WEB-02 |
| `browser-local-revocation` / `BrowserLogoutMutationTests` | Omit exact ticket revocation | Real authenticated logout reports success while the exact original ticket remains unrevoked at its original revision, with no Revoke audit despite committed actor-bound Logout audit. Changed browser generation cannot mask it. | FR-008/009/011; B3-END-01 |
| `browser-issuer-source` / `IssuerLogoutMutationTests` | Omit cookie-source versus native hint equality | A second genuine issuer source obtains and submits its own confirmation for a different requested hint; actual wrong-source revocation and one audit must occur while the intended source remains live. | FR-010/011/013; B3-END-02 |
| `browser-return-once` / `BrowserLogoutMutationTests` | Omit required correlation phase | Replaying genuine original return cookies commits a second Return audit and revision while preserving original correlation deadlines. Issuer stays live; no remote attestation is inferred. | FR-009/010; B3-END-03 |
| `browser-logout-publication` / `BrowserLogoutMutationTests` | Omit original incoming proof publication bound | After a real remote commit and joined cleanup, an authentic pre-callback proof reaches expiry equality but a later live tombstone permits a positive cookie and issuer link. Both effects and commit/cleanup witnesses precede the marker. | FR-008–010/012; B3-END-01/03 |

Each fixed `CriticalDecision` selects one case per provider. Positive native controls use fresh real password/MFA and independently owned native roots; no fabricated principal, callback state, protocol response or pre-created authority replaces the genuine path. The response-rewrite seam intervenes only after actual issuance, independently verifies signatures, and permits exactly one changed field. Existing short-source-expiry behavior is preserved as a separate enum case.

## Tooling and execution boundaries

`eng/browser-session-mutations.json` permits eight unique substitutions in six exact production files. `scripts/verify_mutations.py` adds an explicit `browser-session` suite; it does not broaden existing suites or their source boundaries. The new allowlist regression uses otherwise-valid forbidden-file anchors and requires the boundary-specific rejection, preventing unrelated source-drift errors from passing the control.

The two new required `Browser session mutations` jobs use the existing native image preparation, trusted HTTPS, locked dependencies, 30-minute job limit, serial builds and unchanged verifier deadlines. Final acceptance includes this suite for full changes and accounts for its explicit skip on eligible docs-only changes. Per-mutant compilation, native discovery, exact marker/method/report identity, source hashes and final restoration remain mandatory. Only sanitized mutation receipts are uploaded.

Ordinary native discovery adds 16 cases while preserving all prior 1,877 portable records, for 1,893. BrowserSessions server groups become Foundations 160, Product 98 and Lifecycle 55 per provider (313 total); ordinary Administrator counts become PostgreSQL 868 and SQL Server 973. Browser stories remain Accounts 8, Security 5 and BrowserSessions 13. Existing browser/coverage partition topology and 60 coverage inputs remain unchanged. Expected full raw executions are 4,458; actual reports must establish that total. The proposed full workflow has 79 jobs; no evidence is inferred from inventory counts.

## Review and verification disposition

Independent architecture review passes the scoped design. Security/test review required two corrections: compare stale-generation status against the old proof's effective capped expiry, and ensure the forbidden-file regression fails specifically at the allowlist boundary. Both are implemented. Manifest anchors are unique; existing manifests/oracles retain their bytes. Final architecture, security and test source reviews pass. Actual hosted baseline/mutant/restored receipts remain required.

Initial local native build/discovery confirms all 16 additions. All 227 Python controls pass after updating independent partition/job expectations. Local isolated compilation passes the baseline, all eight substitutions and restored source with zero warnings/errors. Every touched production file is byte-for-byte restored and hashed. This is compile-only verification, with no database execution or kill claim. Actual native discovery passes all 16 single-case CriticalDecision filters, all six server-group filters and six unchanged browser-story filters. Formatting and the syntax-aware quality gate pass without new allowances. Prior formatting/build and independent-review defects remain documented here rather than being counted as passing evidence.

No production behavior, composition or authorization is activated. T022 handoff, remaining B3 integration/browser acceptance and T023/T024 coherent delivery stay separate. A-009 deferrals, six historical findings, 48 human rows, 345 not-assessed controls and all production/public-release gates remain open.

## First hosted checkpoint — 26 October 2026

PR [#100](https://github.com/mosesparadise/OpenIdentityAdmin/pull/100), source `0980ef21e43389383cee5653c6b422f39b72b979`, run [40301454568](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40301454568), establishes all **64 baseline passes, 64 specific mutation kills and 64 restored passes**, with zero skips across ten independently inspected sanitized receipts. The eight new controls pass on each provider. PostgreSQL receipt artifact `12640353921` ZIP SHA-256 is `9ec53e125ea7d046d2481add3a75799d3cc45ef43eb4a7fe750b25f34ef2e1ea`; SQL Server artifact `12641372429` ZIP SHA-256 is `04818b97b8b737ff4dc895c64af33fe4792a6ff91811b6eedd4738e84d5051c6`. Source/mutant hashes, exact oracle identities and tested-merge binding were verified. Raw mutant XML is not uploaded, so receipt review is not claimed as independent raw mutant XML inspection.

Ordinary scoped unit/server evidence passes 2,240 executions (1,614 unit and 313 BrowserSessions per provider), with zero skips/failures. Native discovery preserves all 1,877 previous portable records and adds exactly the 16 new cases. This does **not** establish whole-run acceptance: the inherited T020 browser source repeats six WebKit failures (the two initial presentation assertions and valid cross-site confirmation waiter on each provider). Its 52 browser cases yield 46 passes and six failures. Both rejected evidence and successful mutation controls are retained.

The corrected T020 parent is integrated through a normal feature-branch merge, preserving history. The resulting candidate still requires complete exact-head verification, independent browser/screenshot evidence and retargeting to accepted main before normal squash delivery. No mutation result excuses the inherited browser failures.

The superseding push cancelled the remaining SQL general and aggregate coverage jobs. Final disposition is **74 successful jobs, three failures and two cancellations**. All 67 raw TRX were retained: 4,458 executed, 4,452 passed, six failed, zero skipped; all prior 4,442 identities remain and the 16 added cases pass. All 60 raw coverage inputs were inspected, but no aggregate/floor acceptance is claimed. A passed raw SQL report does not convert its cancelled owning job into success. Tested merge `8630a9746e9e369c65bc6b572c13ad690ed1ff97` has tree `975a3e530e2cd88d61998e93c43518121c3aa999`; independent final receipt SHA-256 is `ac0dc3d6e79c4c110d31f0031b3b6fb1ab06f314d867cb79206af3e7682aff7f`.

## Corrected pre-reconciliation checkpoint — 26 October 2026

Source `edcb95c4c4f31999dffea3d9ee4433524f9ea95b`, tested merge `95022132727fb50c554da2ccb747da9ed2921f54`, tree `a696c8636173baf7f70492e7f8d5e975504523ea`, passes all **79 jobs** in [run 40303909611](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40303909611). Independent review verifies 67 raw TRX, **4,458 passed with zero failures/skips**, all prior identities preserved, 60 coverage inputs/15 unchanged floors and all **64 baseline/specific-kill/restored controls**. Aggregate artifact `12644000552` ZIP SHA-256 is `3bb0f54877a17e26c1ed9173b5e759af38c5c822ea0204afe763a68f90b44525`; final independent receipt SHA-256 is `53d5861554d25d4b4031771911d58be00fe46c60dbfa8f7daace0f9ca40de458`. All 128 fresh screenshot bytes/hashes/dimensions equal the visually accepted PR #99 set.

After PR #99's normal squash, PR #100 was retargeted from its feature parent to verified main `34aa033123cfd0a41e8d7a89b64913a0fb091151`. Git's overlapping inventory/status changes required a normal history merge. Five conflicts retain the already reviewed T021 inventories and status records; main's tree was independently proven identical to the incorporated PR #99 ancestor. Reconciliation head `d8ecaad18945068c85c89939778ff4e06b04645f` has the reviewed source and accepted main as parents, with **exactly the same tree** `a696c8636173baf7f70492e7f8d5e975504523ea`. No source/test content or main change is lost, and shared history is not rewritten. Architecture/security/test source review carries to that exact head after independent tree/ancestry verification.

The passing pre-reconciliation run is retained as its own checkpoint. It does not substitute for required final-head checks; [run 40308935517](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40308935517) binds tested merge `796fba0d7462579d98e5f2010de59fc27837eec6` to accepted main and the reconciled head. Final delivery remains pending until that run and independent evidence acceptance complete.

## Accepted internal delivery — 26 October 2026

| Binding | Verified value |
| --- | --- |
| Reviewed final head | `d8ecaad18945068c85c89939778ff4e06b04645f` |
| Accepted main base | `34aa033123cfd0a41e8d7a89b64913a0fb091151` |
| Tested merge | `796fba0d7462579d98e5f2010de59fc27837eec6` |
| Source/tested merge/squash tree | `a696c8636173baf7f70492e7f8d5e975504523ea` |
| Full run | [40308935517](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40308935517), attempt 1 |
| Normal main squash | `e228461750842987e6a9aa2be1122f9743ac94c0`, PR #100 |
| Independent final receipt SHA-256 | `3c90bf20c5c9c5294bdd03d241a91af4761474862761a07a80619e6f951adf8d` |

All **79 exact-head jobs pass**. Independent raw review verifies **67 TRX and 4,458 passing executions**, zero failures/skips, with every prior identity preserved. Scoped unit/server evidence passes 2,240 cases, including 1,614 unit cases and 313 BrowserSessions cases per provider. Native Administrator discovery remains 1,893 with all 16 exact new oracle selections. All 52 logout browser executions, 104 policy records, 216 layout records and twelve final issuer POST-302 witnesses pass. All 128 actual screenshot bytes, dimensions and hashes match the accepted PR #99 visual set; this rebinds the existing actual review rather than claiming a second manual accessibility assessment.

All **60 actual coverage inputs** are inspected and all **15 unchanged floors** pass. Aggregate artifact `12644489192` ZIP SHA-256 is `1317dfcf1e11d9d251bff2468646de73e2230adc6f4324fc1ed57c06bc188398`. All **64 baseline passes, 64 specific demonstrated-bypass kills and 64 restored passes** are verified across ten sanitized receipts, with zero skips and exact names/source hashes. The 48 prior controls remain intact. Review does not claim independent raw mutant XML, which is not uploaded.

| New suite artifact | ZIP SHA-256 |
| --- | --- |
| PostgreSQL `12644916042` | `36d8bed16c98956709417a1219d4bbb7d32b58751283564f584cabd2b2b0659c` |
| SQL Server `12645171026` | `651a1d13c1476d725eae89ef462a215a1e9471945156fcb079f212497e0c35d9` |

Architecture, security and test source reviews explicitly bind to the final reconciliation head. Independent hosted/raw review passes. Exact main/head, all checks, review/thread state and mergeability were reverified before the normal main-branch squash; the resulting main tree equals the accepted tested tree. The two earlier checkpoints remain preserved. T021 is complete internally, with no activation, full B3, historical finding, A-009 restoration, human or public-release closure.
