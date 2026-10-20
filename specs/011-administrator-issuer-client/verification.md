# B2 verification and historical planning evidence

**20 October implementation reconciliation:** the [B2 acceptance map](b2-acceptance-map.md) binds FR-001–013, SC-001–006 and T005–T026 to concrete source and controls. [Final boundary evidence](native-budget-replay-and-restore.md) adds native quota/expiry/PAR contention and scoped restored-state consequences. Final exact-head runtime/coverage/mutation review and conditional normal squash remain pending; no production, historical or human/release gate closes here. The dated records below preserve their earlier planning/prerequisite scope.

## Historical planning checkpoint

15 October 2026 (Europe/London). This packet implements feature 010 T022's planning obligation. **B2 runtime scenarios remain not_run; the later prerequisite section records dependency/API work only.** B1 remains accepted in PR #52 (`ca410c01`), recorded by PR #54 (`b17e4a7`). This file cannot self-certify its future planning commit; the delivery PR records exact checks/head/squash.

## Verified starting point and authority

Remote main `b17e4a7969c4b0e437f14b1e8f25a4d1f0ae93f7` was verified; the local worktree was clean and a separate `053-admin-issuer-client` branch preserves existing feature/diagnostic work. The user approved continuing with B2 after the completed B1 delivery. Approved v0.2 and AGENTS govern routine work; no expired diagnostic exception is invoked. All six historical findings and 48 human rows remain open.

The managed runtime reported running/connected, current observations and enforced policy with both requested Microsoft regional hosts. The policy file agrees. No network workaround, image pull, SQL experiment or production configuration change occurred. Local Docker capacity remains insufficient for native work; this planning packet uses source/document evidence only.

## Spec Kit and source research

Used the installed pinned Spec Kit 1.1.3 Codex skills workflow: specify, clarify, plan, checklist, tasks and read-only cross-artifact analysis. Resolved the active spec/checklist/task templates, persisted ignored local feature selection, ran `setup-plan.sh --json`, `setup-tasks.sh --json` and prerequisite/path checks. Feature directory `specs/011-administrator-issuer-client` is independent of branch `053-admin-issuer-client`; local selection state is not a published change. No `.specify/extensions.yml` exists, so no registered pre/post hooks apply.

Read approval/constitution/roadmap/handoff, approved PRD/architecture/protocol/data/assurance contracts, feature 010 tasks/plan/research/delivery and actual issuer/account/session/governance/BFF composition. [Research](research.md) records pinned upstream source evidence and gaps. Independent architecture/test/API and protocol/security researchers confirmed the distinct administrator path and identified missing client/core registration, mandatory profile enforcement, assertion replay/time policy and default client HTTP retries.

Clarification scanned scope, data/lifecycle, interactions, nonfunctional bounds, dependencies, failure modes, vocabulary and acceptance. No user decision was missing: the bounded noninteractive subset and existing security ceilings resolve the design. It does not relax the pilot or promise B3 browser/refresh/API behavior.

## Planned control traceability

Identifiers below were read from the pinned [ASVS 5.0.0 catalogue](../../docs/assurance/asvs-5.0.0.csv) and [control register](../../docs/assurance/controls.csv), not invented. These are candidate B2 contributions; **all control results remain not_assessed**. The existing 345-row register is unchanged. Protocol contracts additionally follow OIDC Core authorization/code/ID-token validation, RFC 7636 (PKCE), RFC 7523 (JWT client authentication) and RFC 9126 (PAR). Product strict replay/expiry policy may exceed a library default.

| Version-qualified control IDs | Scoped obligation | Planned scenarios/tasks |
| --- | --- | --- |
| v5.0.0-8.3.1, v5.0.0-8.3.2 | Trusted current authority and committed-change effects at issuer admission; not live API completion | B2-ADM-01–05; T007–012 |
| v5.0.0-9.1.1, v5.0.0-9.1.2, v5.0.0-9.1.3 | Native signature, fixed algorithm and trusted key source | B2-PROOF-01, B2-CLIENT-02; T015/017/019/021 |
| v5.0.0-9.2.1, v5.0.0-9.2.2, v5.0.0-9.2.3 | Explicit lifetime, token purpose and audience | B2-PROOF-01/03, B2-CLIENT-02; T009/015/017/018/021 |
| v5.0.0-10.4.1, v5.0.0-10.4.3, v5.0.0-10.4.4, v5.0.0-10.4.6 | Exact callback, one-minute code ceiling, only needed grant, S256 | B2-REG-01/02, B2-PROOF-02/03; T014/016/018 |
| v5.0.0-10.4.2 | Native single-use code and linked issued-token entry revocation on valid replay; no offline API immediate-revocation claim | B2-PROOF-03; T018/024 |
| v5.0.0-10.4.10, v5.0.0-10.4.11, v5.0.0-10.4.12, v5.0.0-10.4.13, v5.0.0-10.4.16 | Fixed admin backchannel authentication, exact scopes/query mode, mandatory PAR and replay-resistant asymmetric proof; profile-only contribution | B2-REG/PROOF; T014–018/024 |
| v5.0.0-10.1.2, v5.0.0-10.2.1, v5.0.0-10.5.1, v5.0.0-10.5.2, v5.0.0-10.5.3, v5.0.0-10.5.4 | Native transaction/state/PKCE/nonce, stable subject, exact issuer and ID-token client audience | B2-CLIENT-01–04; T019–022 |
| v5.0.0-1.3.6 | Fixed outbound issuer origin/paths; no discovery/redirect SSRF fallback | B2-CLIENT-02/04; T019–022 |

Sender-constrained token and public-client refresh requirements, broader platform applicability, manual/independent assessment and certification remain unresolved release work; B2 uses the approved signed bearer contract and does not introduce DPoP/mTLS or claim whole ASVS L3 compliance.

## Review and analysis disposition

Initial research findings were incorporated before the packet review: distinct profile composition; full session plus current permission admission; strict freshness equality; canonical issuer assertion audience and native type; durable assertion ledger distinct from native replay; mandatory PAR/S256 without fallback; separate client assertion/state keys; explicit removal of native automatic mutation retries; test-owned callback versus B3 activation.

Independent architecture/test/API and protocol/security packet reviews passed all 12 custom checklist items after two substantive requirement corrections:

1. Storage budgets now count retained assertion tombstones and all B2-created native server token/authorization rows, with explicit live/retained client-state/PAR caps, serialized creation/reservation, dependency-aware cleanup and partial/unknown-commit reconciliation. The old live-versus-retained ambiguity is removed; failed cleanup cannot authorize live eviction.
2. Issuance linearizes at successful native code/token persistence commit. Current authority and relevant native persistence must serialize with supported invalidations; invalidation committed first denies issuance. T005 must prove scoped-context/handler/lock ownership, not choose weaker semantics. Network response delivery remains non-atomic and cannot trigger automatic mutation retries.

Review also clarified retained state signing-validation keys alongside decryptors, strict freshness equality and pinned source citation extent. No remaining planning blocker was reported. At the planning review, T004/T005 feasibility and every runtime criterion were unexecuted; document review did not check them off. The later native dependency continuation below records T004 progress while T005 remains open.

Read-only cross-artifact analysis found all 13 FRs and six SCs mapped to 27 unique, sequential tasks, coherent story dependencies and no unresolved critical/high inconsistency after correction. No runtime task is marked complete. No constitution exception or new approval is proposed.

Local checks passed: seven scope controls, AI instruction consistency (eight skills/eight reviewers), assurance integrity (345 unchanged not_assessed controls), 27 assurance controls, relative Markdown file links, task inventory, all 25 referenced version-qualified ASVS IDs against the pinned catalogue and whitespace checks. The entire packet is prose and uses the existing fail-closed scope selector. Hosted exact-head proportional checks and squash must be verified on the planning PR before delivery; no application build, package restore or native B2 execution is claimed.

## Runtime evidence ledger

| Evidence | Status |
| --- | --- |
| Client package locked resolution / native API integration | T004 local restore/build and seven API/transport controls pass; T005 transaction/deadline proof remains open; see [inventory](native-integration-inventory.md) |
| Both-provider B2 admission, registration, replay and failure cases | not_run; T011/T012/T018/T021/T022 |
| B2-specific mutations and all existing suites | not_run; T024 |
| Exact executable-head full CI / coverage / runtime reviews | not_run; T025 |
| B2 implementation squash | not_run; T026 |
| Product browser/BFF session/API/React/refresh activation | outside B2; B3/C/D/E remain open |


## First planning CI and scanner correction

PR #55 head `21621c64c6abae583dbb6519c4393ec1fdd47d1b`, run `38782971396`, passed scope and assurance checks but failed the source-secret scan and final acceptance. All eleven full-suite jobs were intentionally unselected for prose. The exact pinned Gitleaks 8.30.1 archive was verified against workflow SHA-256 `551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb`; a local archive of that exact head reproduced one generic-api-key finding at T018's prose description. The detector matched ordinary wording about registration-key drift and cancellation, not credential material. Rephrased that sentence without changing its test obligation; no scanner rule, allowlist or workflow was changed. The failed run is retained. The corrected complete staged source snapshot passes the same pinned scanner with zero findings; exact corrected-head proportional CI remains required before merge.

## Native dependency continuation

Branch `054-admin-protocol` starts from verified PR #55 planning squash `98139cc942467f4094434691c30668ba72dbeb70`. [Native integration inventory](native-integration-inventory.md) records T004's pinned dependency resolution, advisory/license checks and actual handler/context/transport controls. T005 remains open for scoped native cancellation and transaction/rollback proof; no production path is wired and no B2 acceptance is claimed. The initial local test build exposed a missing namespace import, formatting errors and a dangling generated-output symlink; those failures are retained locally and corrected without changing earlier evidence archives. This paragraph does not certify future exact-head CI or merge.

Local executable validation: Host project/reference build passes with zero warnings/errors; all 400 Host cases pass with zero skips. The seven new prerequisite cases include a four-case negative retry control that fails at the exact one-versus-two request count, followed by byte-exact restoration and 7/7 passing controls. The matrix changes only Host 393→400. No both-provider B2, new B2 mutation, real TLS/redirect or native protocol-exchange acceptance is claimed. Full exact-head CI remains required for this dependency/test checkpoint.

PR #56's first full run `38787608672` exposed an import-order error and newly reported MessagePack advisory GHSA-qhrr-8q5h-9q3h. The [inventory](native-integration-inventory.md#first-full-ci-failures-and-corrections) records the exact failed head, retained hosted artifact, AppHost-owned 2.5.307 patch propagated across all five affected graphs, fresh advisory/license checks and unchanged acceptance obligations. No initial run is called passing; corrected-head full CI remains required.

## A-007 initial-phase continuation — 15 October 2026

The user explicitly requested roadmap implementation and temporary Firefox exclusion. [A-007](../../docs/amendments/A-007-initial-phase-browser-scope.md) records the approved initial scope and restoration boundary. Source begins at PR #56 head `8578441753d847a49d3f2de0137152ad5fe88c2e`, main `98139cc942467f4094434691c30668ba72dbeb70`. Seven Firefox jobs are deferred (93 ordinary cases and four separate history diagnostic executions); all test sources and prior failures remain intact. All full runs now require 48 coverage inputs and unchanged nine floors. Full applicable exact-head verification and independent review remain required before prerequisite delivery. T005/B2 and every historical/human/activation/release gate remain open.

The separate diagnostic sources `8eea5e3f` and `f24be452` remain frozen and do not merge into this prerequisite. The latter native case passed but document identity was unavailable, so its evidence gate failed; no correction is claimed. [PR #56](https://github.com/mosesparadise/OpenIdentityAdmin/pull/56) retains run/artifact/hash dispositions and the upstream draft. Upstream submission was rejected with HTTP 403; no issue was created.

Local A-007 verification: all 192 Python controls pass, including exact two-engine/contract selection, rejection of a missing whole engine, and removal/empty evidence controls for each of 48 inputs. Instruction integrity, all 345 assurance-register entries and whitespace checks pass. Independent architecture/test and security reviews passed the corrected source; their initial WebKit-omission finding was fixed by requiring both active engines on every full run. No native browser/database run was performed locally. Full hosted verification remains pending at this source checkpoint.


## PR #56 prerequisite delivery — 15 October 2026

[PR #56](https://github.com/mosesparadise/OpenIdentityAdmin/pull/56) was normally squash-merged as `2cfdb55bf128503c5c1b1451d1becad2219430aa`, sole parent `98139cc942467f4094434691c30668ba72dbeb70`. Exact head `8c0b9aed9bef948fc9a754d00f7b54b9145e1006` and tested merge `78c4a2652d58d44d20a27117972abc2c558a212d` share tree `a37c3f3dec781404307135cb70803f04da0b1515` with that squash. [Run 38804409013](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38804409013), attempt 1, passed all 63 applicable jobs including final acceptance. The live review/restriction state was checked and no bypass used.

T004's native dependencies/API/transport controls and the MessagePack 2.5.307 advisory patch are delivered. A-007 governs the initial phase: all 12 Chromium/WebKit browser cells and three contract partitions passed. Seven Firefox jobs remain deferred, not passed. Failed earlier runs and both frozen diagnostic branches remain retained; no Firefox correction or historical closure is claimed.

Independent architecture/test review reconciled raw unit evidence: 491 passes (25 Core, 400 Host, 5 Architecture, 61 AppHost), no failures/skips/errors, including all seven native prerequisite identities. It matched all 48 unique coverage merge paths to current-head artifact records and independently recomputed the merged XML's package rates and global 6,393/6,691 lines and 879/901 branches. The existing diagnostic Governance.Driver appears in the aggregate but is not one of the nine unchanged product floors. Individual raw reports for all 48 inputs were not separately downloaded.

Coverage artifact `12006587735`: ZIP SHA-256 `fee04cd59b0b8dac8032701df25e4315535d0808a4b15f86e608c970c6969ca5`; raw XML SHA-256 `d4dbb4f7c1bde2688bd22f25a070b0c00c0dec021f43ea9e39bd49a1c862e14d`.

| Required module | Lines | Branches | Floor |
| --- | ---: | ---: | --- |
| IdentityPlatform.Core | 98.9% | 100.0% | Pass |
| IdentityPlatform.AuthServer | 97.3% | 100.0% | Pass |
| IdentityPlatform.AppHost | 99.5% | 100.0% | Pass |
| IdentityPlatform.Infrastructure | 94.6% | 97.6% | Pass |
| IdentityPlatform.Migrator | 99.0% | 100.0% | Pass |
| IdentityPlatform.Admin.Bff | 94.9% | 95.4% | Pass |
| IdentityPlatform.ServiceDefaults | 100.0% | 100.0% | Pass |
| IdentityPlatform.Admin.Api | 100.0% | 100.0% | Pass |
| IdentityPlatform.Worker | 100.0% | 100.0% | Pass |

Independent security review verified all six mutation ZIP digests, exact source/manifests/recomputed mutant hashes and native case identities: 42 expected kills, 42 baseline passes, 42 restored passes and zero skips. These retained sanitized receipts are not raw TRX, and these are existing suites rather than new B2-specific mutation coverage.

| Mutation suite | Artifact | Kills | ZIP SHA-256 |
| --- | --- | ---: | --- |
| governance-mutations-PostgreSql | 12006606133 | 6 | `66002404f428aac86bdb6c144b7ce1e4f279b0f33860684709e6c512616ab0e7` |
| critical-mutations-PostgreSql | 12005848573 | 7 | `ee4157c6e760d8592220d1b313eaf456c6cd5b71c3dc6405e3c583928df6680f` |
| administrator-mutations-PostgreSql | 12005024024 | 8 | `094bc777d0159f4df9031a570b9382ee6a67a74a643177cc46f76ed028b21893` |
| administrator-mutations-SqlServer | 12006372441 | 8 | `00adfa105d3faad91280e6c948e7251a237453381c7b783f7681577b2f97d9ec` |
| governance-mutations-SqlServer | 12006212279 | 6 | `9890b2aa9c7dcf945c0227ffd8e53e12945856e686658c8800484d081dd02b36` |
| critical-mutations-SqlServer | 12005044417 | 7 | `b3b021004fbc78d6de73b469973cea6b1c51ccc9b5e83ebe991ff1a87b3bf8cf` |

Supply-chain artifact `12006090707` (ZIP SHA-256 `572f49cff17dc9668984296761b8be94ebd81f90138c8fe1ac98d5d79b5bb9d2`) covers 23 projects with zero reported advisories and 252 package/version/license pairs reconciled to locked hashes and project membership. Independent architecture/test and security reviews passed this prerequisite's exact source and evidence. Automated review does not replace human/external assessment.

The separate [T005 native-store prerequisite](native-store-ownership.md) adds four installed-manager controls and raises Host discovery to 404; those cases are not part of the PR #56 run above. Local complete Host evidence passes 404/404 and preserves all prior 400 native case display names; 192 Python controls, code quality, instruction integrity, assurance-register integrity and the staged secret scan pass. Failed local setup attempts and their corrections remain recorded in that prerequisite. Its own full exact-head CI is required before delivery.

T005 and B2 acceptance remain open. Next: implement the reviewed opt-in cancellation/operation owner while preserving native getter/enumerator behavior, then prove exact transaction participation and rollback on both providers for Platform and Sessions contexts and actual native dispatcher paths. All six historical findings, 48 human rows and activation/public-release gates remain open. Restore Firefox at T028 before the A-007 boundary.


## Native ownership implementation candidate — 16 October 2026

[Implementation and verification boundary](native-ownership-implementation.md) records the opt-in owner, four adapters, 27 new pure cases and four actual provider/store fixtures. Local complete Host 457/457 and code quality pass. Exact-head CI, both-provider native execution and final evidence review remain required; this paragraph does not certify future results. T005 and B2 remain open.

## Native ownership prerequisite delivery — 16 October 2026

[PR #60 delivery evidence](native-ownership-delivery.md) records normal squash `a4db4228026d2b02cd1d4ba38995fe8e84384752`, exact source/run/tree bindings, 63 successful jobs, 552 unit cases, 22 native foundation cases per provider, 48 coverage inputs/15 floors, 42 existing security mutation kills and two separate local negative controls with their limits. Independent architecture/test and security reviews pass the bounded scope. The earlier failed import-formatting run remains recorded. T005/B2 stay open for shared multi-scope request lifetime, actual dispatcher cleanup, early durable proof/redemption, quotas and issuance linearization. All historical/human/activation/release gates remain open.


## Dormant response and native client scope delivery — 17 October 2026

[PR #64 delivery evidence](response-boundary-delivery.md) binds source `3354384`, full run `39072758180`, tested merge `578d611` and normal squash `4e0e5c4` to the same tree. All 63 jobs pass, with independent raw review of 644 unit and 44 native cases, all 48 coverage inputs/15 floors and 42 existing mutation kills. The new controls include six real Kestrel deadline/completion cases and actual native client-service-created scopes. The source remains dormant; complete native interoperability, early durable proof/redemption, quota reservation and issuance linearization remain open under T005. All historical/human/activation/release gates remain open; A-007 still governs Firefox restoration.
