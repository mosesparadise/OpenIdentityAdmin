# Run the visible workspace

Use the feature branch until its PR is accepted; after squash use main. Prerequisites are stable .NET10, Docker Desktop Linux containers and a trusted development certificate. Set IdentityPlatform.AppHost as startup project and choose local-demo, or run `dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo` after normal tool/locked dependency restore. No custom script or frontend build command is needed.

Open https://bff.dev.localhost:17444/ for the workspace. Initialize administrator from the initializer dashboard command, then enroll at https://issuer.dev.localhost:17443/account/administrator/enrollment. Use Sign in in the workspace, complete fresh password/MFA and return to Overview. Open Your session, Check session and Sign out. Appearance supports system/light/dark. Stopping Aspire discards the run. Actual Windows walkthrough remains a user-owned acceptance step; automated Linux evidence is recorded separately.
