# Application editor mutation controls

Feature019 T021, 4 November 2026. FR-005–012. This checkpoint extends the two
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

The consumption witnesses bind the exact input and native child transaction, observe actual provider commit, and read status on an independent connection. Seeded early503 controls must fail the witness ordinarily; a commit failure cannot satisfy it.

Every other unexpected result retains ordinary assertions and cannot satisfy the
mutation runner's exact marker check. Each suite requires clean baseline and restored
runs, one exact selected case per provider/mutation, native MTP exit/counters/identity
agreement, one behavioral assertion failure, and restored source hashes. Compilation,
setup, timeout and unrelated assertion failures are rejected. There are no retries or
broader diagnostic allowances. The diagnostic ordinal maximum remains eight.

All 2,375 native names/types and established traits/order are preserved. Exactly 22
existing provider cases gain a CriticalDecision trait; no case is added or renamed.
Both providers receive the new managed suite in CI, with its evidence required by the
final acceptance job. The 80 coverage inputs, 15 module floors and all 55 UI identities
remain unchanged; uninstrumented mutation runs do not establish aggregate coverage.

## Existing blockers

The current npm registry still resolves the generator's v1 parser chain to vulnerable
minimatch5.1.9 (GHSA-g3hv-75wg-m249). Redocly1.34.23 also retains it. A proposed
Redocly2.71.2 override was rejected during source review: openapi-typescript7.13.0 uses
blocked private imports and removed pointer APIs as well as the old styleguide shape.
A minimatch11 override changes its callable CommonJS API. Neither experiment is retained;
the exact package/lock/review files are unchanged. No downgrade, fork or audit waiver is
claimed as a fix.

The expired BootstrapTests CQ008 exception also remains failed. Proper removal requires
cohesive nonpartial scenario classes, compliant shared helpers and an explicit identity
rename map for the six affected test methods; renewing its date is not remediation.

Retained Chromium/WebKit and both native demo acceptance remain open (T007/T014/T019).
The browser download denial is not retried. Firefox stays excluded under the approved
initial-phase deferral. Human/Windows/A-009/vendor/production/public-release gates and
all six historical findings remain open; refresh is disabled.

Execution receipts and final review dispositions will be recorded separately from this
source design. Source presence is not a killed-mutant result or a full-verification pass.
