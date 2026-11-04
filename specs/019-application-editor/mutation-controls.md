# Application editor mutation controls

Feature 019 T021, 4 November 2026. FR-005–012. This checkpoint extends the two
feature018 read controls rather than replacing them. T020 authoritative contracts
already include create/details/edit/replacement and their ETag/CSRF semantics.
T022 full verification remains open; no release or merge acceptance follows from
mutation source or scoped execution alone.

## Reviewed behavioral controls

`eng/application-mutations.json` retains read-commit and projection-overflow and adds:

| Mutation | Required violation before the fixed failure marker |
| --- | --- |
| application-write-permission | Creation succeeds after removing the persisted write grant while read access remains. |
| application-strong-version | Both same-version edits succeed. |
| application-recent-mfa | Replacement succeeds at the original MFA freshness deadline. |
| application-json-csrf | A genuine wrong-purpose proof causes an upstream mutation send. |
| application-audit-atomicity | The injected audit deletion completes and creation returns success with no audit. This specifically tests post-save audit integrity; the existing provider INSERT-denial rollback tests remain separate. |
| application-credential-expiry | Creation succeeds after the injected clock advance expires its originally validated credential. |

`eng/managed-application-mutations.json` adds:

| Mutation | Required violation before the fixed failure marker |
| --- | --- |
| managed-explicit-consent | Explicit denial persists Approved. |
| managed-claim-isolation | The prepared access-token claim injection fires and token issuance succeeds. |
| managed-request-consumption | The exact cached request remains Valid across a witnessed successful native consumption commit. |
| managed-code-consumption | The exact code remains Valid across a witnessed successful native consumption commit. |
| managed-current-credential | Expiry equality admits actual redemption of the original code. A later publication denial does not undo that admission. |

The consumption witnesses bind the exact input and native child transaction, observe actual provider commit, and read status on an independent connection. Seeded early 503 controls must fail the witness ordinarily; a commit failure cannot satisfy it.

Every other unexpected result retains ordinary assertions and cannot satisfy the
mutation runner's exact marker check. Each suite requires clean baseline and restored
runs, one exact selected case per provider/mutation, native MTP exit/counters/identity
agreement, one behavioral assertion failure, and restored source hashes. Compilation,
setup, timeout and unrelated assertion failures are rejected. There are no retries or
broader diagnostic allowances. The diagnostic ordinal maximum remains eight.

All 2,375 native cases and prior traits/provider order are preserved. Exactly 22
existing provider cases gain a CriticalDecision trait. The separate bootstrap quality
remediation explicitly maps 46 renamed cases in [the rename ledger](bootstrap-case-renames.json);
all other names/types are unchanged. All 55 UI identities remain unchanged.
Both providers receive the new managed suite in CI, with its evidence required by the
final acceptance job. The 80 coverage inputs and 15 module floors remain unchanged;
uninstrumented mutation runs do not establish aggregate coverage.

## Verified execution and remaining gates

[Reconciled receipts](mutation-controls-evidence.json) record26 demonstrated behavioral
kills and 52 passing baseline/restored executions across both providers. Independent
review reconstructed mutant hashes and checked exact identities, counters, source
restoration and completion logs. Per-mutant raw TRXs were temporary runner outputs and
were not independently reparsed afterward. Distinct tested source commits remain
explicit; these results are not an exact-head full-CI result.

The contract-generator advisory and expired bootstrap exception are addressed by the
separate [T020/T022 remediation](delivery-checks.md). That record distinguishes current
passes from the earlier failed gates; no audit waiver, renewed exception or expanded
legacy baseline is accepted.

Retained Chromium/WebKit and both native demo acceptance remain open (T007/T014/T019).
The browser download denial is not retried. Firefox stays excluded under the approved
initial-phase deferral. Human/Windows/A-009/vendor/production/public-release gates and
all six historical findings remain open; refresh is disabled.

Source presence alone is not a killed-mutant result or a full-verification pass. Full exact-head CI and aggregate coverage remain T022 obligations.
