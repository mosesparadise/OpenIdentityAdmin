# T020/T021 scenario inventory

Current correction inventory: native discovery preserves all 916 handoff entries and adds 105 SQL-only pure `SqlPrivilegeGuardTests` rows, total 1,021. PostgreSQL has 458 total/445 ordinary; SQL Server has 563 total/550 ordinary. Governance.Failures is 61/166; Policy 74 and Commands 68 per provider, plus 13 browser rows per provider, are unchanged. The 24 diagnostic rows introduced after the original implementation and these 105 pure controls do not claim database execution. [Correction evidence and guard scenarios](governance-failure-corrections.md). The original scenario inventory below remains the historical implementation breakdown.

Status: reconciled local implementation inventory; final acceptance remains pending independent review and strict CI. The user approved the three feature-local partitions and dedicated test-only driver on 13 October 2026. All rows are required on both provider labels; the driver transport/process controls explicitly do not access either database. Native discovery, exact row metadata and required manifests determine the execution inventory, not this document alone.

Actual added ordinary cases per provider: Policy 74, Commands 68 and Failures 49; total 191. Administrator ordinary 242 → 433 per provider; full native 510 → 892 across both providers. Existing Administrator browser13/provider and Issuer native644 remain unchanged. The approved pre-code estimate was 174 additions per provider (74/62/38), ordinary 416/provider and full native858; these remain historical planning figures, not current counts.

## Governance.Policy: 74 rows/provider

### P01 — GOV-01 (4 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.GrantTests.Given_RecognizedBundle_When_CurrentGrantsAreRead_Then_OnlyItsPersistedFiniteSetIsReturned`

Rows: `PlatformAdministrator`, `ApplicationAdministrator`, `IdentityAdministrator`, `Auditor`.

Independent literal grant sets (25/10/12/10), real memberships/claims; no production catalogue copied into expected values.

### P02 — GOV-01 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.GrantTests.Given_OverlappingRecognizedRoles_When_CurrentGrantsAreRead_Then_DistinctPersistedGrantsAreUnioned`

Rows: `overlap`.

Same valid permission from distinct roles succeeds; unrelated nonpermission claims do not change the set.

### P03 — GOV-01 (3 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.GrantTests.Given_NonauthoritativeClaims_When_CurrentGrantsAreRead_Then_NoAuthorityIsInvented`

Rows: `user-claim`, `principal-claim`, `custom-role`.

No reserved recognized grant source; result has no granted permission despite forged principal/user/custom role assertions.

### P04 — GOV-01,11 (15 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.GrantTests.Given_MalformedReservedAuthority_When_CurrentGrantsAreRead_Then_EvaluationDenies`

Rows: `role-name-case`, `role-name-padding`, `normalized-role-alias`, `role-id-alias`, `user-id-alias`, `claim-type-case`, `claim-type-padding`, `claim-value-case`, `claim-value-padding`, `wildcard`, `unknown-grant`, `out-of-bundle-grant`, `duplicate-role-grant`, `custom-role-permission`, `ambiguous-reserved-identity`.

Persist and read each independently invalid shape under both providers; exact ordinal comparisons, never SQL collation acceptance or silently dropped invalid grants.

### P05 — GOV-01,11 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.GrantTests.Given_DuplicateMembershipIdentity_When_Persisted_Then_NoAmbiguousAuthorityCanExist`

Rows: `duplicate-membership`.

Real provider membership PK/unique constraint rejects duplicate persisted keys; separately inspect defensive projection deduplication without pretending impossible SQL rows were seeded.

### P06 — GOV-01 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.GrantTests.Given_CurrentPersistedGrants_When_AnotherStoreRemovesOne_Then_NextEvaluationReflectsRemoval`

Rows: `cross-store-removal`.

Independent provider/context observes committed deletion immediately; removed grant remains absent; no bundle reconstruction or cached view.

### P07 — GOV-01 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.GrantTests.Given_UnknownRequestedPermission_When_Evaluated_Then_DecisionDenies`

Rows: `unknown-request`.

Finite permission request boundary fails closed independently of valid actor grants.

### P08 — GOV-02 (30 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.PopulationTests.Given_OtherwiseUsableAdministrator_When_OneRequiredConditionIsMissing_Then_AccountIsExcluded`

Rows: `missing-user`, `disabled-user`, `native-twofactor-false`, `missing-password`, `empty-security-stamp`, `native-lockout-disabled`, `missing-enrollment`, `pending-enrollment`, `consumed-enrollment`, `recovery-enrollment`, `unknown-enrollment`, `suspended-enrollment`, `zero-factor-generation`, `negative-enrollment-revision`, `missing-authentication`, `zero-authentication-generation`, `negative-authentication-revision`, `negative-failure-count`, `invalid-failure-window`, `native-lockout`, `product-lockout`, `clock-fence`, `invalid-current-time`, `missing-active-factor`, `corrupt-active-factor`, `transplanted-factor`, `stale-factor-generation`, `missing-protection-key`, `missing-platform-membership`, `missing-platform-grant-with-overlap`.

Start with real native password, protected factor, native TwoFactorEnabled, coherent rows and independent complete platform grants; alter one condition; excluded account cannot become a locked survivor. Where schema rejects a corrupt shape, record constraint rejection separately from a policy evaluation.

### P09 — GOV-02 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.PopulationTests.Given_ValidStagedFactor_When_ActiveMaterialIsUnusable_Then_StagedAccessCannotSupplySurvivorAuthority`

Rows: `missing-active`, `corrupt-active`.

Enter actual FactorAccess.Staged scope with valid staged seed; active-specific lookup still excludes missing/corrupt active material.

### P10 — GOV-02 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.PopulationTests.Given_ConfiguredUsableAdministrator_When_NoCurrentSessionExists_Then_PopulationStillCountsTheAccount`

Rows: `logged-out`, `session-expired`.

Complete native/protected credentials and grants count without a live session or recovery codes; do not confuse actor eligibility with survivor capability.

### P11 — GOV-02,07 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.PopulationTests.Given_CurrentPopulation_When_EvaluatedRepeatedly_Then_SecurityStateAndSemanticVersionRemainUnchanged`

Rows: one provider-only row. `read-only` was the planned intent label, not a native scenario argument.

Before/after snapshot includes failures, locks, factor/auth/enrollment generations and revisions, stamps, replay, proof and AuthorityRevision; queries cannot unlock or create proof.

### P12 — GOV-01,02,07 (8 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.CapacityTests.Given_BoundedAuthorityRecords_When_ReadAtLimitOrLimitPlusOne_Then_OnlyCompleteBoundedDecisionsSucceed`

Rows: `accounts-256`, `accounts-257`, `memberships-64`, `memberships-65`, `roles-128`, `roles-129`, `claims-128`, `claims-129`.

Assert actual complete distinct actor/target/affected/witness union for account limit; overlapping IDs count once. At limit use two genuinely usable accounts plus coherent nominees as needed, recognized role plus harmless custom roles for memberships, four reserved plus harmless custom roles for total-role bound, and finite approved grants plus distinct unrelated nonpermission claims for claim bound. At-limit result must succeed, overflow must deny without a truncated grant/survivor result. Independently observe bounded retrieval taking limit+1 before ordinal filtering; do not populate irrelevant rows and infer a bound was exercised.

### P13 — GOV-01,07 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.ConsistencyTests.Given_SerializedCurrentAuthority_When_AnotherWriterContendsBetweenReads_Then_NoMixedSnapshotEscapes`

Rows: `after-membership-read`, `before-claim-read`.

Pause actual production read at each SQL boundary under its real governance/account locks; independent governed writer must be observed waiting, not merely delayed. Reader returns one coherent pre-write snapshot, rolls back technical lock changes, then writer commits and next read reflects it. No unlocked ReadCommitted multi-query authorization credit.

### P14 — GOV-02,03 (3 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Policy.PopulationTests.Given_ValidAmbientMaterialSelection_When_GovernanceEvaluatesActiveAuthority_Then_SelectionIsRejected`

Rows: `staged`, `recovery`, `installation`.

Otherwise valid active-factor account; use actual FactorAccess.Select/SelectRecovery or a VerifiedStagedFactor created by its real VerifyAsync factory followed by Install. No setter/reflection/constructor bypass. Reject ambient material selection; active normal positive control remains P10.

## Governance.Commands: 68 rows/provider

### C01 — GOV-03,05,07 (8 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.CommandTests.Given_CurrentActorAndFreshBoundProof_When_SupportedCommandCommits_Then_StateVersionAndAuditAgree`

Rows: `assign-bundle`, `remove-bundle`, `remove-specialist-grant`, `restore-specialist-grant`, `disable-account`, `enable-account`, `suspend-account`, `resume-account`.

Real password+unused TOTP, authoritative full session, all required actor permissions. Verify exact intended change, AuthorityRevision+1, one accepted audit with actual actor/target (distinct for cross-account commands) and fixed action, commit before success. Removal/disable/suspend has a locked other usable administrator; assign to nominee never fabricates enrollment. Use self-demotion, self-disable and self-suspension as the remove-bundle/disable-account/suspend-account positives with a locked other survivor; post-write commit validation must allow the intended loss of the actor's authority. Other rows prove cross-account/role audit attribution.

### C02 — GOV-05,12 (4 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.CommandTests.Given_PrivilegedActor_When_UnsupportedAuthorityChangeIsRequested_Then_NoWriteOccurs`

Rows: `rename-reserved-role`, `delete-reserved-role`, `unknown-or-out-of-bundle-grant`, `delete-account`.

Reserved role lifecycle and user deletion remain unsupported; no manager dispatch or override. Keep exact pre/post durable state/version/audit/proof unchanged.

### C03 — GOV-03,05 (4 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.ActorTests.Given_OtherwiseCurrentActor_When_RequiredPermissionIsMissing_Then_CommandDenies`

Rows: `users.write`, `roles.permissions`, `users.disable`, `administrators.assign`.

Choose corresponding command so every required authority is independently necessary. Ordinary specialist role permission without administrators.assign cannot change reserved authority.

### C04 — GOV-03 (10 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.ActorTests.Given_ConfiguredAdministrator_When_CurrentSessionBindingIsInvalid_Then_CommandDenies`

Rows: `missing-session`, `revoked-session`, `different-user`, `changed-authentication-generation`, `changed-factor-generation`, `changed-security-stamp`, `untruthful-methods`, `idle-expired`, `absolute-expired`, `restricted-recovery`.

Invoke actual governed entrypoint with currently stored actor/session proof; no roles/claims/recovery interaction or usable-population result can replace it. Existing session tests retain detailed time boundary coverage.

### C05 — GOV-03 (10 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.ProofTests.Given_CurrentSession_When_SensitiveProofIsInvalidOrRebound_Then_CommandDenies`

Rows: `age-300-seconds`, `future-time`, `consumed-proof`, `wrong-password`, `wrong-totp`, `different-session`, `different-target`, `different-action`, `changed-payload`, `changed-expected-version`.

Private transaction-owned proof, native password/TOTP/replay mechanism; independent pre/post snapshots show no authority mutation. Wrong native factor attempts retain normal failure budgets; password success cannot clear earlier factor failures.

### C06 — GOV-04,05,10 (4 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.CommandTests.Given_LastUsableAdministrator_When_VoluntaryAuthorityIsDestroyed_Then_CommandDenies`

Rows: `remove-membership`, `disable-self`, `suspend-self`, `remove-shared-platform-grant`.

First three are single-admin destructive commands. Shared platform grant removal uses two otherwise valid platform admins and denies because neither retains the complete platform-role set; no count-only survivor oracle.

### C07 — GOV-07 (4 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.VersionTests.Given_OriginalIntent_When_ExpectedVersionChanges_Then_CommandDeniesBeforeWriting`

Rows: `authority-revision`, `native-user-stamp`, `native-role-stamp`, `enrollment-revision`.

Change exactly the relevant version after preparing intent; current locks do not rescue stale intent; rejected command consumes no authority version or accepted audit.

### C08 — GOV-07 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.VersionTests.Given_StableAuthorityVersion_When_ActivityAndGovernedChangesOccur_Then_OnlyCommittedAuthorityChangesAdvanceIt`

Rows: one provider-only row. `semantic-version` was the planned intent label, not a native scenario argument.

Read, acquire locks and run ordinary session activity without semantic increment; one committed command increments once; rejected/rolled-back command adds none, independently of governance lock Revision.

### C09 — GOV-03,07 (8 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.CommandTests.Given_AlreadySatisfiedCommand_When_Requested_Then_NoChangePreservesProofAuditAndVersion`

Native command rows: `assign-bundle`, `remove-bundle`, `restore-specialist-grant`, `remove-specialist-grant`, `enable-account`, `disable-account`, `suspend-account`, `resume-account`.

These correspond respectively to the original planned intent labels `assign-existing-bundle`, `remove-absent-bundle`, `add-existing-grant`, `remove-absent-grant`, `enable-enabled`, `disable-disabled`, `suspend-suspended`, `resume-resumed`. The source explicitly prepares already-satisfied state, then requires NoChange and snapshot equality; the shared command labels do not replace the no-change oracle.

Coherent target and valid actor. Exact NoChange; before/after native/product stamps, target state, full-proof budget/reset, replay reservation, audit and AuthorityRevision unchanged. No-op must not be counted as a successful protected transition.

### C10 — GOV-03,05,10 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.RevocationTests.Given_DisabledOrSuspendedAccount_When_ReenabledOrResumed_Then_OldAuthorityCannotResurrect`

Rows: `disable-then-enable`, `suspend-then-resume`.

Actual original AuthServer session plus local session and pending interaction. Disable/suspend commits authentication-generation advance and atomic revocations. Enable/resume does not un-revoke any old authority, reset failures or create/promote factors/enrollment. Old HTTP cookie stays denied; new login requires ordinary current native proof. Additional administrator setup is explicitly fixture provisioning.

### C11 — GOV-03,08 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.ProofTests.Given_RealTransactionOwnedProof_When_ReusedInAnotherTransaction_Then_ConsumptionDenies`

Rows: `foreign-live-context`, `original-transaction-ended`.

Create capability only through the legitimate private factory with real current session/password/TOTP in its owning transaction. Exercise its ordinary internal consumer with another owner/context or after owner disposal; no public constructor, reflection, mutated private fields or manufactured protocol object. No writes/audit escape.

### C12 — GOV-03,08 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.ProofTests.Given_NativePasswordRequiresRehash_When_OriginalSessionNoLongerValidates_Then_CommandRollsBackWithoutRebinding`

Rows: `native-password-rehash`.

Create a real older-iteration native password hash, then use current native manager settings to produce SuccessRehashNeeded. Observe actual native hash/stamp write inside transaction. Recheck the same original stored session: if native rehash changes its security binding, deny and roll back all rehash/target/proof/audit changes; never update the session to match. Ordinary unchanged-hash positives are C01. Verify pinned native behavior rather than substituting a fake hasher result.

### C13 — GOV-05,12 (4 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.RequestTests.Given_InvalidCommandShape_When_Validated_Then_NoDiscoveryOrNativeWriteStarts`

Rows: `unsupported-action`, `contradictory-target-fields`, `noncanonical-identifier`, `invalid-composite-version`.

Finite action enum invalid value, irrelevant user target on role-only action, case/padding/noncanonical ID, and invalid/missing required expected-version shape respectively. Assert fixed typed rejection and no native/provider write. Do not invent request setters or publicly constructible proof to exercise validation.

## Governance.Failures: 49 rows/provider

### F01 — GOV-04 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.ConcurrencyTests.Given_TwoGovernanceNodes_When_DestructiveCommandsContend_Then_OnlyOneCanRemoveAuthority`

Rows: `self-removal`, `cross-removal`.

Two independent child-driver processes execute production internal services against same real provider/key ring. Observe native UPDATE waiters behind known governance lock; release; at most one accepted mutation, one audit and one semantic increment, actual surviving usable witness; loser has no partial state.

### F02 — GOV-06 (5 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.WitnessOrderingTests.Given_LockedWitness_When_SecurityStateCommitsBeforeWaitingMutation_Then_WitnessIsRechecked`

Rows: `lockout`, `recovery`, `suspension`, `disable`, `active-factor-change`.

Hold exact witness authentication lock, observe mutation waiting at intended boundary, commit damaging witness state through legitimate fixed security path where supported, release. Mutation denies if no witness remains. Suspension/disable must not be portrayed as a survivor-guard bypass: arrange an already-safe authorized mutation/order or explicitly scoped test corruption to test stale read, with separate production semantics credit.

### F03 — GOV-06,10 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.WitnessOrderingTests.Given_SafeCommittedGovernance_When_MandatorySecurityEventFollows_Then_UnsafeAuthorityIsNotPreserved`

Rows: `lockout`, `restricted-recovery`.

Reverse ordering: governance commits with real witness, then actual lockout/recovery succeeds even if usable population falls to zero. No revived session/factor or fabricated survivor.

### F04 — GOV-07 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.ConcurrencyTests.Given_DiscoveredLockPlan_When_AffectedRelationshipsChange_Then_OperationDeniesWithoutLateLocks`

Rows: `new-affected-member`, `earlier-sorting-witness`.

Pause between discovery and transaction with test-owned seam; change memberships/witness set; independently observe lock sequence and stale typed denial. Never expand plan, retry or acquire earlier lock class late.

### F05 — GOV-08 (5 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.AtomicityTests.Given_NativeWritesBeforeCommit_When_OperationFails_Then_AllGovernedStateRollsBack`

Rows: `audit-insert-denied`, `manager-concurrency-failure`, `database-session-terminated`, `caller-cancelled`, `child-process-killed`.

Capture independent full pre-state; gate observed audit INSERT after real manager writes; revoke provider INSERT permission, native stale-stamp failure, terminate exact observed DB session, cancel, or kill actual command-owning child as appropriate. Grant/account/stamps/product revisions/replay/proof/accepted audit all rollback; no success frame. Restore permissions and drain ownership in finally.

### F06 — GOV-09 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.DurabilityTests.Given_CommittedCommand_When_OwnerDiesBeforeReturning_Then_NewNodeReconcilesWithoutReexecution`

Rows: `lost-result-and-restart`.

Pause output after real commit, prove state/audit externally, kill exact driver generation. Fresh process reads same state/version/audit and no second mutation is issued. BootstrapConsumed/receipt remain permanent.

### F07 — GOV-07 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.ConcurrencyTests.Given_HeldRequiredLock_When_OperationDeadlineOrCancellationExpires_Then_AllOwnershipIsReleased`

Rows: `five-second-budget`, `caller-cancellation`.

Native observed contention then actual five-second operation cancellation or earlier caller cancellation; no blanket retries. Await owned task, verify runtime-session drain and reacquire protected locks; timing alone is not ownership proof.

### F08 — GOV-11 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.PersistenceTests.Given_PreGovernanceSchemaAndData_When_Migrated_Then_AdditiveRevisionPreservesExistingAuthority`

Rows: `upgrade-and-readiness`.

Populate prior real migration with bootstrap receipt, users/roles/grants/factors/recovery/session rows; apply new migration using migration credentials. AuthorityRevision starts zero, data unchanged, previous schema rejected by readiness before service use; fresh provider migration also succeeds.

### F09 — GOV-11 (6 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.PersistenceTests.Given_InvalidSemanticVersion_When_StorageOrCommandChangesIt_Then_NoWraparoundCanCommit`

Rows: `negative-authority-constraint`, `authority-revision-overflow`, `technical-governance-revision-overflow`, `authentication-revision-overflow`, `authentication-generation-overflow`, `enrollment-revision-overflow`.

Provider-native negative constraint and max-value increments on each actual touched numeric field fail without wraparound, partial target state, accepted audit or consumed proof. Use DisableAccount for authentication-generation increment and SuspendAccount for enrollment revision. Do not reject a valid max-valued factor generation merely for reading it: governance does not increment that field; retained factor/recovery tests own its increment boundaries.

### F10 — GOV-11 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.PersistenceTests.Given_RuntimeDatabasePrincipal_When_GovernedChangesRun_Then_RequiredDmlWorksAndSchemaWritesDeny`

Rows: `runtime-privileges`.

Real runtime-role command succeeds with migration-owned schema; schema alteration fails. Do not execute governed work through administrator fixture connection.

### F11 — GOV-01,11 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.BoundaryTests.Given_DependencyOrProtectionFailure_When_DecisionDenies_Then_OnlyTypedSafeOutcomeEscapes`

Rows: `secret-safe-failure`.

Seed canary into provider/protection exception boundary with actual dependency denial where possible; no raw exception/connection/seed/payload in safe outcome/audit/diagnostics; no authority granted.

### F12 — GOV-03,08 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.BoundaryTests.Given_InitiallyValidProof_When_TimeChangesBeforeProtectedWrite_Then_CommitDenies`

Rows: `proof-expires`, `clock-rollback`.

Owned TimeProvider and real provider gate after proof-before-write; recheck absolute freshness/clock fence at protected boundary; no sleep-through-300s and no changed production budget.

### F13 — GOV-12 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.ExposureTests.Given_InternalGovernance_When_ProductionHostsStart_Then_NoManagementOrPilotAuthorityIsMapped`

Rows: `closed-surfaces`.

Inspect actual Admin.Api/Bff/AuthServer routes and send direct requests to closed surfaces; no new HTTP/debug/force command. Driver-only input path absent from production projects; capability factory not publicly constructible; pilot principal cannot authorize.

### F14 — GOV-10 (1 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.RecoveryTests.Given_SoleAdministrator_When_ExistingRecoveryCompletes_Then_RestrictedAuthorityAndGovernanceRemainDistinct`

Rows: `sole-admin-recovery`.

Reuse real recovery HTTP journey/oracles; recovery/code once-only, restricted replacement then new full factor/session; ordinary last-removal cannot cite recovery code/pending nominee. Existing disabled/suspended/all-factor-loss negatives remain mandatory; AuthorityRevision not reset.

### F15 — GOV-03,07,08 (4 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.NativeOwnershipTests.Given_NativeGovernanceManager_When_CancelledOrDetached_Then_TransactionOwnershipCannotEscape`

Rows: `role-manager-cancelled`, `user-manager-cancelled`, `foreign-context-owner`, `ended-transaction-owner`.

Cancellation rows observe actual native provider statements blocked under the transaction token, cancel and require bounded termination/drain/rollback. Ownership rows use managers from independent real DI contexts or an expired NativeOperation lease; fail before unmanaged writes. The RoleManager must propagate the same operation token through actual provider calls; token equality alone cannot authorize a foreign DbContext.

### F16 — GOV-03,10 (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.ReplayTests.Given_CommittedNativeTotp_When_ReusedAcrossCeremonyAndGovernance_Then_SharedLedgerDeniesReplay`

Rows: `ceremony-then-governance`, `governance-then-ceremony`.

Same account, active generation and actual code under existing native window. First legitimate operation commits reservation; independent second scope using the other real factory rejects exactly that code without accepted second authority. Start with a separately established current session and use FactorOracle unused code for first operation. Do not fabricate an AdministratorInteraction to authorize governance; real ceremony setup only supplies the ordinary ceremony side.

## Implementation-review additions (reconciled with native discovery)

The original 174 rows/provider are a planning estimate, not a ceiling. These additional regressions cover concrete implementation branches found during independent review. They do not change A-004, authorize external management surfaces, or constitute acceptance evidence.

### C14 — GOV-01,03: coherent actor credentials (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.ActorTests.Given_IncoherentActorAndHealthyWitness_When_GovernanceIsRequested_Then_NoProofOrAuthorityChangeCommits`

Rows: `negative-native-failures`, `stale-factor-allocator`. A separately enrolled healthy witness prevents the survivor check from masking actor eligibility. Read permission suppression, full-command denial and private verification-factory denial agree; exact full snapshot remains unchanged.

### C15 — GOV-03,08: distinct credential-denial effects (3 rows/provider)

- `IdentityPlatform.Administrator.Tests.Features.Governance.Commands.DenialTests.Given_ExpiredReplayEntry_When_NativeFactorIsRejected_Then_SavepointRestoresPrunedMaterialAndCountsOnlyDenial` — `savepoint-pruning-rollback`; actual successful one-row native pruning precedes rollback to the verification savepoint, with exact protected material restored.
- `IdentityPlatform.Administrator.Tests.Features.Governance.Commands.DenialTests.Given_FourEarlierFailures_When_FactorVerificationFails_Then_FifthDenialCommitsLockoutWithoutAuthorityChanges` — `fifth-failure-lockout`; exact product/native lockout and allowed native concurrency-stamp change commit with the denial audit, while old full-session validation denies.
- `IdentityPlatform.Administrator.Tests.Features.Governance.Commands.DenialTests.Given_FullProtectedReplayLedger_When_ValidCredentialsArrive_Then_UnavailableRollsBackWithoutCountingFailure` — `full-replay-ledger`; 64 unprunable protected entries and independently valid unused input produce unavailable completion, with complete rollback rather than a counted failed credential.

### C16 — GOV-03,08: native savepoint support (1 row/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Commands.SavepointTests.Given_ActualProviderSavepointCapability_When_NativeGovernanceProofIsRequested_Then_OnlySupportedTransactionsVerify`

Row: `native-savepoint-support`. PostgreSQL supplies a real supported transaction and successful native savepoint observation. SQL Server uses actual MARS connection options, requires `SupportsSavepoints == false`, and must reject before any native savepoint attempt. This is deliberately asymmetric provider capability evidence; PostgreSQL incapability is not fabricated.

C14–C16 add six rows/provider, changing Commands62 →68. F17 adds one final-witness row and F18–F19 add ten driver rows, changing Failures38 →49. Together they reconcile the historical174 estimate to actual191 per provider. Native parameter names vary by method; preserve exact metadata rather than inventing a common scenario parameter.

## Reuse and independence

- Reuse BootstrapFixture/BootstrapDatabase for shared real provider host and isolated migrated runtime-principal leases, FactorServices/IssuerMaterialFixture/FactorOracle for real native protected state, and DatabaseWaiters/DatabaseCommandGate/GovernanceLock for actual SQL boundaries. Add small governance-owned adapters, not copied fixture graphs. Valid second-administrator fixtures require manager-created password/stamp, actual protected active factor and coherent product rows; label them as fixture provisioning, never shipped onboarding.
- Reuse SecuritySnapshot style, but independently include memberships/claims, native stamps, enrollment/auth state, AuthorityRevision, proof/replay and structured actor/target audit. Expected grants/audit/version deltas must not call the production policy under test.
- Approved test driver: a test-only non-HTTP child must actually invoke the internal service for F01/F05/F06. Killing an idle AuthServer or a mock Task is no process-loss evidence. Capture exact child PID/start-time generation, bounded sanitized progress frames and cleanup, never credentials in arguments/output.
- Corruption rows which the real schema prohibits must assert native rejection and be identified as constraint credit. Do not remove production constraints merely to claim provider policy coverage. Defensive duplicate projection checks belong in focused policy tests with honest non-provider attribution if needed; they are not counted above as a fictitious persisted duplicate.

## Partition and evidence implications

- Add explicit Governance.Policy/Commands/Failures stories under existing Administrator project. Six provider invocations add six mandatory coverage inputs: 49 → 55. Preserve nine module floors, existing49 inputs and existing cases. Shared three-story boundary is a plan, not evidence that SQL elapsed execution fits660s; measure each once locally, split a cohesive story further if needed through reviewed exact union/disjoint discovery rather than increasing deadlines.
- Update eng/test-matrix.json counts; exact names in eng/administrator-test-matrix.json; scripts/test_selection.py exact namespace/story allowlists; scripts/verify.py argparse/partition rules and labels; .github/workflows/verify.yml six cells; scripts/check_coverage_inputs.py and independent scripts/test_browser_matrix.py six labels. Add omitted/duplicate/wrong-story/equal-count-substitution seeded selector cases; compare all old510 native UID/metadata identities with final inventory, not only count.
- Ordinary regression execution and coverage does not include intentionally failing mutation runs. Recommended separate governance mutation suite in the same existing Administrator project: six required mutants with unchanged180s build/execution and60s discovery per command; original issuer7/admin8 remain unchanged. This adds two mutation jobs but no ordinary coverage-input cells. Manifest/suite choice requires reviewed tooling packet, not an unreviewed extension of the closed8-item administrator manifest or ordinal cap.
- Six decisive mutants map to P06/C03 (bypassed-current-grant), F17 (omitted-witness-lock), C06 (skipped-last-admin-check), C05/F12 (stale/unbound-proof), F05 (unaudited-commit), C01/C08 (unversioned-write). Each needs a designated unique native CriticalDecision case/provider, independent fixed bypass assertion, native expected exit2, exact failed marker/stack, no run/setup/cleanup errors, baseline/restored controls and exact source restoration. Change neither surviving behavior nor assertion expectations to manufacture a kill.
- Driver is a test tool, not a tenth production coverage module. Ensure coverage collection follows child service execution where claimed; no exclusion of Core/Infrastructure paths reached only in child. Its own executable must pass current static-analysis/documentation/size checks.

## Deliberately bounded coverage

This inventory uses equivalence classes rather than an unbounded Cartesian product of every corrupt record and command. Existing factor, session, recovery, browser and lifecycle suites remain mandatory and supply their detailed boundary coverage; new governance rows verify correct integration and independent current-state decisions. Every specified GOV family has a decisive new or explicitly retained oracle. If coding introduces an additional semantic branch, update this packet and actual native inventory before acceptance instead of silently adding/deleting rows. Human observations, BFF/API activation, onboarding and incident causal closure are outside this inventory.


### F17 — GOV-02,07: final witness observation (1 row/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.WitnessOrderingTests.Given_FinalWitnessObservation_When_MandatoryLockoutRacesNativeCommit_Then_WitnessLockSerializesTheCommit`

Pause the exact real governance context at native transaction commit, after both authoritative survivor reads, audit persistence and final proof validation. A fifth real wrong-password attempt must remain blocked by the witness account lock until the governed commit completes. The seeded omission can receive mutation credit only if the security writer commits while that gate remains closed and the governed command subsequently commits with independently verified membership loss, revision, replay, audit and zero usable survivors. Timeouts and failed observations never earn that marker.

### F18 — bounded driver transport (8 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.DriverTests.Given_BoundedSyntheticDriverInput_When_TransportIsEvaluated_Then_OnlyFixedFailureAndTerminatedChildRemain`

Rows: `valid-input-composition-boundary`, `malformed-json`, `duplicate-root-property`, `duplicate-issuer-property`, `oversized-whitespace-frame`, `excessive-json-depth`, `closed-without-newline`, `unknown-root-property`.

The valid-input control crosses validation and reaches the deliberately invalid composition boundary; its fixed expected failure is not successful governance execution. All eight controls exercise bounded input/output and owned-child termination without contacting either database. Provider labels repeat this transport contract.

### F19 — real owned-process lifetime (2 rows/provider)
`IdentityPlatform.Administrator.Tests.Features.Governance.Failures.DriverTests.Given_OwnedDriverDescendant_When_LeaderExitsOrTerminationIsIgnored_Then_BoundedGroupCleanupLeavesNoLiveGeneration`

Rows: `leader-exited-descendant-holds-stdout`, `stubborn-descendant-parent-hard-stop`.

These controls use real Linux processes without contacting either database. The driver owns one fixed Linux process group and retains PID/start-time identities for its leader and observed descendants. Cleanup verifies that retained generations have terminated and that the owned group has no live members. The controls cover a leader exiting while a descendant holds stdout and a descendant requiring the parent's hard stop. This is bounded cleanup of the owned process group; it does not sandbox arbitrary forks or contain descendants that deliberately escape into another session/process group. No cgroup, PID namespace or equivalent general process-tree containment is claimed.

### Native discovery checkpoint — 13 October 2026

Actual native discovery contains 892 administrator identities: the original510 plus382 governance rows (Policy74, Commands68, Failures49 per provider), with unique native IDs and display names. Clean base `a4c2ea206593830defe5406106b46eda492598a4` and final complete build outputs were each executed from the same absolute staging directory: every original native UID, display name, full type signature and trait was preserved in the final same-path comparison. The earlier872 checkpoint was intermediate; twenty provider-labelled driver identities account for the final increase. Executing identical tests from different output directories changes native UIDs, so cross-directory UID comparisons are invalid; filtered-versus-full comparisons within an invocation remain exact. Within new governance metadata, C08 carries `CriticalDecision=governance-version` and F12's `proof-expires` row carries `CriticalDecision=governance-stale-proof` on both providers. These additions do not change baseline metadata. Discovery is not full execution acceptance.

F13 also requires four additional ordinary production-composition rows: two AuthServer settings and two API/BFF hosts. Native discovery confirms Host 340 and contract core 42, with all seven existing contract browser rows retained. Exact full endpoint inventories passed locally; direct HTTP absence probes are complementary, not substitutes for route enumeration.

### Local execution checkpoint — 13 October 2026

PostgreSQL Policy74 and Commands68 pass. A selected22-case driver/process/failure cohort passes: ten driver controls, F06 lost-result1, F05 atomicity5, F04 changed-plan2, F01 two-node2 and F07 cancellation2. This is not a22-case database-only cohort: the ten driver controls do not access the provider. The four route-inventory controls and ordinary unit431 pass; static analysis including seeded controls passes. Final strict both-provider/browser/native-mutation/complete-matrix/coverage acceptance remains pending. These local receipts do not complete T020/T021 or close historical and human gates.

### Precise security-mutation credit

The six governance defects are current-grant bypass; omitted final witness lock; omitted platform membership in the shared survivor predicate; expired consumed proof; omitted accepted audit; and omitted writer AuthorityRevision advance. The last-administrator defect preserves both projected and actual checks and tests their common membership predicate. Its marker requires durable removal of the only platform membership and a real accepted revision/replay/audit transition. A projected-check-only omission remains protected by the independent actual-survivor defense.

The version defect tests the actual native writer's postcondition inside its original transaction, which is always rolled back. Both independent commit revision defenses remain active. Its marker establishes an uncommitted writer invariant violation, **not** an unversioned durable commit. Current-grant, witness, survivor-membership, stale-proof and audit markers require their independently specified actual durable unsafe effect. Generic errors, missing observations and cleanup failures never constitute a killed security mutant.

The existing administrator replay mutation moves from `CeremonyReplay` to its extracted shared `CeremonyReplayLedger`, retaining the same defect, test identity and old eight-mutant obligation. No original mutation is removed.

### Completed local PostgreSQL partitions

The final ordinary PostgreSQL governance partitions pass independently: Policy74, Commands68 and Failures49, with no skipped or incomplete case. Their strict wrappers bind execution identities to native discovery. This covers all191 new PostgreSQL cases, including the ten provider-labelled driver controls that make no database calls. SQL Server, the existing full regression/browser matrix, actual mutation baseline/killed/restored controls and all55 coverage inputs remain pending.

The administrator inventory now uses a repository-owned version2 wrapper containing exact provider name arrays, complete native type metadata and every trait. Both complete and filtered ordinary/browser discovery and administrator/governance mutation discovery validate this portable metadata. Native discovery retains its own version1 schema. Path-dependent native IDs remain exact within each complete/filtered run, while location fields are excluded from portable comparisons. This reviewed manifest is not an immutable historical baseline; preservation of the original510 records is separately evidenced by the same-path comparison above.
