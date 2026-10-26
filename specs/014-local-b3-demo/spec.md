# Feature Specification: Disposable local administrator demo

**Feature Branch**: `108-local-b3-demo`
**Created**: 2026-10-26
**Status**: Authorized development; implementation and acceptance pending
**Input**: User requested the next recommended item after local run assessment, selected Windows only with Docker Desktop, and disposable data per run.

## Clarifications

- Windows PowerShell and Docker Desktop running Linux containers; no WSL terminal.
- Accounts, databases and protection keys are disposable together. Individual container restart is not a persistence guarantee.
- Existing B3 fresh password plus MFA on every explicit sign-in, local and issuer-assisted logout remain mandatory.

## User Scenarios & Testing

### User Story 1 — Start and enroll locally (Priority: P1)

A developer starts an explicit Development profile from Windows, initializes a synthetic pending administrator through redirected stdin, and completes genuine enrollment over trusted HTTPS.

**Independent Test**: Run the documented Windows commands against each registered provider, observe migration completion and least-privilege runtime connections, initialize once and complete password/TOTP enrollment.

1. Given Docker Desktop Linux containers and a trusted matching development certificate, starting the profile exposes canonical issuer and BFF URLs.
2. Given an empty database, initialization creates only a pending account; enrollment requires its password and a real TOTP proof.
3. Given missing/invalid configuration, startup fails with a finite diagnostic before serving enabled endpoints.

### User Story 2 — Use accepted B3 flows (Priority: P1)

The developer follows BFF login, supplies fresh password/MFA, inspects the session and performs local or confirmed issuer logout.

**Independent Test**: Normal runtime hosts, genuine native protocol traffic and browser journeys on both providers, preserving accepted negative controls.

1. A valid callback establishes the accepted server-held session; invalid/replayed/unowned callbacks cannot dispatch native work outside its boundary.
2. Session/logout pages retain exact-origin, antiforgery, expiry, eligibility and source checks.

### User Story 3 — Dispose the run (Priority: P2)

Stopping/removing owned demo resources discards accounts, database data and private protection files. A new run requires initialization/enrollment again.

**Independent Test**: Inspect resource ownership and absence of persistent volumes; start a new run and reject old cookies.

### Edge Cases

Non-Development activation, publish mode, malformed booleans/URLs, unavailable ports, stale certificates missing required SANs, untrusted/wrong-name TLS, interrupted bootstrap, ambiguous Docker ownership, existing private files/symlinks, partial material writes and lost keys must fail visibly without silent repair or security fallback.

## Requirements

- **FR-001**: An explicit disabled-by-default Development-only local profile MUST start Linux containers from Windows Docker Desktop using the existing Aspire/provider contracts.
- **FR-002**: Both PostgreSQL and SQL Server MUST retain existing schemas, migration histories, role separation and provider registration interfaces.
- **FR-003**: Normal hosts MUST activate existing B3 services through an owned middleware adapter; defaults stay health-only.
- **FR-004**: Browser/backchannel MUST use identical canonical HTTPS authorities with valid certificates and ordinary trust validation.
- **FR-005**: Per-run protection material MUST retain Linux private-file/ring checks, distinct key roles and consumer lifetime ownership. Secret parameters mask presentation but Docker-authorized operators can inspect environment metadata; no secure-erasure claim.
- **FR-006**: Bootstrap MUST use the existing bounded redirected-stdin contract, genuine pending-account enrollment and separate public-only registration/upgrade operations. No fixed passwords, active fixture accounts or mutation retries.
- **FR-007**: Disposal MUST remove only resources owned by this run; no persistent volumes or deletion of unrelated Docker resources.
- **FR-008**: Documentation MUST provide Windows prerequisites, exact commands, readiness/failure guidance and clearly distinguish this demo from React management, refresh and production readiness.
- **NFR-001**: Existing security/coverage/deadline and both-provider gates MUST remain unchanged. A-009 defers only its recorded browser cases.
- **NFR-002**: No secret values, tokens or bootstrap documents may appear in logs, argv or evidence. Local Docker administrator access is trusted.

### Key Entities

Run identity, canonical issuer/BFF addresses, distinct ephemeral protection roles, pending administrator and fixed public registration. No new persisted domain entities or schema migrations.

## Success Criteria

- **SC-001**: Documented Windows setup reaches genuine enrollment, fresh sign-in, session status and both logout choices.
- **SC-002**: Required native-host and retained-browser cases pass on both providers with unchanged accepted controls.
- **SC-003**: Default/invalid/non-Development configurations cannot enable demo endpoints; unsafe storage/TLS/ownership controls deny.
- **SC-004**: A new disposable run rejects prior credentials/cookies until initialized and enrolled.

## Assumptions and limits

Docker Desktop Linux containers, the pinned .NET SDK, PowerShell and image/package access are prerequisites. Windows execution must be reported separately from Linux CI. No production activation, M02-C refresh, M02-D API authority, React management, historical finding or human/public-release gate is closed by this feature.
