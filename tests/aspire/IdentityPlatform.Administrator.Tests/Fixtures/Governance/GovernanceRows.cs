namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Creates explicit provider traits for finite reviewed governance scenario rows.</summary>
internal static class GovernanceRows
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>Crosses the declared scenario identities with both production databases without an environment-dependent skip.</summary>
    internal static IEnumerable<TheoryDataRow<string, string>> For(params string[] scenarios) => Providers.SelectMany(provider =>
        scenarios.Select(scenario => new TheoryDataRow<string, string>(provider, scenario).WithTrait(ProviderTrait, provider)));
}
