namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Executes the independent migration witness controls without relying on database startup to reach their negative oracles.</summary>
public sealed class UpgradeWitnessTests
{
    /// <summary>Only the precise additive column/table contract can pass while every pre-existing row and catalog entry stays protected.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public void Given_ApprovedAdditiveUpgrade_When_ColumnProvenanceOrOldRowsAreForged_Then_IndependentWitnessRejectsEverySubstitution(string provider)
    {
        AdministratorUpgradeWitnessControls.Verify();
        IssuerContinuationUpgradeWitnessControls.Verify(provider);
    }
}
