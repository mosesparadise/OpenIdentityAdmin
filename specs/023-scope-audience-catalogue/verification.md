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

First component run42407808413: schema13/13 per provider remains green; component6/8
per provider fails both positive native-reference cases because the test helper omitted
explicit Owned mode selection. The old overflow-negative case also reached that setup
failure, so its apparent pass is rejected as evidence. Corrected helper selection and
added exact32768-character/128-candidate success controls before32769/129 failures.
Added actual malformed syntax alongside invalid array-member controls and value-free
JsonException normalization. Independent test review also required exact caller-bound
audit fields and fresh full native-scope/product/audit snapshots around denials; both
are implemented for scope/audience actions. Correction source review, build and quality
pass. Corrected native execution remains pending; no T004 acceptance is claimed.

## Corrected T004 component checkpoint

Run[42408239551](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42408239551)
at isolated diagnostic65bf2ed passes all four jobs: schema13/13 and component8/8 on
each provider, 42 actual native passes, zero failed/skipped. Original native discovery
and TRX results reconcile exactly in each job. Tested source matches feature runtime
`9d3a43788ed4e74f323bf3c788de00ce2ce687cc`; only the isolated workflow differs.
[Exact source trees, artifact digests, review dispositions and limits](component-evidence.json).
Independent corrected test/runtime review PASS. The successful exact-limit reference
controls calibrate the overflow denials; the prior setup-failure false positive remains
explicitly rejected. Build and quality pass without expanding allowances.

This completes only the create/edit and current-native-reference component checkpoint.
T004 still needs current permission-bound reads, durable deletion admission/status/worker,
original-approval expiry/revocation/restart behavior, and remaining fault/observed-lock
controls. T005–T016 remain unchecked. No catalogue HTTP/BFF/UI, application assignment,
consented native claims/UserInfo, full CI/coverage/mutation acceptance or merge is claimed.
Original artifacts expire14November2026; decoded logs were inspected locally, while
local archive/TRX-byte retrieval remains unavailable through the file-URL proxy route.

## T004–T007 implementation candidate — 7 November 2026

User selection confirms T005–T007 with unfinished T004 prerequisites. Branch135 now
implements permission-bound ordinal list/detail reads, native scope/audience writes,
atomic audited durable deletion, original approval/current-account enforcement and the
existing Worker queue. The fixed versioned Admin API and BFF expose strict bounded
contracts, native purpose-bound CSRF and one-send publication. The Scopes/Audiences UI
includes bounded search, create/edit, preserved audience selection, explicit deletion,
status/recovery and expired/stale/uncertain states. Assignment, claim emission and UserInfo
remain T008–T014; this candidate does not activate them.

Runtime attempts retained before acceptance:

- Run42412586118: schema13/provider passes; component26/27/provider. The failing fault
  test incremented ApprovalUntil into a schema constraint before its intended snapshot
  check. The correction decrements it and preserves the exact failure assertion.
- Run42415307625: schema13/provider passes; PostgreSQL32/33 and SQLServer31/33 component
  results. EF could not translate sorting after a constructor projection; corrected to
  a scalar member projection before SQL paging. The direct API expiry oracle now uses
  native/source/MFA limits independently of the BFF's earlier transport deadline.
- Run42415911228: schema13/provider passes; PostgreSQL37/39 and SQLServer36/39 component
  results. Queue progress, real stop/restart and before/after-commit faults passed both
  providers. Corrected the test's native access-token URN and explicit ordinal collation
  on SQL scope joins/system-ID exclusions. All four browser attempts failed: SQL reads
  hit the same collation conflict, Chromium expected revision1 after a genuine edit,
  and WebKit found horizontal reflow failure after audience selection. The test now expects revision2 and
  exact edited text; CSS permits the selector to shrink. No timeout, assertion or
  provider requirement was weakened.

Candidate sourcea77ffba is undergoing bounded run42417189419 through isolated validation
headae586c8. That branch-only workflow must never merge. Additional actual paging,
observed-reference contention and held-response GET/DELETE tests address independent
review findings. Local Admin build25 passes; Host catalogue18/18, existing native
contracts44/44 and Node contracts46/46 pass. UI verification includes27 catalogue cases,
unchanged per-module floors and generated assets. These focused results do not constitute
full acceptance. Original local logs retain failed attempts; seven-day hosted artifact
retention is temporary, and decoded job logs are not claimed as locally downloaded TRXs.

Architecture review passes ordinal joins/exclusions, job validation before Location,
original authority/expiry and the CSS correction. Test review passes exact scope/policy
preservation and the actual one-send DELETE recovery snapshot. Final runtime review,
full exact-head checks and normal reviewed delivery remain pending. T004–T007 stay unchecked
until that evidence is reconciled. Existing historical/manual/refresh/release gates remain open.

## Corrected native/transport and mutation evidence

Bounded run[42418041016](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42418041016)
at isolatedf2fb586 (src/tests/scripts/eng identical to producta174f6d) passes all56
component cases per provider, schema13/provider, both Chromium catalogue journeys and
all four critical-decision mutations per provider. Both providers validate actual paging,
observed reference races, restart/queue/commit faults, exact held GET/DELETE publication
boundaries and explicit unchanged-approval recovery. The previous four failures per
provider were the application-specific problem-title helper; the corrected helper
requires exact catalogue titles and preserves safe status/header/field checks.

Mutation markers require independently demonstrated bad outcomes: fully committed deletion
after original expiry or despite a retained revoked grant, unchanged complete queued
approval/native/audit state after a false success, or genuine202 publication after omitting
the current browser-authority observation. Setup failures and unrelated safe errors do not
count as killed mutants. PostgreSQL and SQLServer each kill all four controls, with clean
positive controls and restored source required by the existing verifier.

WebKit still fails reflow on both providers. Safe dimensions show320px viewport and685px
document scroll width, with no undersized controls. Earlier min-width and explicit-height
changes did not resolve it. The next candidate clips/ellipsizes select overflow and retains
more numeric layout diagnostics; no reflow/axe/theme/keyboard assertion is removed.
Local WebKit installation is blocked by the runtime proxy's403 Domain forbidden response;
retained-browser execution continues on the normal hosted runner.

UX review also found a validation link to an absent claim field for email/API input, loss
of focus after explicit status refresh, and403 copy that attributed expired MFA to missing
permission. Corrections target the visible scope-type control, require an unsuperseded
status receipt and current focus ownership, and explain both permission and recent-MFA
requirements. The browser journey checks linked-field keyboard recovery and manual versus
background status focus. UI111 cases now pass, including a late-success/failure navigation
regression. All existing module floors remain unchanged.

Initial full CI557 at a174f6d reports import-order failures; corrected imports and unused
using directives pass the SDK formatter. Failed run557 remains visible and is superseded
by the corrected candidate; it is not counted as acceptance. Local Host1892/1892, UI111,
verification-tooling330 and code-quality checks pass. Full corrected-head CI and WebKit
acceptance remain required before PR128 leaves draft or merges.


## Retained-browser correction and source review — 7 November 2026

Focused run [42419063699](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42419063699)
passes both Chromium journeys but fails WebKit reflow. Diagnostic run
[42419769625](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42419769625)
is intentionally failing evidence: at viewport 320 the document scroll width is 682;
waiting leaves 682, appearance:none with the existing bounded overflow produces 320,
and restoring the original style returns 682. Temporary probes were removed before
acceptance runs. The two earlier diagnostic builds failed formatting before runtime
and establish no layout result.

The first NativeSelect candidate b747b94 inadvertently omitted overflow:hidden.
Focused run [42420390096](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42420390096)
therefore passes both Chromium cells and fails both WebKit cells; finite geometry
shows viewport 320 with scroll widths 673/680. This rejected attempt remains recorded.
The final correction 30f5caf restores overflow:hidden on the select itself while
retaining native semantics, explicit labels/ARIA, a decorative noninteractive SVG arrow,
shared theme colors and unclipped ancestor focus outlines. No touch target, reflow,
theme, axe or focus assertion is weakened. CI558/559 were superseded by these source
corrections and are not acceptance runs.

Corrected focused run [42420821014](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42420821014)
at 48cb78aab23e56de40087de86e96b8997c028ba5 passes all four Chromium/WebKit and
PostgreSQL/SQL Server cells, one complete catalogue journey each, with zero failures
or skips. Each hosted job reconciles exact native discovery and original TRX results.
Its src/tests/scripts/eng trees equal product 30f5caff91f68cc16d342f5782a744f8d9887675.
Independent retained-log/source review PASS; local receipt SHA-256:
`2fde904d933c98d5ec637fae7c92cb1516a87380d5eab8d205f5d2d7356b86b3`.
The raw ZIP/TRX archives were not retrieved and independently reparsed locally.

Architecture/API/security and UX source reviews PASS the exact product revision.
Full CI560 subsequently exceeds the existing SQL protocol-admission execution deadline.
It is rejected as integrated acceptance; focused browser success does not close T004–T007.


## Bounded protocol suite partition — 7 November 2026

CI560 job128549065485 starts the SQL ProtocolAdmission native runner, then exceeds
its unchanged660-second process deadline without producing a completed native report.
There is no recorded individual assertion failure; the timeout is not a clean pass or
proof of a product defect. Its original partial artifact13593818587 and decoded log are
retained. Baseline CI553's successful SQL ProtocolAdmission coverage step took651 seconds,
which includes discovery/collector overhead; it does not establish an exact per-case cause.

Split the existing story using the shared exact administrator-group selector:
Admission107 cases/27 classes and Replay55 cases/16 classes on each provider. Explicit
class inventories preserve all162 original cases, traits and identities. Default and
explicit story invocations require both groups. Both-provider jobs retain660-second
execution and20-minute hosted deadlines. No product C#, test body, mutation target,
coverage floor or exclusion changes. Required coverage inputs become104; native report
count107 and ordinary executions6,090, with130 jobs and128 mutation kills required.

Local selection80, coverage68 and browser-matrix7 controls pass. Seeded omissions,
overlaps, provider/story/UID substitutions, redistributed counts, incompatible CLI and
old combined coverage replacement reject. Actual native discovery retains2,871 cases;
four real filtered discoveries reconcile107/55 per provider as exact disjoint unions.
Independent source review and clean final-head hosted acceptance remain required.
