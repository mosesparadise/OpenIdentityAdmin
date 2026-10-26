# Dedicated B3 security mutation controls

## Candidate scope — 26 October 2026

T021 adds eight exact-source security mutants per provider through the existing isolated mutation verifier. PostgreSQL and SQL Server must each pass the same-source baseline, fail each seeded defect at its specific demonstrated-bypass marker, and pass the restored-source controls. Candidate source and compilation alone do not establish a mutation kill. T021 remains unchecked pending exact-head hosted evidence and independent review.

The existing issuer, administrator, governance and B2 protocol manifests/oracles remain unchanged: their 48 required kills and same-source controls are preserved. The new suite adds 16 required kills, for a proposed total of 64. No exception, timeout, unrelated assertion, empty selection, changed identity, skip, missing report or generic process failure can count as a kill.

## Exact decision witnesses

| ID / oracle class | Seeded defect | Required unsafe observation before marker | Mapping |
| --- | --- | --- | --- |
| `browser-intent-binding` / `BrowserIdentityMutationTests` | Omit expected signed login-intent equality | Genuine native callback authenticates an independently re-signed wrong intent after real code exchange/state consumption. Signature, nonce, audience and other claims stay unchanged; replay still denies. | FR-001–004/013; B3-AUTH-03 |
| `browser-source-deadline` / `BrowserIdentityMutationTests` | Omit original source-expiry `end > now` | Genuine native callback authenticates signed source-expiry equality while ordinary token expiry is still future and authentication time is earlier. This is a B3 native identity-boundary observation, not B1 session resurrection. | FR-003/004/013; B3-AUTH-03 |
| `browser-generation-binding` / `BrowserBindingMutationTests` | Omit persisted/protected generation equality | An authentic older proof authenticates with a genuinely current replacement ticket. The response must carry the exact current subject and minimum original proof/browser/ticket deadline, with no writes or cookies; restored current proof remains usable. | FR-005–008; B3-STATE-01/02, B3-WEB-01 |
| `browser-antiforgery-binding` / `BrowserBindingMutationTests` | Bypass native antiforgery additional-data equality | Original token plus actual newer browser cookie commits a second intent, generation and Start audit. A merely unexpected status is insufficient. | FR-001/007/008; B3-WEB-02 |
| `browser-local-revocation` / `BrowserLogoutMutationTests` | Omit exact ticket revocation | Real authenticated logout reports success while the exact original ticket remains unrevoked at its original revision, with no Revoke audit despite committed actor-bound Logout audit. Changed browser generation cannot mask it. | FR-008/009/011; B3-END-01 |
| `browser-issuer-source` / `IssuerLogoutMutationTests` | Omit cookie-source versus native hint equality | A second genuine issuer source obtains and submits its own confirmation for a different requested hint; actual wrong-source revocation and one audit must occur while the intended source remains live. | FR-010/011/013; B3-END-02 |
| `browser-return-once` / `BrowserLogoutMutationTests` | Omit required correlation phase | Replaying genuine original return cookies commits a second Return audit and revision while preserving original correlation deadlines. Issuer stays live; no remote attestation is inferred. | FR-009/010; B3-END-03 |
| `browser-logout-publication` / `BrowserLogoutMutationTests` | Omit original incoming proof publication bound | After a real remote commit and joined cleanup, an authentic pre-callback proof reaches expiry equality but a later live tombstone permits a positive cookie and issuer link. Both effects and commit/cleanup witnesses precede the marker. | FR-008–010/012; B3-END-01/03 |

Each fixed `CriticalDecision` selects one case per provider. Positive native controls use fresh real password/MFA and independently owned native roots; no fabricated principal, callback state, protocol response or pre-created authority replaces the genuine path. The response-rewrite seam intervenes only after actual issuance, independently verifies signatures, and permits exactly one changed field. Existing short-source-expiry behavior is preserved as a separate enum case.

## Tooling and execution boundaries

`eng/browser-session-mutations.json` permits eight unique substitutions in six exact production files. `scripts/verify_mutations.py` adds an explicit `browser-session` suite; it does not broaden existing suites or their source boundaries. The new allowlist regression uses otherwise-valid forbidden-file anchors and requires the boundary-specific rejection, preventing unrelated source-drift errors from passing the control.

The two new required `Browser session mutations` jobs use the existing native image preparation, trusted HTTPS, locked dependencies, 30-minute job limit, serial builds and unchanged verifier deadlines. Final acceptance includes this suite for full changes and accounts for its explicit skip on eligible docs-only changes. Per-mutant compilation, native discovery, exact marker/method/report identity, source hashes and final restoration remain mandatory. Only sanitized mutation receipts are uploaded.

Ordinary native discovery adds 16 cases while preserving all prior 1,877 portable records, for 1,893. BrowserSessions server groups become Foundations 160, Product 98 and Lifecycle 55 per provider (313 total); ordinary Administrator counts become PostgreSQL 868 and SQL Server 973. Browser stories remain Accounts 8, Security 5 and BrowserSessions 13. Existing browser/coverage partition topology and 60 coverage inputs remain unchanged. Expected full raw executions are 4,458; actual reports must establish that total. The proposed full workflow has 79 jobs; no evidence is inferred from inventory counts.

## Review and verification disposition

Independent architecture review passes the scoped design. Security/test review required two corrections: compare stale-generation status against the old proof's effective capped expiry, and ensure the forbidden-file regression fails specifically at the allowlist boundary. Both are implemented. Manifest anchors are unique; existing manifests/oracles retain their bytes. Final architecture, security and test source reviews pass. Actual hosted baseline/mutant/restored receipts remain required.

Initial local native build/discovery confirms all 16 additions. All 227 Python controls pass after updating independent partition/job expectations. Local isolated compilation passes the baseline, all eight substitutions and restored source with zero warnings/errors. Every touched production file is byte-for-byte restored and hashed. This is compile-only verification, with no database execution or kill claim. Actual native discovery passes all 16 single-case CriticalDecision filters, all six server-group filters and six unchanged browser-story filters. Formatting and the syntax-aware quality gate pass without new allowances. Prior formatting/build and independent-review defects remain documented here rather than being counted as passing evidence.

No production behavior, composition or authorization is activated. T022 handoff, remaining B3 integration/browser acceptance and T023/T024 coherent delivery stay separate. A-009 deferrals, six historical findings, 48 human rows, 345 not-assessed controls and all production/public-release gates remain open.
