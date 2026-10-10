# Feature Specification: Durable Issuer Protection

**Branch**: `002-issuer-foundation` | **Created**: 2026-10-10
**Authority**: Approved v0.2/A-001 and the user's instruction to continue after PR #1.

This is the next independently verifiable part of roadmap milestone 001: durable issuer credentials and session protection. The seeded login/code-flow requirements remain in [the issuer backlog](../../docs/engineering/issuer-flow-backlog.md); this feature does not enable a protocol server, publish discovery or claim full issuer acceptance. Its feature number does not mean roadmap milestone 002 (Admin BFF).

## User scenarios and testing

### US1 — Load externally provisioned credentials (P1)

An operator configures the canonical HTTPS issuer and separate signing, token-encryption and Data Protection wrapping credentials. The host either loads usable credentials or exits safely before listening.

1. Absent issuer configuration preserves the existing health-only foundation profile. Any partial issuer configuration is rejected; there is no permissive fallback.
2. Valid external password-protected PFX files with distinct RSA keys load; actual signatures and encryption round trips prove usability.
3. Unsafe issuer, missing/unreadable/invalid files, wrong passwords, public-only/weak/unsuitable keys, expired/future active certificates or a reused public key across purposes fail without exposing input or generating replacement issuer credentials.
4. Credentials remain alive for their consumer lifetime; partial-load failure disposes already loaded material.

Independent test: configuration and real-cryptography tests plus actual AuthServer process startup with otherwise valid database configuration.

### US2 — Preserve protected state across instances and rollover (P1)

An operator restarts the host or rotates its Data Protection wrapping certificate without losing still-valid protected state.

1. Separate service-provider instances sharing the durable ring and issuer can unprotect the same payload after complete disposal/reload.
2. A different purpose/issuer, tampered payload, missing original ring or missing historical decryptor cannot recover it.
3. Every stored master key is encrypted; malformed, plaintext or undecryptable existing keys fail startup rather than being silently replaced or ignored.
4. Changing the active wrapping certificate while retaining the historical decryptor permits both generations after restart. An expired historical decryptor is allowed; an expired active credential is not.
5. Startup forces real ring access and a protection self-test; mere lazy service registration does not count as readiness.

Independent test: actual framework protection, persisted XML inspection, separate provider lifetimes, explicit creation of a second key generation and negative reloads.

### US3 — Operate and verify the bounded host (P1)

An engineer runs the credential-enabled host in the established isolated Aspire matrix without accidentally enabling incomplete authentication routes.

1. Actual valid host startup succeeds; malformed credentials produce sanitized exit 2 before a listener starts.
2. Both PostgreSQL and SQL Server Aspire profiles execute the credential-enabled host alongside existing schema, isolation, failure and cleanup checks.
3. Discovery, JWKS, authorization, token and account routes remain absent. No controller, fake login, generated token or default secret is introduced.
4. A runbook explains private local storage, backup, restart-required updates, rollover/retention and explicit limitations.

Independent test: native xUnit v3/MTP host tests and both real-provider Aspire profiles, with strict report counts and unchanged per-module coverage floors.

## Requirements

| ID | Requirement | Trace |
| --- | --- | --- |
| KEY-001 | Validate a complete explicit issuer protection profile or preserve an entirely absent health-only profile; reject partial configuration without values in errors. | FR-15/18, NFR-01/08 |
| KEY-002 | Load externally provisioned password-protected RSA PFX credentials; require private keys, at least 3072 bits, appropriate key usage, valid active dates and public-key purpose separation. No generated replacement credentials. | FR-15/18, NFR-01/08 |
| KEY-003 | Use a private persisted issuer-specific, certificate-encrypted Data Protection ring; eagerly verify all stored keys and a real protection round trip. Reject plaintext/malformed/undecryptable ring contents. | FR-18, NFR-01/06/08 |
| KEY-004 | Preserve protection across independent instances and wrapping-certificate rollover, with explicit historical decryptors and purpose/issuer isolation. | FR-15/18, NFR-06/14 |
| KEY-005 | Own/dispose certificate lifetimes, reject unsafe filesystem access and report sanitized startup failure before listening. | FR-18, NFR-01/08 |
| KEY-006 | Keep protocol/login endpoints absent, preserve health semantics, verify both database compositions and document operator actions/remaining issuer work. | FR-09/17/18/22, NFR-11/14 |

## Success criteria

- SC-001: Every KEY requirement maps to passing tests and recorded evidence, with no skipped required case.
- SC-002: A real independently verified signature/encryption round trip and a protection payload survive their documented reload boundaries; every specified deny case fails.
- SC-003: Both real database profiles pass with the credential-enabled AuthServer; existing regression gates and module coverage floors remain enforced.
- SC-004: No private key, password, protected payload or plaintext master key appears in unrestricted output/evidence.

## Scope and assumptions

The first filesystem adapter supports Linux local private storage. Windows ACL enforcement, remote secret stores/HSMs, hot reload, automatic certificate rotation, token signing/encryption overlap selection and public JWKS staging are future adapters/features. Both database engines remain supported on Linux; no schema change is needed here. Development test material is synthetic and generated only by test fixtures, never by runtime startup. Data Protection may create new **persisted, certificate-encrypted master keys**, which is distinct from generating substitute issuer credentials.

No UI, protocol requests or application database writes change in this feature; browser tests and new migrations are therefore inapplicable. Full issuer/login/PKCE acceptance, themes, baseline API version tooling, administrator MFA/BFF and external certification remain explicitly unfinished. A-001 and the mandatory S256/public-release gates remain unchanged.
