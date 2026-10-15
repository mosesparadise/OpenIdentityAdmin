# Witness exception retirement: accepted internal delivery

15 October 2026 (Europe/London). [PR #50](https://github.com/mosesparadise/OpenIdentityAdmin/pull/50) was normally squash-merged as `e780cfa964ebed683e59f1dee50349fd6026c935`. Feature 009 T020 and its bounded CI preparation correction T021 are delivered. The wider T015 retrofit remains open.

## Exact accepted candidate

[Run 38751626290](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38751626290), attempt 1, passed **all 70 jobs** at head `0efb4ab1fb47b253fe1d91b39f74142efae14e39`. Tested merge `3168bfb08877d04c9ef74226983d2c4f41d3f4c3` has parents `ab281729` and that head. Source, tested merge and delivered squash share tree `f01047884126e3fb02aeb5bd958ea4e32984408a`. Final acceptance explicitly passed full scope.

The exact CQ004 witness-method allowance is removed, not renewed. All 1,826 existing legacy records are unchanged. Sequential preparation and committed-effect checks move into the existing fixture; native race ordering, task/resource ownership, cancellation/drain, deadlines, assertions and mutation identities remain unchanged.

The accepted CI correction acquires and verifies the existing pinned database image before native execution. All 56 native preparation steps passed; unit preparation was intentionally excluded. It preserves fresh containers/databases, migrations, readiness and teardown, every existing test/job limit and security oracle. The earlier cold-image startup failures support this preparation change; a green run does not explain the separate DCP timeout or historical certificate cancellation.

## Independently reconciled native and coverage evidence

| Previously affected suite | Native result | Failed-before identity now passed |
| --- | --- | --- |
| PostgreSQL administrator Recovery | 19/19, zero skips/errors | Native recovery-code concurrency rollback case, 18.471 s |
| PostgreSQL issuer Boundaries | 162/162, zero skips/errors | Committed role-addition code invalidation, 18.917 s |
| PostgreSQL/WebKit administrator Accounts | 8/8, zero skips/errors | Native clipboard journey, 46.444 s |
| PostgreSQL/WebKit standard Issuer | 30/30, zero skips/errors | Unrelated-certificate rejection, 1.524 s |

Independent architecture/test review checked actual native TRX result, definition and execution-entry bindings, unique identities and complete counters. All 55 required coverage inputs are present. Aggregate artifact `11982561834` has ZIP SHA-256 `0656afd6d0ca70ed9e56cdbcafa85405556593ceb8b202118e9e9ba5d7a201dd`; the aggregate XML hash is `8346b926134a2a40f96e6e5d86d0df9da58e0563d2e48f3d65d7d582dd92639f`. Recomputed required modules pass the unchanged 90% line / 80% branch floors:

| Module | Lines covered/total | Branches covered/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 93/94 | 6/6 |
| IdentityPlatform.Infrastructure | 2788/2939 | 392/399 |
| IdentityPlatform.ServiceDefaults | 61/61 | 18/18 |
| IdentityPlatform.Migrator | 386/390 | 30/30 |
| IdentityPlatform.AuthServer | 1986/2063 | 223/226 |
| IdentityPlatform.Admin.Api | 70/70 | 10/10 |
| IdentityPlatform.Admin.Bff | 159/159 | 30/30 |
| IdentityPlatform.Worker | 25/25 | 0/0 |
| IdentityPlatform.AppHost | 187/188 | 14/14 |

Six downloaded mutation ZIPs independently reconcile **42 kills, 42 passing baseline controls and 42 passing restored controls**, zero skips. Exact run/head/tested-merge bindings, manifests, source hashes, computed mutant hashes and fixed native cases match. These are sanitized verifier receipts; uploaded artifacts do not contain raw mutant TRX. The unchanged source-bound verifier owns native exit/marker/whole-run validation.

| Suite | Provider | Kills | Artifact | ZIP SHA-256 |
| --- | --- | ---: | --- | --- |
| administrator | SqlServer | 8 | 11982595996 | `7694994ce6944531d8fd9ce9c1949a3419140eeba21f5e5041a864ffd73f79da` |
| governance | PostgreSql | 6 | 11982475743 | `76ea851d6cdcf37fc93ade065643d14132e68b4fbd65faa169468674cb1b97d6` |
| administrator | PostgreSql | 8 | 11982303137 | `ab32abd79dae1e044b334999375de7de94d926d381201d09a6b17fe22707e05d` |
| issuer | SqlServer | 7 | 11981893342 | `83720dcffbc9f95a18289d80671a04c1652122913d1d7f37b5a61455c556bafe` |
| issuer | PostgreSql | 7 | 11981593234 | `8dd86a88f79913fc9689ba4c5d4f78e515bf4854bee7997daa8facaa73158b26` |
| governance | SqlServer | 6 | 11981428576 | `5d5ada2f25d7210a2cabd43e233d053f0391f931ccdaa16340a772675476f67a` |

Independent architecture/test and security reviews pass for this exact candidate. The prior [failed CI and diagnostic disposition](witness-ci-disposition.md) is retained without causal closure. All six historical findings, all 48 human assessment rows and activation/public-release gates remain open. No expired approval or retired diagnostic is revived.

## Next roadmap work

PR #52 already implements feature 010's internal durable BFF ticket store; do not repeat it. Bring this accepted main checkpoint into that branch. Its last run `38750400030` passed 66 jobs but failed SQL AppHost and static quality, followed by final acceptance; aggregate coverage was skipped. The expired allowance dependency is now actually retired on main. The SQL stream has 12 passing terminal cases, including the lock-wait test, but no complete report before the 660-second invocation limit. A reviewed mandatory split of the existing eight AppHost cases and seven BFF cases can retain both-provider totals, per-invocation limits, the 20-minute job bound and all coverage inputs. This is pending implementation acceptance, not a passing SQL result.

Feature 010 B1 acceptance and subsequent B2/B3/C/D/E issuer/client/browser/API/React work remain open. No runtime activation or external release follows from this engineering delivery.
