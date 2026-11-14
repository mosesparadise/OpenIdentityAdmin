# Planned interactive sample walkthrough

Implementation/verification are in progress. These steps describe the intended result,
not a claim that the sample is delivered.

1. Follow the existing [local demo prerequisites](../014-local-b3-demo/quickstart.md)
   and run `dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo`.
2. Initialize/enroll the synthetic administrator from the dashboard, then open
   https://bff.dev.localhost:17444/. No custom PowerShell.
3. Create audience `orders-api`, API scope `orders.read` referencing it, and identity
   scope `sample.email` with the finite email disclosure policy. Create a confidential
   managed application with exact callback `https://sample.dev.localhost:17446/callback`.
   Immediately retain the one-time secret privately for this disposable run before leaving
   the creation page or switching tabs; the UI clears it on navigation. Then assign both
   scopes explicitly using current permission/fresh MFA. If you lost the output, use
   **Replace secret** and retain the replacement before leaving that page.
4. In Aspire's sample-client resource, run **Configure sample client** and enter that
   application's client ID and newly displayed one-time secret. Setup is private to this
   disposable run; input is not echoed. An uncertain/failed submission requires a fresh
   run, not repeated mutation. No registration is automatically created.
5. Open https://sample.dev.localhost:17446/, sign in with fresh password/MFA and choose
   Allow. Inspect the finite identity/scope summary, fetch current profile and call the
   synthetic orders API. Raw tokens are never displayed or held in browser storage.
6. Try Cancel from a fresh sign-in. Change the scope/application state in the Admin UI:
   UserInfo should deny changed current authority; an already-issued offline API token
   can remain valid until its expiry. The UI must explain the difference.
7. Sign out locally. The sample session stops working; issuer/other-app sessions and
   shared configuration are not erased. Test a second browser separately.
8. Stop Aspire and start fresh. State is disposable; repeat setup. Run the same journey
   with both existing provider selections. Windows/manual acceptance remains open until
   actually performed. No production or certification claim follows this demo.
