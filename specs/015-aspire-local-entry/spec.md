# Feature Specification: Native Aspire local entry

**Branch**: `110-aspire-local-entry`
**Created**: 2026-10-27
**Status**: Internally delivered in [PR #107](delivery.md); integrated Linux verification complete; actual Windows walkthrough remains open

## User scenarios

### US1 — Launch from the normal development host (P1)

A Windows developer selects the local demo AppHost launch profile in their IDE or runs the standard .NET project command. No custom PowerShell or shell script is required. Docker Desktop runs the existing disposable Linux graph.

Acceptance: the named profile reaches the authenticated dashboard with the existing canonical HTTPS endpoints; ordinary health-only startup and publish denial remain intact.

### US2 — Initialize through the dashboard (P1)

After migration completes, the developer invokes Initialize administrator on the initializer resource. A dashboard dialog requests a synthetic username and masked password. Success directs them to genuine enrollment, then fresh sign-in and either logout choice.

Acceptance: cancellation before submission creates no account; invalid input creates no account; concurrent or repeated execution cannot repeat a mutation; failure after submission requires a new disposable run. Existing native bootstrap and registration contracts are preserved.

### US3 — Stop and restart normally (P2)

Stopping the AppHost disposes its run-owned containers and anonymous data through Aspire. New runs require fresh initialization/enrollment. No cleanup script or global prune is required.

## Requirements

- **FR-001**: Provide a normal named AppHost launch profile and standard project command; no custom PowerShell is required for startup, initialization or shutdown.
- **FR-002**: Collect initialization credentials through the authenticated dashboard with masked password input, server-side length/byte validation and cancellation; never persist them in configuration, resource metadata, command arguments, results or logs.
- **FR-003**: Preserve exact run/resource ownership and readiness, bounded UTF-8 redirected stdin, ordered native pending-account/registration/revision-one upgrade operations and atomic single-flight/terminal-attempt protection. No mutation retries.
- **FR-004**: Bound subprocess I/O, lifetime and cleanup; link command cancellation to host shutdown; interrupted or uncertain mutation stops only the owned initializer and remains terminal for that run.
- **FR-005**: Retain canonical TLS, existing Linux credential guards, both provider interfaces/histories, genuine enrollment/fresh MFA, accepted browser controls, native Aspire disposal and unchanged coverage/deadline gates.
- **FR-006**: Replace obsolete PowerShell helpers and document IDE/CLI/dashboard flows and platform evidence limits. Actual Windows execution remains a separately recorded obligation.

- **FR-007**: Accept installed stable .NET10.x SDKs and install the10.x channel in CI; update direct package/tool dependencies to current compatible stable releases with exact locks, documented compatibility constraints, preserved frozen evidence and reviewed vendor provenance.

## Success criteria

- **SC-001**: The complete documented local flow needs no custom PowerShell execution.
- **SC-002**: Both providers pass genuine container initialization, enrollment, login/logout and new-run invalidation through the new command path.
- **SC-003**: Negative/concurrent/canceled initialization controls deny unsafe execution and disclose no submitted secret.

## Assumptions and limits

Windows Docker Desktop Linux containers and a supported stable .NET 10.x SDK/trusted development certificate remain prerequisites. A named launch profile is a normal IDE choice; disabling the profile preserves health-only composition. No new authentication protocol, persistence schema or production setup wizard is introduced. Feature 014 Windows walkthrough, A-009 deferrals and all historical/human/release gates remain open.
