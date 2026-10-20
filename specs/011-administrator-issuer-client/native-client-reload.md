# Durable client reload and reached denial controls

20 October 2026. Continues the authorized T005–T009 batch and its coherent B2 dependencies, FR-006/009–011/013 and SC-001–003/005/006. Production client/browser activation remains disabled.

## Reached hosted evidence

[Run 39476698509](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39476698509) tests source `50d8bb7702beb72ebfd7c0ffc1a4886a539d2543`, tree `8c6eda4f762a61367a624b5ae643c7030a2d76f9`. Both admission partitions execute **58 cases: 53 pass, five fail, zero skips**. The actual positive native callback now succeeds, and both the awaited callback-mutation case and the provider-observed concurrent callback case pass. This advances beyond the previous date-representation failure; it is not complete B2 acceptance.

The raw failures are identical across providers:

- The signed-response test passes its first ordinary callback but its independent access-row count expects one and sees zero. It has not reached its re-sign or hostile-response controls.
- The interoperability case reaches early replay and sees 503 where the fixture expected 400.
- Each expiry case (equality and 301 seconds beyond expiry) sees 503 at an assertion shared by both deliveries. Raw evidence alone does not identify the failing iteration.
- The HTTPS case passes its positive baseline and untrusted-TLS control, then observes one fixture failure in the transport-fault loop. The retained result does not identify the selected fault.

The exact published ID-token regression also has independently verified original-fail/corrected-pass evidence, including matching source/test hashes and test identity. It proves that local causal defect, separately from hosted acceptance.

## Corrected observations

`NativeAccountPersistence.AccessTokensAsync` now selects the native persisted access-token **identifier**, matching `CreateTokenEntry`'s `Type = context.TokenType`; the old query used the protocol hint instead. Positive issuer output must still have an independently committed row increment. This does not change production token persistence.

Early replay is rejected by native `ValidateTokenEntry` before durable redemption, so the request has no committed write phase. The existing response boundary deliberately refuses the fixture's `Committed` outcome and suppresses staged output as 503. No client error-publication permit has been implemented. `NativeClientDenial.RequireReplay` now requires precisely this behavior: native `InvalidToken/ID2139`, 503, empty body/location/cookies, no authentication and no hidden fixture exception. Callers separately require unchanged sends and durable state. First consumed expiry and the lock-serialized concurrent loser still require 400. Production publication rules are unchanged; arbitrary denials are not accepted as successful proof.

`NativeLostResponse` previously asserted a successful status on every `StartAsync`, even after it had already aborted the connection. A boundary fallback attempting a sanitized 503 therefore raised a new assertion exception. A controlled local regression fails on that original implementation and passes after correction. The fixture now observes/aborts once and throws the same request-owned `IOException` on subsequent starts. The HTTPS host recognizes only that exact observed exception instance around the original hosting call. Other I/O, aggregate, native and scope-cleanup failures remain failures. The second pure control rejects a non-successful initial response as loss evidence and verifies exception-identity isolation. This proves the reentrant fixture defect; it does not retrospectively prove the exact hosted failure path.

## Persisted state across credential and service-root replacement

Four new provider cases exercise same-key reload and retained state-key replacement on PostgreSQL and SQL Server. They first complete an actual native baseline, then persist a pending callback. All original client consumers and imported certificates are disposed before a fresh root imports protected files and completes the pending exchange. The assertion registration remains unchanged.

Replacement settings are generated once. Separate roots omit the retained signing key and retained decryptor independently. Each must fail native cryptographic processing, publish only the suppressed response, send no PAR/token request, issue no access row and leave the pending state Valid. Restoring both retained roles must complete exactly one exchange and leave state Redeemed. A further capable fresh root must reject replay without resubmission. New state issued with replacement credentials must reject an old-only root before succeeding with replacement credentials.

These are **service-root recreation and credential reload tests**, not operating-system process-restart evidence or automatic production key rotation. The existing pure native-key test separately proves new issuance selects active keys even when native sorting prefers longer-lived retained material. Full process restart, cancellation/outage/restore and remaining B2 acceptance obligations stay open.

## Verification scope

Administrator discovery preserves all prior 1,137 UID/name/type/trait bindings and adds four cases, **1,141 total**. Required admission partitions grow to **60 each**; ordinary Administrator selections are **505 PostgreSQL / 610 SQL Server**, aggregate selections **862 / 967**. Host inventory adds two loss-fixture controls, **1,111 total**. Existing IDs, test deadlines and coverage floors are preserved.

Architecture and protocol/security source reviews pass within this scope. Corrected provider execution and all exact-head required gates remain pending. No local database or Docker experiment is part of this continuation. PR #66 remains draft/unmerged; none of T005–T009 is marked complete. All six historical findings, 48 human rows and activation/public-release blockers remain open. Firefox stays deferred only through internal B2 under A-007 and must return before B3.

Local verification passes **1,111/1,111 Host cases**, zero skips, Administrator build and all **206 Python checks**. Code-quality and instruction/register checks pass; the register still has 345 controls not assessed. Local passing tests do not replace hosted provider acceptance.
