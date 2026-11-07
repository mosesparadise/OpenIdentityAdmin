using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Verifies independent native family boundaries and terminal-state preservation using real manager-created entries.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantSelectionTests(BootstrapFixture fixture)
{
    private const string Valid = "valid";
    private const string Inactive = "inactive";
    private const string Revoked = "revoked";
    private const string Rejected = "rejected";
    private const string Redeemed = "redeemed";
    private const string Subject = "synthetic-selection-subject";
    private const int BatchSize = 8;
    private const int EligibleTokens = 2;
    private const int CreationHoursBeforeNow = 2;
    private const string BoundBypass = "grant-batch-limit-bypass";
    private const string InactiveBypass = "grant-inactive-token-bypass";
    private static readonly string[] AuthorizationStates = [Valid, Revoked, Rejected];
    private static readonly string[] TokenStates = [Valid, Inactive, Revoked, Rejected, Redeemed];

    /// <summary>Empty, exact-boundary and asymmetric populations complete with independent counts and a separate zero-change reconciliation.</summary>
    [Theory]
    [InlineData("PostgreSql", 0, 0, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 0, 9, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 9, 0, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 8, 8, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 1, 9, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 9, 1, Traits = ["Provider", "PostgreSql", "CriticalDecision", "grant-batch-limit"])]
    [InlineData("SqlServer", 0, 0, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 0, 9, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 9, 0, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 8, 8, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 1, 9, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 9, 1, Traits = ["Provider", "SqlServer", "CriticalDecision", "grant-batch-limit"])]
    public async Task Given_IndependentNativeFamilies_When_Batched_Then_EachFamilyHasItsOwnBoundAndExactCompletion(string provider, int authorizations, int tokens)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, authorizations, tokens);
        var accepted = await test.AdmitAsync(test.Intent());
        var batches = (Math.Max(authorizations, tokens) + BatchSize - 1) / BatchSize;
        for (var batch = 1; batch <= batches; batch++)
        {
            Assert.True(await test.BatchAsync(accepted.Id));
            var progress = await test.ReadAsync(accepted.Id);
            Assert.True(progress.AuthorizationCount == Math.Min(batch * BatchSize, authorizations), BoundBypass);
            Assert.Equal(Math.Min(batch * BatchSize, tokens), progress.TokenCount);
            Assert.Equal(GrantJobState.Processing, progress.State);
        }
        Assert.True(await test.BatchAsync(accepted.Id));
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        Assert.Equal(GrantJobState.Succeeded, history.Job.State);
        Assert.Equal(batches + 2, history.Job.Revision);
        Assert.Equal(authorizations, history.Job.AuthorizationCount);
        Assert.Equal(tokens, history.Job.TokenCount);
        var native = await test.StatusesAsync(test.Id);
        Assert.Equal(authorizations, native.Authorizations.Length);
        Assert.Equal(tokens, native.Tokens.Length);
        Assert.All(native.Authorizations, status => Assert.Equal(Revoked, status));
        Assert.All(native.Tokens, status => Assert.Equal(Revoked, status));
        Assert.False(await test.BatchAsync(accepted.Id));
    }

    /// <summary>Expired valid/inactive authorization-less tokens remain revocable; redeemed/rejected/revoked records retain their exact state and do not inflate counts.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql", "CriticalDecision", "grant-inactive-token"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer", "CriticalDecision", "grant-inactive-token"])]
    public async Task Given_MixedNativeStates_When_RevocationCompletes_Then_OnlyEligibleTransitionsAreCounted(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var entries = await SeedStatesAsync(test);
        var accepted = await test.AdmitAsync(test.Intent());
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        Assert.True(history.Job.State == GrantJobState.Succeeded, InactiveBypass);
        Assert.Equal(1, history.Job.AuthorizationCount);
        Assert.True(history.Job.TokenCount == EligibleTokens, InactiveBypass);
        await using var scope = test.Fixture.Scope();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        foreach (var entry in entries)
        {
            var status = entry.Authorization
                ? await authorizations.GetStatusAsync((await authorizations.FindByIdAsync(entry.Id, test.Token))!, test.Token)
                : await tokens.GetStatusAsync((await tokens.FindByIdAsync(entry.Id, test.Token))!, test.Token);
            Assert.Equal(entry.Expected, status);
        }
    }

    /// <summary>Records exact native IDs and independent expected terminal statuses before production selection runs.</summary>
    private static async Task<List<(bool Authorization, string Id, string Expected)>> SeedStatesAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entries = new List<(bool Authorization, string Id, string Expected)>();
        foreach (var status in AuthorizationStates)
        {
            var row = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = test.Id,
                Subject = Subject,
                Status = status,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent
            }, test.Token);
            entries.Add((true, (await authorizations.GetIdAsync(row, test.Token))!, status == Valid ? Revoked : status));
        }
        foreach (var status in TokenStates)
        {
            var row = await tokens.CreateAsync(new OpenIddictTokenDescriptor
            {
                ApplicationId = test.Id,
                Status = status,
                Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken,
                CreationDate = test.Fixture.Clock.Now.AddHours(-CreationHoursBeforeNow).UtcDateTime,
                ExpirationDate = test.Fixture.Clock.Now.AddHours(-1).UtcDateTime
            }, test.Token);
            entries.Add((false, (await tokens.GetIdAsync(row, test.Token))!, status is Valid or Inactive ? Revoked : status));
        }
        return entries;
    }
}
