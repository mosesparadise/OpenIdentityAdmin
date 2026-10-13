# Administrator Windows/WSL2 assessment handoff

Status: prepared; observations not run. The requesting user volunteered to perform this assessment on 13 October 2026. Assessor: repository owner/requesting user. Independent results reviewer: unassigned; review remains pending. AI source review does not replace human accessibility observations or qualified assurance.

## Start here

1. Select the reviewed main commit and record its full SHA, clean-tree status and UTC assessment dates. Use the [administrator operations runbook](administrator-accounts.md) to prepare a fresh disposable environment/account for each browser/provider cell (four sequential cells). Bootstrap and enrollment are once-only: the two browser cells cannot reuse one already-enrolled account for enrollment acceptance. Keep pilot disabled and use only synthetic credentials.
2. Establish trusted HTTPS in the Windows browsers and complete the documented protected Migrator bootstrap. A TLS/startup failure is blocked assessment; do not bypass trust or edit account rows.
3. Record exact Windows, WSL, Docker, SDK, database, browser, authenticator, password-manager and screen-reader versions in the [results template](administrator-manual-results-template.md). Select a disposable vault/profile; do not use a personal vault.
4. Confirm the matrix below before execution. Record omitted/unavailable cells as blocked or not_run with reasons. Capture actual safe observations for each case, including failed attempts and retest lineage.
5. Return the completed redacted results sheet and defect references. Never include passwords, TOTP/setup keys, recovery codes, cookies, access tokens, connection strings, DOM/HAR dumps or recordings containing them.

## Proposed assessment matrix

This is a concrete proposed matrix, not a claim that the assessor has agreed specific installed tools. Confirm the exact versions/tools in the results sheet before execution.

| Cell | Database | Windows browser | Password manager | Screen reader |
| --- | --- | --- | --- | --- |
| PG-EDGE | PostgreSQL | Edge | Isolated built-in vault | NVDA |
| PG-FF | PostgreSQL | Firefox | Assessor-selected independent disposable vault | NVDA |
| SQL-EDGE | SQL Server | Edge | Isolated built-in vault | NVDA |
| SQL-FF | SQL Server | Firefox | Same independent disposable vault product | NVDA |

Run ADM-MAN-01–12 from the [assessment procedure](administrator-manual-assessment.md) in each cell: 48 case/cell rows. For each relevant page, cover light/dark/system, keyboard use, desktop/narrow reflow, 200% text size, 400% zoom and Windows forced-colors. Preserve the procedure's MFA attempt/freshness budgets; prepare a fresh supported interaction rather than resetting database state. This matrix does not claim physical mobile or Safari/VoiceOver support.

## Coordinate the journeys

Perform ADM-MAN-02 enrollment autofill during ADM-MAN-01, before the account becomes enrolled. Record keyboard, screen-reader, paste and visual observations from ADM-MAN-03–05/10 alongside the applicable live form journeys. Where a variant requires repeating once-only enrollment or code delivery, provision a new disposable environment/account and record a separate sub-run reference. Never rewind account rows or reopen bootstrap. Regeneration and replacement legitimately change factors/codes/sessions; update the disposable vault/authenticator through the documented journey before later cases.

## Results to return

Use the existing attribution fields and expand one row per case/cell. Allowed outcomes are passed, failed, blocked and not_run. Include actual focus order/announcement/autofill/paste/error behavior in safe prose. Report missing tools and unclear instructions as gaps, not passes. The baseline below intentionally records no observed result.

| Cell | Case | Outcome | Actual safe observation | Defect / retest |
| --- | --- | --- | --- | --- |
| PG-EDGE | ADM-MAN-01 | not_run | None | None |
| PG-EDGE | ADM-MAN-02 | not_run | None | None |
| PG-EDGE | ADM-MAN-03 | not_run | None | None |
| PG-EDGE | ADM-MAN-04 | not_run | None | None |
| PG-EDGE | ADM-MAN-05 | not_run | None | None |
| PG-EDGE | ADM-MAN-06 | not_run | None | None |
| PG-EDGE | ADM-MAN-07 | not_run | None | None |
| PG-EDGE | ADM-MAN-08 | not_run | None | None |
| PG-EDGE | ADM-MAN-09 | not_run | None | None |
| PG-EDGE | ADM-MAN-10 | not_run | None | None |
| PG-EDGE | ADM-MAN-11 | not_run | None | None |
| PG-EDGE | ADM-MAN-12 | not_run | None | None |
| PG-FF | ADM-MAN-01 | not_run | None | None |
| PG-FF | ADM-MAN-02 | not_run | None | None |
| PG-FF | ADM-MAN-03 | not_run | None | None |
| PG-FF | ADM-MAN-04 | not_run | None | None |
| PG-FF | ADM-MAN-05 | not_run | None | None |
| PG-FF | ADM-MAN-06 | not_run | None | None |
| PG-FF | ADM-MAN-07 | not_run | None | None |
| PG-FF | ADM-MAN-08 | not_run | None | None |
| PG-FF | ADM-MAN-09 | not_run | None | None |
| PG-FF | ADM-MAN-10 | not_run | None | None |
| PG-FF | ADM-MAN-11 | not_run | None | None |
| PG-FF | ADM-MAN-12 | not_run | None | None |
| SQL-EDGE | ADM-MAN-01 | not_run | None | None |
| SQL-EDGE | ADM-MAN-02 | not_run | None | None |
| SQL-EDGE | ADM-MAN-03 | not_run | None | None |
| SQL-EDGE | ADM-MAN-04 | not_run | None | None |
| SQL-EDGE | ADM-MAN-05 | not_run | None | None |
| SQL-EDGE | ADM-MAN-06 | not_run | None | None |
| SQL-EDGE | ADM-MAN-07 | not_run | None | None |
| SQL-EDGE | ADM-MAN-08 | not_run | None | None |
| SQL-EDGE | ADM-MAN-09 | not_run | None | None |
| SQL-EDGE | ADM-MAN-10 | not_run | None | None |
| SQL-EDGE | ADM-MAN-11 | not_run | None | None |
| SQL-EDGE | ADM-MAN-12 | not_run | None | None |
| SQL-FF | ADM-MAN-01 | not_run | None | None |
| SQL-FF | ADM-MAN-02 | not_run | None | None |
| SQL-FF | ADM-MAN-03 | not_run | None | None |
| SQL-FF | ADM-MAN-04 | not_run | None | None |
| SQL-FF | ADM-MAN-05 | not_run | None | None |
| SQL-FF | ADM-MAN-06 | not_run | None | None |
| SQL-FF | ADM-MAN-07 | not_run | None | None |
| SQL-FF | ADM-MAN-08 | not_run | None | None |
| SQL-FF | ADM-MAN-09 | not_run | None | None |
| SQL-FF | ADM-MAN-10 | not_run | None | None |
| SQL-FF | ADM-MAN-11 | not_run | None | None |
| SQL-FF | ADM-MAN-12 | not_run | None | None |

## Separate pilot assessment

The non-administrator relying-party pilot still has its own [acceptance packet](pilot-manual-assessment.md) and requires an interactive HTTPS client and named human observations. This handoff covers administrator account pages only. Neither packet accepts BFF/API authentication, governance runtime, public release or certification.
