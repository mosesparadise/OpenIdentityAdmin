# Implementation Plan: Durable Issuer Protection

**Branch**: `002-issuer-foundation` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

## Summary

Load and validate externally provisioned purpose-separated issuer credentials in AuthServer, configure persisted certificate-encrypted ASP.NET Core Data Protection and force its lazy dependencies to prove usable before listening. Keep protocol routes absent. This is one bounded increment of roadmap milestone 001; the subsequent real login/S256 flow is preserved in the issuer backlog.

## Technical context

.NET SDK 10.0.401/runtime and ASP.NET Core 10.0.12; C#; existing xUnit v3/MTP 4.0.2 product package and MTP 2.5.1; Aspire 13.6.1. Reuse framework X509CertificateLoader, RSA, IDataProtectionProvider, IKeyManager and filesystem XML repository. Declare the already-transitive System.Security.Cryptography.Pkcs dependency directly at stable 10.0.12 for explicit PFX envelope inspection; no new assembly. OpenIddict 7.7.1 remains the future protocol owner; do not instantiate an incomplete server merely to expose keys.

Storage: externally provisioned password-protected PFX and private local Linux directory; no new database records or migrations. Test both existing PostgreSQL and SQL Server compositions. No request-performance claim: only startup configuration changes; no new request handler. Tests have existing linked cancellation/deadlines. Filesystem adapter excludes remote mounts whose I/O cannot be bounded by these APIs.

## Constitution check

All eighteen principles screened before and after design. Approved scope and traceability preserved; Features stay within AuthServer; no protocol reimplementation; no browser tokens, account changes or management API surface. Database semantics unchanged and both-provider regressions mandatory. Secrets/keys use owner-controlled storage and sanitized errors. Startup verification/runbook, pinned dependencies, unchanged coverage and independent architecture/test/security review are required. UI/API-version rules are inapplicable to this configuration-only increment, explicitly retained in later work. No constitutional exception.

## Structure

- `src/backend/IdentityPlatform.AuthServer/Features/Issuer/Credentials/`: settings parsing, certificate validation, material ownership.
- `src/backend/IdentityPlatform.AuthServer/Features/Issuer/DataProtection/`: private storage/ring validation, registration and eager startup verification.
- `src/backend/IdentityPlatform.AuthServer/Program.cs`: composition and sanitized exit 2.
- `tests/backend/IdentityPlatform.Host.Tests/Features/Issuer/`: configuration/crypto/persistence/rotation and actual process tests.
- `tests/shared/`: linked synthetic credential fixture used by Host and Aspire tests; no product reference to test utilities.
- `tests/aspire/IdentityPlatform.AppHost.Tests/Features/LocalProfilesTests.cs`: supply external synthetic material to AuthServer in both existing provider profiles.
- `eng/test-matrix.json`, runbook and feature evidence updated without weakening gates.

## Decisions

1. Presence of any `Identity:Issuer`/`Identity:Protection` configuration opts into complete validation. Entire absence keeps the foundation profile. There is no boolean typo that can silently disable validation.
2. Active signing/encryption/wrapping certificates require RSA >=3072, private key, current validity and explicit purpose-specific key usage. Compare SHA-256 of exported public-key identity across all purposes, including historical wrapping keys; certificate reissue cannot disguise key reuse.
3. Use `EphemeralKeySet` **when importing existing PFX** so private imports are not copied to an unmanaged OS store. Use an explicit composition-root owner, disposed after the host/provider. Native UnprotectKeysWithAnyCertificate captures supplied certificates during registration; its backing XmlKeyDecryptionOptions type is internal, so do not use reflection or build a second service provider. Runtime never generates issuer certificates.
4. Files and key-ring directory must be absolute local paths without symlinks. Linux directory mode 0700; credential files no group/other permissions, owner-readable. No automatic chmod or unsafe directory adoption. Windows/remote-store adapters remain absent.
5. Data Protection application name includes the canonical issuer; named protection purposes supply further isolation. Persist only certificate-encrypted keys. Validate XML structure without logging its contents, then force every key's encryptor and a real protect/unprotect probe before listener startup. Existing undecryptable state is a startup error.
6. Active wrapping certificate encrypts newly generated DP keys; historical wrappers only decrypt old keys and may be expired. Restart-required changes. Operator retains old decryptors until protected state and backups no longer need them. Rotation does not re-encrypt existing keys automatically.
7. Suppress framework Data Protection logs that could include sensitive XML/paths; report one safe startup error with no inner exception/value. No configuration/credential logging.
8. Test externally visible behavior: real crypto, persisted/reloaded framework state, purpose isolation, invalid startup, actual Aspire composition and absent routes. No claims about tokens/cookies that do not yet exist.

## Delivery and verification

Tests precede implementations where practical. Configuration/crypto tests → persisted DP tests → composition/Aspire regression → actual full CI/coverage and independent reviews. Keep strict MTP counts updated. Record exact commits, commands, provider/version scope, reviewer findings and unresolved platform limitations. Targeted mutation checks must demonstrate that purpose separation and plaintext-ring rejection are test-enforced.

No complexity exception or generic credential-provider/repository layer is justified for this single consumer.

## User-requested engineering follow-up

T014–T018 adapt the uploaded EditorConfig/gitignore, enable SDK 10 recommended and selected security/style/naming diagnostics with warnings-as-errors, and add a static-analysis CI job with real positive/negative controls. Formatting/braces/import changes preserve behavior; required analyzer corrections remain narrow and reviewed. Generated EF files alone are marked generated, test scenario naming has a scoped exception, and unused imports are checked by the formatter because Roslyn build enforcement requires XML documentation generation. No new analyzer dependency, schema, endpoint or UI is introduced. Publish a complete living roadmap alongside immutable approved documents; squash the verified PR and continue to first-party login/S256 using Spec Kit.
