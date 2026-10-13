# Governance verification corrections

13 October 2026. Status: reviewed local corrections and focused verification complete; final exact-head full CI, coverage and delivery review remain required. PR #48 remains draft/unmerged at this checkpoint.

## Current authority and integration

The requesting user asked: **“Can you review requirements around the draft PR and squash if everything checks out? Then proceed to the next item.”** After the failed checks were reviewed, the user asked: **“Can you fix the failures?”** This current instruction authorizes correcting the failures and running the verification needed to assess that conditional delivery request. It does not renew A-004, A-005 or A-006, close historical findings, activate management routes, or authorize production/public release.

Remote state was rechecked: PR48 `045-administrator-governance` at `f78f1962dc6c0c4eb403804b94e2905714b813e8`; diagnostic branch at `3699718603d90665117e384128803aa5f75ad12e`; main at `a4c2ea206593830defe5406106b46eda492598a4`. Run `38426110730` had 70 jobs: 66 successful, three failed (SQL governance mutations, SQL Governance.Failures, final acceptance), and coverage skipped. Those results remain failed evidence.

Corrections were prepared in an isolated checkout from the handoff. Independent test and architecture/security reviewers also inspected the cumulative diagnostic integration from the frozen PR head. The reviewed test instrumentation, seeded controls, strict metadata inventories, sanitized diagnostic receipts and history are deliberately included with this correction. No existing branch is rewritten, no production source changes are included, and the historical diagnostic approvals remain expired/concluded. The legacy diagnostic runner's A-006 label grants no current authority and was not used to authorize these correction runs.

## GOV-11 / ADMIN-009: SQL schema privilege oracle

The current pre-correction rerun of the original runtime-principal case failed with SQL 1088, after successful governed DML and attributable accepted audit. Fresh runtime/admin correlation established the exact existing object, expected runtime principal, effective ALTER permission zero, stable connection and independently absent probe column. The original assertion rejected 1088 before its final full-state comparison. [Before-correction observation](evidence/corrections/before-privilege-correlation.json).

`SqlPrivilegeCheck`, `SqlPrivilegeSnapshot` and `SqlPrivilegeEvidence` now bind observations from the current attempt. The runtime metadata read shares the existing five-second ALTER budget, on the explicitly held-open ALTER connection. The later administrator probe retains its separate existing five-second budget. Acceptance requires matching nonempty database/server and positive object identity, the exact EF target, expected runtime identity, runtime ALTER=0, administrator ALTER=1, unchanged singleton/schema, an actual ALTER attempt, and a nonempty connection identity stable across sampling and ALTER. Missing, unknown or mismatched evidence fails closed for SQL 229/4902 as well as 1088.

Raw 1088 remains `ProviderError`; it is not relabeled native denial. Successful ALTER, other provider errors, cancellation, missing probes, altered schema and primary/probe failures cannot pass. PostgreSQL's native denial path, governed DML/audit assertions and final complete snapshot assertion remain intact. No grants, SQL statements under test, production security logic or deadlines were weakened.

105 SQL-only pure controls exercise every incomplete guard against all three codes, valid controls, contradictory native classifications, every incomplete probe and exception ownership. They are diagnostic-logic tests and do not represent 105 database executions. All 916 prior portable native identities/metadata are preserved; total 1,021: PostgreSQL 458 and SQL 563 including 13 browser rows each. Ordinary counts are 445/550; Governance.Failures 61/166. Policy 74 and Commands 68 per provider are unchanged.

## GOV-02, GOV-06–08: decisive witness mutation

The original small-fixture SQL mutant still failed outside its required bypass assertion: governance completed first at `WitnessEvidence.RequireOrderAsync`, after `GateArrived`/`OrderWait`. [Retained non-kill](evidence/corrections/mutant-SqlServer-before-population.json).

A temporary read-only native wait observer, scoped to the leased database and exact governance blocker, found a normalized-name `AspNetUsers` SELECT lock wait. The final observer distinguished a full-user projection, an open transaction, and exactly one blocked runtime session; its finite code 31 denotes that combination. This establishes a later native user lookup blocking behind governance, not a particular query plan, lock granularity or historical CI cause. An intermediate classifier reused one code for unknown/full-user statements; its receipt is retained but supplies no unique phase conclusion. The corrected classifier used disjoint unknown and multiple-session codes. All temporary observer and mutation changes were restored byte-for-byte. [Final trace](evidence/corrections/mutant-SqlServer-trace-unique.json).

`WitnessPopulation` creates a predetermined 512 ordinary users through native `UserManager.CreateAsync`, before fresh command proof preparation. These users are passwordless and disabled; assertions require no roles, MFA, administrator enrollment/authentication/factor/session rows. The discovered governance lock candidates and usable population must remain exactly actor and witness. This supplies a populated-database counterexample without query hints, altered isolation, index changes, bypassed native validation or deadline extensions. The count was chosen before the experiment and was not tuned after failures. An initial fixture verification query failed at a cross-column membership check; IDs are now materialized before comparisons, preserving the same assertions without comparing differently collated native/product columns. That setup failure is retained separately.

Only the critical case's witness factory call changes. The real fifth wrong-password attempt, final native-commit gate, wait-versus-writer decision, mandatory lockout/session denial, native governance commit, exact membership/version/audit/proof checks, empty usable set and final `governance-witness-lock-bypass` assertion remain unchanged. A timeout or unrelated failure remains an invalid mutation result. The existing exact CQ004 exception retains its scope, owner and 15 October expiry; only the one-line method fingerprint and correction rationale changed. No legacy allowance was added.

## Executed focused evidence

SDK 10.0.401/runtime 10.0.12; managed Docker 28.4.0. Pinned PostgreSQL 18 digest `74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336`; SQL digest `4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`. Both images were available with the configured proxy/TLS trust preserved. Current runtime policy applies both Microsoft regional host entries; this does not establish the old environment's configuration cause.

| Check | Result | Evidence |
|---|---|---|
| Locked administrator restore/build | Pass; zero warnings/errors | Existing project/build/analyzer configuration |
| Pure diagnostic controls |129/129; no skips | [Receipt](evidence/corrections/pure.json) |
| SQL ordinary baseline |2/2; no skips | [Receipt](evidence/corrections/baseline-SqlServer-final.json) |
| SQL omitted-witness mutant |1 expected failure; exact marker/method validated | [Receipt](evidence/corrections/mutant-SqlServer.json) |
| Restored SQL ordinary control |2/2; no skips | [Receipt](evidence/corrections/restored-SqlServer-final.json) |
| PostgreSQL ordinary control |2/2; no skips | [Receipt](evidence/corrections/ordinary-PostgreSql.json) |
| Byte-for-byte mutation restoration |Pass, followed by clean rebuild | [Receipt](evidence/corrections/mutant-restoration.json) |
| Code-quality gate and seeded controls |Pass; no baseline growth | Exact exception described above |
| Python administrator selection / mutation controls |31 /56 passed | Repository unittest suites |
| Independent architecture/security and test review |Pass for source, focused evidence and explicit integration | [Disposition](evidence/corrections/reviews.json) |

The populated baseline, restored SQL and PostgreSQL receipts share source identity `6eb62fc47a9b3f95711a38392978a58230e59a6a24d9bd8368edfd0634991af4`. [Changed-file hashes](evidence/corrections/tested-source-files.json) bind the tested implementation to handoff 3699718; subsequent evidence/status documentation does not alter those executable files. The mutant changes only the manifest's omitted-lock source slice and is restored afterward. The pure controls were run on the same guarded-oracle implementation before the independent witness fixture addition.

Commands used the locked administrator project restore, `dotnet build --no-restore --nologo -m:1 -nr:false`, native pre-enumerated JSON discovery, and `dotnet test --project … --no-build --no-restore --no-progress --pre-enumerate-theories on --filter … --report-trx`. Ordinary selection was the exact `PersistenceTests.Given_RuntimeDatabasePrincipal_When_GovernedChangesRun_Then_RequiredDmlWorksAndSchemaWritesDeny` and `WitnessOrderingTests.Given_FinalWitnessObservation_When_MandatoryLockoutRacesNativeCommit_Then_WitnessLockSerializesTheCommit` methods under the chosen Provider trait. The mutant selected only the latter. Repository `validate_report` checked exact names, all counters, native exit code, run-level errors and the exact mutation marker/method. Budgets remained build 180s, discovery 60s and execution 180s. Raw reports/provider values were kept private; only sanitized projections are published.

## Remaining gates

Focused success is not full PR acceptance. Final exact-head CI must pass every required job, all mutation suites/providers with baseline/killed/restored controls, all 55 coverage inputs and nine module floors. Review the actual artifacts and current remote/review state before any conditional squash. PR title remains the Conventional Commit title for the feature's final scope.

All six historical findings remain open: ADM-OPS-01, ADM-EVID-01, SQL/WebKit certificate evidence, mutation-verifier timeout, GOV-EVID-01 and GOV-PRIV-01. New corrected-source evidence does not rewrite their failed receipts or establish missing historical causes. All 48 human case/cell rows remain `not_run`; whole-increment acceptance, management activation and human/public-release/external assurance blockers remain unresolved. No downstream API/BFF/React work follows merely from these local results.
