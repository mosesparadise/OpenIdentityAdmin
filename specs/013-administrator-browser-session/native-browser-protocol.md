# T009 native browser request and source claims

24 October 2026. FR-002–004/013; B3-AUTH-02–04. Base is accepted main `8adf2d7c9eb6e12a73962e0ab913be28243bf7a2`. T009 is delivered for internal development in PR #81, squash `75b143104f65cdfdc5413ba55635dbe13e5ee64a`. The accepted evidence below supersedes the dated pending source checkpoints; complete B3 and activation remain open.

## Issuer ownership and claims

Explicit `AddBrowserSessionProtocol` composes the delivered completion/PAR owner with browser authorization and exchange. Existing `AddBrowserCompletionPar` retains its prerequisite authorization denial. Production startup remains unchanged. Ordinary unbound B2 continues without browser claims, including under the browser registration.

Original form cardinality, canonical native scalar values and exact pair agreement precede attachment. Token exchange rejects even empty/aliased browser transport fields: only the protected code supplies its original binding. At authorization, `BrowserIssuanceAuthority` compares the consumed native PAR ID and protected pair to the terminal attached continuation, exact currently admitted user/session and original persisted session expiry. A previous fresh session for the same user is insufficient. A separate private originating-PAR ID survives code creation, whose native ID is different.

The attached row is read without adding an admission lock after existing governance/profile/account ownership. Completion retention extends past source-session expiry and attachment is terminal; no legal writer modifies a live attached row. Initial and final detached reads compare the entire immutable provenance, original intent expiry, revision, native request, completion digest and current source claims. No observation watermark is written. The final browser read occurs before the final live source-admission read; synchronous browser/claim/deadline checks follow it. This ordering prevents the additional database await from escaping a shorter requested freshness bound.

Authorization code binding remains private. Only bound exchange adds `admin_session_expires_at` as a numeric original source deadline and `admin_login_intent` as the canonical decoded-byte intent digest, with ID-token destinations. Final checks inspect the actual prepared code, ID and access principals, detecting changed private provenance, wrong ID claims or access-token leakage. Prepared code/token expirations are clamped to original intent expiry as well as unchanged B2 ceilings. The source-expiry claim is not shortened to token expiry. Current-account serialization, native early consumption, quota ownership, audit/commit/output suppression and no retries remain in their existing owners.

## Native client integration

`NativeClientProfile.ForBrowserSessions` explicitly selects the exact browser registration. `NativeBrowserChallenge.Attach` captures canonical transport plus the original intent deadline in one native challenge; it is representation, not a durable reservation receipt. T010 must obtain that reservation before calling it. The existing B2 parameter guard allows the pair only with this captured attempt, checks it before persistence and around PAR, and rejects replacement/partial/aliased input.

After native state preparation and before durable generation, only the expected intent digest is added and state expiry is clamped to the original deadline. Raw intent/completion selectors do not become protected-state claims. Ordinary challenges cannot inject a B3 expectation without a capture. Existing durable-state and continuation fingerprints bind the resulting representation.

`NativeBrowserIdentity` runs after native authentication completion and its consumed-state receipt, before merged-principal publication. It requires the exact protected intent together with the native-validated signed B3 identity, reusing the delivered strict raw-shape/native-principal validator. Unrequested browser claims and missing/malformed protected expectations deny. Product canonical ticket creation, browser-generation acceptance, cookies and routes remain T010/T011 and later tasks.

## Verification and review

The following source and failed-run checkpoints precede acceptance. Pending statements here are superseded by the accepted T009 record below; their failures and limits remain retained.

Independent architecture/API and security reviews accepted the integration after correcting the final-read freshness ordering and ordinary-state isolation. Test review required an actual signed wrong-intent callback, not merely direct calls to the validator, and corrected the native numeric type oracle: IdentityModel may expose an integral epoch as Integer32 or Integer64, while strings remain denied. Expired PAR fails during native validation before a trusted redirect is restored; the authorization oracle requires the exact safe local text error, content type and absent redirect/cookie.

Local Host execution passes 1,381/1,381 with zero failures/skips (13 new controls), covering actual native preparation/protection, deadline/parameter/state substitutions, explicit opt-in and signed identity/expected-state correspondence. Administrator compilation passes. Code-quality checks pass without new allowances. These statements do not substitute for provider execution.

The local PostgreSQL attempt failed in `BootstrapHost.CreateAsync` after `HostStart`, before the first protocol assertion, after 92.272 seconds. Remaining cases were cancelled and the task-owned harness processes stopped. No successful local provider execution is claimed, and unchanged startup deadlines were not relaxed. Hosted PostgreSQL and SQL Server lanes remain required.

New provider cases exercise genuine fresh password/unused MFA completion and its one-shot native cookie; exact private code provenance and ID-only source claims; old same-user cookie denial; wrong stored originating request; token-body injection; original intent equality; prepared code/ID/access mutation and changed durable correlation after initial admission with full attachment rollback; final-read shorter freshness expiry; actual native client positive and authentically re-signed wrong-intent callback, replay/send counts, and late challenge replacement before PAR. The freshness probe explicitly requires both prepared token expirations still beyond the injected freshness deadline. Fixtures preserve the old cookie, original test identities and real native handlers. The bridge replaces network I/O only; this is not trusted-TLS/browser-engine/accessibility evidence.

Actual discovery preserves all 1,527 prior Administrator identities and full portable metadata. Discovery adds 30 cases (15/provider), total 1,557: 726 PostgreSQL and 831 SQL Server including existing browser cases; BrowserSessions requires 158/provider and ordinary Administrator 713/818. Hosted acceptance remains pending. Existing 52 coverage inputs, 15 module floors, mutation targets, provider obligations and deadlines remain unchanged. No schema or dependency changes are introduced.

The initial published run `40091629775` at `ef946538841a64e5e69c2245bbcb4812912c2299` was superseded/cancelled after adding the final stored-provenance regression; it supplies no acceptance claim. Run `40091896339` at `4c09a751d0c7c28da753a75383c9c58f77e42183` passes 156/158 BrowserSessions cases on each provider, with the same two deadline-fixture failures. Authorization equality returns the safe native local text error, whereas the initial oracle expected JSON. Exchange setup starts code creation at intent expiry minus one second, then the existing `NativeAccountClockAdvance` advances one second during authorization and correctly prevents code creation before the intended exchange intervention. The correction prepares the code two seconds before expiry, asserts the resulting clock is one second before expiry and independently decrypts the code to require the exact original intent deadline, then advances to equality for exchange. No runtime deadline, native handler or denial requirement is relaxed. Both raw provider reports independently verify all other 156 cases passing with zero skips; corrected hosted execution remains required.

## Accepted T009 — 24 October 2026

[PR #81](https://github.com/mosesparadise/OpenIdentityAdmin/pull/81) was normally squash-merged after all 69 exact-head jobs and independent architecture/API, security and test source/evidence reviews passed. The exact head, base, mergeability and review state were verified before merge; no restriction was bypassed. Remote main was then verified at the actual squash with the same tested tree.

| Boundary | Verified identity |
| --- | --- |
| Reviewed source | `114cd57eebda4cb9e77cb9218ef6067da693b758` |
| Base | `8adf2d7c9eb6e12a73962e0ab913be28243bf7a2` |
| Tested merge | `94f6424fdba9b48c8bd3bd90d40369185371d35a` |
| Source, tested merge and squash tree | `3d2d87e6e295fa9cc76f4b58cfc6908a16f4ca08` |
| Actual squash | `75b143104f65cdfdc5413ba55635dbe13e5ee64a` |
| Workflow / attempt | [40092816157](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40092816157) / 1 |
| Coverage / acceptance jobs | `120872919968` / `120873047520` |

Independent raw review verifies six TRX reports and 1,792 passing executions with zero failures, skips or aborted cases: BrowserSessions 158/provider, Host 1,381, Core 25, Architecture 5 and AppHost 65. Exact names, result/definition/execution identifiers and source bindings match. All 15 new provider cases on each database and all 13 new Host controls pass, including both corrected expiry branches. Every prior Administrator identity and portable metadata is preserved: 1,527 existing plus 30 new equals 1,557. PostgreSQL and SQL Server remain mandatory; no migration, provider implementation or dependency changes were needed.

All 52 expected coverage inputs match this run/head and pass the hosted nonempty-input guard. Independent recomputation of the raw merged Cobertura report passes all 15 unchanged 90% line/80% branch floors. All eight mutation bundles match their API SHA-256 values; sanitized receipts independently verify all 48 kills, 48 passing baseline controls and 48 passing restored controls. Original and mutant source hashes recompute from the approved manifests. Local build, focused Host execution, source quality, formatting and tooling checks support this hosted evidence.

Independent review scope is explicit: six raw TRX reports, raw merged coverage and eight sanitized mutation receipt bundles were inspected. The 52 individual coverage XML inputs were not independently unpacked, and raw mutant TRX reports were not inspected. In-process native provider controls do not establish product browser/TLS/accessibility acceptance. Production composition remains dormant.

The failed run `40091896339` ended with 60 successful, three failed and six cancelled jobs after the correction superseded it. Its failures are both BrowserSessions jobs and aggregate acceptance; coverage was cancelled. Its 156/158 provider passes and successful mutation evidence are retained, without substituting for final-head acceptance. The local Aspire startup failure and earlier cancelled run remain historical limitations described above.

| Retained final artifact | ID | SHA-256 |
| --- | --- | --- |
| foundation-administrator-PostgreSql-browsersessions | `12560101182` | `e3dbb88013a154456b4f0ffba1eb1a4b3c18887b7a7548e60322db9057198868` |
| foundation-administrator-SqlServer-browsersessions | `12559921964` | `b872957cddbe821ae8a0678c0af3eeb9ab59ef97a046af34aab24fc1b0e01598` |
| foundation-unit | `12559727639` | `93e08b439ef56201661fa434f924368acfccf996283949a5f913a6b7c03ebc5e` |
| administrator-protocol-mutations-PostgreSql | `12559369604` | `defa4539294f3076bceadca77d12543291e8c745021b250ae8f94002adbcee02` |
| governance-mutations-PostgreSql | `12560275422` | `1dee11bf3b9fcc4b2f1e366114f4aed1da68c1fa1caa8a2e4e37b3212216f9c3` |
| administrator-protocol-mutations-SqlServer | `12560081513` | `2fdd78edd65048676fe3f2981f991db68ef7db06eeab91d78c03f4609a6e70f3` |
| critical-mutations-PostgreSql | `12559892269` | `c7816756b459b1fe2ea32f63c94978dfd7f6cb00765e791a6acd5374fdb6e1ac` |
| governance-mutations-SqlServer | `12559598965` | `a5c25bb03e6f915ebedcb63e69c2e47ad85eca83709ae85d0110814102aba140` |
| administrator-mutations-PostgreSql | `12559548981` | `ac2f8fe7badf1a38207484a263d1a66b688e06be20357a0e36efbb66aed25c6f` |
| critical-mutations-SqlServer | `12560355301` | `f08b2cfb831d8f23078cb531e7caf0191c75b0aa7b31543b52934545ff294582` |
| administrator-mutations-SqlServer | `12559544260` | `7c6dd3641588f15627bc3ccd9a625da7bdbb3e3dde7a1e56351febc5c044e671` |
| foundation-coverage | `12560640057` | `f31f45e7542a33b517108302084cacedc6686b5c5696b6acbff0b0cdb959a25a` |

## Remaining work and external gates

T009 source/test review, exact-source hosted verification and independent evidence reconciliation are complete. Next is T010 protected BFF start/resume, followed by T011 product callback/B1 establishment. Remaining T003/T004 integrations, full B3, logout, operations, browser acceptance and production activation stay open.

A-009 continues the documented Firefox/Playwright deferrals for internal development only. PR #69, issue #70 and the viewer advisory GHSA-mrhx-c7xc-m3w5 remain open. All six historical findings, 48 human rows, 345 not-assessed assurance controls and human/activation/production/public-release gates remain open. No expired diagnostic authority is revived.
