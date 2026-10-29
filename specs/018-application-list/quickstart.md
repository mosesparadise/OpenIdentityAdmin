# Run the protected Applications screen

Use current main ([PR #118](https://github.com/mosesparadise/OpenIdentityAdmin/pull/118) or later). Prerequisites are stable .NET 10, Docker Desktop using Linux containers, and a trusted development certificate. Follow the [native initialization and enrollment guide](../014-local-b3-demo/quickstart.md).

Set `IdentityPlatform.AppHost` as the startup project and choose `local-demo`, or run:

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

In the Aspire dashboard, run the initializer command, complete administrator enrollment, and open https://bff.dev.localhost:17444/. Sign in with a fresh password and MFA ceremony, then choose **Applications**. The list reads actual registrations, including `identity-admin-bff`. Search matches a case-sensitive literal client-ID prefix; select the page size and use Previous/Next. No custom PowerShell or separate frontend build is needed.

To select SQL Server, append `-- --Database:Provider=SqlServer`. Stopping Aspire discards the run. Sign-out or expired application access requires fresh sign-in; this slice does not refresh access. Creation and editing are the following slice. Automated provider/browser results are recorded in [verification](verification.md); the actual Windows walkthrough remains separate.
