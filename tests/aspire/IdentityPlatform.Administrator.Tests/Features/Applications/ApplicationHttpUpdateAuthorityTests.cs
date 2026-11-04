using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.Applications.ApplicationHttpUpdateEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Checks current per-action authority, native CSRF and exact persisted MFA/source deadlines through the actual mapped HTTPS endpoints.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationHttpUpdateAuthorityTests(BootstrapFixture fixture)
{
    private const string Write = "applications.write";
    private const string Credentials = "applications.credentials";
    private const string Foreign = "https://foreign.example.test";
    private const string InvalidProof = "invalid-native-csrf";
    private const int MfaSeconds = 300;

    /// <summary>Removing write still permits credential-only recovery; removing credential authority still permits ordinary editing.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentBearer_When_OneActionGrantIsRemoved_Then_OnlyTheOtherActionCanCommit(string provider, bool removeWrite)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var before = await SnapshotAsync(test, created.Application.Id);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var permission = removeWrite ? Write : Credentials;
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        foreach (var direct in new[] { false, true })
        {
            using var denied = await test.UpdateAsync(created.Application.Id, removeWrite ? Edit : Replacement(test.Now), create.Headers.ETag!.ToString(), replacement: !removeWrite, direct: direct);
            await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.Forbidden);
        }
        Assert.Equal(before.Digest, (await SnapshotAsync(test, created.Application.Id)).Digest);
        var body = removeWrite ? Replacement(test.Now) : Edit;
        using var allowed = await test.UpdateAsync(created.Application.Id, body, create.Headers.ETag!.ToString(), replacement: removeWrite);
        await RequireAsync(test, before, allowed, body, removeWrite, created.ClientSecret);
    }

    /// <summary>The original native intent deadline wins by MFA equality; a last-second genuine replacement succeeds before both actions require sign-in again.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OriginalNativeDeadline_When_MfaReachesEquality_Then_ApiAndBffRequireSignInWithoutMutation(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var before = await SnapshotAsync(test, created.Application.Id);
        await using var scope = test.Scope();
        var verified = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorSession>()
            .Where(row => row.PublicId == test.SourceSession).Select(row => row.FactorVerifiedAt).SingleAsync(TestContext.Current.CancellationToken);
        var nativeExpiry = await NativeDeadlineAsync(test);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(verified + MfaSeconds), nativeExpiry);
        var earliest = test.AccessExpiresAt < nativeExpiry ? test.AccessExpiresAt : nativeExpiry;
        test.Environment.Issuer.Account.Journey.Clock.Now = earliest.AddSeconds(-1);
        var body = Replacement(test.Now);
        using var accepted = await test.UpdateAsync(created.Application.Id, body, create.Headers.ETag!.ToString(), replacement: true);
        await RequireAsync(test, before, accepted, body, true, created.ClientSecret);
        var proof = await test.TokenAsync();
        var committed = await SnapshotAsync(test, created.Application.Id);
        test.Environment.Issuer.Account.Journey.Clock.Now = nativeExpiry;
        foreach (var direct in new[] { false, true })
        {
            foreach (var replacement in new[] { false, true })
            {
                using var denied = await test.UpdateAsync(created.Application.Id, replacement ? Replacement(test.Now) : Edit,
                    accepted.Headers.ETag!.ToString(), replacement, direct, csrf: proof);
                await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.Unauthorized);
            }
        }
        Assert.Equal(committed.Digest, (await SnapshotAsync(test, created.Application.Id)).Digest);
    }

    /// <summary>Both production browser actions require genuine current native proof and same-origin provenance before any downstream mutation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineBrowserSession_When_UpdateCsrfOrOriginIsInvalid_Then_AllAggregateFieldsRemainUnchanged(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var before = await SnapshotAsync(test, created.Application.Id);
        foreach (var replacement in new[] { false, true })
        {
            var body = replacement ? Replacement(test.Now) : Edit;
            using var missing = await test.UpdateAsync(created.Application.Id, body, create.Headers.ETag!.ToString(), replacement, authenticated: false);
            await ApplicationHttpTests.ProblemAsync(missing, HttpStatusCode.BadRequest);
            using var invalid = await test.UpdateAsync(created.Application.Id, body, create.Headers.ETag.ToString(), replacement, csrf: InvalidProof);
            await ApplicationHttpTests.ProblemAsync(invalid, HttpStatusCode.BadRequest);
            using var foreign = await test.UpdateAsync(created.Application.Id, body, create.Headers.ETag.ToString(), replacement, origin: Foreign);
            await ApplicationHttpTests.ProblemAsync(foreign, HttpStatusCode.BadRequest);
        }
        Assert.Equal(before.Digest, (await SnapshotAsync(test, created.Application.Id)).Digest);
    }

    /// <summary>Missing bearer and subsequent persisted issuer-session revocation cannot be repaired by retained bearer/cookie or a previously valid browser proof.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealRetainedCredentials_When_SourceSessionIsRevoked_Then_ApiAndBffUpdatesCannotCommit(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var before = await SnapshotAsync(test, created.Application.Id);
        foreach (var replacement in new[] { false, true })
        {
            using var anonymous = await test.UpdateAsync(created.Application.Id, replacement ? Replacement(test.Now) : Edit, create.Headers.ETag!.ToString(), replacement, direct: true, authenticated: false);
            await ApplicationHttpTests.ProblemAsync(anonymous, HttpStatusCode.Unauthorized);
        }
        var proof = await test.TokenAsync();
        await using var scope = test.Scope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorSession>()
            .Where(row => row.PublicId == test.SourceSession).ExecuteUpdateAsync(set => set.SetProperty(row => row.Revoked, true), TestContext.Current.CancellationToken));
        foreach (var replacement in new[] { false, true })
        {
            foreach (var direct in new[] { false, true })
            {
                using var revoked = await test.UpdateAsync(created.Application.Id, replacement ? Replacement(test.Now) : Edit, create.Headers.ETag!.ToString(), replacement, direct, csrf: proof);
                await ApplicationHttpTests.ProblemAsync(revoked, HttpStatusCode.Unauthorized);
            }
        }
        Assert.Equal(before.Digest, (await SnapshotAsync(test, created.Application.Id)).Digest);
    }
}
