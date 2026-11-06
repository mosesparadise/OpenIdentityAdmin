# Validate the application grant-revocation journey

The feature branch implements this journey; see [verification](verification.md) for actual
acceptance and the remaining T018 delivery gate. Ordinary Aspire automation uses Chromium
on both providers. Retained Chromium/WebKit recovery tests use the shipped UI and production
Worker composition in hosted fixtures; they are not four full-Aspire recovery runs. Actual
Windows execution and manual accessibility acceptance remain open. Use synthetic disposable
local-demo data and the existing runtime prerequisites.

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

The profile includes a run-owned `worker` container after migration completion. It uses
only Platform runtime access, exposes no host endpoint and requires no issuer protection
keys. Confirm that it is running in the dashboard before expecting job progress.

1. Use the ordinary Aspire dashboard initialization and real administrator password/MFA.
2. Create a managed application and complete a genuine managed sign-in. Retain a second
   application's fixture as the unaffected control. Disable the target in application details.
3. Confirm Revoke grants with fresh MFA and a finite reason. Observe acceptance/reference,
   then committed authorization/token counts. A concurrent re-enable must report conflict.
4. Sign out while a multi-batch fixture runs. Observe completion through a later genuinely
   authenticated session; the target remains disabled until explicitly re-enabled.
5. In automated fixtures, stop/restart the actual worker between committed batches. Verify
   native rows, audit and counts exactly; no manual SQL corrections are part of the demo.
6. Remove actor eligibility/permission before another batch. Observe stopped work, restore
   authority, confirm it remains stopped, then explicitly approve continuation with fresh MFA.
7. Run focused Administrator and Persistence scenarios against both real providers, retained
   Chromium/WebKit journeys, API/BFF contract and UI tests, failure/race/mutation coverage.
   Exact test IDs/commands and results are recorded in verification.md as implemented.

Existing browser download restrictions or Windows/manual/a11y gaps must be recorded as
unexecuted, not passes. Keep Firefox/A-009 and all existing external release gates open.
