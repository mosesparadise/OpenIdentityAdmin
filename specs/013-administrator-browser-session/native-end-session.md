# Native end-session validation prerequisite

22 October 2026. Prepared separately, then rebased onto PR #71 squash `017bfaa5396e6d2931c99af110faa0710c60e7bb`. [Prerequisite delivery](prerequisite-delivery.md) records that accepted identity/transaction source; it does not certify this new fixture. This test-only continuation addresses T003's pinned native no-ID-token-hint feasibility and T004's required provider selection. It does not activate or implement product logout.

`NativeEndSessionFixture` reuses the real administrator enrollment and installed OpenIddict 7.7.1 services. Its explicitly test-only metadata change commits a Web application, exact post-logout return URI and selected native permission through the actual manager under owned profile locking. Each subsequent validation enters a fresh bounded `ProtocolStorageRequest`, selects owned storage and dispatches the complete `ValidateEndSessionRequestContext`. Request fields are assigned before constructing the context, which captures the return URI. No synthetic authentication principal or replacement validator is supplied.

Three discovered cases per provider cover:

- No ID-token hint, with absent, genuine and foreign `logout_hint` values. Successful native and nested authentication completion must contain no ID-token principal. Independently read security/session/audit snapshots stay unchanged after each validation; native token and authorization counts also stay unchanged. Counts are not a proof of unchanged token statuses.
- Exact registered Web return-URI validation and unknown-client denial, including path case, trailing slash, port and query substitutions. Valid controls precede and follow denials.
- Fresh persisted permission removal/restoration, native legacy `ept:logout` compatibility and URI-based native eligibility when `client_id` is omitted, explicitly leaving the validated client identifier null. These accepted native behaviors explicitly do not satisfy the future product's required fixed-client and exact new-manifest policy.

The native library exposes `logout_hint` as an extension parameter rather than a typed request property; the fixture uses one named parameter constant. Its nested authentication context exposes `IdentityToken` and `IdentityTokenPrincipal`. Initial compilation caught those API-name assumptions; corrected code builds with zero warnings/errors.

Actual native discovery contains 1,247 cases, preserving all 1,241 prior display identities and portable metadata and adding six cases. The required `BrowserSessions` story contains three cases per provider. Ordinary administrator counts become PostgreSQL 558 / SQL Server 663; the existing thirteen browser cases per provider are unchanged. Two separate mandatory workflow cells and collector inputs raise active coverage inputs from 50 to 52; all fifteen module floors, existing security mutants, browser deferrals and deadlines stay intact. Selector controls reject omission, substitution or provider/story mixing. Focused selection, evidence-gate, formatting and code-quality controls pass; independent architecture/test/security/API source reviews pass. API review corrected an inference claim: native URI-based eligibility does not populate or bind the omitted client identifier, and the fixture now explicitly asserts it remains null. Build/discovery is not execution: both-provider runtime evidence remains pending.

Requirements: FR-010/013 and feasibility for B3-END-02/03. This proves no HTTP extraction, routing, publication receipt, confirmation, revocation or browser return behavior. The fixture metadata intervention is not the required audited registration upgrade and must never be reused as startup repair. Existing B2 profile admission remains strict. Product implementation still needs fixed-client and session binding, protected issuer confirmation, committed native sign-out publication, purpose-bound BFF correlation and the exact audited upgrade with disabled-state preservation and separate old-code/old-PAR invalidation.

All six historical findings, human controls and production/public-release gates remain open. A-009 permits internal implementation; no new browser deferral is introduced.

## Accepted native prerequisite — 23 October 2026

[PR #72](https://github.com/mosesparadise/OpenIdentityAdmin/pull/72) is squash-merged as `c21be94f914b6b14179dc2e2c09e7f7879937be1`. Source `46698dfeea887d597499046431b82c448833410e`, tested merge `5fe64f3b877b17b1c6ad6c0440de787a39f45580` and squash share tree `4c429aa8a8ce5f697b645595b46b187f91e586ec`; base was `017bfaa5396e6d2931c99af110faa0710c60e7bb`. [Run 39873582108](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39873582108), attempt 1, completed all 69 jobs successfully with no failed or skipped job. Fresh head/base/check/review/ruleset verification preceded normal squash; the fetched squash tree was independently compared afterwards.

All six new native cases pass, three on each provider, with zero failures/skips. All 1,241 prior portable test identities remain present, total 1,247. All 52 required coverage inputs and 15 unchanged module floors pass. Eight mutation jobs verify all 48 behavioral kills, 48 passing baseline controls and 48 passing restored controls with source restoration. Independent architecture/test/security/API source reviews and independent final evidence review pass within the limits below.

| Evidence | Job | Artifact | SHA-256 |
| --- | --- | --- | --- |
| PostgreSQL BrowserSessions | 120154161975 | 12467672560 | `e58e5a5e37c97068b551110a6c1b27f51110cff92f03b90f8a4c0587bcf5e87e` |
| SQL Server BrowserSessions | 120154162107 | 12467942380 | `1c101d5f55e5185e833e3218931c30c370e83d1059e44a35ed584c3606adba1c` |
| Aggregate coverage | 120163360304 | 12467938205 | `3fa2798e9ffa906274c2e8bcd7ad591a0d316f7c373b58b6142421add83b83b4` |

Final acceptance job: `120163574114`. The retained independent review receipt has SHA-256 `4da5e86c8dec4001c38e0bb6c605fc0b4961fa15b95af6aea6897f5d883b6755` and records all mutation jobs, artifact identities/digests and verifier source hashes. Review reconciled finite original logs, exact source/verifier behavior, API artifact metadata and tested-tree identity. Direct raw artifact XML/JSON transfer was unavailable; this is **not** a claim of independent raw-report recount. No access-policy bypass was attempted.

This acceptance supersedes the pending-execution checkpoint above only for PR #72. The subsequent [explicit B3 registration contract](browser-registration-contract.md) changes the fixture and requires its own exact-head verification. T003 and T018 remain partial: native validation does not establish product logout or the audited registration upgrade. All historical, human and activation/public-release gates remain open.
