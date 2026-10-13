# T020/T021 implementation verification and delivery hold

Status: **delivery halted; A-004 irrevocably expired on 13 October 2026.** PR [#48](https://github.com/mosesparadise/OpenIdentityAdmin/pull/48) is a draft and must not merge under A-004. T020/T021 remain incomplete. The implementation lead owns evidence preservation; independent security and architecture reviewers assessed the mutation receipt. No production deployment, API/BFF activation, historical closure or human acceptance follows from this work.

## Source and completed local evidence

The pre-code packet was published at `e5f43f6fac9d2d6532bd931d6cae994a6a80a799`. Implementation head `f78f1962dc6c0c4eb403804b94e2905714b813e8` has tree `1850c1cb43f14f9112325641af0b5887c75f49dc`. Local evidence includes build/static analysis and seeded enforcement, 431 ordinary unit cases, all191 PostgreSQL governance cases (74 policy,68 commands,49 failures), four production route-inventory cases, license inventory and source-secret scan. The same-path comparison preserves510 prior native IDs, names, types and traits;382 additions yield892 administrator cases. The ten driver controls/provider do not contact the database. These receipts verify their own executions only.

Independent scoped source reviews found no open architecture/security issue before CI. They do not replace provider execution, independent external assessment or required human review.

## GOV-EVID-01 — incomplete required SQL mutation evidence

[Run38426110730](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38426110730), [job115395898582](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38426110730/job/115395898582), `Governance mutations SqlServer`, failed its required native verifier on the implementation head.

- All six baseline cases passed; the current-grant mutant received its expected strict behavioral verdict.
- Mutant2, `omitted-witness-lock`, built and discovered successfully. Its native execution returned test-failure exit2 after52.543 seconds, inside the unchanged180-second command budget.
- The strict verifier reported: `Mutation failed outside its demonstrated behavioral bypass assertion; no acceptance evidence produced.`
- Source inspection shows that identity, counters, expected native exit and permitted run-level diagnostics had passed before this final error-message/stack predicate. The available receipt does not identify which message/stack/shape check failed.
- No complete `evidence.json` was produced. The private temporary TRX was removed during normal unwind; its actual failed assertion, exception classification and reached witness stage are not retained. Whole-command duration cannot establish the five-second transaction timing.

This is incomplete mandatory mutation evidence under A-004 condition4 and immediately expired that authorization. It does not establish a timeout, the historical timeout's cause, mutant survival, or an actual unmodified-product trust bypass. New passing runs cannot repair the missing receipt or reactivate A-004.

The retained decoded job-log snapshot has SHA-256 `62f22fb1c4cbdf08f017ea1c93d2293cc875d591bb1c822f76b7f790835a9698`. Raw logs remain private; only allowlisted facts are recorded here.

## GOV-PRIV-01 — SQL runtime-principal denial not demonstrated

The same run's [job115395899978](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38426110730/job/115395899978), `administrator-SqlServer-governance-failures`, completed49 cases with48 passes, one failure and no skips. The failure was:

`PersistenceTests.Given_RuntimeDatabasePrincipal_When_GovernedChangesRun_Then_RequiredDmlWorksAndSchemaWritesDeny(provider: "SqlServer", condition: "runtime-privileges")`.

The fixed assertion at `GovernancePrivileges.RequireAlterDeniedAsync` reported `Expected native ALTER permission denial; raw provider diagnostics are withheld.` This follows accepted governed DML and its independently checked commit. The test accepts only SQL permission codes229/4902. Its current failure message does not distinguish successful ALTER, another provider error or cancellation, and assertion failure prevents the later independent schema-absence probe from running. None of those explanations is established by this receipt. Treat privilege isolation as unverified, not accepted or proven broken.

The retained decoded job-log snapshot has SHA-256 `f1bf7ce771f4370fe9e945f9cc56e3a78584535884a96091f44694fcf1861d89`. The ordinary failure artifact11829385987 was downloaded and its ZIP SHA-256 verified as `124a66cb0d9ee082a7838345ff63667b1b42dd37a4f7db36d81bfdcf43bc6189`; its native TRX independently confirms49 executed,48 passed,one failed,zero skipped/aborted/error counters and the same fixed assertion. It still contains no native privilege-error classification. Unrelated startup/log noise does not establish the cause of this assertion.

## Local cancellation and restoration

On learning of the mandatory SQL failure, the implementation lead deliberately cancelled the already-running local PostgreSQL verifier. Its six baselines and first three mutant checks had returned expected results; cancellation occurred during mutant4 execution. The complete evidence file is absent. Independent review confirmed all six mutation-target source hashes match the published head and the disposable checkout is clean. This proves source restoration only: restored build/discovery/test controls did not complete, and no local complete mutation verdict is accepted. The local diagnostic log SHA-256 is `51fd3621806d5317c623d9131fea53495b8f7faa5e2724d5a053008d7a8e3c09`.

Already-running CI is retained for observation; no runtime edit, rerun or squash is authorized through expired A-004. The completed run contains70 jobs:66 succeeded, the two named governance jobs failed, the final acceptance job failed, and coverage was skipped because its prerequisites failed. The run is not accepted. Existing issuer/admin mutation jobs and the PostgreSQL governance mutation job passed their own CI checks; they cannot replace missing SQL governance evidence or supply whole-increment acceptance. No new run was requested.

## Next decision

Recommend the narrowly scoped [A-005 diagnostic proposal](../../docs/amendments/A-005-governance-diagnostics.md). It requires explicit user approval before instrumentation or another runtime experiment. Keep both new findings, all four historical findings, human observations and public-release gates open. Diagnostic completion alone will not accept T020/T021 or authorize PR48's merge.
