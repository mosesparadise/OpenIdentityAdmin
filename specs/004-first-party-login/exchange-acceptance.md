# T025 code-exchange acceptance plan

Recorded before implementation on `004-exchange-acceptance`, under approved v0.2/A-001 and feature004. Spec Kit prerequisites pass; requirements checklist7/7 checked; no extension hooks. PR #6 subsequently passed all seven exact-head gates at `bd4992c` in run38082495623 and was squash-merged as `5aa330c`; this branch is based on that squash before publication.

## Scope and boundary

Complete the T025 exchange-acceptance gaps traced to LOGIN-005/006/009, US3/AC1–4 and T017/018. Add seven cases per real provider: account deletion1, role addition1, revoked/deleted authorization2, unrelated-cookie positive/negative2 and lost-response recovery1. Existing real61-second code expiration, simultaneous redemption/replay and independent wrong issuer/audience/type/signature/nonce controls remain mandatory in the full suite. T026–T030 and full pilot/manual/external acceptance remain open.

Use genuine protected form/code journeys, exact flow-specific native code records and runtime-role Identity/OpenIddict managers. Capture native token IDs before authorization and identify the single newly created authorization-code entry afterward; never use a globally newest token. Inspection may use the test administrator connection; mutations use owning managers. Do not decrypt native codes or mint authentication tickets. No production change is planned; reproduce any discovered product defect before fixing it.

Design review disposition: architecture, test and security reviewers found no blocker. Implement their causal controls: require the exact code valid with at least ten seconds remaining immediately before exchange (before deletion when the native manager removes it); preserve subject/client/session/authentication-time bindings and unaffected stamp state; observe the actual B cookie on the token POST without logging it. Snapshot token IDs after intentional mutations because native authorization deletion cascades to linked tokens. Require the distinct injected-loss message plus one attempt and a marker set only after a real HTTP200 body is drained and disposed. An unrelated transport error cannot pass.

## Current state and cleanup

Delete the actual code subject through UserManager in an individually owned disposable Aspire environment. Verify current deletion, exact invalid_grant, absent response tokens and no new token rows. Dispose the whole environment instead of reconstructing a deleted shared identity.

For a committed role addition after code issuance, use a native temporary role, assert denial without new tokens, then remove it and restore exact original account scalar state/stamps in fresh bounded scopes. A fresh genuine login/exchange proves recovery. Revoke or delete the exact code-linked authorization through its native manager; prove later exchange rejects and a fresh authorization succeeds. Do not infer authorization-entry revocation from cookie/session revocation or claim immediate revocation at offline JWT validators.

## Unrelated browser identity

The pilot deliberately has one manifest-owned eligible account. Create a second synthetic account through UserManager, temporarily change the test database's manifest ownership solely to obtain a genuine protected login/callback/token-validation journey for browser B, and capture its actual application cookie. No product multi-user mode or forged cookie is introduced.

Issue code A before this setup. For the positive case restore manifest A before exchange: A remains eligible and the genuine B cookie must not change A's validated subject, nonce or original authentication time. For the negative case keep B eligible at the exchange boundary: A is now ineligible, and B's valid session cookie must not rescue code A. Restore the exact manifest in finally and remove the temporary user through fresh bounded manager scopes; failed cleanup invalidates the fixture. Record that the positive case's B cookie is genuinely issued but B is no longer manifest-eligible; this does not claim two simultaneously eligible pilot identities.

## Lost token response

Use a test-only delegating HTTP handler around the real trusted TLS backchannel. After the real issuer returns a successful token response, drain it to Stream.Null without parsing/exposing token material, dispose it and throw a fixed transport exception to the caller. This deterministic client-transport loss models successful issuance followed by failure to deliver tokens to application code; it is not a claim of physical TCP loss before headers.

Require exactly one transport attempt, native code redemption and exactly one access/ID entry linked to that authorization. One explicit adversarial replay verifies invalid_grant, no token response and native linked-entry revocation. This assertion is not an automatic recovery retry. Recover only through a new genuine authorization and independent token validation; the faulting transport's attempt count must remain one. No production response hooks, custom redemption SQL, token detachment or blanket retry.

## Verification and delivery

Place cohesive cases in `Features/Protocol/ExchangeAcceptanceTests.cs`, with a separate nonparallel issuer collection so expanded acceptance does not exhaust unrelated tests' unchanged rate budget. Keep transport and temporary-ownership helpers in Fixtures. Sensitive assertions use fixed messages; no codes, cookies, subjects, credentials or captured responses in diagnostics. Exact no-new-token/audit checks use fresh inspection scopes and account for native revocation semantics rather than promising rollback.

Independent architecture/test/security reviewers inspect this design and the final source. Run strict build/static/unit and actual PostgreSQL locally; SQL Server and both-provider Chromium remain required in normal CI. Expected counts become181 Issuer/provider and592 .NET cases, subject to final discovery evidence. Preserve all nine90/80 coverage floors, no skips/retries and existing execution bounds. Update tasks/roadmap/verification with actual commits/runs, distinguish source checkpoints from later corrections, and publish one Conventional Commit PR for separate review and squash authorization.

## Source review and initial execution

Architecture, test and security source reviews confirmed the seven-case design. Closed findings: select newly created authorization-code entries without mistaking cached-request records for codes; put CancellationToken last as enforced by CA1068; use fixed-message boolean assertions for unexpected Location, content type and error fields; narrow requirement-completion wording to the T025 gaps. The initial seven-case PostgreSQL cohort passed with zero skips in1m21s. The final diagnostic correction is included in the full regression run; full both-provider/browser/coverage acceptance is still pending. No product source changed.
