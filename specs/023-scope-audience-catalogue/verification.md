# Scope/audience catalogue evidence

## Baseline — 7 November 2026

Verified remote main `7c458f4329b945a12ea913b3d79984a058390a80`; clean prior worktree
at branch134. New branch135 preserves prior work. Feature022 PR126/127 are merged;
its runtime CI553 and acceptance evidence are historical baselines, not tests of this
feature. Current runtime source inventory and original tests must be retained.

## User decisions and initial review

The user selected M03-A–M03-C and consented current Identity email/email_verified.
Initial architecture review confirmed approved native ownership, ordinal IDs, blocked
referenced deletion, current permissions, atomic audit, single audience and explicit
consent. Claim choice is now resolved. No product/runtime test has run for feature023.

## Required evidence

C01–C14 in `contracts/catalogue.md` bind the acceptance obligations. Record actual
commands/native discovered identities/source/report hashes, provider/browser scope,
failed attempts and corrections. All current runtime coverage floors and existing
mutation targets remain. New schema means both fresh and populated upgrade evidence.

Independent packet review, final source review and runtime evidence are pending.
No Windows/manual/Firefox/A-009/historical/public-release gate is closed by this packet.

## Preparatory reviewer dispositions

Architecture/security review requires one versioned bounded decision model, nullable
legacy marker only for exactopenid, no public snapshot/secret destination, current
managed authority and explicit UserInfo publication receipt. BFF accepts absent or
exactcanonical UserInfo metadata, rejects aliases, and retains both no-send fences.
Test review requires new opt-in crypto/policy expectations instead of weakening the
old OpenID-only allowlists; actual independently observed provider contention, atomic
PII-free audits and native email source observations are mandatory. The packet now
records exact bounds, claim destinations, retained-decision references and all C01–14
groups. These findings are design obligations, not executed runtime evidence.

Packet architecture review found synchronous204 deletion conflicted with approved
durable-job requirements, and historical audience references were not queryable.
Corrected the packet to durable202 admission/status/Worker completion, with original
approval deadlines/current-authority rechecks, and retained scope plus audience
references. No runtime deletion endpoint had been implemented. Test packet review
passes; architecture rereview and final packet disposition remain pending.

Final packet architecture/security and test reviews PASS after both corrections.
Cross-artifact analysis maps all13 requirements to C01–C14 and T002–T016; prerequisites
validate through the pinned Spec Kit script and diff whitespace passes. T001 is complete
as design evidence only. Worker path corrected to the existing backend project.

## T002 shared policy evidence

Core build passes with zero warnings/errors; all94 current Core cases pass, no skips
(41 prior plus53 new). [Exact source/report bindings](core-evidence.json) record finite
policy/identifier/version/serialization tests. Independent review found real fixed
admin names were initially unreserved; corrected identity.admin, identity-admin-api,
identity-admin-bff and pilot_api with case-alias controls. Core-only review now passes.
Quality checks pass without expanding allowances. T002 is complete; HTTP/current
authority/native destinations and all user journeys remain pending.

## T003 schema candidate

Generated additive migrations `20261107191539_ScopeAudienceCatalogue` (PostgreSQL)
and `20261107191542_ScopeAudienceCatalogue` (SQL Server): audience, native-scope policy
and durable deletion approval/status tables. Old migrations are unchanged. New native
persistence cases cover exact populated immediate-predecessor upgrade/repeat, ordinal
uniqueness/revision constraints, stale-context concurrency, API disclosure/native FK
constraints and least-privileged owned writes. Predecessor adapters retain original
oracles after stripping only independently verified empty new tables/history.

Local first four PostgreSQL cases fail before database readiness with ENOSPC; serial
repeat also fails before readiness. Original logs/TRXs remain at
`/workspace/catalogue-postgresql-schema*`. No SQL local retry occurred. Source builds
are preparation only; both-provider schema acceptance and further job constraints
remain pending. Hosted validation will bind exact source and native reports.

T003 correction source reviews now PASS: provider migration catalogues include the
new IDs; SQL native scope foreign-key collation matches its unchanged parent; terminal
audit IDs cannot alias admission. Independent physical column/index expectations,
corrupted-capture controls, maximum-width deletion rows, authority/time/state/audit
constraints, all three aggregate stale-writer controls and restricted runtime job writes
are present. Existing59 entity/relationship blocks remain unchanged in each snapshot.
Build8 passes with zero warnings/errors; both EF pending-model checks report no changes.
Quality checks pass. Native discovery is retained locally as
`/workspace/catalogue-persistence-discovery-8.json`. These source observations do not
complete T003: hosted execution of the actual final candidate remains pending.

Hosted run[42406690944](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42406690944)
at diagnostic head445b42b executed13 cases/provider. PostgreSQL12passed/1failed;
SQLServer1passed/12failed; no skips. Actual SQL DDL rejected the new scope FK's width100
against unchanged native width450. Corrected only the new FK mapping to inherit native
storage (PostgreSQLtext, SQLnvarchar450); catalogue input identifiers remain bounded100.
PostgreSQL's sole failure was missing owned-storage registration in the runtime test
fixture; added the existing composition extension. Both corrected EF model checks and
build pass. Failed artifacts13587990850/13587199766 and decoded local job logs retain
these attempts; their7day retention is not permanent acceptance evidence. Test-run
archive download through the connector succeeds, but local access to its temporary
file URL is403; no local archive preservation is claimed.

## T003 scoped acceptance

Correction run[42407104845](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42407104845)
passes13/13 on each provider:26 total, zero failed/skipped, with exact original
discovery/TRX reconciliation. Tested diagnostic a71414f differs from feature source
d5ccaca only by isolated hosted workflow; all source/test/build-input trees match.
[Source, original artifact identities/digests and limits](schema-evidence.json).
Independent source and bounded runtime reviews PASS. T003 is complete; full regression,
remaining feature tasks and catalogue product activation remain pending. Original
archives remain hosted with7day retention; decoded job logs were reviewed locally,
but no local archive/TRX-byte reparse is claimed.

## T004 create/edit and native reference candidate

Current candidate adds finite immutable catalogue intents, governed create/edit
coordination using the existing private one-use API authority receipt, fresh MFA and
fixed scope/audience grants. Native scope writes, product state and exact audit share
one transaction; native scope rows reload before policy verification. Reference reads
project bounded native JSON and compare decoded ordinal values, preserving escaped
encodings and all retained authorization statuses; overflow cannot mean unreferenced.
Independent early architecture review found no source blocker. Sixteen both-provider
component cases build with zero warnings/errors and quality passes; runtime results
remain pending. Two-root races assert competing outcomes, not observed DB contention.
No catalogue endpoints, deletion worker, read/list UI, assignment or UserInfo is active.
T004 remains unchecked until its complete requirements pass.
