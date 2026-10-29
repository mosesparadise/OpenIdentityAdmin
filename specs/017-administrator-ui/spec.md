# Feature Specification: Visible administrator workspace

**Feature Branch**: `119-admin-visible-shell`
**Created**: 2026-10-29
**Status**: Authorized first visible slice; implemented candidate; full hosted acceptance pending
**Input**: The user approved the recommendation to deliver a visible shell, then a protected application list, then application creation/editing. This feature delivers the first independently demonstrable slice; M02-D/M04 protect the subsequent management slices.

## User Scenarios & Testing

### US1 — Open the workspace (P1)
A developer runs the existing local-demo Aspire profile and opens the administration application. They see a usable responsive workspace with Overview and Your session navigation, local-environment identity, and an accessible appearance control.

Acceptance: root and session deep links load; default health-only startup remains unchanged; mobile, desktop, light, dark, system and keyboard navigation work. No fake management counts or nonworking management controls imply delivered features.

### US2 — Sign in and understand the session (P1)
An initialized/enrolled administrator follows Sign in, completes the existing fresh password/MFA ceremony, and returns to the workspace. The session page reports the currently observed local account identifier and expiry. Sign out opens the existing protected choices; a fixed link returns to the workspace afterward.

Acceptance: genuine both-provider journeys show signed-in then signed-out state; opening pages/rechecking status never renews a session; an expiry or failed status read removes previously shown identity. No OAuth credential reaches browser code or storage. Existing issuer-assisted logout semantics remain explicit.

### US3 — Recover from unavailable status (P2)
When status cannot be verified, the workspace explains that state without presenting the user as authenticated. A user can deliberately check again. Loading, anonymous, expired and unavailable states remain keyboard-accessible and distinguishable without color.

Acceptance: network/protocol/malformed/late responses cannot reinstate an expired result; retry is explicit and read-only; navigation remains usable with optional theme storage unavailable. JavaScript-disabled output gives a useful navigation fallback.

## Requirements
- **FR-001**: Provide a real workspace on the existing canonical local application origin, with Overview and Your session pages, responsive navigation and a clear local-development badge. Trace: PRD FR-19/24, M02-E.
- **FR-002**: Reuse existing genuine password/MFA and local/issuer-assisted logout; completion and return destinations are fixed application routes, never user-supplied redirect parameters. Trace: FR-07/11.
- **FR-003**: Read only the existing minimal local-session contract; show no permissions, management-data claim or fabricated display name. Handle loading, anonymous, authenticated, expired, unavailable and malformed responses without renewing the ticket. Trace: FR-11/NFR-08.
- **FR-004**: Keep credentials in the server, use same-origin cookies, retain CSP/encoding/cache/header boundaries and preserve exact protocol/API routes. Theme preference is the only persistent browser value. Trace: FR-11/NFR-01/08.
- **FR-005**: Reuse shared semantic slate/indigo light/dark tokens, support system preference, keyboard, visible focus, announcements, 320px reflow, reduced motion and safe first paint. Trace: FR-19/NFR-09/12.
- **FR-006**: Ordinary IDE/.NET Aspire startup needs no custom script or host frontend-build command. Lock, audit, license-review and deterministically verify shipped browser assets in CI. Trace: FR-22/24/NFR-14.
- **FR-007**: Preserve both databases, existing test identities, histories and all release gates. Require genuine local Aspire journeys plus retained Chromium/WebKit presentation/state evidence. Manual Windows/screen-reader acceptance and existing named deferrals remain visible. Trace: FR-17/21/25.

### Key Entities
Only ephemeral local-session observation and non-sensitive theme preference. No database entities or migrations.

## Success Criteria
- **SC-001**: A user starting local-demo can navigate the workspace, complete genuine enrollment/sign-in, view status, sign out and return without entering internal diagnostic URLs.
- **SC-002**: Both databases pass the complete existing disposable-run journey with the workspace integrated; every new state has meaningful positive/negative checks.
- **SC-003**: Retained browser engines render both themes and supported viewport sizes without horizontal overflow, keyboard traps or automated accessibility violations in the covered scope.
- **SC-004**: Rebuilding from exact frontend locks produces the shipped bytes; no host frontend build is required for the documented run.

## Assumptions and boundaries
The user selected Windows Docker Desktop, disposable runs and native Aspire. Existing enrollment and fresh password/MFA remain; no silent session refresh. Public static UI contains no account data until a protected same-origin status read. Management APIs/data/actions are separate next slices with full current-authorization requirements. No dummy application list is presented. Existing production/public-release/human gates remain open.
