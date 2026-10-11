# T024 login and interaction acceptance plan

Current delivery: T024 is squash-merged in [PR #6](https://github.com/mosesparadise/OpenIdentityAdmin/pull/6) and reverified at the PR #12 checkpoint. The plan/checkpoint statements below are historical; use the [current acceptance matrix](acceptance-matrix.md) for remaining obligations.

Status: T024 verified by [run38073502300](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38073502300), all578 .NET cases and seven gates passed. This plan was recorded before implementation on `004-interaction-acceptance`, under approved feature004 and T024. Spec Kit prerequisites pass; requirements checklist7/7 checked; no extension hooks. PR #5 was squash-merged as `d4664a3f0222ffda2b0d4ae01d8e6aaedb551b92` after exact-head CI and review with zero open findings.

Scope: LOGIN-003/004/005, US2/AC1–3 and T011–015. Complete password-entry eligibility, native five-attempt lockout, original cached-request binding, delayed positive max_age, independent tabs and completion/consumption race evidence. Existing freshness, capacity, forged-form and replaced-session tests remain in the required both-provider matrix. T025–T030 remain separate obligations; this increment does not accept the full pilot or authorize its next PR merge.

## HTTP acceptance

Use the actual Aspire issuer and protected Razor forms, synthetic accounts and native Identity managers. Render the form before committing eligibility changes; then submit the correct password. Cover disabled/locked/lockout-disabled/MFA/role-bearing and unowned accounts, generic denial, no password echo, no new authenticated session or completion and a fresh successful control after restoration. Restore exact shared account state in bounded fresh scopes; invalidate the fixture on cleanup failure.

Start lockout from zero native failures. Verify attempts1–4 increment the failure count, attempt5 sets a future lockout (Identity resets its count), and the correct password remains denied during lockout. Expire the lockout through a test-only persisted-state update and prove genuine form login succeeds; no five-minute sleep or product clock endpoint.

Exercise missing/wrong correlation cookies using browser B's own valid antiforgery token with browser A's interaction. Prove B's same token can complete B's own valid interaction, isolating correlation binding from antiforgery rejection.

Resume cached requests with hostile overlays for callback, scope, state, nonce, proof and freshness fields. Inspect redirects before following them; never follow an unapproved destination or print codes/query strings. Require original cached values for OAuth overlays; missing/mismatched outer client_id must receive native invalid_request. Test prompt/max_age overlays before password completion with an existing older session, so freshness bypass would be observable. Successful flows validate original state/nonce/proof and token claims through the independent test RP.

Age a genuinely completed session beyond positive max_age before authorization resumes; require login_required, no consumption approval or code. Retain fresh positive and max_age=0 completion controls. Two authenticated tabs must keep distinct interactions and each complete its own validated flow in sequence; replaying an already consumed continuation cannot authorize again. Also exercise direct authorization resumption after another tab replaced its completed session.

## Deterministic persisted-state races

Pair existing concurrent real HTTP completion with narrower database-transition tests. Seed interactions and sessions through genuine HTTP journeys. Invoke production LoginInteractions in separate runtime DI scopes against the real provider; reuse production pilot service registration and test-owned issuer credentials, not copied password/lockout policy or administrator connections.

A test-only DbCommandInterceptor waits immediately before the conditional LoginInteraction UPDATE, recognizes completion/consumption by the updated column, and requires two distinct contexts. Link an explicit deadline even if a caller supplies CancellationToken.None. Release before actual database execution, await both real operations and restore/dispose only after draining them. No production test hooks or security-operation retries.

Cover two completions, two consumptions (pending-session reuse and completed interaction), and completion versus consumption of the same pending interaction. Verify one valid terminal transition, coherent session/interaction ownership and exact audit/session deltas, with no losing writes. Add a single-arrival cancellation control if a new barrier is introduced. Component transition results do not claim simultaneous HTTP/native-code issuance; HTTP tests independently prove transport and cookie boundaries.

## Evidence and review

Write tests first and reproduce any runtime defect before changing production behavior. Use fixed-message boolean assertions for captured HTML, credentials, identifiers, cookies and native token material. Run strict unit/static checks, both real providers, Chromium and unchanged per-module90/80 coverage gates; update exact required counts only for added cases. Independent architecture/test/security reviews inspect source and evidence. Record executed commands/results and the PR #5 final490-case checkpoint in verification.md. Mark T024 complete only when its final cases pass on both providers.


## Review disposition and discovered capacity gap

Independent architecture, test and security reviewers accepted the paired HTTP/component strategy and identified the valid-antiforgery binding control, exact native overlay outcomes, flow-specific session oracle and callback-race precautions now specified above. No new authentication bypass was confirmed by inspection; reviewers executed no tests.

Security review identified a medium LOGIN-004 capacity gap: CreateAsync counts each request's presented cookies. Two starts that each present seven active interactions can both allocate another; the existing eight-interaction test proves sequential admission and expiration recovery, not atomic concurrent allocation. T030 tracks this separately before full M01-D acceptance. The approved maximum is not silently relaxed, and this task makes no atomic browser-capacity claim.

## Implemented review corrections

The tests use the native authorization error format (`text/plain` with an exact `error:invalid_request` line); a denied callback has no code parameter. Initial execution exposed incorrect JSON/mandatory-code assumptions in the new tests, which were corrected without changing product behavior.

Architecture review required a pre-resumption authentication-time snapshot; HTTP completion now records the exact session ID and timestamp before continuing, and reuse retains that snapshot. Token validation also rejects a changed persisted timestamp. The automatic browser journey retains its existing interaction-specific fallback; no pre-resumption browser snapshot is claimed. Pending component consumption is seeded by an anonymous ordinary request followed by a separate genuine login.

Test review required the lone barrier cancellation control to call the transition with CancellationToken.None, proving that the interceptor's own deadline works independently. A deterministic completed-session mismatch denial followed by genuine matching-session HTTP success complements the mixed race's nondeterministic winner. Native cleanup uses the production connection/command bounds. Security/test review led to full prior-audit preservation, immutable request/predecessor-field checks and independently derived replacement targets.

Current added matrix:44 cases per provider (password entry7, request binding31, database transition6), raising Issuer130→174 and the full expected .NET total490→578. The final both-provider578-case pass and bounded review dispositions are recorded in [verification](verification.md#t024-completed-ci-checkpoint).

Final bounded source re-review: architecture, test and security reviewers closed the findings above. Pending consumption retains an earlier ordinary request; completion cases render their form after authentication so antiforgery identity binding remains valid. Reviewers executed no tests.

Execution-driven correction: the44-case cohort uses its own nonparallel disposable Aspire fixture to avoid unrelated tests exhausting the production600/minute limiter. All real issuer limits remain active. Captured concurrent responses allow one code plus either an exact access_denied callback or native400 invalid_token (depending on whether the loser passed native validation before redemption); an explicit later replay must receive native400 invalid_token. No retry or broadened success condition is introduced.
