namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

/// <summary>Names synthetic fixture inputs independently from production authority and protocol constants.</summary>
internal static class ContractValues
{
    internal const string CookieHeader = "Cookie";
    internal const string CookieName = "__Host-contract-fixture";
    internal const string CookieValue = "synthetic-session";
    internal const string CookiePair = CookieName + "=" + CookieValue;
    internal const string ProofHeader = "X-CSRF-TOKEN";
    internal const string Proof = "synthetic-proof";
    internal const string ExamplePath = "/examples";
    internal const string ExampleById = "/examples/{id}";
    internal const string ExampleId = "fixture";
    internal const string Version = "\"fixture-v1\"";
    internal const string IfMatchHeader = "If-Match";
    internal const int MaximumPage = 100;
    internal const int DefaultPage = 25;
    internal const string ApiDocument = "/openapi/v1.json";
    internal const string JsonMediaType = "application/json";
}
