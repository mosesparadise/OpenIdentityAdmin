# Verification: Durable issuer protection

Status: implementation in progress; no acceptance or issuer completion claimed.

## Design review and analysis

2026-10-10: Spec Kit cross-artifact analysis found all six requirements mapped to thirteen tasks and three independently testable stories; no unmapped tasks or constitutional conflicts. Seven documentation-quality checklist items passed. No extension hooks are configured.

Independent architecture/test and protocol-security reviewers support the bounded key/DP slice. Incorporated findings: compare public keys rather than certificate thumbprints; force lazy ring dependencies; verify every historical key rather than only a fresh round trip; suppress unsafe DP XML logging; distinguish expired historical decryptors from active credentials; do not activate an incomplete OpenIddict server or infer key-selection order. Full token/JWKS overlap and login/PKCE remain follow-on acceptance.

Initial test-first build fails on missing issuer feature namespaces/types, as expected; this is a missing-implementation compile failure, not a behavioral test pass. Runtime files were not changed before the specification/plan/tasks/analysis stage.

## Traceability (execution pending)

| Requirement | Planned evidence |
| --- | --- |
| KEY-001 | CredentialTests partial/absent/issuer cases; IssuerStartupTests |
| KEY-002 | CredentialTests active rejection, public-key reuse, real crypto |
| KEY-003 | ProtectionTests persisted XML, damaged/plaintext/history failures, startup warmup |
| KEY-004 | ProtectionTests independent reload, discriminator/purpose/tamper, rollover/history |
| KEY-005 | CredentialTests disposal; ProtectionTests paths/permissions/logs; actual startup |
| KEY-006 | Actual host route absence and both Aspire provider profiles, existing regression/coverage gates, runbook |

SQL Server image retrieval is blocked by this local environment's network policy; execute that required cell through the repository's normal GitHub CI. Do not substitute PostgreSQL or bypass the blocked destination.

## Local execution before PR

- Clean locked restore and solution build succeeded with zero warnings/errors. All 151 required unit-profile tests passed (Core 25, Host 116, Architecture 5, AppHost harness 5), none skipped. AuthServer preliminary child-process coverage: 96.56% lines / 83.84% branches. Final rebuilt-source/CI evidence is pending.
- All 19 Python gate regressions and AI instruction validation passed. Current advisory report covered all 16 projects with no known vulnerable packages; license inventory contains 209 package/version pairs with existing reviewed exceptions.
- Before-fix regression controls: a provider-specific logging override exposed a synthetic XML sentinel (1 expected failure); unencrypted private-key PFXs with/without a MAC were accepted (2 expected failures). Permanent corrected tests are part of Host's passing matrix.
- Targeted compiled mutations: disabling public-key separation caused all 3 reissued-key cases to fail; allowing plaintext native ring entries caused exactly the plaintext case to fail (1 failure among 18 damaged-ring rows). Sources were restored in a finally block, then rebuilt before final verification. These are two focused decision checks, not a comprehensive mutation score.
- A test-first missing-type compile failure, an XML-namespace interoperability failure, an OpenSSL exception-subtype assertion and a stale-build count mismatch were resolved; none is represented as clean acceptance. Strict expected counts rejected the stale binary.

## Review dispositions

Architecture/test review: rollover now asserts the new payload's actual key ID and wrapper-B certificate; invalid startup is checked after output drains; the expired-active-wrapper test removes the historical duplicate to isolate expiry. Credential lifetime remains explicitly owned by composition and disposed after consumers.

Security review: enforced logging suppression handles category/provider precedence and configuration reload; supported native descriptor/decryptor types and placement are validated before activation; every stored key including revoked entries is materialized; PFX confidentiality is inspected after bounded native import. Private-path comments/runbook state trusted ownership/ancestor prerequisites without claiming race-free filesystem security. Fresh service-provider tests, not a same-process cached reread, establish persistence. Final reviewer disposition and both-provider CI are pending.
