using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Factors;

/// <summary>Proves actual durable ring isolation, historical-wrapper continuity and fixed failure behavior.</summary>
[Collection("administrator-bootstrap")]
public sealed class ProtectionTests(BootstrapFixture fixture)
{
    private const int OtherGeneration = 2;
    private const long PublishedTimestamp6 = 20000000000;
    private const long PublishedTimestamp5 = 2000000000;
    private const long PublishedTimestamp4 = 1234567890;
    private const long PublishedTimestamp3 = 1111111111;
    private const long PublishedTimestamp2 = 1111111109;
    private const long PublishedTimestamp1 = 59;
    private const string PostgreSqlProvider = "PostgreSql";
    private const string SqlServerProvider = "SqlServer";
    private const string Owner = "owner";
    private const string InteractionId = "interaction";
    private const string GenerationChange = "generation";
    private const string KindChange = "kind";
    private const string IssuerChange = "issuer";
    private const string CorruptMaterial = "corrupt";
    private const string MissingKeyChange = "missing-key";
    private const string ProviderTrait = "Provider";
    private const string OtherIssuer = "https://other.example.test/";
    private const string OtherOwner = "other-owner";
    private const string OtherInteraction = "other-interaction";
    private const string PreviousWrapperPath = "Identity:Protection:DataProtection:Previous:0:Path";
    private const string PreviousWrapperPassword = "Identity:Protection:DataProtection:Previous:0:Password";

    private static readonly string[] Scenarios0 = new[] { PostgreSqlProvider, SqlServerProvider };
    private static readonly string[] Scenarios1 = new[] { Owner, GenerationChange, KindChange, InteractionId, IssuerChange, CorruptMaterial, MissingKeyChange };
    /// <summary>Enumerates every cryptographic context component independently from production purpose construction.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Substitutions =>
        from provider in Scenarios0
        from component in Scenarios1
        select new TheoryDataRow<string, string>(provider, component).WithTrait(ProviderTrait, provider);

    /// <summary>Rejects substitutions using real encrypted key files; failure does not replace existing factor material.</summary>
    [Theory, MemberData(nameof(Substitutions))]
    public async Task Given_ProtectedStagedSeed_When_ContextIsSubstituted_Then_UnprotectFailsWithoutDisclosure(string provider, string component)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        using var otherMaterial = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var otherServices = new FactorServices(provider, lease, otherMaterial);
        await using var scope = services.Services.CreateAsyncScope();
        await using var otherScope = otherServices.Services.CreateAsyncScope();
        var protection = scope.ServiceProvider.GetRequiredService<FactorProtection>();
        var value = protection.Protect(FactorScenario.Seed, Owner, 1, FactorProtection.Staged, InteractionId);
        Assert.True(protection.Unprotect(value, Owner, 1, FactorProtection.Staged, InteractionId) == FactorScenario.Seed);
        var selected = component == MissingKeyChange ? otherScope.ServiceProvider.GetRequiredService<FactorProtection>() : protection;
        if (component == IssuerChange)
        {
            selected = new FactorProtection(scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(),
                new FactorProtectionOptions(new Uri(OtherIssuer)));
        }
        var error = Assert.Throws<FactorUnavailableException>(() => selected.Unprotect(component == CorruptMaterial ? CorruptMaterial : value,
            component == Owner ? OtherOwner : Owner, component == GenerationChange ? OtherGeneration : 1,
            component == KindChange ? FactorProtection.Active : FactorProtection.Staged,
            component == KindChange ? null : component == InteractionId ? OtherInteraction : InteractionId));
        Assert.Equal("Administrator factor operation is unavailable.", error.Message);
        Assert.Null(error.InnerException);
        Assert.True(protection.Unprotect(value, Owner, 1, FactorProtection.Staged, InteractionId) == FactorScenario.Seed);
    }

    /// <summary>Preserves protected material through provider restart and wrapper rotation while historical decryptors remain configured.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DurableFactorMaterial_When_ProviderReopensWithRetainedWrapper_Then_MaterialRemainsReadable(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        string protectedSeed;
        await using (var first = new FactorServices(provider, lease, material))
        {
            await using var scope = first.Services.CreateAsyncScope();
            protectedSeed = scope.ServiceProvider.GetRequiredService<FactorProtection>().Protect(FactorScenario.Seed, Owner, 1, FactorProtection.Active);
        }
        material.RetainCurrentWrapper();
        await using var second = new FactorServices(provider, lease, material);
        await using var nextScope = second.Services.CreateAsyncScope();
        Assert.True(nextScope.ServiceProvider.GetRequiredService<FactorProtection>().Unprotect(protectedSeed, Owner, 1, FactorProtection.Active) == FactorScenario.Seed);
        material.Settings.Remove(PreviousWrapperPath);
        material.Settings.Remove(PreviousWrapperPassword);
        Assert.Throws<IdentityPlatform.AuthServer.Features.Issuer.Credentials.IssuerProtectionException>(() => new FactorServices(provider, lease, material));
    }

    /// <summary>Anchors the independent six-digit oracle to the published RFC 6238 SHA-1 vectors.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public void Given_PublishedTotpVectors_When_IndependentOracleRuns_Then_ExpectedDigitsMatch(string provider)
    {
        Assert.Contains(provider, Scenarios0);
        Assert.Equal("287082", FactorOracle.Code(PublishedTimestamp1));
        Assert.Equal("081804", FactorOracle.Code(PublishedTimestamp2));
        Assert.Equal("050471", FactorOracle.Code(PublishedTimestamp3));
        Assert.Equal("005924", FactorOracle.Code(PublishedTimestamp4));
        Assert.Equal("279037", FactorOracle.Code(PublishedTimestamp5));
        Assert.Equal("353130", FactorOracle.Code(PublishedTimestamp6));
    }
}
