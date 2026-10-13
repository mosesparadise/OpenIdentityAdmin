# T034 — bounded Firefox history evidence reconciliation

Status: bounded reconciliation complete; independently reviewed; ADM-EVID-01 remains unresolved and acceptance/public-release-blocking. This is evidence reconciliation, not a browser implementation change. Expired A-003 supplies no runtime authority.

## Original failure

Main run 38355735749 at source 8792f85 failed the Firefox/SQL Accounts history-readiness control. Original artifact 11793083103 was downloaded again; SHA-256 matches `5a43813dd6927ef6f3846ee2c366d7107b12d546b1f7c6c605a035b17e373648`. The ordinary report has eight results with one failure; its whole-case duration is 13.712 seconds. The first positive five-second readiness observation timed out after the 250 ms incomplete-document control passed. The report retains ErrorInfo but no StdOut recording fulfillment/load/phase state. Whole-case duration does not identify the positive-phase mechanism. Concurrent startup is an observed context, not a demonstrated cause.

## Fixed retrospective cohort

The bounded audit includes every selected run from accepted T032 run 38368154130 through the last code post-merge run 38381427039, including its cancellation. All eight Firefox SQL artifact ZIP hashes, sizes and run/head bindings were checked. Independent review confirmed strict native identities, result/definition/execution bindings, counters, fixed completion/window markers and cleanup. The parallel Worker audit checked the corresponding SQL foundation artifacts, for 16 ZIPs and 45 extracted members in total.

Seven complete runs passed eight ordinary Firefox SQL Accounts cases and both fixed diagnostic arms. The cancelled final run passed only the isolated arm, recorded overlap=started, and has neither overlap completion nor an ordinary Accounts report. It supplies no complete acceptance. Outcomes markers were also checked for consistency; no cancelled or missing report was converted to success.

| Run / source | Isolated positive interval | Overlap positive interval | Ordinary Accounts | Artifact / SHA-256 |
| --- | --- | --- | --- | --- |
| [38368154130](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38368154130) / `f3d229e1862ef75fb5802b424e301f1ca654914d` | 14.060 ms | 64.470 ms | 8/8 passed | `11798436638` / `71eb32fcf20f2c5c985898cc447f44417f9588f481e9761c0313167da5111b58` |
| [38370077685](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38370077685) / `fbd8ca92cb4b06e41733aef9b37b6000a87411b7` | 34.040 ms | 85.429 ms | 8/8 passed | `11798614703` / `1e3391f120e8f869fd64dd4785092802dac28499c22bb382d4f59d11bcdad8af` |
| [38374437544](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38374437544) / `6878f37c95db64451a27e18f3d4f9607ae3b6273` | 14.189 ms | 47.633 ms | 8/8 passed | `11801291020` / `0e1ff1c12fc248312411ac1fd9599bebf69735e1f394f0d834dd471dd3fa7543` |
| [38376148746](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38376148746) / `bb43e61085e8fe7819a77fdfda574dce058ba2af` | 14.876 ms | 42.274 ms | 8/8 passed | `11801503284` / `af579d9a437339ec2496304fe875df12730a4c7e0db10326606e41a117056882` |
| [38376685536](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38376685536) / `27b5be81acbca4fc51f42078c7e93cc083510522` | 30.757 ms | 30.310 ms | 8/8 passed | `11801863971` / `4476ca12c24e88842253a763019d2179b06776edefb623019824c275967baf5f` |
| [38378838696](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38378838696) / `047f9eff62c67ecc3a32a6c4d82a19dc75801c30` | 33.491 ms | 68.991 ms | 8/8 passed | `11802427728` / `f16f669b7f663239ca9b3a1a90c97cd5ddfe2c32997b2932f9b499f0a6099ff3` |
| [38379701908](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38379701908) / `72a76a741de3859082b490982e78d4b1e6220251` | 12.059 ms | 117.316 ms | 8/8 passed | `11803223600` / `480968f92ce54fbbfb3d55403a0559c66213c5eb635d7d7d67797bbab71ebac8` |
| [38381427039](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38381427039) / `f0c32c2b42d8369e9855151e7b2b21e809d1ed7e` | 32.058 ms | Incomplete | Absent | `11803597258` / `85dd910a15b188d5c5f438b36cc7c792faa3cc896fa7968292a534edf8da22f7` |

Every listed execution is attempt 1. This retrospective cross-commit cohort is descriptive and does not estimate a randomized failure rate. It does not erase the original failure. Artifact references expire on 20 October 2026 UTC and are not a permanent archive; preserve approved protected evidence before expiry where raw reports are required for later assessment.

## What these observations establish

The successful fixed arms establish accepted delivery and safe-document observation under their own measured conditions. Each overlap arm verified an actual interval from database Running to graph readiness intersecting the positive observation, and completed graph cleanup. The isolated arms started no graph. Ordinary browser acceptance remained separately mandatory; fixed diagnostics supplied no substitute case credit.

The positive windows listed above are measured positive intervals, not whole-case timings or historical timeout estimates. Retained final readiness counters are emitted after cleanup: fulfillment/load activity can advance during cleanup, so fulfilled=True or a positive load count alone cannot prove completion before an earlier failure deadline. Successful acceptance additionally depends on the unchanged absolute budget, repeated safety checks and strict completion markers. On a future failure, the Delivery versus Inspection phase can narrow location, but Inspection does not distinguish the first and second safety inspections; final counters supply no per-event timing.

The fixed overlapping workload starts the default AppHost graph. Ordinary Accounts execution also includes account-suite activity. Passing the fixed overlap does not exclude contention specific to the original concurrent workload, browser scheduling, driver delivery or another mechanism. These remain hypotheses, not findings.

## Bounded disposition

No retained run reproduces the original failure or supplies the missing original phase/route evidence. No new browser code, retry, serialization workaround, larger timeout or repeated identical incident experiment is justified by this audit. Continue the existing fixed and ordinary regression checks on future prescribed CI executions. Preserve the original failure and stop additional incident-specific repetitions unless new evidence distinguishes a hypothesis.

If a natural recurrence leaves ambiguity about delivery versus observation, consider a reviewed, bounded diagnostic addition capturing monotonic fulfillment/load and first/second-inspection milestones before cleanup. Do not add raw DOM, secrets, unrestricted traces, relaxed TLS or longer acceptance budgets. Any proposed correction still requires a demonstrated mechanism and matching permanent regression; green subsequent runs alone cannot close ADM-EVID-01.

## Review and acceptance boundary

Independent test review recomputed hashes, compared extracted report bytes and reconciled all accepted native cases. Architecture/test reviewers agree that successful diagnostics establish neither historical causality nor whole MFA acceptance. T034 can complete as a bounded evidence delivery after its own reviewed documentation checks and normal squash. ADM-EVID-01, ADM-OPS-01, human T028/T029 observations, T020/T021 sequencing and public-release gates remain separate and open. A new exception must be explicit and concrete; this record grants none.

## Prescribed regression during T033 delivery

PR #44 source `3154330b218ad27946472544a96ded54cc250d6d` ran the existing mandatory comparisons in run [38386343247](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38386343247). This was its ordinary exact-head validation, not an additional incident-specific rerun. Both providers passed both fixed Firefox arms and all eight ordinary Firefox Accounts cases; strict report/digest/run-head reconciliation passed. The full workflow and PR merge are recorded separately. These executions supply measured subscope evidence without changing the retrospective cohort or establishing historical causality.

| Provider | Isolated / overlap positive interval | Artifact / SHA-256 |
| --- | --- | --- |
| SqlServer | 14.091 / 83.702 ms | `11807516829` / `94a97040ba7cee352bdb87486b6676a9dbbcfa0467682ed66ed015ab1166dfd6` |
| PostgreSql | 12.173 / 73.803 ms | `11807431060` / `1d0ccb529c714d6f532d3717b7bdb50a6024b772c303976876f96ae0c2063158` |

The prescribed T033 full workflow `38386343247` ultimately failed the separate SQL/WebKit unrelated-certificate evidence control. Its successful Firefox subscopes remain descriptive evidence only; they do not make that workflow clean acceptance. See the T033 packet for the retained artifact, bounded diagnostics and unresolved certificate finding.


Final T033 diagnostic head `15c6e0c1e789ba9114874d7e692dd1cca745ae0a` passed all 62 required jobs in [run 38390215923](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38390215923), and PR #44 was normally squash-merged as `65031d7`. Both providers again passed all eight ordinary Firefox Accounts cases and both fixed arms. Independently verified final artifacts: SQL Server `11808757652`, SHA-256 `cde15ab9d8663a54af753718446595d0c8cbb461fe028826c2d9f66947658435`; PostgreSQL `11808493618`, SHA-256 `5da7ec18b647b4a4ca76ed993a39c2b29bcfc09db2e511677bed6460cf14db9b`. Strict native bindings, result counters and completion/cleanup markers passed. These prescribed results do not enlarge the fixed retrospective cohort or establish a historical cause.
