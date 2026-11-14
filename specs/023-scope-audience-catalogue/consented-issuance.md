# T010–T012 consented issuance and UserInfo

The user selected the next three tasks on 14 November 2026 after accepted T009.
Implement T010 consent, T011 native issuance and T012 UserInfo, including T013's
required fixed-BFF discovery compatibility and the applicable T014–T016 tests,
review and delivery obligations. Base: `f6c959a7cab1f6cb7b799d04348922d2bf259852`.
Existing M03-B/C and consented current email/email_verified approval governs scope.

## Implementation boundaries

- Freeze bounded ordinal requested scopes; reject duplicate, reserved, unassigned,
  disabled and conflicting-audience scopes. The existing explicit Allow/Cancel form
  approves all requested eligible scopes or none; no silent downscoping or inferred
  consent. OpenID-only applications retain their existing behavior.
- Bind the canonical policy digest through native cached request, protected form and
  native code. Legacy absent bindings are admissible only for the original exact
  OpenID profile with no retained references and current fixed native permissions.
  Recheck current native/catalogue policy after locks and before each commit.
- Render plain-text current scope labels, consent descriptions, audience and finite
  email disclosure. Protect the actual displayed policy through the existing form
  and publication boundaries; preserve native antiforgery and fresh password/MFA.
- Issue only native code/S256 and access/ID tokens. Exact granted scopes and at most
  one API audience derive from the bound policy. Email and boolean email_verified
  come from current Identity state only at emission, only for consented identity
  disclosure and only in ID token/UserInfo; access tokens carry neither.
- UserInfo GET/POST uses native access-token validation, exact current managed token,
  authorization, interaction, account, client and policy. No query token fallback,
  fixed-admin/pilot/machine profile or arbitrary claim projection. Use a distinct
  bounded read/publication receipt and native no-store/error formatting.
- Advertise only the implemented fixed UserInfo endpoint. The BFF accepts that exact
  endpoint while retaining both independent no-UserInfo-send fences. Update obsolete
  temporary assignment-hold UI text only when extended issuance is implemented.

## Required evidence

C07–C12: real both-provider consent/subset/audience/claim/denial journeys, independently
validated native tokens, policy/cache/code changes, actual cross-process evidence,
current UserInfo denials, email presence/absence/confirmation/change and original
publication/consumption deadlines. Preserve earlier native identities and assertions;
new extended expectations are explicit opt-ins to strict existing token oracles.
Retained Chromium/WebKit consent and BFF journeys, permanent fault/race regressions,
causal mutations, original coverage floors and exact-head full CI remain required.

Implementation is present on branch `143-managed-scope-consent`; acceptance is pending.
Do not close T010–T013 from this packet.
No local SQL startup retry is planned in the disk-constrained workspace. Preserve all
prior evidence, frozen producers and diagnostic branches. All six historical findings
and human/Windows/Firefox/A-009/refresh/vendor/production/public-release gates stay open.

## Implemented scenarios and local verification

| Controls/tasks | Permanent scenario families | Evidence required for acceptance |
| --- | --- | --- |
| C07/C08; T010 | ManagedRequestedScopeTests, ManagedConsentDriftTests, ManagedConsentBindingTests, ManagedScopeProcessTests | Actual protected displayed consent, same-revision drift, separate process/current-policy read under provider-observed governance contention |
| C07–C09; T011 | ManagedConsentProjectionTests, ManagedConsentHttpTests, retained token/replay/publication tests | Independent crypto oracle, exact scope/audience, current email pair or complete omission; no access-token email |
| C10/C11; T012 | UserInfoTransportTests, ManagedUserInfoTransportTests, ManagedUserInfoDenialTests, ManagedUserInfoPublicationTests | Real GET/POST header/form transport, native safe denials, changed current authority and actual callback/commit/disposal/deadline faults |
| C12; T013 dependency | NativeClientSharedDiscoveryTests, retained native client suite, ManagedUserInfoTransportTests | Exact optional shared discovery, both no-send fences, genuine fixed-BFF login/logout |
| C07–C12; T014–T016 | Existing ApplicationScopeBrowserTests extended; ten managed mutations | Both providers and Chromium/WebKit; Allow/Cancel, token/UserInfo, themes/axe/keyboard/reflow; causal bypass markers |

Local .NET 10.0.404 builds of Host and Administrator tests succeed without warnings.
The entire Host suite passes **1,967/1,967**, zero skipped. Actual native discovery adds
44 administrator cases while preserving every earlier 2,933 identity and full portable
metadata: 2,977 total (HTTP 1,360 PostgreSQL / 1,465 SQL Server; browser inventory
unchanged). The initial Managed Admission/Issuance partitions were 63/82 per provider.
All **337 Python tooling controls** pass. Code-quality checks pass with unchanged
allowances, and the eight-skill/eight-reviewer instruction check passes. UI type checking
and deterministic asset verification pass. All 132 UI cases pass across the six suites
(the scope suite was rerun after replacing the obsolete temporary-hold text assertion). These local checks do not replace hosted
provider/browser/mutation/coverage evidence.

Architecture and security source review found no remaining blocker. Test review caught
and resolved the finite mutation ordinal bound, missing per-case selection traits and
noncausal UserInfo bypass markers. New markers require an independently verified leaked
profile (plus actual callback/commit/disposal witnesses for publication), rather than an
arbitrary unexpected status. Review acceptance remains conditional on runtime evidence.

Late current-authority changes fail closed: ordinary invalidation before projection is a
native bearer denial; ownership/storage/final source-snapshot faults remain safe 503.
The separate-process test exercises real production policy reads with caches disabled,
not a fabricated cache hit. No local provider startup experiment was run. Earlier local
build/disk failures and superseded test receipts remain retained outside the repository.

## First hosted attempt — CI586

[CI586](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43573605329)
checks source `407676440f64522221577355ff14782264d5ab3a` / merge
`b68d1d512460473e15087ffc5f8f0761fb311e64`, tree
`987eb2354fc36c57948bb294af5feb18e898fe6e`. It is not an acceptance run.
Both managed mutation jobs stopped at discovery ordinal6 because the retained historical
case and replacement displayed-consent case shared an identifier. The replacement now
has a distinct `managed-displayed-policy-binding` identifier; the old case/metadata and
strict one-witness runner remain intact. A permanent inventory control catches collisions.
The SDK format gate also identified import ordering and three unused imports; these are
corrected without changing runtime logic. Corrected local tooling passes 338 controls.

The PostgreSQL managed-admission group passes all 63 cases, including real cross-process
contention and displayed-consent drift. The SQL Server persistence job failed before one
migration test could run: the pinned container exited 18 after access denied opening
`/var/opt/mssql/data/tempdb.mdf`. Other SQL Server jobs have passed, but that startup
failure remains retained and unexplained. No diagnostic approval is revived or test skipped.
Remaining runtime/retained-browser results and a complete corrected-head run are required.

### Runtime correction and bounded partition

Both providers pass the 63-case admission group. PostgreSQL's first 82-case issuance group
finishes with 65 passed / 17 failed: sixteen new UserInfo cases return safe 503, and the old
assignment regression's newly added fourth managed ceremony exhausts the native OTP
window. Native extended token cryptographic assertions pass before UserInfo is called.

A real Minimal API JSON result against the actual bounded capture reproduces all three
positive projection failures locally: .NET 10 throws because ProtocolResponseWriter does
not implement PipeWriter.UnflushedBytes. Reporting its existing pending advanced-byte
count fixes all three cases without changing buffer limits or flush/callback ownership.
Permanent tests additionally cover reserved versus advanced bytes, successful completion,
failed/cancelled flushes and callback-added headers. The publication mutant targets the
extra-header predicate rather than a redundant later call: actual JSON flush executes
OnStarting before FormatAsync returns. Both production checks remain in place.

The assignment test now checks fresh assigned-request admission without adding another
MFA ceremony; its original old-artifact, unrelated-client and cleared-assignment journeys
remain intact. The dedicated requested-subset cases prove complete fresh assigned sign-in.

The accepted CI581 SQL issuance group took 9m36s for 66 cases under the original 660-second
command budget. Preserve that group unchanged in membership and put the 16 new disclosure
cases into a third explicit group: Admission 63, Issuance 66, Disclosure 16 per provider.
Strict native identity unions, positive class filters and default routing retain every
case. Add both mandatory hosted cells and coverage inputs (106 total), preserving every
coverage floor, timeout and retry policy. No acceptance is inferred from this partition.

The corrected local Host build and all **1,974 tests** pass with zero failures/skips,
including the seven real formatter/accounting controls. All 338 tooling controls pass
with the new partition and independently enumerated 106 coverage cells. SQL Server's
initial issuance group also finishes: 66 passed / 16 failed, with the same UserInfo 503 defect;
its original assignment journey passes. Both initial provider receipts remain failures,
not acceptance. UX review's referenced-deletion and offline-token wording corrections
are incorporated in the candidate walkthrough.

### Second hosted attempt — CI587

[CI587](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43575678593)
checks source `892c6809f1e93f8fb667a7e753ae02423c3bf04c`, merge
`70f0e9997fc5b10109c2daf745e654b04efa48aa`, tree
`39e8c690528402146c30e17ae5997e85fff2e2ec`. It is not acceptance. Both disclosure
groups finish 8 passed / 8 failed; every failure is the final unchanged-state digest,
after the expected native projection/error and fault-boundary checks. The managed
mutation baselines each stop at 9 passed / 1 failed; no mutant kill is claimed.
Both admission groups and the standalone SQL Server persistence job pass. The prior
container startup failure remains retained and unexplained.

Successful UserInfo reads commit the existing portable UPDATE locks. Independent source
reviews agree that each committed projection advances governance revision by five and
the bound admission/browser/authentication revisions by one each. The new assertion
incorrectly expected those coordination fields to remain unchanged. Preserve production
serialization and publication ownership. A UserInfo-specific oracle now clones the full
before-state and applies only those exact expected increments, then compares every field
against independent fresh reads. It additionally captures governance/admission/profile,
user and enrollment rows. Profile revision, credentials, grants, consent, failure counts,
clock watermarks, sessions and audits must remain unchanged. Callback/ordinary denials
require zero increments; actual postcommit failures and mutation leaks require one set.
The existing generic digest retains all its fields and metadata.

Within the original native cases, additional controls prove unchanged re-signed
bearer acceptance before wrong client/presenter/subject, already-expired and absent-human
subject denials. No machine grant is enabled. Present email and confirmation now change
after issuance, followed by current UserInfo checks and exact committed-state comparisons.
The full UI→assignment→browser consent→native tokens→UserInfo/Cancel chain is already
implemented in ApplicationScopeBrowserTests; independent review confirmed its continuity.
The administrator build and source/test review pass; corrected-head hosted validation,
original report reconciliation and final evidence review remain mandatory.

### Third hosted attempt — CI588

[CI588](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43577257368)
checks source `0bc0ba1bb2d7e20e32d71a43de032b56a296a6c7`, merge
`8338a03a9e413690a4257160cd23b197d12fb10b`, tree
`b06f0cd24cb12b729ba7516568ef4d5c8c0e24dc`. PostgreSQL passes all ten managed
behavioral mutations with clean baseline/restored controls and exact source restoration.
Its disclosure group passes 15/16, confirming the strict lock-state oracle; the new
reconstructed-token positive fails cryptographic validation because the fixture encrypted
an access token while the actual managed profile explicitly uses signed unencrypted tokens.
The fixture now reproduces that exact RS256 JWS profile and retains signature/type/payload
verification before all native negative cases. A local actual IdentityModel reproduction
rejects the former encrypted construction and accepts the corrected signed construction.

The unit group also exposes an existing process-observer race: Linux removes a child
between existence checking and reading `/proc/<pid>/stat`, producing ESRCH. Reuse the
existing shared ProcessTreeEvidence reader, which narrowly handles that native race and
propagates other I/O/access failures. Its permanent real open-file/process-exit control
and all 110 AppHost harness cases pass locally, as do the clean build and independent
review. No production process behavior, timeout, retry, test identity or assertion is
relaxed. Earlier failed receipts remain rejected; full corrected-head acceptance is pending.

### Fourth hosted attempt — CI589

[CI589](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43578290934)
checks source `2e49e092d5fb68c66d1190a91e7a68f0c221d954`, merge
`db591c9d04ce99d762ea4acaa192041480f83b5a`, tree
`d897c59f34091da328c3cf1e33f12413f95afabd`. The unit group passes, including the
corrected process observer. Both providers pass disclosure 15/16; a re-signed negative
bearer unexpectedly returns 200. The reconstructed signed positive now passes its
cryptographic and real UserInfo controls. This run is rejected as acceptance.

OpenIddict 7.7.2 restores private expiration from the native token entry after deriving
it from the signed public `exp`. Comparing only that restored value with the same row
can lose an earlier signed expiration. UserInfo now separately requires one canonical
integral public `exp` and retains the earlier signed/native deadline through current
state verification, commit and publication. Missing, ambiguous, malformed, overflowing,
wrong-type or already-expired values fail closed through the native 401 response.
An explicit ordinal claim-type check also prevents case-insensitive identity lookup
from treating `EXP` as the required public claim. No native cryptography, current-state
check or publication ownership is bypassed. The existing provider regression keeps an
unchanged signed positive before wrong-client/presenter/subject/expiry denials; its
failure message now identifies only the finite binding label, never token contents.

Sixteen permanent Host regressions reproduce restored future private metadata alongside
an independently earlier/missing/invalid public expiration, test both minimum branches,
strict equality and both native integer types, and reject aliases and duplicate expiry.
The expected Host inventory is 1,990 and the complete ordinary execution inventory is
6,300. Full corrected-source validation and final original-evidence review remain required.

The corrected local Host build passes all **1,990/1,990** cases with no failures or
skips. Administrator build, changed-source SDK style checks and repository code-quality
checks pass without new allowances. Independent security review passes after the
canonical claim-type regression; test review confirms the independent signed/native
bounds and retained provider positive/negative controls. Hosted evidence remains pending.

CI589 additionally exposes an existing PostgreSQL pilot-wait evidence gap. The exact
blocked `PilotProtocolGate` UPDATE terminates with the configured five-second native
lock timeout, but the test's provider-timeout classifier recognizes SQL Server only.
The response and exact-command termination assertions pass; deadline classification
fails. The shared test helper now also requires PostgreSQL SQLSTATE `55P03`, the exact
lock-timeout message, invariant `ERROR` severity and no inner failure. Positive and
negative controls reject same-code NOWAIT, query cancellation, wrong severity, wrong
provider, unrelated exceptions and null. The independent observed waiter/holder,
no-caller, elapsed-budget, unchanged-state and no-late-issuance assertions remain intact.
Independent test review supports this narrow correction; no production deadline,
provider configuration, retry or required scenario changes. The failed run remains
retained and corrected-source execution is mandatory.
The final Administrator build passes with zero warnings/errors. A local invocation of
that built classifier passes the exact PostgreSQL positive, wrong-provider negative,
and all embedded SQL/PostgreSQL controls without starting a database.

### Fifth hosted attempt — CI590

[CI590](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43580181755)
checks source `c11b99a142e05e18b20fc399e3a4f2ce3d38a13d`, merge
`aec6dd65e5840c43a89d4c40e9e478471f3997d4`, tree
`77628c1b1bbdd3d4e93836a27c18b1ab8762a752`. The unit suite, PostgreSQL disclosure
16/16 and both protocol replay groups 55/55 pass. Original PostgreSQL disclosure TRX
counters/outcomes and archive/source bindings were independently reparsed. Earlier
signed-expiry and PostgreSQL timeout corrections are exercised successfully.

The PostgreSQL ordinary local-demo group fails during its final disposable-restart
check at LocalDemoBrowser's single navigation to `/bff/session`. Both complete managed
journeys report successful callbacks/exchanges before restart. The failure occurs before
HTTP 200 and authenticated=false assertions; the retained sanitizer reports only Browser
and the source line, so network cause and product causation remain unverified. A fresh
graph's anonymous session status does not require administrator initialization. No
production/readiness change, assertion removal, retry or browser waiver is justified.

Retain the failed run. Add only a closed native Chromium network-error category to the
existing sanitized failure receipt, with exact token boundaries and unknown/ambiguous
fallback. URLs, response bodies, cookies, form values and exception messages are never
emitted. Permanent controls reject private suffixes, unknown extended identifiers,
multiple categories and nonbrowser text while preserving actual source locations. All
existing navigation, lifecycle, status, cookie-replay and timeout assertions stay intact.
This improves future evidence; it does not claim to fix or explain the prior failure.
The diagnostic change builds cleanly; all 110 local AppHost harness cases pass.
A separate local invocation of the actual built classifier passes its exact-end and
realistic network positives, private-data redaction, extended/unknown/ambiguous code
and nonbrowser negatives. Code-quality checks pass without allowances. SQL Server
also finishes disclosure 16/16 in CI590. Both providers therefore exercise the signed
expiry correction successfully; the failed demo receipt still prevents acceptance.

### Sixth hosted attempt — CI591

[CI591](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/43581385821)
checks source `6795edccf10bb1ec987b9762d5ceae296ecbe239`, merge
`3871c0e093d65c40f97f29465d1675cf255bca01`, tree
`d2d7cb8d25fed10b867633a51716339c95772932`. It finishes with 129 successful jobs,
two failed jobs (one browser group and final acceptance), and skipped coverage aggregation.
Both ordinary demos pass 3/3. Independent review reparses their original archives,
source-derived identities and every outcome counter, and confirms readiness, restart,
status and cookie assertions are byte-identical to accepted main. CI590's earlier
navigation failure remains unexplained reliability evidence, not a demonstrated fix.

The SQL Server/Chromium BrowserProtection group fails its application-scope journey
while awaiting the concurrent editor's scope-assignment PUT. The source-line chain is
SaveAsync → StaleAsync → RunAsync, after the genuine Allow/token/UserInfo and Cancel
journeys. The Cancel diagnostic normally remains at Started with no callback/exchange;
it does not mean that the Cancel journey failed. Other required groups and all 140
behavioral mutation controls pass; their results do not override this failed case.

Source review identifies a concrete readiness gap: ScopeController publishes the saved
assignment before its initial catalogue search finishes. The warning remains visible,
but Save is disabled while searching. Locator.PressAsync does not wait for enabled
actionability. The test could therefore send Enter to a disabled control and await a PUT
that never occurred. The permanent scenario now explicitly requires Save enabled inside
the original response waiter before one keyboard activation. Existing response, status,
one-send, stale-state, focus, audit, version and database assertions remain unchanged;
there is no retry, product change or timeout extension.

A bounded local native-browser reproduction invokes the actual compiled SaveAsync on
a synthetic HTTP form, with the same two-second disabled interval and five-second
investigation budget before and after. Playwright 1.63 with separately labelled system
Chromium 151.0.7922.173 records the original keyboard activation before enablement,
zero PUTs and timeout; corrected code records activation after enablement, exactly one
PUT and success. The administrator build passes with zero warnings/errors. This proves
the source readiness gap and its correction, not the unavailable original DOM/network
state in CI591. The original failure stays rejected; all four real retained-engine/provider
scope journeys and complete corrected-source acceptance remain required.
