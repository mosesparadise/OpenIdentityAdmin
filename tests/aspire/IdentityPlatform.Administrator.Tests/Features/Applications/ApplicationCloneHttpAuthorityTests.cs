using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires current write authority, originating session and original MFA/native deadlines at both genuine clone HTTP surfaces.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationCloneHttpAuthorityTests(BootstrapFixture fixture)
{
    private const string Permission = "permission";
    private const string Session = "session";
    private const string Expiry = "expiry";
    private const string Write = "applications.write";
    private const int MfaSeconds = 300;

    /// <summary>Retained bearer/cookie and real CSRF proof cannot replace current permission, a live source session or fresh native authority.</summary>
    [Theory]
    [InlineData("PostgreSql", Permission, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Session, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Expiry, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Permission, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Session, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Expiry, Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealCloneCredentials_When_CurrentAuthorityIsLost_Then_ApiAndBffDenyWithoutMutation(string provider, string loss)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var source = await ApplicationHttpTests.CreatedAsync(create);
        var proof = await test.TokenAsync();
        var before = await ApplicationHttpUpdateEvidence.SnapshotAsync(test, source.Application.Id);
        await LoseAuthorityAsync(test, loss);
        foreach (var direct in new[] { false, true })
        {
            using var denied = await test.CatalogueAsync(HttpMethod.Post, ApplicationCloneHttpTests.Path(source.Application.Id, direct),
                ApplicationCloneHttpTests.Input(test.Now), create.Headers.ETag!.ToString(), direct: direct, csrf: proof);
            await ApplicationHttpTests.ProblemAsync(denied, loss == Permission ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        }
        Assert.Equal(before.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test, source.Application.Id)).Digest);
        Assert.Equal((1, 1), await ApplicationHttpTests.CountsAsync(test));
    }

    /// <summary>Changes actual persisted authority or advances the controlled clock to the original native MFA equality boundary.</summary>
    private static async Task LoseAuthorityAsync(ApplicationHttpFixture test, string loss)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        if (loss == Permission)
        {
            Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Write).ExecuteDeleteAsync(token) > 0);
        }
        else if (loss == Session)
        {
            Assert.Equal(1, await database.Set<AdministratorSession>().Where(row => row.PublicId == test.SourceSession)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.Revoked, true), token));
        }
        else
        {
            var verified = await database.Set<AdministratorSession>().Where(row => row.PublicId == test.SourceSession)
                .Select(row => row.FactorVerifiedAt).SingleAsync(token);
            var deadline = await ApplicationHttpUpdateEvidence.NativeDeadlineAsync(test);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(verified + MfaSeconds), deadline);
            test.Environment.Issuer.Account.Journey.Clock.Now = deadline;
        }
    }
}
