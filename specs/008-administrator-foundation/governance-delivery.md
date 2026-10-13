# Internal governance delivery record

PR [#48](https://github.com/mosesparadise/OpenIdentityAdmin/pull/48) was normally squash-merged on 13 October 2026 as `f4995183453ba194e8136143f3ec6878fb16a7c9`, using its Conventional Commit title `feat(identity): implement internal administrator governance`. This completes the bounded internal development scope of feature 008 T020/T021 and correction task F005. Whole-increment and public-release acceptance remain open.

## Authority and delivered scope

Delivery follows the user's current request to review requirements and squash if they pass, followed by the explicit request to fix failures. A-004 remains expired and A-005/A-006 remain concluded. Their historical restrictions and failed receipts are preserved; none was revived to authorize this delivery.

The delivered capability is internal current-permission governance, command-bound fresh native proof, atomic authority changes/audit and locked usable-administrator preservation, including both-provider additive migrations. The [correction record](governance-failure-corrections.md) explains the correlated SQL privilege oracle, decisive fixed-population witness test, independent inventory totals and production Pkcs 10.0.13 dependency patch. Management API/BFF/React access, new onboarding/recovery paths and production deployment are not activated by this delivery.

## Exact delivered source and CI

- Reviewed PR head: `2e17aca6d6724b8a91f6b49dd7d2a4dd84421598`.
- Base before merge: `a4c2ea206593830defe5406106b46eda492598a4`.
- Tested GitHub merge commit: `f8986578ef26786e0a9a393163c8798fc1708fdd`, whose parents are that base and PR head.
- PR head, tested merge commit and delivered squash share tree `ec11b575b2ce12fa23ee1aa09350c094cd196139`.
- [Run 38479414481](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38479414481), attempt 1: **70/70 jobs succeeded**, including Verification acceptance. No failed, cancelled or skipped required job was accepted.
- All six mutation suites and both real database providers passed. Ordinary SQL Governance.Failures now includes 166 cases; PostgreSQL includes 61. Native discovery retains all 1,021 administrator rows, including browser identities.
- Static analysis, current supply-chain/license/secret checks, browser/contract/lifecycle suites and final coverage all passed at this exact head. Local follow-up evidence also records 182 Python controls and 32 credential cases.

PR metadata, head/base, all 70 check runs, reviews and review threads were rechecked before the normal merge. No review submissions or unresolved threads were present. The main branch summary reported unprotected and no rulesets were listed; detailed administration-only protection inspection is not claimed. No force update, approval impersonation or protection bypass was used. Independent AI architecture/security, test and artifact reviews passed; these do not replace human or external assurance.

## Retained mutation artifact reconciliation

The six downloaded archives were verified against their published SHA-256 digests. Review parsed each actual `evidence.json`, checked the tested commit, exact manifest IDs/methods/provider, original-source hashes and recomputed single-substitution mutant hashes, one expected failure per mutant, identical successful baseline/restored test unions, zero skips and byte-for-byte restoration. Total: **42 demonstrated kills, 42 baseline passes and 42 restored passes**.

| Artifact | Kills | Baseline/restored passes | Artifact ID | Archive SHA-256 |
| --- | ---: | ---: | --- | --- |
| critical-mutations-PostgreSql | 7 | 7/7 | `11856713400` | `710f91048de1b4ce2b73456e94ce4d560fc1d55541c60c0083aefa8a33dff46c` |
| critical-mutations-SqlServer | 7 | 7/7 | `11856004558` | `b1572f9c0466a839f09607c8276d0e7d53b6dac226bc430bca54025e72a79ac1` |
| administrator-mutations-PostgreSql | 8 | 8/8 | `11856283676` | `2bc04d3200a9d76733ffd57b8b5fe438f28e3b9b7eff1a6cf977b4e224e7c71d` |
| administrator-mutations-SqlServer | 8 | 8/8 | `11856858830` | `49d3098ea59b1ebc476c522f97b633d6035891898987eae2350a6099829fcc55` |
| governance-mutations-PostgreSql | 6 | 6/6 | `11855713353` | `ea9f32e0c772d705f14f4647c77c3ffe6f344d48128de4981d6d24ebee7915a8` |
| governance-mutations-SqlServer | 6 | 6/6 | `11855868969` | `67326cef43e15c5d82b6adc003cb19517aa6b388d63adf0461b139ff760ffa5c` |

These are sanitized receipts emitted only after the unchanged strict verifier validates native exit, all counters, exact test identities and the required bypass marker/method. The raw temporary TRX files are not retained in these archives, so review does not claim independent replay of their raw stack traces. The seven-day CI artifact retention is distinct from this durable reconciliation record.

## Coverage

[Coverage job 115583850082](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38479414481/job/115583850082) confirmed **all 55 required coverage inputs** before merging reports. Downloaded aggregate artifact `11856869342` has SHA-256 `4778047564c45acdc5c4886c12478cf65308783d624983adcb5fa0513679cc33`; independent review matched its XML and checked every required module against unchanged 90% line / 80% branch floors.

| Module | Lines | Branches |
| --- | ---: | ---: |
| IdentityPlatform.Core | 98.936% | 100.000% |
| IdentityPlatform.Infrastructure | 94.862% | 98.246% |
| IdentityPlatform.ServiceDefaults | 100.000% | 100.000% |
| IdentityPlatform.Migrator | 98.974% | 100.000% |
| IdentityPlatform.AuthServer | 96.268% | 98.790% |
| IdentityPlatform.Admin.Api | 100.000% | 100.000% |
| IdentityPlatform.Admin.Bff | 100.000% | 100.000% |
| IdentityPlatform.Worker | 100.000% | 100.000% |
| IdentityPlatform.AppHost | 99.468% | 100.000% |

Test-tooling modules appearing in the aggregate do not replace the nine required product modules. Coverage does not prove complete product security or manual acceptance.

## Findings and remaining work

All six historical findings remain open: ADM-OPS-01, ADM-EVID-01, SQL/WebKit certificate evidence, mutation-verifier timeout, GOV-EVID-01 and GOV-PRIV-01. Passing corrected-source evidence neither rewrites failed receipts nor proves their missing historical causes. The original failed run `38426110730` and the first correction run `38478409685` remain preserved. The latter exposed the missed inventory consumer and current Pkcs advisories; its final job set was 14 successful, 53 failed, two cancelled and one skipped, and is not acceptance evidence.

All 48 human case/cell rows remain `not_run` in the [manual assessment handoff](../../docs/operations/administrator-manual-handoff.md). Feature 008 T017/T018/T028/T029, whole-increment acceptance, independent assurance, management activation and production/public-release gates remain open. The requesting user owns the Windows/WSL2 human observations; AI review supplies none of them.

This documentation reconciliation is the next bounded follow-up after PR48. The next independent engineering candidate is feature 009 T015: retire the exact witness-method CQ004 exception before its unchanged 15 October 2026 expiry, preserving its native ordering, assertions, deadlines and mutation identity through separately reviewed verification. That refactor is not implemented by this record. Do not start management API/BFF/React implementation solely because PR48 merged.
