# B2 planning and future implementation evidence

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
