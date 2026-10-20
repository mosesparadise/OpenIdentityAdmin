# B2 implementation acceptance map

20 October 2026. Independent architecture/test/API source review maps the coherent feature 011 implementation to actual requirements, scenarios and tasks. The requesting user selected T005–T009 including required dependencies/tests; the approved plan requires US1–US3 together before B2 acceptance. This map includes the [final boundary additions](native-budget-replay-and-restore.md), whose hosted execution is pending. It is source traceability, not a passing runtime receipt or merge authorization by itself.

## Disposition

The reviewed architecture preserves the approved B2 boundary. No additional concrete implementation gap was identified after the bounded closures below. Acceptance remains conditional on final source/quality checks, both-provider execution, exact-head evidence reconciliation, independent security review and normal squash. Do not equate the source map with passing evidence.

Two review findings are closed in current source:

- `NativeParRaceTests.WarmAsync` completes an actual grant through the peer before arming the target redemption barrier; cold peer composition is outside the held transaction budget.
- `NativeConsumptionBoundaries.VerifyDeadlinesAsync` retains the existing code/state checks and additionally checks pushed request tokens on Platform. Each type receives independent principal and persisted expiry checks at offsets -1/0/+1. Existing test identities, production deadlines and unknown-commit controls remain unchanged.

The successful process callback also retains the existing deletion-cookie oracle through `NativeClientInteroperabilityTests.RequireCookie(attributes: false)`.

## Source-location key

- **AS**: `src/backend/IdentityPlatform.AuthServer/Features/Protocol/`
- **BC**: `src/backend/IdentityPlatform.Admin.Bff/Features/Authentication/`
- **PS**: `src/persistence/IdentityPlatform.Infrastructure/Features/ProtocolStorage/`
- **PA**: `src/persistence/IdentityPlatform.Infrastructure/Features/Identity/ProtocolAdmission/`
- **AT**: `tests/aspire/IdentityPlatform.Administrator.Tests/Features/ProtocolAdmission/`
- **HT**: `tests/backend/IdentityPlatform.Host.Tests/Features/`
- **PT**: `tests/persistence/IdentityPlatform.Persistence.Tests/Features/ProtocolStorage/`
- **E**: `specs/011-administrator-issuer-client/`

Names below refer to concrete source classes/test classes, not invented case IDs. Final acceptance should bind their actual discovered provider-qualified identities to retained reports.

## Functional requirements

| Requirement | Production ownership | Concrete controls and evidence mapping |
| --- | --- | --- |
| FR-001 distinct profiles and authentication | AS `Setup`, `ProtocolProfiles`, `Administrator/AdministratorDecisionPolicy`, `Administrator/AdministratorDecision`; existing account named schemes and pilot policy | HT `AdministratorProtocol/ProtocolCompositionTests`, `ProtocolConfigurationTests`, `PilotTransportIdentityTests`; HT `ProtocolStorage/AdministratorDecisionPolicyTests`; existing pilot suite. E `shared-host-implementation.md`, `account-issuance-implementation.md`. |
| FR-002 current full authority and strict freshness | PA `ProtocolAdmissionProof`, `ProtocolAdmissionOwnership`, `ProtocolAdmissionTime`; AS `Administrator/AdministratorAccountAuthority`, `AdministratorPrincipal` | AT `AdmissionTests`, `AdmissionDriftTests`, `NativeAccountIssuanceTests`, `NativeAccountDenialTests`, `NativeAccountFinalReadTests`, `NativeAuthorityRaceTests`; HT `ProtocolStorage/ProtocolAdmissionTimeTests`. Real enrolled accounts and persisted permissions are used. |
| FR-003 noninteractive behavior | AS `Administrator/AdministratorRequestPolicy`, `AdministratorDecisionPolicy`, `AdministratorDecision` | HT `ProtocolStorage/AdministratorDecisionPolicyTests`, `AdministratorPassthroughTests`; AT `NativeAccountDenialTests`; native login-required/access-denied response oracles. Product fresh-login continuation remains B3. |
| FR-004 fixed manager registration and no repair | PS `Administrator/AdministratorRegistration`, `AdministratorRegistrationPolicy`, `AdministratorPublicKey`; Migrator `Features/AdministratorProtocol/AdministratorRegistrationInitializer` and input/setup | AT `ProtocolInitializationCommandTests`, `NativeRegistrationDriftTests`; PT `NativeRegistrationInitialization`, `NativeRegistrationAuditFailure`; HT registration/input tests. E `native-wire-and-registration-controls.md`; operation guide `docs/operations/administrator-protocol-initialization.md`. |
| FR-005 asymmetric proof, PAR, S256 and native validation | AS `ClientAssertions/*`, `Administrator/AdministratorTransportGuard`, `AdministratorRequestPolicy`, shared native setup; BC `Protocol/NativeClientAssertion`, metadata/backchannel handlers | HT `ProtocolStorage/AdministratorAssertionTests`, `AdministratorAssertionIntegrationTests`; HT `AdministratorProtocol/AdministratorTransportTests`; PT `NativeAssertionAuthentication`; AT `NativeWireBindingTests`, `NativeClientInteroperabilityTests`, `NativeClientHttpsTests`. Native crypto remains authoritative. |
| FR-006 independent durable replay boundaries | PS assertion ledger and owned request/transaction infrastructure; AS `Ownership/DurableServerToken`, `DurableServerRedemption`, `DurableServerConsumption`; BC `Ownership/DurableClientState`, `DurableClientRedemption` | PT `NativeAssertionLedger`, `NativeConsumptionConformance`, `NativeConsumptionRaces`, `NativeConsumptionBoundaries`, `NativeConsumptionReceipts`; AT `NativeProtocolMutationTests` cross-endpoint assertion reuse, `NativeIssuerReplayTests` sequential PAR/code and linked revocation, new `NativeParRaceTests`, `NativeClientLifetimeTests`, `NativeClientProcessTests`. |
| FR-007 live registration/authority/native binding and commit linearization | AS `AdministratorIssuance*`, `AdministratorAccountAuthority`, `NativeIssuanceProof`; PS portable governance/profile/account ownership | AT `NativeAuthorityRaceTests` proves both actual commit orders for session revocation, last-permission removal and profile change; `NativeRegistrationDriftTests`, `NativeAuthorizationStateTests`, `NativeAccountFinalReadTests`, `NativeGenerationTests`, `NativeIssuanceCompletionTests`; PT receipt/freshness controls. E `native-authority-races.md`. Orphan authorization test rejects the missing restored binding before native missing-authorization lookup; do not describe it as direct unknown-ID lookup coverage. |
| FR-008 minimal claims and fixed audiences | AS `AdministratorPrincipal`, native claim destinations and issuance preparation | HT `ProtocolStorage/AdministratorPrincipalTests`; AT `NativeAccountIssuanceTests`, `NativeClientIdentityTests`, native access/identity oracles. No roles, permission snapshot, refresh or API-consumer authority claim. |
| FR-009 original bounded lifetimes and equality | PA `ProtocolAdmissionTime`; PS `AdministratorNativeToken`; AS lifetime configuration/issuance checks; BC state/time validation | HT freshness/time/claim tests; AT `AdmissionTests`, `NativeAccountIssuanceTests`, `NativeClientLifetimeTests`; PT `NativeConsumptionBoundaries` now explicitly covers code, PAR and state principal/persisted equality. Public ID-token NumericDate guards retain their native roundtrip and malformed-input tests. |
| FR-010 durable native client, fixed HTTPS and binding | BC `Credentials/*`, `Protocol/Setup`, profile/metadata/keys/validation/callback; persisted `AdminSessionDbContext` | HT `AdministratorClient/*`; AT native interoperability, signed identity mutation, HTTPS/TLS-trust, reload/missing-key and process-restart tests. E `native-client-composition.md`, `native-client-https.md`, `native-client-identity.md`, `native-client-reload.md`, `native-client-process-restart.md`. In-process native transports and actual HTTPS are reported separately. |
| FR-011 additive schema/grants, budgets, failure and restore | PS protocol model/ledger/token budget/cleanup/state capacity; provider migrations and runtime catalog; owned deadlines/cleanup; BC durable keys/state | PT native ownership, token budgets/race/cleanup, state creation/quotas, assertions; AT cancellation, database-read outage, issuance/client-creation completion faults, logging, restart and new quota/restore tests. PostgreSQL migrations `20261017224010_AdministratorProtocol`, `20261017224013_AdministratorClientStateBudget`; SQL Server `20261017224016_AdministratorProtocol`, `20261017224019_AdministratorClientStateBudget`. Existing schemas/histories preserved through provider catalogs. |
| FR-012 inactive product surface | AuthServer `Features/Setup` and `AdministratorProtocolConfiguration`; BFF/API `Features/Setup`; BC opt-in internal setup | HT production composition/default configuration/route tests plus existing host inventory. Direct source confirms BFF/API feature endpoint maps remain health-only; BFF production setup does not call `AddAdministratorClient`. Explicit AuthServer administrator protocol activation remains separate from its default. No test-owned callback is mapped by production composition. |
| FR-013 preserved assurance and exact-head acceptance | Existing matrix/selectors, mutation manifests, coverage floors, A-007 | T006/T024/T025 final evidence checks. Preserve every prior identity, 50 required coverage inputs/15 floors, all existing mutation suites plus six B2 provider mutation kills, Chromium/WebKit, retired diagnostic holds and all external gates. Counts must come from final discovery/reports. |

## Success criteria and acceptance limits

| Criterion | Source/test support | What still must be recorded |
| --- | --- | --- |
| SC-001 both-provider journeys | Actual enrolled account, native issuer/client and failure controls listed above | Final required provider runs with no missing/failed/skipped accepted cases; earlier green heads cannot certify changed source. |
| SC-002 separate replay boundaries/concurrency/restart | Assertion ledger race and native assertion processing; code consumption race; actual PAR race; callback race; independent roots and actual three-process restart | Final execution of new PAR race and process cases. Restart is graceful process exit/reimport, not forced-crash recovery. |
| SC-003 current authority | Admission/live reads, exact bindings, both-order real issuance races and mutations | Raw both-provider results and decisive original/mutant/restored receipts. No live Admin API enforcement is claimed. |
| SC-004 dormant default and preserved pilot | Production source maps and native composition/route tests | Exact-head default/pilot/combined checks; no B3 browser activation. |
| SC-005 scoped operational consequences/privacy | HTTPS loss/cancellation/TLS, native storage-denial recovery, keys/reload/restart, controlled completion faults/log capture and restore consequence test | New scoped restore result and honest limitations. Logs test covers actual client factory/formatted/direct structured-string output; it is not a whole-platform logging audit or recursive arbitrary object traversal. |
| SC-006 reviewed and delivered exact source | Current source review plus independent review/evidence process | Final source/run/tested-tree bindings, all applicable checks, approved normal squash and merge identity; task/roadmap/handoff reconciliation afterward. |

The restore test demonstrates exact code/state row resurrection on isolated real databases, followed by explicit persisted profile/session/native grant/token/state invalidation and actual callback denial. It does not execute a database backup restore, establish atomic cross-database recovery, prove all invalidation layers independently from one early callback rejection, or provide a production restore command. Production restore-invalidation automation remains missing and must stay explicit. Independent security/lifecycle source review of the helper and final coherent B2 security source review pass with these limits; exact-head provider execution and evidence reconciliation remain prerequisites.

## Task mapping T005–T026

| Task | Concrete implementation/evidence |
| --- | --- |
| T005 | Native handler descriptors/order and owned dispatcher/child transaction/response boundaries in AS/BC/PS; HT native dependency/store/error tests; PT native ownership/consumption/budgets; complete native AT journeys. Prerequisite PR57/60/62/64 delivery records remain valid historical evidence, not current-head substitutes. |
| T006 | `eng/test-matrix.json`, `eng/administrator-test-matrix.json`, selectors/discovery and coverage/mutation manifests. Await final discovered inventories and all required inputs. |
| T007 | PA admission proof/time/ownership; AT AdmissionTests/DriftTests and real account issuance. |
| T008 | AS shared setup/configuration and actual native combined composition; existing pilot/default route controls. |
| T009 | AS administrator decisions/principal/issuance/response ownership; native positive and safe denial tests. |
| T010 | HT administrator protocol, protocol-storage decision/principal/admission-time tests; disabled/pilot/admin/combined controls. |
| T011 | AT real authority cases, freshness boundaries, specialist/auditor and last-permission denial; see AdmissionCases and NativeAccountDenialTests. |
| T012 | AT NativeAuthorityRaceTests/Probe/Mutation/Outcome with actual provider lock observations and durable commits; native post-disable code oracle preserves crypto/reference/binding. |
| T013 | Additive provider Platform/Sessions migrations and catalogs/readiness/grants; native provider conformance and least-privilege runtime execution. |
| T014 | Explicit Migrator initializer; PT initialization/audit rollback controls; AT command idempotence/input/audit tests. |
| T015 | Native post-validation assertions plus durable ledger; PT assertion authentication/ledger/cleanup and both-provider cross-endpoint replay mutant. |
| T016 | Fixed client PAR configuration, transport guards and live registration checks; native PAR issuer and wire binding tests. |
| T017 | HT assertion shape/time/integration, registration/input, decision and principal boundary tests. |
| T018 | PT independent native consumption/quotas/concurrency/failure plus AT peer replay, wire bindings, authorization state, registration drift, issuance faults, new actual wire quotas and PAR contention. |
| T019 | Internal BC native composition, fixed metadata/profile and separate durable credential roles; no production client activation. |
| T020 | BC native state generation/redemption/expiry and backchannel no-retry bounds; PS state capacity/cleanup; PT creation/quota/concurrency controls. |
| T021 | AT actual native interoperability/signed identity negative tests and HTTPS fixture, preserving B1 cookie isolation. |
| T022 | AT callback lifetime/race, HTTP loss/cancellation, Sessions read failure, retained-key/replacement, creation failure/logging and process restart; PT state quota/cleanup/unknown completion. |
| T023 | `docs/operations/administrator-native-client.md` and registration/provider/issuer/session guides; scoped restore consequences. Missing production restore/rotation/cleanup automation remains documented. |
| T024 | `eng/administrator-protocol-mutations.json`, AT NativeProtocolMutationTests; exact decisive authority/assertion/profile original/mutant/restored receipts on both providers. Preserve all prior suites. |
| T025 | Final exact-head CI/raw reports/coverage/route/source review reconciliation. Rewrite stale planning/current-continuation summaries around final accepted evidence; do not erase historical failed runs. |
| T026 | Normal squash only after coherent B2 acceptance. Record reviewed source SHA, test run/tested merge tree, squash SHA, task and handoff updates. Currently pending. |

## Explicit boundaries retained

- All six historical findings and all 48 human assurance rows remain open/not assessed as applicable; no public-release or production gate is closed by this review.
- A-004 remains expired, A-005/A-006 concluded; no retired diagnostic is revived.
- Firefox is deferred only through internal B2 under A-007 and must return before B3 implementation/activation/release.
- B3 request-bound login, product B1 session establishment/CSRF/fixation/logout, live Admin API enforcement, refresh, React and public deployment remain outside this acceptance.
- No general registration repair/key-rotation/restore automation is implied by disposable-data external-writer tests.
- Existing component tests should not be recreated merely to obtain another wire-level variant. New evidence closes named requirements, not an unbounded security-hardening backlog.
