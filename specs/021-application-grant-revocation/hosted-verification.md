# Grant-revocation hosted verification

## Run535 and persisted administrator target protection — 6 November 2026

[Run535](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42186722362)
at `bdad7626383c0bd4a00bc544c37305b1617e8162` passes all 124 jobs. Independent
[raw receipts](run535-evidence.json) reconcile 51 grant,111 ProtocolAdmission and 131 issuer-flow
cases per provider:586 selected native passes, zero failures/skips, six artifact hashes
and exact TRX identity/execution bindings. Both controlled replay cases include all 12
binding checks, the actual failed UPDATE, explicit subsequent cleanup and unaffected
control; both original standalone cases pass. This verifies the bounded correction,
not closure of the initial replay/late-issuance window.

The remaining T005/T006 administrator-ID guard now has two passing real-provider cases
in [protected-target receipts](protected-target-evidence.json). A genuine enrolled fixture
creates an ordinary managed disabled target and nonempty native grants. The production
target reader passes its disabled/version/persisted-state checks in an owned transaction
with actual governance/profile/actor locks. Changing only the persisted administrator
profile's ApplicationId makes that same guard throw exact Forbidden. Complete native
scalar/association, target configuration/policy/audit and entire profile digests stay
unchanged; no grant job, approval or progress audit exists. Finally restoration recovers
the exact original digest, and the same original intent admits under genuine current API
component authority with one immutable approval and zero progress.

This explicitly isolates the target guard. Changing the administrator profile during
HTTP testing would make earlier bearer authentication reject and could mask a missing
target guard. No HTTP-authentication result is claimed for this case; the previously
verified genuine API/BFF target negatives remain separate evidence. Independent design
and source review found no blocker and supports T005/T006 closure with both-provider
execution. All real schema constraints stay intact; no production code changed.

The Administrator build passes with zero warnings/errors, informational-severity style
verification passes, syntax-aware quality passes without allowances, and all 74 selection
controls pass. Native discovery preserves all 2,581 prior portable records/order and adds
exactly two protected-target cases (2,583 total;52 ordinary grant cases/provider). Provider
execution was serial; the unused SQL image cache was released after its completed test
to fit PostgreSQL in the current vfs environment. No database/source/baseline work was deleted.

T008/T010/T012/T015/T016/T018 remain open. Current-head full CI, original deadline/current
permission/MFA and publication tests, remaining authority races, real-browser recovery,
complete-spec review and the documented initial replay window remain distinct obligations.
PR124 stays draft/unmerged; main and every historical/manual/Firefox/A-009/production/
public-release gate remain unchanged.


## Run534 and controlled pilot replay correction — 6 November 2026

[Run534](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42138440154)
at `2a18e68cd340c967b91e03399588e5e3fe1b5daf` completes124successful jobs, zero failures
or skips. [Independent raw receipts](run534-evidence.json) reconcile 51 grant and 131 issuer-flow
cases per provider:364selected native passes. The exact-head invalid-input test awaits
the new target helper, so its missing/stale/foreign-version/unmanaged/native-profile and
persisted-pilot denials plus authorized control are verified on both providers. A green
run did not establish the cause or repair of the earlier replay failure.

A new real-Kestrel pilot journey now controls that ordering. The test obtains a genuine
password/S256 code and pauses the actual native ProcessSignIn handler immediately after
redemption. A fresh observer sees Redeemed; a competing exchange completes invalid_grant
and another fresh observer sees Revoked. Only then does the winner continue to200 and
independent issuer/audience/signature/nonce/lifetime validation. A later replay returns
invalid_grant but the winning access remains unrevoked. PostgreSQL reproduces the exact
final failure in one executed native case. This proves a concrete mechanism consistent
with CI533; CI533's original scheduler ordering was not captured.

The bounded correction registers a pilot-only cleanup handler just before native token
entry validation, retaining native filters and responses. It handles already-revoked
codes only after exact request/client/presenter/token/authorization/subject/application
and persisted pilot-manifest binding. Native RevokeByAuthorizationIdAsync repeats chain
cleanup, catching known relational/cancellation failures with a finite message and
preserving native rejection. It neither changes token validation nor retries issuance.
It is registered in the common protocol setup for both standalone and mixed pilot hosts.

**Limit:** this does not serialize pilot redemption and winning issuance. A token can
still be issued after the initial replay completed and before a later replay cleans it
up. That window remains unresolved; this is not complete race prevention or a public
release/security acceptance claim. The new regression restores the existing final-replay
oracle and must not be presented as proof of broader serialization.

Independent source review required explicit LegacyPilot observer scope selection and
exception-preserving release/join. Both are implemented. Cleanup failure is injected at
the real native token UPDATE; the test requires one actual failure, invalid_grant and
unchanged valid access, then separately repeats replay after disarming the fault. A
second genuine authorization remains distinct and exchanges for cryptographically and
natively valid access. Twelve isolated component negatives alter request/endpoint/grant,
principal/type/presenter/subject and token/authorization bindings, each requiring the
complete native scalar/association digest to remain unchanged. Altered principals are
not credited as cryptographically valid HTTP input.

The initial corrected PostgreSQL case and original standalone PostgreSQL replay case
pass. The sole edit after the initial corrected receipt was removal of an unused import;
additional guard coverage is separately built and executed. Final controlled PostgreSQL and SQL Server plus both original standalone issuer cases
pass1/1each, with zero skips. [Hashed receipts](replay-correction-evidence.json) record
the original1/1failure and these four passes; all three production assembly copies have
the same SHA256. Final controlled-test reports postdate every C# edit. Persisted manifest,
application and authorization-subject mutation permutations are not exhaustively covered. All2,579 prior native
Administrator identities/portable metadata remain; exactly two provider cases are added
(2,581total, ProtocolAdmission111/provider). All74selection controls and syntax-aware
quality checks pass without allowance growth. Full corrected-head CI remains required.

The resumed environment had lost its temporary SDK/package cache. The pinned10.0.403
SDK, locked packages and existing HTTPS development-certificate trust were restored.
Unused test image caches were removed only after verifying zero containers/volumes,
recovering approximately6GB; source, databases and frozen baseline files were untouched.
This changed the prior disk blocker sufficiently to run the focused local PostgreSQL
case. No expired diagnostic approval, production SQL experiment or browser waiver was used.


## Run533 focused acceptance and unresolved replay — 6 November 2026

[Run533](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42133604768)
at `da6f378100006bb317325408c44566ec956f49b7` completes with120successful, three failed
(including final acceptance) and one skipped job. It is **not accepted overall**.
[Independent receipts](run533-evidence.json) reconcile four ZIP digests, every native
result/definition/execution binding and the exact-head discovery inventory:

- PostgreSQL and SQL Server grant suites: 51 passes each.
- PostgreSQL and SQL Server managed-issuance suites: 64 passes each.
- Four ordered admission/re-enable cases, eight extended genuine issuance cases and
  both actual-commit-timed cross-instance cases pass: 230 selected native passes total.

Independent evidence review closes T009 and T014. This does not waive other tasks or
make the PR mergeable. Static job127616903897 reports IDE0005 on the unused EF import in
`GrantIssuerLifecycleTests`; the correction removes that import. The earlier local style
command omitted informational severity, so it did not detect this diagnostic.

Issuer PostgreSQL flows job127616906260 fails the existing
`CodeFlowTests.Concurrent_valid_redemption_has_exactly_one_winner_and_replay_fails`
final native `HasStatusAsync(..., Revoked)` assertion. One winner, independent cryptographic
validation, concurrent-loser invalid_grant and final-replay invalid_grant all passed.
The actual access status was not retained. No transient/cache explanation is established;
entity caching is disabled and observation uses a separate scope. OpenIddict7.7.2 source
suggests a candidate ordering: redeemed-code replay revokes existing rows, winner issuance
adds a valid access entry afterward, and later replay rejects the already revoked code
without repeating family revocation. This remains a hypothesis about this CI failure.

The test now retains that exact `HasStatusAsync` gate and emits only bounded finite
status/type labels and whitelisted library error IDs after a failing observation. It
never converts failure into success on a later read, logs credentials/native identifiers,
retries a request or alters library/product behavior. Independent source/security review
found no blocking issue after restoring native case-insensitive status semantics.

Changed legacy code must comply immediately. Both provider identities are explicitly
renamed from `Concurrent_valid_redemption_has_exactly_one_winner_and_replay_fails` to
`Given_ConcurrentCodeRedemption_When_OneRequestWinsAndCodeIsReplayed_Then_WinnerAccessIsRevoked`
in the same `CodeFlowTests` class. Provider and native-replay traits, method body oracles,
mutation marker and ordinary counts remain. The mutation manifest and current assurance
reference point to the new method; immutable historical evidence retains its old name.
Exactly four obsolete quality allowances are removed, with none added or rebound.
Native discovery verifies646 issuer cases and both renamed cases; nine selector controls,
quality and the345-control assurance consistency check pass. The issuer build passes
with zero warnings/errors after satisfying the cached CompositeFormat analyzer.

The next target-boundary extension reuses the existing grant input/proof scenario on
both real HTTP surfaces. It seeds eligible native rows, requires precise missing404,
stale/foreign-version412, unmanaged/native-profile409 and persisted pilot-ID403 responses,
compares complete native/policy/audit digests and requires no grant jobs/approvals/audits.
A final valid202 control proves the caller remains eligible. Fixture review corrected
an impossible policy-profile mutation: the actual schema permits only the managed
profile, so the test instead removes/restores the native PKCE requirement through its
manager. All relational constraints remain intact. Complete native scalar/association
comparison reuses `ManagedJourneyEvidence.NativeAsync`. Administrator persisted-ID
coverage remains a separate open obligation; neither that nor runtime success is inferred.

The final Administrator build also passes with zero warnings/errors; code quality passes
without allowance growth. Native discovery preserves all2,579 Administrator identities,
portable metadata and order; only six expected source-line locations move. The first
strict whole-JSON comparison rejected those line moves; the portable-inventory comparison
then passed. Explicit changed-file formatting includes informational severity.

Local provider execution is unavailable because of the recorded shared disk exhaustion;
no additional container retry or timing waiver was introduced. Hosted execution of the
latest corrections/extensions remains required. A bounded replay follow-on can use the
existing real-Kestrel pilot fixture with a test-only handler just after native redemption:
hold the winner, observe the loser's actual replay response, then release issuance.
Do not claim a product fix or close the failure from an unexplained green rerun.
PR124 remains draft; all historical/manual/release gates persist.


## Successful integrated checkpoint and issuer acceptance additions — 5 November 2026

[Run531](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42123227146)
at `0a5fc764cd5ea66feb5e8275089d7e8329c35f1d` finishes all 124 jobs successfully,
with zero failures/skips. Independent [raw evidence reconciliation](run531-evidence.json)
verifies 25 ZIP digests against GitHub metadata and 282 selected native passes: 50 grant
and 24 persistence cases/provider, 32 in each retained protection-browser cell and three
ordinary local-demo cases/provider. All 110 behavioral mutants are killed with clean
baseline/restored runs and source hashes matching this exact head. The mutation merge
ref has the expected main/head parents. The merged report passes all 15 modules at the
unchanged 90% line/80% branch floors. Hosted gate/merge logs account for all 100 required
coverage inputs; the reviewer did not separately download all 100 inputs. This is a
successful CI checkpoint, not complete-spec acceptance or a public-release decision.

Independent task review closes T007, T011, T013 and T017 against these receipts. It keeps
all other unchecked tasks open for the concrete boundary gaps listed in tasks.md. The
previous missing-lock fixture and all four admission/re-enable race cases now pass on
both providers; their earlier pending-runtime statements below are superseded.

Current additions preserve all 2,577 prior portable test records and ordering. The eight
existing genuine issuance/disable race identities now continue through actual HTTPS grant
admission, bounded batch completion, exact valid/inactive eligibility and preserved redeemed
rows. Two new cross-instance cases use separate real HTTPS issuer hosts, independently
invoke native token and authorization checks in owned requests, retain unrelated valid
controls, and measure one five-second elapsed bound from the actual progress-commit callback.
Peer exchange validates genuine signatures/issuer/nonce/lifetimes; old-code HTTP denial is
explicitly separate from the causal entry oracle. Completion precedes re-enable and fresh
password/MFA/consent. No production endpoint or audience was added.

Source review found a missing Owned selection before resolving native managers in the
new test harness; it is fixed. Initial compilation found a private token-type constant,
nullable native identifiers and formatting errors, all corrected. Final build has zero
warnings/errors, explicit-file format verification and code-quality checks pass, and all
74 selection, 16 browser-story, eight evidence and three coverage controls pass. New native
discovery totals are 2,579 cases, with 51 ordinary grant cases/provider. No local container
retry is claimed with the shared filesystem nearly full. Corrected hosted execution of
these new issuer cases is required before closing T009/T014. PR124 remains draft; historical,
manual, Firefox/A-009 and production/public-release gates remain open.


### Evidence representation correction

The first issuer-test candidate was published before its staged source-scan result was
inspected. That scan flagged eight source-file SHA-256 values next to filenames containing
security-related words. They are verified public source checksums, not credentials. The
report now represents all 18 source hash maps as explicit path/sha256 records, preserving
all 92 bindings and every checksum. The corrected staged source scan passes without any
allowlist change. Run532 is superseded by this corrected candidate; no result from that
candidate is inferred as acceptance.

## Ordered lifecycle acceptance and metadata fixture correction — 5 November 2026

[Run530](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42121786444)
at `5a288d2569be7b70d8c049f9a58df197cb6382a6` gets beyond the corrected selection gate.
Both ordinary grant lanes execute 48 cases: 47 pass and one fails. Downloaded ZIP digests
match GitHub metadata; native TRX outcomes are retained in [run530-grant-evidence.json](run530-grant-evidence.json).
The alternate effective approver, wrong factor generation and retained-key expiry cases
pass on both providers. Each missing-lock case fails during fixture setup at a restrictive
foreign key, before exercising the worker. The run cannot satisfy acceptance. Its remaining work is superseded by the corrected
candidate under the existing cancel-on-new-head workflow; no full-run pass is claimed.

The corrected fixture removes only the actor's interaction, replay, recovery-code,
factor and session dependents before deleting authentication metadata in one real
transaction. All foreign keys remain enforced. Native grants and immutable approval/audit
history remain untouched. Independent review confirms all five actual dependents are
covered and missing-lock acquisition is classified as Storage. Corrected runtime remains required.

Four new T009 cases order admission versus re-enable on independent API roots, in both
commit orders on both providers. A commit interceptor pauses the winner after its real
checks/writes, captures the loser's exact provider session and requires the native lock
wait graph before releasing the winner. Successful provider callbacks must show one winner
commit and no loser commit. Admission first requires enable409, unchanged native/policy
state and one queued approval/audit. Enable first requires admission412, exact lifecycle
state/audit and no leftover job/approval/grant audit. Shared cleanup releases and joins all
operations. These are current-authority component tests, not HTTP issuance proof.

Independent architecture/security/test review found no blocking source defects. The
Administrator project builds with zero warnings/errors after formatting corrections;
code-quality checks and all 74 selection, 16 browser-story, 8 evidence and 3 coverage
controls pass. Actual discovery preserves all 2,573 prior portable records/order and adds
four cases (2,577 total; 50 grant cases/provider). Updated independent selection expectations
remain explicit. No local provider retry was attempted with the shared filesystem nearly
full; corrected fixture and race execution require hosted runners. Issuance races, genuine
cross-instance native validation and broader boundary/full acceptance remain open. T009
is not complete, PR124 remains draft and all historical/manual/release gates persist.

## Inventory gate correction — 5 November 2026

[Run529](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42107559474)
at `fab66f16a49b1937c2fb5cf0da8c80bd31812b7b` finishes25successful,98failed
and one skipped jobs. The UI correction passes its hosted job. Of the failures,96
stop at the lifecycle selection control before native execution, one fails locked NuGet
restore with a connection reset, and final acceptance fails. No new provider result
is inferred from these jobs.

The three related selection-control suites retained pre-expansion counts despite the
updated native inventory. Full local selection discovery reproduced four failures out
of74 controls. Corrected expectations now match the independently reviewed eight additions:
2,573administrator cases;1,162/1,267ordinary administrator cases and1,541/1,646complete
provider cases for PostgreSQL/SQL Server. All2,565prior portable records and their order
are unchanged. No selector, partition, identity assertion or execution gate is removed.

All74selection controls now pass, as do16browser-story,8evidence and3coverage controls
and instruction integrity. Independent test review reproduced the74passes and verified
the exact count changes and preservation. The earlier local check omitted these related
selection suites; future inventory updates must run the complete selection-control family.
The corrected hosted provider execution and remaining feature acceptance are still required.

## Worker correction and expanded acceptance — 5 November 2026

[Run528](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42095693143)
at head `a2f159580ae9ca9752b43912e597ea534484fae9` finishes119successful/four failed/one
skipped jobs. It remains rejected. Downloaded ZIP digests and native reports were
independently reconciled in [run528-evidence.json](run528-evidence.json):

- Both real-provider grant suites:44passed each.
- Complete persistence suites:24passed each, including fresh/populated migration and
  frozen-producer preservation; feature021 T004 is complete.
- Ordinary Aspire local-demo:3passed each, including actual browser grant submission,
  real Worker completion, re-enable/fresh sign-in and native lifecycle cleanup.
- All four retained Chromium/WebKit protection cells:32passed each, including the new
  genuine grant browser journey and the corrected prior login-focus case.
- Six new grant behavioral mutants killed on each provider, with clean baseline/restored
  receipts and source hashes matching this head. This is12actual kills, not compile failures.

Remaining CI failures: one UI polling test observes `loading` before native WebCrypto
hash validation completes; two unrelated SQL WebKit jobs fail locked NuGet restore with
connection resets; final acceptance fails and aggregate coverage is skipped. The UI test
previously assumed20event-loop turns completed the operation. It now awaits the actual
published terminal observation with a bounded test deadline. A deterministic digest
entry/release barrier proves pending work remains loading and cannot send a mutation.
Independent review caught an initially unarmed hash barrier; the corrected test now
awaits entry into the native digest mock before releasing it. Both polling tests retain
their original read-only/single-send assertions. All83UI cases
pass locally after correction, without product timing or security changes.

Eight additional provider cases compile: an independently enrolled and genuinely
password/MFA-authenticated approver continues while the original account remains disabled,
then loses its own persisted role authority; a missing actor-lock row; a mismatched current
factor generation; and a retained key exactly at24hours with genuinely renewed password/MFA
authority. Original approval/native-state oracles are preserved. The shared fixture still
creates native API entries at a documented component seam; this is not signed bearer or
browser acceptance. All2,565prior portable discovery records/order remain unchanged, with
eight additions (2,573total;48ordinary grant cases/provider).

Independent security/test review also corrected the replay case to assert the coordinator’s translated ApplicationHttpRejected401/409 rather than its internal exception types.
The initial expanded build lacked an import and was corrected. Final builds and quality
checks pass. A focused PostgreSQL attempt could not start its database when the shared
filesystem filled; all three selected cases failed fixture setup and none counts as a
behavioral result. Docker cleanup restored the prior small free space. No additional local
container retry is claimed; hosted execution is required for these new cases. The later
replay-expiry pair has not run locally. No new task completion is inferred from compilation.

Ordered issuance/re-enable/admission races, genuine cross-instance native entry validation,
remaining API/BFF failure boundaries and complete exact-head coverage/acceptance remain open.
PR124 stays draft/unmerged. Existing historical/manual/Firefox/A-009 and release gates persist.

## First implementation candidate — 5 November 2026

[Run527](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42090760689)
tests published feature head `a1b809cb4e0c075dd1475d6a048560fc7481f8d4` through PR merge
ref `1591301d4f753eac22dcb5c9385a1aa3343ef029`. Both ordinary grant lanes and the full
PostgreSQL persistence lane report success. Final job enumeration is109successful,14failed and one skipped. The run is rejected;
aggregate coverage was skipped. Three retained protection cells pass; PostgreSQL Chromium
passes31of32 with an existing login-focus assertion failure. Complete artifact reconciliation
and exact-head acceptance remain pending.

Confirmed failures and bounded corrections:

| Boundary | Observed failure | Treatment |
| --- | --- | --- |
| Static analysis | Import ordering and unused imports in changed files | Apply only the named import corrections; declarations and method bodies remain unchanged. |
| Native-image workflow control | Application mutation job is45minutes; its test still expected30 | Match the existing45-minute workflow budget. Product/request/startup deadlines remain unchanged. Seven controls pass locally. |
| SQL application mutations | Third mutant, `application-write-permission`, failed compilation after two earlier behavioral kills | Authority refactoring moved permission observation into a method without a command parameter. The obsolete mutant references that instance field from a static method. Replace its condition with `permission == CredentialPermission`, retaining the former credential-only enforcement bypass. |
| Package restore | Several jobs fail locked restore with NuGet `Connection reset by peer` | Preserve failed attempts. No dependency, lockfile, TLS or product retry changes; rerun through the corrected candidate. |
| Both ordinary Aspire demos | Browser-category failure at `LocalDemoBrowser.cs:73`, the new grant journey | The separate local-demo container graph omitted Worker entirely. Add its locked publish/pinned non-root runtime stage, Platform-only connection and migrator completion dependency. Require its startup and native cleanup. Retain safe source-location diagnostics for further failures. Actual corrected demo completion remains required. |
| PostgreSQL Chromium protection | Existing login-presentation dark case fails `BrowserLoginPresentation.ErrorAsync:52`, an immediate focus read after summary visibility | Use native bounded `ToBeFocusedAsync`; retain visible focus outline, error text, ARIA and cleared-input checks. No forced focus, sleep or retry of the journey. |

The mutation build failure is reproduced locally as CS0120. The corrected mutant and
restored API each compile successfully with `dotnet build ... --no-restore -m:1`.
This is compilation evidence only: both-provider behavioral kills remain required.
Independent test review confirms the replacement's equivalence and the unchanged
witness's credential-denial control and exact unauthorized-write failure marker.
All60 mutation-tool controls pass. Changed-file import verification and code quality
pass without allowance growth. An initial quality command omitted the SDK environment
and could not find dotnet; the corrected command passes.

The existing graph test now requires the actual Worker resource, exact Platform runtime
reference, no session/provisioning access or issuer keys, no published endpoint and one
migrator-completion dependency. Both provider graph cases fail before the correction
(missing Worker) and pass afterward. All110AppHost Harness cases pass on the corrected
source; AppHost and Administrator projects build with zero warnings/errors. Architecture review confirms the host needs no
issuer material. The runtime fixture waits for Running and includes Worker in owned
container/volume cleanup; only actual demo completion proves processing.

A temporary rendering-only diagnostic compiles the actual GrantPanel and exercises
system Chromium151 with synthetic HTTP responses. Open, missing confirmation, cancel,
action focus, reopen, keyboard submission and completed rendering pass with one POST.
The first diagnostic omitted required response security headers and was rejected; the
corrected fixture supplies those headers. This does not establish native authority,
Worker execution, retained Playwright-browser compatibility or Aspire acceptance.
No synthetic transport is introduced into the required real browser journeys.

Local broad container reruns remain blocked by shared disk exhaustion recorded in
[verification](verification.md). Source and retained evidence are preserved. PR124
remains draft/unmerged; remote main was reverified as
`dce682f9812d228b41ae7fad1dcb1f6636f40d38`. Remaining feature requirements, Firefox/A-009,
historical findings and human/production/public-release gates are unchanged.
