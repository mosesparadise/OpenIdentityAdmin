# Tasks — visible administrator workspace

## Setup and foundations
- [x] T001 Record authorized first visible slice, research, architecture and security boundaries in specs/017-administrator-ui/ and docs/approval.md.
- [x] T002 Resolve independent architecture/security/test/UX planning findings in specs/017-administrator-ui/verification.md.
- [x] T003 Add locked React/TypeScript/Tailwind/Radix package, deterministic generated assets, runtime notices and required CI verification in src/frontend/IdentityPlatform.Admin.Ui/, scripts/ and .github/workflows/verify.yml. FR-004/006.

## US1 — Visible workspace
- [x] T004 [US1] Implement shared-token responsive Overview/Your session shell and accessible appearance/navigation in src/frontend/IdentityPlatform.Admin.Ui/Features/Shell/. FR-001/005.
- [x] T005 [US1] Implement exact embedded asset/document routes and local-demo composition in src/backend/IdentityPlatform.Admin.Bff/Features/AdminUi/; add host boundary tests in tests/backend/IdentityPlatform.Host.Tests/Features/AdminUi/. FR-001/004/006.

## US2 — Genuine sessions
- [x] T006 [US2] Add generated-contract read/strict ephemeral observation and session screens in src/frontend/IdentityPlatform.Admin.Ui/Features/Session/ with late/expiry/fault controls. FR-003/004.
- [x] T007 [US2] Add fixed opt-in workspace callback/return navigation in BFF Features/BrowserSessions/ without changing default composition or security semantics. FR-002.
- [x] T008 [US2] Extend existing real both-provider Aspire journey in tests/aspire/IdentityPlatform.AppHost.Tests/Features/LocalDemo/ and preserve all prior controls. FR-002/007.

## US3 — Failure, presentation and acceptance
- [x] T009 [US3] Add retained-engine browser state/theme/keyboard/accessibility/reflow/security controls in tests/aspire/IdentityPlatform.Administrator.Tests/Features/Browser/, with exact discovery metadata and selectors. FR-003/005/007.
- [x] T010 [US3] Inspect representative browser renders, resolve independent source/evidence findings and run required full CI; record actual results/limits in specs/017-administrator-ui/verification.md. FR-001–007.
- [x] T011 Update README.md, local quickstarts, docs/roadmap.md and docs/operations/new-task-handoff.md with the runnable user journey and accepted delivery; normal squash after exact-head checks. FR-006/007.

## Dependencies and independent work
T001/T002 precede affected implementation. T003 enables T004/T006; T005/T007 enable T008/T009; T010/T011 require all earlier work. Independent architecture/test/UX reviewers may operate in parallel; root owns edits. Every story retains a runnable user journey, not a test-only prototype. Subsequent protected application list and creation/editing remain explicit M02-D/M04 slices; refresh is not a prerequisite for this visible shell.
