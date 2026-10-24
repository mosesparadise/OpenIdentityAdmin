# T011 product callback and canonical browser ticket

Status: accepted for internal development on 24 October 2026 through PR #85, squash `490779b80282ea436d3ec3587f072fae973db682`. The source checkpoints below retain development failures and supporting evidence; final acceptance is recorded at the end. Production Programs remain dormant.

## Product ownership

Explicit Generic Host composition routes only the registered callback into the existing native HTTP/storage response owner. It keeps one original ten-second request budget, finite bodyless HTTPS query framing, no browser bearer authority, no-store and no-referrer. Actual named OpenIddict request handling, redirection passthrough and authentication retain native parsing, signature, issuer/audience/nonce/correlation/code validation and early durable state consumption.

After native state consumption and before token transport, a fresh local scope reads the purpose-isolated original generation and pending cookies. Both must agree with the current durable browser and exact reserved intent, fixed client, correlation, revision and original timestamps. The private state must contain exactly the expected intent/browser/generation/reservation tuple with exact scalar types and no destinations or target-link claim. Missing product admission is checked again by the existing native token-send receipt; it cannot silently downgrade to B2. Invalid product cookies still leave the validated native state spent and send no token request.

The final product capture runs after existing native completion and B3 identity validation, before native merged-principal projection. It freezes the exact HTTP/native context, protected expected tuple, validated backchannel token strings, original identity-principal reference and full claims fingerprint. Final checks reject later mutation, including changes only to the native principal's expiry. No merged principal, arbitrary properties, roles, permission snapshot or refresh capability is copied.

After actual native work completes, a separate local scope creates a fresh opaque handle and uses existing `BrowserSessionIdentity`, B1 policy/codec and T005 `BrowserLoginCompletion.EstablishAsync`. The ticket, winning browser generation, intent state, B1 audit and authenticated browser audit commit atomically. The canonical ticket contains only the named B1 identity's `sub` and `sid`, with no OAuth token properties: there is no current token consumer. Original issuer source expiry is retained; no new eight-hour allowance or ID-token expiry substitutes for it. The BFF uses only Sessions credentials and changes no schema/provider/history.

Only acknowledged commit and successful local disposal release a candidate handle. The response stages the secure host-only session cookie, a same-generation proof bounded by ticket policy, prelogin deletion and fixed `/bff/login` redirect. The native root then disposes and all tracked cleanup drains before actual output. Original pending/browser bounds, native state/ID-token bounds, complete captured identity and effective B1 expiry are checked through final publication. The longer new generation proof never replaces the original callback-admission deadline.

Native redemption/exchange and local ticket establishment are separate commits. Audit rollback produces no ticket/cookie; lost local commit acknowledgement or later cleanup failure can leave an orphan but publishes no cookie or success. No automatic exchange retry, callback recovery, compensating recreation or fresh challenge is performed. A later deliberate protected start can replace the durable generation. Late cookie arrival cannot defeat existing durable generation and ticket revocation checks.

The existing T010 Generic Host composition restriction remains: automatic minimal-host authentication integration belongs to T023. Remaining status/UI/contract integration, browser engines/TLS/accessibility, logout and full B3 acceptance remain pending. This slice enables none of the production Programs.

## Traceability and verification

Requirements: FR-003–005/008/011/013; B3-AUTH-03/04 and B3-STATE-01–03. Existing T003 native validation/consumption and T005 atomic coordination are reused, not reimplemented.

| Scenario family | Boundary verified by the new controls |
| --- | --- |
| `BrowserCallbackTests` | Genuine fresh password/MFA, native authorization, independent product callback root, canonical committed ticket/source provenance/audits, exact cookie policy and independent-root replay |
| `BrowserCallbackAdmissionTests` | Missing/duplicate product cookies, equal-generation foreign reserved browser, deliberate replacement, removed admission/final capture; actual native state spent with no usable session |
| `BrowserCallbackErrorTests` | Actual issuer error for an older same-account cookie, native error-state consumption, no exchange or replay |
| `BrowserCallbackMutationTests` | Late public issued/expiry mutation after successful capture, with unchanged native-time and B3 identity guards still passing |
| `BrowserCallbackAuditTests` | Actual provider INSERT denial separately for browser and B1 audits, exact denial codes, local-only full rollback snapshot, spent native state and restored new-sign-in control |
| `BrowserCallbackFailureTests` | Actual establishment commit acknowledgement loss, exact local/native context disposal faults, original combined deadline and independently shortened signed-source expiry during final disposal |
| `BrowserCallbackRaceTests` | Independent duplicate callbacks with an actual provider-observed blocked native-state contender; one exchange/ticket |
| `BrowserCallbackReplacementTests` | Both callback/replacement commit orders with actual provider-observed browser-budget blocking; prevent establishment or revoke the earlier committed ticket |
| Host framing and tuple tests | Exact callback framing, malformed/duplicate/aliased/typed/destined protected tuple, no target-link substitution |

Native discovery preserves all 1,605 prior Administrator identities and their complete portable metadata. It adds 40 cases (20/provider): 1,645 total, PostgreSQL 770 and SQL Server 875 including unchanged browser cases. BrowserSessions requires 202/provider; ordinary Administrator totals are 757/862. No existing selector, provider, deadline, coverage floor or mutation obligation is removed.

Local supporting evidence: both Administrator and Host builds pass with zero warnings/errors; all 16 focused new Host cases and all 1,426 full Host cases pass with zero skips. All 219 Python controls and the unchanged source-quality gate pass. Exact-head hosted provider execution, coverage, mutation evidence and final independent evidence review remain pending. The earlier local Aspire pre-test startup limitation is preserved in T009 evidence; no unchanged startup diagnostic loop was repeated. Python image-preparation output comes from mocked tooling controls, not a new SQL experiment.

Initial local compile/format corrections are retained as development failures, not acceptance evidence: missing test import, duplicate test constant, formatting, and a source-quality invocation without the configured SDK PATH. Independent source review corrected the initially mutable native identity-principal lifetime and strengthened pre-token durable provenance. No check or source-quality allowance was weakened.

First hosted run `40123962627`, source `985fc967fb11b3999ff27408dfae33adc9e80111`, is failed development evidence. PostgreSQL BrowserSessions exposed 14 callback failures before exchange: native JSON restoration turns small generation values into `Integer32`, while the initial callback check incorrectly demanded `Integer64`. The callback now accepts only those two native integer representations, with the same exact positive expected value, cardinality, name and destination checks. The pre-serialization challenge still requires `Integer64`. A local parser regression failed all seven controls before this fix; the permanent native signing/encryption/validation regression covers 1, `int.MaxValue`, `int.MaxValue + 1` and `long.MaxValue`, and denies textual numbers. All 17 focused callback Host cases pass; the Host inventory is now 1,427.

That first run also caught an unnecessary import and the new B3-only source-expiry test option leaking into the ordinary B2 identity-fault enumeration. The import is removed and B2 retains all its original fault cases; B3 owns its separate source-expiry test. Focused independent architecture, security and test reviews accept these corrections. They do not establish corrected-source provider acceptance. An invalid local wildcard invocation ran no tests and was corrected before the 17-case pass; the quality gate caught an unnamed test lifetime literal, which was named without an allowance.

Second hosted run `40124845376`, source `8a7dd387127e6d01cf591d54d118539c04d040bb`, passed static analysis, all 1,522 unit cases (Host 1,427), and both 110-case protocol-admission lanes. Independent raw unit-artifact review verified the SHA-256, all result/definition/execution bindings and preservation of all prior 1,505 unit cases. PostgreSQL and SQL Server BrowserSessions each passed 201/202: only the shortened-source disposal case failed on each provider. Its precomputed deadline preceded the actual signed source expiry by one second because the existing issuer token helper advances the clock during issuance. The probe now captures the actual native-validated source deadline, advances to exact equality after native context disposal, and requires the committed ticket and fixture clock to match that deadline while the original pending intent is still current. Production guards are unchanged; this is a corrected test boundary, not accepted provider evidence until its new-source execution passes.

## Evidence limits and review

In-process HTTP over real provider stores is not browser-engine, trusted-TLS, theme or accessibility evidence. Full T012/T015/T016 acceptance remains separate. The fixture retains the issuer cookie produced by the actual new password/MFA ceremony; its enrollment cookie is used only for the explicit error control. Replacement-only direct B1 setup from T010 is not used as callback proof.

The shorter-source fixture re-signs exactly one source-expiry claim after successful real issuer issuance. It independently checks signatures and unchanged other fields, then requires native callback acceptance and committed ticket state before disposal crosses that deadline. It proves retention/publication of a native-validated supplied deadline, not a shortened production issuer-session lifecycle. Production issuer policy still fixes the original eight-hour source session. The pending-expiry fixture also reaches the native state's clamped expiry; it proves the combined original deadline without claiming which individual guard rejected output.

Independent architecture/API review passes the production bridge. Final scoped security source review passes after the principal mutation correction and decisive public issued/expiry regression refinement; executed provider evidence remains pending. Independent test review corrections include actual issuer-error coverage, local rollback snapshots that exclude separately consumed native rows, stronger ticket/audit/cookie oracles and explicit race ordering. Final scoped test source review passes. Final evidence dispositions will be recorded after execution.

All six historical findings, 48 human rows, 345 not-assessed controls, production/public-release gates, A-009/T028 browser restoration, PR #69/issue #70 and the viewer advisory remain open. No expired diagnostic approval is revived.

## Accepted T011 — 24 October 2026

[PR #85](https://github.com/mosesparadise/OpenIdentityAdmin/pull/85) was normally squash-merged after all 69 exact-head checks and independent architecture/API, security, test, mutation and coverage reviews passed. Exact head, base, mergeability and review state were checked before merge; no restriction was bypassed. Remote main and the actual squash tree match the reviewed/tested source.

| Boundary | Verified identity |
| --- | --- |
| Reviewed source | `14593d965e089b7c0b87ebb57f02fa6838e79e64` |
| Base | `6cc9670cd3c54c70dad21521bd3edb590cb5f1a5` |
| Tested merge | `768ffe78f9b4e57114d18616b62a208290d21bb6` |
| Source, tested merge and squash tree | `cea595d0c172242dc458e3fc27bb371f7360d5f3` |
| Actual squash | `490779b80282ea436d3ec3587f072fae973db682` |
| Workflow / attempt | [40125924536](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/40125924536) / 1 |
| Coverage / acceptance jobs | `120972197915` / `120972334943` |

Independent raw artifact review verifies six TRX reports and 1,926 passing executions, with zero failures, skips or aborts: BrowserSessions 202/provider, Host 1,427, Core 25, Architecture 5 and AppHost 65. Exact test names, definitions, entries, methods and execution identifiers match. All prior 182 BrowserSessions cases per provider and 1,410 Host cases are preserved; the 40 added provider cases and 17 added Host cases pass. All 1,605 prior Administrator identities and portable metadata remain unchanged, with 40 additions producing 1,645. Both providers' unchanged 110-case protocol-admission lanes also pass. Actual checkout logs for both callback lanes, unit and coverage bind the tested merge above.

All 52 required coverage inputs match the final run/head and their API IDs, sizes and SHA-256 values match the hosted download/merge-input log. The unchanged nonempty-input guard passes. Independent recomputation of raw merged Cobertura passes all 15 unchanged 90% line/80% branch floors. Admin.Bff measures 2,504/2,670 lines (93.78%) and 625/634 quantified branches (98.58%). All eight actual mutation ZIPs match their API SHA-256; independently inspected sanitized receipts verify 48 kills, 48 passing baseline controls and 48 passing restored controls, with exact manifests and recomputed original/mutant source hashes. No source-quality allowance, coverage floor, deadline or required provider case was relaxed.

Review limits remain explicit: the six raw TRX reports, merged coverage XML and eight sanitized mutation receipt bundles were inspected. Individual input XML files and raw mutant TRX reports were not independently unpacked. Coverage branch rates follow quantified condition fractions; unquantified branch markers are not additional measured branches. In-process real-provider controls do not establish trusted TLS, browser engines, theme/accessibility or full B3 acceptance. The two earlier source runs remain rejected development evidence; the final corrected source passed every required check on attempt 1 without reruns or skipped required cases.

| Retained final artifact | ID | SHA-256 |
| --- | --- | --- |
| foundation-unit | `12573656598` | `bf6249cf737daddf46334be41cc3bd69ec97285d53bbda384936454b28337a6b` |
| foundation-administrator-PostgreSql-browsersessions | `12573422329` | `f52074fc4127ccd451dadfc15e391c4db85f30e299e7734f4cc0f67f655696f1` |
| foundation-administrator-SqlServer-browsersessions | `12573138662` | `1314211a12b337b97f207953098ce3118ebe92dc18274ce14a24a372356f471b` |
| administrator-mutations-PostgreSql | `12573722532` | `2a44e84c9a2c9bf3afbee12df850bbebb56712d839a82013e89c20c61b041b1d` |
| administrator-mutations-SqlServer | `12573623200` | `090170344120e87880ca2d3ee2d79af65132655fb366344df032a7a83cbd74cc` |
| administrator-protocol-mutations-PostgreSql | `12572837332` | `c2610699f6fa9149a4a131b267621c19f1158e059d411c7ed271809be27ef2a3` |
| administrator-protocol-mutations-SqlServer | `12573162458` | `da65fc6502c19c9007d06fadb0b0407b5a26aaea187282ca73b27883755b2a53` |
| critical-mutations-PostgreSql | `12573712248` | `5cb350ad681fed70e340f39e00381aa8e61fd6ccde3e713460b38c568aa621e9` |
| critical-mutations-SqlServer | `12573477782` | `7f0906e7aa867fd0526ba0812c03e852bdd7895a0efd65364c72fc6361e08a8f` |
| governance-mutations-PostgreSql | `12573757010` | `9ec6c3f474f3134d8c7663b9db7757073bec965e9ddfd0483d60946a7a7eada4` |
| governance-mutations-SqlServer | `12573717358` | `95aa912c15ae75ae26b13b55e3b2c1b37dc5dec82d1f660497cb946d4de66115` |
| foundation-coverage | `12572968752` | `1e04bbf0285f040fc289dc03d894de458c3a77b52950b434753824f6a213dbd2` |

T011 is complete for internal development. The next bounded T012 slice carries remaining product journeys through actual callback/ticket outcomes: a second deliberate sign-in with genuine existing B1/issuer cookies, account/source substitution, pilot/recovery isolation, expired PAR/native state and one supported committed authority invalidation. Reuse the accepted native mechanisms and staged fixture helpers rather than repeating the full B2 race matrix. T012 remains open for trusted-TLS Chromium/WebKit and deferred Firefox evidence; A-009 is not a blanket browser/TLS waiver. T013 then supplies the asynchronous current-generation session-status adapter, no-renewal reads and safe entry/error views.

Production composition/full B3, all six historical findings, 48 human rows, 345 not-assessed controls, public-release gates, A-009/T028 browser restoration, PR #69/issue #70 and the viewer advisory remain open. No expired diagnostic approval is revived.
