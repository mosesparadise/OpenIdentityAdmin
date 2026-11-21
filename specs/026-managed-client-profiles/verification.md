# Managed client profile delivery evidence

## Managed web integration settings

PR141 delivered T001 at squash commit `d69b874a70b0c6fcad49a12584acd37acee2eaea`. CI632 tested source `150bc4c51683e20245e46d045b0e9e923cf911ec` and merge `ab4bec7edf3ac08f818d22cac857fbc52016ac2c`. The squash has the tested tree `521e1d7c83e5bdeb9ac74c8dffe7720ba0b68402`. All 149 jobs passed. The evidence review reconciled 143 original archives against their hosted hashes. [Hosted run](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44888506086)

The four editor journeys covered PostgreSQL and SQL Server with Chromium and WebKit. They checked keyboard expansion, the application’s exact client ID and current callback, exclusion of the issued secret, and clearing on page hide. Qualified manual accessibility and expanded-content theme assessment remain open.

The original reports contained 6,599 passing native cases, 149 UI cases and 47 contract tooling cases. All 17 modules met the existing coverage floors. The mutation review confirmed 161 causal kills with their baseline and restoration controls. These counts describe the tested source. They do not constitute a benchmark or release approval.

Both documentation viewers remain disabled. The approved internal advisory record retains the two open DOMPurify findings for the embedded Swagger bundle. Viewer activation requires verified patched assets and the remaining documentation activation checks. [Advisory assessment](../../eng/contracts/vendor/advisory-review-2026-11-21.json)

## Client profile storage

PR142 adds provider migrations and readiness checks for the expanded profile and
credential combinations. Existing confidential web records,
credential metadata and retirement history survive the upgrade. A downgrade
with a newer profile present fails atomically on PostgreSQL and SQL Server.

CI633 tested the stacked source `3450dc015a26e9eaf7b29deb8a826afd854c93c1`
at tree `dbc543359653ee51c7052d46680ee05606564dfb`. All 149 jobs passed.
The original persistence reports contain 42 passing cases per provider,
including seven affected cases on each. The managed request-cache expiry case
also passed on each provider. The independent reviews reconciled 143 original
archives, all 17 coverage floors and the 161 mutation controls. [Hosted CI633](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44888508741)

After PR141 landed, resolving the task-register conflict against main preserved
that tree. CI638 tested source `113eb4822e5f99c76f4ce2f4057e413197e521cc`
at merge `04e379ec226e480d1a0bd6cb991a77197da913c8`. All 149 jobs passed.
PR142 squash-merged as `7461cdd72307b68cb102cb5598d6e88c1571e224`, with the
same tree. [Hosted CI638](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44894745010)

The final run supplied 143 original archives. Reviewers verified 6,599 passing
native cases, all 84 persistence cases, 149 UI cases, 47 contract tooling cases,
all 17 module coverage floors and 161 causal mutation kills with passing baseline
and restoration controls. The rollback evidence includes a populated public native
row. The retirement history in the migration fixture is synthetic. The storage
increment does not activate additional client types in the issuer.
