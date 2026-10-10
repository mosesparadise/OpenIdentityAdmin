# AI instruction evaluation — 10 October 2026

Eight project skills and eight reviewer TOMLs are installed. `python3 scripts/check-ai-instructions.py` passed: file counts, required front matter/output/handoff, parseable TOML, inherited model selection and requested read-only reviewer mode. This validates structure, not enforcement of the host sandbox or native CLI discovery.

Four independent subagents read the actual reviewer configurations, referenced skills, relevant approved requirements and `docs/engineering-review-fixtures.md`. The main agent owns edits. These were disclosed synthetic, bounded instruction evaluations; reviewers did not execute the snippets or tests and were not blinded to the fixture design. The host delegated agents explicitly read the instructions; this does not prove automatic discovery by every Codex client.

| Reviewer | Seeded faulty case | Described correct case | Real system verdict |
|---|---|---|---|
| Architecture | Changes required: dependency inversion and HTTP-coupled Core | Pass within snippet scope | Insufficient evidence |
| Security | Changes required: anonymous permission mutation | Pass; distinguishes managed-client secret from BFF OAuth credentials | Insufficient evidence |
| Tests | Changes required: fake provider matrix and skipped SQL result | Pass for real-provider/failing-prerequisite description | Insufficient evidence |
| API | Changes required: bearer contract/storage in React; conditional compatibility issue | Pass for separate BFF contract | Insufficient evidence |
| Performance | Changes required: weakened checks and unsupported revocation claim | Pass for correctly bounded measurement description | Insufficient evidence |
| Compliance | Changes required: unsupported certification and skipped-as-passed reporting | Pass for honest internal-candidate labeling under A-001 | Insufficient evidence |
| UX | Changes required: keyboard/name, color-only consent and stated contrast defects | Pass for accessible-control description | Insufficient evidence |
| Aspire | Changes required: validation bypasses, fixed sleeps, shared volumes and unsafe retries | Pass for bounded isolated composition description | Insufficient evidence |

All eight identified their seeded defects, accepted the stated correct controls within scope, and preserved uncertainty. This is limited evidence about instruction behavior; it is not a quantitative reliability study, application acceptance, independent security assessment or certification. Repeat with additional realistic unseen regressions as product features are implemented.

## Actual foundation review and dispositions

Architecture/test and security reviewers separately inspected the evolving implementation, read-only. Their findings are distinct from the fixture evaluation:

- **SEC-001:** unredacted trace state. Confirmed by a failing regression; processor now removes it. Exporter-level tests also verify safe-span redaction and suppression of immutable event/link/baggage payloads.
- **SEC-002:** malformed bootstrap boolean escaped safe handling and echoed input. Actual child-process regression failed before the fix; explicit `TryParse` now returns safe exit 2. Before run: 2 failed/8 passed; after run: 10 passed, later expanded to 43 host tests.
- **ARC-001:** promised passkey table absent from Identity V1 defaults. Runtime and design-time options now select V3. Unpublished initial platform migrations were regenerated at V3; model parity and native manager storage are tested. No existing user deployment or database was changed.
- **TEST-001:** direct EF migration calls bypassed the product migrator. Persistence now exercises `DatabaseUpgrade.RunAsync`; tests cover partial failure/recovery, repetition, cancellation and safe logs. Aspire executes the real migrator process.
- **TEST-002:** BFF isolation denial could pass because credentials were broken. Tests now first prove session readiness and read/write, then require provider-specific privilege errors for cross-schema access, DDL and history mutation.
- **TEST-003:** missing scope-case, concurrency and fresh-scope account evidence. Added native scope-name cases, competing manager updates and fresh-scope user/passkey reads.

PostgreSQL execution supports these storage dispositions; SQL Server remains a required unverified matrix cell locally. Production/issuer acceptance does not follow. Follow-up security review confirmed the failed-provisioning regression: verbose PostgreSQL statement logging exposed a synthetic password before the fix; the strengthened test failed once before and passed after dedicated unpooled bootstrap connections suppressed statement/context logging. The positive control waits for the actual schema error. Transaction sampling suppression was then added and its test enables sampling globally. The subsequent SQL Server CI log regression passed using a post-failure capture control; its scope is the normal server error log, not arbitrary auditing/tracing.

Follow-up test review prompted strict TRX completeness and outer process deadlines; actual captured container/process teardown assertions; asynchronous builder disposal; explicit startup cancellation; and dependency/license/secret CI checks. Review also found that missing process metadata could silently weaken cleanup evidence; running/unknown states now require a positive PID, with negative regression cases. Python verification-gate tests currently pass: four TRX cases, eight advisory/license cases and three per-module coverage cases. The three existing PostgreSQL distributed cases passed with strict lifecycle assertions. The cancellation test initially awaited intentionally held startup to finish; review of the pinned Aspire factory confirmed the harness error, and it now cancels the concurrent startup wait before disposal. The corrected PostgreSQL case passed in 15 seconds, including captured-resource removal. SQL Server CI subsequently passed native persistence and provisioning-log controls, but found an outage-readiness deadline defect; the direct bounded history query fix is being verified. These dispositions are implementation evidence, not full feature acceptance.

The second SQL CI run passed the readiness regression and all three persistence scenarios. Three distributed scenarios passed; in the fourth, exact container/process cleanup assertions passed before a redundant EF CanConnectAsync probe timed out against removed storage. Independent test review approved deleting that probe while retaining the direct lifecycle evidence. The final clean matrix is pending. A diagnostic merge of the second run's hashed artifacts meets every module coverage floor, but does not convert that failed workflow into acceptance.
