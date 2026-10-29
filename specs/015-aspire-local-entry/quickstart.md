# Native Aspire walkthrough

Prerequisites: Docker Desktop using Linux containers, stable .NET 10.x SDK and trusted current .NET development HTTPS certificate covering *.dev.localhost. No PowerShell helper is needed.

Select IdentityPlatform.AppHost as startup project and its `local-demo` launch profile, or run `dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo`. In the authenticated dashboard wait for migrations and runtime health, then invoke Initialize administrator on initializer. Enter synthetic credentials; follow the genuine enrollment instructions in the [local quickstart](../014-local-b3-demo/quickstart.md), then open the [visible workspace](../017-administrator-ui/quickstart.md) at https://bff.dev.localhost:17444/ for sign-in, session details and logout. Stop the AppHost normally to discard this run. Windows execution remains unverified until recorded.
