# T009 native browser request and source claims

24 October 2026. FR-002–004/013; B3-AUTH-02–04. Base is accepted main `8adf2d7c9eb6e12a73962e0ab913be28243bf7a2`. This source checkpoint implements dormant native B3 composition; hosted acceptance remains pending. T009 is not yet marked complete.

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

Independent architecture/API and security reviews accepted the integration after correcting the final-read freshness ordering and ordinary-state isolation. Test review required an actual signed wrong-intent callback, not merely direct calls to the validator, and corrected the native numeric type oracle: IdentityModel may expose an integral epoch as Integer32 or Integer64, while strings remain denied. Expired PAR fails during native validation before a trusted redirect is restored; the authorization oracle requires the corresponding safe JSON error.

Local Host execution passes 1,381/1,381 with zero failures/skips (13 new controls), covering actual native preparation/protection, deadline/parameter/state substitutions, explicit opt-in and signed identity/expected-state correspondence. Administrator compilation passes. Code-quality checks pass without new allowances. These statements do not substitute for provider execution.

The local PostgreSQL attempt failed in `BootstrapHost.CreateAsync` after `HostStart`, before the first protocol assertion, after 92.272 seconds. Remaining cases were cancelled and the task-owned harness processes stopped. No successful local provider execution is claimed, and unchanged startup deadlines were not relaxed. Hosted PostgreSQL and SQL Server lanes remain required.

New provider cases exercise genuine fresh password/unused MFA completion and its one-shot native cookie; exact private code provenance and ID-only source claims; old same-user cookie denial; wrong stored originating request; token-body injection; original intent equality; prepared code/ID/access mutation and changed durable correlation after initial admission with full attachment rollback; final-read shorter freshness expiry; actual native client positive and authentically re-signed wrong-intent callback, replay/send counts, and late challenge replacement before PAR. The freshness probe explicitly requires both prepared token expirations still beyond the injected freshness deadline. Fixtures preserve the old cookie, original test identities and real native handlers. The bridge replaces network I/O only; this is not trusted-TLS/browser-engine/accessibility evidence.

Actual discovery preserves all 1,527 prior Administrator identities and full portable metadata. Discovery adds 30 cases (15/provider), total 1,557: 726 PostgreSQL and 831 SQL Server including existing browser cases; BrowserSessions requires 158/provider and ordinary Administrator 713/818. Hosted acceptance remains pending. Existing 52 coverage inputs, 15 module floors, mutation targets, provider obligations and deadlines remain unchanged. No schema or dependency changes are introduced.

## Remaining work and external gates

Required before T009 acceptance: final source/test review, exact-source hosted provider/full workflow evidence and independent result reconciliation. Then continue T010 protected BFF start/resume and T011 product callback/B1 establishment. Remaining T003/T004 integrations, full B3, logout, operations, browser acceptance and production activation stay open.

A-009 continues the documented Firefox/Playwright deferrals for internal development only. PR #69, issue #70 and the viewer advisory GHSA-mrhx-c7xc-m3w5 remain open. All six historical findings, 48 human rows, 345 not-assessed assurance controls and human/activation/production/public-release gates remain open. No expired diagnostic authority is revived.
