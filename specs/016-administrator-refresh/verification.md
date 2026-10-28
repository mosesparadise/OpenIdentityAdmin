# Refresh verification and review record

**Cleanup assertion candidate (28 October 2026):** [Dormant cleanup proof ownership](cleanup-assertion.md) implements native assertion consumption, exact disabled-profile cleanup eligibility and one-use owned receipts, with six new both-provider persistence identities. Local build, quality, inventory and independent source reviews pass; hosted acceptance is pending. T003–T022 remain open. Sessions credential/cleanup capacity (T005), refresh profile permissions (T007), integrated admission and original transport/deadline proof remain dependencies; product refresh stays disabled.

**Latest accepted increment (28 October 2026):** [PR112 native cleanup delivery](native-cleanup.md#accepted-internal-delivery--28-october-2026), squash `5e8719040a9beaddb6e15c1e9197b5a79ec44048`, adds22 native component cases and four navigation-control executions while preserving every prior inventory. Independent review verifies95 jobs,89 artifacts,79 raw reports/4,697 passes,76 coverage inputs,15 unchanged floors and64 mutation controls. Both genuine provider demos pass. The old5d failure remains historically unexplained; the separately reproduced navigation correction has its own accepted evidence. T003–T022 and all external gates remain open. The earlier native-primitive checkpoint below is preserved, not the latest inventory.

## Native primitive delivery — 28 October 2026

[Accepted native evidence](native-integration.md#accepted-internal-delivery--28-october-2026) records PR110 squash `5cccabe559f24822d2827f86a7b3ae221a1e723e`, all95 exact-head checks and independent review. All89 artifact hashes were independently verified;79 native reports contain4,671 passes, including16 new cases per provider. All76 coverage inputs,15 unchanged floors and64 mutation controls pass. Source/tested merge/squash share tree `d5c8a7b7d4c56a818e91099a0b671c72700df057`.

This completes only the bounded handler/storage primitive increment. T003 remains open for genuine crypto/client transport and authenticated initial-response-loss cleanup; dependent implementation and activation are not claimed. Planning PR109 is merged at `34c5df010df65ac2f2166a525d4028fcd7e86dcd`. The historical planning observations below retain their original scope.

## Planning checkpoint — 28 October 2026

Remote main verified at `44d86237cafa0c407561e084eb35db64938434e6` (PR108), following implementation PR107 squash `b9fdfc7bf3e18c20efe655cdcfe48bd405c9edd3`. Existing worktrees/branches remain preserved. No runtime, SQL, Docker, SDK/package or workflow changes occur in this packet.

The managed environment reports current enforced restricted HTTP policy with the two MCR hosts configured. This packet uses repository/source reads only; no new image acquisition, diagnostic approval or network experiment is claimed. Original expired diagnostics stay concluded.

Applied repository workflows: identity-feature-delivery, identity-protocol-security, pinned Spec Kit1.1.3 `$speckit-specify`, clarification scan, `$speckit-plan`, `$speckit-tasks`, requirements/security checklists and read-only `$speckit-analyze`. `.specify/extensions.yml` is absent, so no extension hooks execute. Local feature pointer is ignored by Git. Approved choices resolve scope/freshness/lifetime questions; technical feasibility remains T003.

## Requirement and scenario allocation

IDs below are planned scenario groups, not discovered/executed test names. T004 must bind every row to actual `Given_<condition>_When_<action>_Then_<outcome>` identities, provider/browser applicability, required partition and evidence; no invented test count.

| Group | Requirement / approved allocation | Mandatory observations | Tasks |
| --- | --- | --- | --- |
| REF-INIT | FR-001/002; PRD FR-09/11, NFR-08 | Genuine password/MFA, old/new fixed profiles, native offline grant, pre-send capacity reservation and initial-response-loss cleanup targeting, protected callback capture, exact canonical identity, wrong response/missing token denial | T003/007/010 |
| REF-AUTH | FR-003/005; PRD FR-05/09/11 | Current account/client/session/authorization/factor/stamp/permission/scope/resource, original auth context; initial300s vs continuing age; both commit orders | T003/008/009 |
| REF-TIME | FR-006; PRD FR-09/11 | Before/equal/after access, idle, source and original refresh/local absolute caps; shorter policy, rollback clock, active browser beyond initial30-minute cookies, lost/late-cookie races, no GET/background renewal | T008–011/020 |
| REF-ROTATE | FR-004; PRD FR-09/11 | Native null-leeway and absent redemption timestamp, one-use rotation, replay authorization+token status separately, unrelated family positive, early consumption survives final issuance rollback, swallowed failure | T003/009/014 |
| REF-OWNER | FR-007; PRD FR-11/17/24 | Two real BFF processes, exact one send, winner/loser, expired/paused owner, no SQL transaction spanning network, revision+generation fence | T004/012/013/015 |
| REF-LOSS | FR-008; PRD FR-11/12 | Before/after dispatch, issuer commit, native response validation, local commit, lost ack/disposal; actual reached boundary, native sends, durable/audit state, no restart retry | T012/013/015 |
| REF-END | FR-009; PRD FR-09–12 | Local/remote independent results, both logout/replacement orders, old predecessor/unknown successor family denial, exact owner, native200 insufficiency, pending cleanup | T003/016–018 |
| REF-WEB | FR-010; PRD FR-11/16/19, NFR-08/09 | Origin/antiforgery/cookie/fixation/headers/trusted proxies, realTLS, no HTML API challenge, no tokens in browser/storage/wire captures/diagnostics, error keyboard/theme/axe | T011/018/019 |
| REF-AUDIT | FR-011; PRD FR-12, NFR-08 | Actual audit permission denial and rollback, protected key failures/rotation, safe actor/correlation and no raw secret output | T005/009/010/012/016/020 |
| REF-DATA | FR-012; PRD FR-17/18 | Both providers same semantics, exact prior populated histories/data/DB identity, second upgrade, native managers, restrictive runtime grants | T005/006/007 |
| REF-OPS | FR-013; PRD FR-18/21/24 | Capacity equality, reserved cleanup slots guaranteeing quota-full local logout, admission rollback/concurrent replacement and no live eviction, bounded100 cleanup, old predecessor retention, native scope cancellation/lock wait/deadline, actual process exit, restore denies stale state | T003/004/005/012/015/017/020 |
| REF-GATES | FR-014/015; PRD FR-21–25 | Exact inventory/coverage/mutations, independent source/evidence review, fail-closed selectors and current documentation; external limits explicit | T002/004/019/021/022 |

SC-001 maps REF-OWNER/LOSS; SC-002 REF-AUTH/TIME/ROTATE; SC-003 REF-LOSS/END/OPS; SC-004 REF-INIT/WEB/AUDIT; SC-005 REF-DATA/GATES. No accepted requirement lacks a task.

## Reuse evidence at the right boundary

`BrowserLoginFixture.PeerAsync` supplies independent service roots sharing real stores but uses TestServer/NativeClientIssuerBridge; it is not actual process/TLS evidence. `BrowserHttpsFixture`/`BrowserHttpsClient` supply genuine Kestrel/trusted TLS; extend to two BFF nodes with one fixed external origin and test routing. `BrowserProcessTests`/`NativeClientProcess` supply actual exited-process controls. Do not describe DI recreation as a process restart.

`BrowserCallbackFailureTests`, `BrowserLogoutFailureTests` and `NativeBrowserParLossTests` supply reached-boundary/audit-denial/acknowledgement-loss patterns and exact sends. Their in-process staging controls do not substitute for actual network loss. Each new injected fault needs its specific reach oracle plus ordinary/restored controls and independent durable-state observation. A broad exception or test-driver exit alone is not proof.

`BrowserAuditMigrationTests`, `DatabaseCompatibilityConformance` and `MigrationCompatibilityTests` supply immediate-prior populated upgrade plus older frozen producer paths. Preserve `scripts/prepare_provider_baseline.py` frozen38eb41e locks. Add exact refresh additions and independently seeded damaged-history/witness negatives.

## Evidence obligations

Baseline implementation run40662669919 passed93 jobs/4,639 native cases,74 coverage inputs,15 modules at90%line/80%branch and64 mutation controls. Those are **prior feature015 evidence**, not refresh execution. Add actual new partitions/inputs without removing baseline obligations, reducing floors, expanding legacy allowances or increasing deadlines. Record source/tested-merge/tree, SDK/native package/browser/provider versions, finite native discovery, raw result paths/hashes, command filters, actual counts and failures. Excluded generated code still needs behavior evidence.

Targeted new behavioral mutants must defeat owner fencing, no-resend, absolute expiry, current eligibility, authorization-wide replay denial, logout ordering and secret projection. Require passing originals, exact decisive failures and restored passes for both providers as applicable. Test verifiers with missing/duplicate/wrong/fabricated evidence. A known browser deferral is `deferred_not_run`, never pass.

A-009 retains Firefox restoration and the exact issuer WebKit certificate-negative case per provider; seven historical Firefox coverage inputs remain deferred. All remaining Chromium/WebKit and new refresh cases stay required. Retain Playwright1.63.0 and strict CSP with native negative control; no `bypassCSP`, ignored TLS, sleeps/retries or new blanket exclusion. Same-host/two-port secrecy evidence must not be relabeled as distinct-site acceptance.

## Independent reviews and execution

Architecture/security/API and test/Aspire reviewers independently returned **PASS for planning** after these corrections:

- Reserved cleanup capacity was originally allocated too late and could block logout at quota. It is now reserved before initial code exchange; T003 must prove authenticated cleanup targeting after total initial-response loss. Logout activates its existing slot. Quota-full logout, admission rollback and concurrent replacement have explicit tests.
- Early durable consumption and later issuance are separate epochs; later issuance rollback cannot revive the predecessor. Final issuance rechecks current family/authority/time.
- Cookie renewal now reissues the same opaque identity/generation only after owned commit/disposal/final deadline checks, bounded by new idle and original caps. Tests cover active use beyond the original30-minute cookie interval and lost/late-cookie races.
- T003 discovers its own characterization IDs; T004 expands subsequent inventory. Ordered delivery now respects T011's dependency on T013.

The architecture/security reviewer approved CHK001–010; test/Aspire independently approved CHK009/010. Review used actual repository/tagged native source and the packet, not runtime execution or formal security assessment. No planning blocker remains.

Read-only Spec Kit analysis found **0 unresolved findings**, with15/15 FR and5/5 SC mapped across22 tasks (US1:4, US2:4, US3:3, setup/foundations:7, cross-cutting:4). No unmapped task or constitution conflict remains. T001/T002 are planning work only; all20 runtime tasks T003–T022 remain unchecked.

Local checks actually run:

```text
.specify/scripts/bash/check-prerequisites.sh --json --require-spec --require-tasks --include-tasks
python3 scripts/check_assurance.py
python3 scripts/check-ai-instructions.py
python3 scripts/test_ci_scope.py
git diff --check
```

Results: prerequisites resolved all design artifacts; assurance345not_assessed unchanged; eight skills/eight reviewer configurations valid; all7 CI-scope controls pass; whitespace clean. An additional read-only path/task check found all local Markdown links valid and exactly22 sequential task IDs. No runtime/provider/browser/full CI was run for this prose packet. Hosted documentation acceptance and normal squash are required on the exact final PR head; the PR records their actual result.

Six historical findings,48 human rows,345 not-assessed assurance controls,fourteen viewer findings,actual Windows feature014 T014 and all production/public-release gates remain open. M02-D online API validation/five-second enforcement and M02-E React/proxy are not delivered by M02-C planning.
