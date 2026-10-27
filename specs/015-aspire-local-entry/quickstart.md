# Native Aspire walkthrough

Prerequisites: Docker Desktop using Linux containers, stable .NET 10.x SDK and trusted current .NET development HTTPS certificate covering *.dev.localhost. No PowerShell helper is needed.

Select IdentityPlatform.AppHost as startup project and its `local-demo` launch profile, or run `dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo`. In the authenticated dashboard wait for migrations and runtime health, then invoke Initialize administrator on initializer. Enter synthetic credentials; follow the existing genuine enrollment and BFF sign-in/logout URLs in feature014 quickstart. Stop the AppHost normally to discard this run. Windows execution remains unverified until recorded.
