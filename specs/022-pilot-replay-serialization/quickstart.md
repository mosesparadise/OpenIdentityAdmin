# Pilot replay correction validation

The T002–T004 foundation is implemented on branch130, with SQL Server verification
blocked by local container startup. T005 generation fencing is not implemented, so
the initial replay defect remains open and this branch is not ready to merge. The existing administrator UI and grant
revocation are already delivered through PR124; use the [native Aspire grant
walkthrough](../021-application-grant-revocation/quickstart.md) for local testing now.

Implementation verification starts from the clean feature branch with the repository
.NET10 SDK policy, locked dependencies, trusted development HTTPS and Docker ready.
Use the existing test-matrix runner and native discovery tools; determine exact new
case selectors from discovery rather than inventing names/counts in advance.

1. Retain the complete new regression source and hashes, run against the uncorrected
   baseline and preserve the actual failure. Do not use another replay to clean up
   the winning access entry before asserting the initial-replay consequence.
2. Apply the new migrations through the normal Migrator to disposable PostgreSQL and
   SQL Server databases. Verify fresh and upgrade histories/data. No request repairs
   missing coordination state or enables the pilot.
3. Run C01–C12 from [the acceptance matrix](contracts/exchange.md), including real
   two-process contention, fault paths and both standalone/mixed profile routing.
4. Run unchanged regression/mutation/coverage and browser/Aspire gates on the exact
   integrated head. Reconcile actual native identities and original artifacts.
5. Review the evidence and publish a precise correction status. Unexecuted cells stay
   open; a green planning PR does not establish runtime acceptance.

For a future corrected local topology, drain/stop all old pilot issuers, apply normal
Migrator upgrades, then start only corrected issuer binaries. Mixed old/new pilot
processes do not share this guarantee. Preserve existing native state and histories;
normal disposable Aspire behavior stays as selected by the user. This guide does not
authorize production migration or imply immediate offline token invalidation.
