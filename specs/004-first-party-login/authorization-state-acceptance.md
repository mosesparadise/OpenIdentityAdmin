# T032 — Current eligibility at authorization

Planned before implementation on `012-authorization-state`, following PR #14 squash `44a0e90`. Approved v0.2/A-001, LOGIN-005/009, US3 AC4, SC-001/003 and T011/014 govern this bounded item. T018 already proves after-code changes; this item proves the earlier authorization HTTP decision. Existing project/feature/composition structure remains unchanged.

## Matrix and oracles

Use genuine protected password login, trusted HTTPS, the actual Aspire issuer and native PostgreSQL/SQL Server. Commit restrictions in a separate service scope before sending the authorization request. Never replace authentication with a fabricated cookie or bypass native validation.

Cross eight representative restrictions (account disabled, changed security stamp, session revoked, session expired, client disabled, removed scope permission, scope disabled, changed resource) with four boundaries:

1. A new ordinary authorization using an established session.
2. A new authorization with original `prompt=none`.
3. A cached `prompt=none` request paused after its bound pending interaction is allocated, before consumption.
4. A cached fresh-login request paused after genuine password completion, before its authorization continuation.

Include unchanged eligible controls for all four boundaries and a completed-request/replacement-session subject-isolation control using the existing TemporaryPilotOwner fixture. Subject B must remain current and eligible, with an independently validated genuine login, while A's completion is attempted using B's actual session cookie and A's original correlation proof. Safe theory labels only; secrets and native identifiers remain in memory.

Account/session restrictions require a login form on a new ordinary request, `login_required` on new/pending silent requests, and `access_denied` for a completed request whose session is no longer eligible. Registration policy restrictions require `access_denied`; a removed native scope permission is rejected earlier with local HTTP 400 text `invalid_request`, error identity ID2051 and no Location or callback/state (native ValidateScopePermissions and AttachRedirectUri). Confirm this exact source-derived oracle in execution. Validate exact callback and original state before following any callback. Silent failures must never display a login form. No denial may return a code or tokens, create a native authorization/code/access/identity token, consume an existing interaction, or substitute another session/subject. Native request-cache token creation/processing is permitted and must not be confused with issuance.

Capture persisted issuance/audit/session/interaction evidence after the restriction commit, immediately before each tested boundary. Select the session actually presented, including the new session created by completed fresh login. Pause pending silent requests only after their original `prompt=none` request has allocated a bound Pending interaction; never overlay prompt on a cached request. A new ordinary invalid-session request may allocate a Pending interaction/budget, but cannot change existing interaction bindings or emit an authorization approval. Assert no new authorization approval or authenticated session and preserve existing completion binding. Inspect response bodies/headers for sensitive material without printing them. Prove success controls through exchange and independent issuer/audience/signature/nonce/subject validation. Keep case-owned revoked/expired sessions terminal. Restore shared account/application/scope restrictions with bounded independent cleanup; invalidate the shared fixture if restoration fails. Then complete a fresh genuine login/code/exchange, without retrying a denied exchange or reviving a revoked session as a claimed recovery mechanism.

## Execution and delivery

Use the existing serialized fixture pattern; no new runtime configuration or production package is planned. Preserve all required cases, request/rate/concurrency policies, TLS and the 660-second provider invocation deadline. Measure total duration; SQL acceptance runs in normal GitHub CI because its image redirect is denied locally. Browser cells and existing behavioral mutation controls remain mandatory. New defects require reproducible before-fix evidence and narrowly scoped correction with independent review.

- [x] AZ-A Resolve independent architecture, security and test plan reviews; confirm native error and fixture boundaries.
- [x] AZ-B Implement the both-provider HTTP matrix and genuine recovery controls; reproduce any defect before correcting production code.
- [x] AZ-C Complete independent source/evidence reviews, strict unit/provider/browser counts, static analysis, mutation controls and unchanged nine-module coverage floors.
- [ ] AZ-D Reconcile tasks, acceptance matrix, roadmap and current evidence; inspect exact final PR head/checks/reviews and normal squash-merge with a Conventional Commit title. Record final delivery on the PR.

Spec Kit prerequisites resolve feature 004; requirements checklist is 7/7 checked; no extension hooks configured. T021/full M01-D and qualified manual accessibility/password-manager/usability, M01-E/M01-F and external assurance/certification remain open. This increment neither assesses ASVS controls nor authorizes production deployment.

Design disposition: architecture/security/test reviewers confirmed the bounded oracles and fixture conditions. Native permission rejects locally; pending-silent staging must stop before consumption; the completed case must restrict its newly created session (no redundant completed warmup flow); distinct subject B stays eligible during A's denied continuation. These are source-review conclusions, not execution evidence.

## Initial local execution and fixture correction

The initial 37-case PostgreSQL cohort executed 35 passes and two failures in 53.421s. Denial assertions passed; redundant warmup exchanges exhausted the unchanged 600/minute policy late in the cohort, failing one recovery and a later setup with HTTP 429. This is a test-fixture request-budget failure, not accepted evidence or a product eligibility defect. Remove the redundant warmup code exchange: establish each session through the genuine protected form/completion, then test the authorization boundary. Keep all four independently validated eligible controls and every fresh independently validated recovery, all 37 cases, unchanged rate limits/deadlines and no wait/retry. The setup interaction remains completed/unconsumed in its isolated browser until teardown. Re-execute the corrected cohort and full provider matrix.

The corrected local PostgreSQL cohort passed all 37 cases with zero failures/skips in 53.964s. Subsequent review tightened existing native authorization field snapshots and fragment rejection; the full source matrix must verify those final assertions. No production eligibility defect was found and no production source changed. Architecture/security/test source reviews resolved diagnostic safety, actual protected-form rendering, exact B-cookie transplantation and native authorization snapshot findings. Full CI, final evidence and delivery remain pending.


## T032 verified source checkpoint

T032 source `8b8f12ac6095593b392738eb5592a3255ffe3a6d` passed all 17 checks and 1,017 ordinary cases with zero failures/skips in [run 38162030027](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38162030027). Final documentation-head verification and normal squash delivery are recorded on [PR #15](https://github.com/mosesparadise/OpenIdentityAdmin/pull/15); this source checkpoint does not claim its own future merge.

Source `8b8f12ac6095593b392738eb5592a3255ffe3a6d` and tested merge `e90740b124b1418313a16cb44a2a1f51c445f878` share tree `e9f148e7f780e3d198cba34f98e4a52807f01e31`.

The 37 new cases per provider cross eight representative committed restrictions with new ordinary, new silent, pending cached silent and completed fresh-login authorization. Four unchanged-state controls and a distinct current eligible subject's genuine cookie prove success and completed-subject isolation. Denials preserve existing issuance/session/interaction/audit state; ordinary reauthentication renders the actual protected form. Every restriction case restores shared metadata and completes a fresh independently validated login/exchange. Case-owned revoked/expired sessions remain terminal. No production code changed.

| Ordinary partition | Passing cases | Scope |
| --- | ---: | --- |
| Unit/architecture | 243 | Core25, Host204, Architecture5, AppHost9 |
| PostgreSQL | 300 | Persistence4, Aspire4, HTTP issuer292, including37 T032 |
| SQL Server | 300 | Same required partition |
| Chromium, Firefox, WebKit × both providers | 174 | 29 in each of six cells |
| **Total** | **1,017** | **Zero failures/skips/incomplete cases** |

Separate from that count: 60 focused WebKit observations; seven passing baseline, seven demonstrated behavioral mutation kills and seven restored passes per provider. All nine unchanged 90% line/80% branch coverage floors passed. AuthServer 96.0%/97.6%, Infrastructure 99.1%/100%, Migrator 98.2%/100%, AppHost 98.8%/100% line/branch; the other five modules 100%/100%. Issuer durations: PostgreSQL 445.764s; SQL Server 577.695s, both within the unchanged 660s deadline. Existing [pinned versions and Ubuntu/Linux execution scope](acceptance-matrix.md#t031-verified-source-checkpoint) remain unchanged; no Windows/WSL2 manual execution or performance SLA is claimed.

Architecture, security and test source/evidence reviews resolved all bounded findings; documentation reconciliation retains the manual/product gaps. AI review is not an independent security assessment or GitHub self-approval. The initial local fixture rate-budget failure and corrected execution are preserved in [the execution plan](authorization-state-acceptance.md). T021/full M01-D, qualified manual password-manager/screen-reader/accessibility and timed usability, M01-E/M01-F, Admin BFF/React and the wider R1 product remain open. A-001 retains actual certification as a public-release gate; all 345 ASVS controls remain pending/not_assessed.

AZ-D source reconciliation is complete; exact final documentation-head verification and actual squash delivery are recorded on PR #15. Its checkbox remains a delivery gate at this source checkpoint.

Local final source also passed the full static/formatting enforcement gate (including clean controls and seeded CA5350/IDE0055/IDE1006 rejection), all 243 unit/architecture cases, and all 300 required PostgreSQL cases. The local issuer partition executed 292 cases in 513.248s, zero failures/skips, under the unchanged 660s deadline. SQL Server and browser acceptance above use normal GitHub CI, without a local network-policy bypass.
