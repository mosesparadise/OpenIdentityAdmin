# Verification: Durable issuer protection

Status: bounded implementation and independent reviews complete; initial implementation CI passed. The fixture-only optimization and evidence follow-up require the latest PR-head checks before merge. This is not full issuer, milestone-001 or product-release acceptance.

## Scope and provenance

Spec Kit 1.1.3 specification, plan, task generation and read-only analysis preceded runtime changes. All six KEY requirements map to thirteen tasks and three stories; seven requirements-checklist items passed. No extension hooks are configured. The full login/code-flow requirements remain in [the issuer backlog](../../docs/engineering/issuer-flow-backlog.md).

[CI run 38048127515](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38048127515) passed all five jobs for implementation commit `d3d84785eb7dab9d4db7557528f2ca28beef4289`. [The evidence manifest](evidence-manifest.json) records artifact IDs and SHA-256 digests; GitHub retention is seven days. [Draft PR #2](https://github.com/mosesparadise/OpenIdentityAdmin/pull/2) carries subsequent head-check status. Do not infer final-head verification from the baseline run.

Pinned scope: Linux, SDK 10.0.401/runtime 10.0.12, xUnit v3 package `xunit.v3.mtp-v2` 4.0.2, native MTP 2.5.1, Aspire 13.6.1, and the repository's digest-pinned PostgreSQL/SQL Server containers. No new schema, request handler or UI is introduced.

Executed locally: locked restore; solution build with zero warnings/errors; unit profile; PostgreSQL profile with child-process coverage; all gate regressions; AI instruction validation; current advisory/license checks; Gitleaks snapshot scan; two compiled decision mutations. SQL Server image retrieval is blocked by local network policy, so its required cell ran in normal GitHub CI without substitution or bypass.

CI executed `dotnet tool restore`, `dotnet restore --locked-mode`, `dotnet build --no-restore --nologo`, all four Python instruction/gate checks, and each of `python3 scripts/verify.py unit`, `PostgreSql`, `SqlServer` under `dotnet coverage collect -s eng/coverage.xml`. The coverage job merged all three reports and ran `python3 scripts/check-coverage.py`. Exact commands, supply-chain checks and the pinned Gitleaks checksum are in [the workflow](../../.github/workflows/verify.yml).

## Executed matrix

| Profile / boundary | Required passed | Result |
| --- | ---: | --- |
| Unit: Core / Host / Architecture / AppHost harness | 25 / 116 / 5 / 5 | 151 passed |
| PostgreSQL: native persistence / actual Aspire composition | 3 / 4 | 7 passed |
| SQL Server: native persistence / actual Aspire composition | 3 / 4 | 7 passed |
| Python report / coverage / supply-chain gate regressions | 8 / 3 / 8 | 19 passed |

All **165 .NET cases** passed, with no failed, skipped or unexecuted required cases. Strict MTP evidence also rejects missing, stale, zero-count and unexpected-count reports. Aspire exercises credential-enabled AuthServer startup together with existing schema, least-privilege isolation, health/outage and cleanup behavior on both providers. Protocol/account-route absence is checked against real hosts.

Host includes six actual-process issuer cases, eighteen damaged-ring cases, both plaintext-private-key PFX formats, and independent reload/rollover/diagnostic-isolation checks. Process tests use otherwise valid database configuration so an issuer failure cannot be hidden behind a database-configuration error.

The initial CI Host process took about 152 seconds of its unchanged 180-second deadline. A test-only follow-up caches encrypted synthetic baseline PFX bytes; each case still gets separate private files, settings, ring and imported objects. Negative/rotation replacements still generate their own keys. Independent review found no isolation or false-positive issue. A fresh local build and all 151 unit cases passed after this edit (Host TRX interval about 7.2 seconds); cross-machine/coverage timings are not a product-performance benchmark. Final CI rechecks this follow-up.

## Requirement and acceptance evidence

Tests below are in `tests/backend/IdentityPlatform.Host.Tests/Features/Issuer/`; Aspire evidence is in `tests/aspire/IdentityPlatform.AppHost.Tests/Features/LocalProfilesTests.cs`.

| Requirement / scenario | Passing evidence |
| --- | --- |
| KEY-001; US1/AC1 | `Entirely_absent_profile_keeps_health_only_composition`, eight `Partial_profile_is_rejected` rows and actual partial-profile process exit |
| KEY-002; US1/AC2–3 | `Loaded_external_keys_perform_independently_verified_crypto_and_are_owned_until_provider_disposal`; unsafe URI and eleven unusable-material cases; MAC-only/unprotected private bags; ambiguous multi-key PFX; four/five historical-wrapper boundary; three reissued-key purpose-reuse cases |
| KEY-005; US1/AC4 | Owned material remains usable until host/provider disposal; disposed certificates cannot supply private-key operations; partial-load cleanup explicitly owned in `IssuerCredentials.Load` and reviewed |
| KEY-003; US2/AC3,5 | Persisted XML inspection; eighteen `Damaged_or_unprotected_ring_fails_without_logging_xml_or_generating_replacement` rows; every historical/revoked key materialized; active-wrapper probe; actual startup failures; 256-document bound |
| KEY-004; US2/AC1–2 | `Persisted_payload_survives_disposal_and_independent_instance_with_only_encrypted_master_keys`: full provider reload, issuer/purpose isolation, tampering and lost-ring denial |
| KEY-004; US2/AC4 | `New_wrapper_and_new_master_key_preserve_both_generations_after_restart`: second payload's actual key ID and persisted wrapper-B certificate, both generations reload, missing old wrapper denied; `Expired_historical_wrapper_can_decrypt_but_cannot_be_active`; native revocation survives reload |
| KEY-005; US3/AC1 | Eleven filesystem rejection cases, category/provider logging override and reload with unrelated-log positive control; six actual-host cases with drained output and safe exit 2 |
| KEY-006; US3/AC2–4 | Both real-provider Aspire profiles, absent protocol/account routes, existing health semantics and [operator runbook](../../docs/operations/issuer-protection.md) |

SC-001 is supported by complete traceability/matrix evidence; SC-002 by actual crypto, reload and deny cases; SC-003 by both provider jobs and module gates; SC-004 by output assertions, suppression controls and secret scans. These are bounded engineering evidence, not universal confidentiality or external certification claims.

Browser/theme/accessibility, application API contracts, token/cookie behavior and new migration tests are inapplicable to this configuration-only increment. Their approved requirements remain in future features. There is no request-performance claim.

## Per-module coverage and supply chain

Merged CI coverage includes actual child hosts. Floors remain **90% lines / 80% branches per module**. No new exclusion was added.

| Handwritten module | Lines | Branches |
| --- | ---: | ---: |
| Core | 100.0% | 100.0% |
| Infrastructure | 98.6% | 100.0% |
| ServiceDefaults | 100.0% | 100.0% |
| Migrator | 98.6% | 97.8% |
| AuthServer | 97.2% | 94.4% |
| Admin.Api | 100.0% | 100.0% |
| Admin.Bff | 100.0% | 100.0% |
| Worker | 100.0% | 100.0% |
| AppHost | 100.0% | 100.0% |

All sixteen projects were represented in current NuGet advisory evidence, with no known vulnerable packages reported. The locked license inventory contains 209 package/version pairs and retains existing reviewed exceptions. Gitleaks 8.30.1 found no leaks in the source snapshot. These scans do not establish general compliance.

## Defect controls and mutations

- Before the logging fix, a provider-specific override exposed a synthetic XML sentinel: one expected failure. Before PFX inspection, unencrypted private-key bags with/without a MAC were accepted: two expected failures. Permanent corrected cases pass in Host's matrix.
- Removing public-key purpose separation from compiled product code caused all three reissued-key cases to fail.
- Allowing plaintext native ring entries caused exactly the plaintext case to fail among eighteen damaged-ring rows.
- Mutation sources were restored in a finally block, then rebuilt before final unit verification. These are two focused decision checks, not a comprehensive mutation score.
- A test-first missing-type compilation failure, XML-namespace interoperability failure, OpenSSL exception-subtype assertion and stale-build count mismatch were resolved. None is represented as clean acceptance; strict expected counts rejected the stale binary.

Only synthetic material was used. Raw failure XML, credentials and protected payloads are intentionally excluded from committed evidence.

## Independent review dispositions

Architecture/test review **passed** with no unresolved findings. Incorporated findings: compare SPKI rather than certificate thumbprints; force lazy ring dependencies; verify historical state; assert the rollover payload's actual new key and wrapper certificate; drain invalid-process output before checking listener absence; remove the retained duplicate when testing active-wrapper expiry. Composition owns certificates until all consumers are disposed. The subsequent fixture-only optimization also passed read-only review.

Security review **passed** with no concrete blocker. Incorporated findings: enforce DP logging suppression against provider/category precedence and reload; validate supported native descriptor/decryptor types and shape before activation; verify even revoked keys; inspect actual PFX private-bag confidentiality after bounded native import. Operator-trusted ownership/ancestor requirements are explicit. Fresh provider lifetimes, not a cached reread, establish reload behavior.

Reviewers inspected source and available test/defect evidence independently; they did not claim to execute the full CI matrix or perform a formal external assessment. The main agent executed checks and resolved findings.

## Spec Kit convergence

After implementation and reviews, the present source/tests/evidence were assessed against all 13 tasks, 6 KEY requirements, 4 success criteria, 13 story acceptance scenarios, 8 plan decisions and 18 constitution principles. Result: **converged**, with zero missing, partial, contradictory or unrequested-scope findings and zero findings at any severity. The convergence pass left tasks.md byte-for-byte unchanged (SHA-256 `52e0c423a3d6b43fe053e1a97bdc0ad08947e8d9f119e350966b49476b6180e6`); task completion markers had already been reconciled during implementation. No empty convergence phase was appended. Final PR-head CI remains the merge check for the fixture/evidence follow-up.

## Limits and follow-on work

The first adapter requires private local Linux storage and trusted owners/ancestors. It does not claim race-free filesystem security, Windows ACL support, HSM/remote secret integration, hot reload or automatic certificate rotation. Existing DP keys are not rewrapped automatically; retain historical decryptors for all required stored keys/backups, including revoked keys.

No discovery/JWKS, login, token issuance, browser session, Admin BFF flow or public key staging is implemented here. [The follow-on backlog](../../docs/engineering/issuer-flow-backlog.md) preserves their acceptance requirements. A-001's mandatory S256 and external certification/public-release gates remain unchanged.
