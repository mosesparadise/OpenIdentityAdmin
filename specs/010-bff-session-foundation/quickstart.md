# Validation guide

The store is not implemented by this planning PR. The runtime commands below are future acceptance commands, not recorded executions or a new diagnostic launch authorization.

## Planning verification

From repository root:

```sh
python3 scripts/test_ci_scope.py
python3 scripts/check-ai-instructions.py
python3 scripts/check_assurance.py
python3 scripts/test_assurance.py
git diff --check
```

Verify relative Markdown links and require the entire diff to be eligible prose under the unchanged `scripts/ci_scope.py`. GitHub must independently select `docs`, pass scope/assurance/source-secret checks and pass its visible final acceptance gate. Intentionally unselected product suites are not test passes. Do not include Spec Kit's local `.specify/feature.json` state in this prose-only delivery.

## Runtime prerequisites

Before implementation verification, recheck current authority, exact source/main, PR #50 and the shared expired allowance. Use the pinned SDK/locked dependencies, disposable synthetic resources, both provider images and reviewed fixture configuration. Read actual managed network/Docker guidance before container execution. Never weaken TLS or use production data. New store discovery/count/coverage integration must exist before invoking the full strict selectors.

After implementation and reviewed inventory integration, the existing verification entry points include:

```sh
dotnet restore --locked-mode
dotnet build --no-restore
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql --partition foundation
python3 scripts/verify.py SqlServer --partition foundation
```

These are acceptance entry points, not sufficient by themselves: all required exact-head CI, including existing browsers/mutations/coverage, remains mandatory for executable changes. Add a reviewed separately bounded partition if necessary; do not extend current invocation limits or silently omit cases.

## Required demonstrations

| Evidence family | Observable result |
| --- | --- |
| BFF-STORE-01 | Fresh opaque handles and exact native ticket/token restoration across disposed/recreated instances on each provider |
| BFF-STORE-02 | Plaintext sentinels absent from stored payload/logs; wrong key/purpose/binding, tamper, unsupported version and oversize denied |
| BFF-STORE-03 | Positive control immediately before expiry; denial exactly at/after idle and absolute caps; shorter upstream limits and rollback honored |
| BFF-STORE-04 | Two-instance expected-revision renewal has at most one winner; revoke/renew and cleanup/renew interleavings never resurrect or delete a live renewal |
| BFF-STORE-05 | Audit failure rolls back transition; cancellation/dependency/lost-response paths issue no false success or automatic mutation retry |
| BFF-STORE-06 | Both-provider additive/repeated upgrade, readiness and runtime grants; no DDL, migration-history or identity-store access |
| BFF-STORE-07 | Dedicated protected ring survives restart/rotation; lost/corrupt/plaintext material fails safely; issuer protection regressions remain green |
| BFF-STORE-08 | Production endpoint inventory unchanged; no browser/admin/proxy activation or claims of current authorization |

Retain source/merge/tree binding, native discovery, command/environment versions, report hashes, every native counter, owned cleanup/restoration, reviewer findings and remaining gaps in `verification.md`. A row passes only with its actual boundary evidence. No human, conformance, throughput or five-second API claim is supplied here.
