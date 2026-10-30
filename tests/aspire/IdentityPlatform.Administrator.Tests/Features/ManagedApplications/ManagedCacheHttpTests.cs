using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises actual managed native caching through protected registration and real HTTPS on each supported provider.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheHttpTests(BootstrapFixture fixture)
{
    /// <summary>Initial cache commits exactly one bound anonymous request and never grants administrator or managed human authentication.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealManagedRegistration_When_NativeCacheAndResumeRun_Then_OnlyBoundAnonymousRequestCommits(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var before = await test.SnapshotAsync();
        var initial = ManagedCacheTestContext.Initial();
        using var response = await test.SendAsync(initial);
        var resume = await test.ResumeAsync(response);
        var cached = await test.SnapshotAsync();
        var row = Assert.Single(cached.Interactions);
        var token = Assert.Single(cached.Tokens);
        Assert.Equal(test.Id, row.ApplicationId);
        Assert.Equal(token.Id, row.RequestId);
        Assert.Equal(ManagedLoginState.PendingPassword, row.State);
        Assert.Equal(300, row.ExpiresAt - row.CreatedAt);
        Assert.Equal(token.ExpirationDate, DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt).UtcDateTime);
        Assert.Null(row.UserId);
        Assert.Null(row.CeremonyId);
        Assert.Null(row.MfaVerifiedAt);
        Assert.Null(row.ConsentVerifiedAt);
        Assert.Null(token.Subject);
        Assert.Null(token.Authorization);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, token.Status);
        Assert.Equal(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, token.Type);
        Assert.Equal(0, cached.Authorizations);
        Assert.Equal(before.Sessions, cached.Sessions);
        using var pending = await test.SendAsync(resume);
        await ManagedHumanHttp.EntryAsync(pending);
        var after = await test.SnapshotAsync();
        Assert.Equal(row.ExpiresAt, Assert.Single(after.Interactions).ExpiresAt);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, Assert.Single(after.Tokens).Status);
        Assert.Equal(before.Sessions, after.Sessions);
        Assert.Equal(0, after.Authorizations);
    }
}
