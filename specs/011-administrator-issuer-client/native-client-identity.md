# Native identity-token dates and signed response controls

20 October 2026. Continues the authorized T005–T009 batch and coherent B2 dependencies, FR-005–011/013 and SC-002/003/005/006. The client remains dormant in production. This is implementation and verification evidence, not completed B2 acceptance.

## Confirmed representation defect

[Run 39474224515](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39474224515) tests source `b41d84299aeea1ffd5177468041aef8e02cc9507`, tree `eb46ccfb4c62c54405c0a4e7d8afa7dc7046f058`. The completed run has **59 successful jobs, five failures and one skipped coverage aggregate**. Both admission jobs execute 57 cases: 51 pass, six fail, zero skips. The finite observer reports `InvalidToken/unknown` at every new positive callback baseline. That category alone does not locate a failing handler; source inspection and the controlled regression below establish a specific defect independently. The callbacks fail before lifetime, mutation and HTTPS negative scenarios can receive acceptance credit.

Pinned OpenIddict 7.7.1 `ValidateIdentityModelToken` strips private `oi_` claims from non-state tokens. `MapInternalClaims` restores private creation/expiration only for native state. Actual validated ID tokens retain public `iat` and `exp`. `NativeClientTokenTime.Current` incorrectly read private dates and rejected otherwise valid native ID output. Earlier `NativeClientTimeTests` fabricated private identity dates and therefore missed this mismatch.

`NativeClientIdentityRoundTripTests` signs a synthetic identity token, invokes actual native parameter resolution, cryptographic validation and internal mapping, confirms absent private dates and present public dates, then calls the strict guard. With the original guard this same regression executes **one case and fails** at the time check after successful native validation. The corrected guard executes **one case and passes**, zero skips. Architecture and security reviewers independently confirm the native source behavior and representation boundary. The pure test uses a fixture signing key explicitly trusted by its configuration; it does not replace real issuer/client provider acceptance.

The guard now requires exactly one public integral numeric claim for each date. It counts raw claims, so identical duplicates also deny. Textual/fractional types, parse failure, integer overflow and timestamps outside the representable Unix range fail closed without throwing. There is no private-date fallback. Existing conditions remain: expiry strictly later than now and issuance, issuance at most 30 seconds ahead, and lifetime at most 300 seconds. Native audience/nonce/well-known-claim checks remain installed. State continues to use its separate native private-date representation.

Twelve malformed-date cases cover both `iat` and `exp`; missing and expired public dates cannot be rescued by injected private dates. Existing before/equal/after-expiry cases now model the actual identity representation without changing their scenario identities or expected outcomes.

## Authentic signed hostile responses

Two new provider cases first require an ordinary real native exchange, then an **unchanged re-sign positive control**, followed by fresh flows for wrong nonce, issuer, audience, JOSE type and expiry equality. The test-only bridge intervenes only after real successful issuer `/connect/token` processing. It never changes production handlers, database grants or client trust.

`NativeIdentityResponse` independently verifies the original and replacement RS256 signatures with the actual disposable issuer public certificate. It preserves the original key identifier and all other header/payload fields, and proves precisely the intended structural difference. Every outer response field except `id_token`, including the actual access token, remains unchanged. The expiry intervention advances the fixture clock to the next whole second so `exp == now` while `exp > iat`; it does not accidentally test reversed date ordering.

Each flow requires one actual token send and a separately observed committed issuer access-token increment. Negative callbacks must fail native authentication; fresh Sessions reads require state Redeemed. Replay from the other client instance must deny without another token send. The unchanged re-sign control prevents a broken transformation/signature from falsely satisfying all negative cases. These cases remain unaccepted until hosted execution reaches and passes them; in-process response mutation is not additional TLS proof.

## SQL quota-race preparation and observation

The preceding run still fails the SQL foundation state-quota race with winner cancellation during its deliberate lock hold. Preserving the primary failure removed cleanup masking but did not establish the timing cause. The second independent client root previously first entered native generation while the winner held a real transaction lock. Both roots now complete actual native generation and validation before fixture rows are cleared and the 1,023-row quota setup begins. This is test preparation, not a demonstrated production-timeout fix or a retry.

The peer-entry and blocked-observation waits now monitor unexpected winner completion too. A completed winner stops observation and preserves its original failure. Fixed phase/task-status diagnostics distinguish winner-lock entry, peer-lock entry, blocked-peer observation and release/join; they emit no exception messages or protocol data. Every path still releases and joins the observer and both real operations. Existing production and observation deadlines remain unchanged. Two Host controls verify early successful/failing winner detection.

## Verification and remaining acceptance

Final corrected local source passes **1,109/1,109 Host cases**, zero skips; the preceding 1,107-case source remains separately retained. Administrator and Persistence builds pass and all **206 Python checks** pass. Actual Administrator discovery preserves every prior **1,135** UID/type/trait/name and adds two signed-response cases: **1,137 total**, ordinary PostgreSQL **503**, SQL Server **608**, admission **58 each**, aggregate selection **860 / 965**. Fifteen Host additions comprise the native representation regression, twelve malformed-date controls and two early-winner controls. No existing test identity is removed.

The preceding run also fails the existing SQL Server WebKit untrusted-certificate case: the strict native observer sees a canceled exact request without a certificate-specific marker. Its 30-case suite has 29 passes and one failure; this is not accepted TLS evidence. The source correction here changes no browser trust or certificate acceptance rule and launches no retired diagnostic. The failed browser evidence remains open alongside the three protocol/foundation jobs and final acceptance. Both-provider execution, full exact-head CI/coverage and remaining B2 negative/race/operations/mutation evidence are still required. No local SQL or Docker experiment ran.

PR #66 remains draft/unmerged. None of T005–T009 is checked complete. All six historical findings, 48 human rows and production/public-release blockers remain open. Firefox remains deferred only through internal B2 under A-007 and must be restored before B3 implementation/activation/release.
