using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Verifies the separate code/authorization and access/identity-token reservation deltas at their exact native row ceilings.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenCapacityTests(BootstrapFixture fixture)
{
    private const int TokenLimit = 20000;
    private const int AuthorizationLimit = 2000;
    private const int ExchangeDelta = 2;
    private const int CodeDelta = 1;

    /// <summary>Two genuine approved inputs exist before quota filling; only the exact last issuance reservation fits and overflow consumes no extra output capacity.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_LastManagedNativeCapacity_When_TwoApprovedInputsIssue_Then_ExactFinalDeltaFitsAndOverflowIsWithheld(string provider, bool exchange, bool authorizations)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var first = new ManagedTokenFlow(test);
        var second = new ManagedTokenFlow(test);
        await first.ApproveAsync();
        await second.ApproveAsync();
        var firstCode = exchange ? await first.CodeAsync() : null;
        var secondCode = exchange ? await second.CodeAsync() : null;
        var before = await test.SnapshotAsync();
        var delta = exchange ? ExchangeDelta : CodeDelta;
        await ManagedCacheHistoryTests.FillAsync(test, authorizations ? AuthorizationLimit - CodeDelta - before.Authorizations : TokenLimit - delta - before.Tokens.Length,
            authorizations ? null : OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        using var accepted = exchange ? await first.ExchangeAsync(firstCode!) : await test.SendAsync(first.Resume);
        Assert.Equal(exchange ? HttpStatusCode.OK : HttpStatusCode.Redirect, accepted.StatusCode);
        var full = await test.SnapshotAsync();
        Assert.Equal(authorizations ? AuthorizationLimit : TokenLimit, authorizations ? full.Authorizations : full.Tokens.Length);
        using var overflow = exchange ? await second.ExchangeAsync(secondCode!) : await test.SendAsync(second.Resume);
        await ManagedCacheTestContext.UnavailableAsync(overflow);
        var after = await test.SnapshotAsync();
        Assert.Equal(full.Tokens.Select(row => row.Id), after.Tokens.Select(row => row.Id));
        Assert.Equal(full.Authorizations, after.Authorizations);
    }
}
