# Feature Specification: Engineering standards

**Feature**: 009-engineering-standards | **Created**: 2026-10-12 | **Status**: Approved user direction; implementation in progress

## User Scenarios & Testing

### US1 — Proportional verification (P1)
A maintainer changes prose and receives relevant checks without starting unnecessary database/browser suites. Mixed, executable, configuration, policy, unknown and scheduled changes receive full verification.

Given only eligible prose paths changed, when verification selects work, then lightweight checks and a final acceptance gate run. Given a rename from executable code, missing change evidence or a mixed change, when selecting work, then full verification is required. Given a required job fails/cancels/skips, when evaluating acceptance, then merge acceptance fails.

### US2 — Maintainable contributions (P1)
A contributor receives actionable findings for new/changed code with unclear scenario names, missing documentation, unexplained values or excessive size. Existing debt is visible and cannot be silently expanded.

Given a new or changed violation, when the gate runs, then it fails. Given an unchanged recorded legacy violation, then its specific remediation task remains visible. Given an expired, stale or widened exception, then validation fails. Given independent negative test inputs/expected values, then the rule preserves their independence.

### US3 — Configured API descriptions (P2)
An operator sets supported presentation metadata through configuration without editing application code or enabling protected documentation routes.

Given valid settings, when generating a contract, then both surfaces reflect those settings. Given invalid settings, then validation fails safely. Given production configuration, then documentation stays unavailable. Given hostile synthetic description text, then served JSON contains the probe and viewers do not execute it.

## Requirements

- ENG-01: Safe documentation selection and visible final acceptance; unknown evidence selects full checks.
- ENG-02: Given_<condition>_When_<action>_Then_<outcome> C# test names; equivalent descriptions in other languages; independent assertions and exact inventories preserved.
- ENG-03: No unexplained domain/protocol literals; use meaningful feature-owned names or validated settings without making security invariants tunable.
- ENG-04: Cohesive reusable operations; review thresholds 40 logical lines/method and 300/class; documented scoped exceptions.
- ENG-05: Meaningful XML for handwritten C# methods and detailed safe examples for externally consumed APIs/public reusable classes; native generated EF excluded, tests need no example boilerplate.
- ENG-06: Constitution1.1.0, AGENTS, all eight local identity skills/reviewers and task guidance carry principal-engineer obligations; pinned Spec Kit integration remains intact.
- ENG-07: Immediate new/changed-code checks with explicitly tracked existing debt; baseline cannot grow silently; exceptions have reason, owner and expiry.
- ENG-08: OpenAPI presentation options read from appsettings and validated; actual supported versions, route/security contracts and disabled production docs stay authoritative.
- ENG-09: Seeded positive/negative gate controls, independent architecture/test/API reviews, actual evidence and normal squash delivery.

## Success Criteria

Every seeded invalid selection and required-job failure blocks acceptance. Every supported new/changed rule violation is rejected, with unchanged legacy findings linked to remediation. Both API descriptions can be configured and invalid settings fail safely. No existing security assertion is weakened. Existing debt is not represented as completed cleanup.

## Scope and decisions

User approved on 2026-10-12: Engineering tool at eng/IdentityPlatform.CodeQuality, Engineering solution folder,40/300 review thresholds with explicit exceptions, meaningful documentation exclusions, immediate enforcement with separate legacy-cleanup PRs alongside MFA. This is an engineering prerequisite, not an expansion of production authentication. M02-A increment 2 follows; its T016 design remains required. No new production host or shared domain-service layer.
