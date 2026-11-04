# Editor and credential recovery — 4 November 2026

Feature019 T017/T018 add visible edit, stale-version recovery, explicit secret replacement and operation-specific failure states to the existing Applications workspace on `123-application-editor`. The ordinary native Aspire `local-demo` entry remains the way to run it; see the [quickstart](quickstart.md). Merged `main` remains unchanged. Retained browser/native-demo acceptance is pending and T019 stays open.

## Implemented behavior

Managed details offer separate **Edit application** and **Replace secret** actions. Client ID is immutable. Editing accepts only display name and exact HTTPS callbacks. Replacement requires an unchecked confirmation and a newly chosen future whole-second UTC expiry, explains immediate invalidation of the current secret, and names the current-permission/recent-MFA requirement. Existing API/BFF authority, audit and atomicity are reused without changes to their decisions.

Both mutations use generated BFF types, exact opaque targets, native CSRF and the original strong If-Match. The UI preserves the observation and session deadline that opened the form through proof acquisition, dispatch and asynchronous response validation. Neither a response nor a successor version renews that authority. PUT accepts already-expired credential metadata; replacement output is additionally bounded by its new secret expiry. Exact resource identity, profile, successor revision and action-specific response fields are checked. Revision comparison uses integer arithmetic beyond JavaScript's safe-number range. Server-issued timestamps are compared to prior server metadata, without requiring the browser clock to equal the issuer clock.

412/428 responses expose explicit **Reload current details**, discard the stale draft, and never rebase or retry. Preflight failure states accurately identify the unsent operation. Unconfirmed dispatched results remain potentially committed. Replacement 403 explains both permission and MFA freshness without guessing which failed, and offers explicit sign-in. Known successful replacement status survives secret dismissal and page hiding.

One-time output remains outside live announcements and is manually selectable. Opening either mutation form immediately drops previous secret output. Dismissal, navigation, hidden/pagehide, disposal, session change and original deadlines clear references and DOM state; late responses cannot restore them. This is bounded reference lifetime, not guaranteed secure memory erasure. Shared field/error linkage, heading focus, immutable identity text, confirmation layout and existing slate/indigo themes are retained.

## Verification scope

**Clean results: 107 cases per provider, 55 UI tests and 83 selection controls.** A later diagnostic-only test assertion change was rebuilt and rechecked with three focused cases per provider; these are repeat checks, not additional unique coverage. All accepted reports have zero failures/skips. Both Administrator and AppHost test projects build with zero warnings/errors. The [final evidence manifest](editor-recovery-evidence.json) records exact results, source hashes and rejected attempts. Existing eleven session and nine list Node identities are preserved; all prior 26 editor identities remain in order with nine additions. Each critical editor module retains 95% lines/90% branches/100% functions. No package or coverage-floor changes are included.

Two new genuine native journeys per provider exercise protected BFF updates and actual issuer behavior. The callback edit journey rejects the old callback, original cached request and old code, then completes fresh password/MFA/consent at the new callback. The replacement journey rejects an old code using the new valid secret, rejects a fresh code using the old secret, then exchanges that same fresh code using the new secret. Successful output is cryptographically verified, audit-bound and denied actual management API access. Denials compare complete persisted native/human/policy/audit digests. Existing single-flow token/audit uniqueness checks remain default; multiple-flow checks bind the exact native request while retaining all application tokens in uniqueness assertions.

Retained browser source adds actual UI create/edit/conflict/replacement, keyboard-linked validation, one-time output erasure, safe storage and both-theme/reflow/accessibility checks on PostgreSQL/SQL Server. Independent contexts produce a real stale version without fighting intentional hidden-tab clearing. The same mutation journey is included in both native Aspire demo cases. These sources are compiled, **not accepted as executed browser evidence**. Native issuer HTTP tests do not replace browser consent journeys.

## Traceability

| Requirement/task | Permanent evidence |
| --- | --- |
| FR-001/007/010, T017 input and exact successor binding | `Given_edit_and_replacement_input_When_validated_Then_identity_is_immutable_and_confirmation_expiry_are_explicit`; `Given_update_response_When_revision_identity_or_action_fields_drift_Then_no_successor_is_accepted` |
| FR-007/012, T017 actual edited issuer configuration | `ManagedUpdateJourneyTests.Given_ManagedClientWithCachedRequestAndCode_When_CallbackIsEditedOverHttps_Then_OldConfigurationFailsAndNewIdentityOnlyJourneySucceeds`, both providers |
| FR-005/010/012, T017 credential recovery | `ManagedUpdateJourneyTests.Given_ManagedClientWithIssuedCode_When_SecretIsReplacedOverHttps_Then_OldCodeAndSecretFailIndependentlyAndNewCredentialsSucceed`, both providers; existing T015/T016 authority/audit evidence retained |
| FR-009/010, T018 lifetime and uncertainty | `Given_update_in_flight_When_original_deadline_or_owner_changes_Then_no_late_mutation_or_secret_survives`; `Given_replacement_expiry_and_crypto_wait_When_the_boundary_passes_Then_original_deadlines_still_control_output`; `Given_completed_replacement_When_secret_is_dismissed_then_page_hidden_Then_recovery_preserves_known_commit_and_recent_MFA_guidance` |
| FR-009/011, T017/T018 transport | `Given_generated_update_transport_When_dispatched_Then_opaque_target_strong_version_and_CSRF_are_exact` |
| FR-001–012, T019 retained acceptance | `ApplicationEditorBrowserTests.Given_AuthenticatedAdministrator_When_EditorCreationConflictAndReplacementRun_Then_RealMutationsAndBoundedAccessibleOutputAreObserved` and existing two-provider `LocalDemoRuntimeTests` — compiled only; execution and remaining browser consent/history obligations open |

The [editor identity inventory](../../eng/admin-ui/editor-test-identities.json) and [native metadata inventory](../../eng/administrator-test-matrix.json) contain complete exact test IDs. The [evidence manifest](editor-recovery-evidence.json) binds results and source, preserving all prior identities without weakening selection or coverage floors.

## Remaining acceptance and delivery

The previously recorded `cdn.playwright.dev`403 is not retried. Required Chromium/WebKit engines are unavailable in this workspace; Firefox remains excluded. T007/T014/T019 browser obligations remain open, including actual browser consent, lifecycle/history races, retained artifacts and full native demo execution. The user-authorized bypass permits implementation progress; it is not a browser pass or release waiver.

Two existing full-verification blockers remain: the unchanged expired feature009 BootstrapTests CQ008 exception, and GHSA-g3hv-75wg-m249 in the unchanged contract-generator dependency chain. No allowance is renewed, dependency override applied, advisory suppressed or gate weakened. All six historical findings and human/Windows/A-009/vendor/production/public-release gates remain open. Refresh stays disabled. No full CI, PR, merge or production action is included.

Next bounded work is T020/T021: reconcile the already generated contracts and exact inventories with the remaining contract, coverage-input and mutation obligations, then resolve the existing full-verification blockers before T022/T023 delivery. Do not repeat completed endpoint or issuer implementation.
