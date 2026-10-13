# T033 — bounded Worker availability investigation

Date: 13 October 2026 (Europe/London). Status: in progress; ADM-OPS-01 remains unresolved. The user authorized the recommended Worker investigation, Firefox investigation and acceptance reconciliation after the engineering batch. This packet supplies no authority for T020/T021, deployment or release.

## Question and stopping rule

The original SQL Server Development profile failed at the explicit Worker health wait. The pinned Aspire message covers several unavailable states. Original artifact 11786420766 was downloaded again and its SHA-256 matched `7b1875dd61a22bd3acab8e9990bbc465469b730e55ea84985b774e9410c8b4bd`: the six-case AppHost report contains one failed Development case, duration 41.958 seconds, ErrorInfo and no StdOut. There is no retained Worker state/exit/generation that can establish the historical cause.

Review the complete fixed CI cohort from accepted T032 run 38368154130 through the last code post-merge run 38381427039, including its cancellation. Then execute the fixed two-arm contrast once per provider in each prescribed local/CI validation invocation. Keep ordinary profiles and future regression execution mandatory. A failed control must not be rerun without an evidenced source/test correction. After the fixed comparison, stop additional incident-specific experiments if no natural recurrence supplies new evidence. Successful injected controls establish diagnostic discrimination, not historical causality.

## Evidence already reconciled

All 16 selected ZIP artifacts (SQL foundation and Firefox SQL Accounts across eight runs) were downloaded and checked against GitHub's SHA-256 digests. AppHost reports were reconciled against the six native SQL identities and strict result/definition/execution bindings. The complete successful executions are runs 38368154130, 38370077685, 38374437544, 38376148746, 38376685536, 38378838696 and 38379701908. Each has all six SQL cases passed; Development case durations range 68.002–73.836 seconds. These are whole-case durations including later deliberate database stop; they are not Worker startup latency measurements. Successful profiles intentionally emit no failure transition log.

Run 38381427039 was cancelled when the subsequent main update superseded its workflow. Its SQL artifact contains only Persistence.trx, with no accepted AppHost report. It supplies no profile acceptance. Later passing runs do not erase either this incomplete execution or the original failure. This retrospective cohort is descriptive, spans several commits and is not a randomized experiment or statistical reliability estimate.

## Retained SQL evidence register

Every entry is attempt 1. Digests below were recomputed from the downloaded ZIPs; all matched GitHub metadata. Artifact expiry is 20 October 2026 UTC, so these references are not a permanent archive. Retain approved protected evidence before expiry if later independent assessment requires the raw reports. A digest alone cannot reconstruct an expired artifact.

| Run / source | AppHost result | Artifact / SHA-256 |
| --- | --- | --- |
| [38368154130](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38368154130) / `f3d229e1862ef75fb5802b424e301f1ca654914d` | 6/6 passed | `11797658578` / `c8efd1bfb030523f90cdec437fb9cedcc360c67a6636fef90667b5e371db00b3` |
| [38370077685](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38370077685) / `fbd8ca92cb4b06e41733aef9b37b6000a87411b7` | 6/6 passed | `11799051978` / `4fe23ec397281818145625bb661754de8bd10155f9b8543d807099b00a3804de` |
| [38374437544](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38374437544) / `6878f37c95db64451a27e18f3d4f9607ae3b6273` | 6/6 passed | `11801156058` / `ae170fdb0a3edb880404cadb667aeff4a330f3760318c55fd0a1801b5b0dbf62` |
| [38376148746](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38376148746) / `bb43e61085e8fe7819a77fdfda574dce058ba2af` | 6/6 passed | `11800869288` / `60409f21f7c49acef75157b5efcfa0741bf2871c0990a304956da3b63b6154e2` |
| [38376685536](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38376685536) / `27b5be81acbca4fc51f42078c7e93cc083510522` | 6/6 passed | `11801779273` / `56601afa8f0d07fb6d15a7ebfb108feb5ad112a0c3ea212cb2c7cafd546a4212` |
| [38378838696](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38378838696) / `047f9eff62c67ecc3a32a6c4d82a19dc75801c30` | 6/6 passed | `11802622103` / `c245396e5e37845a24da641f76803862e451f2db1451da0a8533cbaa87d45cfa` |
| [38379701908](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38379701908) / `72a76a741de3859082b490982e78d4b1e6220251` | 6/6 passed | `11803441530` / `089a64de3e9eb54fec41bd3ec9d864343ddf6c8d82c286807ed4e5c48486d43c` |
| [38381427039](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38381427039) / `f0c32c2b42d8369e9855151e7b2b21e809d1ed7e` | Cancelled; AppHost report absent | `11803721869` / `2f4739c5acacbc9bc48318ea3926d96ac43e560b97d9f1e6e48a7b3de97eb941` |

The reports were checked against native SQL identities with the existing strict TRX verifier, including counters and result/definition/execution bindings. The register preserves the cancelled run rather than selecting only successful artifacts. Successful failure-only observers emit no transition log, so absence of such output is not evidence about intermediate Worker states.

## Controlled contrast

| Arm | Action | Independent required observations |
| --- | --- | --- |
| Worker configuration failure | Override only Worker's provider with an invalid synthetic value before graph startup | Native Worker reaches Finished with exit 2; exact pinned DistributedApplicationException and unavailable message from the explicit fail-closed health wait; recorder retains first Worker-unavailable observation with that state/exit |
| Database loss | Reach actual Worker readiness, then stop the disposable database | After database stop, observe the same Worker process generation Running without exit and explicitly Unhealthy, plus trusted HTTPS alive 200 and health 503; only then start a fresh health wait and separate two-second cancellation, requiring that cancellation token to fire while the three-minute case token remains active |

Both PostgreSQL and SQL Server are required. Keep three-minute case deadlines, normal TLS, no retries and existing original profile assertions. The two-second cancellation belongs only to the discriminator; it does not alter production or ordinary readiness limits. Record only allowlisted state, exit/PID, health and fixed stage labels. Require observer-failed=False and joined=True; retain publication-drain=not-confirmed because consumer timestamps do not prove publisher ordering.

The successful-action observer is intentionally silent. The contrast must explicitly retain the captured log after the observer joins and independently assert the expected record, rather than infer state from the selected arm. Start lifecycle observation before StartAsync. Dispose resources in finally and attempt removal verification even after startup, capture or assertion failures. Missing capture or incomplete cleanup remains an explicit failure. After the database-loss wait is cancelled, independently verify the same Worker process generation still runs without an exit; any Worker-unavailable transition is contradictory evidence. A cached pre-stop Healthy snapshot cannot satisfy the post-stop precondition. Preserve primary failure and cleanup failure as sanitized stage/type evidence; no exception payload, connection settings or raw resource snapshots are retained.

## Proposed delivery and validation

Preserve the existing AppHost test project and Features hierarchy; proposed file placement is awaiting the user's pattern preference. Two permanent theory scenarios with both-provider rows would add two cases/provider to the existing matrix, without removing any case or increasing an invocation/job deadline. Update every independent selection/count oracle together; the new cases prove availability/diagnostic semantics and do not count as an incident correction. No new production code, dependency, host or schema is justified by current evidence.

Architecture/Aspire design review accepts the contrast with the execution-bound and cleanup requirements recorded above. Independent test review verified all 16 artifact hashes/run bindings and strict report reconciliation, and required the post-stop Unhealthy/same-generation precondition to exclude cached readiness. The user's placement preference remains pending before implementation. Before merge: strict native identity comparison, pure controls, full static/quality checks, actual provider execution under CI child-process instrumentation, preserved original profile results and exact-head required checks. Record all local/CI outcomes and reviewer dispositions. A packet/diagnostic PR can close bounded T033 delivery while ADM-OPS-01 remains open.

## Outcome

Pending controlled execution and final source/CI review. No root-cause fix or incident closure is claimed. Local documentation-scope selection controls, AI instruction integrity, assurance-register integrity and 27 assurance controls pass; these are documentation checks, not new provider executions.
