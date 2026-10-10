# T023 bootstrap acceptance plan

Status: complete on both real providers; [CI evidence](verification.md#t023-completed-ci-checkpoint). PR #5 awaits review. Planned before implementation on branch `004-bootstrap-acceptance`. Existing requirements checklist:7/7 checked; Spec Kit prerequisites resolve to feature004. No extension hooks. Independent architecture review accepted the isolated fixture, runtime permissions and deterministic race design.

Scope: LOGIN-001/008, US1/AC1-2; the existing approved feature plan's bootstrap convergence task. No administrator/product capability or acceptance relaxation. Runtime changes only for reproduced defects.

Fixture: separate health-only Aspire collection per provider. Create a unique disposable database per case, apply real Platform migrations, grant platform_runtime with the existing Aspire parameter value, invoke actual PilotBootstrap in fresh runtime scopes. Admin scopes only set up, inject denial, inspect and drop. No shared data reset. Test-only Migrator project reference supplies both migration assemblies and role provisioning. Nonpooled connections and bounded startup/test/drop cleanup. Failure to clean up fails the case/environment.

Case groups:
- Seven nonempty subsets of user/application/scope without manifest: reject without adoption/repair.
- Seven missing-record subsets with an existing manifest: reject without partial creation.
- Wrong user/application/scope ownership IDs; manifest username/callback drift.
- User rename, disabled, locked, lockout disabled, MFA, role membership, missing/changed password verifier.
- Callback drift; extra/missing permissions and requirements; client/application/consent type; settings override; enabled/profile markers; changed secret.
- Scope enabled/profile markers, empty/extra/wrong resources.
- Positive no-op with nonzero access-failed count: hash, stamp, state preserved, only verified-no-op audit appended.
- Fresh initialization concurrency: a test-only SaveChangesInterceptor gates two distinct contexts at their first Added PlatformUser, both before INSERT. Bound rendezvous, await both real operations, assert exactly one true creation winner and a classified conflict, coherent IDs/records and single created audit. Explicit rerun adds only no-op audit.
- Single-caller rendezvous cancellation drains the actual initialization task, leaves every table empty and permits a fresh explicit retry. The interceptor links the test deadline even when Identity supplies CancellationToken.None.
- Real audit INSERT permission denial on first creation and existing no-op: classify exact database permission error; compare all relevant state; restore permission in finally; explicit fresh-scope retry succeeds.

The suite adds49 cases per provider:44 conflict states, one valid no-op, one first-creation race, one cancellation control and two audit-denial states. Snapshot comparison covers users, roles/memberships, user/role claims, logins, Identity tokens/passkeys, native applications/scopes/authorizations/tokens, manifest, audit, sessions and interactions. Positive no-op/retry comparisons retain the complete original creation audit while separately requiring exactly one appended verified-no-op event. Empty and coherent-success assertions also reject unexpected related records. Compare with fixed-message boolean assertions; never serialize to logs/TRX. Native managers seed legitimate changes, direct EF corruption only for explicitly invalid persisted fixtures.

T024-T029 stay open; T026 links this bootstrap audit result instead of claiming its other fault obligations completed. Both real providers, exact matrix, seven CI jobs, narrow independent reviews and durable verification are required before marking T023 done. PR #4 merge authorization does not authorize merging the next PR.


## Review dispositions

Architecture review accepted the fresh-database/least-privilege design and confirmed the test-only Migrator reference. Test review caught an unbounded rendezvous when Identity supplies CancellationToken.None and a default password-policy mismatch; both were corrected before execution. A permanent single-caller cancellation control drains the task before cleanup. Follow-up review required complete Identity snapshots and original-audit preservation; both are implemented. Final architecture/test source reviews found no remaining scoped issue and did not claim test execution. Both-provider execution evidence belongs in [verification](verification.md).
