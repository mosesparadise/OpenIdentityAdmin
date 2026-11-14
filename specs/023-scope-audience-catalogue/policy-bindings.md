# T009 managed catalogue policy bindings

**Accepted delivery:** T009 is squash-merged in PR132 after CI581 and independent
original-evidence review. [Final acceptance](policy-acceptance.md) supersedes the
pending language in the historical checkpoints below. T010 is next; extended issuance
and all historical/manual/public-release gates remain open.

Continuation from accepted main `95777ca91a7556233ef8636667888df07d0032f8`,
13 November 2026. The user requested the next recommended task. Existing M03-B/C
approval covers this prerequisite, both-provider verification and reviewed delivery.

## Bounded implementation

Add a versioned canonical policy snapshot and SHA-256 binding to managed interactions,
plus indexed retained scope/audience references. Capture only current native permissions
and managed catalogue policy under existing governance serialization. Requested/granted
names are ordinal, bounded and explicitly validated; a decision has at most one API
audience. Native permissions/resources remain authoritative. Scope/audience identifiers,
revisions and full-metadata digests detect consent/display/mapping/disclosure drift without
storing personal data or duplicating large display strings. Serialized UTF-8 is bounded
to 16 KiB and malformed, noncanonical, duplicate, partial or substituted data rejects.

New existing OpenID-only reservations persist canonical OpenID-only bindings. Migrated
legacy rows keep both new columns null; only the unchanged exact OpenID profile can
accept that legacy representation. Do not repair malformed bindings or infer approval.
Extend explicit pre-save immutable fields and independent persisted-state checks.
Existing protected selector/application revision binds the stored interaction; new
extended private principal/form bindings remain with T010/T011.

Retained references protect original scope/audience IDs even after remapping or clearing
application assignments. Details, mutation responses and durable deletion must use one
shared reference decision. Restrictive relationships prevent cascades. Remove references
only in the owning transaction when existing cleanup proves the terminal parent eligible;
expiry alone or a consumed native lineage does not release them.

Both provider migrations are additive, preserve old rows and histories, and retain the
existing runtime grants/provisioning lifecycle.

Restore on resume rejected Aspire 13.6.4 with NU1904/NU1903. The necessary bounded
dependency prerequisite updates the Aspire SDK/hosting family to 13.6.5 and regenerates
lock files, preserving audit enforcement. Microsoft identifies 13.6.5 as the patched
release for [Dashboard authorization](https://github.com/advisories/GHSA-4g86-54fm-h9h8)
and [hosting cookie disclosure](https://github.com/advisories/GHSA-f7vh-h5g3-7jrh).
The resolved Aspire dependency minima also advance the affected Microsoft.Extensions/Bcl/Hashing transitive patches; no unrelated direct package upgrade or historical assurance closure is implied.

## Protocol boundary

T009 does not enable extra-scope authorization, consent, token claims or UserInfo.
Managed registration/demand/prepared-principal gates remain exact OpenID-only. Nonempty
assignments retain T008's explicit sign-in hold. T010 consent and T011 issuance will
consume the new current-policy mechanism; T012–T014 remain separate.

## Verification and acceptance

C05/C07/C08 and FR007/008/011/013 require canonical input/byte boundaries, frozen inputs,
current native-policy/cache drift, retained-reference deletion/cleanup, actual save-fault
rollback, populated upgrades, least-privileged runtime access and preserved genuine
OpenID flows on both providers. Keep prior discovery identities, browser obligations,
mutation controls and coverage floors. Add meaningful critical-decision controls and
independent architecture/security/test review before exact-head full CI and squash.

Implementation and runtime acceptance are pending. T009 remains unchecked; all existing
historical/manual/Windows/Firefox/A-009/refresh/production/public-release gates stay open.

## Candidate verification — 14 November 2026

The candidate adds the current-policy reader, strict canonical codec, new interaction
bindings, retained-reference model/migrations, shared catalogue reference decisions
and existing public-flow checks. Extended consent/issuance remains disabled.
Architecture/security source review found an owned EF reference read bypassing the
storage wrapper; this is corrected and re-reviewed. Test review corrections add
explicit owned fixture selection, exact physical upgrade metadata, a genuine legacy
journey and post-save null/null downgrade controls. SQL Server's inherited native
ScopeId width permits a declared composite-key warning; supported values are bounded
to 328 bytes, and the real schema test inserts the maximum supported scope identity.

Local Infrastructure/AuthServer and focused Host/Persistence/Administrator builds
passed with no warnings. Canonical codec controls pass 10/10. Code-quality gates pass
without allowance growth. Native discovery preserves every earlier identity and
portable metadata record: Administrator 2,923→2,933; Persistence 76→80. Ordinary
administrator totals are 1,338 PostgreSQL / 1,443 SQL Server; managed Admission57 /
Issuance66 and Catalogue50 each. Host inventory is1,915. Selection controls pass80/80 and mutation-harness controls pass62/62. Two new behavioral mutation
controls retain the existing credential/lifecycle mutations at their extracted source.

Local provider acceptance is **not established**. Two PostgreSQL schema tests failed
before readiness with `initdb: No space left on device`; after removal of obsolete
Aspire/compiler caches, the single-case attempt also failed at readiness. Full local
solution copying exhausted the same filesystem. Those failed receipts remain under
`/workspace/t009-focused-evidence/` and `/workspace/t009-*-test.log`; do not turn them
into passing tests or retry local database startup without changed capacity. Current
focused project builds and pure controls are separate successful observations.
Use hosted CI for both-provider acceptance, mutations, original coverage reports and
all unchanged browser/contract/operations obligations. No local SQL experiment ran.

Acceptance, T009 completion and merge remain pending. Existing human/release gates
remain open. Source review and local compilation are not substitutes for full CI.

### First hosted candidate and corrections

PR132 source `16b7d9b3b6b40c91dd36f5e0b4c526229ae0e069` ran
[CI578](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43534915618)
and failed. Both persistence jobs reported 34 passes / 6 failures: one historical
lifecycle fixture wrote the current EF interaction shape against the old schema;
the other five failures exposed missing new migration IDs in the providers' explicit
history catalogs. Architecture checks independently caught the catalog mismatch.
Several startup-dependent mutation baselines timed out and produced no acceptance
evidence. Static analysis also rejected import ordering and unused imports.

Corrections append the exact generated migration IDs to both authoritative catalogs,
seed all 29 original interaction fields through parameterized historical SQL, and
apply SDK import formatting. Architecture and test reviewers independently checked
the fixes and found no further blocking source issue. Focused Persistence and
Architecture builds pass without warnings; all five Architecture tests pass,
including both native runtime/design-time migration checks. SDK whitespace/import
verification and the code-quality gate pass without allowance growth. Existing historical row
fingerprints and negative controls remain intact. No timeout or gate is relaxed.

CI578 remains a failed candidate, not acceptance evidence. Publishing the corrected
candidate may automatically supersede unfinished jobs under the existing workflow
concurrency policy. Its completed failure logs and run remain available. Original
artifact ZIP retrieval from this workspace was denied HTTP403, so no unavailable
artifact is claimed inspected. Corrected exact-head full CI and original-report
reconciliation are still required before T009 completion or squash.

### Nullable historical fixture correction

The second candidate, `5ef6d1a1f9cff5e3a630cb92387f9971c89010cd`, is tested by
[CI579](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43536556419)
against merge `799260960de8633e9f1eca9b3cd174c285d7c74f`. Each provider's persistence
job reports 39 passes and one failure in the historical lifecycle fixture: EF does
not map the CLR type `DBNull`. The fixture now preserves CLR null arguments through
`ExecuteSqlInterpolatedAsync` and a fixed `FormattableStringFactory` template. All
29 original fields remain parameterized in the same order. Independent test review
checked the EF10.0.14 implementation's null mapping and the frozen provider schemas.
The corrected Persistence project builds with no warnings; formatting and code-quality
checks pass. Actual corrected-fixture runtime validation remains pending.

CI579's managed-application mutation jobs pass all seven behavioral controls on each
provider, including the new policy-binding and current-policy mutations. Its unit
and hosted static-analysis jobs also pass. These results establish only their
observed candidate scope; they do not make CI579 or the subsequent correction an
accepted full run. Original artifact retrieval and complete exact-head evidence
reconciliation remain required.

Before publishing the nullable correction, CI579 had 72 successful jobs and the two
known persistence-fixture failures. Both managed issuance groups, the PostgreSQL
catalogue/admission groups, retained Chromium journeys and WebKit contracts passed.
The existing concurrency policy may cancel unfinished jobs when this correction is
published; CI579 is not treated as a completed passing run.

### Protected-page readiness correction

CI580 at `61f44b2b9e7660791a069ab4e595e48ab52c466d` passes all 40 persistence cases on
each provider. Its PostgreSQL/WebKit BrowserLogin job132275702893 reports 8/9 passing
cases; the foreign-account setup in `BrowserOtherAccount.SignInAsync` times out at
its exact-security-URL `Load` wait before the B3 cookie-substitution scenario starts.
The failed original archive/log is retained. This observation does not establish
whether the URL transition or full-load event failed, or prove an upstream browser
regression. The same SQL Server/WebKit case passes.

Architecture and test review support a bounded semantic-readiness correction. The
helper waits for DOM readiness at the same exact protected security URL and performs
its existing single reload. It retains HTTP 200 and additionally requires the exact
reload response/final page URL and visible fixed Account security heading. Existing
distinct-account, live-session and native-cookie proofs, and every subsequent
substitution/503/no-ticket/no-exchange assertion remain. Navigation deadlines and
test identities are unchanged; no retry, skipped case or new deferral is introduced.
Fresh exact-head full validation remains required.

Artifact access is resolved: connector-provided Sediment file references download
through the native file tool. Direct URL HTTP403 receipts remain, but no environment
allowlist change is needed. Original ZIP digests are now checked and native TRX and
coverage payloads reparsed locally; source-bound hosted identity validation remains
an additional requirement. Earlier notes about blocked retrieval are superseded.

The readiness correction builds with zero warnings/errors and passes code-quality
and SDK formatting checks. Both independent reviewers found no assertion loss.
All four unchanged BrowserLogin cells (nine cases each) remain required in fresh CI.
