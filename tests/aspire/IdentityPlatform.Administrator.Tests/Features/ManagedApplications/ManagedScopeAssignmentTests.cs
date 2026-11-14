using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves the deliberately staged assignment boundary against genuine cached requests, authorization codes and independent clients.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedScopeAssignmentTests(BootstrapFixture fixture)
{
    private const string OtherClient = "unassigned-scope-control";
    private const string InvalidToken = "invalid_token";
    private const string InvalidGrant = "invalid_grant";

    private const string ScopeCollection = "/api/v1/scopes";

    /// <summary>Additional native scopes permit fresh explicit OpenID consent; assignment changes never revive older artifacts or affect unrelated clients.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PendingRequestAndCode_When_ScopesAreAssignedThenCleared_Then_OnlyFreshUnassignedProfilesCanIssue(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var old = new ManagedTokenFlow(test);
        await old.ApproveAsync();
        var code = await old.CodeAsync();
        var cached = new ManagedTokenFlow(test);
        await cached.CacheAsync();
        using var otherResponse = await test.Http.CreateAsync(ApplicationHttpTests.Input(test.Http.Now, OtherClient));
        var other = new ManagedCacheTestContext(test.Http, await ApplicationHttpTests.CreatedAsync(otherResponse));
        using var scope = await test.Http.CatalogueAsync(HttpMethod.Post, ScopeCollection, ApplicationScopeHttpTests.Scope);
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        var proof = await test.Http.TokenAsync();
        using var assigned = await test.Http.CatalogueAsync(HttpMethod.Put, ApplicationScopeHttpTests.Path(test.Id), ApplicationScopeHttpTests.Assign,
            ApplicationVersion.Create(test.Id, 1), direct: false, csrf: proof);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var freshAssigned = new ManagedTokenFlow(test);
        await freshAssigned.CacheAsync();
        await RejectOldAsync(test, cached, old, code);
        await CompleteAsync(other);
        using var cleared = await test.Http.CatalogueAsync(HttpMethod.Put, ApplicationScopeHttpTests.Path(test.Id), ApplicationScopeHttpTests.Clear,
            assigned.Headers.ETag!.ToString(), direct: false, csrf: proof);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        await RejectOldAsync(test, cached, old, code);
        await CompleteAsync(test);
    }

    /// <summary>Checks unchanged persisted native/managed authority around rejection of each original request artifact.</summary>
    private static async Task RejectOldAsync(ManagedCacheTestContext test, ManagedTokenFlow cached, ManagedTokenFlow old, string code)
    {
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        using var request = await test.SendAsync(cached.Resume);
        await ManagedTokenEvidence.ErrorAsync(request, HttpStatusCode.BadRequest, InvalidToken, local: true);
        using var exchange = await old.ExchangeAsync(code);
        await ManagedTokenEvidence.ErrorAsync(exchange, HttpStatusCode.BadRequest, InvalidGrant);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
    }

    /// <summary>Runs fresh password, MFA and consent and verifies native token cryptography without minting administrator authority.</summary>
    private static async Task CompleteAsync(ManagedCacheTestContext test)
    {
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        await ManagedJourneyEvidence.ExchangeAsync(test, flow, await flow.CodeAsync(), denyManagement: true);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
    }
}
