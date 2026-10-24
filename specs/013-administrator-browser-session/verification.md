# B3 planning verification and acceptance map

**Current three-item batch (24 October):** The user explicitly selected T005, T007 and T008 with required prerequisites. [T005 coordination evidence](browser-coordination.md) records current source, preserved inventories, local checks and the pre-test Aspire startup failure. All three tasks remain open pending their required implementation and exact-source acceptance; T006 is already delivered.

**22 October implementation amendment:** [A-009](../../docs/amendments/A-009-roadmap-browser-deferral.md) supersedes the older Firefox-before-B3 sequencing statements in this planning record. T028 stays deferred/open; it does not block code. T003 native identity/state feasibility and T007 pure validation/projection may proceed independently of the remaining native transaction/end-session work. Concrete security prerequisites for dependent routes and external acceptance remain required.

21 October 2026, Europe/London. This packet completes specification/planning only. All B3 runtime scenarios below are **not_run**; their labels are planned scenario IDs, not discovered case counts. Feature 011 T028 and feature 013 T002–T024 remain open.

## Executable identity/state slice — 22 October

[Native integration and implementation](native-integration.md) records the first dormant B3 production policy and 66 actual focused Host executions, all passed with zero skips. T002 records A-009; T003/T007 are partially implemented, not completed. Build and code-quality checks pass. Full Host execution passes **1,192/1,192**, zero failures/skips; all **211 Python controls** pass. Native Host TRX SHA-256: `8178558adb8ba94e58242ad667ddb3c951592f1cfe79450191c756497885aac2`. Independent architecture/test and security source reviews pass. Hosted exact-head verification remains pending; no B3 database/browser execution is claimed. The first full Host attempt failed 32 process-start controls because the restored SDK was not on the subprocess PATH; correcting the command environment produced the full passing result without a source/test assertion change. All prior planning-only observations retain their dates.

## Shared transaction continuation — 22 October

[Implementation, invariants, independent reviews and test inventory](native-transactions.md) record the next T003 prerequisite. Local builds, formatting, code quality and native selector reconciliation pass; new database execution awaits the corrected hosted head. Initial PR #71 static/advisory failures and their scoped corrections are preserved in that record. No full B3 acceptance claim is made.

## Exact-source prerequisite evidence — 22 October

[Delivery evidence](prerequisite-delivery.md) records final source `53c78662dc9db8a90d1dd1352392f9855178183e`, all 67 successful jobs, both native providers, 48 mutation kills, 50 coverage inputs/15 floors, retained artifacts, independent review and the raw XML transfer limitation. PR #71 is squash-merged as `017bfaa5396e6d2931c99af110faa0710c60e7bb`; source/tested merge/squash share the recorded tree. This supersedes the pending statements above. T003/T007 remain partial; complete product scenarios below remain not_run.

## Native end-session validation preparation — 22 October

[Fixture and evidence limits](native-end-session.md) record six discovered cases, zero-warning/error build and independent architecture/test/security/API source review. All 1,241 prior native identities and portable metadata remain; discovery totals 1,247. Provider execution is pending. This is native validation characterization, not product logout or complete T003/B3 acceptance.

## Verified starting checkpoint

Remote main was verified as `2da9ef03dda7d2e7f019ad5f813b8d0515b2b9cf` (PR #67 B2 delivery record), tree `e47d3c84dc946118ff2e63272b5417586ed5a99a`, after B2 implementation PR #66 squash `42214382f5b6246332fe9b1ecfe00463a5ed9965`. The workspace was clean before branch `068-administrator-browser-plan` was created from that main. [B2 delivery](../011-administrator-issuer-client/b2-delivery.md) records 67 successful exact-head jobs, 3,378/3,378 raw executions, zero skips, 50 coverage inputs/15 unchanged floors and 48 mutation kills with baseline/restored controls. None is B3 execution evidence.

Current network configuration was inspected read-only through the cloud runtime skill. The environment is connected/running; configured restricted hosts include both MCR data hosts, while provider-reported network enforcement state is unknown. No new runtime reachability, proxy CONNECT success, image pull or SQL experiment is claimed. No network-policy change was made.

The user explicitly chose **new password and MFA for each sign-in** and **both local and issuer-assisted logout**. These answers settle the two product questions. All activation/production/public-release gates remain separate.

## Planned scenario traceability

Every row requires concrete source/test identities and actual native evidence before runtime acceptance. All six browser/provider cells apply where browser behavior is involved; pure policy controls supplement rather than replace them.

| Scenario | Decisive behavior | Requirements | Tasks | Status |
| --- | --- | --- | --- | --- |
| B3-AUTH-01 | Actual fresh password/TOTP bound to initiating BFF/issuer browser, new source session, native exchange and committed B1 ticket; existing fresh cookie cannot skip new ceremony | FR-001/002/004 | T003/006/008/010–012 | not_run |
| B3-AUTH-02 | Intent/completion replay, expiry equality, cross-browser/tab/account swap, pilot/recovery, arbitrary return, expired PAR/state; new challenge does not revive old state | FR-001/002/013 | T003/006/008–010/012 | not_run |
| B3-AUTH-03 | Unique typed signed original deadline and intent; wrong issuer/audience/nonce/claim destination/duplicate/type/expired source denied; no token-expiry substitute | FR-003/004/013 | T003/007/009/011/012 | not_run |
| B3-AUTH-04 | Current supported authority changes before issuance deny; canonical local principal has exact scheme/sub/sid and no permission or arbitrary property copies | FR-003/004/013/015 | T007/009/011/012 | not_run |
| B3-WEB-01 | Minimal no-store session status, opaque host-only cookie, expiry without read renewal, store outage 503, protected API 401/403 contract | FR-006/013 | T007/013/014/016 | not_run |
| B3-WEB-02 | Native antiforgery plus exact origin on login/replacement/logout, stale-generation tokens, login CSRF, narrow callback/resume/return exceptions, no bearer/CORS bypass | FR-001/007/008 | T007/010/013/014/016 | not_run |
| B3-WEB-03 | Browser network/storage, rendered content and retained diagnostics contain no OAuth secrets; raw query/referrer suppression, no unsafe error echo | FR-004/006/011 | T007/013/016 | not_run |
| B3-WEB-04 | Both themes, keyboard/accessibility, safe focus/error recovery, multiple tabs/back/refresh; no hidden authentication shortcut | FR-006–008/014 | T007/013/016 | not_run |
| B3-STATE-01 | Callback/logout both commit orders; stale response/cookie arrival never makes an invalidated generation usable | FR-005/008/009 | T005/010/011/015/017 | not_run |
| B3-STATE-02 | Explicit replacement has fresh handle/generation, revokes old ticket; old antiforgery/callback cannot replace current account; replacement rollback preserves prior session | FR-005/008/009 | T005/010/015/017 | not_run |
| B3-STATE-03 | Consumed callback followed by ticket failure, cancelled/unknown commit or lost cookie response: no retry/reissue, bounded orphan and later explicit recovery | FR-005/008/011 | T005/006/011/015 | not_run |
| B3-STATE-04 | Capacity equality/overflow/concurrent instances, ambiguous reservations and expiry/tombstone cleanup; no live eviction or extra native rows | FR-002/011/012 | T005/006/015 | not_run |
| B3-END-01 | Local audited revocation/generation invalidation independently survives issuer failure; rollback/ambiguous outcome never called durable sign-out | FR-008/009/011 | T017/019/020 | not_run |
| B3-END-02 | Native end-session exact client/return/source binding and issuer antiforgery confirmation; no GET mutation or unrelated session revocation; issuer commit verified independently | FR-007/010/011/013 | T003/018/020 | not_run |
| B3-END-03 | Remote cancellation/outage/expired or forged return yields truthful local success plus incomplete/unverified remote outcome; registration old→new/idempotent/drift/disabled/precondition/rollback/old-code/old-PAR controls | FR-009/010/013/015 | T003/018–020 | not_run |
| B3-OPS-01 | Sessions-only runtime grants, additive migrations preserving prior data/history, provider conformance and actor provenance in atomic audit intents | FR-011/012 | T005/006/015/022 | not_run |
| B3-OPS-02 | Retained-key multi-instance restart, missing/corrupt keys, process loss and safe no-retry reconciliation | FR-005/011/012 | T005/015/022 | not_run |
| B3-OPS-03 | Restore consequence and explicit invalidation, orphan cleanup/retention and bounded failure without claimed production automation | FR-011/012/015 | T005/015/022 | not_run |
| B3-OPS-04 | Firefox restored, all required identities/coverage/mutation receipts and independent reviews; all remaining human/release gates visible | FR-013–015 | T002/004/021–024 | not_run |

Approved scope mapping: PRD FR-11 (BFF server-held browser session), FR-12 (audit), FR-17 (both databases), FR-18 (operations), FR-20 (R1 logout versus later arbitrary-client logout), FR-24 (Aspire); NFR-05 live API permission enforcement remains M02-D, not B3 acceptance. Preserve the approved security/verification documents and 345-control assurance register; this mapping changes no assurance status.

## Independent planning review and dispositions

Architecture/test/API source research identified the missing original-source-deadline transport, request-bound administrator continuation, product callback ownership and native end-session dependency. Security review confirmed the same boundaries and the need for browser-generation ordering beyond B1 CAS.

Draft findings corrected before final review:

1. **Remote-result overreach:** removed an unnecessary signed remote-attestation service/full-B3 prerequisite. Native issuer revocation is proved independently; BFF browser return remains unverified and cannot claim remote commit. Issuer confirmation can describe its own committed result.
2. **Registration transition:** added explicit audited, version/precondition-bound B2→B3 fixed-manifest upgrade, preserving disabled state, rejecting arbitrary drift/startup repair and invalidating old codes through existing code-principal revision checks plus explicitly revoking old pending PAR through native managers in the upgrade transaction; both-provider transition tests are required.
3. **Old PAR versus code:** source review corrected an overbroad revision-invalidation claim. Existing code-principal revision guards deny old codes; the upgrade must separately revoke affected pending PAR through native managers under the same owned transaction, with distinct both-provider controls in T003/T018/B3-END-03.
4. **Identity expectation precision:** the original BFF expectation is its intent/generation. The issuer binds actual subject/session to the completed ceremony; no browser-provided expected identity or derivation from `auth_time`/token expiry replaces that binding.

Independent architecture/test/API review passed requirements CHK001–008 after the explicit PAR correction; independent security/API review passed security-boundaries CHK001–010 after the same correction. No planning blocker remains. Native feasibility remains unperformed and implementation-blocking; planning review cannot substitute for that evidence.

## Planning checks and delivery

Executed local checks: proportional selector controls 7/7; AI-instruction inventory (eight skills/eight reviewers); assurance snapshot integrity (345 controls, all not_assessed); assurance regression controls 27/27; relative Markdown links across all 13 changed files; 24 unique feature task IDs; scenario-reference checks; complete changed-file classification as docs; and `git diff --check`. All pass. Read-only SpecKit analysis found the draft remote-attestation/registration/PAR inconsistencies above; corrections leave no uncovered functional requirement, duplicate task ID or unresolved product decision.

Initial hosted run [39562157395](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39562157395) at source `1d565d5b68ae17f56ecd5e7bd4e13f88b6eecfef` passed scope/assurance but failed Source secret checks and therefore final acceptance; all twelve full-job skips were expected. Pinned Gitleaks 8.30.1 locally reproduced one generic-api-key false positive on ordinary prose at the final contract paragraph. Reworded that sentence without any scanner/rule/allowlist change; the complete corrected source snapshot passes the same scanner with zero findings. Final corrected-head hosted acceptance remains recorded on the PR.

The PR runs only the repository's fail-closed proportional documentation workflow; the required final gate must pass with the full-job skips explicitly accounted for. No B3 tests, SQL experiment, full workflow or production correction ran for this packet. The delivery PR records the exact final documentation head, tested tree, checks and normal squash outcome after they exist.

All six historical findings, 48 human rows and 345 not-assessed assurance controls remain open. A-004 is expired; A-005/A-006 concluded. Firefox deferral under A-007 ended at internal B2. B3 planning does not implement B3 or activate BFF/API/React, refresh, production or public release.
