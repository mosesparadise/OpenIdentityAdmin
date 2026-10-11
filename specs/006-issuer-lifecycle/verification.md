# Issuer lifecycle verification — restart in progress

The user confirmed the dedicated lifecycle test project and three sequential PRs on 11 October 2026. PR #17 implements restart only; it is not accepted until both-provider evidence, required checks and source/evidence reviews pass. Distribution, rotation, T021 human assessment, M01-F and public-release gates remain open.

Spec Kit template resolution, setup-plan/setup-tasks and prerequisites ran. No extension hooks exist. The requirements checklist and independent design review are complete. The prior accepted checkpoint is PR #16 squash `559380e952059383741a7bc75c259b5bb39ed1e1`; its 13 checks/901 ordinary cases do not establish lifecycle behavior.

## Implementation and review in progress

The new native xUnit v3/MTP project reuses explicit fixture source links, with 13 intended restart cases per provider. Existing AppHost harness tests add process-generation and accumulated-cleanup controls. Each provider receives its own <=660-second lifecycle invocation with child-process coverage. The initial combined SQL job exceeded 20 minutes; the corrected design uses separate standard/lifecycle jobs, each retaining that same limit. Both new lifecycle coverage artifacts are mandatory alongside the original inputs (eleven for the full browser matrix). Corrected scheduling acceptance remains pending.

Independent architecture/security/test source reviews identified and corrected: cancellation that could abandon stopped callbacks; replay snapshots taken too late; missing session-inventory and completed one-shot generation checks; unauthenticated rather than authenticated wrong-client controls; generic denials masking native binding failures; broad continuation/wrapper failure oracles; incomplete JWKS private-field guards; contradictory TRX counters; and missing lifecycle catalogue/manifest checks. Architecture/security/test source reviews passed after corrections. Executed final-provider evidence review remains pending.

## Experiments

- Initial compilation exposed missing Aspire testing namespace and formatting/static-analysis violations; fixed without disabling rules.
- First PostgreSQL run failed: Aspire reports a deliberately stopped .NET project as `Finished`, while the harness waited only for `Exited`. The fixture was invalidated, remaining cases failed closed, and teardown attempted all resources. MTP also refused duplicate test/cleanup result identities. This is failed evidence, not a pass. Terminal handling now accepts explicit `Finished`/`Exited` while independently requiring OS process exit.
- Corrected PostgreSQL experiment at the initial implementation checkpoint: 13/13 passed, zero skipped, 309.106s module duration (309.545s outer run). This precedes the final descendant/health-freshness/accumulation changes and does not accept them.
- Additional review/inspection identified Aspire's `dotnet` launcher versus the actual AuthServer child. The adapter now tracks both using Linux PID/start-tick identities, rejects a surviving orphan child, recognizes zombie/dead states, and waits within bounded termination budgets. A private locked recorder prevents concurrent observers from losing captured generations; the unit control retains all 64 concurrent additions.
- A malformed synthetic `/proc` fixture was rejected by the new parser unit test; its missing field was corrected. The strengthened current source subsequently built with zero warnings/errors and passed all 245 unit-profile cases (Core 25, Host 204, Architecture 5, AppHost 11); current provider reruns remain pending.
- Initial full CI source checkpoint passed all six browser cells, unit, dependency/license/secret, assurance and focused contrast checks. Static analysis rejected import ordering in the new session test; ordering was corrected. Provider/mutation/coverage acceptance and final exact-head checks remain pending.
- Local Python checks: lifecycle evidence selection (4 tests), existing evidence parser (8), browser matrix (8) passed. These do not replace provider execution.

- Final strengthened local PostgreSQL experiment: all 13 exact lifecycle identities passed, zero skipped, 365.308s module / 366.071s outer duration. Recorded process replacement observations were 15.215–21.628s; these include the deliberately rejected wrapper startup where applicable and are not production recovery objectives.
- Security follow-up added a successful `IssuerCredentials.Load` and matching wrapping-certificate thumbprint assertion before the unrelated-wrapper child launch. The temporary loaded bundle is disposed before startup. This distinguishes valid replacement material from inability to decrypt the old populated ring. The focused original-cookie/wrapper PostgreSQL case passed (1/1, zero skips, 56.384s module / 56.845s outer); build had zero warnings/errors. Full final-head CI must include this additional assertion.

## Restart traceability

All names below are in `IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart`; each row executes separately for PostgreSQL and SQL Server. The strict discovery gate requires 13 distinct identities per provider, all executed with passing rows, complete counters, matching definitions/entries and no run errors.

| Plan / requirements | Test class and method | Cases per provider / evidence |
| --- | --- | --- |
| RST-01; LIFE-001/002/004 | `SessionContinuityTests.Original_cookie_survives_process_replacement_without_login_or_ticket_refresh` | 1; original identity/authentication time, unchanged complete session inventory, no replacement ticket or credential fallback; absent/tampered cookie denies. |
| RST-02; LIFE-002/008 | `SessionContinuityTests.Original_pending_form_keeps_antiforgery_and_browser_binding_after_restart` | 1; original form/cookies succeed; invalid antiforgery and foreign browser with valid own antiforgery fail before owner consumption with unchanged persisted state. |
| RST-03; LIFE-002/003 | `SessionContinuityTests.Completed_continuation_retains_owner_binding_and_single_consumption_after_restart` | 1; exact foreign-browser callback/error/state denial, original completed owner identity, single consumption, replay denial. |
| RST-04; LIFE-003 | `CodeContinuityTests.Exact_live_code_retains_bindings_and_redeems_after_restart` | 3; proof/client/redirect native errors ID2052/ID2069/ID2072 followed by exact-code valid redemption and replay denial. Wrong client is independently authenticated. |
| RST-05; LIFE-003/005 | `CodeContinuityTests.Still_unexpired_redeemed_code_remains_replayed_and_revokes_linked_tokens` | 1; native ID2010 while original code remains unexpired, linked token status revoked, no new issuance. |
| RST-08; LIFE-003/005 | `CodeContinuityTests.Original_code_reaches_unchanged_real_expiry_and_fresh_flow_recovers` | 1; original native lifetime <=60s, real expiry ID2016, fresh genuine recovery. |
| RST-06; LIFE-004/008 | `ProtectionContinuityTests.Original_tokens_validate_with_fresh_public_keys_and_new_tokens_with_retained_keys` | 1; original public tokens and fresh JWKS, new tokens and retained public keys, tamper/unrelated-key denials, all RSA private fields including `oth` rejected. |
| RST-09; LIFE-001/008 | `ProtectionContinuityTests.Populated_ring_rejects_unrelated_wrapper_then_original_cookie_recovers` | 1; valid unrelated wrapper, fixed safe exit 2 without readiness, restored original wrapper/ring and original cookie recovery. |
| RST-07; LIFE-005 | `EligibilityContinuityTests.Restriction_committed_while_issuer_stopped_denies_original_cookie_then_fresh_login_recovers` | 3; revoked/expired session or disabled account committed while stopped; denial without issuance; bounded restoration only for reversible account restriction and explicit fresh-session recovery. |

Every case uses `RestartIssuerAsync`: original launcher/actual AuthServer child termination, distinct new generations, fresh health report and HTTPS 200, unchanged canonical issuer/callback/database IDs, completed bootstrap/migrator generations and original credential/ring hashes. Captured generations accumulate under a lock; teardown attempts all resources and verifies containers removed and child generations no longer executing before accepting the fixture. Linux zombie/dead records are non-executing; unreadable/malformed evidence fails. Shared AppHost harness controls reject no-op/stale/reused-PID observations and surviving orphan children, and retain all 64 concurrent generation additions.

Live-code success and replay assertions require more than 10 seconds of original validity at their decision boundaries; restart cannot replace the code or extend its 60-second native lifetime. The helper's 45-second restart and nested 20-second negative-start limits remain unchanged. No new production endpoint, persistence model, credential policy or coverage exclusion was added.

Final evidence will record actual discovered/executed identities, unchanged native-code timing margins, both provider/job durations, teardown, exact source/merge trees, coverage inputs and reviewer dispositions before acceptance.


## CI scheduling failure and correction

[Run 38175469170](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38175469170), source `d5af709133f6391a324bf6309a31d02de1cb57c6`, tested merge `e1a9daeaa5151184f220e48c834328377451b757`, tree `34669d9fbfd59d2cfb808073b11be020e37430a7` (identical source/merge trees), is **not accepted full-matrix evidence**. It passed unit 245, all six browser cells (174), PostgreSQL 313 including lifecycle 13, both seven-mutation jobs, static analysis, dependency/license/secret, assurance and focused WebKit contrast (60 observations, zero findings).

SQL Server passed persistence 4 (41.702s), AppHost 4 (216.283s), and issuer 292 (620.761s) before beginning lifecycle. The whole job reached its 20-minute budget during lifecycle execution; cancellation is not a lifecycle pass and coverage was skipped. GitHub's terminal cleanup killed remaining processes; this interrupted run is not evidence of successful fixture teardown. PostgreSQL completed in 13m52s; its lifecycle MTP module took 288.539s (TRX start-to-finish 288.362s), and restart observations were 12.909–17.780s. All live-code assertions retained their strict original validity margin.

The correction adds two provider lifecycle rows to the existing verify matrix. `--partition standard` retains all 300 existing provider cases and their artifact names; `--partition lifecycle` runs all 13 lifecycle cases under distinct provider-lifecycle names; the default remains all 313. A regression proves the two selections are disjoint and their union preserves the complete profile, and rejects unit/browser misuse. Coverage now requires both added artifacts; missing or empty inputs cannot hide behind passing standard coverage. All five verify jobs are coverage prerequisites. No test/job deadline, skip rule, security policy, module floor or exclusion changed. No new production source or dependency was added.

Independent architecture review accepted this scheduling correction as ordinary implementation work under the approved scope. Local selection tests now pass 5/5, browser/coverage-input tests 8/8, evidence tests 8/8 and coverage-floor tests 3/3. Both lifecycle artifacts, complete corrected-head regression/coverage and final source/evidence review remain required before merge.
