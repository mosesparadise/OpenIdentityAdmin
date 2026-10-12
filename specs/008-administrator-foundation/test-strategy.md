# Verification strategy

Execution results and review dispositions are recorded in [verification.md](verification.md).

| Cases | Requirement | Boundary / oracle |
| --- | --- | --- |
| BOOT-01 valid initialization | ADMIN-001/002/003 | Actual Migrator process, independently query native user/roles, pending state, receipt and one audit. Verify password through real UserManager; no tokens/sessions/factors. |
| BOOT-02 input/configuration denials | ADMIN-001/011 | Missing/malformed/duplicate/oversize/unknown fields, nonredirected input, secret canaries, unsafe operation combination, normal upgrade does not bootstrap. |
| BOOT-03 repeat and changed state | ADMIN-002 | Fresh process repeats same operation; altered password, disabled/deleted user and removed memberships stay altered. No reset/regrant/re-enable/recreate. Conflicting operation/normalized identity fails. |
| BOOT-04 collisions/corruption | ADMIN-002/009 | Normalized username and reserved-role collisions, receipt/consumed mismatch, missing singleton, partial enrollment state; no adoption/repair. Unrelated pilot/account data unchanged. |
| BOOT-05 observable contention | ADMIN-002/009 | Independent connections plus real two-process race; prove shared-lock contention and single successful creation/audit, not just Task.WhenAll. |
| BOOT-06 atomic failure | ADMIN-006 | Deny actual audit INSERT after successful manager writes; all account/role/receipt/consumed changes roll back. Native manager rejection and cancellation also leave no partial state. |
| BOOT-07 crash/result loss | ADMIN-002/006 | Kill or cancel before deterministic commit boundary, assert rollback/released lock; separately lose post-commit response and reconcile durable receipt. No blanket retry. |
| BOOT-08 upgrade/privileges | ADMIN-009 | Fresh/upgrade migrations preserve existing pilot/Identity state; bootstrap works using runtime DML credentials and does not migrate; old schema/readiness fails. |
| BOOT-09 no exposed authority | ADMIN-003/012 | Actual pilot HTTP login/authorize attempts reject the created disabled/role-bearing user, with a genuine synthetic-pilot positive control and independent session/token/audit state assertions. Admin API/BFF documentation and management remain unmapped. No administrator session/authentication claim. |
| BOOT-10 evidence controls | ADMIN-011 | Strict exhaustive discovery and exact MTP identities/counters/no-skips, isolated provider partitions, seed omission/substitution/empty-report failures; mandatory coverage inputs. |

Use the dedicated Administrator Aspire project approved by the user, one isolated invocation per provider. Reuse fixture patterns (not pilot policy): real disposable database, runtime credentials for product operations; elevated setup credentials only for migration/fault injection/independent inspection. Bound process and resource cleanup. Do not insert these tests into the issuer's existing deadline.

Later MFA tests must prove actual protected Razor ceremonies, cross-node TOTP replay denial, single-winner recovery consumption, generation/stamp/browser binding, safe expiry/throttling, commit-before-cookie and no secret-bearing captures. Later governance tests must demonstrate actual contention and current-state authority, including indirect loss through role bundles and legitimate lockout. Match theme/browser/provider matrix and retain human assessment separately.

## First-increment reviewed discovery inventory

The reviewed inventory contains 45 cases per provider (90 total), all in `IdentityPlatform.Administrator.Tests.Features.Bootstrap`, with exactly one Provider trait and no Browser trait. The selected project's discovered identities must match the explicit reviewed inventory; theory rows count separately. Final execution must verify exact names/UID binding and MTP result definitions/entries/counters, not only totals.

| Group | Concrete rows per provider |
| --- | --- |
| Creation (3) | actual process and exact bundle/state/password/audit; native minimum/no-composition acceptance; native maximum acceptance |
| Replay (6) | ordinary, changed password, disabled state, deleted user, removed membership, renamed user; each remains unchanged |
| Creation conflicts (9) | existing normalized user, reserved role, pending enrollment, consumed-without-receipt, receipt-without-consumed, missing singleton, mismatched receipt operation, mismatched identity, unowned permission claim |
| Concurrency (2) | identical operation: one created/one already-initialized; conflicting operations: one created/one conflict; observed shared lock contention |
| Atomic failure (5) | denied audit INSERT, manager rejection after role writes, cancellation while waiting for lock, precommit process termination, cancellation token propagation into the actual native manager INSERT (interceptor); separate real provider lock cancellation |
| Process/upgrade (5) | lost postcommit response, outdated schema, runtime DML/no schema alteration, preserved unrelated pilot/account data, ordinary startup/upgrade never bootstraps |
| Isolation (1) | real pilot positive control + bootstrapped-account denial with independent state evidence |
| Input/process boundaries (6) | invalid UTF-8, raw-byte overflow, valid JSON without EOF deadline, malformed input with secret canary, invalid escaped high/low surrogate |
| Configuration/process denials (4) | unknown operation, local database-role provisioning, missing platform connection, real terminal input |
| Ordinal permission type (4) | unrelated case/padding variants allowed; each mixed with an exact reserved claim rejected atomically |

Pure parser variations (unknown/duplicate fields, extra JSON, blank/invalid GUID, invalid username/reference, password 15/16/256/257, exact byte limit, malformed escaped Unicode) belong to existing unit suites in addition to these process/provider cases. If implementation discovery requires a justified split, update this inventory and matrix in the same reviewed PR; never silently accept changed totals.

Runner contract: add an `administrator` partition; `standard` excludes both Issuer.Lifecycle and Administrator, `all` includes both, browser/unit reject the administrator partition. The administrator partition selects only this project and exact Bootstrap namespace for one recognized provider, while validating the complete two-provider catalogue first. Add isolated 20-minute CI jobs and preserve the existing 660-second per-invocation ceiling, with two required `administrator-{provider}` coverage inputs. Seed omitted/duplicate/substituted identity, unknown namespace/provider, wrong-provider selection, Browser contamination, empty report and false counters. Existing standard/lifecycle/browser selections and limits remain unchanged.

## Increment 2 evidence design

[MFA-01–13](mfa-design.md#required-test-inventory-before-implementation-acceptance) defines the next evidence families and decisive oracles. They are planned, not executed tests. T027/T028 must translate them to exact independent discovered identities and provider/browser/story partitions, preserving all current cases, deadlines and coverage inputs. A correct recovery race creates no full session; distinguish one successful transition audit from legitimate denial audits.
