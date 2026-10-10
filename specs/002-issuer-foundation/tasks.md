# Tasks: Durable Issuer Protection

## Phase 1 — Setup

- [x] T001 Record bounded approved scope, full follow-on issuer backlog and architecture decisions in specs/002-issuer-foundation/spec.md, plan.md, research.md, data-model.md and contracts/configuration.md (KEY-001–006).
- [x] T002 Complete read-only cross-artifact analysis and architecture/test/security design review; record dispositions in specs/002-issuer-foundation/verification.md (KEY-001–006).

## Phase 2 — Shared test foundation

- [x] T003 Add synthetic external PFX/private-directory fixtures in tests/shared/IssuerMaterialFixture.cs, linked from Host and AppHost test projects; never log fixture secrets (KEY-002–005).

## Phase 3 — US1: externally provisioned credentials

Independent test: actual RSA operations and safe real-process startup failures.

- [x] T004 [US1] Add complete/partial profile, URI/file/password/RSA/usage/time/purpose-reuse and lifetime tests in tests/backend/IdentityPlatform.Host.Tests/Features/Issuer/CredentialTests.cs (KEY-001/002/005).
- [x] T005 [US1] Implement parsing, safe errors and owned loading in src/backend/IdentityPlatform.AuthServer/Features/Issuer/Credentials/: active RSA private keys >=3072 bits, valid now, explicit DigitalSignature or KeyEncipherment; at most four previous wrapping keys; compare public-key identity (KEY-001/002/005).

## Phase 4 — US2: persisted protection and rollover

Independent test: real protect/unprotect after full provider disposal, XML inspection and two certificate/key generations.

- [x] T006 [US2] Add persistence, isolation/tamper, malformed/plaintext/undecryptable ring, permissions and rollover tests in tests/backend/IdentityPlatform.Host.Tests/Features/Issuer/ProtectionTests.cs (KEY-003/004/005).
- [x] T007 [US2] Implement private local Linux storage validation and framework DP registration/eager verification in src/backend/IdentityPlatform.AuthServer/Features/Issuer/DataProtection/; require existing absolute non-symlink directory mode 0700; reject plaintext key XML and suppress unsafe framework diagnostics (KEY-003/004/005).

## Phase 5 — US3: bounded runtime operation

Independent test: actual AuthServer process and both credential-enabled real-provider Aspire profiles.

- [x] T008 [US3] Add real-process valid/invalid startup and absent-route tests in tests/backend/IdentityPlatform.Host.Tests/Features/Issuer/IssuerStartupTests.cs (KEY-001/005/006).
- [x] T009 [US3] Wire optional complete profile and safe exit 2/eager verification in src/backend/IdentityPlatform.AuthServer/Program.cs (KEY-001/003/005/006).
- [x] T010 [US3] Supply synthetic external credentials to the existing isolated AuthServer resources in tests/aspire/IdentityPlatform.AppHost.Tests/Features/LocalProfilesTests.cs; verify absent protocol/login routes on both providers (KEY-006).
- [x] T011 [US3] Document provision/backup/retention/restart and rollover in docs/operations/issuer-protection.md; update README.md and eng/test-matrix.json (KEY-001–006).

## Phase 6 — Verification and convergence

- [x] T012 Execute unit and both real-provider profiles, strict MTP evidence, child-process per-module coverage and supply-chain gates; perform targeted purpose-separation/plaintext-ring mutations and record results in specs/002-issuer-foundation/verification.md (KEY-001–006).
- [x] T013 Obtain independent architecture/test/security implementation reviews, resolve findings, reconcile all task/requirement evidence and create the next reviewable PR; preserve issuer backlog and A-001 limits in specs/002-issuer-foundation/verification.md (KEY-001–006).

## Dependencies and execution strategy

T001–002 precede code; T003 precedes story tests. US1 supports US2; both support US3. Write tests before implementation where possible, recording missing-symbol compilation separately from behavioral failure. T010's two provider executions may run in separate CI jobs; reviewers can independently inspect the same stable diff. Main owns edits/integration. T012 requires all implementation; T013 requires actual evidence, not only test registration.

No further user approval is needed for this routine approved work. No production deployment, certification claim or login/protocol acceptance is part of this feature.
