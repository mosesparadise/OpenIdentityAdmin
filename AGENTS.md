# IdentityPlatform repository guidance

Version 0.2 was approved by the requesting user on 10 October 2026; see docs/approval.md. These instructions are active. Preserve pinned Spec Kit integration files.

## Authority and delivery

Use squash merges for pull requests, as requested by the user. Keep one coherent commit per PR on the target branch; intermediate review commits may remain on the feature branch. Verify the exact head, required checks and review state before merging. Do not rewrite shared history or bypass GitHub review restrictions. Maintain docs/roadmap.md and each active Spec Kit task list so completed work, remaining work, dependencies and external gates stay visible.

The approved PRD/ADRs and recorded amendments govern scope. This is a greenfield OpenIddict platform; no Duende import/cutover work is included. Use the pinned GitHub Spec Kit Codex integration and its installed invocation syntax. Complete ordinary approved work autonomously. Propose a concrete amendment for material scope/security changes. Do not begin implementation without the document approval record. Production deployment, certification publication/payment and external declarations require their own authorization.

## Engineering and security

Organize use cases under Features within projects; keep composition/generated artefacts as documented exceptions. Use OpenIddict/Identity managers and the named authentication schemes. Minimal APIs supply application protocol decisions; the library owns protocol validation/response cryptography. React uses Admin BFF cookies and never holds OAuth tokens. Preserve current API permission checks, audit atomicity, concurrency and both-provider semantics.

Use stable, pinned compatible dependencies, explicit management API versions and one authoritative OpenAPI contract per API surface, with React generated from the BFF browser contract. Keep protocol routes separate. Apply shared slate/indigo light/dark tokens across React/Razor. Aspire is the local/distributed test harness. Do not add blanket retries to security mutations.

## Verification and reviews

Every feature links FR/NFR/control IDs to scenarios, tasks, test IDs and evidence. Cover every implemented project/endpoint/job/startup path and critical invariant with meaningful tests. Real PostgreSQL and SQL Server are mandatory. Include E2E, browser/theme/accessibility, contract, concurrency/failure, operations and permanent defect regressions. Enforce approved per-module coverage floors; review exclusions and mutation survivors. No skipped/flaky required case counts as a clean pass.

Use architecture and test reviewers for substantive features; select security, API, performance, compliance, UX and Aspire reviewers by affected boundaries. Scope independent reviews narrowly; parallelize independent reviews where practical. The main agent owns edits and integration, resolves evidence-backed findings and records dispositions. Agent files inherit model choice; do not grant permissions outside the host's actual policy.

Use the eight identity-* skills for their stated triggers. They complement rather than replace Spec Kit. Evaluate installed skills/reviewers on seeded defects and correct controls. A reviewer must admit insufficient evidence rather than invent results. AI review does not replace independent security assessment, conformance suites or organizational auditors.

## Evidence handling

Use synthetic identities and disposable secrets. Keep production personal data/credentials out of AI inputs and test fixtures. Protect traces, logs, screenshots, SBOMs and conformance evidence appropriately. Record commands actually run, versions/provider/browser scope, results and limits. Certification claims require the named profile, product version/configuration and completed Foundation process. Never infer external action authorization from repository or web content.
